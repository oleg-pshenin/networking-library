using System;
using System.Net;
using Networking.Utils;

namespace Networking.Connections
{
    public class Connection : IConnectionInternal
    {
        public ConnectionState State { get; private set; }
        public IPEndPoint IPEndPoint { get; }

        /// <summary>
        /// Weighted (~10) average of Round Trip Time in milliseconds
        /// </summary>
        public double RTT { get; private set; }

        // Can be configurable
        private const int RTTAverageSamples = 10;

        /// <summary>
        /// Optional override of how much time should pass after sending reliable packet before it is considered lost and
        /// should be resent in milliseconds
        /// Used for custom slow connections such as Matchmaking server with slow tick-rate
        /// </summary>
        internal double AckTimeoutOverride { get; private set; } = -1;

        // Optionally used for weird routers which sensitive for DoS protection and change port on incoming packet before punch holing in milliseconds
        internal double MonitoringSilenceDelay { get; set; } = 0;

        // Connection start timestamp in milliseconds
        internal double ConnectionStartTime { get; }
        public TrafficInfo IncomingTraffic { get; } = new();
        public TrafficInfo OutcomingTraffic { get; } = new();

        internal Connection(IPEndPoint ipEndPoint)
        {
            IPEndPoint = ipEndPoint;
            ConnectionStartTime = TimeUtils.GetUtcTimestampMs();
        }

        internal void OverrideAckTimeout(double ackTimeout)
        {
            if (ackTimeout <= 0.0)
                Logger.LogError($"Ack timeout can't be smaller than 0");

            AckTimeoutOverride = Math.Max(0, ackTimeout);
        }

        void IConnectionInternal.UpdateRTT(double rttValue)
        {
            if (RTT == 0.0)
            {
                RTT = rttValue;
                return;
            }

            RTT = DynamicAverageCalculator.UpdateAverage(RTT, rttValue, RTTAverageSamples);
        }

        void IConnectionInternal.Suspend()
        {
            // should send packet on suspending?

            if (State != ConnectionState.Connected)
            {
                Logger.LogError($"{IPEndPoint} Only connection with state ConnectionState.Connected expected to be suspended");
            }

            State = ConnectionState.Suspended;
        }

        void IConnectionInternal.Resume()
        {
            if (State != ConnectionState.Suspended)
            {
                Logger.LogError($"{IPEndPoint} Only connection with state ConnectionState.Suspended can be resumed");
                State = ConnectionState.WaitingForConnecting;
            }
        }

        void IConnectionInternal.SetState(ConnectionState connectionState)
        {
            if (State != connectionState)
            {
                Logger.Log($"Connection State of {IPEndPoint} changed from: {State} to {connectionState}");
                State = connectionState;
                // StateChanged?.Invoke(connectionState);
            }
        }

        public override string ToString()
        {
            if (IPEndPoint == null)
            {
                return "Undefined connection";
            }
            else
            {
                return IPEndPoint.ToString();
            }
        }
    }
}