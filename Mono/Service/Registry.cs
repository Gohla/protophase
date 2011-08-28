using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Protophase.Shared;
using ZMQ;

namespace Protophase.Service {
    public class Registry {
        private String _registryAddress;
        private Socket _registrySocket;
        private String _listenRPCAddress;
        private String _remoteRPCAddress;
        private Socket _localRPCSocket;
        private Context _context = new Context(1);
        private Dictionary<String, object> _objects = new Dictionary<String, object>();

        public Registry(String registryAddress) {
            _registryAddress = registryAddress;
            int port = AvailablePort.Find(6666);
            _remoteRPCAddress = "tcp://localhost:" + port;
            _listenRPCAddress = "tcp://*:" + port;
            Connect();
        }

        public Registry(String registryAddress, String listenRPCAddress, String remoteRPCAddress) {
            _registryAddress = registryAddress;
            _listenRPCAddress = listenRPCAddress;
            _remoteRPCAddress = remoteRPCAddress;
            Connect();
        }

        private void Connect() {
            _registrySocket = _context.Socket(SocketType.REQ);
            _registrySocket.Connect(_registryAddress);
            _localRPCSocket = _context.Socket(SocketType.REP);
            _localRPCSocket.Bind(_listenRPCAddress);
        }

        public void Receive() {
            byte[] message = _localRPCSocket.Recv(SendRecvOpt.NOBLOCK);
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
                    _localRPCSocket.Send(sendStream.GetBuffer());
                } catch(System.Exception e) {
                    Console.WriteLine("RPC failed:" + e.Message + "\n" + e.StackTrace);
                    _localRPCSocket.Send();
                }
            }
        }

        public bool Register<T>(String uid, T obj) {
            Type type = typeof(T);

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

            ServiceInfo serviceInfo = new ServiceInfo(uid, serviceType, serviceVersion, _remoteRPCAddress, rpcMethods);

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

            // Update own object dictionary.
            if(StreamUtil.ReadBool(receiveStream)) {
                _objects.Remove(uid);
                return true;
            }

            return false;
        }

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

        public Service GetService(String uid) {
            ServiceInfo serviceInfo = FindByUID(uid);
            if(serviceInfo == null) return null;

            return new Service(serviceInfo, _context);
        }
    }
}