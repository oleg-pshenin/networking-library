using Networking.Data.Core;
using ProtoBuf;

namespace Networking.Data
{
    [ProtoContract]
    public class RequestFullNetworkStateSync : NetworkData
    {
        [ProtoMember(1)] public int NetworkId;

        public override string ToString()
        {
            return $"NetworkData: {GetType()}, NetworkId: {NetworkId}";
        }
    }
}