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
            BinaryFormatter sendFormatter = new BinaryFormatter();
            MemoryStream sendStream = new MemoryStream();
            sendFormatter.Serialize(sendStream, serviceInfo);

            _socket.Send(sendStream.GetBuffer());
            _socket.Recv();
        }
    }
}

