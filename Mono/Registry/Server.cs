using System;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Protophase.Shared;
using ZMQ;

namespace Protophase.Registry {
    public enum MessageType {
        AddService,
        RemoveService,
        FindByGUID
    }

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
                        MessageType type = (MessageType)stream.ReadByte();

                        // Execute command
                        switch(type) {
                            case MessageType.AddService: {
                                BinaryFormatter formatter = new BinaryFormatter();
                                ServiceInfo serviceInfo = formatter.Deserialize(stream) as ServiceInfo;
                                AddService(serviceInfo);
                                socket.Send();
                                break;
                            }
                            case MessageType.RemoveService: {
                                BinaryFormatter formatter = new BinaryFormatter();
                                String guid = formatter.Deserialize(stream) as String;
                                RemoveService(guid);
                                socket.Send();
                                break;
                            }
                            case MessageType.FindByGUID: {
                                BinaryFormatter receiveFormatter = new BinaryFormatter();
                                String guid = receiveFormatter.Deserialize(stream) as String;
                                ServiceInfo serviceInfo = FindService(guid);

                                BinaryFormatter sendFormatter = new BinaryFormatter();
                                MemoryStream sendStream = new MemoryStream();
                                if(serviceInfo != null) {
                                    sendFormatter.Serialize(sendStream, 1);
                                    sendFormatter.Serialize(sendStream, serviceInfo);
                                } else {
                                    sendFormatter.Serialize(sendStream, 0);
                                }

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

