using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace Protophase.Shared {
    /**
    Information about a service.
    **/
    [Serializable]
    public class ServiceInfo : IEquatable<ServiceInfo>, IComparable<ServiceInfo>, IEquatable<String>, 
        IComparable<String> {
        /**
        Constructor.
        
        @param  uid             The not-generated UID.
        @param  type            The service type.
        @param  version         The service version.
        @param  rpcAddress      The remote RPC address.
        @param  publishAddress  The remote publish address.
        @param  rpcMethods      The available RPC methods.
        **/
        public ServiceInfo(String uid, String type, String version, Address rpcAddress, Address publishAddress, 
            List<String> rpcMethods) : this(uid, type, version, rpcAddress, publishAddress, rpcMethods, false) {
        }

        /**
        Constructor.
        
        @param  uid             The UID.
        @param  type            The service type.
        @param  version         The service version.
        @param  rpcAddress      The remote RPC address.
        @param  publishAddress  The remote publish address.
        @param  rpcMethods      The available RPC methods.
        @param  generatedUID    Set to true if the given UID was generated.
        **/
        public ServiceInfo(String uid, String type, String version, Address rpcAddress, Address publishAddress, 
            List<String> rpcMethods, bool generatedUID) {
            UID = uid;
            Type = type;
            Version = version;
            RPCAddress = rpcAddress;
            PublishAddress = publishAddress;
            RPCMethods = rpcMethods;
            GeneratedUID = generatedUID;
        }

        public String UID;
        public String Type;
        public String Version;
        public Address RPCAddress;
        public Address PublishAddress;
        public List<String> RPCMethods;
        [NonSerializedAttribute] public readonly bool GeneratedUID;

        /**
        Tests if this object is equal to another.
        
        @param  other   The object check equality against.
        
        @return True if the objects are equal, false if they are not.
        **/
        public bool Equals(ServiceInfo other) {
            return UID == other.UID;
        }

        /**
        Compares this ServiceInfo object to another to determine their relative ordering.
        
        @param  other   Another instance to compare.
        
        @return Negative if this object is less than the other, 0 if they are equal, or positive if this is greater.
        **/
        public int CompareTo(ServiceInfo other) {
            return UID.CompareTo(other.UID);
        }

        /**
        Tests if this object is equal to another.
        
        @param  other   The object check equality against.
        
        @return True if the objects are equal, false if they are not.
        **/
        public bool Equals(String other) {
            return UID == other;
        }

        /**
        Compares this ServiceInfo object to another to determine their relative ordering.
        
        @param  other   Another instance to compare.
        
        @return Negative if this object is less than the other, 0 if they are equal, or positive if this is greater.
        **/
        public int CompareTo(String other) {
            return UID.CompareTo(other);
        }

        /**
        Calculates the hash code for this object.
        
        @return The hash code for this object.
        **/
        public override int GetHashCode() {
            return UID.GetHashCode();
        }

        /**
        Convert this object into a string representation.
        
        @return A string representation of this object.
        **/
        public override String ToString() {
            return UID + ", " + Type + ", " + Version + ", " + RPCAddress + ", " + PublishAddress +
                ", [" + String.Join(", ", RPCMethods.ToArray()) + "]";
        }
    }
}

