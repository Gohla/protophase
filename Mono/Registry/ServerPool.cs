using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Protophase.Service;
using Protophase.Shared;
using ZMQ;

namespace Protophase.Registry
{
    /*
     Contains variables and methods for the ServerPool part of a registry Server.
     */
    public partial class Server
    {
        private List<long> _reservedUids = new List<long>();
        private long _serverUid;
        private List<ServerInfo> _knownServers = new List<ServerInfo>();
        private Dictionary<long, Socket> _serverRpcSockets = new Dictionary<long, Socket>();
        private Dictionary<long, Socket> _serverPubSockets = new Dictionary<long, Socket>();
        private DateTime _lastSync = DateTime.Now;
        private DateTime _lastPulse = DateTime.Now;
        private const int SERVER_TIMEOUT = 5;
        private const int SERVER_FULLSYNC_INTERVAL = 10;



        //received rpc message had a ServerPool prefix, so will be treated as a ServerPool message from now on.
        private void ReceiveServerPoolMessage(MemoryStream stream) 
        {
            ServerPoolMessageRPC poolMessageRpcType = (ServerPoolMessageRPC)stream.ReadByte();
            switch (poolMessageRpcType)
            {
                case ServerPoolMessageRPC.RequestServerUid:
                    {
                        Address myPublicRpcAddress = StreamUtil.Read<Address>(stream);
                        Address myPublicPubAddress = StreamUtil.Read<Address>(stream);
                        MemoryStream sendStream = new MemoryStream();
                        long newUid = NewServerUIDInPool(myPublicRpcAddress, myPublicPubAddress);
                        StreamUtil.Write(sendStream, newUid);
                        StreamUtil.Write(sendStream, _knownServers);
                        _rpcSocket.Send(sendStream.GetBuffer());
                    }
                    break;

                case ServerPoolMessageRPC.ReserveServerUid:
                    {
                        long proposal = StreamUtil.Read<long>(stream);
                        bool ok = true;
                        if (_serverUid == proposal || _knownServers.Where(x => (x.GlobalServerId == proposal)).Any() || _reservedUids.Contains(proposal))
                            ok = false;
                        MemoryStream sendStream = new MemoryStream();
                        StreamUtil.Write(sendStream, ok);
                        _rpcSocket.Send(sendStream.GetBuffer());
                    }
                    break;
                case ServerPoolMessageRPC.AddServer:
                    {
                        ServerInfo si = StreamUtil.Read<ServerInfo>(stream);
                        AddServerToServerPool(si);
                        _rpcSocket.Send();
                        PoolDebug("Remote server joined pool");
                    }
                    break;
            }
        }

        


        /*
         * Negotiates the adding of this server to the known pool of a known member of a pool.
         */
        public void AddToServerPool(Address aMemberRPCAddress, Address aMemberPubAddress, Address ownPublicallyAccessibleRPCAddress, Address ownPublicallyAccessiblePubAddress)
        {
            if (_servicesByUID.Any() || _servicesByType.Any() || _servicesPerApplication.Any() || _knownServers.Any())
            {
                Console.WriteLine("Can not add a registry with clients to a registry pool");
                return;
            }
            Socket newServerConnection = _context.Socket(SocketType.REQ);
            newServerConnection.Connect(aMemberRPCAddress);
            //Request a unique ID, let the remote server know how this server connected to it.
            SendServerPoolMessage(newServerConnection, ServerPoolMessageRPC.RequestServerUid, aMemberRPCAddress, aMemberPubAddress);
            byte[] message = newServerConnection.Recv();
            MemoryStream receiveStream = new MemoryStream(message);
            //Receive unique ID
            _serverUid = StreamUtil.Read<long>(receiveStream);
            //Receive remote known servers list
            var remoteKnownServers = StreamUtil.Read<List<ServerInfo>>(receiveStream);

            ServerInfo thisNewPoolMember = new ServerInfo(ownPublicallyAccessibleRPCAddress, ownPublicallyAccessiblePubAddress);
            thisNewPoolMember.GlobalServerId = _serverUid;

            foreach (var remoteServer in remoteKnownServers)
            {
                AddServerToServerPool(remoteServer);
                SendServerPoolMessage(_serverRpcSockets[remoteServer.GlobalServerId], ServerPoolMessageRPC.AddServer, thisNewPoolMember);
                _serverRpcSockets[remoteServer.GlobalServerId].Recv();
            }
            //Finally add self to known servers.
            _knownServers.Add(thisNewPoolMember); 
        }


        /*
         * - Adds a Registry Server to the pool
         * - Creates the Rpc and pub Sockets to this server
         * - Notifies clients of this alternate registry
         */
        private void AddServerToServerPool(ServerInfo si)
        {
            Socket newServerConnectionRPC = _context.Socket(SocketType.REQ);
            newServerConnectionRPC.Connect(si._rpcRemotelyAccessibleAddress);
            _serverRpcSockets.Add(si.GlobalServerId, newServerConnectionRPC);
            Socket newServerConnectionPub = _context.Socket(SocketType.SUB);
            newServerConnectionPub.Connect(si._pubRemotelyAccessibleAddress);
            _serverPubSockets.Add(si.GlobalServerId, newServerConnectionPub);
            newServerConnectionPub.Subscribe(new byte[0]);
            _knownServers.Add(si);
            //publish this new server's info to this registry's clients.
            MemoryStream stream = new MemoryStream();
            stream.WriteByte((byte)RegistryPublishType.AlternateRegistryAvailable);
            StreamUtil.Write(stream, new AlternateRegistryServer(si.GlobalServerId, si._rpcRemotelyAccessibleAddress, si._pubRemotelyAccessibleAddress));
            _publishSocket.Send(stream.GetBuffer());
            PoolDebug("Added " + si.GlobalServerId);
        }

        /*
         * Removes a Registry Server from the pool and notifies clients the alternate registry is no longer available.
         */
        private void RemoveServerFromPool(ServerInfo si)
        {
            PoolDebug("Removing server from pool: " + si.GlobalServerId);
            _serverRpcSockets[si.GlobalServerId].Dispose();
            _serverRpcSockets.Remove(si.GlobalServerId);
            _serverPubSockets[si.GlobalServerId].Dispose();
            _serverPubSockets.Remove(si.GlobalServerId);
            _knownServers.Remove(si);
            //publish this dead server's id to the clients
            MemoryStream stream = new MemoryStream();
            stream.WriteByte((byte)RegistryPublishType.AlternateRegistryUnavailable);
            StreamUtil.Write(stream, si.GlobalServerId);
            _publishSocket.Send(stream.GetBuffer());
        }


        /*
         * If this Server is not a member of a pool, create a new one (and create own ServerInfo)
         * Returns a free ServerID, and checks whether the proposal is OK with other known RegistryServers.
         * The other servers will add this proposal to a Reserved list in order to make sure it won't be hogged by another.
         */
        private long NewServerUIDInPool(Address remoteServerConnectedToRpc, Address remoteServerConnectedToPub)
        {
            //Console.WriteLine("NewServerGUID called");
            if (!_knownServers.Any())
            {
                _serverUid = 1;
                ServerInfo own = new ServerInfo(remoteServerConnectedToRpc, remoteServerConnectedToPub);
                own.GlobalServerId = _serverUid;
                _knownServers.Add(own);
                //Console.WriteLine("Started a Server Pool!");
            }
            long reserved = 0;
            if (_reservedUids.Any())
                reserved = _reservedUids.Max();

            long proposal = Math.Max(_serverUid,  Math.Max(_knownServers.Max(x => (x.GlobalServerId)), reserved));
            bool proposalAccepted;
            do
            {
                proposal++;
                _reservedUids.Add(proposal);
                proposalAccepted = true;
                foreach (KeyValuePair<long, Socket> knownServer in _serverRpcSockets)
                {
                    //Dont send a reserve message to self
                    if (knownServer.Key == _serverUid)
                        continue;
                    //Send a reserve message for current proposal
                    Socket server = knownServer.Value;
                    SendServerPoolMessage(server, ServerPoolMessageRPC.ReserveServerUid, proposal);
                    byte[] message = server.Recv();
                    MemoryStream receiveStream = new MemoryStream(message);
                    //If the proposal is not accepted, will increase and try again.
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

        public int PoolSize
        {
            get { return _knownServers.Count; }
        }

        public void PoolDebug(string str)
        {
            string str2 = "{ ";
            foreach (var x in _knownServers)
                str2 += x.GlobalServerId + ", ";
            str2 += " }";
            Console.WriteLine("ServerId: " + _rpcAddress.Port + " " + str2 + " services:" + _servicesByUID.Count  + ": " + str);
        }

        public void DumpPool()
        {
            PoolDebug("");
        }

        /*
         * Recieves all published messages this Registry is subscribed on.
         * The own socket; _publishSocket is used to Publish data on, the _serverPubSockets are used to receive incoming published events (subscriptions)
         */

        private void RecieveSubscribed()
        {
            foreach (Socket subscription in _serverPubSockets.Values.ToArray())
            {
                byte[] message = subscription.Recv(SendRecvOpt.NOBLOCK);
                while (message != null)
                {
                    MemoryStream stream = StreamUtil.CreateStream(message);
                    RegistryPublishType publishType = (RegistryPublishType)stream.ReadByte();

                    switch (publishType)
                    {
                        case RegistryPublishType.ServerPoolMessage:
                            {
                                ServerPoolMessagePublish msgType = (ServerPoolMessagePublish) stream.ReadByte();
                                switch (msgType)
                                {
                                    case ServerPoolMessagePublish.ServerPulse:
                                        {
                                            long serverID = StreamUtil.Read<long>(stream);
                                            _knownServers.Where(x => (x.GlobalServerId == serverID)).Single().Activity =
                                                DateTime.Now;
                                        }
                                        break;
                                    case ServerPoolMessagePublish.ServicePulse:
                                        {
                                            ulong serviceID = StreamUtil.Read<ulong>(stream);
                                            if (_servicesPerApplication.ContainsKey(serviceID))
                                                _servicesPerApplication[serviceID].Activity = DateTime.Now;
                                        }
                                        break;
                                    case ServerPoolMessagePublish.FullSync:
                                        {
                                            SyncMessage syncMsg = StreamUtil.Read<SyncMessage>(stream);
                                            Synchronize(syncMsg);
                                        }
                                        break;
                                }
                            }
                            break;
                    }
                    // Try to get more messages.
                    message = subscription.Recv(SendRecvOpt.NOBLOCK);
                }
            }
        }

        /*
         * Makes sure other registries in the pool are notified of the current pulse.
         */
        private void ApplicationPulseReceived(ulong appId)
        {
            if (_servicesPerApplication.ContainsKey(appId))
                _servicesPerApplication[appId].Activity = DateTime.Now;
            ServerPoolPublish(ServerPoolMessagePublish.ServicePulse, appId);
        }


        /*
         * Unifies all known data from the other Server.
         */
        private void Synchronize(SyncMessage msg)
        {
            PoolDebug("Synchronizing with " + msg.ServerId);
            MergeServicesByType(msg.ServicesByType);
            MergeServicesByUid(msg.ServicesByUid);
            MergeServicesPerApplication(msg.ServicesPerApplication);
            MergeKnownServers(msg.KnownServers);
        }

        private void MergeServicesByType(Dictionary<String, Dictionary<String, ServiceInfo>> remote)
        {
            Dictionary<String, ServiceInfo> dict;
            foreach (var type in remote.Keys)
            {
                foreach (var uid in remote[type].Keys)
                {
                    if (_servicesByType.TryGetValue(type, out dict))
                        if (!dict.ContainsKey(uid))
                            dict.Add(uid, remote[type][uid]);
                    else
                    {
                        dict = new Dictionary<String, ServiceInfo>();
                        dict.Add(uid, remote[type][uid]);
                        _servicesByType.Add(type, dict);
                    }
                }
            }
        }

        private void MergeServicesByUid(Dictionary<String, ServiceInfo> remote)
        {
            foreach (var service in remote)
                if (!_servicesByUID.ContainsKey(service.Key))
                    _servicesByUID.Add(service.Key, service.Value);
        }

        private void MergeServicesPerApplication(Dictionary<ulong, ServiceUidHolder> remote)
        {
            foreach (var app in remote)
            {
                if (!_servicesPerApplication.ContainsKey(app.Key)) //Add all services known by the other server.
                    _servicesPerApplication.Add(app.Key, app.Value);
                else
                {
                    foreach (var service in app.Value.Services)
                        if (!_servicesPerApplication[app.Key].Services.Contains(service)) //add all locally unknown services from the remote application
                            _servicesPerApplication[app.Key].Services.Add(service);
                }
            }
        }


        /* Returns a list of AlternateRegistryServer
         */
        private List<AlternateRegistryServer> AlternateRegistries()
        {
            List<AlternateRegistryServer> altRegs = new List<AlternateRegistryServer>();
            foreach (var server in _knownServers)
                altRegs.Add(new AlternateRegistryServer(server.GlobalServerId, server._rpcRemotelyAccessibleAddress, server._pubRemotelyAccessibleAddress));
            return altRegs;
        }


        private void MergeKnownServers(List<ServerInfo> remote)
        {
            foreach (var server in remote)
                if (!_knownServers.Where(x => (x.GlobalServerId == server.GlobalServerId)).Any())
                    AddServerToServerPool(server);
        }

        /*   
         * Sends server pool messages with accompanying data through socket
         */
        private void SendServerPoolMessage(Socket socket, ServerPoolMessageRPC msgType, params object[] data)
        {
            MemoryStream stream = new MemoryStream();
            stream.WriteByte((byte)RegistryMessageType.ServerPoolMessage);
            stream.WriteByte((byte)msgType);
            foreach (object obj in data)
                StreamUtil.Write(stream, obj);
            socket.Send(stream.GetBuffer());
        }

        private void ServerPoolPublish(ServerPoolMessagePublish msgType, params object[] data)
        {
            MemoryStream stream = new MemoryStream();
            stream.WriteByte((byte)RegistryPublishType.ServerPoolMessage);
            stream.WriteByte((byte)msgType);
            foreach (object obj in data)
                StreamUtil.Write(stream, obj);
            _publishSocket.Send(stream.GetBuffer());
        }

        //Enums for RPC calls between two servers.
        private enum ServerPoolMessageRPC
        {
            RequestServerUid,
            ReserveServerUid,
            AddServer
        }

        private enum ServerPoolMessagePublish
        {
            ServerPulse,
            ServicePulse,
            FullSync
        }

        /*
         * Manages the known server list.
         */

        private void ManageServerPool()
        {
            if (_lastPulse.AddSeconds(SERVER_TIMEOUT / 2) < DateTime.Now)
            {
                ServerPoolPublish(ServerPoolMessagePublish.ServerPulse, _serverUid);
                _lastPulse = DateTime.Now;
            }
            var timedOutServers = _knownServers.Where(x => (x.GlobalServerId != _serverUid && x.Activity.AddSeconds(SERVER_TIMEOUT) < DateTime.Now));
            foreach (var server in timedOutServers.ToArray())
                RemoveServerFromPool(server);

            if (_lastSync.AddSeconds(SERVER_FULLSYNC_INTERVAL) < DateTime.Now)
            {
                _lastSync = DateTime.Now;
                SyncMessage thisData = new SyncMessage();
                thisData.KnownServers = _knownServers;
                thisData.ServerId = _serverUid;
                thisData.ServicesByType = _servicesByType;
                thisData.ServicesByUid = _servicesByUID;
                thisData.ServicesPerApplication = _servicesPerApplication;
                ServerPoolPublish(ServerPoolMessagePublish.FullSync, thisData);
            }
        }
    }
    [Serializable]
    public struct SyncMessage
    {
        public long ServerId;
        public List<ServerInfo> KnownServers;
        public Dictionary<String, ServiceInfo> ServicesByUid;
        public Dictionary<String, Dictionary<String, ServiceInfo>> ServicesByType;
        public Dictionary<ulong, ServiceUidHolder> ServicesPerApplication;
    }
}
