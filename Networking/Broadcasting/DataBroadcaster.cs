using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Networking.Broadcasting.Channels;
using Networking.Broadcasting.Packets;
using Networking.Broadcasting.Packets.Reliable;
using Networking.Broadcasting.Packets.ReliableFragmented;
using Networking.Broadcasting.Packets.Unreliable;
using Networking.Broadcasting.Packets.UnreliableSequenced;
using Networking.Connections;
using Networking.Data.Core;
using Networking.Utils;

namespace Networking.Broadcasting
{
    /// <summary>
    /// Need to congestion control per connection
    /// Assuming that SENDER (NOT SERVER) always can allow any output bandwidth
    /// The hardest question here is how to control ack times of reliable channels (if data was sent later)
    /// Congestion is based on reliable packets so we need to count it separately from reliable channels only
    /// Also can include some ping logic to measure that
    /// Sequenced packets can be optimized as well in such scenario but it should be controlled within channel broadcaster
    /// Also we need to always prioritize ack packets to send over anything else to give reliable congestion results
    /// </summary>
    public class DataBroadcaster : IDataBroadcaster, IMainThreadUpdateable
    {
        internal static bool LogPackets;
        internal static bool LogInteractions;

        private readonly Dictionary<BroadcastingChannel, BaseChannelBroadcaster> _broadcasterByChannelMapping = new()
        {
            { BroadcastingChannel.Unreliable, new UnreliableChannelBroadcaster() },
            { BroadcastingChannel.UnreliableSequenced, new UnreliableSequencedChannelBroadcaster() },

            { BroadcastingChannel.Reliable, new ReliableChannelBroadcaster() },
            { BroadcastingChannel.ReliableSequenced, new ReliableSequencedChannelBroadcaster() },
            { BroadcastingChannel.ReliableFragmented, new ReliableFragmentedChannelBroadcaster() },
        };

        private readonly Dictionary<Type, BroadcastingChannel> _channelByPacketTypeMapping = new()
        {
            { typeof(UnreliablePacket), BroadcastingChannel.Unreliable },

            { typeof(UnreliableSequencedPacket), BroadcastingChannel.UnreliableSequenced },

            { typeof(ReliablePacket), BroadcastingChannel.Reliable },
            { typeof(ReliableAckPacket), BroadcastingChannel.Reliable },

            { typeof(ReliableSequencedPacket), BroadcastingChannel.ReliableSequenced },
            { typeof(ReliableSequencedAckPacket), BroadcastingChannel.ReliableSequenced },

            { typeof(ReliableFragmentedPacket), BroadcastingChannel.ReliableFragmented },
            { typeof(ReliableFragmentedAckPacket), BroadcastingChannel.ReliableFragmented },
        };

        private readonly IConnectionManager _connectionManager;
        private readonly PacketsBundler _packetsBundler = new();
        private readonly IUdpMessenger _udpMessenger;

        private readonly Dictionary<Connection, List<BasePacket>> _packetsPerConnection = new();
        private readonly Dictionary<Type, Dictionary<object, Action<Connection, NetworkData>>> _dataListeners = new();

        private readonly List<Type> _cachedDataTypes = new();

        internal DataBroadcaster(IConnectionManager connectionManager, IUdpMessenger udpMessenger)
        {
            _connectionManager = connectionManager;
            _connectionManager.ConnectionAdded += ConnectionAddedHandler;
            _connectionManager.ConnectionRemoved += ConnectionRemovedHandler;

            _udpMessenger = udpMessenger;
            _udpMessenger.MessageReceived += MessageReceivedHandler;
        }

        private void ConnectionAddedHandler(Connection connection)
        {
            _packetsPerConnection.Add(connection, new List<BasePacket>());
            foreach (var channelBroadcaster in _broadcasterByChannelMapping.Values)
            {
                channelBroadcaster.AddConnection(connection);
            }
        }

        private void ConnectionRemovedHandler(Connection connection)
        {
            _packetsPerConnection.Remove(connection);

            foreach (var channelBroadcaster in _broadcasterByChannelMapping.Values)
            {
                channelBroadcaster.RemoveConnection(connection);
            }
        }

        public void AddDataToSend(List<Connection> connections, NetworkData data, BroadcastingChannel broadcastingChannel)
        {
            foreach (var connection in connections)
            {
                AddDataToSend(connection, data, broadcastingChannel);
            }
        }

        public void AddDataToSend(Connection connection, NetworkData data, BroadcastingChannel broadcastingChannel)
        {
            if (LogPackets && data.ShouldBeLogged)
                Logger.Log($"Sending packet: {data}");

            _broadcasterByChannelMapping[broadcastingChannel].AddDataToSend(connection, data);
        }

        public void MainThreadUpdate()
        {
            SendAll();
        }

        /// <summary>
        /// Sending all of the data gathered during the frame/tick, rather than sending individually
        /// Tick rate is not stable but equal to main thread refresh rate
        /// </summary>
        private void SendAll()
        {
            foreach (var connection in _connectionManager.Connections)
            {
                if (!_packetsPerConnection.ContainsKey(connection))
                {
                    Logger.LogError($"Unhandled connection by DataBroadcaster: {connection}");
                    _packetsPerConnection.Add(connection, new List<BasePacket>());
                }
                else
                {
                    _packetsPerConnection[connection].Clear();
                }
            }

            foreach (var channelBroadcaster in _broadcasterByChannelMapping.Values)
            {
                foreach (var packetToBroadcast in channelBroadcaster.GetPacketsToBroadcast())
                {
                    if (!packetToBroadcast.packet.IsValid())
                    {
                        Logger.LogError($"Generated invalid packet: {packetToBroadcast.packet.GetType()}");
                        continue;
                    }

                    if (_packetsPerConnection.ContainsKey(packetToBroadcast.connection))
                    {
                        _packetsPerConnection[packetToBroadcast.connection].Add(packetToBroadcast.packet);
                    }
                    else
                    {
                        Logger.LogError($"Attempt to broadcast packet to unknown connection: {packetToBroadcast.connection}");
                    }
                }
            }

            foreach (var connection in _connectionManager.Connections)
            {
                var bundles = _packetsBundler.Bundle(_packetsPerConnection[connection]);

                foreach (var bundle in bundles)
                {
                    var message = ProtobufSerializer.ObjectToByteArray(bundle);
                    _udpMessenger.Send(connection.IPEndPoint, message);

                    connection.OutcomingTraffic.AddRawData(message.Length, bundle.Packets.Count);
                }
            }
        }

        private void MessageReceivedHandler(IPEndPoint ipEndPoint, byte[] message)
        {
            var packetBundle = ProtobufSerializer.ByteArrayToObject<PacketsBundle>(message);
            if (packetBundle == null)
            {
                // dropping packet as it is incorrect type
                return;
            }
            
            // should it be excluding connections not added manually?
            // except for the cases where it should be able to receive from anyone, like in case of matchmaking server?
            var connection = _connectionManager.GetOrAddConnection(ipEndPoint);
            var packets = _packetsBundler.UnBundle(packetBundle);

            connection.IncomingTraffic.AddRawData(message.Length, packets.Count);

            foreach (var packet in packets)
            {
                if (!packet.IsValid())
                {
                    Logger.LogError($"Received invalid packet: {packet.GetType()}");
                    continue;
                }

                var packetType = packet.GetType();
                if (!_channelByPacketTypeMapping.ContainsKey(packetType))
                {
                    Logger.LogError($"Couldn't find channel for packet type: {packetType}");
                    continue;
                }

                var channel = _channelByPacketTypeMapping[packetType];
                if (!_broadcasterByChannelMapping.ContainsKey(channel))
                {
                    Logger.LogError($"Couldn't find channel broadcaster for channel: {channel}");
                    continue;
                }

                var data = _broadcasterByChannelMapping[channel].GetDataFromPacket(connection, packet);
                if (data == null)
                    continue;

                NotifyOfDataReceive(data, connection, channel);
            }
        }

        private void NotifyOfDataReceive(NetworkData data, Connection connection, BroadcastingChannel channel)
        {
            if (LogPackets && data.ShouldBeLogged)
                Logger.Log($"Received packet from {channel} channel : {data}");

            _cachedDataTypes.Clear();
            // bad naming
            var baseType = data.GetType();
            while (baseType != null && baseType != typeof(object))
            {
                _cachedDataTypes.Add(baseType);
                baseType = baseType.BaseType;
            }

            // TODO: somehow figure out with garbage allocation
            // The issue is invokation can lead to adding data to send which is for local machine leads to instant reinvokation
            foreach (var dataType in _cachedDataTypes.ToList())
            {
                if (!_dataListeners.ContainsKey(dataType))
                    continue;

                foreach (var dataTypeListener in _dataListeners[dataType])
                {
                    dataTypeListener.Value?.Invoke(connection, data);
                }
            }
        }

        public void ListenForReceive(object listener, Type dataType, Action<Connection, NetworkData> dataReceivedCallback)
        {
            if (LogInteractions)
                Logger.Log($"ListenForReceive called by: {listener} for data type: {dataType}");

            if (listener == null)
            {
                Logger.LogError("Listener can't be null");
                return;
            }

            if (!_dataListeners.ContainsKey(dataType))
            {
                _dataListeners.Add(dataType, new Dictionary<object, Action<Connection, NetworkData>>());
            }

            if (_dataListeners[dataType].ContainsKey(listener))
            {
                Logger.LogError("Attempt to listen the same type multiple times by one listener");
                return;
            }

            _dataListeners[dataType].Add(listener, dataReceivedCallback);
        }

        public void ListenForReceive<T>(object listener, Action<Connection, T> dataReceivedCallback) where T : NetworkData
        {
            void DowncastData(Connection connection, NetworkData data)
            {
                dataReceivedCallback?.Invoke(connection, data as T);
            }

            ListenForReceive(listener, typeof(T), DowncastData);
        }

        public void UnListen<T>(object listener) where T : NetworkData
        {
            if (LogInteractions)
                Logger.Log($"UnListen called by: {listener} for all data types");

            if (listener == null)
            {
                Logger.LogError("Listener can't be null");
                return;
            }

            var dataType = typeof(T);
            if (!_dataListeners.ContainsKey(dataType))
            {
                Logger.LogError($"No listeners for type: {dataType}");
                return;
            }

            if (!_dataListeners[dataType].ContainsKey(listener))
            {
                Logger.LogError("Listener is not registered so can't be removed");
                return;
            }

            _dataListeners[dataType].Remove(listener);
        }
    }
}