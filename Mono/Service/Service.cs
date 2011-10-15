using System;
using System.Diagnostics;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Protophase.Shared;
using ZMQ;
using Exception = System.Exception;


namespace Protophase.Service {
    /**
    Generic remote service, used to receive published messages and send RPC requests to remote services.
    **/
    public class Service : IDisposable {
        private List<ServiceInfo> _servicesInfo = new List<ServiceInfo>();
        private String _serviceType;
        private Context _context;
        private Socket _rpcSocket;
        private Socket _publishedSocket;

        private uint _publishedCounter = 0;
        private event PublishedEvent _published;

        static internal List<Service> _serviceObjects = new List<Service>();
        static private MultiValueDictionary<String, Service> _serviceObjectsByType =
            new MultiValueDictionary<String, Service>();

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
        
        @param  serviceInfo Information describing a remote service.
        @param  context     The ZMQ context.
        **/
        public Service(ServiceInfo serviceInfo, Context context)
            : this(new ServiceInfo[] { serviceInfo }, context) { }

        /**
        Constructor.
        
        @param  servicesInfo Information describing a remote services.
        @param  context      The ZMQ context.
        **/
        public Service(ServiceInfo[] servicesInfo, Context context) {
            _servicesInfo.InsertRange(0, servicesInfo);
            _serviceType = servicesInfo[0].Type; // TODO: Check if all services have the same type?
            _context = context;

            Initialize();
            ConnectAll();

            _serviceObjects.Add(this);
            _serviceObjectsByType.Add(_serviceType, this);
        }

        /**
        Finaliser.
        **/
        ~Service() {
            Dispose();
        }

        /**
        Dispose of this object, cleaning up any resources it uses.
        **/
        public void Dispose() {
            _serviceObjects.Remove(this);
            _serviceObjectsByType.Remove(_serviceType, this);

            _rpcSocket.Dispose();
            _publishedSocket.Dispose();

            GC.SuppressFinalize(this);
        }

        /**
        Initializes sockets.
        **/
        private void Initialize() {
            _rpcSocket = _context.Socket(SocketType.REQ);
            _publishedSocket = _context.Socket(SocketType.SUB);
        }

        /**
        Connects to the RPC and publish/subscribe sockets of the given remote service.
        
        @param  serviceInfo The service info.
        **/
        private void Connect(ServiceInfo serviceInfo) {
            _rpcSocket.Connect("tcp://" + serviceInfo.Address + ":" + serviceInfo.RPCPort);
            _publishedSocket.Connect("tcp://" + serviceInfo.Address + ":" + serviceInfo.PublishPort);
        }

        /**
        Connects to the RPC and publish/subscribe sockets of all services.
        **/
        private void ConnectAll() {
            foreach(ServiceInfo serviceInfo in _servicesInfo) Connect(serviceInfo);
        }

        /**
        Subscribes for published messages.
        **/
        private void Subscribe() {
            // Empty byte array to subscribe to all messages.
            _publishedSocket.Subscribe(new byte[] { });
        }

        /**
        Unsubscribes from published messages.
        **/
        private void Unsubscribe() {
            // Empty byte array to unsubscribe to all messages.
            _publishedSocket.Unsubscribe(new byte[] { });
        }

        /**
        Receives all published messages.
        **/
        internal void Receive() {
            byte[] message = _publishedSocket.Recv(SendRecvOpt.NOBLOCK);
            while(message != null) {
                MemoryStream stream = StreamUtil.CreateStream(message);

                object obj = StreamUtil.Read<object>(stream);
                if(_published != null) _published(obj);

                // Try to get more messages.
                message = _publishedSocket.Recv(SendRecvOpt.NOBLOCK);
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
            return "[" + String.Join(", ", _servicesInfo.Select(s => s.ToString()).ToArray()) + "]";
        }
    }
}