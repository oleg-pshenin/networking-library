using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Networking.Broadcasting;
using Networking.Utils;

namespace Networking.Connections
{
    public class ConnectionManager : IConnectionManager
    {
        internal static bool LogInteractions = true;
        private readonly Dictionary<string, Connection> _connectionsByAddress = new();

        public event Action<Connection> ConnectionAdded;
        public event Action<Connection> ConnectionRemoved;
        public List<Connection> Connections { get; } = new();
        private readonly IUdpMessenger _udpMessenger;

        internal ConnectionManager(IUdpMessenger udpMessenger)
        {
            _udpMessenger = udpMessenger;
        }

        public bool HasConnection(IPEndPoint ipEndPoint)
        {
            return HasConnection(ipEndPoint.ToString());
        }

        public bool HasConnection(string ipEndPoint)
        {
            return _connectionsByAddress.ContainsKey(ipEndPoint);
        }

        public Connection GetOrAddConnection(IPEndPoint ipEndPoint)
        {
            if (ipEndPoint == null)
            {
                Logger.LogWarning($"Can't get or add connection by null ipEndPoint");
                return null;
            }

            // invert that?
            return GetOrAddConnection(ipEndPoint.ToString());
        }

        public Connection GetOrAddConnection(string ipEndPoint)
        {
            if (LogInteractions)
                Logger.Log($"GetOrAddConnection: {ipEndPoint}");

            if (!_connectionsByAddress.ContainsKey(ipEndPoint))
                return AddConnection(ipEndPoint.ParseToIpEndPoint());

            return _connectionsByAddress[ipEndPoint];
        }

        private Connection AddConnection(IPEndPoint ipEndPoint)
        {
            if (LogInteractions)
                Logger.Log($"AddConnection: {ipEndPoint}");

            var address = ipEndPoint.ToString();

            if (!_connectionsByAddress.ContainsKey(address))
            {
                var connection = new Connection(ipEndPoint);
                _connectionsByAddress.Add(address, connection);
                Connections.Add(connection);

                _udpMessenger.StartReceivingFrom(connection.IPEndPoint);

                ConnectionAdded?.Invoke(connection);
            }
            else
            {
                Logger.LogError($"Connection with this address already exists: {address}");
            }

            return _connectionsByAddress[address];
        }

        public void RemoveConnection(IPEndPoint ipEndPoint)
        {
            var address = ipEndPoint.ToString();
            if (!_connectionsByAddress.ContainsKey(address))
            {
                Logger.LogError($"Attempt to remove not existing connection: {ipEndPoint}");
                return;
            }

            RemoveConnection(_connectionsByAddress[address]);
        }

        // Should get rid of removing from list and remember them as closed?
        public void RemoveConnection(Connection connection)
        {
            if (LogInteractions)
                Logger.Log($"RemoveConnection: {connection}");

            if (!Connections.Contains(connection))
            {
                Logger.LogError($"Attempt to remove not existing connection: {connection}");
                return;
            }

            _connectionsByAddress.Remove(connection.IPEndPoint.ToString());
            Connections.Remove(connection);

            _udpMessenger.StopReceivingFrom(connection.IPEndPoint);

            ConnectionRemoved?.Invoke(connection);
        }

        public void RemoveAll()
        {
            if (LogInteractions)
                Logger.Log($"RemoveAll");

            foreach (var connection in Connections.ToList())
            {
                RemoveConnection(connection);
            }
        }
    }
}