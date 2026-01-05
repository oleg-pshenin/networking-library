using System;

namespace Networking.Utils
{
    public static class TimeUtils
    {
        private static readonly DateTime UnixStartTime = new(1970, 1, 1);

        /// <summary>
        /// Returns seconds elapsed from 1970.1.1 to utc now
        /// </summary>
        public static double GetUtcTimestamp()
        {
            return DateTime.UtcNow.Subtract(UnixStartTime).TotalSeconds;
        }

        /// <summary>
        /// Returns milliseconds elapsed from 1970.1.1 to utc now
        /// </summary>
        public static double GetUtcTimestampMs()
        {
            return DateTime.UtcNow.Subtract(UnixStartTime).TotalMilliseconds;
        }
    }
}