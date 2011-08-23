using System;
using System.Runtime.Serialization;

namespace Protophase.Shared {
    [Serializable]
    public class ServiceInfo {
        public ServiceInfo(String uid, String type, String version, String address) {
            UID = uid;
            Type = type;
            Version = version;
            Address = address;
        }

        public String UID;
        public String Type;
        public String Version;
        public String Address;

        public override String ToString() {
            return UID + ", " + Type + ", " + Version + ", " + Address;
        }
    }
}

