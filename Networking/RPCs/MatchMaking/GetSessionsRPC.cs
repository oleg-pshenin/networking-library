using System.Collections.Generic;
using Networking.RPCs.Core;
using ProtoBuf;

namespace Networking.RPCs.MatchMaking
{
    /// <summary>
    /// Sent by Client to MMS to get a list of discoverable sessions on MMS
    /// </summary>
    public static class GetSessionsRPC
    {
        [ProtoContract]
        public class Request : RPCRequest
        {
            [ProtoMember(1)] public string Filter;
            [ProtoMember(2)] public bool ShowPassword;

            public override string ToString()
            {
                return $"RPCResponse: {GetType()}, Filter: {Filter}, ShowPassword: {ShowPassword}";
            }
        }

        [ProtoContract]
        public class Session
        {
            [ProtoMember(1)] public int Id;
            [ProtoMember(2)] public string Name;
            [ProtoMember(3)] public int CurrentPlayers;
            [ProtoMember(4)] public int MaxPlayers;
            [ProtoMember(5)] public bool Locked;
            [ProtoMember(6)] public double Ping;
        }

        [ProtoContract]
        public class Response : RPCResponse
        {
            [ProtoMember(1)] public List<Session> Sessions;

            public override string ToString()
            {
                return $"RPCResponse: {GetType()}, Sessions: {Sessions?.Count}";
            }
        }
    }
}