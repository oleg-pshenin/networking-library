using System.Collections.Generic;
using Networking.Broadcasting.Packets;
using Networking.Broadcasting.Packets.Reliable;
using Networking.Connections;
using Networking.Data.Core;
using Networking.Utils;

namespace Networking.Broadcasting.Channels
{
    /// <summary>
    /// Sends packet with ack id
    /// Waits for some time
    /// If time exceeds AckTimeout then sends packet again with the same ack id
    /// Until ack received back
    /// On receive sends back ack packet with received id
    /// </summary>
    public class ReliableChannelBroadcaster : BaseChannelBroadcaster
    {
        /// <summary>
        /// How much time should pass after sending reliable packet before it is considered lost and should be resent in
        /// milliseconds
        /// </summary>
        internal static double AckTimeout = 200;

        /// <summary>
        /// Connection -> ack id -> packet
        /// </summary>
        private readonly Dictionary<Connection, Dictionary<int, ReliablePacket>> _waitingForAckPackets = new();
        /// <summary>
        /// Connection -> Hash of received ack ids
        /// </summary>
        private readonly Dictionary<Connection, HashSet<int>> _receivedPacketsCache = new();
        /// <summary>
        /// Connection -> List of ack ids
        /// </summary>
        private readonly Dictionary<Connection, List<int>> _ackPacketsToSend = new();

        private int _ackId;

        public override void AddConnection(Connection connection)
        {
            _waitingForAckPackets.Add(connection, new Dictionary<int, ReliablePacket>());
            _receivedPacketsCache.Add(connection, new HashSet<int>());
            _ackPacketsToSend.Add(connection, new List<int>());
        }

        public override void RemoveConnection(Connection connection)
        {
            _waitingForAckPackets[connection].Clear();
            _waitingForAckPackets.Remove(connection);

            _receivedPacketsCache[connection].Clear();
            _receivedPacketsCache.Remove(connection);

            _ackPacketsToSend[connection].Clear();
            _ackPacketsToSend.Remove(connection);
        }

        public override void AddDataToSend(Connection connection, NetworkData data)
        {
            VerifyConnection(connection);
            QueuedDataToBroadcast.Add((connection, data));
        }

        private void VerifyConnection(Connection connection)
        {
            if (!_ackPacketsToSend.ContainsKey(connection))
            {
                Logger.LogError($"Unhandled connection by ReliableChannelBroadcaster: {connection}");
                AddConnection(connection);
            }
        }

        public override List<(Connection connection, BasePacket packet)> GetPacketsToBroadcast()
        {
            PacketsToBroadcastCached.Clear();
            BroadcastAckPackets();
            BroadcastAgainTimeExceededQueuedData();
            BroadcastQueuedData();
            return PacketsToBroadcastCached;
        }

        private void BroadcastAckPackets()
        {
            foreach (var connection in _ackPacketsToSend.Keys)
            {
                foreach (var ackId in _ackPacketsToSend[connection])
                {
                    var packet = new ReliableAckPacket()
                    {
                        AckId = ackId,
                    };

                    PacketsToBroadcastCached.Add((connection, packet));
                }

                _ackPacketsToSend[connection].Clear();
            }
        }

        private void BroadcastAgainTimeExceededQueuedData()
        {
            var timestamp = TimeUtils.GetUtcTimestampMs();

            foreach (var connection in _waitingForAckPackets.Keys)
            {
                foreach (var packet in _waitingForAckPackets[connection].Values)
                {
                    var ackTimeout = connection.AckTimeoutOverride > 0 ? connection.AckTimeoutOverride : AckTimeout;

                    if (timestamp > packet.LastSentTimestamp + ackTimeout)
                    {
                        packet.SendingAttempt++;
                        packet.LastSentTimestamp = timestamp;

                        Logger.LogWarning($"Attempt to send ReliablePacket {packet.SendingAttempt} time with ack id: {packet.AckId} to: {connection}");
                        PacketsToBroadcastCached.Add((connection, packet));
                    }
                }
            }
        }

        private void BroadcastQueuedData()
        {
            var timestamp = TimeUtils.GetUtcTimestampMs();
            foreach (var (connection, baseData) in QueuedDataToBroadcast)
            {
                _ackId++;

                var packet = new ReliablePacket()
                {
                    SendingAttempt = 1,
                    LastSentTimestamp = timestamp,
                    AckId = _ackId,

                    IsCompressed = baseData.ShouldUseCompression,
                    Data = SerializeNetworkData(connection, baseData),
                };

                PacketsToBroadcastCached.Add((connection, packet));

                _waitingForAckPackets[connection].Add(_ackId, packet);
            }

            QueuedDataToBroadcast.Clear();
        }

        public override NetworkData GetDataFromPacket(Connection connection, BasePacket packet)
        {
            VerifyConnection(connection);
            switch (packet)
            {
                case ReliablePacket reliablePacket:
                {
                    var ackId = reliablePacket.AckId;
                    _ackPacketsToSend[connection].Add(ackId);

                    if (_receivedPacketsCache[connection].Contains(ackId))
                    {
                        Logger.LogWarning($"Received ReliablePacket with the same ack id {ackId} second time from: {connection}");
                        return null;
                    }

                    _receivedPacketsCache[connection].Add(ackId);

                    return DeserializeNetworkData(connection, reliablePacket);
                }
                case ReliableAckPacket reliableAckPacket:
                {
                    var ackId = reliableAckPacket.AckId;
                    if (!_waitingForAckPackets[connection].ContainsKey(ackId))
                    {
                        Logger.LogWarning($"Couldn't find packet waiting for acknowledgement with id: {ackId} from: {connection}");
                    }
                    else
                    {
                        _waitingForAckPackets[connection].Remove(ackId);
                    }

                    return null;
                }
                default:
                    Logger.LogError($"Unexpected packet type: {packet.GetType()}. Only ReliablePacket and ReliableAckPacket is expected");
                    return DeserializeNetworkData(connection, packet);
            }
        }
    }
}