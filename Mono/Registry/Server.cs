using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Protophase.Shared;
using System.Linq;
using ZMQ;


namespace Protophase.Registry {
    /**
    Registry server that keeps a registry of existing objects.
    **/
    public class Server : IDisposable {
        private Context _context;
        private bool _stopAutoUpdate = false;

        private Address _rpcAddress;
        private Address _publishAddress;
        private Socket _rpcSocket;
        private Socket _publishSocket;

        private Dictionary<String, ServiceInfo> _servicesByUID = new Dictionary<String, ServiceInfo>();
        private Dictionary<String, Dictionary<String, ServiceInfo>> _servicesByType =
            new Dictionary<String, Dictionary<String, ServiceInfo>>();

        private Dictionary<ulong, ServiceUidHolder> _servicesPerApplication = new Dictionary<ulong, ServiceUidHolder>();

        private List<long> _reservedUids = new List<long>();
        private long _serverUid;
        private List<ServerInfo> _knownServers = new List<ServerInfo>();
        private Dictionary<long, Socket> _serverRpcSockets = new Dictionary<long, Socket>();
        private DateTime _lastSynch = DateTime.Now;
        

        private ulong _nextApplicationID = 0;

        private static readonly float SERVICE_TIMEOUT = 5.0f;

        /**
        Delegate for the idle event.
        **/
        public delegate void IdleEvent();

        /**
        Event that is called after each update loop when in auto update mode.
        **/
        public event IdleEvent Idle;

        /**
        Default constructor. Using the TCP transport, binds to all addresses using default ports.
        **/
        public Server() : this("*") { }

        /**
        Simple constructor. Using the TCP transport, binds to given remote address with default ports.
        
        @param  address   The address to bind on. Use * to bind to all addresses.
        **/
        public Server(String address) : this(new Address(Transport.TCP, address, 5555), 
            new Address(Transport.TCP, address, 5556)) { }

        /**
        Constructor.
        
        @param  rpcAddress      The RPC address to bind to.
        @param  publishAddress  The publish address to bind to.
        **/
        public Server(Address rpcAddress, Address publishAddress) {
            _context = SharedContext.Get();

            _rpcAddress = rpcAddress;
            _publishAddress = publishAddress;

            BindRPC();
            BindPublish();
        }

        /**
        Finaliser.
        **/
        ~Server() {
            Dispose(true);
        }

        /**
        Dispose of this object, unregisters all services and cleans up any resources it uses.
        **/
        public void Dispose() {
            Dispose(false);
        }

        /**
        Dispose of this object, unregisters all services and cleans up any resources it uses.
        **/
        protected virtual void Dispose(bool finalized) {
            if(!finalized) {
                // Unregister all service objects.
                UnregisterAll();

                // Dispose context and sockets.
                _rpcSocket.Dispose();
                _publishSocket.Dispose();

                GC.SuppressFinalize(this);
            }

            SharedContext.Dispose();
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
        Receives all network messages.
        **/
        private void Receive() {
            // Get next messages from clients.
            byte[] message = _rpcSocket.Recv(SendRecvOpt.NOBLOCK);
            while(message != null) {
                MemoryStream stream = StreamUtil.CreateStream(message);

                // Read message type
                RegistryMessageType messageType = (RegistryMessageType)stream.ReadByte();

                // Execute command
                switch(messageType) {
                    case RegistryMessageType.RegisterApplication: {
                        MemoryStream sendStream = new MemoryStream();
                        StreamUtil.Write(sendStream, _nextApplicationID++);
                        StreamUtil.Write(sendStream, SERVICE_TIMEOUT);
                        _rpcSocket.Send(sendStream.GetBuffer());

                        break;
                    }
                    case RegistryMessageType.RegisterService: {
                        ulong appID = StreamUtil.Read<ulong>(stream);
                        ServiceInfo serviceInfo = StreamUtil.Read<ServiceInfo>(stream);

                        MemoryStream sendStream = new MemoryStream();
                        StreamUtil.WriteBool(sendStream, Register(appID, serviceInfo));
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
                    case RegistryMessageType.Pulse: {
                        ulong appID = StreamUtil.Read<ulong>(stream);
                        if(_servicesPerApplication.ContainsKey(appID))
                            _servicesPerApplication[appID].Activity = DateTime.Now;

                        _rpcSocket.Send();
                        break;
                    }

                    //Registry to Registry communications

                    case RegistryMessageType.RequestServerUid:
                    {
                        ServerInfo newServer = StreamUtil.Read<ServerInfo>(stream);
                        MemoryStream sendStream = new MemoryStream();
                        long newUid = NewServerUIDInPool();
                        newServer.GlobalServerId = newUid;
                        AddServerToServerPool(newServer);
                        StreamUtil.Write(sendStream, newUid);
                        StreamUtil.Write(sendStream, _knownServers);
                        StreamUtil.Write(sendStream, _serverUid);
                        //TODO add rest of initial stuff. - Might be nice to do this in a struct so a Periodical Synch method can also use it.
                        _rpcSocket.Send(sendStream.GetBuffer());
                        break;
                    }
                    case RegistryMessageType.ReserveServerUid:
                    {
                        long proposal = StreamUtil.Read<long>(stream);
                        bool ok = true;
                        if (_knownServers.Where(x => (x.GlobalServerId == proposal)).Any() || _reservedUids.Contains(proposal))
                            ok = false;
                        MemoryStream sendStream = new MemoryStream();
                        StreamUtil.Write(sendStream, ok);
                        _rpcSocket.Send(sendStream.GetBuffer());
                    }
                    break;



                    default: {
                        Console.WriteLine("Received unknown message type: " + messageType);
                        _rpcSocket.Send();
                        break;
                    }
                }

                // Try to get more messages.
                message = _rpcSocket.Recv(SendRecvOpt.NOBLOCK);
            }
        }

        /*
         * - Adds a Registry Server to the pool
         * - Creates the Rpc Socket to this server
         */
        private void AddServerToServerPool(ServerInfo si)
        {
            _knownServers.Add(si);
            Socket newServerConnection = _context.Socket(SocketType.REQ);
            //TODO fix this so it works with * as address
            newServerConnection.Connect(Transport.TCP, "localhost", si._rpcPort);
            _serverRpcSockets.Add(si.GlobalServerId, newServerConnection);
            if (_reservedUids.Contains(si.GlobalServerId))
                _reservedUids.Remove(si.GlobalServerId);
            Console.WriteLine("Added Server to serverpool. Size=" + _knownServers.Count);
        }
        /*
         * Removes a Registry Server from the pool
         */
        private void RemoveServerFromPool(ServerInfo si)
        {
            _knownServers.Remove(si);
            _serverRpcSockets[si.GlobalServerId].Dispose();
            _serverRpcSockets.Remove(si.GlobalServerId);
            if (_reservedUids.Contains(si.GlobalServerId))
                _reservedUids.Remove(si.GlobalServerId);
        }


        /*
         * If this Server is not a member of a pool, create a new one (and create own ServerInfo)
         * Returns a free ServerID, and checks whether the proposal is OK with other known RegistryServers.
         * The other servers will add this proposal to a Reserved list in order to make sure it won't be hogged by another.
         */
        private long NewServerUIDInPool()
        {
            Console.WriteLine("NewServerGUID called");
            if (!_knownServers.Any())
            {
                _serverUid = 1;
                ServerInfo own = new ServerInfo("localhost", _rpcAddress.Port, _publishAddress.Port);
                own.GlobalServerId = _serverUid;
                _knownServers.Add(own);
            }
            long reserved = 0;
            if (_reservedUids.Any())
                reserved = _reservedUids.Max();

            long proposal = Math.Max(_knownServers.Max(x => (x.GlobalServerId)), reserved);
            bool proposalAccepted = true;
            do
            {
                proposal++;
                _reservedUids.Add(proposal);
                foreach (KeyValuePair<long, Socket> knownServer in _serverRpcSockets)
                {
                    //Dont send a reserve message to self
                    if (knownServer.Key == _serverUid)
                        continue;
                    //Send a reserve message for current proposal
                    Socket server = knownServer.Value;
                    MemoryStream stream = new MemoryStream();
                    stream.WriteByte((byte) RegistryMessageType.ReserveServerUid);
                    StreamUtil.Write(stream, proposal);
                    server.Send(stream.GetBuffer());
                    byte[] message = server.Recv();
                    MemoryStream receiveStream = new MemoryStream(message);
                    //The proposal is not accepted, will increase and try again.
                    if (!StreamUtil.Read<bool>(receiveStream))
                    {
                        proposalAccepted = false;
                        break;
                    }
                }
            //Loop with proposal candidates until a proposal is accepted. (Should not take more than 1-2 iterations)
            } while (!proposalAccepted);
            return proposal;
        }

        /**
        Removes services from timed out applications.
        **/
        private void RemoveTimedOutServices() {
            // Find timed out applications and add their services to unregisterServices.
            var timedOutApplications = _servicesPerApplication.Where(x => (x.Value.Activity.AddSeconds(SERVICE_TIMEOUT) < DateTime.Now)).Select(x => (x.Key));
            List<String> unregisterServices = new List<String>();
            foreach(ulong appID in timedOutApplications.ToArray()) {
                Console.WriteLine("Application with ID " + appID + " timed out (" + SERVICE_TIMEOUT + " seconds)");
                foreach(ServiceInfo serviceInfo in _servicesPerApplication[appID].Services) {
                    unregisterServices.Add(serviceInfo.UID);
                }
            }

            // Unregister all 'dead' services.
            foreach(String uid in unregisterServices)
                Unregister(uid);

            // Unmap all timed out applications.
            foreach(ulong appID in timedOutApplications.ToArray())
                _servicesPerApplication.Remove(appID);
        }

        /**
        Sends and receives all network messages.
        **/
        public void Update() {
            Receive();
            RemoveTimedOutServices();
            SyncServers();
        }

        private void SyncServers()
        {
            if (_lastSynch.AddSeconds(10) > DateTime.Now)
            {
                _lastSynch = DateTime.Now;
                //TODO Actually send a message containing all data from this class. (serviceInfos, possible other known Servers, etc.)
                //TODO Publish/Subscribe events when services/servers are added or removed.
            }
        }

        /**
        Enters an infinite loop that automatically calls Update. After each update the Idle event is triggered. Use
        StopAutoUpdate to break out of this loop.
        
        @param  millisecondsSleep   The number of milliseconds to sleep between updates.
        **/
        public void AutoUpdate() { AutoUpdate(0); }

        /**
        Enters an infinite loop that automatically calls Update. After each update the Idle event is triggered. Use
        StopAutoUpdate to break out of this loop.
        
        @param  millisecondsSleep   The number of milliseconds to sleep between updates.
        **/
        public void AutoUpdate(int millisecondsSleep) {
            while(!_stopAutoUpdate) {
                Update();
                if(Idle != null) Idle();
                Thread.Sleep(millisecondsSleep);
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
        
        @param  appID       Application identifier.
        @param  serviceInfo Information describing the service to register.
        
        @return True if service is successfully registered, false if a service with given UID already exists.
        **/
        public bool Register(ulong appID, ServiceInfo serviceInfo) {
            // Don't allow duplicate UIDs.
            if(_servicesByUID.ContainsKey(serviceInfo.UID)) return false;

            // Map service by UID.
            _servicesByUID.Add(serviceInfo.UID, serviceInfo);

            // Map service by type.
            Dictionary<String, ServiceInfo> dict;
            if(_servicesByType.TryGetValue(serviceInfo.Type, out dict)) {
                dict.Add(serviceInfo.UID, serviceInfo);
            } else {
                dict = new Dictionary<String, ServiceInfo>();
                dict.Add(serviceInfo.UID, serviceInfo);
                _servicesByType.Add(serviceInfo.Type, dict);
            }

            // Map service by application ID.
            if(!_servicesPerApplication.ContainsKey(appID)) {
                ServiceUidHolder services = new ServiceUidHolder();
                services.Services.Add(serviceInfo);
                _servicesPerApplication.Add(appID, services);
            } else
                _servicesPerApplication[appID].Services.Add(serviceInfo);

            // Publish service registered message
            // Serialize to binary
            MemoryStream stream = new MemoryStream();
            // Write message type
            stream.WriteByte((byte)RegistryPublishType.ServiceRegistered);
            // Write service info
            StreamUtil.Write(stream, serviceInfo);

            // Publish message
            _publishSocket.Send(stream.GetBuffer());

            Console.WriteLine("Added service: " + serviceInfo);

            return true;
        }

        /**
        Unregisters a service
        
        @param  uid The UID of the service to unregister.
        
        @return True if service is successfully unregistered, false if service with given UID does not exists.
        **/
        public bool Unregister(String uid) {
            ServiceInfo serviceInfo;
            if(_servicesByUID.TryGetValue(uid, out serviceInfo)) {
                // Unmap service per UID.
                _servicesByUID.Remove(uid);

                // Unmap service per type.
                Dictionary<String, ServiceInfo> dict;
                if(_servicesByType.TryGetValue(serviceInfo.Type, out dict)) {
                    dict.Remove(uid);
                }

                // Unmap service per application ID.
                var results = from servicelist in _servicesPerApplication
                              where servicelist.Value.Services.Where(x => (x.UID == uid)).Any()
                              select new { servicelist.Key, ServiceInfo = servicelist.Value.Services.Where(x => (x.UID == uid)).Single() };
                if(results.Any()) {
                    ulong appID = results.Single().Key;
                    if(_servicesPerApplication.ContainsKey(appID))
                        _servicesPerApplication[appID].Services.Remove(results.Single().ServiceInfo);
                    //Remove the application instance if there are no longer any services attached
                    if (_servicesPerApplication[appID].Services.Count == 0)
                        _servicesPerApplication.Remove(appID);
                }

                // Publish service unregistered message
                // Serialize to binary
                MemoryStream stream = new MemoryStream();
                // Write message type
                stream.WriteByte((byte)RegistryPublishType.ServiceUnregistered);
                // Write service info
                StreamUtil.Write(stream, serviceInfo);

                // Publish message
                _publishSocket.Send(stream.GetBuffer());

                Console.WriteLine("Removed service: " + serviceInfo);

                return true;
            }

            return false;
        }

        /**
        Unregisters all services.
        **/
        public void UnregisterAll() {
            List<String> uids = new List<String>(_servicesByUID.Keys);
            foreach(String uid in uids) 
                Unregister(uid);
        }

        /**
        Searches for a service by UID.
        
        @param  uid The UID of the service to find.
        
        @return The service info with given UID, or null if it was not found.
        **/
        public ServiceInfo FindByUID(String uid) {
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
        public ServiceInfo[] FindByType(String type) {
            Dictionary<String, ServiceInfo> services;
            if(_servicesByType.TryGetValue(type, out services)) {
                ServiceInfo[] servicesArray = new ServiceInfo[services.Count];
                services.Values.CopyTo(servicesArray, 0);
                return servicesArray;
            }

            return null;
        }

        /*   
         * Accepts a socket and RegistryMessageType together with a number of objects which are sent over the socket. (got sick of copying the same code over and over)
         * 
         */

        private void SendData(Socket socket, RegistryMessageType msgType, params object[] data)
        {
            MemoryStream stream = new MemoryStream();
            // Write message type
            stream.WriteByte((byte)msgType);
            foreach (object obj in data)
                StreamUtil.Write(stream, obj);
            socket.Send(stream.GetBuffer());
        }

        /*
         * Negotiates the adding of this server to the known pool of a known member of a pool.
         */
        public void AddToServerPool(string aMemberAddress, uint aMemberRpcPort, uint aMemberPubPort)
        {
            
            Console.WriteLine(_rpcAddress.Port + " Adding self to remote server...");
            ServerInfo serverInfo = new ServerInfo("localhost", _rpcAddress.Port, _publishAddress.Port);
            Socket newServerConnection = _context.Socket(SocketType.REQ);
            newServerConnection.Connect(Transport.TCP, aMemberAddress, aMemberRpcPort);
            SendData(newServerConnection, RegistryMessageType.RequestServerUid, serverInfo);
            byte[] message = newServerConnection.Recv();

            Console.WriteLine(_rpcAddress.Port + " Receiving request to join pool message...");
            MemoryStream receiveStream = new MemoryStream(message);
            _serverUid = StreamUtil.Read<long>(receiveStream);
            var remoteKnownServers = StreamUtil.Read<List<ServerInfo>>(receiveStream);
            foreach (var remoteServer in remoteKnownServers)
                AddServerToServerPool(remoteServer);
            Console.WriteLine("Joined pool!");
        }

        //For debuggind purposes
        public void DumpServerPool()
        {
            string s = "{";
            foreach (var x in _knownServers)
                s += x._pubPort + ", ";
            s += "}";
            Console.WriteLine(_rpcAddress.Port + " : " + s);
        }
    }
}

