using ProtoBuf;

namespace Networking.Broadcasting.Packets.ReliableFragmented
{
    [ProtoContract]
    public class ReliableFragmentedPacket : BasePacket
    {
        protected override int SizeOverhead { get; } = 12;

        [ProtoMember(1)] public int AckId;
        [ProtoMember(2)] public int PartId;
        [ProtoMember(3)] public int PartsNumber;

        public int SendingAttempt;
        public double LastSentTimestamp;

        public override bool IsValid()
        {
            return AckId > 0 && PartId >= 0 && PartsNumber > 0;
        }
    }
}