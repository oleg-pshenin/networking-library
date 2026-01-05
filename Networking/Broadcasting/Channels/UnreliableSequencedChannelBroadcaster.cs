using System;
using System.Collections.Generic;
using Networking.Broadcasting.Packets;
using Networking.Broadcasting.Packets.UnreliableSequenced;
using Networking.Connections;
using Networking.Data.Core;
using Networking.Utils;

namespace Networking.Broadcasting.Channels
{
    /// <summary>
    /// Almost the same as regular udp except for tracking for sequence
    /// It means that only the last one in a sequence will be used, if previous will come, it will be discarded
    /// Good to use for player updates as you don't want to receive package N and then override it with N-1
    /// </summary>
    public class UnreliableSequencedChannelBroadcaster : BaseChannelBroadcaster
    {
        /// <summary>
        /// Connection -> SequenceIndex -> highest received SequenceValue
        /// </summary>
        private readonly Dictionary<Connection, Dictionary<int, int>> _receivedSequencesCache = new();
        /// <summary>
        /// Connection -> SequenceIndex -> last sent SequenceValue
        /// </summary>
        private readonly Dictionary<int, int> _sequences = new();

        public override void AddConnection(Connection connection)
        {
            _receivedSequencesCache.Add(connection, new Dictionary<int, int>());
        }

        public override void RemoveConnection(Connection connection)
        {
            _receivedSequencesCache.Remove(connection);
        }

        public override void AddDataToSend(Connection connection, NetworkData data)
        {
            data.SequenceIndex = data.GetSequenceIndex();
            if (data.SequenceIndex == 0)
            {
                Logger.LogError($"Attempt to send network data via sequenced protocol without specifying sequence index, it will be ignored: {data}");
                return;
            }

            // excluding same sequence packets
            QueuedDataToBroadcast.RemoveAll(x => x.connection == connection && x.baseData.SequenceIndex == data.SequenceIndex);
            QueuedDataToBroadcast.Add((connection, data));
        }

        public override List<(Connection connection, BasePacket packet)> GetPacketsToBroadcast()
        {
            PacketsToBroadcastCached.Clear();
            foreach (var (connection, baseData) in QueuedDataToBroadcast)
            {
                var packet = new UnreliableSequencedPacket()
                {
                    SequenceIndex = baseData.SequenceIndex,
                    SequenceValue = GetNextSequenceValue(baseData.SequenceIndex),
                    IsCompressed = baseData.ShouldUseCompression,
                    Data = SerializeNetworkData(connection, baseData),
                };

                PacketsToBroadcastCached.Add((connection, packet));
            }

            QueuedDataToBroadcast.Clear();

            return PacketsToBroadcastCached;
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
            switch (packet)
            {
                case UnreliableSequencedPacket unreliableSequencedPacket:
                    var data = DeserializeNetworkData(connection, unreliableSequencedPacket);
                    if (IsValidReceivedSequence(connection, unreliableSequencedPacket.SequenceIndex, unreliableSequencedPacket.SequenceValue))
                    {
                        return data;
                    }
                    else
                    {
                        Logger.LogError($"Incorrect sequence value: {data}");
                        return null;
                    }
                default:
                    Logger.LogError($"Unexpected packet type: {packet.GetType()}. Only UnreliablePacket is expected");
                    return DeserializeNetworkData(connection, packet);
            }
        }

        private bool IsValidReceivedSequence(Connection connection, int sequenceIndex, int sequenceValue)
        {
            if (!_receivedSequencesCache.ContainsKey(connection))
            {
                Logger.LogError($"Couldn't find sequence from connection: {connection}, please make sure it is registered");
                return false;
            }

            var sequences = _receivedSequencesCache[connection];

            if (!sequences.ContainsKey(sequenceIndex))
            {
                sequences[sequenceIndex] = 0;
            }

            var result = sequences[sequenceIndex] < sequenceValue;
            sequences[sequenceIndex] = Math.Max(sequences[sequenceIndex], sequenceValue);
            return result;
        }
    }
}