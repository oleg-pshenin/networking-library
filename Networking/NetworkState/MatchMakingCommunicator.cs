using System;
using System.Net;
using Networking.Connections;
using Networking.RPCs.Core;
using Networking.RPCs.MatchMaking;
using Networking.Utils;

namespace Networking.NetworkState
{
    /// <summary>
    /// Slightly simplified interface for communication with matchmaking server by client and server
    /// Also keeps some rpc state to prevent multiple calls until previous one is finished (via fail or response)
    /// </summary>
    public class MatchMakingCommunicator
    {
        private readonly IPEndPoint _matchMakingServerIpEndPoint;
        private readonly IConnectionManager _connectionManager;
        private readonly IRPCManager _rpcManager;
        private Connection _matchMakingConnection;
        private RPC _getSessionsRPC;
        private RPC _connectToSessionRPC;
        private RPC _createSessionRPC;

        public MatchMakingCommunicator(NetworkAgent networkAgent, IPEndPoint matchMakingServerIpEndPoint)
        {
            _connectionManager = networkAgent.ConnectionManager;
            _rpcManager = networkAgent.RPCManager;
            _matchMakingServerIpEndPoint = matchMakingServerIpEndPoint;
        }

        public void StartConnection()
        {
            Logger.Log($"MatchMakingCommunicator started to address {_matchMakingServerIpEndPoint}");
            _matchMakingConnection = _connectionManager.GetOrAddConnection(_matchMakingServerIpEndPoint);
            // Average rtt can be more than 400ms + 100ms+ on processing requests
            // To not extra D-DOS weak matchmaking server, making ack timeout significantly bigger 
            _matchMakingConnection.OverrideAckTimeout(1000);
        }

        public void EndConnection()
        {
            if (_matchMakingConnection != null)
            {
                _connectionManager.RemoveConnection(_matchMakingConnection);
                _matchMakingConnection = null;
            }
            else
            {
                Logger.LogWarning($"Attempt to end matchmaking connection even though it is not started yet");
            }
        }

        public bool IsConnectionStarted()
        {
            return _matchMakingConnection != null;
        }

        public void CreateSessionRPC(CreateSessionRPC.Request request, Action<CreateSessionRPC.Response> responseCallback, Action failedCallback)
        {
            if (!IsConnectionStarted())
            {
                Logger.LogError($"Attempt to call CreateSessionRPC but connection is not started");
                return;
            }

            if (_createSessionRPC != null && !_createSessionRPC.IsFinished)
            {
                Logger.LogError($"Attempt to call CreateSessionRPC while it is in progress");
                failedCallback?.Invoke();
            }
            else
            {
                // some discarding of previous?   
                _createSessionRPC = _rpcManager.CallRPC(_matchMakingConnection, request, responseCallback, failedCallback);
            }
        }


        public void GetSessionsRPC(GetSessionsRPC.Request request, Action<GetSessionsRPC.Response> responseCallback, Action failedCallback)
        {
            if (!IsConnectionStarted())
            {
                Logger.LogError($"Attempt to call GetSessionsRPC but connection is not started");
                return;
            }

            if (_getSessionsRPC != null && !_getSessionsRPC.IsFinished)
            {
                Logger.LogError($"Attempt to call GetSessionsRPC while it is in progress");
                failedCallback?.Invoke();
            }
            else
            {
                // some discarding of previous?   
                _getSessionsRPC = _rpcManager.CallRPC(_matchMakingConnection, request, responseCallback, failedCallback);
            }
        }

        public void ConnectToSessionRPC(ConnectToSessionRPC.Request request, Action<ConnectToSessionRPC.Response> responseCallback, Action failedCallback)
        {
            if (!IsConnectionStarted())
            {
                Logger.LogError($"Attempt to call ConnectToSessionRPC but connection is not started");
                return;
            }

            if (_connectToSessionRPC != null && !_connectToSessionRPC.IsFinished)
            {
                Logger.LogError($"Attempt to call ConnectToSessionRPC while it is in progress");
                failedCallback?.Invoke();
            }
            else
            {
                // some discarding of previous?   
                _connectToSessionRPC = _rpcManager.CallRPC(_matchMakingConnection, request, responseCallback, failedCallback);
            }
        }

        public void UpdateSessionInfo(int sessionId, int currentPlayers)
        {
            if (!IsConnectionStarted())
            {
                Logger.LogError($"Attempt to call UpdateSession but connection is not started");
                return;
            }

            _rpcManager.CallRPC<UpdateSessionRPC.Request, UpdateSessionRPC.Response>(_matchMakingConnection, new UpdateSessionRPC.Request()
            {
                SessionId = sessionId,
                CurrentPlayers = currentPlayers,
            }, null, null);
        }
    }
}