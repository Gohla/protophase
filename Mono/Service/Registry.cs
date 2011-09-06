using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Protophase.Shared;
using ZMQ;

namespace Protophase.Service {
    /**
    Registry client that handles all communication with a registry server.
    **/
    public class Registry : IDisposable {
        private Context _context = new Context(1);
        private Dictionary<String, object> _objects = new Dictionary<String, object>();

        private String _registryAddress;
        private Socket _registrySocket;

        private String _remoteAddress;

        private ushort _incomingRPCPort;
        private Socket _incomingRPCSocket;

        private Dictionary<String, Socket> _publishSockets = new Dictionary<String, Socket>();
        private static ushort _nextPublishPort = 1338;

        /**
        Simple constructor.
        
        @param  registryAddress The (remote) address of the registry server in ZMQ socket style (e.g.
                                tcp://localhost:5555)
        **/
        public Registry(String registryAddress) {
            _registryAddress = registryAddress;
            _remoteAddress = "localhost";

            _incomingRPCPort = 1337;

            ConnectRegistry();
        }

        /**
        Constructor.
        
        @param  registryAddress The (remote) address of the registry server in ZMQ socket style (e.g.
                                tcp://localhost:5555)
        @param  remoteAddress   The remote address that is reported to the registry.
        @param  bindRPCPort     The port that is used for listening to RPC requests.
        @param  bindPublishPort The port that is used for listening to publish/subscribe requests.
        **/
        public Registry(String registryAddress, String remoteAddress, ushort bindRPCPort, ushort bindPublishPort) {
            _registryAddress = registryAddress;
            _remoteAddress = remoteAddress;

            _incomingRPCPort = bindRPCPort;

            ConnectRegistry();
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
            if(_incomingRPCSocket != null) _incomingRPCSocket.Dispose();
            _registrySocket.Dispose();
            _context.Dispose();

            GC.SuppressFinalize(this);
        }

        /**
        Connects to the registry server.
        **/
        private void ConnectRegistry() {
            if(_registrySocket != null) return;
            _registrySocket = _context.Socket(SocketType.REQ);
            _registrySocket.Connect(_registryAddress);
        }

        /**
        Starts listening for RPC requests.
        **/
        private void BindRPC() {
            if(_incomingRPCSocket != null) return;
            _incomingRPCPort = AvailablePort.Find(_incomingRPCPort);
            _incomingRPCSocket = _context.Socket(SocketType.REP);
            _incomingRPCSocket.Bind("tcp://*:" + _incomingRPCPort);
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
                _nextPublishPort = AvailablePort.Find(_nextPublishPort);
                socket.Bind("tcp://*:" + _nextPublishPort);

                _publishSockets.Add(uid, socket);

                return _nextPublishPort++;
            }

            return 0;
        }

        /**
        Receives RPC requests for all services.
        **/
        public void Receive() {
            if(_incomingRPCSocket == null) return;

            byte[] message = _incomingRPCSocket.Recv(SendRecvOpt.NOBLOCK);
            if(message != null) {
                MemoryStream stream = StreamUtil.CreateStream(message);

                try {
                    // Read UID, method name and parameters.
                    String uid = StreamUtil.Read<String>(stream);
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
                    _incomingRPCSocket.Send(sendStream.GetBuffer());
                } catch(System.Exception e) {
                    Console.WriteLine("RPC failed:" + e.Message + "\n" + e.StackTrace);

                    // Send back empty reply so that the client doesn't time out.
                    _incomingRPCSocket.Send();
                }
            }
        }

        /**
        Registers given service with the registry server with a unique name.
        
        @tparam T   Type of the service.
        @param  uid The UID of the service.
        @param  obj The service object.
        
        @return True if service is successfully registered, false if a service with given UID already exists.
        **/
        public bool Register<T>(String uid, T obj) {
            Type type = typeof(T);

            // Bind RPC and publish sockets.
            BindRPC();
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

            ServiceInfo serviceInfo = new ServiceInfo(uid, serviceType, serviceVersion, _remoteAddress,
                                                      _incomingRPCPort, publishPort, rpcMethods);

            // Serialize to binary
            MemoryStream stream = new MemoryStream();
            // Write message type
            stream.WriteByte((byte)RegistryMessageType.RegisterService);
            // Write service info
            StreamUtil.Write<ServiceInfo>(stream, serviceInfo);

            // Send to registry and await results.
            _registrySocket.Send(stream.GetBuffer());
            MemoryStream receiveStream = StreamUtil.CreateStream(_registrySocket.Recv());

            // Update own object dictionary.
            if(StreamUtil.ReadBool(receiveStream)) {
                _objects.Add(uid, obj);
                return true;
            }

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
            _registrySocket.Send(stream.GetBuffer());
            MemoryStream receiveStream = StreamUtil.CreateStream(_registrySocket.Recv());

            if(StreamUtil.ReadBool(receiveStream)) {
                // Dispose of publish/subscribe socket.
                Socket socket;
                if(_publishSockets.TryGetValue(uid, out socket)) {
                    socket.Dispose();
                    _publishSockets.Remove(uid);
                }

                // Update own object dictionary.
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
        
        @return The service with given UID, or null if it was not found.
        **/
        public ServiceInfo FindByUID(String uid) {
            // Serialize to binary
            MemoryStream stream = new MemoryStream();
            // Write message type
            stream.WriteByte((byte)RegistryMessageType.FindByUID);
            // Write UID
            StreamUtil.Write<String>(stream, uid);

            // Send to registry and await results.
            _registrySocket.Send(stream.GetBuffer());
            byte[] message = _registrySocket.Recv();
            MemoryStream receiveStream = new MemoryStream(message);
            return StreamUtil.ReadWithNullCheck<ServiceInfo>(receiveStream);
        }

        /**
        Gets a remote service object by UID.
        
        @param  uid The UID of the service to get.
        
        @return The remote service object with given UID, or null if it was not found.
        **/
        public Service GetService(String uid) {
            ServiceInfo serviceInfo = FindByUID(uid);
            if(serviceInfo == null) return null;

            return new Service(serviceInfo, _context);
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