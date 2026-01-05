using Networking.Broadcasting;
using Networking.Data.Core;
using ProtoBuf;

namespace Networking.Entities.Core
{
    [ProtoContract]
    public abstract class EntitySyncData : NetworkData
    {
        public abstract BroadcastingChannel BroadcastingChannel { get; }

        [ProtoMember(1)] internal int EntityId = -1;
        [ProtoMember(2)] internal int OwnerId = -1;

        public override string ToString()
        {
            return $"NetworkData: {GetType()}, EntityId: {EntityId}, OwnerId: {OwnerId}";
        }

        public override int GetSequenceIndex()
        {
            return (EntityId, OwnerId).GetHashCode();
        }
    }
}