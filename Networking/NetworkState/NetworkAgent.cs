using System.Collections.Generic;
using System.Net;
using Networking.Broadcasting;
using Networking.Broadcasting.SubNetDiscovery;
using Networking.Connections;
using Networking.NetworkState.Configs;
using Networking.RPCs.Core;
using Networking.Utils;

namespace Networking.NetworkState
{
    public abstract class NetworkAgent : IMainThreadUpdateable
    {
        public IPEndPoint LocalIPEndPoint => $"127.0.0.1:{ListeningPort}".ParseToIpEndPoint();
        public int ListeningPort { get; }
        public IConnectionManager ConnectionManager { get; }
        public IDataBroadcaster DataBroadcaster { get; }
        public IRPCManager RPCManager { get; }
        public ISubNetDiscoverer SubNetDiscoverer { get; }


        private readonly IUdpMessenger _udpMessenger;
        private readonly List<IMainThreadUpdateable> _mainThreadServices = new();

        protected NetworkAgent(NetworkAgentConfig config)
        {
            _udpMessenger = new UdpMessengerAsync(config.ListeningPort);
            ListeningPort = _udpMessenger.ListeningPort;

            if (config.ListenFromAnySource)
                _udpMessenger.StartReceivingFromAny();

            ConnectionManager = new ConnectionManager(_udpMessenger);
            DataBroadcaster = new DataBroadcaster(ConnectionManager, _udpMessenger);
            RPCManager = new RPCManager(ConnectionManager, DataBroadcaster);
            SubNetDiscoverer = new SubNetDiscoverer(_udpMessenger);
            
            var connectionMonitor = new ConnectionMonitor(ConnectionManager, DataBroadcaster);

            _mainThreadServices.Add(_udpMessenger as IMainThreadUpdateable);
            _mainThreadServices.Add(connectionMonitor);
            _mainThreadServices.Add(DataBroadcaster as IMainThreadUpdateable);
            _mainThreadServices.Add(RPCManager as IMainThreadUpdateable);
        }

        public virtual void MainThreadUpdate()
        {
            foreach (var mainThreadUpdateable in _mainThreadServices)
            {
                mainThreadUpdateable.MainThreadUpdate();
            }
        }

        public void ShutDown()
        {
            _udpMessenger.Stop();
            ConnectionManager.RemoveAll();
            _mainThreadServices.Clear();
        }
    }
}