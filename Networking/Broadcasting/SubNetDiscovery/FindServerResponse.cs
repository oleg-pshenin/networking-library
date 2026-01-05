using ProtoBuf;

namespace Networking.Broadcasting.SubNetDiscovery
{
    [ProtoContract]
    public class FindServerResponse : BroadcastMessage
    {
        [ProtoMember(1)] public string Name;
        [ProtoMember(2)] public int CurrentPlayers;
        [ProtoMember(3)] public int MaxPlayers;

        public override string ToString()
        {
            return $"BroadcastMessage: {GetType()}, Name: {Name}, CurrentPlayers: {CurrentPlayers}, MaxPlayers: {MaxPlayers}";
        }
    }
}