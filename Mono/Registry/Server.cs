using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Protophase.Shared;
using ZMQ;

namespace Protophase.Registry {
    /**
    Registry server that keeps a registry of existing objects.
    **/
    public class Server {
        private String _address;
        private Dictionary<String, ServiceInfo> _servicesByUID = new Dictionary<String, ServiceInfo>();
        private Dictionary<String, Dictionary<String, ServiceInfo>> _servicesByType =
            new Dictionary<String, Dictionary<String, ServiceInfo>>();

        /**
        Constructor.
        
        @param  address The address the registry should listen on in ZMQ socket style (e.g. tcp://*:5555)
        **/
        public Server(String address) {
            _address = address;
        }

        /**
        Starts the main loop that receives and responds to messages. Does not return.
        **/
        public void Start() {
            using(Context context = new Context(1)) {
                using(Socket socket = context.Socket(SocketType.REP)) {
                    socket.Bind(_address);
                    
                    while(true) {
                        // Wait for next message from a client.
                        MemoryStream stream = StreamUtil.CreateStream(socket.Recv());

                        // Read message type
                        RegistryMessageType type = (RegistryMessageType)stream.ReadByte();

                        // Execute command
                        switch(type) {
                            case RegistryMessageType.RegisterService: {
                                ServiceInfo serviceInfo = StreamUtil.Read<ServiceInfo>(stream);

                                MemoryStream sendStream = new MemoryStream();
                                StreamUtil.WriteBool(sendStream, Register(serviceInfo));
                                socket.Send(sendStream.GetBuffer());

                                break;
                            }
                            case RegistryMessageType.UnregisterService: {
                                String uid = StreamUtil.Read<String>(stream);

                                MemoryStream sendStream = new MemoryStream();
                                StreamUtil.WriteBool(sendStream, Unregister(uid));
                                socket.Send(sendStream.GetBuffer());

                                break;
                            }
                            case RegistryMessageType.FindByUID: {
                                String uid = StreamUtil.Read<String>(stream);
                                ServiceInfo serviceInfo = FindByUID(uid);

                                MemoryStream sendStream = new MemoryStream();
                                StreamUtil.WriteWithNullCheck<ServiceInfo>(sendStream, serviceInfo);
                                socket.Send(sendStream.GetBuffer());

                                break;
                            }
                        }

                        // Sleep to not hog CPU.
                        // TODO: Use a thread with yielding?
                        Thread.Sleep(33);
                    }
                }
            }
        }

        /**
        Register a service.
        
        @param  serviceInfo Information describing the service to register
        
        @return True if service is successfully registered, false if a service with given UID already exists.
        **/
        private bool Register(ServiceInfo serviceInfo) {
            if(_servicesByUID.ContainsKey(serviceInfo.UID)) return false;

            _servicesByUID.Add(serviceInfo.UID, serviceInfo);

            Dictionary<String, ServiceInfo> dict;
            if(_servicesByType.TryGetValue(serviceInfo.Type, out dict)) {
                dict.Add(serviceInfo.UID, serviceInfo);
            } else {
                dict = new Dictionary<String, ServiceInfo> ();
                dict.Add(serviceInfo.UID, serviceInfo);
                _servicesByType.Add(serviceInfo.Type, dict);
            }

            Console.WriteLine("Added service: " + serviceInfo);

            return true;
        }

        /**
        Unregisters a service
        
        @param  uid The UID of the service to unregister.
        
        @return True if service is successfully unregistered, false if service with given UID does not exists.
        **/
        private bool Unregister(String uid) {
            ServiceInfo serviceInfo;
            if(_servicesByUID.TryGetValue(uid, out serviceInfo)) {
                _servicesByUID.Remove(uid);

                Dictionary<String, ServiceInfo> dict;
                if(_servicesByType.TryGetValue(serviceInfo.Type, out dict)) {
                    dict.Remove(uid);
                }

                Console.WriteLine("Removed service: " + serviceInfo);

                return true;
            }

            return false;
        }

        /**
        Searches for a service by UID.
        
        @param  uid The UID of the service to find.
        
        @return The service with given UID, or null if it was not found.
        **/
        private ServiceInfo FindByUID(String uid) {
            ServiceInfo serviceInfo;
            if(_servicesByUID.TryGetValue(uid, out serviceInfo)) {
                return serviceInfo;
            }

            return null;
        }
    }
}

