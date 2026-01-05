using Networking.RPCs.Core;
using ProtoBuf;

namespace Networking.RPCs
{
    public static class ClientRegistrationRPC
    {
        [ProtoContract]
        public class Request : RPCRequest
        {
            [ProtoMember(1)] public string Nickname;
            // optional from mm server
            [ProtoMember(2)] public int SessionId = -1;

            public override string ToString()
            {
                return $"RPCRequest: {GetType()}, Nickname: {Nickname}, SessionId: {SessionId}";
            }
        }

        [ProtoContract]
        public class Response : RPCResponse
        {
            [ProtoMember(1)] public bool Accept;
            [ProtoMember(2)] public int ServerNetworkId;
            [ProtoMember(3)] public int ClientNetworkId;
            [ProtoMember(4)] public string DeclineReason;

            public override string ToString()
            {
                return $"RPCResponse: {GetType()}, Accept: {Accept}, ServerNetworkId: {ServerNetworkId}, ClientNetworkId: {ClientNetworkId}, DeclineReason: {DeclineReason}";
            }
        }
    }
}