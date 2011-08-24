using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Protophase.Shared {
    public static class StreamUtil {
        private static IFormatter _formatter = new BinaryFormatter();

        public static MemoryStream CreateStream(byte[] message) {
            return new MemoryStream(message);
        }

        public static void Write<T>(Stream stream, T obj) where T : class {
            _formatter.Serialize(stream, obj);
        }

        public static void WriteWithNullCheck<T>(Stream stream, T obj) {
            if(obj != null) {
                WriteBool(stream, true);
                _formatter.Serialize(stream, obj);
            } else {
                WriteBool(stream, false);
            }
        }

        public static void WriteBool(Stream stream, bool b) {
            stream.WriteByte(b ? (byte)1 : (byte)0);
        }

        public static T Read<T>(Stream stream) where T : class {
            return _formatter.Deserialize(stream) as T;
        }

        public static T ReadWithNullCheck<T>(Stream stream) where T : class {
            if(!ReadBool(stream)) return null;
            else return _formatter.Deserialize(stream) as T;
        }

        public static bool ReadBool(Stream stream) {
            return stream.ReadByte() == 1 ? true : false;
        }
    }
}

