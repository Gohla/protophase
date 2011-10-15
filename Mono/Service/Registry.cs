using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Protophase.Shared;
using ZMQ;
using System.Threading;

namespace Protophase.Service {
    /**
    Registry client that handles all communication with a registry server.
    **/
    public class Registry : IDisposable {
        private Context _context = new Context(1);
        private Dictionary<String, object> _objects = new Dictionary<String, object>();
        private Dictionary<object, String> _objectsReverse = new Dictionary<object, String>();
        private bool _stopAutoUpdate = false;

        private String _registryAddress;
        private ushort _registryRPCPort;
        private ushort _registryPublishPort;
        private Socket _registryRPCSocket;
        private Socket _registryPublishSocket;

        private ulong _applicationID;
        private ulong _uidPrefix = 0;
        private String _remoteAddress;

        private Dictionary<String, Socket> _rpcSockets = new Dictionary<String, Socket>();
        private Dictionary<String, Socket> _publishSockets = new Dictionary<String, Socket>();

        private static readonly ushort INITIALPORT = 1024;
        private static readonly String UID_GENERATOR_PREFIX = "__";

        /**
        Delegate for the idle event.
        **/
        public delegate void IdleEvent();

        /**
        Event that is called after each update loop when in auto update mode.
        **/
        public event IdleEvent Idle;

        /**
        Default constructor. Connects to localhost registry with default ports.
        **/
        public Registry() : this("localhost") { }

        /**
        Simple constructor. Connects to registry on given address with default ports.
        
        @param  registryAddress The (remote) address of the registry server in (e.g. 127.0.13.37)
        **/
        public Registry(String registryAddress) : this(registryAddress, 5555, 5556, "localhost") { }

        /**
        Constructor.
        
        @param  registryAddress The (remote) address of the registry server in (e.g. 11.33.33.77)
        @param  rpcPort         The registry server RPC port.
        @param  publishPort     The registry server publish port.
        @param  remoteAddress   The remote address that is reported to the registry.
        **/
        public Registry(String registryAddress, ushort rpcPort, ushort publishPort, String remoteAddress) {
            _registryAddress = registryAddress;
            _registryRPCPort = rpcPort;
            _registryPublishPort = publishPort;
            _remoteAddress = remoteAddress;
            
            ConnectRegistryRPC();
            ConnectRegistryPublish(); // TODO: This could be delayed until a service object is retrieved.
            RequestApplicationID();
        }

        /**
        Finaliser.
        **/
        ~Registry() {
            Dispose();
        }

        /**
        Dispose of this object, unregisters all services and cleans up any resources it uses.
        **/
        public void Dispose() {
            UnregisterAll();
            // Dispose all service objects before disposing of the context.
            for(int i = Service._serviceObjects.Count - 1; i >= 0; --i)
                Service._serviceObjects[i].Dispose();
            _registryRPCSocket.Dispose();
            _registryPublishSocket.Dispose();
            _context.Dispose();
            GC.SuppressFinalize(this);
        }

        /**
        Connects to the registry server RPC socket.
        **/
        private void ConnectRegistryRPC() {
            if(_registryRPCSocket != null) return;
            _registryRPCSocket = _context.Socket(SocketType.REQ);
            _registryRPCSocket.Connect(Transport.TCP, _registryAddress, _registryRPCPort);
        }

        /**
        Connects to the registry server publish socket and subscribes for published messages.
        **/
        private void ConnectRegistryPublish() {
            if(_registryPublishSocket != null) return;
            _registryPublishSocket = _context.Socket(SocketType.SUB);
            _registryPublishSocket.Connect(Transport.TCP, _registryAddress, _registryPublishPort);
            _registryPublishSocket.Subscribe(new byte[0]);
        }

        /**
        Register this application with the registry to receive an application ID.
        **/
        private void RequestApplicationID() {
            // Serialize to binary
            MemoryStream stream = new MemoryStream();
            // Write message type
            stream.WriteByte((byte)RegistryMessageType.RegisterApplication);

            // Send to registry and await results.
            _registryRPCSocket.Send(stream.GetBuffer());
            byte[] message = _registryRPCSocket.Recv();
            MemoryStream receiveStream = new MemoryStream(message);
            _applicationID = StreamUtil.Read<ulong>(receiveStream);
        }

        /**
        Starts listening for RPC requests for the service with given UID.
        
        @param  uid The UID of the service.
        
        @return The port that is used for listening, or 0 if already listening.
        **/
        private ushort BindRPC(String uid) {
            if(!_rpcSockets.ContainsKey(uid)) {
                Socket socket = _context.Socket(SocketType.REP);

                ushort port = AvailablePort.Find(INITIALPORT);
                bool bound = false;

                // Retry binding socket until it succeeds. TODO: This could infinite loop..
                while(!bound) {
                    try {
                        socket.Bind(Transport.TCP, "*", port);
                    }
                    catch (ZMQ.Exception) {
                        port = AvailablePort.Find(INITIALPORT);
                    }

                    bound = true;
                }

                _rpcSockets.Add(uid, socket);
                return port;
            }

            return 0;
        }

        /**
        Starts listening for publish/subscribe requests for the service with given UID. If already listening for that
        service this function will do nothing.
        
        @param  uid The UID of the service.
        
        @return The port that is used for listening, or 0 if already listening.
        **/
        private ushort BindPublish(String uid) {
            if(!_publishSockets.ContainsKey(uid)) {
                Socket socket = _context.Socket(SocketType.PUB);

                ushort port = AvailablePort.Find(INITIALPORT);
                bool bound = false;

                // Retry binding socket until it succeeds. TODO: This could infinite loop..
                while(!bound) {
                    try {
                        socket.Bind(Transport.TCP, "*", port);
                    } catch(ZMQ.Exception) {
                        port = AvailablePort.Find(INITIALPORT);
                    }

                    bound = true;
                }

                _publishSockets.Add(uid, socket);
                return port;
            }

            return 0;
        }

        /**
        Receive published messages from the registry.
        **/
        private void ReceiveRegistryPublish() {
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
            if(Service._serviceObjectsByType.TryGetValue(serviceInfo.Type, out services)) {
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
            if(Service._serviceObjectsByType.TryGetValue(serviceInfo.Type, out services)) {
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
                            // TODO: Handle incorrect method name.
                            Type type = obj.GetType();
                            ret = type.GetMethod(name).Invoke(obj, pars);
                        } else {
                            // TODO: Handle incorrect UID
                        }

                        // Send return value (or null if no value was returned)
                        MemoryStream sendStream = new MemoryStream();
                        StreamUtil.WriteWithNullCheck(sendStream, ret);
                        socket.Send(sendStream.GetBuffer());
                    } catch(System.Exception e) {
                        Console.WriteLine("RPC failed:" + e.Message + "\n" + e.StackTrace);

                        // Send back empty reply so that the client doesn't time out.
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
            // Serialize to binary
            MemoryStream stream = new MemoryStream();
            // Write message type
            stream.WriteByte((byte)RegistryMessageType.Pulse);
            // Write service info
            StreamUtil.Write(stream, _applicationID);

            // Send to registry and await results.
            _registryRPCSocket.Send(stream.GetBuffer());
            _registryRPCSocket.Recv();
        }

        /**
        Sends and receives all network messages.
        **/
        public void Update() {
            Receive();
            foreach(Service service in Service._serviceObjects) service.Receive();
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
        @param  nullableUID The UID of the service.
        @param  obj         The service object.
        
        @return True if service is successfully registered, false if a service with given UID already exists or if the 
                UID is reserved.
        **/
        public bool Register<T>(String uid, T obj) {
            if(uid.StartsWith(UID_GENERATOR_PREFIX)) return false;

            Type type = typeof(T);

            // Bind RPC and publish sockets.
            ushort rpcPort = BindRPC(uid);
            ushort publishPort = BindPublish(uid);

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
                    PublishedEvent pubDelegate = (object pubObj) => Publish(uid, pubObj);
                    evt.AddEventHandler(obj, pubDelegate);
                }
            }

            ServiceInfo serviceInfo = new ServiceInfo(uid, serviceType, serviceVersion, _remoteAddress, rpcPort,
                publishPort, rpcMethods);

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

                // Update own object dictionaries.
                _objectsReverse.Remove(_objects[uid]);
                _objects.Remove(uid);

                // TODO: Unsubscribe from publish events.

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
        UID.
        
        @param  uid The UID of the service to get.
        
        @return The remote service object with given UID, or null if it was not found.
        **/
        public Service GetServiceByUID(String uid) {
            ServiceInfo serviceInfo = FindByUID(uid);
            if(serviceInfo == null) return null;

            return new Service(serviceInfo, _context, false);
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

            return new Service(servicesInfo, _context, true);
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