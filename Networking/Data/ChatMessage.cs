using Networking.Data.Core;
using ProtoBuf;

namespace Networking.Data
{
    [ProtoContract]
    public class ChatMessage : NetworkData
    {
        [ProtoMember(1)] public string Author;
        [ProtoMember(2)] public string Message;

        public override string ToString()
        {
            return $"NetworkData: {GetType()}, Author: {Author}, Message: {Message}";
        }
    }
}