using Networking.Data.Core;
using ProtoBuf;

namespace Networking.RPCs.Core
{
    [ProtoContract]
    public class RPCResponseData : NetworkData
    {
        [ProtoMember(1)] public int CallId;
        [ProtoMember(2)] public RPCResponse RPCResponse;

        public override string ToString()
        {
            var responseLog = RPCResponse != null ? RPCResponse.ToString() : "NULL";
            return $"NetworkData: {GetType()}, CallId: {CallId}, RPCResponse: {responseLog}";
        }
    }
}