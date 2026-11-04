namespace Dino_Engine.Networking
{
    /// <summary>How a message travels.</summary>
    public enum Delivery
    {
        /// <summary>Fire and forget: it may be lost, repeated or overtaken. Must fit in <see cref="INetConnection.MaxUnreliableSize"/>.</summary>
        Unreliable,
        /// <summary>Always arrives, once, in the order sent among other reliable messages, and may be of any reasonable size.</summary>
        Reliable,
    }

    /// <summary>One link to another machine, or to the other half of this one.</summary>
    public interface INetConnection
    {
        /// <summary>Largest message that can go unreliably. It can grow while connected, as the path is measured.</summary>
        int MaxUnreliableSize { get; }

        /// <summary>Where the other end is, for showing to a person.</summary>
        string Address { get; }

        void Send(byte[] data, Delivery delivery);

        /// <summary>Ends the connection, telling the other end why.</summary>
        void Close(string reason);
    }

    /// <summary>What a transport reports, always from inside <see cref="INetTransport.Poll"/> and so on the thread that polls it.</summary>
    public interface INetListener
    {
        void OnConnected(INetConnection connection);
        void OnMessage(INetConnection connection, byte[] data, Delivery delivery);
        void OnDisconnected(INetConnection connection, string reason);
    }

    /// <summary>
    /// Moves messages. The game knows nothing of how: a transport can be two queues in one process, a UDP socket or Steam's relay
    /// network, and is all that changes between them. It is shaped after what they have in common, connections that carry whole
    /// messages reliably or not, so that any of them fits.
    /// </summary>
    public interface INetTransport : IDisposable
    {
        void Poll(INetListener listener);
    }
}
