using System;
using System.Collections.Generic;
using Networking.Broadcasting;
using Networking.Connections;
using Networking.Data;
using Networking.Entities;
using Networking.Entities.Core;
using Networking.NetworkState.Configs;
using Networking.RPCs;
using Networking.Utils;

namespace Networking.NetworkState
{
    /// <summary>
    /// Minimal server implementation which works with direct connections only
    /// </summary>
    public class AuthSyncer : NetworkStateSyncer
    {
        protected readonly List<Connection> UnauthConnections = new();
        private readonly Dictionary<Connection, int> _networkIdByConnection = new();

        private readonly IdIterator _entityId;
        private readonly IdIterator _networkId;

        private Connection _localUnauthConnection;
        private SessionInfoEntity _sessionInfo;
        private SyncRateTimer _sessionInfoSyncRateTimer;
        private Dictionary<Type, INetworkEntityFactory> _networkEntityFactoryByInstantiateDataType = new();

        protected AuthSyncer(NetworkAgentConfig config, NetworkState networkState) : base(config, networkState)
        {
            _entityId = new IdIterator();
            _networkId = new IdIterator();

            ConnectionManager.ConnectionRemoved += ConnectionRemovedHandler;

            SetNetworkId(_networkId.Next());

            DataBroadcaster.ListenForReceive<RequestFullNetworkStateSync>(this, RequestFullNetworkStateSyncHandler);
            RPCManager.HandleRPCSync<ClientRegistrationRPC.Request, ClientRegistrationRPC.Response>(this, ClientRegistrationRPCHandler);
        }

        public void RegisterNetworkEntityFactory(INetworkEntityFactory networkEntityFactory)
        {
            if (_networkEntityFactoryByInstantiateDataType.ContainsKey(networkEntityFactory.EntityInstantiateDataType))
            {
                Logger.LogError($"Attempt to override network entity factory of type: {networkEntityFactory.EntityInstantiateDataType}");
                return;
            }

            _networkEntityFactoryByInstantiateDataType.Add(networkEntityFactory.EntityInstantiateDataType, networkEntityFactory);
        }

        public override void MainThreadUpdate()
        {
            base.MainThreadUpdate();
            SessionInfoFullRefresh();
        }

        public void InitSessionInfo()
        {
            Logger.Log($"InitSessionInfo");
            CreateEntity(new SessionInfoEntity.InstantiateData(), SessionInfoEntityInstantiationHandler);
        }

        private void SessionInfoEntityInstantiationHandler(NetworkEntity networkEntity)
        {
            _sessionInfo = networkEntity as SessionInfoEntity;
            _sessionInfoSyncRateTimer = new SyncRateTimer(2);
            SessionInfoFullRefresh();
        }


        // updating data based on some delay
        // entity itself doesn't have access to that so we updating it externally
        private void SessionInfoFullRefresh()
        {
            if (_sessionInfoSyncRateTimer == null || _sessionInfo == null)
                return;

            if (_sessionInfoSyncRateTimer.TrySync())
            {
                foreach (var connection in UnauthConnections)
                {
                    _sessionInfo.UpdatePlayer(new SessionInfoEntity.PlayerInfo()
                    {
                        IPEndPoint = connection.IPEndPoint.ToString(),
                        Nickname = $"Nickname_{connection}",
                        Ping = connection.RTT,
                    });
                }

                _sessionInfo.Sync();
            }
        }

        public void SetLocalUnauthConnection(Connection connection)
        {
            if (_localUnauthConnection != null)
            {
                Logger.LogError($"Attempt to call MarkConnectionAsLocal {connection} with already existing local connection: {_localUnauthConnection.IPEndPoint}");
            }

            _localUnauthConnection = connection;
        }

        // can be rpc with various possible responses
        private void RequestFullNetworkStateSyncHandler(Connection connection, RequestFullNetworkStateSync requestFullNetworkStateSync)
        {
            if (connection == _localUnauthConnection)
                return;

            if (UnauthConnections.Contains(connection))
            {
                var networkId = _networkIdByConnection[connection];
                if (networkId == requestFullNetworkStateSync.NetworkId)
                {
                    foreach (var networkEntity in NetworkState.NetworkEntities)
                    {
                        if (networkEntity.OwnerId != networkId)
                        {
                            var instantiateData = networkEntity.GetInstantiateData();
                            instantiateData.EntityId = networkEntity.EntityId;
                            instantiateData.OwnerId = networkEntity.OwnerId;
                            if (instantiateData.OwnerId == -1 || instantiateData.EntityId == -1)
                            {
                                Logger.LogError($"Incorrect instantiate data: {instantiateData} from network entity: {networkEntity}");
                                continue;
                            }

                            DataBroadcaster.AddDataToSend(connection, instantiateData, instantiateData.BroadcastingChannel);
                        }
                    }
                }
                else
                {
                    Logger.LogError($"Client network id is different from requested");
                }
            }
        }

        private void ConnectionRemovedHandler(Connection connection)
        {
            if (UnauthConnections.Contains(connection))
            {
                var networkId = _networkIdByConnection[connection];

                UnauthConnections.Remove(connection);
                _networkIdByConnection.Remove(connection);

                OnUnauthConnectionRemoved(connection);

                var entitiesToDestroy = new List<NetworkEntity>();
                foreach (var networkEntity in NetworkState.NetworkEntities)
                {
                    if (networkEntity.OwnerId == networkId)
                    {
                        entitiesToDestroy.Add(networkEntity);
                    }
                }

                foreach (var networkEntity in entitiesToDestroy)
                {
                    EntityDestroyDataHandler(null, networkEntity.GetDestroyData());
                }

                _sessionInfo?.RemovePlayer(connection.IPEndPoint.ToString());
            }
        }

        protected virtual ClientRegistrationRPC.Response ClientRegistrationRPCHandler(Connection connection, ClientRegistrationRPC.Request request)
        {
            if (!UnauthConnections.Contains(connection))
            {
                UnauthConnections.Add(connection);
                var networkId = _networkId.Next();
                _networkIdByConnection[connection] = networkId;

                OnUnauthConnectionAdded(connection);

                _sessionInfo.UpdatePlayer(new SessionInfoEntity.PlayerInfo()
                {
                    IPEndPoint = connection.IPEndPoint.ToString(),
                    Nickname = $"Nickname_{connection}",
                    Ping = connection.RTT,
                });

                return new ClientRegistrationRPC.Response()
                {
                    Accept = true,
                    ClientNetworkId = networkId,
                    ServerNetworkId = NetworkId,
                };
            }

            return new ClientRegistrationRPC.Response()
            {
                Accept = true,
                ClientNetworkId = _networkIdByConnection[connection],
                ServerNetworkId = NetworkId,
            };
        }

        protected virtual void OnUnauthConnectionAdded(Connection connection)
        {
        }

        protected virtual void OnUnauthConnectionRemoved(Connection connection)
        {
        }

        /// <summary>
        /// As server instead of sending the data to ourself, we directly invoke the handler with that data
        /// </summary>
        public override void CreateEntity(EntityInstantiateData entityInstantiateData, Action<NetworkEntity> entityInstantiatedCallback = null)
        {
            entityInstantiateData.OwnerId = NetworkId;
            entityInstantiateData.OwnerInstantiationId = RegisterEntityInstantiationCallback(entityInstantiatedCallback);
            EntityInstantiateDataHandler(null, entityInstantiateData);
        }

        public NetworkEntity CreateEntityInstantly(EntityInstantiateData entityInstantiateData)
        {
            entityInstantiateData.OwnerId = NetworkId;
            return CreateNetworkEntity(entityInstantiateData);
        }

        /// <summary>
        /// As server instead of sending the data to ourself, we directly invoke the handler with that data
        /// </summary>
        public override void DestroyEntity(NetworkEntity networkEntity)
        {
            var entityDestroyData = networkEntity.GetDestroyData();
            EntityDestroyDataHandler(null, entityDestroyData);
        }

        protected override void SyncState()
        {
            if (!IsRegistered())
                return;

            void BroadcastSyncEntityData(EntitySyncData entitySyncData)
            {
                foreach (var unauthConnection in UnauthConnections)
                {
                    if (unauthConnection != _localUnauthConnection)
                        DataBroadcaster.AddDataToSend(unauthConnection, entitySyncData, entitySyncData.BroadcastingChannel);
                }
            }

            foreach (var networkEntity in NetworkState.NetworkEntities)
            {
                if (AmIOwner(networkEntity))
                {
                    foreach (var syncData in networkEntity.GetOwnerSyncData())
                    {
                        BroadcastSyncEntityData(syncData);
                        if (networkEntity.SelfSyncRequired)
                            networkEntity.ApplySyncData(syncData);
                    }

                    networkEntity.ResetOwnerSyncData();
                }
                else
                {
                    foreach (var syncData in networkEntity.GetAuthSyncData())
                    {
                        BroadcastSyncEntityData(syncData);
                        if (networkEntity.SelfSyncRequired)
                            networkEntity.ApplySyncData(syncData);
                    }

                    networkEntity.ResetAuthSyncData();
                }
            }
        }

        /// <summary>
        /// As a server this handler is invoked only on client request to instantiate network entity
        /// We should assign new unique entity id and broadcast it to everyone including the one who send it to us.
        /// As clients can't directly instantiate entities
        /// </summary>
        protected override void EntityInstantiateDataHandler(Connection connection, EntityInstantiateData entityInstantiateData)
        {
            CreateNetworkEntity(entityInstantiateData, connection == _localUnauthConnection);
        }

        /// <summary>
        /// Synchronously creates entity, called both by client request handler and locally with null connection
        /// Returns just created network entity
        /// </summary>
        private NetworkEntity CreateNetworkEntity(EntityInstantiateData entityInstantiateData, bool localUnauthConnection = false)
        {
            if (entityInstantiateData.EntityId > 0)
            {
                // it is fine though if we load from the save, but entities binding should be done separately, not clear what to do yet
                // also not clear what happens with owners id, only server has reliable owner id
                // Logger.LogError($"Received authorized entityInstantiateData, should be authorized by server only: {entityInstantiateData}");
                // return;
            }

            entityInstantiateData.EntityId = _entityId.Next();
            var entityInstantiateDataType = entityInstantiateData.GetType();
            if (_networkEntityFactoryByInstantiateDataType.ContainsKey(entityInstantiateDataType))
            {
                _networkEntityFactoryByInstantiateDataType[entityInstantiateDataType].PreInstantiate(entityInstantiateData, this);
            }

            foreach (var unauthConnection in UnauthConnections)
            {
                if (unauthConnection != _localUnauthConnection)
                    DataBroadcaster.AddDataToSend(unauthConnection, entityInstantiateData, entityInstantiateData.BroadcastingChannel);
            }

            var networkEntity = entityInstantiateData.Instantiate();
            networkEntity.IsAuthInstance = true;
            networkEntity.IsOwnerInstance = AmIOwner(networkEntity) || localUnauthConnection;
            networkEntity.SetNetworkEntityGetter(NetworkState.GetNetworkEntity);
            networkEntity.SetDestroyDelegate(DestroyEntity);
            NetworkState.AddNetworkEntity(networkEntity);

            return networkEntity;
        }

        /// <summary>
        /// As a server this handler is invoked only on client request to sync network entity
        /// We should verify that this entity still exists and then broadcast it to everyone except for the one who send it to
        /// us.
        /// As clients send sync requests based on the changes of entities they have authority on
        /// </summary>
        protected override void EntitySyncDataHandler(Connection connection, EntitySyncData entitySyncData)
        {
            if (!IsRegistered())
            {
                Logger.LogError($"Can't sync entity as network state syncer is not registered yet");
                return;
            }

            var networkEntityToSync = NetworkState.GetNetworkEntity(entitySyncData.EntityId, entitySyncData.OwnerId);
            if (networkEntityToSync == null)
            {
                Logger.LogError($"Couldn't find entity to sync: {entitySyncData}");
            }
            else
            {
                if (AmIOwner(networkEntityToSync))
                {
                    Logger.LogError($"Received sync packet for entity owned by myself and I am AUTH, ignoring: {entitySyncData}");
                }
                else
                {
                    foreach (var unauthConnection in UnauthConnections)
                    {
                        if (unauthConnection != connection && unauthConnection != _localUnauthConnection)
                            DataBroadcaster.AddDataToSend(unauthConnection, entitySyncData, entitySyncData.BroadcastingChannel);
                    }

                    // we don't need to apply any sync data locally if we got it from local connection
                    if (connection != _localUnauthConnection)
                    {
                        networkEntityToSync.ApplySyncData(entitySyncData);
                    }
                }
            }
        }

        /// <summary>
        /// As a server this handler is invoked only on client request to destroy network entity
        /// We should verify that this entity still exists and then broadcast it to everyone including the one who send it to
        /// us.
        /// As clients can't directly destroy entities
        /// </summary>
        protected override void EntityDestroyDataHandler(Connection connection, EntityDestroyData entityDestroyData)
        {
            if (!IsRegistered())
            {
                Logger.LogError($"Can't destroy entity as network state syncer is not registered yet");
                return;
            }

            var networkEntityToDestroy = NetworkState.GetNetworkEntity(entityDestroyData.EntityId, entityDestroyData.OwnerId);
            if (networkEntityToDestroy == null)
            {
                Logger.LogError($"Couldn't find entity to destroy: {entityDestroyData}");
            }
            else
            {
                foreach (var unauthConnection in UnauthConnections)
                {
                    if (unauthConnection != _localUnauthConnection)
                        DataBroadcaster.AddDataToSend(unauthConnection, entityDestroyData, BroadcastingChannel.Reliable);
                }

                NetworkState.RemoveNetworkEntity(networkEntityToDestroy);
            }
        }
    }
}