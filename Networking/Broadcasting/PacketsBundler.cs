using System.Collections.Generic;
using Networking.Broadcasting.Packets;
using Networking.Utils;

namespace Networking.Broadcasting
{
    internal class PacketsBundler
    {
        // 576 of ip MTU - 8 UDP - 20-60 IP
        // private const int MAX_BUNDLE_SIZE_IN_BYTES = 508;
        // 1500 of ethernet MTU - 8 UDP - 20-60 IP
        private const int MAX_BUNDLE_SIZE_IN_BYTES = 1432;

        // a bit dangerous as it requires usage of list before the next bundling
        private readonly List<PacketsBundle> _bundles = new();

        internal List<PacketsBundle> Bundle(List<BasePacket> packets)
        {
            _bundles.Clear();
            PacketsBundle currentBundle = null;

            var cumulativeSize = 0;
            foreach (var packet in packets)
            {
                var packetSize = packet.GetPacketSize();
                if (packetSize > MAX_BUNDLE_SIZE_IN_BYTES)
                {
                    Logger.LogError($"Attempt to bundle packet of size bigger than maximum size: {MAX_BUNDLE_SIZE_IN_BYTES.ToDataSize()}, please use fragmented protocol: {packetSize.ToDataSize()}");
                    // continue;
                }

                if (currentBundle == null || cumulativeSize + packetSize > MAX_BUNDLE_SIZE_IN_BYTES)
                {
                    currentBundle = new PacketsBundle();
                    _bundles.Add(currentBundle);
                    cumulativeSize = 0;
                }

                currentBundle.Packets.Add(packet);
                cumulativeSize += packetSize;
            }

            return _bundles;
        }

        internal List<BasePacket> UnBundle(PacketsBundle packetsBundle)
        {
            return packetsBundle.Packets;
        }
    }
}