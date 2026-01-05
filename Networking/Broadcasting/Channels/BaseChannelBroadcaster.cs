using System.Collections.Generic;
using EasyCompressor;
using Networking.Broadcasting.Packets;
using Networking.Connections;
using Networking.Data.Core;
using Networking.Utils;

namespace Networking.Broadcasting.Channels
{
    public abstract class BaseChannelBroadcaster
    {
        protected virtual bool FragmentedChannel => false;

        /// <summary>
        /// Some safe value below 1500 bytes of ethernet mtu
        /// </summary>
        protected const int MaxPacketDataSize = 1400;
        private readonly BaseCompressor _compressor;

        internal BaseChannelBroadcaster()
        {
            // lzma is very good at compressing (90%-99% on real average voxel data), but quite low on performance and very bad for GA
            // zstd looks like perfect fit in terms of minimum allocations and very high speed and more or less similar compression as lzma
            // do not use built in deflate, it is shity
            _compressor = new ZstdCompressor();
        }


        // TODO: should be replaced with dictionary?
        /// <summary>
        /// Internal list of pairs connection + network data which then getting converted to packets. Filled when
        /// PrepareToBroadcast is called
        /// </summary>
        protected readonly List<(Connection connection, NetworkData baseData)> QueuedDataToBroadcast = new();


        // TODO: should be replaced with dictionary?
        /// <summary>
        /// List which is getting sent back to DataBroadcaster when GetPacketsToBroadcast is called
        /// Cached only means that it is used for garbage free list usage
        /// </summary>
        protected readonly List<(Connection connection, BasePacket baseData)> PacketsToBroadcastCached = new();

        public abstract void AddConnection(Connection connection);
        public abstract void RemoveConnection(Connection connection);

        /// <summary>
        /// Queuing data to send before centralised broadcasting by tick over all of the channels
        /// </summary>
        public abstract void AddDataToSend(Connection connection, NetworkData data);

        /// <summary>
        /// Returns base packets, but internally each of channel has its own packets types to manage different scenarios such
        /// as AckPackages for Reliable UDP
        /// </summary>
        public abstract List<(Connection connection, BasePacket packet)> GetPacketsToBroadcast();

        /// <summary>
        /// Extracts data from packet with a type associated with specific channel type, taking metadata.
        /// Return sometimes can be null if packet contains metadata only
        /// </summary>
        /// <returns>Null or NetworkData</returns>
        public abstract NetworkData GetDataFromPacket(Connection connection, BasePacket packet);

        protected byte[] SerializeNetworkData(Connection connection, NetworkData baseData)
        {
            var data = ProtobufSerializer.ObjectToByteArray(baseData);
            var rawSize = data.Length;

            if (baseData.ShouldUseCompression)
            {
                data = _compressor.Compress(data);

                if (rawSize < data.Length)
                {
                    Logger.LogError($"Data after compression takes more size, data is not suitable for effective compression, please set to false ShouldUseCompression property: {baseData}");
                }
                else if (rawSize * 0.5 < data.Length)
                {
                    Logger.LogWarning($"Compression ratio is more than 0.5, data is not suitable for effective compression, performance cost can be bigger than bandwidth: {baseData}");
                }

                Logger.Log($"Compression ratio: {(double)data.Length / rawSize:0.00} of {baseData}");
            }

            if (!FragmentedChannel && data.Length > MaxPacketDataSize)
            {
                Logger.LogError($"Attempt to send data of size: {data.Length} with max packet size limit: {MaxPacketDataSize}, {baseData}");
                if (!baseData.ShouldUseCompression)
                {
                    Logger.LogError($"Try to use compression for this data: {baseData}");
                }
            }

            connection.OutcomingTraffic.AddDetailedData(baseData.GetType(), rawSize, data.Length);

            return data;
        }

        protected NetworkData DeserializeNetworkData(Connection connection, BasePacket basePacket)
        {
            var compressedSize = basePacket.Data.Length;
            if (basePacket.IsCompressed)
                basePacket.Data = _compressor.Decompress(basePacket.Data);

            var baseData = ProtobufSerializer.ByteArrayToObject<NetworkData>(basePacket.Data);
            connection.IncomingTraffic.AddDetailedData(baseData.GetType(), basePacket.Data.Length, compressedSize);

            return baseData;
        }

        protected NetworkData DeserializeNetworkData(Connection connection, bool isCompressed, byte[] data)
        {
            var compressedSize = data.Length;
            if (isCompressed)
                data = _compressor.Decompress(data);

            var baseData = ProtobufSerializer.ByteArrayToObject<NetworkData>(data);
            connection.IncomingTraffic.AddDetailedData(baseData.GetType(), data.Length, compressedSize);

            return baseData;
        }
    }
}