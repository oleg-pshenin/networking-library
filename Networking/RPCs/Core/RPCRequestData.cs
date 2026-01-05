using Networking.Data.Core;
using ProtoBuf;

namespace Networking.RPCs.Core
{
    [ProtoContract]
    public class RPCRequestData : NetworkData
    {
        [ProtoMember(1)] public int CallId;
        [ProtoMember(2)] public RPCRequest RPCRequest;

        public override string ToString()
        {
            var requestLog = RPCRequest != null ? RPCRequest.ToString() : "NULL";
            return $"NetworkData: {GetType()}, CallId: {CallId}, RPCRequest: {requestLog}";
        }
    }
}