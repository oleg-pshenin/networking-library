using System;
using System.Net;
using Networking.Utils;

namespace Networking.NetworkState.Configs
{
    public class ServerConfig : NetworkAgentConfig
    {
        public bool StartMatchMakingSession;
        public bool AutoSubNetDiscoverable;
        public IPEndPoint MatchMakingServerIPEndPoint;
        public string Password;
        public int MaxPlayers;

        public new static ServerConfig GetDefault()
        {
            var serverConfig = new ServerConfig();
            serverConfig.SetDefaultValues();
            return serverConfig;
        }

        protected override void SetDefaultValues()
        {
            base.SetDefaultValues();
            AutoSubNetDiscoverable = true;
            StartMatchMakingSession = true;
            MatchMakingServerIPEndPoint = NetworkManager.DefaultMatchMakingServerIpEndPoint;
            Password = string.Empty;
            MaxPlayers = 4;
        }

        public static ServerConfig GetAdjustedDefault(Action<ServerConfig> adjustingDelegate)
        {
            var defaultConfig = GetDefault();
            adjustingDelegate.Invoke(defaultConfig);
            return defaultConfig;
        }

        private const int MaxPasswordLength = 16;

        public override void Validate()
        {
            base.Validate();

            if (Password.Length > MaxPasswordLength)
            {
                Logger.LogError($"Can't set password of length more than {MaxPasswordLength}, password will not be applied");
                Password = string.Empty;
            }
        }
    }
}