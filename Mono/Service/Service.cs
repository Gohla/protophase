using System;
using System.IO;
using Protophase.Shared;
using ZMQ;

namespace Protophase.Service {
    public delegate void PublishedEvent(object obj);

    public class Service {
        private ServiceInfo _serviceInfo;
        private Context _context;
        private Socket _rpcSocket;
        private Socket _publishedSocket;

        private uint _publishedCounter = 0;
        private event PublishedEvent _published;

        public event PublishedEvent Published {
            add {
                _published += value;
                if(++_publishedCounter == 1) Subscribe();
            }
            remove {
                _published -= value;
                if(--_publishedCounter == 0) Unsubscribe();
            }
        }

        public Service(ServiceInfo serviceInfo, Context context) {
            _serviceInfo = serviceInfo;
            _context = context;

            Connect();
        }

        private void Connect() {
            _rpcSocket = _context.Socket(SocketType.REQ);
            _rpcSocket.Connect("tcp://" + _serviceInfo.Address + ":" + _serviceInfo.RPCPort);
            _publishedSocket = _context.Socket(SocketType.SUB);
            _publishedSocket.Connect("tcp://" + _serviceInfo.Address + ":" + _serviceInfo.PublishPort);
        }

        private void Subscribe() {
            // Empty byte array to subscribe to all messages.
            _publishedSocket.Subscribe(new byte[]{});
        }

        private void Unsubscribe() {
            // Empty byte array to unsubscribe to all messages.
            _publishedSocket.Unsubscribe(new byte[]{});
        }

        public void Receive() {
            byte[] message = _publishedSocket.Recv(SendRecvOpt.NOBLOCK);
            if(message != null) {
                MemoryStream stream = StreamUtil.CreateStream(message);

                object obj = StreamUtil.Read<object>(stream);
                if(_published != null) _published(obj);
            }
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
            _rpcSocket.Send(stream.GetBuffer());
            // Receive return value
            // TODO: Make timeout configurable
            byte[] message = _rpcSocket.Recv(2000);
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