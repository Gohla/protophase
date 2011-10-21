using System;

namespace Protophase.Registry
{
    [Serializable]
    public class ServerInfo : IEquatable<ServerInfo>
    {
        private long _globalServerId = 0;
        public String _address;
        public uint _rpcPort;
        public uint _pubPort;
        public ServerInfo(string address, uint rpcPort, uint pubPort)
        {
            _address = address;
            _rpcPort = rpcPort;
            _pubPort = pubPort;
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