using ProtoBuf;

namespace Networking.RPCs.Core
{
    [ProtoContract]
    public abstract class RPCRequest
    {
        public abstract override string ToString();
    }
}