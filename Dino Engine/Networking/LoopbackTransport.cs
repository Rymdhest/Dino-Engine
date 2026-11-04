using System.Collections.Concurrent;

namespace Dino_Engine.Networking
{
    /// <summary>
    /// Two ends of a connection inside one process. It is how someone playing alone reaches the server running beside them, so
    /// the game has only the one way of doing everything. Either end can be polled from its own thread.
    /// </summary>
    public sealed class LoopbackTransport : INetTransport
    {
        /// <summary>As much as a packet on a real network, so that a game alone behaves as it does with friends.</summary>
        public const int UnreliableLimit = 1200;

        private readonly record struct Item(byte[]? Data, Delivery Delivery, string? Closed);

        private readonly ConcurrentQueue<Item> _incoming = new();
        private readonly Connection _connection;
        private LoopbackTransport _other = null!;
        private bool _announced;
        private bool _closed;
        private bool _reported;

        private LoopbackTransport()
        {
            _connection = new Connection(this);
        }

        /// <summary>Makes a connected pair: one end for the server, one for the client.</summary>
        public static (LoopbackTransport server, LoopbackTransport client) CreatePair()
        {
            var server = new LoopbackTransport();
            var client = new LoopbackTransport();
            server._other = client;
            client._other = server;
            return (server, client);
        }

        public void Poll(INetListener listener)
        {
            if (!_announced)
            {
                _announced = true;
                listener.OnConnected(_connection);
            }

            while (_incoming.TryDequeue(out Item item))
            {
                if (item.Closed != null)
                {
                    _closed = true;
                    if (_reported) continue;
                    _reported = true;
                    listener.OnDisconnected(_connection, item.Closed);
                    continue;
                }
                if (!_closed) listener.OnMessage(_connection, item.Data!, item.Delivery);
            }
        }

        public void Dispose()
        {
            if (_closed) return;
            _closed = true;
            _other._incoming.Enqueue(new Item(null, Delivery.Reliable, "The other end went away."));
        }

        private sealed class Connection : INetConnection
        {
            private readonly LoopbackTransport _owner;

            public Connection(LoopbackTransport owner)
            {
                _owner = owner;
            }

            public int MaxUnreliableSize => UnreliableLimit;
            public string Address => "this machine";

            public void Send(byte[] data, Delivery delivery)
            {
                if (_owner._closed) return;
                _owner._other._incoming.Enqueue(new Item((byte[])data.Clone(), delivery, null));
            }

            public void Close(string reason)
            {
                if (_owner._closed) return;
                _owner._closed = true;
                _owner._other._incoming.Enqueue(new Item(null, Delivery.Reliable, reason));
                _owner._incoming.Enqueue(new Item(null, Delivery.Reliable, reason));
            }
        }
    }
}
