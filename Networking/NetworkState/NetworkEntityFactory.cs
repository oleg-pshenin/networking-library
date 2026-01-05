using System;
using Networking.Entities.Core;

namespace Networking.NetworkState
{
    /// <summary>
    /// Executes on Auth side just before creation of network entity from EntityInstantiateData
    /// </summary>
    public abstract class NetworkEntityFactory<TEntityInstantiateData> : INetworkEntityFactory where TEntityInstantiateData : EntityInstantiateData
    {
        public Type EntityInstantiateDataType => typeof(TEntityInstantiateData);

        public void PreInstantiate(EntityInstantiateData entityInstantiateData, AuthSyncer authSyncer)
        {
            PreInstantiate(entityInstantiateData as TEntityInstantiateData, authSyncer);
        }

        protected abstract void PreInstantiate(TEntityInstantiateData entityInstantiateData, AuthSyncer authSyncer);
    }
}