using System;
using System.Globalization;
using System.Net;

namespace Networking.Utils
{
    public static class StringUtils
    {
        public static string ToDataSize(this int byteCount)
        {
            return ToDataSize((double)byteCount);
        }

        public static string ToDataSize(this long byteCount)
        {
            return ToDataSize((double)byteCount);
        }

        public static string ToDataSize(this float byteCount)
        {
            return ToDataSize((double)byteCount);
        }

        public static string ToDataSize(this double byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB

            if (byteCount <= 1)
            {
                return $"{byteCount:0.0} {suf[0]}";
            }
            else if (byteCount > 1_000_000_000_000_000_000)
            {
                Logger.LogError($"Too big data size value to parse: {byteCount}");
                return $"{byteCount:0.0} {suf[0]}";
            }

            var place = Convert.ToInt32(Math.Floor(Math.Log(byteCount, 1024)));
            var num = Math.Round(byteCount / Math.Pow(1024, place), 2);
            return $"{Math.Sign(byteCount) * num:0.0} {suf[place]}";
        }

        public static IPEndPoint ParseToIpEndPoint(this string ipEndPoint)
        {
            var ep = ipEndPoint.Split(':');
            if (ep.Length < 2)
                throw new FormatException("Invalid endpoint format");

            IPAddress ip;

            // ipv6 handling
            if (ep.Length > 2)
            {
                if (!IPAddress.TryParse(string.Join(":", ep, 0, ep.Length - 1), out ip))
                {
                    throw new FormatException("Invalid ip-address");
                }
            }
            // ipv4 handling
            else
            {
                if (!IPAddress.TryParse(ep[0], out ip))
                {
                    throw new FormatException("Invalid ip-address");
                }
            }

            if (!int.TryParse(ep[ep.Length - 1], NumberStyles.None, NumberFormatInfo.CurrentInfo, out var port))
            {
                throw new FormatException("Invalid port");
            }

            return new IPEndPoint(ip, port);
        }
    }
}