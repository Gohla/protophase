using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace Protophase.Shared {
    [Serializable]
    public class ServiceInfo {
        public ServiceInfo(String uid, String type, String version, String address, List<String> rpcMethods) {
            UID = uid;
            Type = type;
            Version = version;
            Address = address;
            RPCMethods = rpcMethods;
        }

        public String UID;
        public String Type;
        public String Version;
        public String Address;
        public List<String> RPCMethods;

        public override String ToString() {
            return UID + ", " + Type + ", " + Version + ", " + Address + ", RPC methods: [" +
                String.Join(", ", RPCMethods.ToArray()) + "]";
        }
    }
}

