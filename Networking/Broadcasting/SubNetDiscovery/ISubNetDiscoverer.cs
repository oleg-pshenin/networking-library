using System;
using System.Net;

namespace Networking.Broadcasting.SubNetDiscovery
{
    public interface ISubNetDiscoverer
    {
        event Action<FindServerRequest> FindServerRequestReceived;
        event Action<FindServerResponse> FindServerResponseReceived;
        void BroadcastToServers(int serverPort);
        void SendServerInfoToClient(IPEndPoint ipEndPoint, FindServerResponse findServerResponse);
    }
}