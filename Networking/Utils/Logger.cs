using System;
using System.Diagnostics;

namespace Networking.Utils
{
    public static class Logger
    {
        private static Action<string> _logInfo;
        private static Action<string> _logWarning;
        private static Action<string> _logError;
        private static Func<string> _prefixGetter;

        public static void SetLogDelegate(Action<string> logInfo, Action<string> logWarning, Action<string> logError)
        {
            _logInfo = logInfo;
            _logWarning = logWarning;
            _logError = logError;
        }

        public static void SetPrefix(Func<string> prefixGetter)
        {
            _prefixGetter = prefixGetter;
        }

        private static string GetPrefix()
        {
            return _prefixGetter != null ? _prefixGetter.Invoke() + ": " : string.Empty;
        }

        [Conditional("DEBUG")]
        public static void Log(string log)
        {
            Console.WriteLine($"{GetPrefix()}#I# {log}");
            _logInfo?.Invoke(log);
        }

        [Conditional("DEBUG")]
        public static void LogWarning(string log)
        {
            Console.WriteLine($"{GetPrefix()}#W# {log}");
            _logWarning?.Invoke(log);
        }

        public static void LogError(string log)
        {
            Console.WriteLine($"{GetPrefix()}#E# {log}");
            _logError?.Invoke(log);
        }
    }
}