namespace Networking.RPCs.Core
{
    public enum RPCState
    {
        None,
        Initialized,
        WaitingForResponse,
        Responded,
        Failed
    }
}