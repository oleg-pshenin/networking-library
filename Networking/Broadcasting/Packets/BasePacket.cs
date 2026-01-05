using Networking.Broadcasting.Packets.Reliable;
using Networking.Broadcasting.Packets.ReliableFragmented;
using Networking.Broadcasting.Packets.Unreliable;
using Networking.Broadcasting.Packets.UnreliableSequenced;
using ProtoBuf;

namespace Networking.Broadcasting.Packets
{
    [ProtoContract]
    [ProtoInclude(10, typeof(UnreliablePacket))]
    [ProtoInclude(11, typeof(UnreliableSequencedPacket))]
    [ProtoInclude(12, typeof(ReliablePacket))]
    [ProtoInclude(13, typeof(ReliableAckPacket))]
    [ProtoInclude(14, typeof(ReliableFragmentedPacket))]
    [ProtoInclude(15, typeof(ReliableFragmentedAckPacket))]
    [ProtoInclude(16, typeof(ReliableSequencedPacket))]
    [ProtoInclude(17, typeof(ReliableSequencedAckPacket))]
    public abstract class BasePacket
    {
        private const int PerPacketSizeOverhead = 8;
        protected abstract int SizeOverhead { get; }

        [ProtoMember(1)] public bool IsCompressed;
        [ProtoMember(2)] public byte[] Data;

        public int GetPacketSize()
        {
            var dataSize = Data?.Length ?? 0;
            return dataSize + SizeOverhead + PerPacketSizeOverhead;
        }

        public abstract bool IsValid();
    }
}