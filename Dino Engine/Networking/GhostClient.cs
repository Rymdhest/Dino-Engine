using Dino_Engine.ECS.ECS_Architecture;

namespace Dino_Engine.Networking
{
    /// <summary>Handles events as their moment comes, so that a spark goes off when the blow is seen to land, not when the packet arrived.</summary>
    public sealed class NetEventHandlers
    {
        private readonly record struct Pending(uint Tick, Action Invoke);

        private readonly Dictionary<Type, Delegate> _handlers = new();
        private readonly List<Pending> _pending = new();

        public void On<T>(Action<T> handler) where T : struct
        {
            _handlers[typeof(T)] = handler;
        }

        internal void Queue<T>(T value, uint tick) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out Delegate? handler)) return;
            var action = (Action<T>)handler;
            _pending.Add(new Pending(tick, () => action(value)));
        }

        /// <summary>Runs everything due at <paramref name="tick"/>.</summary>
        public void Flush(double tick)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].Tick > tick) continue;
                _pending[i].Invoke();
                _pending.RemoveAt(i--);
            }
        }

        public void Clear() => _pending.Clear();
    }

    /// <summary>What a client knows of one server entity.</summary>
    public sealed class Ghost
    {
        internal readonly record struct Sample(uint Sequence, uint Tick, ulong Mask, int[] Row);

        internal readonly List<Sample> Samples = new(8);
        internal uint LeavesAt;
        internal bool Leaving;
        internal bool Settled;

        public readonly uint Id;

        /// <summary>This ghost's entity on the client, once it has one.</summary>
        public Entity Entity;

        /// <summary>Components the client works out for itself, and which snapshots must leave alone: the hero's own position, say.</summary>
        public ulong Predicted;

        internal Ghost(uint id)
        {
            Id = id;
        }

        /// <summary>The tick of the newest state heard from the server.</summary>
        public uint NewestTick => Samples.Count > 0 ? Samples[^1].Tick : 0;
    }

    /// <summary>How the game gives ghosts a body. The replicated components are supplied; this adds what is needed to draw them.</summary>
    public interface IGhostHost
    {
        /// <summary>A ghost has appeared. Adds to <paramref name="components"/> whatever its entity needs on this machine.</summary>
        void Decorate(Ghost ghost, List<IComponent> components);

        void Created(Ghost ghost);

        void Removing(Ghost ghost);
    }

    /// <summary>
    /// The client's side of replication. It keeps the last while of every ghost's rows. A snapshot's differences are read against
    /// those, and the same rows are what ghosts are drawn from: a little in the past, blended between two real
    /// states, so that what is on screen is always something the server said, with no guessing to go wrong.
    /// </summary>
    public sealed class GhostClient
    {
        private const int MostGhostsPerSnapshot = 1024;
        private const int MostDepartures = 512;
        private const int MostEvents = 256;
        private const int MostStrings = 64;
        private const int SeenEvents = 2048;

        private readonly ECSWorld _ecs;
        private readonly IGhostHost _host;
        private readonly NetEventHandlers _events;
        private readonly NetStrings _strings = new();
        private readonly Dictionary<uint, Ghost> _ghosts = new();
        private readonly List<Ghost> _pendingCreate = new();
        private readonly List<uint> _departed = new();
        private readonly List<IComponent> _components = new();
        private readonly HashSet<uint> _seenEvents = new();
        private readonly Queue<uint> _seenOrder = new();

        // A snapshot is read in full before any of it counts: one that turns out to be damaged must leave no half of itself behind.
        private readonly record struct Update(uint Id, ulong Mask, int[] Row);
        private readonly record struct Departure(uint Id, uint Tick);
        private readonly List<Update> _updates = new();
        private readonly List<Departure> _departures = new();

        public GhostClient(ECSWorld ecs, IGhostHost host, NetEventHandlers events)
        {
            _ecs = ecs;
            _host = host;
            _events = events;
        }

        public IReadOnlyDictionary<uint, Ghost> Ghosts => _ghosts;
        public NetStrings Strings => _strings;

        /// <summary>The newest snapshot received, and which of the 32 before it arrived: what goes back to the server.</summary>
        public uint LatestSequence { get; private set; }
        public uint AckBits { get; private set; }

        /// <summary>Set if a snapshot referred to something not held. Asks the server to send everything again.</summary>
        public bool NeedsReset { get; set; }

        /// <summary>How often that has happened. Anything but 0 is a bug.</summary>
        public int Mismatches { get; private set; }

        public int SnapshotsRead { get; private set; }

        /// <summary>Reads one snapshot. Anything older than one already read is dropped: the newer one says everything it would have.</summary>
        public void ReadSnapshot(BitReader reader, uint tick)
        {
            uint sequence = reader.ReadVarUInt();
            if (sequence <= LatestSequence) return;

            int firstString = (int)reader.ReadVarUInt();
            int stringCount = reader.ReadCount(MostStrings);
            var strings = new (int, string)[stringCount];
            for (int i = 0; i < stringCount; i++) strings[i] = (firstString + i, reader.ReadString(NetStrings.LongestString));

            int departures = reader.ReadCount(MostDepartures);
            _departures.Clear();
            for (int i = 0; i < departures; i++)
            {
                uint id = reader.ReadVarUInt();
                _departures.Add(new Departure(id, reader.ReadUInt()));
            }

            int events = reader.ReadCount(MostEvents);
            for (int i = 0; i < events; i++)
            {
                uint eventSequence = reader.ReadVarUInt();
                uint eventTick = reader.ReadUInt();
                int type = reader.ReadByte();
                if (type >= NetSchema.Events.Length) throw new NetReadException($"An event of unknown type {type}.");

                // Each one has to be read past whether it is new or not: bits run straight on.
                bool isNew = _seenEvents.Add(eventSequence);
                NetSchema.Events[type].Dispatch(reader, eventTick, isNew ? _events : NoOne);
                if (!isNew) continue;

                _seenOrder.Enqueue(eventSequence);
                if (_seenOrder.Count > SeenEvents) _seenEvents.Remove(_seenOrder.Dequeue());
            }

            _updates.Clear();
            for (int i = 0; reader.ReadBool(); i++)
            {
                if (i >= MostGhostsPerSnapshot) throw new NetReadException("Too many ghosts in one snapshot.");
                if (!ReadGhost(reader, sequence))
                {
                    // Bits can't be skipped without knowing what they were measured from, so this one is lost.
                    NeedsReset = true;
                    Mismatches++;
                    return;
                }
            }

            Commit(sequence, tick, strings);
        }

        private static readonly NetEventHandlers NoOne = new();

        private bool ReadGhost(BitReader reader, uint sequence)
        {
            uint id = reader.ReadVarUInt();
            uint age = reader.ReadBits(7);
            bool hasMask = reader.ReadBool();

            _ghosts.TryGetValue(id, out Ghost? ghost);
            int[] baseline = Array.Empty<int>();
            ulong baselineMask = 0;
            if (age != 0)
            {
                uint baselineSequence = sequence - age;
                bool found = false;
                if (ghost != null)
                {
                    for (int i = ghost.Samples.Count - 1; i >= 0 && !found; i--)
                    {
                        if (ghost.Samples[i].Sequence != baselineSequence) continue;
                        baseline = ghost.Samples[i].Row;
                        baselineMask = ghost.Samples[i].Mask;
                        found = true;
                    }
                }
                if (!found) return false;
            }

            ulong mask = hasMask ? reader.ReadVarULong() : baselineMask;
            if (age == 0 && !hasMask) throw new NetReadException("A ghost with nothing to go on.");

            ComponentCodec[] codecs = NetSchema.Components;
            int lanes = 0;
            foreach (ComponentCodec codec in codecs)
            {
                if ((mask & (1ul << codec.Index)) != 0) lanes += codec.Lanes;
            }
            if (codecs.Length < 64 && mask >> codecs.Length != 0) throw new NetReadException("A ghost with components that don't exist.");

            int[] row = new int[lanes];
            int at = 0;
            int baselineAt = 0;
            foreach (ComponentCodec codec in codecs)
            {
                ulong bit = 1ul << codec.Index;
                bool inRow = (mask & bit) != 0;
                bool inBaseline = (baselineMask & bit) != 0;

                if (inRow)
                {
                    bool changed = reader.ReadBool();
                    for (int lane = 0; lane < codec.Lanes; lane++)
                    {
                        int value = inBaseline ? baseline[baselineAt + lane] : 0;
                        if (changed && reader.ReadBool()) value += reader.ReadDelta();
                        row[at + lane] = value;
                    }
                    at += codec.Lanes;
                }
                if (inBaseline) baselineAt += codec.Lanes;
            }

            _updates.Add(new Update(id, mask, row));
            return true;
        }

        private void Commit(uint sequence, uint tick, (int id, string text)[] strings)
        {
            foreach ((int id, string text) in strings) _strings.Define(id, text);

            foreach (Departure departure in _departures)
            {
                if (!_ghosts.TryGetValue(departure.Id, out Ghost? ghost) || ghost.Leaving) continue;
                ghost.Leaving = true;
                ghost.LeavesAt = departure.Tick;
            }

            foreach (Update update in _updates)
            {
                if (!_ghosts.TryGetValue(update.Id, out Ghost? ghost))
                {
                    ghost = new Ghost(update.Id);
                    _ghosts.Add(update.Id, ghost);
                    _pendingCreate.Add(ghost);
                }

                AddSample(ghost, new Ghost.Sample(sequence, tick, update.Mask, update.Row));
                ghost.Leaving = false;
                ghost.Settled = false;
            }

            int shift = (int)Math.Min(sequence - LatestSequence, 33u);
            uint bits = shift >= 32 ? 0u : AckBits << shift;
            if (LatestSequence != 0 && shift <= 32) bits |= 1u << (shift - 1);
            AckBits = bits;
            LatestSequence = sequence;
            SnapshotsRead++;
        }

        private static void AddSample(Ghost ghost, Ghost.Sample sample)
        {
            // A ghost that stood still has no samples for that time, and blending across the gap would have it drift the whole way
            // instead of standing and then moving. Holding its last state until just before the new one is what really happened.
            if (ghost.Samples.Count > 0)
            {
                Ghost.Sample last = ghost.Samples[^1];
                if (sample.Tick > last.Tick + StillAfter && last.Mask == sample.Mask)
                {
                    ghost.Samples.Add(last with { Tick = sample.Tick - HoldUntil, Sequence = 0 });
                }
            }
            ghost.Samples.Add(sample);

            // Everything within the window has to stay, as the server may measure from any of it. Two are always kept to blend between.
            uint oldest = sample.Sequence > GhostServer.BaselineWindow ? sample.Sequence - GhostServer.BaselineWindow : 0;
            int drop = 0;
            while (drop < ghost.Samples.Count - 2 && ghost.Samples[drop].Sequence < oldest) drop++;
            if (drop > 0) ghost.Samples.RemoveRange(0, drop);
        }

        private const uint StillAfter = 12;     // Ticks without news: longer than losing a few packets explains.
        private const uint HoldUntil = 4;

        /// <summary>Gives new ghosts their entities. Call where it is safe to create entities: they exist from the next ECS update.</summary>
        public void CreatePending()
        {
            foreach (Ghost ghost in _pendingCreate)
            {
                if (ghost.Samples.Count == 0) continue;

                Ghost.Sample first = ghost.Samples[^1];
                _components.Clear();
                int at = 0;
                foreach (ComponentCodec codec in NetSchema.Components)
                {
                    if ((first.Mask & (1ul << codec.Index)) == 0) continue;
                    _components.Add(codec.Create(first.Row, at, _strings));
                    at += codec.Lanes;
                }

                _host.Decorate(ghost, _components);
                ghost.Entity = _ecs.CreateEntity(_components.ToArray());
                _host.Created(ghost);
            }
            _pendingCreate.Clear();
        }

        /// <summary>Removes every ghost, for when the world they belong to has gone. Their entities are left to the caller.</summary>
        public void Clear()
        {
            _ghosts.Clear();
            _pendingCreate.Clear();
            _events.Clear();
            _seenEvents.Clear();
            _seenOrder.Clear();
            LatestSequence = 0;
            AckBits = 0;
        }

        /// <summary>Sets every ghost's components to how things stood at <paramref name="renderTick"/>.</summary>
        public void Apply(double renderTick)
        {
            _departed.Clear();
            foreach (Ghost ghost in _ghosts.Values)
            {
                if (ghost.Leaving && renderTick >= ghost.LeavesAt)
                {
                    _departed.Add(ghost.Id);
                    continue;
                }

                if (!ghost.Entity.IsValid() || ghost.Samples.Count == 0 || ghost.Settled) continue;
                if (!_ecs.TryGetEntityLocation(ghost.Entity, out _, out _)) continue;
                ApplyGhost(ghost, renderTick);
            }

            foreach (uint id in _departed)
            {
                Ghost ghost = _ghosts[id];
                if (ghost.Entity.IsValid())
                {
                    _host.Removing(ghost);
                    _ecs.DestroyEntity(ghost.Entity);
                }
                _pendingCreate.Remove(ghost);
                _ghosts.Remove(id);
            }
        }

        private void ApplyGhost(Ghost ghost, double renderTick)
        {
            List<Ghost.Sample> samples = ghost.Samples;

            // The last state at or before the moment, and the first one after it.
            int from = 0;
            for (int i = samples.Count - 1; i >= 0; i--)
            {
                if (samples[i].Tick > renderTick) continue;
                from = i;
                break;
            }
            int to = from + 1 < samples.Count ? from + 1 : from;

            Ghost.Sample a = samples[from];
            Ghost.Sample b = samples[to];
            float t = b.Tick > a.Tick ? (float)Math.Clamp((renderTick - a.Tick) / (b.Tick - a.Tick), 0.0, 1.0) : 0f;

            EntityView view = _ecs.GetEntityView(ghost.Entity);
            int aAt = 0;
            int bAt = 0;
            foreach (ComponentCodec codec in NetSchema.Components)
            {
                ulong bit = 1ul << codec.Index;
                bool inA = (a.Mask & bit) != 0;
                bool inB = (b.Mask & bit) != 0;

                if (inA && (ghost.Predicted & bit) == 0)
                {
                    if (!codec.Has(view)) _ecs.AddComponentToEntity(ghost.Entity, codec.Create(a.Row, aAt, _strings));
                    else if (inB) codec.Blend(view, a.Row, aAt, b.Row, bAt, t, _strings);
                    else codec.Apply(view, a.Row, aAt, _strings);
                }
                else if (!inA && (ghost.Predicted & bit) == 0 && codec.Has(view) && WasSent(samples, bit))
                {
                    _ecs.RemoveComponentFromEntity(ghost.Entity, codec.Type);
                }

                if (inA) aAt += codec.Lanes;
                if (inB) bAt += codec.Lanes;
            }

            // Nothing new can change it once the newest state has been reached, until another comes.
            ghost.Settled = from == samples.Count - 1 && a.Tick <= renderTick;
        }

        private static bool WasSent(List<Ghost.Sample> samples, ulong bit)
        {
            foreach (Ghost.Sample sample in samples)
            {
                if ((sample.Mask & bit) != 0) return true;
            }
            return false;
        }

        /// <summary>The newest word on one component of a ghost, without waiting for it to come round: a hero's own health, say.</summary>
        public T? Newest<T>(Ghost ghost) where T : struct, IComponent
        {
            if (ghost.Samples.Count == 0) return null;
            Ghost.Sample sample = ghost.Samples[^1];
            int index = NetSchema.IndexOfComponent(typeof(T));
            if ((sample.Mask & (1ul << index)) == 0) return null;

            int at = 0;
            foreach (ComponentCodec codec in NetSchema.Components)
            {
                if (codec.Index == index) return (T)codec.Create(sample.Row, at, _strings);
                if ((sample.Mask & (1ul << codec.Index)) != 0) at += codec.Lanes;
            }
            return null;
        }
    }
}
