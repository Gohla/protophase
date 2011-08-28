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

        public object Call(String name, params object[] pars) {
            // Serialize to binary
            MemoryStream stream = new MemoryStream();
            // Write UID method name
            // TODO: Validate method name
            StreamUtil.Write(stream, _serviceInfo.UID);
            StreamUtil.Write(stream, name);
            StreamUtil.Write(stream, pars);

            // Send to object and await response.
            _socket.Send(stream.GetBuffer());
            // Receive return value
            // TODO: Make timeout configurable
            byte[] message = _socket.Recv(1000);
            if(message == null) return null;
            MemoryStream receiveStream = StreamUtil.CreateStream(message);
            return StreamUtil.ReadWithNullCheck<object>(receiveStream);
        }

        public T Call<T>(String name, params object[] pars) {
            return (T)Call(name, pars);
        }

        public override String ToString() {
            return _serviceInfo.ToString();
        }
    }
}