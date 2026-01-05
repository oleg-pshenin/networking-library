using System;
using Networking.Utils;

namespace Networking.NetworkState.Configs
{
    public class NetworkAgentConfig : IValidatable
    {
        public int ListeningPort;
        public bool ListenFromAnySource;

        public virtual void Validate()
        {
            // validation?
        }

        public static NetworkAgentConfig GetDefault()
        {
            var networkAgentConfig = new NetworkAgentConfig();
            networkAgentConfig.SetDefaultValues();
            return networkAgentConfig;
        }

        protected virtual void SetDefaultValues()
        {
            ListeningPort = 0;
            ListenFromAnySource = false;
        }

        public static NetworkAgentConfig GetAdjustedDefault(Action<NetworkAgentConfig> adjustingDelegate)
        {
            var defaultConfig = GetDefault();
            adjustingDelegate.Invoke(defaultConfig);
            return defaultConfig;
        }
    }
}