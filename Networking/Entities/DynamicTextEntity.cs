using Networking.Broadcasting;
using Networking.Entities.Core;
using ProtoBuf;

namespace Networking.Entities
{
    public class DynamicTextEntity : NetworkEntity<DynamicTextEntity.InstantiateData, DynamicTextEntity.SyncData>
    {
        [ProtoContract]
        public class InstantiateData : EntityInstantiateData
        {
            [ProtoMember(1)] public string Text;

            public override NetworkEntity Instantiate()
            {
                return new DynamicTextEntity(this);
            }
        }

        [ProtoContract]
        public class SyncData : EntitySyncData
        {
            // should be reliable sequenced
            public override BroadcastingChannel BroadcastingChannel => BroadcastingChannel.ReliableSequenced;

            [ProtoMember(1)] public string Text;
        }

        private DynamicTextEntity(InstantiateData instantiateData) : base(instantiateData)
        {
            Text = instantiateData.Text;
        }

        private string _text;

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                AddOwnerSyncData(new SyncData()
                {
                    Text = Text,
                });
            }
        }

        protected override InstantiateData GetInstantiateDataTyped()
        {
            return new InstantiateData()
            {
                Text = Text,
            };
        }

        protected override void ApplySyncData(SyncData entitySyncData)
        {
            Text = entitySyncData.Text;
        }

        public override void MainThreadUpdate()
        {
        }
    }
}