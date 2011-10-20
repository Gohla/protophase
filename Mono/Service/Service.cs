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
        private HashSet<ServiceInfo> _servicesInfo = new HashSet<ServiceInfo>();
        private String _serviceType;
        private Registry _registry;
        private Socket _rpcSocket;
        private Socket _publishedSocket;
        
        private uint _publishedCounter = 0;
        private event PublishedDelegate _published;
        private bool _canUpdateServices;

        static internal List<Service> _serviceObjects = new List<Service>();
        static internal MultiValueDictionary<String, Service> _serviceObjectsByType =
            new MultiValueDictionary<String, Service>();

        /**
        Event that is called when a message is published for this service.
        **/
        public event PublishedDelegate Published {
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
        @param  registry            The registry this service belongs to.
        @param  canUpdateServices   Set to true if the services may be updated.
        **/
        public Service(ServiceInfo serviceInfo, Registry registry, bool canUpdateServices)
            : this(new ServiceInfo[] { serviceInfo }, registry, canUpdateServices) { }

        /**
        Construct from multiple service info.
        
        @exception  Exception   When not all services are of the same type.
        
        @param  servicesInfo        Information describing remote services. Services must be of the same type.
        @param  registry            The registry this service belongs to.
        @param  canUpdateServices   Set to true if the services may be updated.
        **/
        public Service(ServiceInfo[] servicesInfo, Registry registry, bool canUpdateServices)
        {
            if(servicesInfo.Length != 0) {
                _serviceType = servicesInfo[0].Type;
                foreach(ServiceInfo serviceInfo in servicesInfo)
                {
                    if(serviceInfo.Type != _serviceType)
                        throw new System.Exception("All services must be of the same type.");

                    _servicesInfo.Add(serviceInfo);
                }
            }
            _registry = registry;
            _canUpdateServices = canUpdateServices;

            Initialize();
            ConnectAll();

            _registry.AddService(this, _serviceType);
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
            _registry.RemoveService(this, _serviceType);

            _rpcSocket.Dispose();
            _publishedSocket.Dispose();

            GC.SuppressFinalize(this);
        }

        /**
        Initializes sockets.
        **/
        private void Initialize() {
            _rpcSocket = _registry.Context.Socket(SocketType.REQ);
            _publishedSocket = _registry.Context.Socket(SocketType.SUB);
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
        Calls a method on the remote service (RPC). If no message is returned after 5 seconds the call times out.
        
        @param  name    The name of the method to call.
        @param  pars    A variable-length parameter list containing the parameters to pass to the method.
        
        @exception  Exception   Thrown when RPC call fails due to a failed service or no services being available.
        @exception  Exception   Thrown when given method name is not RPC callable.
        
        @return The object returned by the remote method call, or null if the call times out. Note that the method can 
                also return null.
        **/
        public object Call(String name, params object[] pars) {
            return Call(name, 5000, pars);
        }

        /**
        Calls a method on the remote service (RPC).
        
        @param  name    The name of the method to call.
        @param  timeout How long to wait for the RPC call to return. A timeout of 0 or lower will block until the
                        message has arrived.
        @param  pars    A variable-length parameter list containing the parameters to pass to the method.
        
        @exception  Exception   Thrown when RPC call fails due to a failed service or no services being available.
        @exception  Exception   Thrown when given method name is not RPC callable.
        
        @return The object returned by the remote method call, or null if the call times out. Note that the method can 
                also return null.
        **/
        public object Call(String name, int timeout, params object[] pars) {
            // Validate method name.
            foreach(ServiceInfo serviceInfo in _servicesInfo) {
                if(!serviceInfo.RPCMethods.Contains(name))
                    throw new System.Exception("One or more services does not have a method named " + name + " that can be RPC called");
            }

            // Serialize to binary
            MemoryStream stream = new MemoryStream();
            // Write UID method name
            StreamUtil.Write(stream, name);
            StreamUtil.Write(stream, pars);

            try
            {
                // Send to object and await response.
                _rpcSocket.Send(stream.GetBuffer(), SendRecvOpt.NOBLOCK);

                // Receive return value
                byte[] message;
                if(timeout <= 0)
                    message = _rpcSocket.Recv();
                else
                    message = _rpcSocket.Recv(timeout);

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
        @param  timeout How long to wait for the RPC call to return. A timeout of 0 or lower will block until the
                        message has arrived.
        @param  pars    A variable-length parameter list containing the parameters to pass to the method.
        
        @exception  Exception   Thrown when RPC call fails due to a failed service or no services being available.
        @exception  Exception   Thrown when given method name is not RPC callable.
        
        @return The object returned by the remote method call, or null if the call times out. Note that the method can 
                also return null.
        **/
        public T Call<T>(String name, int timeout, params object[] pars) {
            return (T)Call(name, timeout, pars);
        }

        /**
        Calls a method on the remote service (RPC) and tries to convert the return value. If no message is returned 
        after 5 seconds the call times out.
        
        @param  name    The name of the method to call.
        @param  pars    A variable-length parameter list containing the parameters to pass to the method.
        
        @exception  Exception   Thrown when RPC call fails due to a failed service or no services being available.
        @exception  Exception   Thrown when given method name is not RPC callable.
        
        @return The object returned by the remote method call, or null if the call times out. Note that the method can 
                also return null.
        **/
        public T Call<T>(String name, params object[] pars)
        {
            return (T)Call(name, pars);
        }

        /**
        Adds a service of the same type.
        
        @param  serviceInfo Information describing the service.
        
        @return True if it succeeds, false if given service info is not of the same type, is already added or services 
                may not be updated.
        **/
        public bool AddService(ServiceInfo serviceInfo) {
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

            if(_servicesInfo.Remove(serviceInfo)) {
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