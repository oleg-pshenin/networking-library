using System;
using System.Collections.Generic;
using System.Linq;
using Networking.Broadcasting.Packets;
using Networking.Broadcasting.Packets.ReliableFragmented;
using Networking.Connections;
using Networking.Data.Core;
using Networking.Utils;

namespace Networking.Broadcasting.Channels
{
    public class ReliableFragmentedChannelBroadcaster : BaseChannelBroadcaster
    {
        /// <summary>
        /// How much time should pass after sending reliable packet before it is considered lost and should be resent in
        /// milliseconds
        /// </summary>
        internal static double AckTimeout = 1000;

        protected override bool FragmentedChannel => true;

        private readonly Dictionary<Connection, Dictionary<int, List<ReliableFragmentedPacket>>> _waitingForAckPackets = new();
        private readonly Dictionary<Connection, Dictionary<int, List<ReliableFragmentedPacket>>> _assemblingCache = new();
        private readonly Dictionary<Connection, HashSet<int>> _assembledCached = new();
        private readonly Dictionary<Connection, List<(int ackId, int partId)>> _ackPacketsToSend = new();

        private int _ackId;

        public override void AddConnection(Connection connection)
        {
            _waitingForAckPackets.Add(connection, new Dictionary<int, List<ReliableFragmentedPacket>>());
            _assemblingCache.Add(connection, new Dictionary<int, List<ReliableFragmentedPacket>>());
            _assembledCached.Add(connection, new HashSet<int>());

            _ackPacketsToSend.Add(connection, new List<(int, int)>());
        }

        public override void RemoveConnection(Connection connection)
        {
            _waitingForAckPackets[connection].Clear();
            _waitingForAckPackets.Remove(connection);

            _assemblingCache[connection].Clear();
            _assemblingCache.Remove(connection);

            _assembledCached[connection].Clear();
            _assembledCached.Remove(connection);

            _ackPacketsToSend[connection].Clear();
            _ackPacketsToSend.Remove(connection);
        }

        private void VerifyConnection(Connection connection)
        {
            if (!_waitingForAckPackets.ContainsKey(connection))
            {
                Logger.LogError($"Unhandled connection by ReliableBaseChannelBroadcaster: {connection}");
                AddConnection(connection);
            }
        }

        public override void AddDataToSend(Connection connection, NetworkData data)
        {
            VerifyConnection(connection);

            if (!data.ShouldUseCompression)
            {
                Logger.LogError($"Fragmented data usually expected to use compression: {data}");
            }

            QueuedDataToBroadcast.Add((connection, data));
        }

        public override List<(Connection connection, BasePacket packet)> GetPacketsToBroadcast()
        {
            PacketsToBroadcastCached.Clear();

            BroadcastAckPackets();
            BroadcastAgainTimeExceededQueuedData();
            FragmentQueuedData();

            return PacketsToBroadcastCached;
        }

        private void BroadcastAckPackets()
        {
            foreach (var connection in _ackPacketsToSend.Keys)
            {
                foreach (var (ackId, partId) in _ackPacketsToSend[connection])
                {
                    var packet = new ReliableFragmentedAckPacket()
                    {
                        AckId = ackId,
                        PartId = partId
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
                foreach (var packets in _waitingForAckPackets[connection].Values)
                {
                    var ackTimeout = connection.AckTimeoutOverride > 0 ? connection.AckTimeoutOverride : AckTimeout;

                    foreach (var packet in packets)
                    {
                        if (timestamp > packet.LastSentTimestamp + ackTimeout)
                            continue;

                        packet.SendingAttempt++;
                        packet.LastSentTimestamp = timestamp;

                        Logger.LogWarning($"Attempt to send reliable packet {packet.SendingAttempt} time with ack id: {packet.AckId} to: {connection}");
                        PacketsToBroadcastCached.Add((connection, packet));
                    }
                }
            }
        }

        private void FragmentQueuedData()
        {
            var timestamp = TimeUtils.GetUtcTimestampMs();
            foreach (var (connection, baseData) in QueuedDataToBroadcast)
            {
                _ackId++;

                _waitingForAckPackets[connection][_ackId] = new List<ReliableFragmentedPacket>();

                var data = SerializeNetworkData(connection, baseData);
                if (data.Length <= MaxPacketDataSize)
                {
                    Logger.LogError($"Attempt to send data less then max packet size over fragmented: {baseData}");
                }

                var partsNumber = Convert.ToInt32(Math.Ceiling((double)data.Length / MaxPacketDataSize));

                for (var i = 0; i < partsNumber; i++)
                {
                    var dataFragment = data.Skip(i * MaxPacketDataSize).Take(MaxPacketDataSize).ToArray();
                    var packet = new ReliableFragmentedPacket()
                    {
                        IsCompressed = baseData.ShouldUseCompression,
                        AckId = _ackId,
                        Data = dataFragment,
                        PartId = i,
                        PartsNumber = partsNumber,

                        // Metadata
                        SendingAttempt = 1,
                        LastSentTimestamp = timestamp,
                    };

                    PacketsToBroadcastCached.Add((connection, packet));
                    _waitingForAckPackets[connection][_ackId].Add(packet);
                }
            }

            QueuedDataToBroadcast.Clear();
        }


        public override NetworkData GetDataFromPacket(Connection connection, BasePacket packet)
        {
            VerifyConnection(connection);
            switch (packet)
            {
                case ReliableFragmentedPacket reliableFragmentedPacket:
                {
                    var ackId = reliableFragmentedPacket.AckId;
                    var partId = reliableFragmentedPacket.PartId;
                    var partsNumber = reliableFragmentedPacket.PartsNumber;

                    _ackPacketsToSend[connection].Add((ackId, partId));

                    if (_assembledCached[connection].Contains(ackId))
                    {
                        Logger.LogWarning($"Received ReliableFragmentedPacket with the same ack id {ackId}, part id {partId} for already assembled data second time from: {connection}");
                        return null;
                    }

                    if (!_assemblingCache[connection].ContainsKey(ackId))
                    {
                        _assemblingCache[connection].Add(ackId, new List<ReliableFragmentedPacket>(partsNumber));
                    }

                    if (_assemblingCache[connection][ackId].All(x => x.PartId != partId))
                    {
                        _assemblingCache[connection][ackId].Add(reliableFragmentedPacket);

                        if (_assemblingCache[connection][ackId].Count == partsNumber)
                        {
                            var totalSize = 0;
                            foreach (var assemblingPacket in _assemblingCache[connection][ackId])
                            {
                                totalSize += assemblingPacket.Data.Length;
                            }

                            // TODO: figure out with options for pooling
                            var assembledArray = new byte[totalSize];

                            var offset = 0;
                            foreach (var assemblingPacket in _assemblingCache[connection][ackId].OrderBy(x => x.PartId))
                            {
                                assemblingPacket.Data.CopyTo(assembledArray, offset);
                                offset += assemblingPacket.Data.Length;
                            }

                            var networkData = DeserializeNetworkData(connection, reliableFragmentedPacket.IsCompressed, assembledArray);

                            Logger.LogError($"Assembled data of size {assembledArray.Length} in {partsNumber} fragments from {connection}. Data: {networkData}");
                            _assembledCached[connection].Add(ackId);
                            return networkData;
                        }

                        return null;
                    }

                    Logger.LogWarning($"Received ReliableFragmentedPacket with the same ack id {ackId} and the same partId {partId} second time from: {connection}");
                    return null;
                }
                case ReliableFragmentedAckPacket reliableFragmentedAckPacket:
                {
                    var ackId = reliableFragmentedAckPacket.AckId;
                    if (_waitingForAckPackets[connection].ContainsKey(ackId))
                    {
                        for (var i = 0; i < _waitingForAckPackets[connection][ackId].Count; i++)
                        {
                            if (_waitingForAckPackets[connection][ackId][i].PartId == reliableFragmentedAckPacket.PartId)
                            {
                                _waitingForAckPackets[connection][ackId].RemoveAt(i);
                                if (_waitingForAckPackets[connection][ackId].Count == 0)
                                {
                                    _waitingForAckPackets[connection].Remove(ackId);
                                    Logger.LogError($"Fully transferred to: {connection}:{ackId}");
                                }

                                return null;
                            }
                        }

                        Logger.LogWarning($"Couldn't find packet waiting for acknowledgement with id: {reliableFragmentedAckPacket.AckId} from: {connection.IPEndPoint}");
                    }
                    else
                    {
                        Logger.LogWarning($"Couldn't find packet waiting for acknowledgement with id: {reliableFragmentedAckPacket.AckId} from: {connection.IPEndPoint}");
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