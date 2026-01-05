using System;
using System.Collections.Generic;
using System.Linq;
using Networking.Entities.Core;
using Networking.Utils;

namespace Networking.NetworkState
{
    public class NetworkState : IMainThreadUpdateable
    {
        public bool IsShared { get; internal set; }

        public event Action<NetworkEntity> EntityAdded;
        public event Action<NetworkEntity> EntityRemoved;

        public readonly List<NetworkEntity> NetworkEntities = new();
        public readonly Dictionary<int, NetworkEntity> NetworkEntitiesById = new();

        private readonly List<INetworkStateListener> _listener = new();

        public void RegisterListener(INetworkStateListener listener, bool invokeAllPrevious = true)
        {
            if (listener == null)
                return;

            if (_listener.Contains(listener))
            {
                Logger.LogError($"Attempt to register listener second time: {listener}");
                return;
            }

            foreach (var entity in NetworkEntities)
            {
                listener.EntityAddedHandler(entity);
            }

            _listener.Add(listener);
        }

        public void RemoveListener(INetworkStateListener listener)
        {
            if (_listener.Contains(listener))
            {
                _listener.Remove(listener);
            }
            else
            {
                Logger.LogError($"Attempt to remove not registered listener: {listener}");
            }
        }

        internal NetworkEntity GetNetworkEntity(int entityId, int ownerId)
        {
            var networkEntity = GetNetworkEntity(entityId);
            if (networkEntity == null) 
                return null;

            if (networkEntity.OwnerId != ownerId)
            {
                Logger.LogError($"Different owner id of entity with the same entity id");
                return null;
            }

            return networkEntity;
        }

        internal NetworkEntity GetNetworkEntity(int entityId)
        {
            if (!NetworkEntitiesById.ContainsKey(entityId))
            {
                Logger.LogError($"Couldn't find network entity with id: {entityId}");
                return null;
            }

            return NetworkEntitiesById[entityId];
        }

        internal void AddNetworkEntity(NetworkEntity networkEntity)
        {
            if (networkEntity == null)
            {
                Logger.LogError($"Attempt to add null network entity");
                return;
            }

            if (NetworkEntitiesById.ContainsKey(networkEntity.EntityId))
            {
                Logger.LogError($"Attempt to add network entity with the same entity id, existing: {NetworkEntitiesById[networkEntity.EntityId]}, new: {networkEntity}");
                return;
            }

            if (NetworkEntities.Contains(networkEntity))
            {
                Logger.LogError($"Attempt to add network entity which already was added, but with different entity id: {networkEntity}");
                return;
            }

            NetworkEntities.Add(networkEntity);
            NetworkEntitiesById.Add(networkEntity.EntityId, networkEntity);

            foreach (var listener in _listener)
            {
                listener?.EntityAddedHandler(networkEntity);
            }

            EntityAdded?.Invoke(networkEntity);
        }

        internal void RemoveNetworkEntity(NetworkEntity networkEntity)
        {
            if (networkEntity == null)
            {
                Logger.LogError($"Attempt to remove null network entity");
                return;
            }

            if (NetworkEntitiesById.ContainsKey(networkEntity.EntityId))
            {
                NetworkEntitiesById.Remove(networkEntity.EntityId);
                NetworkEntities.Remove(networkEntity);
            }
            else
            {
                Logger.LogError($"Attempt to remove network entity but couldn't find it by entity id: {networkEntity}");

                if (NetworkEntities.Contains(networkEntity))
                {
                    Logger.LogError($"Also it exists as an object with different entity id: {networkEntity}");
                    NetworkEntities.Remove(networkEntity);

                    var oldEntityId = -1;
                    foreach (var key in NetworkEntitiesById.Keys)
                    {
                        if (NetworkEntitiesById[key] == networkEntity)
                        {
                            oldEntityId = key;
                            break;
                        }
                    }

                    if (oldEntityId == -1)
                    {
                        Logger.LogError($"Couldn't found instance in dictionary so it was registered incorrectly: {networkEntity}");
                    }
                    else
                    {
                        NetworkEntitiesById.Remove(oldEntityId);
                    }
                }
                else
                {
                    return;
                }
            }

            foreach (var listener in _listener)
            {
                listener?.EntityRemovedHandler(networkEntity);
            }

            EntityRemoved?.Invoke(networkEntity);
        }

        public void MainThreadUpdate()
        {
            foreach (var networkEntity in NetworkEntities)
            {
                networkEntity.MainThreadUpdate();
            }
        }

        public void ShutDown()
        {
            foreach (var networkEntity in NetworkEntities.ToList())
            {
                RemoveNetworkEntity(networkEntity);
            }

            NetworkEntities.Clear();
            NetworkEntitiesById.Clear();
            _listener.Clear();
            IsShared = false;
        }
    }
}