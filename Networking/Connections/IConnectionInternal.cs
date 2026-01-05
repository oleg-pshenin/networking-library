namespace Networking.Connections
{
    public interface IConnectionInternal
    {
        void UpdateRTT(double rttValue);
        void Suspend();
        void Resume();
        void SetState(ConnectionState connectionState);
    }
}