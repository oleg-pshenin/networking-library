using ProtoBuf;

namespace Networking.Broadcasting.Packets.ReliableFragmented
{
    [ProtoContract]
    public class ReliableFragmentedAckPacket : BasePacket
    {
        protected override int SizeOverhead { get; } = 8;

        [ProtoMember(1)] public int AckId;
        [ProtoMember(2)] public int PartId;

        public override bool IsValid()
        {
            return AckId > 0 && PartId >= 0;
        }
    }
}