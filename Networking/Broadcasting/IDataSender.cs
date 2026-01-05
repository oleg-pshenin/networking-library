using System.Collections.Generic;
using Networking.Connections;
using Networking.Data.Core;

namespace Networking.Broadcasting
{
    public interface IDataSender
    {
        void AddDataToSend(Connection connection, NetworkData data, BroadcastingChannel broadcastingChannel);
        void AddDataToSend(List<Connection> connections, NetworkData data, BroadcastingChannel broadcastingChannel);
    }
}