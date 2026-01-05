using ProtoBuf;

namespace Networking.Broadcasting.SubNetDiscovery
{
    [ProtoContract]
    public class FindServerRequest : BroadcastMessage
    {
        [ProtoMember(1)] public string MinimumPayload = "00112233";

        public override string ToString()
        {
            return $"BroadcastMessage: {GetType()}";
        }
    }
}