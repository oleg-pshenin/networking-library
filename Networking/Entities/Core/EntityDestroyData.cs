using Networking.Data.Core;
using ProtoBuf;

namespace Networking.Entities.Core
{
    /// <summary>
    /// Prioritize indirect constructors such as GetDestroyData within NetworkEntity
    /// </summary>
    [ProtoContract]
    public class EntityDestroyData : NetworkData
    {
        [ProtoMember(1)] internal int EntityId = -1;
        [ProtoMember(2)] internal int OwnerId = -1;

        public virtual void OnDestroy(NetworkEntity networkEntity)
        {
        }

        public override string ToString()
        {
            return $"NetworkData: {GetType()}, EntityId: {EntityId}, OwnerId: {OwnerId}";
        }
    }
}