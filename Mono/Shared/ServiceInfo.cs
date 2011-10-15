using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace Protophase.Shared {
    /**
    Information about a service.
    **/
    [Serializable]
    public class ServiceInfo : IEquatable<ServiceInfo> {
        /**
        Constructor.
        
        @param  uid         The UID.
        @param  type        The service type.
        @param  version     The service version.
        @param  address     The remote address.
        @param  rpcPort     The RPC listening port.
        @param  publishPort The publish/subscribe listening port.
        @param  rpcMethods  The available RPC methods.
        **/
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

        /**
        Tests if this object is equal to another.
        
        @param  other   The object check equality against.
        
        @return True if the objects are equal, false if they are not.
        **/
        public bool Equals(ServiceInfo other) {
            return UID == other.UID;
        }

        /**
        Convert this object into a string representation.
        
        @return A string representation of this object.
        **/
        public override String ToString() {
            return UID + ", " + Type + ", " + Version + ", " + Address + ", " + RPCPort + ", " + PublishPort +
                ", RPC methods: [" + String.Join(", ", RPCMethods.ToArray()) + "]";
        }
    }
}

