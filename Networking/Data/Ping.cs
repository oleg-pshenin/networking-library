using Networking.Data.Core;
using ProtoBuf;

namespace Networking.Data
{
    [ProtoContract]
    public class Ping : NetworkData
    {
        [ProtoMember(1)] public int Id;
        [ProtoMember(2)] public double Timestamp;
        [ProtoMember(3)] public string Payload;

        public override bool ShouldBeLogged => false;

        public override string ToString()
        {
            return $"NetworkData: {GetType()}, Id: {Id}, Payload Size: {Payload.Length}";
        }
    }
}