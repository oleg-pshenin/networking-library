using System;
using Networking.Utils;

namespace Networking.Entities.Core
{
    public class SyncRateTimer
    {
        public double SyncRate { get; private set; }
        private double _lastSyncTimestampMs;

        public SyncRateTimer(double syncRate = 30)
        {
            _lastSyncTimestampMs = 0;
            SetSyncRate(syncRate);
        }

        public void SetSyncRate(double syncRate)
        {
            SyncRate = syncRate;
        }

        public bool TrySync()
        {
            if (SyncRate > 0)
            {
                var currentTimestampMs = TimeUtils.GetUtcTimestampMs();
                var frameTimeMs = 1000.0 / SyncRate;

                if (_lastSyncTimestampMs + frameTimeMs < currentTimestampMs)
                {
                    _lastSyncTimestampMs = Math.Floor(currentTimestampMs / frameTimeMs) * frameTimeMs;
                    return true;
                }

                return false;
            }

            return false;
        }
    }
}