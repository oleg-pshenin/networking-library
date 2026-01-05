using ProtoBuf;

namespace Networking.Broadcasting.Packets.Reliable
{
    [ProtoContract]
    public class ReliablePacket : BasePacket
    {
        protected override int SizeOverhead { get; } = 4;

        [ProtoMember(1)] public int AckId;

        public int SendingAttempt;
        public double LastSentTimestamp;

        public override bool IsValid()
        {
            return AckId > 0;
        }
    }
}