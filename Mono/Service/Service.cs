using System;
using System.Diagnostics;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Protophase.Shared;
using ZMQ;

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
        private bool _canUpdateServices;

        static internal List<Service> _serviceObjects = new List<Service>();
        static internal MultiValueDictionary<String, Service> _serviceObjectsByType =
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
        Construct from one service info.
        
        @param  serviceInfo         Information describing a remote service.
        @param  context             The ZMQ context.
        @param  canUpdateServices   Set to true if the services may be updated.
        **/
        public Service(ServiceInfo serviceInfo, Context context, bool canUpdateServices)
            : this(new ServiceInfo[] { serviceInfo }, context, canUpdateServices) { }

        /**
        Construct from multiple service info.
        
        @param  servicesInfo        Information describing remote services.
        @param  context             The ZMQ context.
        @param  canUpdateServices   Set to true if the services may be updated.
        **/
        public Service(ServiceInfo[] servicesInfo, Context context, bool canUpdateServices) {
            _servicesInfo.InsertRange(0, servicesInfo);
            _serviceType = servicesInfo[0].Type; // TODO: Check if all services have the same type?
            _context = context;
            _canUpdateServices = canUpdateServices;

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
            _rpcSocket.Connect(Transport.TCP, serviceInfo.Address, serviceInfo.RPCPort);
            _publishedSocket.Connect(Transport.TCP, serviceInfo.Address, serviceInfo.PublishPort);
        }

        /**
        Connects to the RPC and publish/subscribe sockets of all services.
        **/
        private void ConnectAll() {
            foreach(ServiceInfo serviceInfo in _servicesInfo) 
                Connect(serviceInfo);
        }

        /**
        Recreate the sockets so that the removed services are not connected anymore. Workaround because ZMQ sockets
        do not have a disconnect function.
        **/
        private void RecreateSockets() {
            // TODO: Call Receive first to clear the message queue?
            // TODO: Make sure that no RPC calls or incomming published messages get lost.
            
            _rpcSocket.Dispose();
            _publishedSocket.Dispose();

            Initialize();
            ConnectAll();

            if(_publishedCounter >= 1) 
                Subscribe();
        }


        /**
        Subscribes for published messages.
        **/
        private void Subscribe() {
            // Empty byte array to subscribe to all messages.
            _publishedSocket.Subscribe(new byte[0]);
        }

        /**
        Unsubscribes from published messages.
        **/
        private void Unsubscribe() {
            // Empty byte array to unsubscribe to all messages.
            _publishedSocket.Unsubscribe(new byte[0]);
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
        
        @exception  Exception   Thrown when RPC call fails due to a failed service or no services being available.
        
        @return The object returned by the remote method call, or null if the call times out. Note that the method can 
                also return null.
        **/
        public object Call(String name, params object[] pars) {
            // Serialize to binary
            MemoryStream stream = new MemoryStream();
            // Write UID method name
            // TODO: Validate method name
            StreamUtil.Write(stream, name);
            StreamUtil.Write(stream, pars);

            try
            {
                // Send to object and await response.
                _rpcSocket.Send(stream.GetBuffer(), SendRecvOpt.NOBLOCK);

                // Receive return value
                // TODO: Make timeout configurable
                byte[] message = _rpcSocket.Recv(2000);
                if(message == null)
                    throw new System.Exception("RPC call failed");
                MemoryStream receiveStream = StreamUtil.CreateStream(message);
                return StreamUtil.ReadWithNullCheck<object>(receiveStream);
            }
            catch (ZMQ.Exception e)
            {
                throw new System.Exception(e.Message);
            }
        }

        /**
        Calls a method on the remote service (RPC) and tries to convert the return value.
        
        @param  name    The name of the method to call.
        @param  pars    A variable-length parameter list containing the parameters to pass to the method.
        
        @exception  Exception   Thrown when RPC call fails due to a failed service or no services being available.
        
        @return The object returned by the remote method call, or null if the call times out. Note that the method can 
                also return null.
        **/
        public T Call<T>(String name, params object[] pars) {
            return (T)Call(name, pars);
        }

        /**
        Adds a service of the same type.
        
        @param  serviceInfo Information describing the service.
        
        @return True if it succeeds, false if given service info is not of the same type, is already added or services 
                may not be updated.
        **/
        public bool AddService(ServiceInfo serviceInfo) {
             // TODO: Contains in a List is slow, use a HashSet?
            if(serviceInfo.Type != _serviceType || _servicesInfo.Contains(serviceInfo) || !_canUpdateServices) return false;

            _servicesInfo.Add(serviceInfo);
            Connect(serviceInfo);

            return true;
        }

        /**
        Removes a service.
        
        @param  serviceInfo Information describing the service.
        
        @return True if it succeeds, false if given service was not found or if services may not be updated.
        **/
        public bool RemoveService(ServiceInfo serviceInfo) {
            if(!_canUpdateServices) return false;

            // TODO: Remove in a List is slow, use a HashSet?
            if(_servicesInfo.Remove(serviceInfo)) {
                // TODO: Create a timer here so that multiple calls in a row only recreate sockets once.
                RecreateSockets();
                return true;
            }

            return false;
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