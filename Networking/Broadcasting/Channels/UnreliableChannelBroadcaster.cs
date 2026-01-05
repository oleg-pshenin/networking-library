using System.Collections.Generic;
using Networking.Broadcasting.Packets;
using Networking.Broadcasting.Packets.Unreliable;
using Networking.Connections;
using Networking.Data.Core;
using Networking.Utils;

namespace Networking.Broadcasting.Channels
{
    /// <summary>
    /// Simple protocol, acts as basic udp, supports compression as any other protocol
    /// Good to use for streaming not important data, such as frequent decorative stuff
    /// </summary>
    public class UnreliableChannelBroadcaster : BaseChannelBroadcaster
    {
        public override void AddDataToSend(Connection connection, NetworkData data)
        {
            QueuedDataToBroadcast.Add((connection, data));
        }

        public override List<(Connection connection, BasePacket packet)> GetPacketsToBroadcast()
        {
            PacketsToBroadcastCached.Clear();
            foreach (var (connection, baseData) in QueuedDataToBroadcast)
            {
                var packet = new UnreliablePacket()
                {
                    IsCompressed = baseData.ShouldUseCompression,
                    Data = SerializeNetworkData(connection, baseData),
                };

                PacketsToBroadcastCached.Add((connection, packet));
            }

            QueuedDataToBroadcast.Clear();

            return PacketsToBroadcastCached;
        }

        public override NetworkData GetDataFromPacket(Connection connection, BasePacket packet)
        {
            switch (packet)
            {
                case UnreliablePacket unreliablePacket:
                    return DeserializeNetworkData(connection, unreliablePacket);
                default:
                    Logger.LogError($"Unexpected packet type: {packet.GetType()}. Only UnreliablePacket is expected");
                    return DeserializeNetworkData(connection, packet);
            }
        }

        public override void AddConnection(Connection connection)
        {
        }

        public override void RemoveConnection(Connection connection)
        {
        }
    }
}