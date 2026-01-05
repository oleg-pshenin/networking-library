using System;
using System.Collections.Generic;
using Networking.Utils;

namespace Networking.Entities.Core
{
    /// <summary>
    /// Acts as an interface but with combination of public and internal to prevent accident access
    /// </summary>
    public abstract class NetworkEntity : IMainThreadUpdateable
    {
        /// <summary>
        /// When changes are applied to entity directly and not to view (esp if view acts as read only), if true,
        /// sync data wil lbe applied to yourself with refresh
        /// </summary>
        public virtual bool SelfSyncRequired => false;

        public int EntityId { get; internal set; }
        public int OwnerId { get; internal set; }
        internal int OwnerInstantiationId { get; set; }
        public bool IsOwnerInstance { get; internal set; }
        public bool IsAuthInstance { get; internal set; }
        public abstract void Destroy();
        public abstract override string ToString();
        public abstract void MainThreadUpdate();

        public EntityInstantiateData GetInstantiateDataForSave()
        {
            // Later to think about encapsulation
            return GetInstantiateData();
        }

        internal abstract EntityInstantiateData GetInstantiateData();
        internal abstract EntityDestroyData GetDestroyData();
        internal abstract List<EntitySyncData> GetOwnerSyncData();
        internal abstract void ResetOwnerSyncData();
        internal abstract List<EntitySyncData> GetAuthSyncData();
        internal abstract void ResetAuthSyncData();

        internal abstract void ApplySyncData(EntitySyncData entitySyncData);
        internal abstract void SetDestroyDelegate(Action<NetworkEntity> selfDestroyDelegate);
        internal abstract void SetNetworkEntityGetter(Func<int, NetworkEntity> networkEntityGetter);
    }
}