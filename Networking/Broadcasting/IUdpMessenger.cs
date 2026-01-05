using System;
using System.Net;

namespace Networking.Broadcasting
{
    public interface IUdpMessenger
    {
        event Action<IPEndPoint, byte[]> MessageReceived;
        event Action<IPEndPoint, byte[]> UnknownSourceMessageReceived;
        int ListeningPort { get; }
        void Start(int listeningPort);
        void Stop();
        void Send(IPEndPoint ipEndPoint, byte[] data);
        void StartReceivingFromAny();
        void StartReceivingFrom(IPEndPoint ipEndPoint);
        void StopReceivingFrom(IPEndPoint ipEndPoint);
        void SendBroadcast(int port, byte[] data);
    }
}