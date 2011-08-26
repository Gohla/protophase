using System;

namespace Protophase.Shared {
    [AttributeUsage(AttributeTargets.Method)]
    public class RPC : Attribute {}

    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
    public class ServiceType : Attribute {
        public String Type;

        public ServiceType(String type) {
            Type = type;
        }
    }

    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
    public class ServiceVersion : Attribute {
        public String Version;

        public ServiceVersion(String version) {
            Version = version;
        }
    }
}