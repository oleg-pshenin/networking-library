using System;
using System.Collections.Generic;
using System.Net;

namespace Networking.Connections
{
    public interface IConnectionManager
    {
        event Action<Connection> ConnectionAdded;
        event Action<Connection> ConnectionRemoved;
        List<Connection> Connections { get; }
        bool HasConnection(IPEndPoint ipEndPoint);
        bool HasConnection(string ipEndPoint);
        Connection GetOrAddConnection(string ipEndPoint);
        Connection GetOrAddConnection(IPEndPoint ipEndPoint);

        void RemoveConnection(IPEndPoint ipEndPoint);
        void RemoveConnection(Connection connection);
        void RemoveAll();
    }
}