namespace Networking.Broadcasting
{
    public enum BroadcastingChannel
    {
        Unreliable,
        UnreliableSequenced,
        Reliable,
        ReliableSequenced,
        ReliableOrdered,
        ReliableFragmented,
    }
}