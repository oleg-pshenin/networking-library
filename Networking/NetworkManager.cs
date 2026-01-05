using System;
using System.Net;
using Networking.Data.Core;
using Networking.NetworkState;
using Networking.NetworkState.Configs;
using Networking.NetworkState.View;
using Networking.Utils;

namespace Networking
{
    /// <summary>
    /// Manages start of network agents and makes sure that there is only a single Network State
    /// Does settings auto-binding between local client and local server
    /// </summary>
    public class NetworkManager : IMainThreadUpdateable
    {
        internal static readonly IPEndPoint DefaultMatchMakingServerIpEndPoint = "127.0.0.1:1909".ParseToIpEndPoint();
        internal static bool LogInteractions;

        public event Action ServerStarted;
        public event Action ClientStarted;

        public NetworkState.NetworkState NetworkState { get; private set; }
        public NetworkStatePresenter NetworkStatePresenter { get; private set; }

        public Client Client { get; private set; }
        public Server Server { get; private set; }

        public NetworkManager(NetworkConfig networkConfig = null)
        {
            DataTypeRegister.Init();
            DataTypeRegister.Register(BuiltInDataTypes.SystemDataTypes);
            DataTypeRegister.Register(BuiltInDataTypes.RegularDataTypes);
            DataTypeRegister.Register(BuiltInDataTypes.MatchMakingDataTypes);

            networkConfig ??= NetworkConfig.GetDefault();

            networkConfig.Validate();
            networkConfig.ApplyStaticValues();

            NetworkState = new NetworkState.NetworkState();
            NetworkStatePresenter = new NetworkStatePresenter();
            NetworkStatePresenter.SetupNetworkState(NetworkState);
        }

        public Server StartServer(ServerConfig serverConfig)
        {
            if (LogInteractions)
                Logger.Log($"Start server called");

            if (Client != null)
            {
                Logger.LogError($"Can't create server after client, please restart");
                return null;
            }

            serverConfig ??= ServerConfig.GetDefault();

            var server = new Server(serverConfig, NetworkState);
            server.InitSessionInfo();

            Server = server;
            ServerStarted?.Invoke();
            return server;
        }

        public Client StartClient(ClientConfig clientConfig)
        {
            if (LogInteractions)
                Logger.Log($"Start client called");

            clientConfig ??= ClientConfig.GetDefault();

            if (Server != null)
                NetworkState.IsShared = true;

            var client = new Client(clientConfig, NetworkState);

            // means that it is already in use by server so custom logic happens
            if (Server != null)
            {
                var clientConnection = Server.ConnectionManager.GetOrAddConnection(client.LocalIPEndPoint);
                Server.SetLocalUnauthConnection(clientConnection);

                var serverConnection = client.ConnectionManager.GetOrAddConnection(Server.LocalIPEndPoint);
                client.ConnectToAuth(serverConnection);
            }
            else
            {
                // ok so here server is not started then we should enable matchmaking communicator to have a way of connecting
                client.StartAsStandAloneClient();
            }

            Client = client;
            ClientStarted?.Invoke();
            return Client;
        }

        public void MainThreadUpdate()
        {
            NetworkState?.MainThreadUpdate();

            Client?.MainThreadUpdate();
            Server?.MainThreadUpdate();
        }

        /// <summary>
        /// Should be called on closing the app otherwise it will lead to various async issues
        /// </summary>
        public void ShutDown()
        {
            if (LogInteractions)
                Logger.Log($"Network shutdown");

            NetworkState?.ShutDown();
            Client?.ShutDown();
            Server?.ShutDown();

            NetworkState = null;
            NetworkStatePresenter = null;
            Client = null;
            Server = null;
        }
    }
}