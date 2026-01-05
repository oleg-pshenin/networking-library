using Networking.RPCs.Core;
using ProtoBuf;

namespace Networking.RPCs.MatchMaking
{
    /// <summary>
    /// Called by Client to MMS to redirect the call to Server to get an accept from it via AcceptConnectionRPC.Request
    /// </summary>
    public static class ConnectToSessionRPC
    {
        [ProtoContract]
        public class Request : RPCRequest
        {
            [ProtoMember(1)] public int SessionId;
            [ProtoMember(2)] public string Password;
            [ProtoMember(3)] public bool ClientIsDoSSensitive;

            public override string ToString()
            {
                return $"RPCRequest: {GetType()}, SessionId: {SessionId}, Password: {Password}, ClientIsDoSSensitive: {ClientIsDoSSensitive}";
            }
        }

        [ProtoContract]
        public class Response : RPCResponse
        {
            [ProtoMember(1)] public bool Success;
            [ProtoMember(2)] public string ServerIPEndPoint;
            [ProtoMember(3)] public string FailReason;

            public override string ToString()
            {
                return $"RPCResponse: {GetType()}, Success: {Success}, ServerIpEndPoint: {ServerIPEndPoint}, FailReason: {FailReason}";
            }
        }
    }
}