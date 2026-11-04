using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using OpenTK.Mathematics;

namespace Dino_NetLab
{
    public enum TestMood
    {
        Calm,
        Angry,
        Asleep,
    }

    [Replicated]
    public struct TestStatsComponent : IComponent
    {
        public int Count;
        public uint Serial;
        [NetStep(0.25f)] public float Energy;
        public string Name;
        public TestMood Mood;
        public bool Flag;
        public byte Small;
        [NotReplicated] public float Private;
    }

    /// <summary>Comes and goes on entities, as a status effect would.</summary>
    [Replicated]
    public struct TestBurningComponent : IComponent
    {
        [NetStep(0.1f)] public float Left;
    }

    [NetEvent]
    public struct TestBangEvent
    {
        public int Number;
        public Vector3 Position;
        public string Text;
    }

    /// <summary>Tests of the replication layer alone, on made-up components: no game, no world, no window.</summary>
    internal static class CoreTests
    {
        public static int Run(Options options)
        {
            var report = new Report();

            report.Section("Bit stream");
            BitStreamRoundTrip(report);

            report.Section("Schema");
            SchemaRoundTrip(report);

            report.Section("Replication");
            foreach ((string name, NetworkConditions conditions) in Scenarios.All)
            {
                ReplicationConverges(report, name, conditions, options.Int("seed", 1));
            }
            ViewerTeleports(report);
            StandingGuardThenMoving(report);
            BudgetIsKept(report);
            EventsArriveOnce(report);

            report.Section("Hostile input");
            SnapshotFuzz(report, options.Int("fuzz", 40000));

            report.Section("Clocks");
            foreach ((string name, NetworkConditions conditions) in Scenarios.All)
            {
                ClocksSettle(report, name, conditions);
            }

            return report.Finish();
        }

        private static void BitStreamRoundTrip(Report report)
        {
            var random = new Random(7);
            int failures = 0;
            for (int round = 0; round < 3000; round++)
            {
                var writer = new BitWriter(8);
                var script = new List<(int kind, long value, int bits)>();
                int count = random.Next(1, 60);
                for (int i = 0; i < count; i++)
                {
                    int kind = random.Next(6);
                    int bits = random.Next(1, 33);
                    long value = kind switch
                    {
                        0 => (long)(random.NextInt64(0, 1L << 32) & ((1L << bits) - 1)),
                        1 => random.Next(2),
                        2 => random.NextInt64(0, 1L << 32),
                        3 => random.Next(int.MinValue, int.MaxValue),
                        4 => Interesting(random),
                        _ => random.Next(int.MinValue, int.MaxValue),
                    };
                    script.Add((kind, value, bits));
                    switch (kind)
                    {
                        case 0: writer.WriteBits((uint)value, bits); break;
                        case 1: writer.WriteBool(value != 0); break;
                        case 2: writer.WriteVarUInt((uint)value); break;
                        case 3: writer.WriteVarInt((int)value); break;
                        case 4: writer.WriteDelta((int)value); break;
                        case 5: writer.WriteFloat(BitConverter.Int32BitsToSingle((int)value)); break;
                    }
                }

                var reader = new BitReader(writer.ToArray());
                foreach ((int kind, long value, int bits) in script)
                {
                    long read = kind switch
                    {
                        0 => reader.ReadBits(bits),
                        1 => reader.ReadBool() ? 1 : 0,
                        2 => reader.ReadVarUInt(),
                        3 => reader.ReadVarInt(),
                        4 => reader.ReadDelta(),
                        _ => BitConverter.SingleToInt32Bits(reader.ReadFloat()),
                    };
                    if (read != value) failures++;
                }
            }
            report.Check("3000 random streams read back exactly", failures == 0, $"{failures} wrong values");

            var text = new BitWriter();
            text.WriteBool(true);
            text.WriteString("Åsa och Björn ✓");
            text.WriteVarULong(ulong.MaxValue);
            var back = new BitReader(text.ToArray());
            back.ReadBool();
            report.Check("Strings and long numbers survive not being byte aligned", back.ReadString(64) == "Åsa och Björn ✓" && back.ReadVarULong() == ulong.MaxValue);

            var appended = new BitWriter();
            var part = new BitWriter();
            part.WriteBits(0x15, 5);
            appended.WriteBits(1, 3);
            appended.Append(part);
            appended.WriteBits(0x2A, 6);
            var parts = new BitReader(appended.ToArray());
            report.Check("Appending keeps bits in order", parts.ReadBits(3) == 1 && parts.ReadBits(5) == 0x15 && parts.ReadBits(6) == 0x2A);

            bool threw = false;
            try
            {
                new BitReader(new byte[2]).ReadBits(32);
            }
            catch (NetReadException)
            {
                threw = true;
            }
            report.Check("Reading past the end is reported, not a crash", threw);
        }

        private static long Interesting(Random random)
        {
            int[] values = { 0, 1, -1, 7, 8, -8, -9, 127, 128, -128, -129, 32767, 32768, -32768, int.MaxValue, int.MinValue };
            return values[random.Next(values.Length)];
        }

        private static void SchemaRoundTrip(Report report)
        {
            var server = new Harness(new NetworkConditions(), 3);
            Entity entity = server.Spawn(new Vector3(12.34f, 0f, -56.78f), new TestStatsComponent
            {
                Count = -123456,
                Serial = 4000000000u,
                Energy = 3.3f,
                Name = "Fearsome Sentry",
                Mood = TestMood.Asleep,
                Flag = true,
                Small = 200,
                Private = 99f,
            });
            server.Run(30);

            Ghost? ghost = server.Client.Ghosts.Values.FirstOrDefault();
            report.Check("A ghost appeared", ghost != null);
            if (ghost == null) return;

            EntityView view = server.ClientWorld.GetEntityView(ghost.Entity);
            var stats = view.Get<TestStatsComponent>();
            Vector3 position = view.Get<PositionComponent>().value;
            report.Check("Integers survive whole", stats.Count == -123456 && stats.Serial == 4000000000u && stats.Small == 200);
            report.Check("Floats arrive to their step", MathF.Abs(stats.Energy - 3.25f) < 1e-4f, $"got {stats.Energy}");
            report.Check("Strings, enums and booleans arrive", stats.Name == "Fearsome Sentry" && stats.Mood == TestMood.Asleep && stats.Flag);
            report.Check("Private fields stay behind", stats.Private == 0f);
            report.Check("Vectors arrive to 1/64 m", (position - new Vector3(12.34f, 0f, -56.78f)).Length < 1f / 64f);
            report.Check("The schema has a hash", NetSchema.Hash != 0);
        }

        private static void ReplicationConverges(Report report, string name, NetworkConditions conditions, int seed)
        {
            var harness = new Harness(conditions, seed);
            var random = new Random(seed);
            var live = new List<Entity>();
            for (int i = 0; i < 260; i++) live.Add(harness.Spawn(RandomSpot(random), RandomStats(random, i)));

            // A minute of a busy world: things moving, dying, being born, catching fire and going out, and the viewer wandering.
            for (int tick = 0; tick < 3600; tick++)
            {
                foreach (Entity entity in live)
                {
                    if (random.NextDouble() > 0.3) continue;
                    EntityView view = harness.ServerWorld.GetEntityView(entity);
                    var position = view.Get<PositionComponent>();
                    position.value += new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f);
                    view.Set(position);
                    if (random.NextDouble() > 0.1) continue;
                    var stats = view.Get<TestStatsComponent>();
                    stats.Count++;
                    stats.Energy = (float)random.NextDouble() * 10f;
                    stats.Mood = (TestMood)random.Next(3);
                    view.Set(stats);
                }

                if (random.NextDouble() < 0.1 && live.Count > 100)
                {
                    int index = random.Next(live.Count);
                    harness.ServerWorld.DestroyEntity(live[index]);
                    live.RemoveAt(index);
                }
                if (random.NextDouble() < 0.1) live.Add(harness.Spawn(RandomSpot(random), RandomStats(random, tick)));

                if (random.NextDouble() < 0.02)
                {
                    Entity entity = live[random.Next(live.Count)];
                    EntityView view = harness.ServerWorld.GetEntityView(entity);
                    if (view.Has<TestBurningComponent>()) harness.ServerWorld.RemoveComponentFromEntity(entity, typeof(TestBurningComponent));
                    else harness.ServerWorld.AddComponentToEntity(entity, new TestBurningComponent { Left = 5f });
                }

                harness.View.Focus = new Vector2(MathF.Sin(tick * 0.004f) * 90f, MathF.Cos(tick * 0.003f) * 90f);
                harness.Step();
            }

            // Then everything stops. However badly it went, the client must end up with exactly what the server has.
            harness.Conditions.LossChance = Math.Min(conditions.LossChance, 0.05);
            harness.Run(60 * 12);
            int wrong = harness.CountDifferences(out string first);
            report.Check($"{name}: client matches server once things settle", wrong == 0, $"{wrong} differences, first: {first}");
            report.Check($"{name}: nothing ever referred to state the client didn't hold", harness.Client.Mismatches == 0, $"{harness.Client.Mismatches} mismatches");
            report.Note($"{name}: {harness.Client.SnapshotsRead} snapshots, {harness.AverageBytes:0} bytes each on average, largest {harness.LargestPacket}, {harness.BytesPerSecond / 1024.0:0.0} KB/s");
        }

        // A hero travelling to a shrine and straight back: ghosts leave and return while confirmations of their old state are still
        // on the wire. Those must not be believed, as the client may have let go of what they confirm.
        private static void ViewerTeleports(Report report)
        {
            var harness = new Harness(new NetworkConditions { Latency = 240, Jitter = 60, LossChance = 0.05 }, 21);
            var random = new Random(21);
            var live = new List<Entity>();
            for (int i = 0; i < 60; i++) live.Add(harness.Spawn(new Vector3(random.NextSingle() * 60f, 0f, random.NextSingle() * 60f), RandomStats(random, i)));

            for (int tick = 0; tick < 3600; tick++)
            {
                foreach (Entity entity in live)
                {
                    EntityView view = harness.ServerWorld.GetEntityView(entity);
                    var position = view.Get<PositionComponent>();
                    position.value.X += random.NextSingle() - 0.5f;
                    view.Set(position);
                }

                // Away long enough for the client to drop the ghosts, back before the confirmations of what it held arrive.
                harness.View.Focus = tick % 40 < 14 ? new Vector2(5000f, 5000f) : new Vector2(30f, 30f);
                harness.Step();
            }

            harness.View.Focus = new Vector2(30f, 30f);
            harness.Run(60 * 10);
            report.Check("A viewer jumping away and back: nothing refers to state the client let go of", harness.Client.Mismatches == 0, $"{harness.Client.Mismatches} mismatches");
            report.Check("A viewer jumping away and back: everything comes back right", harness.CountDifferences(out string first) == 0, first);
        }

        // An enemy standing watch sends nothing for as long as it stands. When it wakes, what the client last confirmed is older than
        // anything a difference may be measured from, and it has to go in full.
        private static void StandingGuardThenMoving(Report report)
        {
            var harness = new Harness(new NetworkConditions { Latency = 50, Jitter = 10 }, 31);
            var random = new Random(31);
            var live = new List<Entity>();
            for (int i = 0; i < 40; i++) live.Add(harness.Spawn(RandomSpot(random) * 0.3f, RandomStats(random, i)));

            for (int round = 0; round < 6; round++)
            {
                harness.Run(60 * 7);
                foreach (Entity entity in live)
                {
                    EntityView view = harness.ServerWorld.GetEntityView(entity);
                    var position = view.Get<PositionComponent>();
                    position.value += new Vector3(1.5f, 0f, -0.75f);
                    view.Set(position);
                }
                harness.Run(60);
                int wrong = harness.CountDifferences(out string first);
                if (wrong != 0)
                {
                    report.Check("Something idle for longer than the baseline window moves again", false, first);
                    return;
                }
            }
            report.Check("Something idle for longer than the baseline window moves again", harness.Client.Mismatches == 0, $"{harness.Client.Mismatches} mismatches");
        }

        private static Vector3 RandomSpot(Random random) => new Vector3(random.NextSingle() * 400f - 200f, 0f, random.NextSingle() * 400f - 200f);

        private static TestStatsComponent RandomStats(Random random, int index) => new TestStatsComponent
        {
            Count = index,
            Energy = random.NextSingle() * 10f,
            Name = $"Thing {index % 37}",
            Mood = (TestMood)random.Next(3),
        };

        private static void BudgetIsKept(Report report)
        {
            var harness = new Harness(new NetworkConditions(), 5);
            harness.Budget = 500;
            var random = new Random(5);
            var live = new List<Entity>();
            for (int i = 0; i < 1500; i++) live.Add(harness.Spawn(RandomSpot(random) * 0.2f, RandomStats(random, i)));

            for (int tick = 0; tick < 900; tick++)
            {
                foreach (Entity entity in live)
                {
                    EntityView view = harness.ServerWorld.GetEntityView(entity);
                    var position = view.Get<PositionComponent>();
                    position.value.X += 0.1f;
                    view.Set(position);
                }
                harness.Step();
            }
            harness.Run(60 * 6);
            report.Check("1500 moving things never make a packet over budget", harness.LargestPacket <= harness.Budget, $"largest {harness.LargestPacket} of {harness.Budget}");
            report.Check("Under that load everything still arrives eventually", harness.CountDifferences(out string first) == 0, first);
        }

        private static void EventsArriveOnce(Report report)
        {
            var harness = new Harness(new NetworkConditions { Latency = 60, Jitter = 20, LossChance = 0.15, DuplicateChance = 0.1 }, 9);
            var heard = new Dictionary<int, int>();
            harness.Events.On<TestBangEvent>(e => heard[e.Number] = heard.GetValueOrDefault(e.Number) + 1);

            const int Sent = 300;
            for (int i = 0; i < Sent; i++)
            {
                harness.Ghosts.Queue(harness.View, harness.Ghosts.Encode(new TestBangEvent { Number = i, Position = Vector3.One, Text = "bang" }, harness.Tick));
                harness.Run(6);
            }
            harness.Run(120);

            int twice = heard.Values.Count(c => c > 1);
            report.Check("No event is heard twice, through 15% loss and 10% duplication", twice == 0, $"{twice} heard twice");
            report.Check("Nearly every event gets through", heard.Count >= Sent * 0.98, $"{heard.Count} of {Sent}");
        }

        private static void SnapshotFuzz(Report report, int rounds)
        {
            // Real snapshots to damage, so that the fuzz gets past the first few fields and reaches the code that matters.
            var source = new Harness(new NetworkConditions(), 11);
            var random = new Random(11);
            for (int i = 0; i < 40; i++) source.Spawn(RandomSpot(random) * 0.3f, RandomStats(random, i));
            source.KeepPackets = true;
            for (int tick = 0; tick < 200; tick++)
            {
                source.Ghosts.Queue(source.View, source.Ghosts.Encode(new TestBangEvent { Number = tick, Text = "x" }, source.Tick));
                source.Step();
            }

            int rejected = 0;
            int survived = 0;
            string? crash = null;
            for (int round = 0; round < rounds && crash == null; round++)
            {
                var world = new ECSWorld();
                var client = new GhostClient(world, new NullHost(), new NetEventHandlers());
                try
                {
                    for (int i = 0; i < 4; i++)
                    {
                        byte[] packet = (byte[])source.Packets[random.Next(source.Packets.Count)].Clone();
                        Damage(packet, random, out int length);
                        client.ReadSnapshot(new BitReader(packet, 0, length), 1000u + (uint)i);
                        client.CreatePending();
                        world.ApplyDeferredCommands();
                        client.Apply(1000.0 + i);
                        survived++;
                    }
                }
                catch (NetReadException)
                {
                    rejected++;
                }
                catch (Exception e)
                {
                    crash = e.ToString();
                }
            }
            report.Check($"{rounds} damaged snapshots: refused or absorbed, never a crash", crash == null, crash ?? "");
            report.Note($"{rejected} refused, {survived} absorbed");

            string? garbageCrash = null;
            for (int round = 0; round < rounds && garbageCrash == null; round++)
            {
                byte[] garbage = new byte[random.Next(0, 1300)];
                random.NextBytes(garbage);
                var client = new GhostClient(new ECSWorld(), new NullHost(), new NetEventHandlers());
                try
                {
                    client.ReadSnapshot(new BitReader(garbage), 5u);
                }
                catch (NetReadException)
                {
                }
                catch (Exception e)
                {
                    garbageCrash = e.ToString();
                }
            }
            report.Check("Pure noise never crashes it either", garbageCrash == null, garbageCrash ?? "");
        }

        private static void Damage(byte[] packet, Random random, out int length)
        {
            length = packet.Length;
            switch (random.Next(3))
            {
                case 0:
                    for (int i = 0; i < 1 + random.Next(8); i++) packet[random.Next(packet.Length)] ^= (byte)(1 << random.Next(8));
                    break;
                case 1:
                    length = random.Next(packet.Length + 1);
                    break;
                default:
                    int from = random.Next(packet.Length);
                    for (int i = from; i < Math.Min(from + 16, packet.Length); i++) packet[i] = (byte)random.Next(256);
                    break;
            }
        }

        private sealed class NullHost : IGhostHost
        {
            public void Decorate(Ghost ghost, List<IComponent> components) { }
            public void Created(Ghost ghost) { }
            public void Removing(Ghost ghost) { }
        }

        // The two clocks driven by a fake server over a simulated link: does a client find the right lead and stay there?
        private static void ClocksSettle(Report report, string name, NetworkConditions conditions)
        {
            double now = 0.0;
            var random = new Random(3);
            var command = new CommandClock();
            var render = new InterpolationClock();
            uint serverTick = 1000;
            command.Start(serverTick);

            var toServer = new PriorityQueue<uint, double>();
            var toClient = new PriorityQueue<(uint tick, int spare), double>();
            uint newestArrived = 0;
            int worstSpare = int.MaxValue;
            int late = 0;
            int commands = 0;
            double behindNewest = 0.0;
            int samples = 0;
            double serverAccumulator = 0.0;
            double slowest = 1.0;
            double fastest = 1.0;

            double Oneway()
            {
                double stall = random.NextDouble() < conditions.SpikeChance ? conditions.SpikeLength : 0.0;
                return (conditions.Latency * 0.5 + random.NextDouble() * conditions.Jitter * 0.5 + stall) / 1000.0;
            }

            for (int frame = 0; frame < 60 * 90; frame++)
            {
                double delta = 1.0 / 60.0 + (random.NextDouble() - 0.5) * 0.004;
                now += delta;
                serverAccumulator += delta;

                while (serverAccumulator >= NetTime.TickSecondsPrecise)
                {
                    serverAccumulator -= NetTime.TickSecondsPrecise;
                    while (toServer.TryPeek(out uint tick, out double due) && due <= now)
                    {
                        toServer.Dequeue();
                        if (tick > newestArrived) newestArrived = tick;
                        int spare = (int)tick - (int)serverTick;
                        worstSpare = Math.Min(worstSpare, spare);
                        if (frame > 60 * 30)
                        {
                            commands++;
                            if (spare < 0) late++;
                        }
                    }

                    serverTick++;
                    if (serverTick % 2 == 0)
                    {
                        if (random.NextDouble() >= conditions.LossChance) toClient.Enqueue((serverTick, worstSpare == int.MaxValue ? 0 : worstSpare), now + Oneway());
                        if (serverTick % 30 == 0) worstSpare = int.MaxValue;
                    }
                }

                uint newest = 0;
                while (toClient.TryPeek(out (uint tick, int spare) snapshot, out double due) && due <= now)
                {
                    toClient.Dequeue();
                    newest = snapshot.tick;
                    render.OnSnapshot(snapshot.tick, 2);
                    command.OnFeedback(snapshot.spare);
                }

                render.Advance(delta);
                int ticks = command.Advance(delta);
                for (int i = 0; i < ticks; i++)
                {
                    if (random.NextDouble() >= conditions.LossChance) toServer.Enqueue(command.Tick, now + Oneway());
                    command.Next();
                }

                if (frame > 60 * 30)
                {
                    slowest = Math.Min(slowest, command.Rate);
                    fastest = Math.Max(fastest, command.Rate);
                    if (newest != 0)
                    {
                        behindNewest += newest - render.RenderTick;
                        samples++;
                    }
                }
            }

            double lateShare = commands == 0 ? 1.0 : (double)late / commands;
            report.Check($"{name}: after settling, under 3% of commands arrive late", lateShare < 0.03 || conditions.SpikeChance > 0, $"{lateShare:P1}");
            report.Note($"{name}: {lateShare:P2} late, client lead {(int)command.Tick - (int)serverTick} ticks, rate {slowest:0.000}..{fastest:0.000}, drawn {behindNewest / Math.Max(samples, 1) * 1000.0 / 60.0:0} ms behind the newest snapshot");
        }
    }

    /// <summary>A server world and a client world joined by a simulated network, stepped by hand so that a minute passes in a moment.</summary>
    internal sealed class Harness : IGhostHost, INetListener
    {
        private readonly INetTransport _serverEnd;
        private readonly INetTransport _clientEnd;
        private readonly ClientEnd _clientListener;
        private INetConnection? _serverConnection;
        private double _now;
        private long _bytes;
        private int _packets;

        public readonly ECSWorld ServerWorld = new();
        public readonly ECSWorld ClientWorld = new();
        public readonly GhostServer Ghosts;
        public readonly GhostView View = new();
        public readonly GhostClient Client;
        public readonly NetEventHandlers Events = new();
        public readonly NetworkConditions Conditions;
        public readonly List<byte[]> Packets = new();
        public uint Tick { get; private set; } = 100;
        public int Budget = 1150;
        public int LargestPacket { get; private set; }
        public bool KeepPackets;

        public double AverageBytes => _packets == 0 ? 0.0 : (double)_bytes / _packets;
        public double BytesPerSecond => _now == 0.0 ? 0.0 : _bytes / (_now / 1000.0);

        public Harness(NetworkConditions conditions, int seed)
        {
            Conditions = new NetworkConditions
            {
                Latency = conditions.Latency,
                Jitter = conditions.Jitter,
                LossChance = conditions.LossChance,
                DuplicateChance = conditions.DuplicateChance,
                SpikeChance = conditions.SpikeChance,
                SpikeLength = conditions.SpikeLength,
            };
            (LoopbackTransport serverEnd, LoopbackTransport clientEnd) = LoopbackTransport.CreatePair();
            _serverEnd = new ImpairedTransport(serverEnd, Conditions, () => _now, seed * 2 + 1);
            _clientEnd = new ImpairedTransport(clientEnd, Conditions, () => _now, seed * 2 + 2);
            Ghosts = new GhostServer(ServerWorld);
            Client = new GhostClient(ClientWorld, this, Events);
            _clientListener = new ClientEnd(this);
        }

        public Entity Spawn(Vector3 position, TestStatsComponent stats)
        {
            Entity entity = ServerWorld.CreateEntity(
                new NetIdentityComponent(Ghosts.NextId()),
                new PositionComponent(position),
                stats);
            ServerWorld.ApplyDeferredCommands();
            return entity;
        }

        public void Run(int ticks)
        {
            for (int i = 0; i < ticks; i++) Step();
        }

        public void Step()
        {
            _now += 1000.0 / 60.0;
            Tick++;
            ServerWorld.ApplyDeferredCommands();
            _serverEnd.Poll(this);
            _clientEnd.Poll(_clientListener);

            if (Tick % 2 == 0 && _serverConnection != null)
            {
                Ghosts.Capture();
                var writer = new BitWriter();
                writer.WriteUInt(Tick);
                Ghosts.WriteSnapshot(View, writer, Budget * 8, Tick);
                byte[] packet = writer.ToArray();
                LargestPacket = Math.Max(LargestPacket, packet.Length);
                _bytes += packet.Length;
                _packets++;
                if (KeepPackets) Packets.Add(packet.AsSpan(4).ToArray());
                _serverConnection.Send(packet, Delivery.Unreliable);
            }

            Client.CreatePending();
            ClientWorld.ApplyDeferredCommands();
            Client.Apply(Tick - 6);
            Events.Flush(Tick - 6);
            ClientWorld.ApplyDeferredCommands();
            ClientWorld.DirtyEntities.Clear();
            ServerWorld.DirtyEntities.Clear();

            _clientListener.SendAck();
        }

        public int CountDifferences(out string first)
        {
            first = "";
            int wrong = 0;
            Ghosts.Capture();

            var expected = new HashSet<uint>();
            foreach (Archetype archetype in ServerWorld.QueryArchetypes(new BitMask(typeof(NetIdentityComponent)), BitMask.Empty))
            {
                foreach (EntityView view in new ComponentAccessor(archetype))
                {
                    Vector3 position = view.Get<PositionComponent>().value;
                    uint id = view.Get<NetIdentityComponent>().Id;
                    if ((position.Xz - View.Focus).Length > View.Radius) continue;
                    expected.Add(id);

                    if (!Client.Ghosts.TryGetValue(id, out Ghost? ghost))
                    {
                        wrong++;
                        if (first.Length == 0) first = $"ghost {id} missing";
                        continue;
                    }

                    EntityView seen = ClientWorld.GetEntityView(ghost.Entity);
                    var stats = view.Get<TestStatsComponent>();
                    var theirs = seen.Get<TestStatsComponent>();
                    bool same = (seen.Get<PositionComponent>().value - position).Length < 0.02f
                        && stats.Count == theirs.Count && stats.Mood == theirs.Mood && stats.Name == theirs.Name
                        && MathF.Abs(stats.Energy - theirs.Energy) <= 0.125f + 1e-4f
                        && view.Has<TestBurningComponent>() == seen.Has<TestBurningComponent>();
                    if (same) continue;

                    wrong++;
                    if (first.Length == 0) first = $"ghost {id}: server {position} {stats.Count}, client {seen.Get<PositionComponent>().value} {theirs.Count}, burning {view.Has<TestBurningComponent>()}/{seen.Has<TestBurningComponent>()}";
                }
            }

            foreach (Ghost ghost in Client.Ghosts.Values)
            {
                if (expected.Contains(ghost.Id)) continue;
                if (!ServerHas(ghost.Id))
                {
                    wrong++;
                    if (first.Length == 0) first = $"ghost {ghost.Id} outlived its entity";
                }
            }
            return wrong;
        }

        private bool ServerHas(uint id)
        {
            foreach (Archetype archetype in ServerWorld.QueryArchetypes(new BitMask(typeof(NetIdentityComponent)), BitMask.Empty))
            {
                foreach (EntityView view in new ComponentAccessor(archetype))
                {
                    if (view.Get<NetIdentityComponent>().Id == id) return true;
                }
            }
            return false;
        }

        void IGhostHost.Decorate(Ghost ghost, List<IComponent> components) { }
        void IGhostHost.Created(Ghost ghost) { }
        void IGhostHost.Removing(Ghost ghost) { }

        void INetListener.OnConnected(INetConnection connection) => _serverConnection = connection;

        void INetListener.OnMessage(INetConnection connection, byte[] data, Delivery delivery)
        {
            var reader = new BitReader(data);
            View.Acknowledge(reader.ReadUInt(), reader.ReadUInt());
            if (reader.ReadBool()) View.ResetBaselines();
        }

        void INetListener.OnDisconnected(INetConnection connection, string reason) { }

        private sealed class ClientEnd : INetListener
        {
            private readonly Harness _owner;
            private INetConnection? _connection;

            public ClientEnd(Harness owner)
            {
                _owner = owner;
            }

            public void SendAck()
            {
                if (_connection == null) return;
                var writer = new BitWriter(16);
                writer.WriteUInt(_owner.Client.LatestSequence);
                writer.WriteUInt(_owner.Client.AckBits);
                writer.WriteBool(_owner.Client.NeedsReset);
                _owner.Client.NeedsReset = false;
                _connection.Send(writer.ToArray(), Delivery.Unreliable);
            }

            public void OnConnected(INetConnection connection) => _connection = connection;

            public void OnMessage(INetConnection connection, byte[] data, Delivery delivery)
            {
                var reader = new BitReader(data);
                _owner.Client.ReadSnapshot(reader, reader.ReadUInt());
            }

            public void OnDisconnected(INetConnection connection, string reason) { }
        }
    }
}
