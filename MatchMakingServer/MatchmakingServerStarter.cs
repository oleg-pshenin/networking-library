using System;
using System.Threading;
using Networking;
using Networking.NetworkState.Configs;

namespace MatchMakingServer
{
    public class MatchmakingServerStarter
    {
        private const int ListeningPort = 1909;
        // Ticks per second
        private const int TickRate = 10;
        private MatchMakingServer _matchMakingServer;

        // should use some config later
        public void Start()
        {
            var networkManager = new NetworkManager(NetworkConfig.GetAdjustedDefault(_ => { }));
            _matchMakingServer = new MatchMakingServer(NetworkAgentConfig.GetAdjustedDefault(config =>
            {
                config.ListeningPort = ListeningPort;
                config.ListenFromAnySource = true;
            }));

            while (true)
            {
                _matchMakingServer.MainThreadUpdate();
                Thread.Sleep(Convert.ToInt32(1000.0 / TickRate));
            }
        }
    }
}