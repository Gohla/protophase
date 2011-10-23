using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        private DateTime _lastSynch = DateTime.Now;



        private void SyncServers()
        {
            if (_lastSynch.AddSeconds(10) > DateTime.Now)
            {
                _lastSynch = DateTime.Now;
                //TODO Actually send a message containing all data from this class. (serviceInfos, possible other known Servers, etc.)
                //TODO Publish/Subscribe events when services/servers are added or removed.
            }
        }





        /*
         * Negotiates the adding of this server to the known pool of a known member of a pool.
         */
        public void AddToServerPool(string aMemberAddress, uint aMemberRpcPort, uint aMemberPubPort)
        {

            Console.WriteLine(_rpcAddress.Port + " Adding self to remote server...");
            //ServerInfo serverInfo = new ServerInfo("localhost", _rpcAddress.Port, _publishAddress.Port);
            Socket newServerConnection = _context.Socket(SocketType.REQ);
            newServerConnection.Connect(Transport.TCP, aMemberAddress, aMemberRpcPort);
            SendData(newServerConnection, RegistryMessageType.RequestServerUid);
            byte[] message = newServerConnection.Recv();

            Console.WriteLine(_rpcAddress.Port + " Receiving request to join pool message...");
            MemoryStream receiveStream = new MemoryStream(message);
            _serverUid = StreamUtil.Read<long>(receiveStream);
            var remoteKnownServers = StreamUtil.Read<List<ServerInfo>>(receiveStream);

            ServerInfo thisNewPoolMember = new ServerInfo(_rpcAddress, _publishAddress);
            thisNewPoolMember.GlobalServerId = _serverUid;

            foreach (var remoteServer in remoteKnownServers)
            {

                //AddServerToServerPool(remoteServer);
            }
            Console.WriteLine("Joined pool!");
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
            newServerConnection.Connect(si._rpcAddress);
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
                ServerInfo own = new ServerInfo(_rpcAddress, _publishAddress);
                own.GlobalServerId = _serverUid;
                _knownServers.Add(own);
                Console.WriteLine("Started a Server Pool!");
            }
            long reserved = 0;
            if (_reservedUids.Any())
                reserved = _reservedUids.Max();

            long proposal = Math.Max(_knownServers.Max(x => (x.GlobalServerId)), reserved);
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
                    MemoryStream stream = new MemoryStream();
                    stream.WriteByte((byte)RegistryMessageType.ReserveServerUid);
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



        /*   
         * Sends data through socket of a certain message type
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



    }
}
