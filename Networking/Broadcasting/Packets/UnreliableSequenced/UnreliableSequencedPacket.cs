using ProtoBuf;

namespace Networking.Broadcasting.Packets.UnreliableSequenced
{
    [ProtoContract]
    public class UnreliableSequencedPacket : BasePacket
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