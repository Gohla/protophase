using System;
using Protophase.Shared;

namespace Protophase.Registry
{
    [Serializable]
    public class ServerInfo : IEquatable<ServerInfo>
    {
        private long _globalServerId = 0;
        public Address _rpcAddress;
        public Address _pubAddress;
        public ServerInfo(Address rpcAddress, Address pubAddress)
        {
            _rpcAddress = rpcAddress;
            _pubAddress = pubAddress;
        }
        public long GlobalServerId
        {
            get { return _globalServerId; }
            set
            {
                if (_globalServerId == 0)
                    _globalServerId = value;
                else
                    throw new Exception("Can only assign a server id once.");
            }
        }

        public bool Equals(ServerInfo other)
        {
            return other.GlobalServerId == _globalServerId;
        }
    }
}