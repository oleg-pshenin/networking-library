using System;

namespace MatchMakingServer
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var matchmakingServer = new MatchmakingServerStarter();
            matchmakingServer.Start();
            Console.ReadLine();
        }
    }
}