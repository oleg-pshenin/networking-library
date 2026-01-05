using System;
using Networking.Broadcasting;
using Networking.Connections;
using Networking.Data;
using Networking.Entities.Core;
using Networking.NetworkState.Configs;
using Networking.RPCs;
using Networking.Utils;

namespace Networking.NetworkState
{
    public class UnauthSyncer : NetworkStateSyncer
    {
        public Connection AuthConnection { get; private set; }

        protected UnauthSyncer(NetworkAgentConfig config, NetworkState networkState) : base(config, networkState)
        {
        }

        public override bool IsRegistered()
        {
            if (AuthConnection == null)
                return false;

            return base.IsRegistered();
        }

        /// <summary>
        /// As client, we can only send a request to server to instantiate the entity. We will receive the authorized
        /// instantiate data on response and the state will be in sync
        /// Good example of not using it would be to have bullets being the part some weapon network entity which is created
        /// only once then synced but without delay for owner client
        /// </summary>
        public override void CreateEntity(EntityInstantiateData entityInstantiateData, Action<NetworkEntity> entityInstantiatedCallback = null)
        {
            if (!IsRegistered())
            {
                Logger.LogError($"Can't create entity as network state syncer is not registered yet");
                return;
            }

            entityInstantiateData.OwnerId = NetworkId;
            entityInstantiateData.OwnerInstantiationId = RegisterEntityInstantiationCallback(entityInstantiatedCallback);
            DataBroadcaster.AddDataToSend(AuthConnection, entityInstantiateData, entityInstantiateData.BroadcastingChannel);
        }

        /// <summary>
        /// As client, we can only send a request to server to destroy the entity. We will receive the authorized destroy data
        /// on response and the state will be in sync
        /// </summary>
        public override void DestroyEntity(NetworkEntity networkEntity)
        {
            if (!IsRegistered())
            {
                Logger.LogError($"Can't destroy entity as network state syncer is not registered yet");
                return;
            }

            if (AmIOwner(networkEntity))
            {
                var entityDestroyData = networkEntity.GetDestroyData();
                DataBroadcaster.AddDataToSend(AuthConnection, entityDestroyData, BroadcastingChannel.Reliable);
            }
            else
            {
                Logger.LogError($"Attempt to destroy entity without ownership or auth: {networkEntity}");
            }
        }

        protected override void SyncState()
        {
            if (!IsRegistered())
                return;

            foreach (var networkEntity in NetworkState.NetworkEntities)
            {
                if (AmIOwner(networkEntity))
                {
                    foreach (var syncData in networkEntity.GetOwnerSyncData())
                    {
                        DataBroadcaster.AddDataToSend(AuthConnection, syncData, syncData.BroadcastingChannel);
                        if (networkEntity.SelfSyncRequired)
                            networkEntity.ApplySyncData(syncData);
                    }

                    networkEntity.ResetOwnerSyncData();
                }
            }
        }

        /// <summary>
        /// Handler invoked on receiving EntityInstantiateData from server. The only place of actual adding network entity to
        /// sync models.
        /// Invoked as well directly by server to update its own state
        /// Will not be invoked for local Unauth as network state is synced via Auth
        /// </summary>
        protected override void EntityInstantiateDataHandler(Connection connection, EntityInstantiateData entityInstantiateData)
        {
            if (!IsRegistered())
            {
                Logger.LogError($"Can't instantiate entity as network state syncer is not registered yet");
                return;
            }

            if (connection != AuthConnection)
            {
                Logger.LogError($"Received EntityInstantiateData: {entityInstantiateData} from unknown source: {connection}");
                return;
            }

            if (entityInstantiateData.EntityId < 0)
            {
                Logger.LogError($"Received incomplete entityInstantiateData, should be authorized by server at first: {entityInstantiateData}");
            }
            else
            {
                var networkEntity = entityInstantiateData.Instantiate();
                networkEntity.SetNetworkEntityGetter(NetworkState.GetNetworkEntity);
                if (AmIOwner(networkEntity))
                {
                    networkEntity.IsOwnerInstance = true;
                    networkEntity.SetDestroyDelegate(DestroyEntity);
                }

                NetworkState.AddNetworkEntity(networkEntity);
            }
        }

        /// <summary>
        /// Handler invoked on receiving EntitySyncData from server. The only place of actual inner syncing network entity to
        /// sync models.
        /// Invoked as well directly by server to update its own state
        /// </summary>
        protected override void EntitySyncDataHandler(Connection connection, EntitySyncData entitySyncData)
        {
            if (!IsRegistered())
            {
                Logger.LogError($"Can't sync entity as network state syncer is not registered yet");
                return;
            }

            if (connection != AuthConnection)
            {
                Logger.LogError($"Received EntitySyncData: {entitySyncData} from unknown source: {connection}");
                return;
            }

            var networkEntityToSync = NetworkState.GetNetworkEntity(entitySyncData.EntityId, entitySyncData.OwnerId);
            if (networkEntityToSync != null)
            {
                if (AmIOwner(networkEntityToSync))
                {
                    // received from server and that's fine
                    networkEntityToSync.ApplySyncData(entitySyncData);
                }
                else
                {
                    networkEntityToSync.ApplySyncData(entitySyncData);
                }
            }
        }

        /// <summary>
        /// Handler invoked on receiving EntityDestroyData from server. The only place of actual removing network entity to
        /// sync models.
        /// Invoked as well directly by server to update its own state
        /// </summary>
        protected override void EntityDestroyDataHandler(Connection connection, EntityDestroyData entityDestroyData)
        {
            if (!IsRegistered())
            {
                Logger.LogError($"Can't destroy entity as network state syncer is not registered yet");
                return;
            }

            if (connection != AuthConnection)
            {
                Logger.LogError($"Received EntityDestroyData: {entityDestroyData} from unknown source: {connection}");
                return;
            }

            var networkEntityToDestroy = NetworkState.GetNetworkEntity(entityDestroyData.EntityId, entityDestroyData.OwnerId);
            if (networkEntityToDestroy == null)
            {
                Logger.LogError($"Couldn't find entity to destroy: {entityDestroyData}");
            }
            else
            {
                NetworkState.RemoveNetworkEntity(networkEntityToDestroy);
            }
        }

        public void ConnectToAuth(Connection connection, int sessionId = -1)
        {
            if (AuthConnection != null)
            {
                Logger.LogError($"Hot reconnection to different server is not supported, restart the client");
                return;
            }

            if (connection == null)
            {
                Logger.LogError($"Can't ConnectToServer as provided null server connection");
                return;
            }

            SetAuthConnection(connection);

            var rpcRequest = new ClientRegistrationRPC.Request()
            {
                Nickname = string.Empty,
                SessionId = sessionId,
            };
            RPCManager.CallRPC<ClientRegistrationRPC.Request, ClientRegistrationRPC.Response>(AuthConnection, rpcRequest, ClientRegistrationRPCHandler, ClientRegistrationRPCFailedHandler);
        }

        protected virtual void OnConnectToAuth()
        {
        }

        private void SetAuthConnection(Connection connection)
        {
            AuthConnection = connection;
        }

        private void ClientRegistrationRPCHandler(ClientRegistrationRPC.Response response)
        {
            if (response.Accept)
            {
                SetNetworkId(response.ClientNetworkId);
                DataBroadcaster.AddDataToSend(AuthConnection, new RequestFullNetworkStateSync()
                {
                    NetworkId = NetworkId,
                }, BroadcastingChannel.Reliable);
                OnConnectToAuth();
            }
            else
            {
                Logger.LogError($"Registration is not accepted: {response.DeclineReason}");
            }
        }

        private void ClientRegistrationRPCFailedHandler()
        {
            Logger.LogError($"Couldn't get rpc response");
        }
    }
}