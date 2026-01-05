using Networking.Broadcasting.Packets;
using Networking.Broadcasting.SubNetDiscovery;
using Networking.NetworkState.Configs;
using Networking.RPCs.MatchMaking;
using Networking.Utils;

namespace Networking.NetworkState
{
    public class Client : UnauthSyncer
    {
        public MatchMakingCommunicator MatchMakingCommunicator { get; }

        private readonly ClientConfig _clientConfig;
        private int _targetSessionId;

        internal Client(ClientConfig clientConfig, NetworkState networkState) : base(clientConfig, networkState)
        {
            Logger.Log($"Client started");
            _clientConfig = clientConfig;
            MatchMakingCommunicator = new MatchMakingCommunicator(this, clientConfig.MatchMakingServerIPEndPoint);
            if (_clientConfig.SearchForSubNet)
            {
                SubNetDiscoverer.FindServerResponseReceived += FindServerResponseReceivedHandler;
            }
        }

        public void StartAsStandAloneClient()
        {
            if (_clientConfig.ConnectToMatchMaking)
            {
                MatchMakingCommunicator.StartConnection();
            }

            if (_clientConfig.SearchForSubNet)
            {
                SubNetDiscoverer.BroadcastToServers(_clientConfig.ServerListeningPort);
            }
        }

        private void FindServerResponseReceivedHandler(FindServerResponse response)
        {
            if (IsRegistered())
                return;

            var serverConnection = ConnectionManager.GetOrAddConnection(response.SenderIPEndPoint);
            ConnectToAuth(serverConnection, _targetSessionId);
            Logger.Log($"Trying to connect to server in subnet: {response.SenderIPEndPoint}");
        }

        protected override void OnConnectToAuth()
        {
            base.OnConnectToAuth();
            if (MatchMakingCommunicator != null)
                MatchMakingCommunicator.EndConnection();
        }

        public void TryToConnectToSessionRPC(GetSessionsRPC.Session session)
        {
            _targetSessionId = session.Id;
            MatchMakingCommunicator.ConnectToSessionRPC(new ConnectToSessionRPC.Request()
            {
                SessionId = session.Id,
                ClientIsDoSSensitive = _clientConfig.IsDoSSensitive,
            }, ConnectToSessionRPCHandler, ConnectToSessionRPCFailedHandler);
        }

        private void ConnectToSessionRPCHandler(ConnectToSessionRPC.Response response)
        {
            if (response.Success)
            {
                var serverConnection = ConnectionManager.GetOrAddConnection(response.ServerIPEndPoint);
                ConnectToAuth(serverConnection, _targetSessionId);
            }
            else
            {
                Logger.LogError($"Couldn't connect to session because of: {response.FailReason}");
            }
        }

        private void ConnectToSessionRPCFailedHandler()
        {
            Logger.LogError($"ConnectToSessionRPCFailedHandler");
        }
    }
}