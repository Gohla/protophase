using System;

namespace Protophase.Shared {
    /**
    Method attribute to indicate that this method can be called remotely.
    **/
    [AttributeUsage(AttributeTargets.Method)]
    public class RPC : Attribute {}

    /**
    Class attribute to denote the type of the service.
    **/
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
    public class ServiceType : Attribute {
        public String Type;

        /**
        Constructor.
        
        @param  type    The type of the service.
        **/
        public ServiceType(String type) {
            Type = type;
        }
    }

    /**
    Class attribute to denote the version of the service.
    **/
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
    public class ServiceVersion : Attribute {
        public String Version;

        /**
        Constructor.
        
        @param  version The version of the service.
        **/
        public ServiceVersion(String version) {
            Version = version;
        }
    }
}