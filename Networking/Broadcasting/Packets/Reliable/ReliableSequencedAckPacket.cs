using ProtoBuf;

namespace Networking.Broadcasting.Packets.Reliable
{
    [ProtoContract]
    public class ReliableSequencedAckPacket : BasePacket
    {
        protected override int SizeOverhead { get; } = 8;

        [ProtoMember(1)] public int SequenceIndex;
        [ProtoMember(2)] public int SequenceValue;

        public override bool IsValid()
        {
            return SequenceIndex != 0 && SequenceValue >= 0;
        }
    }
}