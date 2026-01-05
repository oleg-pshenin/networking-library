namespace Networking.Connections
{
    /// <summary>
    /// WaitingForConnecting - no data received from connection
    /// Connected - received any data within last LostConnectionTimeOut
    /// Suspended - connection is set on hold, no outcoming data will be sent, incoming data will be ignored
    /// Lost - no data received within last LostConnectionTimeOut
    /// </summary>
    public enum ConnectionState
    {
        WaitingForConnecting,
        Connected,
        Suspended,
        Lost,
    }
}