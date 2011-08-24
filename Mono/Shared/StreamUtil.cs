using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Protophase.Shared {
    public static class StreamUtil {
        private static IFormatter _formatter = new BinaryFormatter();

        public static void Write<T>(Stream stream, T obj) where T : class {
            _formatter.Serialize(stream, obj);
        }

        public static void WriteWithNullCheck<T>(Stream stream, T obj) {
            if(obj != null) {
                stream.WriteByte(1);
                _formatter.Serialize(stream, obj);
            } else {
                stream.WriteByte(0);
            }
        }

        public static T Read<T>(Stream stream) where T : class {
            return _formatter.Deserialize(stream) as T;
        }
    }
}

