using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Protophase.Shared;
using ZMQ;

namespace Protophase.Service {
    public class Registry {
        private String _address;
        private Context _context = new Context(1);
        private Socket _socket;

        public Registry(String address) {
            _address = address;
            _socket = _context.Socket(SocketType.REQ);
            _socket.Connect(_address);
        }

        public bool Register(ServiceInfo serviceInfo) {
            MemoryStream stream = new MemoryStream();
            stream.WriteByte((byte)RegistryMessageType.RegisterService);
            StreamUtil.Write<ServiceInfo>(stream, serviceInfo);

            _socket.Send(stream.GetBuffer());
            MemoryStream receiveStream = StreamUtil.CreateStream(_socket.Recv());
            return StreamUtil.ReadBool(receiveStream);
        }

        public bool Unregister(String uid) {
            MemoryStream stream = new MemoryStream();
            stream.WriteByte((byte)RegistryMessageType.UnregisterService);
            StreamUtil.Write<String>(stream, uid);

            _socket.Send(stream.GetBuffer());
            MemoryStream receiveStream = StreamUtil.CreateStream(_socket.Recv());
            return StreamUtil.ReadBool(receiveStream);
        }

        public ServiceInfo FindByUID(String uid) {
            MemoryStream stream = new MemoryStream();
            stream.WriteByte((byte)RegistryMessageType.FindByUID);
            StreamUtil.Write<String>(stream, uid);

            _socket.Send(stream.GetBuffer());
            byte[] message = _socket.Recv();
            MemoryStream receiveStream = new MemoryStream(message);
            return StreamUtil.ReadWithNullCheck<ServiceInfo>(receiveStream);
        }
    }
}