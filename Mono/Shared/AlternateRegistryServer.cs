using System;
using Protophase.Shared;

namespace Protophase.Service
{
    [Serializable]
    public class AlternateRegistryServer
    {
        public AlternateRegistryServer(long serverId, Address serverRPCAddress, Address serverPubAddress)
        {
            ServerID = serverId;
            ServerPubAddress = serverPubAddress;
            ServerRPCAddress = serverRPCAddress;
        }
        public readonly long ServerID;
        public readonly Address ServerRPCAddress;
        public readonly Address ServerPubAddress;
    }
}