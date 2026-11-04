using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.Networking
{
    /// <summary>Makes an entity exist on clients. Its [Replicated] components go with it.</summary>
    public struct NetIdentityComponent : IComponent
    {
        public uint Id;
        public float Importance;    // 1 for a bystander. Near the budget, what matters more goes first.
        public bool Everywhere;     // Sent however far from the viewer, as another hero is.

        public NetIdentityComponent(uint id, float importance = 1f, bool everywhere = false)
        {
            Id = id;
            Importance = importance;
            Everywhere = everywhere;
        }
    }

    /// <summary>An event ready to send, encoded once however many hear it.</summary>
    public sealed class NetEventPacket
    {
        internal int Type;
        internal uint Tick;
        internal BitWriter Bits = null!;
    }

    /// <summary>
    /// One client's picture of the world, as far as the server knows it: which ghosts it has, and which state of each it has
    /// confirmed receiving. Everything sent is a difference from something confirmed, so a lost packet never leaves a
    /// client unable to read the next one, and what is lost is simply carried by the next difference. Nothing depends on any one packet arriving, and
    /// the same goes for a ghost's arrival and departure, strings and events. That is why this needs no reliable channel, and
    /// why it can't drift: whatever happens to the network, a client ends up with what the server has.
    /// </summary>
    public sealed class GhostView
    {
        internal const int SentRing = 256;
        private const uint LossLag = 30;
        private const int LossSample = 30;

        internal sealed class ViewGhost
        {
            public int[]? Baseline;
            public ulong BaselineMask;
            public uint BaselineSequence;
            public int[]? LastSent;
            public uint LastSentSequence;
            public uint Joined;     // Nothing confirmed from before this says anything about what the client holds now.
            public float Priority;
        }

        internal readonly record struct SentGhost(uint Id, int[] Row, ulong Mask);

        internal sealed class SentPacket
        {
            public uint Sequence;
            public bool Acked;
            public readonly List<SentGhost> Ghosts = new();
            public readonly List<uint> Despawns = new();
            public readonly List<uint> Events = new();
            public int StringsEnd;
        }

        internal readonly record struct QueuedEvent(uint Sequence, NetEventPacket Packet, uint Expires);

        internal readonly Dictionary<uint, ViewGhost> Ghosts = new();
        internal readonly Dictionary<uint, uint> Departing = new();     // A ghost's id, and the tick it left.
        internal readonly List<QueuedEvent> Events = new();
        internal readonly SentPacket[] Sent = new SentPacket[SentRing];
        internal uint Sequence;
        internal int ConfirmedStrings = 1;  // 0 is the empty string, which everyone has.
        internal uint NextEvent = 1;
        private int _sentRecently;
        private int _ackedRecently;

        /// <summary>Where this client is looking from. Things beyond <see cref="Radius"/> of it aren't sent.</summary>
        public Vector2 Focus;
        public float Radius = 140f;

        /// <summary>Snapshots to wait for an acknowledgement before sending the same state again.</summary>
        public int ResendGap = 6;

        public int GhostCount => Ghosts.Count;

        /// <summary>Share of recent snapshots that went unacknowledged, 0..1.</summary>
        public float Loss { get; private set; }

        internal SentPacket Begin(uint sequence)
        {
            // A packet this old has had time to be confirmed, or never will be.
            if (sequence > LossLag)
            {
                SentPacket? older = Sent[(sequence - LossLag) % SentRing];
                if (older != null && older.Sequence == sequence - LossLag)
                {
                    _sentRecently++;
                    if (older.Acked) _ackedRecently++;
                }
                if (_sentRecently >= LossSample)
                {
                    Loss = 1f - (float)_ackedRecently / _sentRecently;
                    _sentRecently = 0;
                    _ackedRecently = 0;
                }
            }

            ref SentPacket packet = ref Sent[sequence % SentRing];
            packet ??= new SentPacket();
            packet.Sequence = sequence;
            packet.Acked = false;
            packet.Ghosts.Clear();
            packet.Despawns.Clear();
            packet.Events.Clear();
            packet.StringsEnd = 0;
            return packet;
        }

        /// <summary>
        /// The client has received <paramref name="latest"/>, and of the 32 before it, those whose bits are set. Each confirmed
        /// packet moves on what it carried: from here on, that's what differences are measured from.
        /// </summary>
        public void Acknowledge(uint latest, uint earlier)
        {
            if (latest == 0 || latest > Sequence) return;
            Confirm(latest);
            for (int i = 0; i < 32; i++)
            {
                if ((earlier & (1u << i)) == 0 || latest <= (uint)i + 1) continue;
                Confirm(latest - (uint)i - 1);
            }
        }

        private void Confirm(uint sequence)
        {
            SentPacket? packet = Sent[sequence % SentRing];
            if (packet == null || packet.Sequence != sequence || packet.Acked) return;
            packet.Acked = true;

            foreach (SentGhost sent in packet.Ghosts)
            {
                if (!Ghosts.TryGetValue(sent.Id, out ViewGhost? ghost) || sequence <= ghost.BaselineSequence || sequence < ghost.Joined) continue;
                ghost.Baseline = sent.Row;
                ghost.BaselineMask = sent.Mask;
                ghost.BaselineSequence = sequence;
            }

            foreach (uint id in packet.Despawns) Departing.Remove(id);
            if (packet.StringsEnd > ConfirmedStrings) ConfirmedStrings = packet.StringsEnd;
            foreach (uint id in packet.Events) Events.RemoveAll(e => e.Sequence == id);
        }

        /// <summary>
        /// Forgets what the client is known to hold, so that everything goes again in full. It should never be needed; it is
        /// here so that even a mistake in this code can't leave a client stuck.
        /// </summary>
        public void ResetBaselines()
        {
            foreach (ViewGhost ghost in Ghosts.Values)
            {
                ghost.Baseline = null;
                ghost.BaselineMask = 0;
                ghost.BaselineSequence = 0;
                ghost.LastSent = null;
                ghost.Joined = Sequence + 1;
            }
        }
    }

    /// <summary>
    /// The server's side of replication. Each snapshot it turns every entity with a <see cref="NetIdentityComponent"/> into a row
    /// of integers, then for each client writes the rows that differ from what that client has, most important first, for as long as
    /// they fit in the packet. What doesn't fit waits and gains priority, so that a crowd slows updates down rather than
    /// losing any, and a packet is never bigger than the network can carry whole.
    /// </summary>
    public sealed class GhostServer
    {
        /// <summary>
        /// How many snapshots back a difference may be measured from. A client keeps every row this long, and the server won't
        /// refer to anything older, which is what guarantees a client always has what a snapshot refers to.
        /// </summary>
        public const int BaselineWindow = 128;

        private const int WindowBits = 7;
        private const float LeaveMargin = 1.2f;     // Hysteresis, so something at the edge doesn't flicker in and out.
        private const int MostDepartures = 200;
        private const int MostStrings = 24;
        private const int EventBits = 3000;
        private const uint EventLife = 45;          // Ticks. After this it would look odd anyway.

        private sealed class Record
        {
            public uint Id;
            public int[] Row = Array.Empty<int>();
            public ulong Mask;
            public int HighestString;
            public Vector2 Position;
            public bool HasPosition;
            public float Importance;
            public bool Everywhere;
            public uint Stamp;
        }

        private sealed class Layout
        {
            public ulong Mask;
            public int Lanes;
            public ComponentCodec[] Codecs = Array.Empty<ComponentCodec>();
            public int[] Offsets = Array.Empty<int>();
            public int FlatLane = -1;   // A height the client works out itself, from the same ground.
        }

        private readonly record struct Candidate(uint Id, GhostView.ViewGhost Ghost, Record Record, float Score);

        private readonly ECSWorld _world;
        private readonly NetStrings _strings = new();
        private readonly Dictionary<uint, Record> _records = new();
        private readonly Dictionary<Archetype, Layout> _layouts = new();
        private readonly BitMask _identityMask = new BitMask(typeof(NetIdentityComponent));
        private readonly BitWriter _scratch = new();
        private readonly List<uint> _gone = new();
        private readonly List<Candidate> _candidates = new();
        private int[] _capture = new int[64];
        private uint _stamp;
        private uint _nextId = 1;

        public GhostServer(ECSWorld world)
        {
            _world = world;
        }

        public int GhostCount => _records.Count;

        /// <summary>A number no ghost in this server's life has had, so a late message can't be mistaken for a newcomer.</summary>
        public uint NextId() => _nextId++;

        /// <summary>Reads every replicated entity. Once per snapshot, however many clients there are.</summary>
        public void Capture()
        {
            _stamp++;
            foreach (Archetype archetype in _world.QueryArchetypes(_identityMask, BitMask.Empty))
            {
                Layout layout = LayoutOf(archetype);
                if (_capture.Length < layout.Lanes) _capture = new int[layout.Lanes * 2];

                List<NetIdentityComponent> identities = archetype.GetComponentArray<NetIdentityComponent>()!;
                List<PositionComponent>? positions = archetype.GetComponentArray<PositionComponent>();

                for (int i = 0; i < archetype.EntityCount; i++)
                {
                    NetIdentityComponent identity = identities[i];
                    _strings.Highest = 0;
                    for (int c = 0; c < layout.Codecs.Length; c++) layout.Codecs[c].Capture(archetype, i, _capture, layout.Offsets[c], _strings);
                    if (layout.FlatLane >= 0) _capture[layout.FlatLane] = 0;

                    if (!_records.TryGetValue(identity.Id, out Record? record))
                    {
                        record = new Record { Id = identity.Id };
                        _records.Add(identity.Id, record);
                    }

                    // A row is never written to once made: a new one is what says something changed.
                    if (record.Mask != layout.Mask || !_capture.AsSpan(0, layout.Lanes).SequenceEqual(record.Row))
                    {
                        record.Row = _capture.AsSpan(0, layout.Lanes).ToArray();
                        record.Mask = layout.Mask;
                        record.HighestString = _strings.Highest;
                    }

                    record.HasPosition = positions != null;
                    if (positions != null) record.Position = positions[i].value.Xz;
                    record.Importance = identity.Importance;
                    record.Everywhere = identity.Everywhere;
                    record.Stamp = _stamp;
                }
            }

            _gone.Clear();
            foreach (Record record in _records.Values)
            {
                if (record.Stamp != _stamp) _gone.Add(record.Id);
            }
            foreach (uint id in _gone) _records.Remove(id);
        }

        private Layout LayoutOf(Archetype archetype)
        {
            if (_layouts.TryGetValue(archetype, out Layout? layout)) return layout;

            ulong mask = 0;
            ulong replaced = 0;
            foreach (ComponentCodec codec in NetSchema.Components)
            {
                if (!archetype.Mask.Has(ComponentTypeRegistry.GetId(codec.Type))) continue;
                mask |= 1ul << codec.Index;
                replaced |= codec.Replaced;
            }
            mask &= ~replaced;

            var codecs = new List<ComponentCodec>();
            var offsets = new List<int>();
            int lanes = 0;
            int flatLane = -1;
            bool terrainFixed = archetype.Has<FixToTerrainHeightComponent>();
            foreach (ComponentCodec codec in NetSchema.Components)
            {
                if ((mask & (1ul << codec.Index)) == 0) continue;
                if (terrainFixed && codec.Type == typeof(PositionComponent)) flatLane = lanes + 1;
                codecs.Add(codec);
                offsets.Add(lanes);
                lanes += codec.Lanes;
            }

            layout = new Layout { Mask = mask, Lanes = lanes, Codecs = codecs.ToArray(), Offsets = offsets.ToArray(), FlatLane = flatLane };
            _layouts.Add(archetype, layout);
            return layout;
        }

        /// <summary>Encodes an event, for <see cref="Queue"/> to as many clients as should hear it.</summary>
        public NetEventPacket Encode<T>(in T value, uint tick) where T : struct
        {
            int type = NetSchema.IndexOfEvent(typeof(T));
            var bits = new BitWriter(32);
            T copy = value;
            ((EventCodec<T>)NetSchema.Events[type]).Write(ref copy, bits);
            return new NetEventPacket { Type = type, Tick = tick, Bits = bits };
        }

        public void Queue(GhostView view, NetEventPacket packet)
        {
            view.Events.Add(new GhostView.QueuedEvent(view.NextEvent++, packet, packet.Tick + EventLife));
        }

        /// <summary>Writes this client's snapshot into <paramref name="writer"/>, keeping it within <paramref name="maxBits"/> in all.</summary>
        public void WriteSnapshot(GhostView view, BitWriter writer, int maxBits, uint tick)
        {
            uint sequence = ++view.Sequence;
            GhostView.SentPacket packet = view.Begin(sequence);

            writer.WriteVarUInt(sequence);
            WriteStrings(view, writer, packet);
            UpdateRelevance(view, tick);
            WriteDepartures(view, writer, packet);
            WriteEvents(view, writer, packet, tick);
            WriteGhosts(view, writer, packet, sequence, maxBits - 1);
            writer.WriteBool(false);
        }

        private void WriteStrings(GhostView view, BitWriter writer, GhostView.SentPacket packet)
        {
            int first = view.ConfirmedStrings;
            int count = Math.Min(_strings.Count - first, MostStrings);
            writer.WriteVarUInt((uint)first);
            writer.WriteVarUInt((uint)count);
            for (int i = 0; i < count; i++) writer.WriteString(_strings.Get(first + i));
            packet.StringsEnd = first + count;
        }

        private void UpdateRelevance(GhostView view, uint tick)
        {
            float enter = view.Radius * view.Radius;
            float leave = enter * LeaveMargin * LeaveMargin;

            foreach (Record record in _records.Values)
            {
                bool known = view.Ghosts.ContainsKey(record.Id);
                float distance = record.HasPosition ? (record.Position - view.Focus).LengthSquared : 0f;
                bool relevant = record.Everywhere || distance < (known ? leave : enter);

                if (relevant && !known)
                {
                    view.Ghosts.Add(record.Id, new GhostView.ViewGhost { Joined = view.Sequence });
                    view.Departing.Remove(record.Id);
                }
                else if (!relevant && known)
                {
                    view.Ghosts.Remove(record.Id);
                    view.Departing[record.Id] = tick;
                }
            }

            _gone.Clear();
            foreach (uint id in view.Ghosts.Keys)
            {
                if (!_records.ContainsKey(id)) _gone.Add(id);
            }
            foreach (uint id in _gone)
            {
                view.Ghosts.Remove(id);
                view.Departing[id] = tick;
            }
        }

        // Said again in every snapshot until confirmed, so a lost one can't leave a corpse standing on a client.
        private static void WriteDepartures(GhostView view, BitWriter writer, GhostView.SentPacket packet)
        {
            int count = Math.Min(view.Departing.Count, MostDepartures);
            writer.WriteVarUInt((uint)count);
            foreach ((uint id, uint leftAt) in view.Departing)
            {
                if (packet.Despawns.Count == count) break;
                writer.WriteVarUInt(id);
                writer.WriteUInt(leftAt);
                packet.Despawns.Add(id);
            }
        }

        private static void WriteEvents(GhostView view, BitWriter writer, GhostView.SentPacket packet, uint tick)
        {
            view.Events.RemoveAll(e => e.Expires < tick);

            int spent = 0;
            int count = 0;
            foreach (GhostView.QueuedEvent queued in view.Events)
            {
                int size = queued.Packet.Bits.BitLength + 56;
                if (spent + size > EventBits) break;
                spent += size;
                count++;
            }

            writer.WriteVarUInt((uint)count);
            for (int i = 0; i < count; i++)
            {
                GhostView.QueuedEvent queued = view.Events[i];
                writer.WriteVarUInt(queued.Sequence);
                writer.WriteUInt(queued.Packet.Tick);
                writer.WriteByte((byte)queued.Packet.Type);
                writer.Append(queued.Packet.Bits);
                packet.Events.Add(queued.Sequence);
            }
        }

        private void WriteGhosts(GhostView view, BitWriter writer, GhostView.SentPacket packet, uint sequence, int maxBits)
        {
            _candidates.Clear();
            foreach ((uint id, GhostView.ViewGhost ghost) in view.Ghosts)
            {
                Record record = _records[id];
                if (ReferenceEquals(record.Row, ghost.Baseline)) continue;
                if (ReferenceEquals(record.Row, ghost.LastSent) && sequence - ghost.LastSentSequence < view.ResendGap) continue;
                if (record.HighestString >= packet.StringsEnd && record.HighestString != 0) continue;

                // Nearer things first, but anything left waiting long enough outranks them.
                float nearness = record.HasPosition ? 1f - MathF.Min((record.Position - view.Focus).Length / view.Radius, 1f) : 1f;
                ghost.Priority += record.Importance * (1f + 3f * nearness * nearness);
                _candidates.Add(new Candidate(id, ghost, record, ghost.Priority));
            }
            _candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

            foreach (Candidate candidate in _candidates)
            {
                _scratch.Clear();
                WriteGhost(_scratch, candidate.Ghost, candidate.Record, sequence);
                if (writer.BitLength + _scratch.BitLength + 1 > maxBits) break;

                writer.WriteBool(true);
                writer.Append(_scratch);
                candidate.Ghost.Priority = 0f;
                candidate.Ghost.LastSent = candidate.Record.Row;
                candidate.Ghost.LastSentSequence = sequence;
                packet.Ghosts.Add(new GhostView.SentGhost(candidate.Id, candidate.Record.Row, candidate.Record.Mask));
            }
        }

        private static void WriteGhost(BitWriter writer, GhostView.ViewGhost ghost, Record record, uint sequence)
        {
            uint age = sequence - ghost.BaselineSequence;
            bool hasBaseline = ghost.Baseline != null && age < BaselineWindow;
            int[] baseline = hasBaseline ? ghost.Baseline! : Array.Empty<int>();
            ulong baselineMask = hasBaseline ? ghost.BaselineMask : 0ul;

            writer.WriteVarUInt(record.Id);
            writer.WriteBits(hasBaseline ? age : 0u, WindowBits);
            bool maskChanged = !hasBaseline || baselineMask != record.Mask;
            writer.WriteBool(maskChanged);
            if (maskChanged) writer.WriteVarULong(record.Mask);

            int at = 0;
            int baselineAt = 0;
            foreach (ComponentCodec codec in NetSchema.Components)
            {
                ulong bit = 1ul << codec.Index;
                bool inRow = (record.Mask & bit) != 0;
                bool inBaseline = (baselineMask & bit) != 0;

                if (inRow)
                {
                    bool changed = false;
                    for (int lane = 0; lane < codec.Lanes && !changed; lane++)
                    {
                        changed = record.Row[at + lane] != (inBaseline ? baseline[baselineAt + lane] : 0);
                    }

                    writer.WriteBool(changed);
                    if (changed)
                    {
                        for (int lane = 0; lane < codec.Lanes; lane++)
                        {
                            int delta = record.Row[at + lane] - (inBaseline ? baseline[baselineAt + lane] : 0);
                            writer.WriteBool(delta != 0);
                            if (delta != 0) writer.WriteDelta(delta);
                        }
                    }
                    at += codec.Lanes;
                }
                if (inBaseline) baselineAt += codec.Lanes;
            }
        }
    }
}
