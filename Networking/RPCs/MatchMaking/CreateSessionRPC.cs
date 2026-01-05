using Networking.RPCs.Core;
using ProtoBuf;

namespace Networking.RPCs.MatchMaking
{
    /// <summary>
    /// Sent by server to MMS to create discoverable session on MMS
    /// </summary>
    public static class CreateSessionRPC
    {
        [ProtoContract]
        public class Request : RPCRequest
        {
            [ProtoMember(1)] public string Name;
            [ProtoMember(2)] public int MaxPlayers;
            [ProtoMember(3)] public string Password;

            public override string ToString()
            {
                return $"RPCRequest: {GetType()}, Name: {Name}, MaxPlayers: {MaxPlayers}, Password: {Password}";
            }
        }

        [ProtoContract]
        public class Response : RPCResponse
        {
            [ProtoMember(1)] public bool Success;
            [ProtoMember(2)] public int SessionId;
            [ProtoMember(3)] public string FailReason;

            public override string ToString()
            {
                return $"RPCResponse: {GetType()}, Success: {Success}, SessionId: {SessionId}, FailReason: {FailReason}";
            }
        }
    }
}