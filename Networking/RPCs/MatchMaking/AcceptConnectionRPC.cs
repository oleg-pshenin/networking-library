using Networking.RPCs.Core;
using ProtoBuf;

namespace Networking.RPCs.MatchMaking
{
    /// <summary>
    /// Called by MMS to Server to get acceptance of specific Client connection to Response later to Client via
    /// ConnectToSessionRPC.Response
    /// </summary>
    public static class AcceptConnectionRPC
    {
        [ProtoContract]
        public class Request : RPCRequest
        {
            [ProtoMember(1)] public string ClientIPEndPoint;
            [ProtoMember(2)] public bool ClientIsDoSSensitive;

            public override string ToString()
            {
                return $"RPCRequest: {GetType()}, ClientIpEndPoint: {ClientIPEndPoint}, ClientIsDoSSensitive: {ClientIsDoSSensitive}";
            }
        }

        [ProtoContract]
        public class Response : RPCResponse
        {
            [ProtoMember(1)] public bool Success;
            [ProtoMember(2)] public string FailReason;

            public override string ToString()
            {
                return $"RPCResponse: {GetType()}, Success: {Success}, FailReason: {FailReason}";
            }
        }
    }
}