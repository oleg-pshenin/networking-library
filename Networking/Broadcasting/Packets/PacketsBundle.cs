using System.Collections.Generic;
using ProtoBuf;

namespace Networking.Broadcasting.Packets
{
    [ProtoContract]
    public class PacketsBundle
    {
        [ProtoMember(1)] public readonly List<BasePacket> Packets = new();
    }
}