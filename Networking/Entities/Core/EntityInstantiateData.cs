using Networking.Broadcasting;
using Networking.Data.Core;
using ProtoBuf;

namespace Networking.Entities.Core
{
    [ProtoContract]
    public abstract class EntityInstantiateData : NetworkData
    {
        public virtual BroadcastingChannel BroadcastingChannel => BroadcastingChannel.Reliable;

        [ProtoMember(1)] internal int EntityId = -1;
        [ProtoMember(2)] internal int OwnerId = -1;

        /// <summary>
        /// Compare to previous two fields which are in sync between clients, this particular one is in use only locally
        /// To handle instantiation callback of specific instance.
        /// It is part of protobuf though because we send instantiation data for validation to server which replies with previous
        /// two fields filled
        /// </summary>
        [ProtoMember(3)] internal int OwnerInstantiationId = 0;

        public abstract NetworkEntity Instantiate();

        public override string ToString()
        {
            return $"NetworkData: {GetType()}, EntityId: {EntityId}, OwnerId: {OwnerId}";
        }
    }
}