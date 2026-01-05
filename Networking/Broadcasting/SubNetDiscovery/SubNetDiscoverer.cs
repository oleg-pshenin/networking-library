using System;
using System.Net;
using Networking.Utils;

namespace Networking.Broadcasting.SubNetDiscovery
{
    public class SubNetDiscoverer : ISubNetDiscoverer
    {
        public static bool LogSubNetDiscovery = true;
        
        public event Action<FindServerRequest> FindServerRequestReceived;
        public event Action<FindServerResponse> FindServerResponseReceived;
        
        private readonly IUdpMessenger _udpMessenger;

        internal SubNetDiscoverer(IUdpMessenger udpMessenger)
        {
            udpMessenger.UnknownSourceMessageReceived += UnknownSourceMessageReceivedHandler;
            _udpMessenger = udpMessenger;
        }

        private void UnknownSourceMessageReceivedHandler(IPEndPoint ipEndPoint, byte[] data)
        {
            // here we check for correct type of data
            // then either answer or ignore
            var broadcastMessage = ProtobufSerializer.ByteArrayToObject<BroadcastMessage>(data);
            if (broadcastMessage == null)
            {
                // skipping if unknown message type
                return;
            }
            broadcastMessage.SenderIPEndPoint = ipEndPoint;
            switch (broadcastMessage)
            {
                case FindServerRequest findServerRequest:
                    if (LogSubNetDiscovery)
                        Logger.Log("Received FindServerRequest");
                    
                    FindServerRequestReceived?.Invoke(findServerRequest);
                    break;
                case FindServerResponse findServerResponse:
                    if (LogSubNetDiscovery)
                        Logger.Log("Received FindServerResponse");
                    
                    FindServerResponseReceived?.Invoke(findServerResponse);
                    break;
                default:
                    if (LogSubNetDiscovery)
                        Logger.Log($"Received unknown type of BroadcastMessage: {typeof(BroadcastMessage)}");
                    break;
            }
        }

        public void BroadcastToServers(int serverPort)
        {
            Logger.Log($"Broadcast call to servers: {serverPort}");
            _udpMessenger.SendBroadcast(serverPort, ProtobufSerializer.ObjectToByteArray(new FindServerRequest()));
        }

        public void SendServerInfoToClient(IPEndPoint ipEndPoint, FindServerResponse findServerResponse)
        {
            Logger.Log($"Server info sending to: {ipEndPoint} with data: {findServerResponse}");
            _udpMessenger.Send(ipEndPoint, ProtobufSerializer.ObjectToByteArray(findServerResponse));
        }
    }
}