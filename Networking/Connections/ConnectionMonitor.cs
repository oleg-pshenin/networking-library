using System;
using System.Collections.Generic;
using Networking.Broadcasting;
using Networking.Data;
using Networking.Utils;

namespace Networking.Connections
{
    public class ConnectionMonitor : IMainThreadUpdateable
    {
        /// <summary>
        /// How much time should pass since last received data from connection to remove connection in milliseconds
        /// </summary>
        public static double ConnectionRemovalTimeOut = 15000;

        /// <summary>
        /// How much time should pass since last received data from connection to mark connection as lost in milliseconds
        /// </summary>
        public static double ConnectionLostTimeOut = 5000;

        /// <summary>
        /// How much time should pass since starting connection without receiving to mark connection as lost in milliseconds
        /// </summary>
        public static double ConnectionEstablishTimeOut = 5000;

        /// <summary>
        /// Number of ping calls per second to each connection.
        /// Equals to (1 / Seconds between calls)
        /// </summary>
        public static double PingRate = 0.5;

        private const string PingPayload = "01";
        private const string PongPayload = "01";

        private readonly IConnectionManager _connectionManager;
        private readonly IDataBroadcaster _dataBroadcaster;
        private int _pingId;

        private double _lastPingTimestampMs;
        private readonly List<Connection> _newConnections = new();

        internal ConnectionMonitor(IConnectionManager connectionManager, IDataBroadcaster dataBroadcaster)
        {
            _connectionManager = connectionManager;
            _connectionManager.ConnectionAdded += ConnectionAddedHandler;
            _connectionManager.ConnectionRemoved += ConnectionRemovedHandler;
            _dataBroadcaster = dataBroadcaster;

            _dataBroadcaster.ListenForReceive<Ping>(this, PingHandler);
            _dataBroadcaster.ListenForReceive<Pong>(this, PongHandler);
        }

        private void ConnectionAddedHandler(Connection connection)
        {
            if (!_newConnections.Contains(connection))
                _newConnections.Add(connection);
        }

        private void ConnectionRemovedHandler(Connection connection)
        {
            if (_newConnections.Contains(connection))
                _newConnections.Remove(connection);
        }

        private void PingHandler(Connection connection, Ping ping)
        {
            var pong = new Pong()
            {
                Id = ping.Id,
                Timestamp = ping.Timestamp,
                Payload = PongPayload,
            };

            _dataBroadcaster.AddDataToSend(connection, pong, BroadcastingChannel.Unreliable);
        }

        private void PongHandler(Connection connection, Pong pong)
        {
            var timestampMs = TimeUtils.GetUtcTimestampMs();
            var rttMs = timestampMs - pong.Timestamp;

            (connection as IConnectionInternal).UpdateRTT(rttMs);
        }

        public void MainThreadUpdate()
        {
            var timeStampMs = TimeUtils.GetUtcTimestampMs();

            // The idea is to instantly push ping messages to new connections to open ports
            // As waiting for the next ping frame can take up to several seconds
            // In terms of timing can be considered as previous delayed ping series
            foreach (var connection in _newConnections)
            {
                var ping = new Ping()
                {
                    Id = _pingId,
                    Timestamp = timeStampMs,
                    Payload = PingPayload,
                };

                if (timeStampMs > connection.ConnectionStartTime + connection.MonitoringSilenceDelay)
                    _dataBroadcaster.AddDataToSend(connection, ping, BroadcastingChannel.Unreliable);
            }

            _newConnections.Clear();

            var frameDelay = 1000.0 / PingRate;
            if (timeStampMs < _lastPingTimestampMs + frameDelay)
                return;

            // Stabilizing ping rate to prevent extra delays because of main thread update rate desync with raw sync rate 
            _lastPingTimestampMs = Math.Floor(timeStampMs / frameDelay) * frameDelay;

            _pingId++;

            var connectionsToRemove = new List<Connection>();

            foreach (var connection in _connectionManager.Connections)
            {
                var internalConnection = (IConnectionInternal)connection;

                var receivedAnyData = connection.IncomingTraffic.AnyDataReceived;
                var lastIncomingDataTimestampMs = connection.IncomingTraffic.LastDataReceivedTimestampMs;

                switch (connection.State)
                {
                    case ConnectionState.WaitingForConnecting:
                        if (receivedAnyData)
                        {
                            internalConnection.SetState(ConnectionState.Connected);
                        }
                        else
                        {
                            if (connection.ConnectionStartTime + ConnectionEstablishTimeOut < timeStampMs)
                            {
                                internalConnection.SetState(ConnectionState.Lost);
                            }
                        }

                        break;
                    case ConnectionState.Connected:
                        if (lastIncomingDataTimestampMs + ConnectionLostTimeOut < timeStampMs)
                        {
                            internalConnection.SetState(ConnectionState.Lost);
                        }

                        break;
                    case ConnectionState.Suspended:
                        continue;
                    case ConnectionState.Lost:
                        if (lastIncomingDataTimestampMs + ConnectionLostTimeOut > timeStampMs)
                        {
                            internalConnection.SetState(ConnectionState.Connected);
                        }
                        else if (lastIncomingDataTimestampMs + ConnectionRemovalTimeOut < timeStampMs)
                        {
                            Logger.Log($"Disconnecting from: {connection} because of timeout");
                            connectionsToRemove.Add(connection);
                            continue;
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                var ping = new Ping()
                {
                    Id = _pingId,
                    Timestamp = timeStampMs,
                    Payload = PingPayload,
                };

                if (timeStampMs < connection.ConnectionStartTime + connection.MonitoringSilenceDelay)
                    continue;

                _dataBroadcaster.AddDataToSend(connection, ping, BroadcastingChannel.Unreliable);
            }

            foreach (var connection in connectionsToRemove)
            {
                _connectionManager.RemoveConnection(connection);
            }
        }
    }
}