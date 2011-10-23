using System;
using Protophase.Shared;

namespace Protophase.Registry
{
    [Serializable]
    public class ServerInfo : IEquatable<ServerInfo>
    {
        private long _globalServerId = 0;
        public Address _rpcRemotelyAccessibleAddress;
        public Address _pubRemotelyAccessibleAddress;
        public DateTime Activity { get; set; }
        public ServerInfo(Address rpcRemotelyAccessibleAddress, Address pubRemotelyAccessibleAddress)
        {
            _rpcRemotelyAccessibleAddress = rpcRemotelyAccessibleAddress;
            _pubRemotelyAccessibleAddress = pubRemotelyAccessibleAddress;
            Activity = DateTime.Now;
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