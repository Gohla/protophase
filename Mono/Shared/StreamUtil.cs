using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Protophase.Shared {
    /**
    Utility class for binary serialization.
    **/
    public static class StreamUtil {
        private static IFormatter _formatter = new BinaryFormatter();

        /**
        Creates a stream from given byte array.
        
        @param  message The message to create a stream from.
        
        @return Memory stream containing given message.
        **/
        public static MemoryStream CreateStream(byte[] message) {
            return new MemoryStream(message);
        }

        /**
        Writes an object to the stream. Cannot be used if obj can be null, use WriteWithNullCheck instead.
        
        @tparam T   Type of the object.
        @param  stream  The stream to write to.
        @param  obj     The object to write. Cannot be null!
        **/
        public static void Write<T>(Stream stream, T obj) where T : class {
            _formatter.Serialize(stream, obj);
        }

        /**
        Writes an object to the stream with support for null objects.
        
        @tparam T   Type of the object.
        @param  stream  The stream to write to.
        @param  obj     The object to write.
        **/
        public static void WriteWithNullCheck<T>(Stream stream, T obj) {
            if(obj != null) {
                WriteBool(stream, true);
                _formatter.Serialize(stream, obj);
            } else {
                WriteBool(stream, false);
            }
        }

        /**
        Writes a bool to the stream.
        
        @param  stream  The stream to write to.
        @param  b       The boolean to write.
        **/
        public static void WriteBool(Stream stream, bool b) {
            stream.WriteByte(b ? (byte)1 : (byte)0);
        }

        /**
        Reads an object from the stream. Cannot be used if the object to read can be null, use ReadWithNullCheck
        instead.
        
        @tparam T   Type to convert the read object to.
        @param  stream  The stream to read from.
        
        @return Written object.
        **/
        public static T Read<T>(Stream stream) where T : class {
            return (T)_formatter.Deserialize(stream);
        }

        /**
        Reads an object from the stream with support for null objects.
        
        @tparam T   Type to convert the read object to.
        @param  stream  The stream to read from.
        
        @return Written object, or null if null object was sent.
        **/
        public static T ReadWithNullCheck<T>(Stream stream) where T : class {
            if(!ReadBool(stream)) return null;
            else return (T)_formatter.Deserialize(stream);
        }

        /**
        Reads a bool from the stream.
        
        @param  stream  The stream to read from.
        
        @return Written bool.
        **/
        public static bool ReadBool(Stream stream) {
            return stream.ReadByte() == 1 ? true : false;
        }
    }
}

