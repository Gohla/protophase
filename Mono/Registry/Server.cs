using System;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using Protophase.Shared;
using ZMQ;

namespace Protophase.Registry {
    public class Server {
        private String _address;
        private Dictionary<String, ServiceInfo> _servicesByUID = new Dictionary<String, ServiceInfo>();
        private Dictionary<String, Dictionary<String, ServiceInfo>> _servicesByType =
            new Dictionary<String, Dictionary<String, ServiceInfo>>();

        public Server(String address) {
            _address = address;
        }

        public void Start() {
            using(Context context = new Context(1)) {
                using(Socket socket = context.Socket(SocketType.REP)) {
                    socket.Bind(_address);
                    
                    while(true) {
                        // Wait for next message from a client.
                        byte[] message = socket.Recv();
                        MemoryStream stream = new MemoryStream(message);

                        // Get the message type.
                        RegistryMessageType type = (RegistryMessageType)stream.ReadByte();

                        // Execute command
                        switch(type) {
                            case RegistryMessageType.AddService: {
                                ServiceInfo serviceInfo = StreamUtil.Read<ServiceInfo>(stream);
                                AddService(serviceInfo);

                                socket.Send();

                                break;
                            }
                            case RegistryMessageType.RemoveService: {
                                String guid = StreamUtil.Read<String>(stream);
                                RemoveService(guid);

                                socket.Send();

                                break;
                            }
                            case RegistryMessageType.FindByGUID: {
                                String guid = StreamUtil.Read<String>(stream);
                                ServiceInfo serviceInfo = FindService(guid);

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

        private void AddService(ServiceInfo serviceInfo) {
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
        }

        private void RemoveService(String guid) {
            ServiceInfo serviceInfo;
            if(_servicesByUID.TryGetValue(guid, out serviceInfo)) {
                _servicesByUID.Remove(guid);

                Dictionary<String, ServiceInfo> dict;
                if(_servicesByType.TryGetValue(serviceInfo.Type, out dict)) {
                    dict.Remove(guid);
                }
            }

            Console.WriteLine("Removed service: " + serviceInfo);
        }

        private ServiceInfo FindService(String guid) {
            ServiceInfo serviceInfo;
            if(_servicesByUID.TryGetValue(guid, out serviceInfo)) {
                return serviceInfo;
            }

            return null;
        }
    }
}

