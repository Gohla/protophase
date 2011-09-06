using System;
using System.IO;
using Protophase.Shared;
using ZMQ;

namespace Protophase.Service {
    /**
    Delegate used for receiving published messages from a remote service.
    
    @param  obj The object that was published.
    **/
    public delegate void PublishedEvent(object obj);

    /**
    Generic remote service, used to receive published messages and send RPC requests to remote services.
    **/
    public class Service : IDisposable {
        private ServiceInfo _serviceInfo;
        private Context _context;
        private Socket _rpcSocket;
        private Socket _publishedSocket;

        private uint _publishedCounter = 0;
        private event PublishedEvent _published;

        /**
        Event that is called when a message is published for this service.
        **/
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

        /**
        Constructor.
        
        @param  serviceInfo Information describing the remote service.
        @param  context     The ZMQ context.
        **/
        public Service(ServiceInfo serviceInfo, Context context) {
            _serviceInfo = serviceInfo;
            _context = context;

            Connect();
        }

        /**
        Dispose of this object, cleaning up any resources it uses.
        **/
        public void Dispose() {
            _rpcSocket.Dispose();
            _publishedSocket.Dispose();
        }

        /**
        Connects to the RPC and publish/subscribe sockets of the remote service.
        **/
        private void Connect() {
            _rpcSocket = _context.Socket(SocketType.REQ);
            _rpcSocket.Connect("tcp://" + _serviceInfo.Address + ":" + _serviceInfo.RPCPort);
            _publishedSocket = _context.Socket(SocketType.SUB);
            _publishedSocket.Connect("tcp://" + _serviceInfo.Address + ":" + _serviceInfo.PublishPort);
        }

        /**
        Subscribes for published messages.
        **/
        private void Subscribe() {
            // Empty byte array to subscribe to all messages.
            _publishedSocket.Subscribe(new byte[]{});
        }

        /**
        Unsubscribes from published messages.
        **/
        private void Unsubscribe() {
            // Empty byte array to unsubscribe to all messages.
            _publishedSocket.Unsubscribe(new byte[]{});
        }

        /**
        Receives all published messages.
        **/
        public void Receive() {
            byte[] message = _publishedSocket.Recv(SendRecvOpt.NOBLOCK);
            if(message != null) {
                MemoryStream stream = StreamUtil.CreateStream(message);

                object obj = StreamUtil.Read<object>(stream);
                if(_published != null) _published(obj);
            }
        }

        /**
        Calls a method on the remote service (RPC).
        
        @param  name    The name of the method to call.
        @param  pars    A variable-length parameter list containing the parameters to pass to the method.
        
        @return The object returned by the remote method call, or null if the call times out. Note that the method
                can also return null.
        **/
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
            // TODO: Properly handle when the service doesn't respond (throw exception, fix socket state?)
            byte[] message = _rpcSocket.Recv(2000);
            if(message == null) return null;
            MemoryStream receiveStream = StreamUtil.CreateStream(message);
            return StreamUtil.ReadWithNullCheck<object>(receiveStream);
        }

        /**
        Calls a method on the remote service (RPC) and tries to convert the return value.
        **/
        public T Call<T>(String name, params object[] pars) {
            return (T)Call(name, pars);
        }

        /**
        Convert this object into a string representation.
        
        @return A string representation of this object.
        **/
        public override String ToString() {
            return _serviceInfo.ToString();
        }
    }
}