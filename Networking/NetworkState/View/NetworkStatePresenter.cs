using System;
using System.Collections.Generic;
using Networking.Entities.Core;
using Networking.Utils;

namespace Networking.NetworkState.View
{
    public class NetworkStatePresenter : INetworkStateListener
    {
        private readonly Dictionary<Type, INetworkEntityViewFactory> _networkEntityViewFactories = new();
        private readonly Dictionary<NetworkEntity, INetworkEntityView> _networkEntityViews = new();
        private NetworkState _networkState;

        internal NetworkStatePresenter()
        {
        }

        internal void SetupNetworkState(NetworkState networkState)
        {
            if (_networkState != null)
            {
                // check for the same state and overriding if it is changed
                return;
            }

            _networkState = networkState;
            _networkState.RegisterListener(this);
        }

        public void RegisterNetworkEntityViewFactory(INetworkEntityViewFactory networkEntityViewFactory)
        {
            if (_networkEntityViewFactories.ContainsKey(networkEntityViewFactory.NetworkEntityType))
            {
                Logger.LogError($"Attempt to override already existing network entity view factory of type: {networkEntityViewFactory.NetworkEntityType}");
                return;
            }

            Logger.Log($"Registered factory of type: {networkEntityViewFactory.NetworkEntityType}");
            _networkEntityViewFactories.Add(networkEntityViewFactory.NetworkEntityType, networkEntityViewFactory);
        }

        public INetworkEntityView GetView(NetworkEntity networkEntity)
        {
            if (!_networkEntityViews.ContainsKey(networkEntity))
            {
                Logger.LogError($"Couldn't find view for {networkEntity}, check for correct registration of corresponding factory");
                return null;
            }

            return _networkEntityViews[networkEntity];
        }

        public TNetworkEntityView GetViewOfType<TNetworkEntityView>(NetworkEntity networkEntity) where TNetworkEntityView : class, INetworkEntityView
        {
            var view = GetView(networkEntity);
            if (view != null)
            {
                if (view is TNetworkEntityView networkEntityView)
                {
                    return networkEntityView;
                }
                else
                {
                    Logger.LogError($"Couldn't cast view for {networkEntity} to {typeof(TNetworkEntityView)}, its type: {view.GetType()}, check the generic type");
                    return null;
                }
            }

            return null;
        }

        void INetworkStateListener.EntityAddedHandler(NetworkEntity networkEntity)
        {
            var type = networkEntity.GetType();
            Logger.Log($"EntityAddedHandler: {type}");

            if (_networkEntityViewFactories.ContainsKey(type))
            {
                var view = _networkEntityViewFactories[type].CreateView(networkEntity);
                _networkEntityViews.Add(networkEntity, view);
            }
            else
            {
                Logger.LogError($"Missing network entity view factory for type: {type}");
            }
        }

        void INetworkStateListener.EntityRemovedHandler(NetworkEntity networkEntity)
        {
            var type = networkEntity.GetType();
            if (_networkEntityViewFactories.ContainsKey(type))
            {
                if (_networkEntityViews.ContainsKey(networkEntity))
                {
                    _networkEntityViewFactories[type].DestroyView(_networkEntityViews[networkEntity]);
                    _networkEntityViews.Remove(networkEntity);
                }
                else
                {
                    Logger.LogError($"Missing network entity view for network entity: {networkEntity}");
                }
            }
            else
            {
                Logger.LogError($"Missing network entity view factory for type: {type}");
            }
        }
    }
}