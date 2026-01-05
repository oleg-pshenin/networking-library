using System.Collections.Generic;
using Networking.Connections;
using Networking.Utils;

namespace MatchMakingServer
{
    /// <summary>
    /// Session is just a representation of server discoverability over matchmaking, it is not connected with server or
    /// game instance or even game session lifetime
    /// </summary>
    public class Session
    {
        public readonly Connection Server;
        public readonly double CreationTimestamp;
        public double LastActivity;

        // do i need?
        public List<Connection> ExternalConnections = new();

        public Session(Connection server)
        {
            Server = server;
            CreationTimestamp = TimeUtils.GetUtcTimestamp();
            LastActivity = CreationTimestamp;
        }

        public int Id;
        public string Name;
        public int CurrentPlayers;
        public int MaxPlayers;
        public string Password = string.Empty;
    }
}