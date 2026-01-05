using System;
using Networking.Entities.Core;

namespace Networking.NetworkState.View
{
    public abstract class NetworkEntityViewFactory<TNetworkEntity> : INetworkEntityViewFactory where TNetworkEntity : NetworkEntity
    {
        public Type NetworkEntityType => typeof(TNetworkEntity);

        public INetworkEntityView CreateView(NetworkEntity networkEntity)
        {
            return CreateView(networkEntity as TNetworkEntity);;
        }

        public void DestroyView(INetworkEntityView networkEntityView)
        {
            // later to be handled more broad?
            networkEntityView.DestroyView();
        }

        // should network entity be able to mark themselves as the one to remove
        // so that destroying flow can be initiated from view (as well as state changing happens already
        // also need to figure out what to send to destroy
        // should it be view interface instead of network entity?
        // how to define the type?
        // one network entity can be represented as many instances of many things

        protected abstract INetworkEntityView CreateView(TNetworkEntity networkEntity);
    }
}