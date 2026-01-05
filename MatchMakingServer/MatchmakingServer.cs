using System;
using System.Collections.Generic;
using System.Linq;
using Networking.Connections;
using Networking.NetworkState;
using Networking.NetworkState.Configs;
using Networking.RPCs.MatchMaking;
using Networking.Utils;

namespace MatchMakingServer
{
    public class MatchMakingServer : NetworkAgent
    {
        // 10 minutes
        private const double MaxSessionLivingTime = 60 * 10;
        private readonly Dictionary<Connection, Session> _sessionByConnection = new();
        private readonly List<Session> _sessions = new();
        private int _tickNumber;

        private readonly IdIterator _sessionId;

        public MatchMakingServer(NetworkAgentConfig config) : base(config)
        {
            Logger.Log("Matchmaking server started");
            _sessionId = new IdIterator();

            // Auth rpcs handling
            RPCManager.HandleRPCSync<CreateSessionRPC.Request, CreateSessionRPC.Response>(this, CreateGameSessionRPCHandler);
            RPCManager.HandleRPCSync<UpdateSessionRPC.Request, UpdateSessionRPC.Response>(this, UpdateSessionRPCHandler);

            // Unauth rpcs handling
            RPCManager.HandleRPCSync<GetSessionsRPC.Request, GetSessionsRPC.Response>(this, GetGameSessionsRPCHandler);
            RPCManager.HandleRPCAsync<ConnectToSessionRPC.Request, ConnectToSessionRPC.Response>(this, ConnectToSessionRPCAsyncHandler);

            ConnectionManager.ConnectionRemoved += ConnectionRemovedHandler;

            Logger.SetPrefix(() => _tickNumber.ToString());
        }

        private void ConnectionRemovedHandler(Connection connection)
        {
            if (_sessionByConnection.ContainsKey(connection))
            {
                DestroySession(_sessionByConnection[connection]);
            }
        }

        public override void MainThreadUpdate()
        {
            base.MainThreadUpdate();

            _tickNumber++;
            // if (_tickNumber % 10000 == 0)
            // {
            //     Logger.Log($"{_tickNumber} ticks passed, current number of sessions: {_sessions.Count}");
            // }

            var currentTimestamp = TimeUtils.GetUtcTimestamp();
            var removedSessionsNumber = _sessions.RemoveAll(x => currentTimestamp - x.CreationTimestamp > MaxSessionLivingTime);

            if (removedSessionsNumber > 0)
                Logger.Log($"Removed {removedSessionsNumber} by time, current number of sessions: {_sessions.Count}");
        }

        private CreateSessionRPC.Response CreateGameSessionRPCHandler(Connection connection, CreateSessionRPC.Request request)
        {
            Logger.Log($"CreateGameSessionRPCHandler from {connection}");

            var response = new CreateSessionRPC.Response();

            if (_sessionByConnection.ContainsKey(connection))
            {
                Logger.Log($"Attempt to create second game session by one server");
                DestroySession(_sessionByConnection[connection]);
            }

            var session = new Session(connection)
            {
                Id = _sessionId.Next(),
                Name = request.Name + connection.IPEndPoint,
                CurrentPlayers = 0,
                MaxPlayers = request.MaxPlayers,
                Password = request.Password
            };

            _sessionByConnection[connection] = session;
            _sessions.Add(session);

            response.Success = true;
            response.SessionId = session.Id;

            return response;
        }

        private UpdateSessionRPC.Response UpdateSessionRPCHandler(Connection connection, UpdateSessionRPC.Request request)
        {
            if (!_sessionByConnection.ContainsKey(connection))
            {
                return new UpdateSessionRPC.Response()
                {
                    Success = false,
                    FailReason = "Couldn't find any sessions opened by your address"
                };
            }

            var session = _sessionByConnection[connection];
            if (session.Id != request.SessionId)
            {
                return new UpdateSessionRPC.Response()
                {
                    Success = false,
                    FailReason = "Different session is registered by this connection"
                };
            }

            session.CurrentPlayers = request.CurrentPlayers;

            return new UpdateSessionRPC.Response()
            {
                Success = true,
            };
        }

        private void DestroySession(Session session)
        {
            Logger.Log($"DestroySession");

            _sessionByConnection.Remove(session.Server);
            _sessions.Remove(session);
        }

        private GetSessionsRPC.Response GetGameSessionsRPCHandler(Connection connection, GetSessionsRPC.Request request)
        {
            Logger.Log($"GetGameSessionsRPCHandler");

            var response = new GetSessionsRPC.Response
            {
                Sessions = new List<GetSessionsRPC.Session>()
            };

            foreach (var gameSession in _sessions)
            {
                response.Sessions.Add(new GetSessionsRPC.Session
                {
                    Id = gameSession.Id,
                    Name = gameSession.Name + (request.ShowPassword ? ":" + gameSession.Password : ""),
                    CurrentPlayers = gameSession.CurrentPlayers,
                    MaxPlayers = gameSession.MaxPlayers,
                    Locked = gameSession.Password != string.Empty,
                    Ping = connection.RTT,
                });
            }

            return response;
        }

        private void ConnectToSessionRPCAsyncHandler(Connection connection, ConnectToSessionRPC.Request request, Action<ConnectToSessionRPC.Response> responseCallback)
        {
            Logger.Log($"ConnectToSessionRPCAsyncHandler");
            var response = new ConnectToSessionRPC.Response();
            var targetSession = _sessions.FirstOrDefault(session => session.Id == request.SessionId);

            if (targetSession?.Server == null)
            {
                response.Success = false;
                response.FailReason = $"Couldn't find session with requested id: {request.SessionId}";
                responseCallback?.Invoke(response);
            }
            else
            {
                if (targetSession.CurrentPlayers >= targetSession.MaxPlayers)
                {
                    response.Success = false;
                    response.FailReason = $"Max players in session already reached: {targetSession.MaxPlayers}";
                    responseCallback?.Invoke(response);
                }
                else if (targetSession.Password != string.Empty && targetSession.Password != request.Password)
                {
                    response.Success = false;
                    response.FailReason = $"Incorrect password";
                    responseCallback?.Invoke(response);
                }
                else
                {
                    response.Success = true;
                    var serverIpEndPoint = targetSession.Server.IPEndPoint.ToString();

                    RPCManager.CallRPC<AcceptConnectionRPC.Request, AcceptConnectionRPC.Response>(targetSession.Server, new AcceptConnectionRPC.Request()
                    {
                        ClientIPEndPoint = connection.IPEndPoint.ToString(),
                        ClientIsDoSSensitive = request.ClientIsDoSSensitive,
                    }, acceptConnectionResponse =>
                    {
                        if (acceptConnectionResponse.Success)
                        {
                            response.Success = true;
                            response.ServerIPEndPoint = serverIpEndPoint;
                        }
                        else
                        {
                            response.Success = false;
                            response.FailReason = acceptConnectionResponse.FailReason;
                        }

                        responseCallback?.Invoke(response);
                    }, () =>
                    {
                        response.Success = false;
                        response.FailReason = $"Failed to reach the server";
                        responseCallback?.Invoke(response);
                    });
                }
            }
        }
    }
}