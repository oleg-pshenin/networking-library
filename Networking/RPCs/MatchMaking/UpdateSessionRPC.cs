using Networking.RPCs.Core;
using ProtoBuf;

namespace Networking.RPCs.MatchMaking
{
    /// <summary>
    /// Sent by server to MMS to update current session
    /// </summary>
    public static class UpdateSessionRPC
    {
        [ProtoContract]
        public class Request : RPCRequest
        {
            [ProtoMember(1)] public int SessionId;
            [ProtoMember(2)] public int CurrentPlayers;

            public override string ToString()
            {
                return $"RPCRequest: {GetType()}, SessionId: {SessionId}, CurrentPlayers: {CurrentPlayers}";
            }
        }

        [ProtoContract]
        public class Response : RPCResponse
        {
            [ProtoMember(1)] public bool Success;
            [ProtoMember(3)] public string FailReason;

            public override string ToString()
            {
                return $"RPCResponse: {GetType()}, Success: {Success}, FailReason: {FailReason}";
            }
        }
    }
}