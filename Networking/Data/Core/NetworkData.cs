using System;
using ProtoBuf;

namespace Networking.Data.Core
{
    [ProtoContract]
    public abstract class NetworkData
    {
        /// <summary>
        /// Use carefully, for primitive data types contracts it will only INCREASE data size and significantly decrease
        /// performance, GC heavy
        /// Use only for texts/voxel data/other large arrays and then use profiling to see actual compression ratio
        /// </summary>
        public virtual bool ShouldUseCompression => false;

        public virtual bool ShouldBeLogged => true;

        /// <summary>
        /// As any class already has ToString method, new abstract is in use here for creating requirement of to string
        /// implementation
        /// </summary>
        /// <returns></returns>
        public abstract override string ToString();

        /// <summary>
        /// Optional value for sequenced protocols
        /// How to guarantee non empty
        /// </summary>
        internal int SequenceIndex;

        public virtual int GetSequenceIndex()
        {
            throw new NotImplementedException($"UpdateSequenceIndex is not implemented for data sent to sequence protocol: {GetType()}");
        }
    }
}