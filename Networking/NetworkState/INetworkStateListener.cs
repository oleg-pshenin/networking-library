using Networking.Entities.Core;

namespace Networking.NetworkState
{
    public interface INetworkStateListener
    {
        /// <summary>
        /// Params are always not null
        /// </summary>
        /// <param name="networkEntity"></param>
        void EntityAddedHandler(NetworkEntity networkEntity);

        void EntityRemovedHandler(NetworkEntity networkEntity);
    }
}