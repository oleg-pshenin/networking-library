using ProtoBuf;

namespace Networking.RPCs.Core
{
    [ProtoContract]
    public abstract class RPCResponse
    {
        public abstract override string ToString();
    }
}