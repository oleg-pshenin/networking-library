using System.Collections.Generic;
using Networking.Broadcasting;
using Networking.Entities.Core;
using ProtoBuf;

namespace Networking.Entities
{
    public class SessionInfoEntity : NetworkEntity<SessionInfoEntity.InstantiateData, SessionInfoEntity.SyncData>
    {
        public override bool SelfSyncRequired => true;

        [ProtoContract]
        public class PlayerInfo
        {
            [ProtoMember(1)] public string IPEndPoint;
            [ProtoMember(2)] public string Nickname;
            [ProtoMember(3)] public double Ping;
        }

        [ProtoContract]
        public class InstantiateData : EntityInstantiateData
        {
            [ProtoMember(1)] public List<PlayerInfo> Players = new();

            public override NetworkEntity Instantiate()
            {
                return new SessionInfoEntity(this);
            }

            public override string ToString()
            {
                return $"NetworkData: {GetType()}, EntityId: {EntityId}, OwnerId: {OwnerId}, Players: {Players.Count}";
            }
        }

        [ProtoContract]
        public class SyncData : EntitySyncData
        {
            public override BroadcastingChannel BroadcastingChannel => BroadcastingChannel.UnreliableSequenced;

            [ProtoMember(1)] public List<PlayerInfo> Players = new();

            public override string ToString()
            {
                return $"NetworkData: {GetType()}, EntityId: {EntityId}, OwnerId: {OwnerId}, Players: {Players.Count}";
            }
        }

        private SessionInfoEntity(InstantiateData instantiateData) : base(instantiateData)
        {
            Players = instantiateData.Players;
        }

        public List<PlayerInfo> Players;

        protected override InstantiateData GetInstantiateDataTyped()
        {
            return new InstantiateData()
            {
                Players = Players,
            };
        }

        protected override void ApplySyncData(SyncData entitySyncData)
        {
            Players = entitySyncData.Players;
        }

        public void UpdatePlayer(PlayerInfo playerInfo)
        {
            var found = false;
            foreach (var player in Players)
            {
                if (player.IPEndPoint == playerInfo.IPEndPoint)
                {
                    Players[Players.IndexOf(player)].Nickname = playerInfo.Nickname;
                    Players[Players.IndexOf(player)].Ping = playerInfo.Ping;
                    found = true;
                    break;
                }
            }

            if (!found)
                Players.Add(playerInfo);
        }

        public void RemovePlayer(string ipEndPoint)
        {
            var playerIndex = -1;
            foreach (var player in Players)
            {
                playerIndex++;
                if (player.IPEndPoint == ipEndPoint)
                {
                    playerIndex = Players.IndexOf(player);
                }
            }

            if (playerIndex >= 0)
            {
                Players.RemoveAt(playerIndex);
            }
        }

        public void Sync()
        {
            AddOwnerSyncData(new SyncData()
            {
                Players = Players,
            });
        }

        public override void MainThreadUpdate()
        {
        }
    }
}