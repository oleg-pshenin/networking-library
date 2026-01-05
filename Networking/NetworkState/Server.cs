using System;
using System.Linq;
using Networking.Broadcasting.SubNetDiscovery;
using Networking.Connections;
using Networking.NetworkState.Configs;
using Networking.RPCs;
using Networking.RPCs.MatchMaking;
using Networking.Utils;

namespace Networking.NetworkState
{
    /// <summary>
    /// Real usage server which works with matchmaking to connect with clients
    /// Still as a base class child supports direct connections
    /// </summary>
    public class Server : AuthSyncer
    {
        public MatchMakingCommunicator MatchMakingCommunicator { get; }
        private readonly ServerConfig _serverConfig;

        private int _currentSessionId = -1;

        internal Server(ServerConfig serverConfig, NetworkState networkState) : base(serverConfig, networkState)
        {
            Logger.Log($"Server started");

            _serverConfig = serverConfig;
            RPCManager.HandleRPCAsync<AcceptConnectionRPC.Request, AcceptConnectionRPC.Response>(this, AcceptConnectionRPCHandler);

            MatchMakingCommunicator = new MatchMakingCommunicator(this, serverConfig.MatchMakingServerIPEndPoint);
            if (serverConfig.StartMatchMakingSession)
            {
                MatchMakingCommunicator.StartConnection();
                StartMatchMakingSession();
            }

            if (serverConfig.AutoSubNetDiscoverable)
            {
                SubNetDiscoverer.FindServerRequestReceived += FindServerRequestReceivedHandler;
            }
        }

        private void FindServerRequestReceivedHandler(FindServerRequest request)
        {
            if (UnauthConnections.Any(x => Equals(x.IPEndPoint, request.SenderIPEndPoint)))
            {
                Logger.LogWarning($"Received broadcast message from already connected client: {request.SenderIPEndPoint}");
                return;
            }

            SubNetDiscoverer.SendServerInfoToClient(request.SenderIPEndPoint, new FindServerResponse()
            {
                Name = "Session: ",
                CurrentPlayers = UnauthConnections.Count,
                MaxPlayers = _serverConfig.MaxPlayers,
            });

            ConnectionManager.GetOrAddConnection(request.SenderIPEndPoint);
        }

        protected override ClientRegistrationRPC.Response ClientRegistrationRPCHandler(Connection connection, ClientRegistrationRPC.Request request)
        {
            if (UnauthConnections.Count >= _serverConfig.MaxPlayers)
            {
                return new ClientRegistrationRPC.Response()
                {
                    Accept = false,
                    DeclineReason = $"Too many clients already: {UnauthConnections.Count}, max: {_serverConfig.MaxPlayers}",
                };
            }

            if (request.SessionId > 0 && request.SessionId != _currentSessionId)
            {
                return new ClientRegistrationRPC.Response()
                {
                    Accept = false,
                    DeclineReason = $"Incorrect session id",
                };
            }

            return base.ClientRegistrationRPCHandler(connection, request);
        }

        protected override void OnUnauthConnectionAdded(Connection connection)
        {
            if (MatchMakingCommunicator.IsConnectionStarted())
                MatchMakingCommunicator.UpdateSessionInfo(_currentSessionId, UnauthConnections.Count);
        }

        protected override void OnUnauthConnectionRemoved(Connection connection)
        {
            if (MatchMakingCommunicator.IsConnectionStarted())
                MatchMakingCommunicator.UpdateSessionInfo(_currentSessionId, UnauthConnections.Count);
        }

        public void StartMatchMakingSession()
        {
            MatchMakingCommunicator.CreateSessionRPC(new CreateSessionRPC.Request()
            {
                Name = "Session: ",
                MaxPlayers = _serverConfig.MaxPlayers,
                Password = _serverConfig.Password
            }, CreateSessionRPCHandler, CreateSessionRPCFailedHandler);
        }

        private void CreateSessionRPCHandler(CreateSessionRPC.Response response)
        {
            if (response.Success)
            {
                _currentSessionId = response.SessionId;
            }
            else
            {
                Logger.LogError($"Failed to create session rpc: {response.FailReason}");
            }
        }

        private void CreateSessionRPCFailedHandler()
        {
            Logger.LogError($"CreateSessionRPCFailedHandler");
        }

        private void AcceptConnectionRPCHandler(Connection connection, AcceptConnectionRPC.Request request, Action<AcceptConnectionRPC.Response> responseCallback)
        {
            // here can be request to UI to confirm request then we need to send it back
            // also can check by number of active users

            var response = new AcceptConnectionRPC.Response();

            if (ConnectionManager.HasConnection(request.ClientIPEndPoint))
            {
                response.Success = false;
                response.FailReason = "Already connected";
                responseCallback?.Invoke(response);
            }
            else
            {
                var clientConnection = ConnectionManager.GetOrAddConnection(request.ClientIPEndPoint);
                if (request.ClientIsDoSSensitive)
                {
                    clientConnection.MonitoringSilenceDelay = connection.RTT * 2;
                }
                // auto accept, later should check for number of players

                response.Success = true;
                responseCallback?.Invoke(response);
            }
        }
    }
}