using System;
using System.Collections.Generic;
using Networking.Utils;

namespace Networking.Entities.Core
{
    public abstract class NetworkEntity<TInstantiateData, TSyncData> : NetworkEntity where TInstantiateData : EntityInstantiateData where TSyncData : EntitySyncData
    {
        public event Action<TSyncData> SyncDataApplied;
        public event Action Synced;

        private readonly List<EntitySyncData> _ownerSyncData = new();
        private readonly List<EntitySyncData> _authSyncData = new();

        private Func<int, NetworkEntity> _networkEntityGetter;
        private Action<NetworkEntity> _selfDestroyDelegate;

        protected NetworkEntity(EntityInstantiateData entityInstantiateData)
        {
            EntityId = entityInstantiateData.EntityId;
            OwnerId = entityInstantiateData.OwnerId;
            OwnerInstantiationId = entityInstantiateData.OwnerInstantiationId;

            if (EntityId < 0 || OwnerId < 0)
                Logger.LogError($"Created network entity with negative entity id or owner id: {this}");
        }

        internal sealed override List<EntitySyncData> GetOwnerSyncData() => _ownerSyncData;
        internal sealed override void ResetOwnerSyncData() => _ownerSyncData.Clear();
        internal sealed override List<EntitySyncData> GetAuthSyncData() => _authSyncData;
        internal sealed override void ResetAuthSyncData() => _authSyncData.Clear();

        internal sealed override EntityInstantiateData GetInstantiateData()
        {
            var instantiateData = GetInstantiateDataTyped();
            instantiateData.EntityId = EntityId;
            instantiateData.OwnerId = OwnerId;
            return instantiateData;
        }

        protected abstract TInstantiateData GetInstantiateDataTyped();

        internal sealed override EntityDestroyData GetDestroyData()
        {
            return new EntityDestroyData()
            {
                EntityId = EntityId,
                OwnerId = OwnerId,
            };
        }

        internal sealed override void SetNetworkEntityGetter(Func<int, NetworkEntity> networkEntityGetter)
        {
            _networkEntityGetter = networkEntityGetter;
        }

        internal sealed override void SetDestroyDelegate(Action<NetworkEntity> selfDestroyDelegate)
        {
            _selfDestroyDelegate = selfDestroyDelegate;
        }

        internal sealed override void ApplySyncData(EntitySyncData entitySyncData)
        {
            if (entitySyncData != null && entitySyncData is TSyncData typedSyncData)
            {
                if (entitySyncData.EntityId != EntityId || entitySyncData.OwnerId != OwnerId)
                {
                    Logger.LogError($"Attempt to sync network entity with different entity id or owner id, sync data: {entitySyncData}, network entity: {ToString()}");
                }
                else
                {
                    ApplySyncData(typedSyncData);
                    SyncDataApplied?.Invoke(typedSyncData);
                    Synced?.Invoke();
                }
            }
            else
            {
                if (entitySyncData == null)
                    Logger.LogError($"Provided null entity sync data to {ToString()}");
                else
                    Logger.LogError($"Provided incorrect type of entity sync data {entitySyncData.GetType()} to {ToString()}");
            }
        }

        protected void AddOwnerSyncData(TSyncData ownerSyncData)
        {
            if (!IsOwnerInstance)
            {
                Logger.LogError($"Attempt to add owner sync data {ownerSyncData} to entity: {this}, but it is not the owner");
                return;
            }

            if (ownerSyncData == null)
                return;

            ownerSyncData.EntityId = EntityId;
            ownerSyncData.OwnerId = OwnerId;
            _ownerSyncData.Add(ownerSyncData);
        }

        protected void AddAuthSyncData(TSyncData authSyncData)
        {
            if (!IsAuthInstance)
            {
                Logger.LogError($"Attempt to add auth sync data {authSyncData} to entity: {this}, but it is not the auth");
                return;
            }

            if (authSyncData == null)
                return;

            authSyncData.EntityId = EntityId;
            authSyncData.OwnerId = OwnerId;
            _authSyncData.Add(authSyncData);
        }

        protected abstract void ApplySyncData(TSyncData entitySyncData);

        protected NetworkEntity GetNetworkEntityById(int entityId)
        {
            return _networkEntityGetter.Invoke(entityId);
        }

        public override void Destroy()
        {
            _selfDestroyDelegate?.Invoke(this);
        }

        public override string ToString()
        {
            return $"NetworkEntity of type {GetType()}, with id {EntityId}, owner: {OwnerId}";
        }
    }
}