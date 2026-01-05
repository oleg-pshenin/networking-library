using System;
using System.Collections.Generic;
using Networking.Entities;
using Networking.Entities.Core;
using Networking.RPCs;
using Networking.RPCs.Core;
using Networking.RPCs.MatchMaking;

namespace Networking.Data.Core
{
    public static class BuiltInDataTypes
    {
        public static readonly List<Type> SystemDataTypes = new()
        {
            typeof(EntitySyncData),
            typeof(EntityInstantiateData),
            typeof(EntityDestroyData),

            typeof(RPCRequestData),
            typeof(RPCResponseData),
        };

        public static readonly List<Type> RegularDataTypes = new()
        {
            typeof(Ping),
            typeof(Pong),
            typeof(ChatMessage),
            typeof(RequestFullNetworkStateSync),

            typeof(ClientRegistrationRPC.Request),
            typeof(ClientRegistrationRPC.Response),

            typeof(DynamicTextEntity.InstantiateData),
            typeof(DynamicTextEntity.SyncData),

            typeof(SessionInfoEntity.InstantiateData),
            typeof(SessionInfoEntity.SyncData),
        };

        public static readonly List<Type> MatchMakingDataTypes = new()
        {
            typeof(CreateSessionRPC.Request),
            typeof(CreateSessionRPC.Response),

            typeof(GetSessionsRPC.Request),
            typeof(GetSessionsRPC.Response),

            typeof(ConnectToSessionRPC.Request),
            typeof(ConnectToSessionRPC.Response),

            typeof(AcceptConnectionRPC.Request),
            typeof(AcceptConnectionRPC.Response),

            typeof(UpdateSessionRPC.Request),
            typeof(UpdateSessionRPC.Response),
        };
    }
}