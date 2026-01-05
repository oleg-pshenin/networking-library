using ProtoBuf;

namespace Networking.Broadcasting.Packets.Reliable
{
    [ProtoContract]
    public class ReliableAckPacket : BasePacket
    {
        protected override int SizeOverhead { get; } = 4;

        [ProtoMember(1)] public int AckId;

        public override bool IsValid()
        {
            return AckId > 0;
        }
    }
}