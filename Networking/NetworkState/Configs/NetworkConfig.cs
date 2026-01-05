using System;
using Networking.Broadcasting;
using Networking.Broadcasting.Channels;
using Networking.Connections;
using Networking.RPCs.Core;
using Networking.Utils;

namespace Networking.NetworkState.Configs
{
    public class NetworkConfig : IValidatable
    {
        private const double MinConnectionRemovalTimeOut = 2000;
        private const double MinConnectionLostTimeOut = 1000;
        private const double MinConnectionEstablishTimeOut = 1000;
        private const double MinPingRate = 0.1;
        private const double MaxPingRate = 15;
        private const double MinReliableAckTimeout = 16.6;
        private const double MinReliableFragmentedAckTimeout = 100;

        /// <summary>
        /// How much time should pass since last received data from connection to remove connection in milliseconds
        /// </summary>
        public double ConnectionRemovalTimeOut;

        /// <summary>
        /// How much time should pass since last received data from connection to mark connection as lost in milliseconds
        /// </summary>
        public double ConnectionLostTimeOut;

        /// <summary>
        /// How much time should pass since starting connection without receiving to mark connection as lost in milliseconds
        /// </summary>
        public double ConnectionEstablishTimeOut;

        /// <summary>
        /// Number of ping calls per second to each connection.
        /// Equals to (1 / Seconds between calls)
        /// </summary>
        public double PingRate;

        /// <summary>
        /// How much time should pass after sending reliable packet before it is considered lost and should be resent in
        /// milliseconds
        /// </summary>
        public double ReliableAckTimeout;

        /// <summary>
        /// How much time should pass after sending reliable packet before it is considered lost and should be resent in
        /// milliseconds
        /// </summary>
        public double ReliableSequencedAckTimeout;

        /// <summary>
        /// How much time should pass after sending reliable fragmented packet before it is considered lost and should be
        /// resent in milliseconds
        /// It is fine for it to be noticeably bigger than regular reliable timeout as it is used for heavy data which includes
        /// fragmentation adn compression which takes time
        /// </summary>
        public double ReliableFragmentedAckTimeout;

        public bool LogNetworkManager;
        public bool LogConnectionManager;

        public bool LogRawUdpTraffic;
        public bool LogUdpMessenger;

        public bool LogNetworkDataTraffic;
        public bool LogDataBroadcaster;

        public bool LogRPCs;
        public bool LogRPCManager;

        public static NetworkConfig GetDefault()
        {
            return new NetworkConfig()
            {
                LogNetworkManager = true,
                LogConnectionManager = false,

                LogRawUdpTraffic = false,
                LogUdpMessenger = true,

                LogNetworkDataTraffic = true,
                LogDataBroadcaster = true,

                LogRPCs = false,
                LogRPCManager = true,

                ConnectionRemovalTimeOut = 15000,
                ConnectionLostTimeOut = 5000,
                ConnectionEstablishTimeOut = 5000,
                PingRate = 0.5,
                ReliableAckTimeout = 200,
                ReliableSequencedAckTimeout = 200,
                ReliableFragmentedAckTimeout = 1000,
            };
        }

        public static NetworkConfig GetAdjustedDefault(Action<NetworkConfig> adjustingDelegate)
        {
            var defaultConfig = GetDefault();
            adjustingDelegate.Invoke(defaultConfig);
            return defaultConfig;
        }

        public void Validate()
        {
            if (PingRate < MinPingRate)
            {
                Logger.LogWarning($"Fixed PingRate from: {PingRate} to {MinPingRate}");
                PingRate = MinPingRate;
            }
            else if (PingRate > MaxPingRate)
            {
                Logger.LogWarning($"Fixed PingRate from: {PingRate} to {MaxPingRate}");
                PingRate = MaxPingRate;
            }

            if (ConnectionRemovalTimeOut < MinConnectionRemovalTimeOut)
            {
                Logger.LogWarning($"Fixed ConnectionRemovalTimeOut from: {ConnectionRemovalTimeOut} to {MinConnectionRemovalTimeOut}");
                ConnectionRemovalTimeOut = MinConnectionRemovalTimeOut;
            }

            if (ConnectionLostTimeOut < MinConnectionLostTimeOut)
            {
                Logger.LogWarning($"Fixed ConnectionLostTimeOut from: {ConnectionLostTimeOut} to {MinConnectionLostTimeOut}");
                ConnectionLostTimeOut = MinConnectionLostTimeOut;
            }

            if (ConnectionEstablishTimeOut < MinConnectionEstablishTimeOut)
            {
                Logger.LogWarning($"Fixed ConnectionEstablishingTimeOut from: {ConnectionEstablishTimeOut} to {MinConnectionEstablishTimeOut}");
                ConnectionEstablishTimeOut = MinConnectionEstablishTimeOut;
            }

            if (ReliableAckTimeout < MinReliableAckTimeout)
            {
                Logger.LogWarning($"Fixed ReliableAckTimeout from: {ReliableAckTimeout} to {MinReliableAckTimeout}");
                ReliableAckTimeout = MinReliableAckTimeout;
            }

            if (ReliableFragmentedAckTimeout < MinReliableFragmentedAckTimeout)
            {
                Logger.LogWarning($"Fixed ReliableFragmentedAckTimeout from: {ReliableFragmentedAckTimeout} to {MinReliableFragmentedAckTimeout}");
                ReliableFragmentedAckTimeout = MinReliableFragmentedAckTimeout;
            }

            var pingPeriod = 1000.0 / PingRate;
            if (pingPeriod > ConnectionLostTimeOut)
            {
                Logger.LogError($"Ping rate is bigger than ConnectionLostTimeOut, without heavy traffic it will lead to incorrect marking connections as lost, please check configuration");
                PingRate = 2 * (1000.0 / ConnectionLostTimeOut);
            }
        }

        public void ApplyStaticValues()
        {
            NetworkManager.LogInteractions = LogNetworkManager;

            UdpMessengerAsync.LogRawTraffic = LogRawUdpTraffic;
            UdpMessengerAsync.LogInteractions = LogUdpMessenger;

            ConnectionManager.LogInteractions = LogConnectionManager;

            ConnectionMonitor.ConnectionRemovalTimeOut = ConnectionRemovalTimeOut;
            ConnectionMonitor.ConnectionLostTimeOut = ConnectionLostTimeOut;
            ConnectionMonitor.ConnectionEstablishTimeOut = ConnectionEstablishTimeOut;
            ConnectionMonitor.PingRate = PingRate;

            DataBroadcaster.LogPackets = LogNetworkDataTraffic;
            DataBroadcaster.LogInteractions = LogDataBroadcaster;

            RPCManager.LogCalls = LogRPCs;
            RPCManager.LogInteractions = LogRPCManager;

            ReliableChannelBroadcaster.AckTimeout = ReliableAckTimeout;
            ReliableSequencedChannelBroadcaster.AckTimeout = ReliableSequencedAckTimeout;
            ReliableFragmentedChannelBroadcaster.AckTimeout = ReliableFragmentedAckTimeout;
        }
    }
}