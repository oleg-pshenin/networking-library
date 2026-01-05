using System.Net;
using ProtoBuf;

namespace Networking.Broadcasting.SubNetDiscovery
{
    [ProtoContract]
    [ProtoInclude(1, typeof(FindServerRequest))]
    [ProtoInclude(2, typeof(FindServerResponse))]
    public abstract class BroadcastMessage
    {
        public IPEndPoint SenderIPEndPoint;
        
        public abstract override string ToString();
    }
}