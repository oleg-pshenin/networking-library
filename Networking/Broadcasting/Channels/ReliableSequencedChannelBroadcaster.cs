using System.Collections.Generic;
using Networking.Broadcasting.Packets;
using Networking.Broadcasting.Packets.Reliable;
using Networking.Connections;
using Networking.Data.Core;
using Networking.Utils;

namespace Networking.Broadcasting.Channels
{
    /// <summary>
    /// Sends packet with sequence index and sequence value
    /// Waits for some time
    /// If time is exceeds AckTimeout then sends again last packet from sequence?
    /// Or waits for ack only for the last in sequence?
    /// IF ack is received - resets waiting up to this number, if it is the same then we don't need to resend anything
    /// </summary>
    public class ReliableSequencedChannelBroadcaster : BaseChannelBroadcaster
    {
        /// <summary>
        /// How much time should pass after sending reliable packet before it is considered lost and should be resent in
        /// milliseconds
        /// </summary>
        internal static double AckTimeout = 200;

        /// <summary>
        /// Connection -> SequenceIndex -> highest by SequenceValue packet
        /// </summary>
        private readonly Dictionary<Connection, Dictionary<int, ReliableSequencedPacket>> _waitingForAckPackets = new();
        /// <summary>
        /// Connection -> SequenceIndex -> highest received SequenceValue
        /// </summary>
        private readonly Dictionary<Connection, Dictionary<int, int>> _receivedPacketsCache = new();
        /// <summary>
        /// Connection -> SequenceIndex -> highest received SequenceValue
        /// </summary>
        private readonly Dictionary<Connection, Dictionary<int, int>> _ackPacketsToSend = new();
        /// <summary>
        /// Connection -> SequenceIndex -> last sent SequenceValue
        /// </summary>
        private readonly Dictionary<int, int> _sequences = new();

        public override void AddConnection(Connection connection)
        {
            _waitingForAckPackets.Add(connection, new Dictionary<int, ReliableSequencedPacket>());
            _receivedPacketsCache.Add(connection, new Dictionary<int, int>());
            _ackPacketsToSend.Add(connection, new Dictionary<int, int>());
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

        private void VerifyConnection(Connection connection)
        {
            if (!_ackPacketsToSend.ContainsKey(connection))
            {
                Logger.LogError($"Unhandled connection by ReliableSequencedChannelBroadcaster: {connection}");
                AddConnection(connection);
            }
        }

        public override void AddDataToSend(Connection connection, NetworkData data)
        {
            data.SequenceIndex = data.GetSequenceIndex();
            if (data.SequenceIndex == 0)
            {
                Logger.LogError($"Attempt to send network data via sequenced protocol without specifying sequence index, it will be ignored: {data}");
                return;
            }

            VerifyConnection(connection);

            QueuedDataToBroadcast.RemoveAll(x => x.connection == connection && x.baseData.SequenceIndex == data.SequenceIndex);
            QueuedDataToBroadcast.Add((connection, data));
        }

        public override List<(Connection connection, BasePacket packet)> GetPacketsToBroadcast()
        {
            PacketsToBroadcastCached.Clear();
            BroadcastAckPackets();
            BroadcastQueuedData();
            // order changed from other reliable protocols because timeout queue can change depends on sent data
            BroadcastAgainTimeExceededQueuedData();
            return PacketsToBroadcastCached;
        }

        private void BroadcastAckPackets()
        {
            foreach (var connection in _ackPacketsToSend.Keys)
            {
                foreach (var sequenceData in _ackPacketsToSend[connection])
                {
                    var packet = new ReliableSequencedAckPacket()
                    {
                        SequenceIndex = sequenceData.Key,
                        SequenceValue = sequenceData.Value,
                    };

                    PacketsToBroadcastCached.Add((connection, packet));
                }

                _ackPacketsToSend[connection].Clear();
            }
        }

        private void BroadcastQueuedData()
        {
            var timestamp = TimeUtils.GetUtcTimestampMs();
            foreach (var (connection, baseData) in QueuedDataToBroadcast)
            {
                var packet = new ReliableSequencedPacket()
                {
                    SequenceIndex = baseData.SequenceIndex,
                    SequenceValue = GetNextSequenceValue(baseData.SequenceIndex),
                    SendingAttempt = 1,
                    LastSentTimestamp = timestamp,

                    IsCompressed = baseData.ShouldUseCompression,
                    Data = SerializeNetworkData(connection, baseData),
                };

                PacketsToBroadcastCached.Add((connection, packet));

                _waitingForAckPackets[connection][baseData.SequenceIndex] = packet;
            }

            QueuedDataToBroadcast.Clear();
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

                        Logger.LogWarning(
                            $"Attempt to send ReliableSequencedPacket {packet.SendingAttempt} time with sequence index and value: {packet.SequenceIndex}, {packet.SequenceValue} to: {connection}");
                        PacketsToBroadcastCached.Add((connection, packet));
                    }
                }
            }
        }

        private int GetNextSequenceValue(int sequenceIndex)
        {
            if (!_sequences.ContainsKey(sequenceIndex))
                _sequences.Add(sequenceIndex, 0);

            _sequences[sequenceIndex]++;
            return _sequences[sequenceIndex];
        }

        public override NetworkData GetDataFromPacket(Connection connection, BasePacket packet)
        {
            VerifyConnection(connection);
            switch (packet)
            {
                case ReliableSequencedPacket reliableSequencedPacket:
                {
                    var sequenceIndex = reliableSequencedPacket.SequenceIndex;
                    var sequenceValue = reliableSequencedPacket.SequenceValue;

                    if (!_ackPacketsToSend[connection].ContainsKey(sequenceIndex))
                        _ackPacketsToSend[connection][sequenceIndex] = 0;

                    if (!_receivedPacketsCache[connection].ContainsKey(sequenceIndex))
                        _receivedPacketsCache[connection][sequenceIndex] = 0;

                    if (_receivedPacketsCache[connection][sequenceIndex] >= sequenceValue)
                    {
                        _ackPacketsToSend[connection][sequenceIndex] = _receivedPacketsCache[connection][sequenceIndex];
                        Logger.LogWarning($"Received ReliableSequencedPacket with sequence value bigger or equal to already received {sequenceIndex}, {sequenceValue} from: {connection}");
                        return null;
                    }

                    _receivedPacketsCache[connection][sequenceIndex] = sequenceValue;
                    _ackPacketsToSend[connection][sequenceIndex] = sequenceValue;

                    return DeserializeNetworkData(connection, reliableSequencedPacket);
                }
                case ReliableSequencedAckPacket reliableSequencedAckPacket:
                {
                    var sequenceIndex = reliableSequencedAckPacket.SequenceIndex;
                    var sequenceValue = reliableSequencedAckPacket.SequenceValue;

                    if (!_waitingForAckPackets[connection].ContainsKey(sequenceIndex))
                    {
                        Logger.LogError($"Received ReliableSequencedAckPacket with unknown sequence index: {sequenceIndex}");
                        return null;
                    }

                    var waitingForAckPacketInSequence = _waitingForAckPackets[connection][sequenceIndex];
                    if (waitingForAckPacketInSequence.SequenceValue < sequenceValue)
                    {
                        Logger.LogError(
                            $"Received ReliableSequencedAckPacket with sequence value bigger than waiting for ack: {sequenceIndex}, {sequenceValue}, {waitingForAckPacketInSequence.SequenceValue}");
                    }
                    else if (waitingForAckPacketInSequence.SequenceValue == sequenceValue)
                    {
                        _waitingForAckPackets[connection].Remove(sequenceIndex);
                    }
                    else
                    {
                        Logger.LogError($"Received ReliableSequencedAckPacket with outdated sequence value: {sequenceValue}, {waitingForAckPacketInSequence.SequenceValue}");
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