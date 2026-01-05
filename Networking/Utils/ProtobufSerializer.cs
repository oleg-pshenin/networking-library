using System;
using System.IO;
using ProtoBuf;

namespace Networking.Utils
{
    public static class ProtobufSerializer
    {
        // TODO: figure out with options for pooling


        public static byte[] ObjectToByteArray(object obj)
        {
            using var memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, obj);
            return memoryStream.ToArray();
        }

        public static T ByteArrayToObject<T>(byte[] arrBytes) where T : class
        {
            using var memoryStream = new MemoryStream(arrBytes);
            return Serializer.Deserialize<T>(memoryStream);
        }

        public static object ByteArrayToObject(Type type, byte[] arrBytes)
        {
            using var memoryStream = new MemoryStream(arrBytes);
            return Serializer.Deserialize(type, memoryStream);
        }
    }
}