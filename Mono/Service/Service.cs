using System;
using System.IO;
using Protophase.Shared;
using ZMQ;

namespace Protophase.Service {
    public class Service {
        private ServiceInfo _serviceInfo;
        private Context _context;
        private Socket _socket;

        public Service(ServiceInfo serviceInfo, Context context) {
            _serviceInfo = serviceInfo;
            _context = context;
            Connect();
        }

        private void Connect() {
            _socket = _context.Socket(SocketType.REQ);
            _socket.Connect(_serviceInfo.Address);
        }

        public void Call(String name) {
            // Serialize to binary
            MemoryStream stream = new MemoryStream();
            // Write UID method name
            // TODO: Validate method name
            StreamUtil.Write<String>(stream, _serviceInfo.UID);
            StreamUtil.Write<String>(stream, name);

            // Send to object and await response.
            _socket.Send(stream.GetBuffer());
            // TODO: Method return value
            // TODO: Make timeout configurable
            _socket.Recv(1000);
        }

        public override String ToString() {
            return _serviceInfo.ToString();
        }
    }
}