using System;
using System.Collections.Generic;
using Networking.Utils;

namespace Networking.Connections
{
    public class TrafficInfo
    {
        /// <summary>
        /// Constant offset in milliseconds to prevent zero division, equal to zero delay between 60 fps machines
        /// </summary>
        private const double InitialTimestampOffset = -16.6;

        public event Action TrafficUpdated;

        public bool AnyDataReceived { get; private set; }
        public double FirstDataReceivedTimestampMs { get; private set; }
        public double LastDataReceivedTimestampMs { get; private set; }
        public int TotalTrafficSize { get; private set; }
        public int TotalPacketsNumber { get; private set; }
        public double TotalTrafficTimeS => (LastDataReceivedTimestampMs - FirstDataReceivedTimestampMs) * 0.001;

        // bytes a second
        public double TrafficPerSecond => AnyDataReceived ? TotalTrafficSize / TotalTrafficTimeS : 0;
        public readonly Dictionary<Type, (int number, int size, int compressedSize)> DetailedNetworkData = new();

        public TrafficInfo()
        {
            var timeStampMs = TimeUtils.GetUtcTimestampMs();

            FirstDataReceivedTimestampMs = timeStampMs + InitialTimestampOffset;
            LastDataReceivedTimestampMs = timeStampMs;
        }

        internal void AddRawData(int size, int packetsNumber)
        {
            var timeStampMs = TimeUtils.GetUtcTimestampMs();

            if (!AnyDataReceived)
            {
                FirstDataReceivedTimestampMs = timeStampMs + InitialTimestampOffset;
                AnyDataReceived = true;
            }

            LastDataReceivedTimestampMs = timeStampMs;
            TotalTrafficSize += size;
            TotalPacketsNumber += packetsNumber;

            TrafficUpdated?.Invoke();
        }

        internal void AddDetailedData(Type networkDataType, int size, int compressedSize)
        {
            if (!DetailedNetworkData.ContainsKey(networkDataType))
                DetailedNetworkData.Add(networkDataType, (0, 0, 0));

            var current = DetailedNetworkData[networkDataType];
            current.number += 1;
            current.size += size;
            current.compressedSize += compressedSize;
            DetailedNetworkData[networkDataType] = current;
        }
    }
}