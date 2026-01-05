using ProtoBuf;

namespace Networking.Broadcasting.Packets.Unreliable
{
    [ProtoContract]
    public class UnreliablePacket : BasePacket
    {
        protected override int SizeOverhead { get; } = 0;

        public override bool IsValid()
        {
            return true;
        }
    }
}