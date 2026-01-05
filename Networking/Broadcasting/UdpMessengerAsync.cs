using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Networking.Utils;

namespace Networking.Broadcasting
{
    public class UdpMessengerAsync : IUdpMessenger, IMainThreadUpdateable
    {
        internal static bool LogRawTraffic = true;
        internal static bool LogInteractions = true;

        private static readonly IPEndPoint AnyIpEndPoint = new(IPAddress.Any, 0);

        public event Action<IPEndPoint, byte[]> MessageReceived;
        public event Action<IPEndPoint, byte[]> UnknownSourceMessageReceived;
        public int ListeningPort { get; private set; }

        private readonly List<IPEndPoint> _knownSources = new();
        private readonly ConcurrentQueue<(IPEndPoint ipEndPoint, byte[] message)> _receivedMessagesQueue = new();
        private readonly ConcurrentQueue<(IPEndPoint ipEndPoint, byte[] message)> _receivedUnknownSourceMessagesQueue = new();

        private UdpClient _udpClient;
        private CancellationTokenSource _cancelTokenSource;

        internal UdpMessengerAsync(int listeningPort)
        {
            if (LogInteractions)
                Logger.Log($"UdpMessengerAsync created");

            Start(listeningPort);
        }

        public void Start(int listeningPort)
        {
            Stop();

            if (LogInteractions)
                Logger.Log($"Start called with listening port: {listeningPort}");

            if (listeningPort == 0)
            {
            }
            else if (listeningPort < 0)
            {
                Logger.LogError($"Listening port can't be smaller than 0, set it to 0 if you don't need specified port");
                listeningPort = 0;
            }
            else if (listeningPort < 1024)
            {
                Logger.LogError($"Listening port smaller than 1024 is occupied by IANA, use bigger one");
                listeningPort = 0;
            }
            else if (listeningPort < 49152)
            {
                Logger.LogWarning($"Safe listening port should be withing range of 49152 – 65535 otherwise it could be occupied by registered service");
            }
            else if (listeningPort > 65535)
            {
                Logger.LogWarning($"Listening port can't be bigger than 65535");
                listeningPort = 0;
            }

            _udpClient = new UdpClient(listeningPort);
            _udpClient.EnableBroadcast = true;

            FixUdpClientICMP(_udpClient);

            ListeningPort = ((IPEndPoint)_udpClient.Client.LocalEndPoint).Port;

            if (listeningPort != 0 && listeningPort != ListeningPort)
                Logger.LogError($"Couldn't set requested listening port: {listeningPort}, it will be: {ListeningPort}");

            Logger.Log($"Your listening port is: {ListeningPort}");

            _cancelTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancelTokenSource.Token;

            Task.Run(async () =>
            {
                var udpClient = _udpClient;
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var receivedResults = await udpClient.ReceiveAsync();

                        if (_knownSources.Contains(receivedResults.RemoteEndPoint))
                        {
                            // if (LogRawTraffic)
                            //     Logger.Log($"UDP of size: {receivedResults.Buffer.Length} from: {receivedResults.RemoteEndPoint}");

                            _receivedMessagesQueue.Enqueue((receivedResults.RemoteEndPoint, receivedResults.Buffer));
                        }
                        else
                        {
                            // if (LogRawTraffic)
                            //     Logger.Log($"UDP of size: {receivedResults.Buffer.Length} from unknown source: {receivedResults.RemoteEndPoint}. Data will be discarded");
                            
                            // we should drop broadcasting messages
                            if (_knownSources.Contains(AnyIpEndPoint))
                                _receivedMessagesQueue.Enqueue((receivedResults.RemoteEndPoint, receivedResults.Buffer));
                            
                            _receivedUnknownSourceMessagesQueue.Enqueue((receivedResults.RemoteEndPoint, receivedResults.Buffer));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"{ex.GetType()}: {ex.Message}");
                    }
                }

                udpClient.Close();
            }, cancellationToken);
        }

        private const uint IOC_IN = 0x80000000;
        private const uint IOC_VENDOR = 0x18000000;
        private const uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;

        private static void FixUdpClientICMP(UdpClient udpClient)
        {
            // Fix for ICMP messages: https://stackoverflow.com/questions/7201862/an-existing-connection-was-forcibly-closed-by-the-remote-host
            unchecked
            {
                udpClient.Client.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);
            }
        }

        /// <summary>
        /// Should be called on closing the app otherwise separate thread will throw things
        /// </summary>
        public void Stop()
        {
            if (_cancelTokenSource != null)
            {
                if (LogInteractions)
                    Logger.Log($"Stop called, listened sources: {_knownSources.Count}");

                // udp client will be closed inside
                _cancelTokenSource.Cancel();
                _cancelTokenSource = null;
            }
        }

        public void MainThreadUpdate()
        {
            while (!_receivedMessagesQueue.IsEmpty)
            {
                if (_receivedMessagesQueue.TryDequeue(out var receivedMessage))
                {
                    MessageReceived?.Invoke(receivedMessage.ipEndPoint, receivedMessage.message);
                }
                else
                {
                    Logger.LogError("Couldn't dequeue from received messages queue");
                }
            }
            
            while (!_receivedUnknownSourceMessagesQueue.IsEmpty)
            {
                if (_receivedUnknownSourceMessagesQueue.TryDequeue(out var receivedMessage))
                {
                    UnknownSourceMessageReceived?.Invoke(receivedMessage.ipEndPoint, receivedMessage.message);
                }
                else
                {
                    Logger.LogError("Couldn't dequeue from unknown source received messages queue");
                }
            }
        }

        public void Send(IPEndPoint ipEndPoint, byte[] data)
        {
            if (LogRawTraffic)
                Logger.Log($"UDP of size: {data.Length} to: {ipEndPoint}");

            _udpClient.Send(data, data.Length, ipEndPoint);
        }
        
        public void SendBroadcast(int port, byte[] data)
        {
            if (LogRawTraffic)
                Logger.Log($"UDP broadcast of size: {data.Length}");

            _udpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, port));
        }
        
        public void StartReceivingFromAny()
        {
            if (LogInteractions)
                Logger.Log($"StartReceivingFromAny called");

            StartReceivingFrom(AnyIpEndPoint);
        }

        public void StartReceivingFrom(IPEndPoint ipEndPoint)
        {
            if (LogInteractions)
                Logger.Log($"StartReceivingFrom called: {ipEndPoint}");

            _knownSources.Add(ipEndPoint);
        }

        public void StopReceivingFrom(IPEndPoint ipEndPoint)
        {
            if (LogInteractions)
                Logger.Log($"StopReceivingFrom called: {ipEndPoint}");

            _knownSources.Remove(ipEndPoint);
        }
    }
}