using System;
using Networking.Connections;
using Networking.Data.Core;

namespace Networking.Broadcasting
{
    public interface IDataReceiver
    {
        void ListenForReceive(object listener, Type dataType, Action<Connection, NetworkData> dataReceivedCallback);
        void ListenForReceive<T>(object listener, Action<Connection, T> dataReceivedCallback) where T : NetworkData;
        void UnListen<T>(object listener) where T : NetworkData;
    }
}