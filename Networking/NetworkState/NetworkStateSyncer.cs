using System;
using System.Collections.Generic;
using Networking.Connections;
using Networking.Entities.Core;
using Networking.NetworkState.Configs;
using Networking.Utils;

namespace Networking.NetworkState
{
    public abstract class NetworkStateSyncer : NetworkAgent
    {
        protected int NetworkId { get; private set; } = -1;
        public NetworkState NetworkState { get; }

        private readonly Dictionary<int, Action<NetworkEntity>> _instantiationCallbacks = new();
        private readonly IdIterator _instantiationId;

        protected NetworkStateSyncer(NetworkAgentConfig config, NetworkState networkState) : base(config)
        {
            NetworkState = networkState;
            NetworkState.EntityAdded += EntityAddedHandler;

            _instantiationId = new IdIterator();

            DataBroadcaster.ListenForReceive<EntityInstantiateData>(this, EntityInstantiateDataHandler);
            DataBroadcaster.ListenForReceive<EntitySyncData>(this, EntitySyncDataHandler);
            DataBroadcaster.ListenForReceive<EntityDestroyData>(this, EntityDestroyDataHandler);
        }

        private void EntityAddedHandler(NetworkEntity networkEntity)
        {
            if (AmIOwner(networkEntity))
            {
                var localInstantiationId = networkEntity.OwnerInstantiationId;
                if (_instantiationCallbacks.ContainsKey(localInstantiationId))
                {
                    _instantiationCallbacks[localInstantiationId]?.Invoke(networkEntity);
                    _instantiationCallbacks.Remove(localInstantiationId);
                }
            }
        }

        protected int RegisterEntityInstantiationCallback(Action<NetworkEntity> entityInstantiatedCallback)
        {
            var instantiationId = _instantiationId.Next();
            if (entityInstantiatedCallback != null)
            {
                _instantiationCallbacks[instantiationId] = entityInstantiatedCallback;
            }

            return instantiationId;
        }

        /// <summary>
        /// Create Entity is external call
        /// Here we can safely apply owner id and set callbacks, then we need to delegate the data to direct instantiation or
        /// broadcasting
        /// </summary>
        public abstract void CreateEntity(EntityInstantiateData entityInstantiateData, Action<NetworkEntity> entityInstantiatedCallback = null);

        public abstract void DestroyEntity(NetworkEntity networkEntity);
        protected abstract void SyncState();

        /// <summary>
        /// Handler invoked on receiving EntityInstantiateData from server. The only place of actual adding network entity to
        /// sync models.
        /// Invoked as well directly by server to update its own state
        /// Will not be invoked for local Unauth as network state is synced via Auth
        /// </summary>
        protected abstract void EntityInstantiateDataHandler(Connection connection, EntityInstantiateData entityInstantiateData);

        /// <summary>
        /// Handler invoked on receiving EntitySyncData from server. The only place of actual inner syncing network entity to
        /// sync models.
        /// Invoked as well directly by server to update its own state
        /// </summary>
        protected abstract void EntitySyncDataHandler(Connection connection, EntitySyncData entitySyncData);

        /// <summary>
        /// Handler invoked on receiving EntityDestroyData from server. The only place of actual removing network entity to
        /// sync models.
        /// Invoked as well directly by server to update its own state
        /// </summary>
        protected abstract void EntityDestroyDataHandler(Connection connection, EntityDestroyData entityDestroyData);

        public override void MainThreadUpdate()
        {
            SyncState();
            base.MainThreadUpdate();
        }

        protected void SetNetworkId(int id)
        {
            NetworkId = id;
        }

        public virtual bool IsRegistered()
        {
            return NetworkId > 0;
        }

        public bool AmIOwner(NetworkEntity networkEntity)
        {
            return networkEntity.OwnerId == NetworkId;
        }
    }
}