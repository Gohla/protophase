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

        public void Register(ServiceInfo serviceInfo) {
            MemoryStream stream = new MemoryStream();
            stream.WriteByte((byte)RegistryMessageType.AddService);
            StreamUtil.Write<ServiceInfo>(stream, serviceInfo);

            _socket.Send(stream.GetBuffer());
            _socket.Recv();
        }
    }
}