using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace Protophase.Shared {
    [Serializable]
    public class ServiceInfo {
        public ServiceInfo(String uid, String type, String version, String address, ushort rpcPort, ushort publishPort,
                           List<String> rpcMethods) {
            UID = uid;
            Type = type;
            Version = version;
            Address = address;
            RPCPort = rpcPort;
            PublishPort = publishPort;
            RPCMethods = rpcMethods;
        }

        public String UID;
        public String Type;
        public String Version;
        public String Address;
        public ushort RPCPort;
        public ushort PublishPort;
        public List<String> RPCMethods;

        public override String ToString() {
            return UID + ", " + Type + ", " + Version + ", " + Address + ", " + RPCPort + ", " + PublishPort +
                ", RPC methods: [" + String.Join(", ", RPCMethods.ToArray()) + "]";
        }
    }
}

