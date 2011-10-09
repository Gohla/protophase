using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Protophase.Shared;
using System.Linq;
using ZMQ;

namespace Protophase.Registry {
    /**
    Delegate for the idle event in Server.
    **/
    public delegate void IdleEvent();

    /**
    Registry server that keeps a registry of existing objects.
    **/
    public class Server : IDisposable {
        private Context _context = new Context(1);
        private bool _stopAutoUpdate = false;

        private String _rpcAddress;
        private Socket _rpcSocket;

        private String _publishAddress;
        private Socket _publishSocket;

        private Dictionary<String, ServiceInfo> _servicesByUID = new Dictionary<String, ServiceInfo>();
        private Dictionary<String, Dictionary<String, ServiceInfo>> _servicesByType =
            new Dictionary<String, Dictionary<String, ServiceInfo>>();

        private Dictionary<string, ServiceUidHolder> _servicesPerApplication = new Dictionary<string, ServiceUidHolder>();

        /**
        Event that is called after each update loop when in auto update mode.
        **/
        public event IdleEvent Idle;

        /**
        Constructor.
        
        @param  rpcAddress      The address the registry should listen on for RPC commands in ZMQ socket style (e.g.
                                tcp://*:5555)
        @param  publishAddress  The address the registry should listen on for publishing service changes in ZMQ
                                socket style (e.g. tcp://*:5555)
        **/
        public Server(String rpcAddress, String publishAddress) {
            _rpcAddress = rpcAddress;
            _publishAddress = publishAddress;

            BindRPC();
            BindPublish();
        }

        /**
        Finaliser.
        **/
        ~Server() {
            Dispose();
        }

        /**
        Dispose of this object, unregisters all services and cleans up any resources it uses.
        **/
        public void Dispose() {
            UnregisterAll();

            _rpcSocket.Dispose();
            _publishSocket.Dispose();
            _context.Dispose();

            GC.SuppressFinalize(this);
        }

        /**
        Starts listening for RPC commands.
        **/
        private void BindRPC() {
            _rpcSocket = _context.Socket(SocketType.REP);
            _rpcSocket.Bind(_rpcAddress);
        }

        /**
        Starts listening for publish/subscribe requests for service changes.
        **/
        private void BindPublish() {
            _publishSocket = _context.Socket(SocketType.PUB);
            _publishSocket.Bind(_publishAddress);
        }

        /**
        Receives commands from clients.
        **/
        public void Update() {
            // Get next messages from clients.
            byte[] message = _rpcSocket.Recv(SendRecvOpt.NOBLOCK);
            while(message != null) {
                MemoryStream stream = StreamUtil.CreateStream(message);

                // Read message type
                RegistryMessageType messageType = (RegistryMessageType)stream.ReadByte();

                // Execute command
                switch(messageType) {
                    case RegistryMessageType.RegisterService: {
                        ServiceInfo serviceInfo = StreamUtil.Read<ServiceInfo>(stream);

                        MemoryStream sendStream = new MemoryStream();
                        StreamUtil.WriteBool(sendStream, Register(serviceInfo));
                        _rpcSocket.Send(sendStream.GetBuffer());

                        break;
                    }
                    case RegistryMessageType.UnregisterService: {
                        String uid = StreamUtil.Read<String>(stream);

                        MemoryStream sendStream = new MemoryStream();
                        StreamUtil.WriteBool(sendStream, Unregister(uid));
                        _rpcSocket.Send(sendStream.GetBuffer());

                        break;
                    }
                    case RegistryMessageType.FindByUID: {
                        String uid = StreamUtil.Read<String>(stream);
                        ServiceInfo serviceInfo = FindByUID(uid);

                        MemoryStream sendStream = new MemoryStream();
                        StreamUtil.WriteWithNullCheck(sendStream, serviceInfo);
                        _rpcSocket.Send(sendStream.GetBuffer());

                        break;
                    }
                    case RegistryMessageType.FindByType: {
                        String type = StreamUtil.Read<String>(stream);
                        ServiceInfo[] services = FindByType(type);

                        MemoryStream sendStream = new MemoryStream();
                        StreamUtil.WriteWithNullCheck(sendStream, services);
                        _rpcSocket.Send(sendStream.GetBuffer());

                        break;
                    }
                    case RegistryMessageType.Pulse:
                        {
                            
                            ApplicationInstance remoteApp = StreamUtil.Read<ApplicationInstance>(stream);
                            Console.WriteLine("Received Pulse " + remoteApp.Guid);
                            _servicesPerApplication[remoteApp.Guid].Activity = DateTime.Now;
                            //TODO: lookup corresponding client and set an Activity variable to current timestamp
                            

                            _rpcSocket.Send();
                            break;
                        }


                    default: {
                        Console.WriteLine("Received unknown message type: " + messageType);
                        // TODO: Block sender after x unknown message types?
                        _rpcSocket.Send();
                        break;
                    }
                }
                message = _rpcSocket.Recv(SendRecvOpt.NOBLOCK);
            }
            RemoveTimedOutServices();
        }

        /**
        Enters an infinite loop that automatically calls Update. After each update the Idle event is triggered. Use
        StopAutoUpdate to break out of this loop.
        **/
        public void AutoUpdate() {
            while(!_stopAutoUpdate) {
                Update();
                if(Idle != null) Idle();
                Thread.Sleep(1000);
            }

            _stopAutoUpdate = false;
        }

        /**
        Stop the auto update loop.
        **/
        public void StopAutoUpdate() {
            _stopAutoUpdate = true;
        }

        /**
        Register a service.
        
        @param  serviceInfo Information describing the service to register
        
        @return True if service is successfully registered, false if a service with given UID already exists.
        **/
        private bool Register(ServiceInfo serviceInfo) {
            if(_servicesByUID.ContainsKey(serviceInfo.UID)) return false;

            _servicesByUID.Add(serviceInfo.UID, serviceInfo);

            Dictionary<String, ServiceInfo> dict;
            if(_servicesByType.TryGetValue(serviceInfo.Type, out dict)) {
                dict.Add(serviceInfo.UID, serviceInfo);
            } else {
                dict = new Dictionary<String, ServiceInfo> ();
                dict.Add(serviceInfo.UID, serviceInfo);
                _servicesByType.Add(serviceInfo.Type, dict);
            }


            //Add service to _servicesPerApplication
            if (!_servicesPerApplication.ContainsKey(serviceInfo.Guid))
            {
                ServiceUidHolder  services = new ServiceUidHolder();
                services.Services.Add(serviceInfo);
                _servicesPerApplication.Add(serviceInfo.Guid, services);
            }
            else
                _servicesPerApplication[serviceInfo.Guid].Services.Add(serviceInfo);

            Console.WriteLine("Added service: " + serviceInfo);

            return true;
        }

        /**
        Unregisters a service
        
        @param  uid The UID of the service to unregister.
        
        @return True if service is successfully unregistered, false if service with given UID does not exists.
        **/
        private bool Unregister(String uid) {
            ServiceInfo serviceInfo;
            if(_servicesByUID.TryGetValue(uid, out serviceInfo)) {
                _servicesByUID.Remove(uid);

                Dictionary<String, ServiceInfo> dict;
                if(_servicesByType.TryGetValue(serviceInfo.Type, out dict)) {
                    dict.Remove(uid);
                }

                //Remove service from _servicesPerApplication
                var results =   from servicelist in _servicesPerApplication
                                where servicelist.Value.Services.Where(x => (x.UID == uid)).Any()
                                select new { servicelist.Key, ServiceInfo = servicelist.Value.Services.Where(x => (x.UID==uid)).Single() };
                _servicesPerApplication[results.Single().Key].Services.Remove(results.Single().ServiceInfo);


                Console.WriteLine("Removed service: " + serviceInfo);

                return true;
            }

            return false;
        }

        /**
        Unregisters all services.
        **/
        private void UnregisterAll() {
            List<String> uids = new List<String>(_servicesByUID.Keys);
            foreach(String uid in uids) Unregister(uid);
        }

        /**
        Searches for a service by UID.
        
        @param  uid The UID of the service to find.
        
        @return The service info with given UID, or null if it was not found.
        **/
        private ServiceInfo FindByUID(String uid) {
            ServiceInfo serviceInfo;
            if(_servicesByUID.TryGetValue(uid, out serviceInfo)) {
                return serviceInfo;
            }

            return null;
        }

        /**
        Searches for services by type.
        
        @param  type The type of the services to find.
        
        @return The services info with given type, or null if no services were found.
        **/
        private ServiceInfo[] FindByType(String type) {
            Dictionary<String, ServiceInfo> services;
            if(_servicesByType.TryGetValue(type, out services)) {
                ServiceInfo[] servicesArray = new ServiceInfo[services.Count];
                services.Values.CopyTo(servicesArray, 0);
                return servicesArray;
            }

            return null;
        }

        /**
         * This method checks from dead connections. If a dead connection is found all services are removed from the active services pool.
         **/
        private void RemoveTimedOutServices()
        {
            var timedOutGUIDs = _servicesPerApplication.Where(x => (x.Value.Activity.AddSeconds(5) < DateTime.Now)).Select(x => (x.Key));
            List<String> unregUIDS = new List<string>();
            foreach (string timeOutedGuid in timedOutGUIDs.ToArray())
            {
                foreach (ServiceInfo si in _servicesPerApplication[timeOutedGuid].Services)
                {
                    unregUIDS.Add(si.UID);
                    Console.WriteLine("Service: " + si.UID + " timed out!");
                }
            }
            foreach (string unreg in unregUIDS)
                Unregister(unreg);
        }



    }
}

