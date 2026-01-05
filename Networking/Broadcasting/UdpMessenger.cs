using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Networking.Utils;

namespace Networking.Broadcasting
{
    /// <summary>
    /// Thread based udp listener
    /// </summary>
    [Obsolete]
    public class UdpThreadMessenger : IUdpMessenger, IMainThreadUpdateable
    {
        private static readonly IPEndPoint AnyIpEndPoint = new(IPAddress.Any, 0);
        public event Action<IPEndPoint, byte[]> MessageReceived;
        public event Action<IPEndPoint, byte[]> UnknownSourceMessageReceived;
        public int ListeningPort { get; }

        public void Start(int listeningPort)
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        private readonly UdpClient _udpClient;
        private readonly Dictionary<string, Thread> _receivingThreads = new();
        private readonly ConcurrentQueue<(IPEndPoint ipEndPoint, byte[] message)> _receivedMessagesQueue = new();

        internal UdpThreadMessenger(int listeningPort)
        {
            // Later should add filtering for default ports to prevent ports overlap
            if (listeningPort < 0)
            {
                Logger.LogError($"Listening port can't be smaller than 0, set it to 0 if you don't need specified port");
                listeningPort = 0;
            }

            _udpClient = new UdpClient(listeningPort);
            ListeningPort = ((IPEndPoint)_udpClient.Client.LocalEndPoint).Port;

            if (listeningPort != 0 && listeningPort != ListeningPort)
                Logger.LogError($"Couldn't set requested listening port: {listeningPort}, it will be: {ListeningPort}");

            Logger.Log($"Your listening port is: {ListeningPort}");
        }

        public void MainThreadUpdate()
        {
            while (!_receivedMessagesQueue.IsEmpty)
            {
                if (_receivedMessagesQueue.TryDequeue(out var receivedMessage))
                {
                    // Logger.Log($"Received from {receivedMessage.ipEndPoint} {receivedMessage.message.Length} bytes");
                    MessageReceived?.Invoke(receivedMessage.ipEndPoint, receivedMessage.message);
                }
                else
                {
                    Logger.LogError("Couldn't dequeue from packets queue");
                }
            }
        }

        public void Send(IPEndPoint ipEndPoint, byte[] data)
        {
            _udpClient.Send(data, data.Length, ipEndPoint);
            // Logger.Log($"Sent to {ipEndPoint} {data.Length} bytes");
        }

        public void StartReceivingFromAny()
        {
            StartReceivingFrom(AnyIpEndPoint);
        }

        public void StartReceivingFrom(IPEndPoint ipEndPoint)
        {
            void ReceivingLoopDowncast(object endPoint)
            {
                ReceivingLoop(endPoint as IPEndPoint);
            }

            var address = ipEndPoint.ToString();
            if (_receivingThreads.ContainsKey(address))
            {
                Logger.LogError($"Attempt to start second thread on the same address: {address}");
                return;
            }

            var receivingThread = new Thread(ReceivingLoopDowncast);
            _receivingThreads.Add(ipEndPoint.ToString(), receivingThread);

            receivingThread.Start(ipEndPoint);
        }

        public void StopReceivingFrom(IPEndPoint ipEndPoint)
        {
            var address = ipEndPoint.ToString();
            if (!_receivingThreads.ContainsKey(address))
            {
                Logger.LogWarning($"Can't stop receiving from {address} as it is not started or already aborted");
                return;
            }

            var thread = _receivingThreads[address];
            if (thread.IsAlive)
            {
                thread.Abort();
            }

            _receivingThreads.Remove(address);
        }

        public void SendBroadcast(int port, byte[] data)
        {
            throw new NotImplementedException();
        }

        public void StopReceivingAll()
        {
            foreach (var openedThread in _receivingThreads.Values)
                openedThread.Abort();

            _receivingThreads.Clear();
        }

        private void ReceivingLoop(IPEndPoint ipEndPoint)
        {
            try
            {
                while (true)
                {
                    var receivedData = _udpClient.Receive(ref ipEndPoint);
                    _receivedMessagesQueue.Enqueue((ipEndPoint, receivedData));
                }
            }
            catch (SocketException ex)
            {
                Logger.LogError($"Listening from {ipEndPoint} SocketException: {ex.Message}");
            }
            catch (ThreadAbortException ex)
            {
                Logger.LogError($"Listening from {ipEndPoint} ThreadAbortException: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"{ex.GetType()}: {ex.Message}");
            }
            finally
            {
                StopReceivingFrom(ipEndPoint);
            }
        }
    }
}