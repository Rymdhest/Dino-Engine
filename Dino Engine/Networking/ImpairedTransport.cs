namespace Dino_Engine.Networking
{
    /// <summary>What a bad network does to a message. All times in milliseconds, one way.</summary>
    public sealed class NetworkConditions
    {
        public double Latency;
        public double Jitter;           // Added at random on top of the latency, up to this much.
        public double LossChance;       // Of an unreliable message, 0..1.
        public double DuplicateChance;
        public double SpikeChance;      // Per message, of everything stalling for SpikeLength: the wifi moment.
        public double SpikeLength = 400;

        public static NetworkConditions Parse(string text)
        {
            // latency,jitter,loss%[,duplicate%]
            string[] parts = text.Split(',');
            var conditions = new NetworkConditions();
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            if (parts.Length > 0) conditions.Latency = double.Parse(parts[0], culture);
            if (parts.Length > 1) conditions.Jitter = double.Parse(parts[1], culture);
            if (parts.Length > 2) conditions.LossChance = double.Parse(parts[2], culture) / 100.0;
            if (parts.Length > 3) conditions.DuplicateChance = double.Parse(parts[3], culture) / 100.0;
            return conditions;
        }
    }

    /// <summary>
    /// Puts a bad network between a transport and the game: delay, jitter, loss, repeats and reordering, in both directions. It
    /// is what tests run on, and it can be switched on in the real game to feel what a player on a poor line feels. Reliable
    /// messages are delayed but not lost, and stay in order, as a real reliable channel would keep them.
    /// </summary>
    public sealed class ImpairedTransport : INetTransport, INetListener
    {
        private readonly record struct Pending(double Due, long Order, INetConnection Connection, byte[] Data, Delivery Delivery, bool Outgoing);

        private readonly INetTransport _inner;
        private readonly NetworkConditions _conditions;
        private readonly Func<double> _clock;
        private readonly Random _random;
        private readonly PriorityQueue<Pending, (double, long)> _queue = new();
        private readonly Dictionary<INetConnection, Wrapped> _wrapped = new();
        private readonly List<Action<INetListener>> _announcements = new();
        private double _reliableDueIn;
        private double _reliableDueOut;
        private double _stalledUntil;
        private long _order;

        /// <param name="clock">Milliseconds. A test passes its own, so that a minute of network can pass in a moment.</param>
        public ImpairedTransport(INetTransport inner, NetworkConditions conditions, Func<double> clock, int seed = 1)
        {
            _inner = inner;
            _conditions = conditions;
            _clock = clock;
            _random = new Random(seed);
        }

        /// <summary>
        /// Keeps the line running for a program that has stopped reading it. A message's trip is timed from when it gets here, so
        /// anything sent while nobody polls would otherwise set off when they came back, and a stalled client would never see
        /// what a real one does: everything sent meanwhile, there at once.
        /// </summary>
        public void Carry() => _inner.Poll(this);

        public void Poll(INetListener listener)
        {
            Carry();

            foreach (Action<INetListener> announce in _announcements) announce(listener);
            _announcements.Clear();

            double now = _clock();
            while (_queue.TryPeek(out Pending pending, out _) && pending.Due <= now)
            {
                _queue.Dequeue();
                if (pending.Outgoing) pending.Connection.Send(pending.Data, pending.Delivery);
                else listener.OnMessage(Wrap(pending.Connection), pending.Data, pending.Delivery);
            }
        }

        public void Dispose() => _inner.Dispose();

        void INetListener.OnConnected(INetConnection connection)
        {
            Wrapped wrapped = Wrap(connection);
            _announcements.Add(listener => listener.OnConnected(wrapped));
        }

        void INetListener.OnMessage(INetConnection connection, byte[] data, Delivery delivery) => Delay(connection, data, delivery, outgoing: false);

        void INetListener.OnDisconnected(INetConnection connection, string reason)
        {
            Wrapped wrapped = Wrap(connection);
            _announcements.Add(listener => listener.OnDisconnected(wrapped, reason));
        }

        private Wrapped Wrap(INetConnection connection)
        {
            if (!_wrapped.TryGetValue(connection, out Wrapped? wrapped))
            {
                wrapped = new Wrapped(this, connection);
                _wrapped.Add(connection, wrapped);
            }
            return wrapped;
        }

        private void Delay(INetConnection connection, byte[] data, Delivery delivery, bool outgoing)
        {
            double now = _clock();
            if (_random.NextDouble() < _conditions.SpikeChance) _stalledUntil = Math.Max(_stalledUntil, now + _conditions.SpikeLength);

            if (delivery == Delivery.Unreliable)
            {
                if (_random.NextDouble() < _conditions.LossChance) return;
                Enqueue(connection, data, delivery, outgoing, Arrival(now));
                if (_random.NextDouble() < _conditions.DuplicateChance) Enqueue(connection, data, delivery, outgoing, Arrival(now));
                return;
            }

            // A reliable channel can't let one message pass another.
            ref double due = ref outgoing ? ref _reliableDueOut : ref _reliableDueIn;
            due = Math.Max(due, Arrival(now));
            Enqueue(connection, data, delivery, outgoing, due);
        }

        private double Arrival(double now)
        {
            return Math.Max(now, _stalledUntil) + _conditions.Latency * 0.5 + _random.NextDouble() * _conditions.Jitter * 0.5;
        }

        private void Enqueue(INetConnection connection, byte[] data, Delivery delivery, bool outgoing, double due)
        {
            long order = _order++;
            _queue.Enqueue(new Pending(due, order, connection, data, delivery, outgoing), (due, order));
        }

        private sealed class Wrapped : INetConnection
        {
            private readonly ImpairedTransport _owner;
            private readonly INetConnection _inner;

            public Wrapped(ImpairedTransport owner, INetConnection inner)
            {
                _owner = owner;
                _inner = inner;
            }

            public int MaxUnreliableSize => _inner.MaxUnreliableSize;
            public string Address => _inner.Address;

            public void Send(byte[] data, Delivery delivery) => _owner.Delay(_inner, data, delivery, outgoing: true);

            public void Close(string reason) => _inner.Close(reason);
        }
    }
}
