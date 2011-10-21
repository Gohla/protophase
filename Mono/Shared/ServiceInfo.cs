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
        
        @param  uid             The UID.
        @param  type            The service type.
        @param  version         The service version.
        @param  rpcAddress      The remote RPC address.
        @param  publishAddress  The remote publish address.
        @param  rpcMethods      The available RPC methods.
        **/
        public ServiceInfo(String uid, String type, String version, Address rpcAddress, Address publishAddress, 
            List<String> rpcMethods) {
            UID = uid;
            Type = type;
            Version = version;
            RPCAddress = rpcAddress;
            PublishAddress = publishAddress;
            RPCMethods = rpcMethods;
        }

        public String UID;
        public String Type;
        public String Version;
        public Address RPCAddress;
        public Address PublishAddress;
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
        Calculates the hash code for this object.
        
        @return The hash code for this object.
        **/
        public override int GetHashCode()
        {
            return UID.GetHashCode();
        }

        /**
        Convert this object into a string representation.
        
        @return A string representation of this object.
        **/
        public override String ToString() {
            return UID + ", " + Type + ", " + Version + ", " + RPCAddress + ", " + PublishAddress + ", " +
                ", RPC methods: [" + String.Join(", ", RPCMethods.ToArray()) + "]";
        }
    }
}

