using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Protophase.Shared;
using ZMQ;
using System.Threading;
using System.Linq;
using Exception = System.Exception;

namespace Protophase.Service {
    /**
    Registry client that handles all communication with a registry server.
    **/
    public class Registry : IDisposable {
        private Context _context;
        private Dictionary<String, object> _objects = new Dictionary<String, object>();
        private Dictionary<object, String> _objectsReverse = new Dictionary<object, String>();
        private MultiValueDictionary<String, Tuple<PublishedDelegate, EventInfo>> _publishedDelegates =
            new MultiValueDictionary<String, Tuple<PublishedDelegate, EventInfo>>();
        private bool _stopAutoUpdate = false;

        private List<AlternateRegistryServer> _serverAlternatives = new List<AlternateRegistryServer>();
        private long _serverID;

        private Address _registryRPCAddress;
        private Address _registryPublishAddress;

        private Socket _registryRPCSocket;
        private Socket _registryPublishSocket;

        private String _remoteAddress;
        private Transport _hostTransport;

        private ulong _applicationID;
        private ulong _uidPrefix = 0;
        private float _pulseTimestep;
        private DateTime _lastPulse = DateTime.MinValue;

        private Dictionary<String, Socket> _rpcSockets = new Dictionary<String, Socket>();
        private Dictionary<String, Socket> _publishSockets = new Dictionary<String, Socket>();

        private List<Service> _serviceObjects = new List<Service>();
        private MultiValueDictionary<String, Service> _serviceObjectsByType =
            new MultiValueDictionary<String, Service>();

        private static readonly String UID_GENERATOR_PREFIX = "__";
        private static readonly float REGISTRY_TIMEOUT_DIVIDER = 3.0f;

        /**
        Delegate for the idle event.
        **/
        public delegate void IdleEvent();

        /**
        Event that is called after each update loop when in auto update mode.
        **/
        public event IdleEvent Idle;

        /**
        Gets the ZMQ context.
        **/
        public Context Context { get { return _context; } }

        /**
        Default constructor. Connects to localhost registry with default ports using the TCP transport. Services are
        hosted using the TCP transport using localhost as remote address.
        **/
        public Registry() : this("localhost") { }

        /**
        Simple constructor. Connects to registry on given address with default ports using the TCP transport.
        Services are hosted using the TCP transport using localhost as remote address.
        
        @param  registryAddress The (remote) address of the registry server in (e.g. 127.0.13.37)
        **/
        public Registry(String registryAddress) : this(new Address(Transport.TCP, registryAddress, 5555),
            new Address(Transport.TCP, registryAddress, 5556), "localhost") { }

        /**
        Constructor.
        
        @exception  Exception   Thrown when RPC and publish addresses have different transports.
        
        @param  rpcAddress      The address of the registry's RPC socket.
        @param  publishAddress  The address of the registry's publish socket.
        @param  remoteAddress   The remote address that is reported to the registry. Only used in TCP, PGM and EPGM
                                host transports.
        **/
        public Registry(Address rpcAddress, Address publishAddress, String remoteAddress) {
            if(rpcAddress.Transport != publishAddress.Transport)
                throw new Exception("RPC and publish registry addresses cannot have different transports.");

            _context = SharedContext.Get();

            _registryRPCAddress = rpcAddress;
            _registryPublishAddress = publishAddress;
            _remoteAddress = remoteAddress;
            _hostTransport = rpcAddress.Transport;

            ConnectRegistryRPC();
            ConnectRegistryPublish();
            RegisterApplication();
        }

        /**
        Finaliser.
        **/
        ~Registry() {
            Dispose(true);
        }

        /**
        Dispose of this object, unregisters all services and cleans up any resources it uses.
        **/
        public void Dispose() {
            Dispose(false);
        }

        /**
        Dispose of this object, unregisters all services and cleans up any resources it uses.
        **/
        protected virtual void Dispose(bool finalized) {
            // Unregister all service objects.
            if(!finalized) UnregisterAll();

            // Dispose all service objects before disposing of the context.
            for(int i = _serviceObjects.Count - 1; i >= 0; --i)
                _serviceObjects[i].Dispose();

            if(!finalized) {
                // Dispose context and sockets.
                _registryRPCSocket.Dispose();
                if(_registryPublishSocket != null) _registryPublishSocket.Dispose();

                GC.SuppressFinalize(this);
            }

            SharedContext.Dispose();
        }

        /**
        Adds a service to the service lists. Should only be called from the Service class.
        **/
        internal void AddService(Service service, String type) {
            _serviceObjects.Add(service);
            _serviceObjectsByType.Add(type, service);
        }

        /**
        Removes a service from the service lists. Should only be called from the Service class.
        **/
        internal void RemoveService(Service service, String type) {
            _serviceObjects.Remove(service);
            _serviceObjectsByType.Remove(type, service);
        }

        /**
        Connects to the registry server RPC socket.
        **/
        private void ConnectRegistryRPC() {
            if(_registryRPCSocket != null) return;
            _registryRPCSocket = _context.Socket(SocketType.REQ);
            _registryRPCSocket.Connect(_registryRPCAddress);
        }

        /**
        Connects to the registry server publish socket and subscribes for published messages.
        **/
        private void ConnectRegistryPublish() {
            if(_registryPublishSocket != null) return;
            _registryPublishSocket = _context.Socket(SocketType.SUB);
            _registryPublishSocket.Connect(_registryPublishAddress);
            _registryPublishSocket.Subscribe(new byte[0]);
        }

        /**
        Register this application with the registry.
        **/
        private void RegisterApplication() {
            // Serialize to binary
            MemoryStream stream = new MemoryStream();
            // Write message type
            stream.WriteByte((byte)RegistryMessageType.RegisterApplication);
            // Send to registry and await results.
            _registryRPCSocket.Send(stream.GetBuffer());
            byte[] message = _registryRPCSocket.Recv();
            MemoryStream receiveStream = new MemoryStream(message);
            _applicationID = StreamUtil.Read<ulong>(receiveStream);
            _pulseTimestep = StreamUtil.Read<float>(receiveStream) / REGISTRY_TIMEOUT_DIVIDER;
            _serverAlternatives = StreamUtil.Read<List<AlternateRegistryServer>>(receiveStream);
            _serverID = StreamUtil.Read<long>(receiveStream);
        }

        /**
        Starts listening for RPC requests for the service with given UID.
        
        @param  uid         The UID of the service.
        
        @exception  Exception   Thrown when no available port could be found for TCP, PGM or EPGM transports.
        
        @return The address that is used for listening, or null if already listening.
        **/
        private Address BindRPC(String uid) {
            if(!_rpcSockets.ContainsKey(uid)) {
                Socket socket = _context.Socket(SocketType.REP);
                Address address;

                switch(_hostTransport) {
                    case Transport.TCP:
                    case Transport.PGM:
                    case Transport.EPGM:
                        ushort port = socket.BindAvailableTCPPort(_hostTransport, "*");
                        if(port == 0) 
                            throw new System.Exception("Could not find an available port to bind on.");
                        address = new Address(_hostTransport, _remoteAddress, port);
                        break;
                    case Transport.INPROC:
                        address = new Address(_hostTransport, uid + "/RPC");
                        socket.Bind(address);
                        break;
                    default:
                        throw new Exception("Unsupported transport: " + Enum.GetName(typeof(Transport), _hostTransport));
                }
                
                
                _rpcSockets.Add(uid, socket);
                return address;
            }

            return null;
        }

        /**
        Starts listening for publish/subscribe requests for the service with given UID. If already listening for that
        service this function will do nothing.
        
        @param  uid         The UID of the service.
        
        @exception  Exception   Thrown when no available port could be found for TCP, PGM or EPGM transports.
        
        @return The address that is used for listening, or null if already listening.
        **/
        private Address BindPublish(String uid) {
            if(!_publishSockets.ContainsKey(uid)) {
                Socket socket = _context.Socket(SocketType.REP);
                Address address;

                switch(_hostTransport) {
                    case Transport.TCP:
                    case Transport.PGM:
                    case Transport.EPGM:
                        ushort port = socket.BindAvailableTCPPort(_hostTransport, "*");
                        if(port == 0) 
                            throw new System.Exception("Could not find an available port to bind on.");
                        address = new Address(_hostTransport, _remoteAddress, port);
                        break;
                    case Transport.INPROC:
                        address = new Address(_hostTransport, uid + "/Publish");
                        socket.Bind(address);
                        break;
                    default:
                        throw new Exception("Unsupported transport: " + Enum.GetName(typeof(Transport), _hostTransport));
                }

                _publishSockets.Add(uid, socket);
                return address;
            }

            return null;
        }

        /**
        Receive published messages from the registry.
        **/
        private void ReceiveRegistryPublish() {
            if(_registryPublishSocket == null) return;

            byte[] message = _registryPublishSocket.Recv(SendRecvOpt.NOBLOCK);
            while(message != null) {
                MemoryStream stream = StreamUtil.CreateStream(message);

                // Read publish type
                RegistryPublishType publishType = (RegistryPublishType)stream.ReadByte();
                switch(publishType) {
                    case RegistryPublishType.ServiceRegistered: {
                        ServiceRegistered(StreamUtil.Read<ServiceInfo>(stream));
                        break;
                    }
                    case RegistryPublishType.ServiceUnregistered: {
                        ServiceUnregistered(StreamUtil.Read<ServiceInfo>(stream));
                        break;
                    }
                    case RegistryPublishType.AlternateRegistryAvailable:
                        AlternateRegistryServer alt = StreamUtil.Read<AlternateRegistryServer>(stream);
                        if (!_serverAlternatives.Where(x=>(x.ServerID == alt.ServerID)).Any())
                            _serverAlternatives.Add(alt);
                        break;
                    case RegistryPublishType.AlternateRegistryUnavailable:
                        long serverId = StreamUtil.Read<long>(stream);
                        AlternateRegistryServer remove = null;
                        if (_serverAlternatives.Where(x => (x.ServerID == serverId)).Any())
                            remove = _serverAlternatives.Where(x => (x.ServerID == serverId)).Single();
                        if (remove != null)
                            _serverAlternatives.Remove(remove);
                        break;
                }
                // Try to get more messages.
                message = _registryPublishSocket.Recv(SendRecvOpt.NOBLOCK);
            }
        }

        /**
        Called when a service is registered.
        
        @param  serviceInfo Information describing the registered service.
        **/
        private void ServiceRegistered(ServiceInfo serviceInfo) {
            List<Service> services;
            if(_serviceObjectsByType.TryGetValue(serviceInfo.Type, out services)) {
                foreach(Service service in services)
                    service.AddService(serviceInfo);
            }
        }

        /**
        Called when a service is unregistered.
        
        @param  serviceInfo Information describing the registered service.
        **/
        private void ServiceUnregistered(ServiceInfo serviceInfo) {
            List<Service> services;
            if(_serviceObjectsByType.TryGetValue(serviceInfo.Type, out services)) {
                foreach(Service service in services)
                    service.RemoveService(serviceInfo);
            }
        }

        /**
        Receive messages for all registered objects.
        **/
        private void ReceiveRegistered() {
            foreach(KeyValuePair<String, Socket> pair in _rpcSockets) {
                Socket socket = pair.Value;
                byte[] message = socket.Recv(SendRecvOpt.NOBLOCK);
                while(message != null) {
                    String uid = pair.Key;
                    MemoryStream stream = StreamUtil.CreateStream(message);

                    try {
                        // Read UID, method name and parameters.
                        String name = StreamUtil.Read<String>(stream);
                        object[] pars = StreamUtil.Read<object[]>(stream);

                        // Call method on object.
                        object obj;
                        object ret = null;
                        if(_objects.TryGetValue(uid, out obj)) {
                            MethodInfo methodInfo = obj.GetType().GetMethod(name);

                            // Check if method may be RPC called.
                            if(methodInfo.GetCustomAttributes(typeof(RPC), true).Length == 0) {
                                Console.WriteLine("RPC failed: Method " + name + " is not marked as being RPC callable.");
                                socket.Send();
                            } else {
                                // Call the method with given parameters.
                                ret = methodInfo.Invoke(obj, pars);

                                // Send return value (or null if no value was returned)
                                MemoryStream sendStream = new MemoryStream();
                                StreamUtil.WriteWithNullCheck(sendStream, ret);
                                socket.Send(sendStream.GetBuffer());
                            }

                        } else {
                            Console.WriteLine("RPC failed: Object with UID " + uid + " is not registered as a service.");
                            socket.Send();
                        }
                    } catch(System.Exception e) {
                        Console.WriteLine("RPC failed: " + e.Message);
                        socket.Send();
                    }

                    // Try to get more messages.
                    message = socket.Recv(SendRecvOpt.NOBLOCK);
                }
            }
        }

        /**
        Receives all network messages.
        **/
        private void Receive() {
            ReceiveRegistryPublish();
            ReceiveRegistered();
        }

        /**
        Send a Pulse message so the registry knows we're alive.
        **/
        private void SendPulse() {
            if (_lastPulse.AddSeconds(_pulseTimestep) < DateTime.Now) {
                // Serialize to binary
                MemoryStream stream = new MemoryStream();
                // Write message type
                stream.WriteByte((byte)RegistryMessageType.Pulse);
                // Write service info
                StreamUtil.Write(stream, _applicationID);

                // Send to registry and await results.
                try
                {
                    _registryRPCSocket.Send(stream.GetBuffer());
                    byte[] message = _registryRPCSocket.Recv(1000);
                    //Receive bool, if true -> all ok, if false will receive new appID
                    //bool ack = Stream

                }
                catch (ZMQ.Exception e)
                {
                    Console.WriteLine("Error in registry connection: " + e.Errno + ": " + e.Message);
                    if (e.Errno == 156384763)
                    {
                        if (_serverAlternatives.Count <= 1)
                            throw new Exception("Connection with registry failed without any alternitives present.");
                        else
                        {
                            AlternateRegistryServer alt =
                                _serverAlternatives.Where(x => (x.ServerID != _serverID)).OrderBy(x => x.ServerID).Last();
                            _serverID = alt.ServerID;
                            _registryRPCAddress = alt.ServerRPCAddress;
                            _registryPublishAddress = alt.ServerPubAddress;
                            _registryRPCSocket = _context.Socket(SocketType.REQ);
                            _registryRPCSocket.Connect(_registryRPCAddress);
                            _registryPublishSocket = _context.Socket(SocketType.SUB);
                            _registryPublishSocket.Connect(_registryPublishAddress);
                            _registryPublishSocket.Subscribe(new byte[0]);
                            Console.WriteLine("Failing registry detected. Transitioned to alternate registry server. New server id: " + _serverID);
                        }

                    }
                }
                _lastPulse = DateTime.Now;
            }
        }

        /**
        Sends and receives all network messages.
        **/
        public void Update() {
            Receive();
            foreach(Service service in _serviceObjects) service.Receive();
            SendPulse();
        }

        /**
        Enters an infinite loop that automatically calls Update. After each update the Idle event is triggered. Use
        StopAutoUpdate to break out of this loop.
        **/
        public void AutoUpdate() { AutoUpdate(0); }

        /**
        Enters an infinite loop that automatically calls Update. After each update the Idle event is triggered. Use
        StopAutoUpdate to break out of this loop.
        
        @param  millisecondsSleep   The number of milliseconds to sleep between updates.
        **/
        public void AutoUpdate(int millisecondsSleep) {
            while(!_stopAutoUpdate) {
                Update();
                if(Idle != null) Idle();
                Thread.Sleep(millisecondsSleep);
            }

            _stopAutoUpdate = false;
        }

        /**
        Stop the auto update loop.
        **/
        public void StopAutoUpdate() {
            _stopAutoUpdate = true;
        }

        /**
        Registers given service with the registry server with a generated name.
        
        @tparam T   Type of the service.
        @param  obj         The service object.
        
        @return True if service is successfully registered, false if a service with given UID already exists.
        **/
        public bool Register<T>(T obj) {
            return Register(_applicationID + UID_GENERATOR_PREFIX + _uidPrefix++, obj);
        }

        /**
        Registers given service with the registry server with a unique name.
        
        @tparam T   Type of the service.
        @param  uid The UID of the service.
        @param  obj The service object.
        
        @return True if service is successfully registered, false if a service with given UID already exists or if the 
                UID is reserved.
        **/
        public bool Register<T>(String uid, T obj) {
            if(uid.StartsWith(UID_GENERATOR_PREFIX)) return false;

            Type type = typeof(T);

            // Bind RPC and publish sockets.
            Address rpcAddress;
            Address publishAddress;
            try {
                rpcAddress = BindRPC(uid);
                publishAddress = BindPublish(uid);

                if(rpcAddress == null || publishAddress == null) {
                    Console.WriteLine("Could not register object: Failed to bind socket.");
                    return false;
                }
            } catch (System.Exception e) {
                Console.WriteLine("Could not register object: " + e.Message);
                return false;
            }

            // Get service type and version.
            ServiceType[] serviceTypes = type.GetCustomAttributes(typeof(ServiceType), true) as ServiceType[];
            ServiceVersion[] serviceVersions =
                type.GetCustomAttributes(typeof(ServiceVersion), true) as ServiceVersion[];
            String serviceType = serviceTypes.Length == 1 ? serviceTypes[0].Type : "Generic";
            String serviceVersion = serviceVersions.Length == 1 ? serviceVersions[0].Version : "0.1";

            // Find all methods that can be RPC called.
            MethodInfo[] methods = type.GetMethods();
            List<String> rpcMethods = new List<String>();
            foreach(MethodInfo method in methods) {
                if(method.GetCustomAttributes(typeof(RPC), true).Length > 0) {
                    rpcMethods.Add(method.Name);
                }
            }

            // Subscribe to publisher events.
            EventInfo[] events = type.GetEvents();
            foreach(EventInfo evt in events) {
                if(evt.GetCustomAttributes(typeof(Publisher), true).Length > 0) {
                    PublishedDelegate pubDelegate = (object pubObj) => Publish(uid, pubObj);
                    evt.AddEventHandler(obj, pubDelegate);
                    _publishedDelegates.Add(uid, Tuple.Create(pubDelegate, evt));
                }
            }

            ServiceInfo serviceInfo = new ServiceInfo(uid, serviceType, serviceVersion, rpcAddress, publishAddress, 
                rpcMethods);

            // Serialize to binary
            MemoryStream stream = new MemoryStream();
            // Write message type
            stream.WriteByte((byte)RegistryMessageType.RegisterService);
            // Write application id
            StreamUtil.Write(stream, _applicationID);
            // Write service info
            StreamUtil.Write(stream, serviceInfo);

            // Send to registry and await results.
            _registryRPCSocket.Send(stream.GetBuffer());
            MemoryStream receiveStream = StreamUtil.CreateStream(_registryRPCSocket.Recv());

            // Update own object dictionaries.
            if(StreamUtil.ReadBool(receiveStream)) {
                _objects.Add(uid, obj);
                _objectsReverse.Add(obj, uid);
                return true;
            }

            return false;
        }

        /**
        Unregisters given service object.
        
        @param  obj The object to unregister.
        
        @return True if service is successfully unregistered, false if service with given UID does not exists or if
                given object is not a service.
        **/
        public bool Unregister(object obj) {
            String uid;
            if(_objectsReverse.TryGetValue(obj, out uid))
                return Unregister(uid);

            return false;
        }

        /**
        Unregisters service with given UID.
        
        @param  uid The UID of the service.
        
        @return True if service is successfully unregistered, false if service with given UID does not exists.
        **/
        public bool Unregister(String uid) {
            // Serialize to binary
            MemoryStream stream = new MemoryStream();
            // Write message type
            stream.WriteByte((byte)RegistryMessageType.UnregisterService);
            // Write UID
            StreamUtil.Write<String>(stream, uid);

            // Send to registry and await results.
            _registryRPCSocket.Send(stream.GetBuffer());
            MemoryStream receiveStream = StreamUtil.CreateStream(_registryRPCSocket.Recv());

            if(StreamUtil.ReadBool(receiveStream)) {
                // Dispose of RPC socket.
                Socket socket;
                if(_rpcSockets.TryGetValue(uid, out socket)) {
                    socket.Dispose();
                    _rpcSockets.Remove(uid);
                }

                // Dispose of publish/subscribe socket.              
                if(_publishSockets.TryGetValue(uid, out socket)) {
                    socket.Dispose();
                    _publishSockets.Remove(uid);
                }

                object obj = _objects[uid];

                // Unsubscribe from publish events.
                List<Tuple<PublishedDelegate, EventInfo>> publishedDelegates;
                if(_publishedDelegates.TryGetValue(uid, out publishedDelegates)) {
                    foreach(Tuple<PublishedDelegate, EventInfo> tuple in publishedDelegates) {
                        tuple.Item2.RemoveEventHandler(obj, tuple.Item1);
                    }
                }
                _publishedDelegates.Remove(uid);

                // Update own object dictionaries.
                _objectsReverse.Remove(obj);
                _objects.Remove(uid);

                return true;
            }

            return false;
        }

        /**
        Unregisters all services.
        **/
        public void UnregisterAll() {
            List<String> uids = new List<String>(_objects.Keys);
            foreach(String uid in uids) Unregister(uid);
        }

        /**
        Searches for a service by UID.
        
        @param  uid The UID of the service to find.
        
        @return The service info with given UID, or null if it was not found.
        **/
        public ServiceInfo FindByUID(String uid) {
            // Serialize to binary
            MemoryStream stream = new MemoryStream();
            // Write message type
            stream.WriteByte((byte)RegistryMessageType.FindByUID);
            // Write UID
            StreamUtil.Write<String>(stream, uid);

            // Send to registry and await results.
            _registryRPCSocket.Send(stream.GetBuffer());
            byte[] message = _registryRPCSocket.Recv();
            MemoryStream receiveStream = new MemoryStream(message);
            return StreamUtil.ReadWithNullCheck<ServiceInfo>(receiveStream);
        }

        /**
        Searches for services by type.
        
        @param  type The type of the services to find.
        
        @return The services info with given type, or null if no services were found.
        **/
        public ServiceInfo[] FindByType(String type) {
            // Serialize to binary
            MemoryStream stream = new MemoryStream();
            // Write message type
            stream.WriteByte((byte)RegistryMessageType.FindByType);
            // Write UID
            StreamUtil.Write<String>(stream, type);

            // Send to registry and await results.
            _registryRPCSocket.Send(stream.GetBuffer());
            byte[] message = _registryRPCSocket.Recv();
            MemoryStream receiveStream = new MemoryStream(message);
            return StreamUtil.ReadWithNullCheck<ServiceInfo[]>(receiveStream);
        }

        /**
        Gets a remote service object by UID. This connects to only one service, the service described by the given
        UID. When this service is added or removed from the registry, the returned Service object will be updated
        automatically.
        
        @param  uid The UID of the service to get.
        
        @return The remote service object with given UID, or null if it was not found.
        **/
        public Service GetServiceByUID(String uid) {
            ServiceInfo serviceInfo = FindByUID(uid);
            if(serviceInfo == null) return null;
            return new Service(serviceInfo, this);
        }

        /**
        Gets a remote service object by type. This connects to all services with given type and automatically load
        balances RPC calls and receives published messages from all services. When new services with the same type
        are added or removed from the registry, the returned Service object will be updated automatically.
        
        @param  type The type of the services to find.
        
        @return The remote service object with given type, or null if no services were found.
        **/
        public Service GetServiceByType(String type) {
            ServiceInfo[] servicesInfo = FindByType(type);
            if(servicesInfo == null) return null;
            return new Service(servicesInfo, this);
        }


        /**
        Publishes an object for the service with given UID.
        
        @param  uid The UID of the service to publish for.
        @param  obj The object to send as a message.
        **/
        public void Publish(String uid, object obj) {
            Socket socket;
            if(_publishSockets.TryGetValue(uid, out socket)) {
                // Serialize to binary
                MemoryStream stream = new MemoryStream();
                // Write object
                StreamUtil.Write(stream, obj);

                // Publish data
                socket.Send(stream.GetBuffer());
            }
        }
    }
}