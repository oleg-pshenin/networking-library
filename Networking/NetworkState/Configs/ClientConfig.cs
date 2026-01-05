using System;
using System.Net;

namespace Networking.NetworkState.Configs
{
    public class ClientConfig : NetworkAgentConfig
    {
        /// <summary>
        /// Some routers may change NAT port from the one that is in use with matchmaking if something externally came to it
        /// So this parameter will invoke delay on game server to not send ping packet earlier than client sends punch hole
        /// packet upfront
        /// </summary>
        public bool IsDoSSensitive;
        public bool SearchForSubNet;
        public bool ConnectToMatchMaking;
        public int ServerListeningPort;
        public IPEndPoint MatchMakingServerIPEndPoint;

        public new static ClientConfig GetDefault()
        {
            var clientConfig = new ClientConfig();
            clientConfig.SetDefaultValues();
            return clientConfig;
        }

        protected override void SetDefaultValues()
        {
            base.SetDefaultValues();
            IsDoSSensitive = false;
            SearchForSubNet = false;
            ConnectToMatchMaking = true;
            ServerListeningPort = 23456;
            MatchMakingServerIPEndPoint = NetworkManager.DefaultMatchMakingServerIpEndPoint;
        }

        public static ClientConfig GetAdjustedDefault(Action<ClientConfig> adjustingDelegate)
        {
            var defaultConfig = GetDefault();
            adjustingDelegate.Invoke(defaultConfig);
            return defaultConfig;
        }

        public override void Validate()
        {
            base.Validate();
        }
    }
}