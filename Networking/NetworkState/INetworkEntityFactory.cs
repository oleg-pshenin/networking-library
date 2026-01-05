using System;
using Networking.Entities.Core;

namespace Networking.NetworkState
{
    public interface INetworkEntityFactory
    {
        Type EntityInstantiateDataType { get; }
        void PreInstantiate(EntityInstantiateData entityInstantiateData, AuthSyncer authSyncer);
    }
}