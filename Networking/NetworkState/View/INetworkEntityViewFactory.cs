using System;
using Networking.Entities.Core;

namespace Networking.NetworkState.View
{
    public interface INetworkEntityViewFactory
    {
        Type NetworkEntityType { get; }


        INetworkEntityView CreateView(NetworkEntity networkEntity);
        void DestroyView(INetworkEntityView networkEntityView);
    }
}