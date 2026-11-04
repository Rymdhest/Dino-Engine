using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_NetLab
{
    /// <summary>A hero played by a script, on a client with no window: the real client, every part of it but the drawing.</summary>
    internal sealed class LabPlayer : IClientHost
    {
        private readonly Random _random;
        private readonly WorldCache _worlds;
        private readonly BitMask _enemyMask = new BitMask(typeof(EnemyComponent), typeof(PositionComponent));
        private double _nextFrame;
        private WorldRequest? _wanted;
        private Vector2 _wander;
        private int _wanderLeft;

        public readonly string Name;
        public readonly ECSWorld Ecs = new();
        public readonly GameClient Client;
        public readonly double FrameSeconds;
        public string? DisconnectReason;
        public int Arrivals;
        public int Swings;
        public int HitsSeen;
        public bool Fights = true;
        public int WhirlEveryFrames;    // Instead of fighting: a press of whirl, this often.
        public int WhirlsPressed;
        public bool PressUse;           // Presses use, once.
        public readonly List<(InteractKind kind, SiteState site)> PanelsOpened = new();
        private int _frames;
        public double DivergenceTotal;
        public double DivergenceWorst;
        public long DivergenceSamples;
        public readonly List<float> DivergenceSorted = new();

        public LabPlayer(string name, INetTransport transport, WorldCache worlds, HeroState hero, double frameSeconds, int seed)
        {
            Name = name;
            _worlds = worlds;
            _random = new Random(seed);
            FrameSeconds = frameSeconds;
            Client = new GameClient(transport, this, Ecs, hero, name);
            Client.Events.On<DamageEvent>(e =>
            {
                if (!e.ToHero) HitsSeen++;
            });
        }

        /// <summary>Runs a frame if one is due.</summary>
        public void Advance(double now)
        {
            while (_nextFrame <= now)
            {
                _nextFrame += FrameSeconds * (0.9 + _random.NextDouble() * 0.2);
                Client.Update(FrameSeconds);
                if (_wanted is WorldRequest request)
                {
                    Task<WorldData> world = _worlds.Get(request);
                    if (world.IsCompleted)
                    {
                        _wanted = null;
                        Client.EnterWorld(world.Result);
                    }
                }

                Ecs.ApplyDeferredCommands();
                if (Client.State == ClientState.Playing && Client.Render.Started) Client.Ghosts.Apply(Client.Render.RenderTick);
                Ecs.ApplyDeferredCommands();
                Ecs.DirtyEntities.Clear();
            }
        }

        public HeroIntent ReadIntent()
        {
            if (PressUse)
            {
                PressUse = false;
                return new HeroIntent { Interact = true };
            }

            if (WhirlEveryFrames > 0 && Client.Session != null)
            {
                if (++_frames % WhirlEveryFrames != 0) return default;
                WhirlsPressed++;
                return new HeroIntent { Whirl = true };
            }

            if (!Fights || Client.Session == null) return default;

            var intent = new HeroIntent();
            Vector2 here = Client.Predictor.Current.Position;
            Vector2 nearest = default;
            float best = float.PositiveInfinity;
            foreach (Archetype archetype in Ecs.QueryArchetypes(_enemyMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    if (entity.Get<EnemyComponent>().State == EnemyState.Dead) continue;
                    Vector2 position = entity.Get<PositionComponent>().value.Xz;
                    float distance = (position - here).Length;
                    if (distance >= best) continue;
                    best = distance;
                    nearest = position;
                }
            }

            if (--_wanderLeft <= 0)
            {
                _wander = new Vector2(_random.NextSingle() - 0.5f, _random.NextSingle() - 0.5f);
                _wanderLeft = _random.Next(15, 90);
            }

            if (best < 90f)
            {
                Vector2 toward = nearest - here;
                intent.Aim = toward;
                intent.Move = best > 4.5f ? toward : _wander;
                intent.Attack = best < 6f;
                if (intent.Attack) Swings++;
                intent.Whirl = best < 7f && _random.NextDouble() < 0.02;
                intent.Dash = best is > 10f and < 18f && _random.NextDouble() < 0.05;
            }
            else
            {
                intent.Aim = _wander;
                intent.Move = _wander;
                intent.Dash = _random.NextDouble() < 0.01;
            }
            intent.Flask = _random.NextDouble() < 0.004;
            intent.Interact = _random.NextDouble() < 0.01;
            return intent;
        }

        public void WorldNeeded(WorldRequest request) => _wanted = request;
        public void Arrived(Vector2 position) => Arrivals++;
        public void HeroActed(HeroStepEvents events, in HeroSimState hero) { }
        public void OpenPanel(InteractKind kind, SiteState site) => PanelsOpened.Add((kind, site));
        public void Disconnected(string reason) => DisconnectReason = reason;
        public void Decorate(Ghost ghost, List<IComponent> components) { }
        public void Created(Ghost ghost) { }
        public void Removing(Ghost ghost) { }
    }

    /// <summary>
    /// A whole game in one process on a simulated clock: a real server and real clients, joined by a line as bad as you like. Time
    /// passes a millisecond at a time, so a server tick and each client's frames fall as they would, at their own rates.
    /// </summary>
    internal sealed class GameLab : IDisposable
    {
        private const int History = 512;

        private readonly WorldCache _worlds;
        private readonly Dictionary<uint, Vector2[]> _truth = new();
        private readonly BitMask _identity = new BitMask(typeof(NetIdentityComponent), typeof(PositionComponent));
        private double _now;
        private double _nextTick;
        private uint _recorded;

        public readonly GameServer Server;
        public readonly List<LabPlayer> Players = new();
        public double SecondsRun => _now;

        /// <summary>What a server tick really costs, in milliseconds of this machine's time.</summary>
        public double TickCostTotal;
        public double TickCostWorst;
        public int TicksTimed;

        public GameLab(WorldCache worlds, WorldRequest request, string startSite = "BanditCamp")
        {
            _worlds = worlds;

            // Generation takes real time, and this clock doesn't wait for it.
            worlds.Get(request).Wait();
            Server = new GameServer(new ServerSettings { Request = request, StartSite = startSite, AllowTeleport = true }, worlds);
            Server.Clock = () => (long)(_now * 1000.0);
        }

        public LabPlayer AddPlayer(string name, NetworkConditions conditions, int seed, double frameRate = 60.0, HeroState? hero = null)
        {
            (LoopbackTransport serverEnd, LoopbackTransport clientEnd) = LoopbackTransport.CreatePair();
            Server.AddTransport(serverEnd);
            var impaired = new ImpairedTransport(clientEnd, conditions, () => _now * 1000.0, seed);
            var player = new LabPlayer(name, impaired, _worlds, hero ?? StrongHero(), 1.0 / frameRate, seed);
            player.Client.Clock = () => (long)(_now * 1000.0);
            Players.Add(player);
            return player;
        }

        public static HeroState StrongHero()
        {
            HeroState hero = HeroState.CreateNew();
            hero.Level = 12;
            hero.Might = 40;
            hero.Spirit = 30;
            return hero;
        }

        /// <summary>Lets <paramref name="seconds"/> of game go by.</summary>
        public void Run(double seconds, Action? eachTick = null)
        {
            double end = _now + seconds;
            while (_now < end)
            {
                _now += 0.001;
                while (_nextTick <= _now)
                {
                    _nextTick += NetTime.TickSecondsPrecise;

                    long started = System.Diagnostics.Stopwatch.GetTimestamp();
                    Server.Step();
                    double took = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    TickCostTotal += took;
                    TickCostWorst = Math.Max(TickCostWorst, took);
                    TicksTimed++;

                    RecordTruth();
                    eachTick?.Invoke();
                    foreach (LabPlayer player in Players) MeasureDivergence(player);
                }

                foreach (LabPlayer player in Players) player.Advance(_now);
            }
        }

        /// <summary>Stops the server for a while, as a stall would, while clients carry on.</summary>
        public void StallServer(double seconds)
        {
            double end = _now + seconds;
            while (_now < end)
            {
                _now += 0.001;
                foreach (LabPlayer player in Players) player.Advance(_now);
            }
            _nextTick = _now;
        }

        // Where everything really is, tick by tick, to hold what clients show up against.
        private void RecordTruth()
        {
            _recorded = Server.Tick - 1;
            int slot = (int)(_recorded % History);
            foreach (Archetype archetype in Server.Ecs.QueryArchetypes(_identity, BitMask.Empty))
            {
                List<NetIdentityComponent> identities = archetype.GetComponentArray<NetIdentityComponent>()!;
                List<PositionComponent> positions = archetype.GetComponentArray<PositionComponent>()!;
                for (int i = 0; i < archetype.EntityCount; i++)
                {
                    uint id = identities[i].Id;
                    if (!_truth.TryGetValue(id, out Vector2[]? trail))
                    {
                        trail = new Vector2[History];
                        Array.Fill(trail, positions[i].value.Xz);
                        _truth.Add(id, trail);
                    }
                    trail[slot] = positions[i].value.Xz;
                }
            }
        }

        // How far from the truth each thing is shown, at the moment it is being shown as.
        private void MeasureDivergence(LabPlayer player)
        {
            if (player.Client.State != ClientState.Playing || !player.Client.Render.Started) return;
            uint moment = (uint)Math.Max(player.Client.Render.RenderTick, 0.0);
            if (moment + History < _recorded + 8 || moment > _recorded) return;

            foreach (Ghost ghost in player.Client.Ghosts.Ghosts.Values)
            {
                if (ghost.Predicted != 0 || !ghost.Entity.IsValid()) continue;
                if (!_truth.TryGetValue(ghost.Id, out Vector2[]? trail)) continue;
                if (!player.Ecs.TryGetEntityLocation(ghost.Entity, out Archetype archetype, out int index) || !archetype.Has<PositionComponent>()) continue;

                float error = (archetype.GetComponent<PositionComponent>(index).value.Xz - trail[moment % History]).Length;
                if (error > 15f) continue;  // Teleported: that's a jump, not drift.

                player.DivergenceTotal += error;
                player.DivergenceSamples++;
                player.DivergenceWorst = Math.Max(player.DivergenceWorst, error);
                if (player.DivergenceSamples % 37 == 0) player.DivergenceSorted.Add(error);
            }
        }

        public void Dispose() => Server.Dispose();
    }

    /// <summary>A thing nobody has written any networking for, used to show that none is needed.</summary>
    [Replicated]
    public struct LabChilledComponent : IComponent
    {
        [NetStep(0.1f)] public float Strength;
        public string Source;
    }

    /// <summary>The whole game under test, on every kind of line.</summary>
    internal static class GameTests
    {
        public static int Run(Options options)
        {
            var report = new Report();
            var request = new WorldRequest(options.Int("seed", 303), "isle");
            var worlds = new WorldCache();
            Console.WriteLine("Growing a world...");
            WorldData world = worlds.Get(request).Result;

            report.Section("Invites");
            InvitesSurviveBeingPasted(report);

            report.Section("Worlds");
            WorldsAgree(report, request, world);

            report.Section("Five heroes in a fight, on every kind of line");
            foreach ((string name, NetworkConditions conditions) in Scenarios.All)
            {
                FiveHeroes(report, worlds, request, name, conditions, options.Int("seconds", 60));
            }

            report.Section("A mixed party");
            MixedParty(report, worlds, request);

            report.Section("A horde");
            Horde(report, worlds, request, Scenarios.All[1].conditions, "Good broadband");
            Horde(report, worlds, request, Scenarios.All[3].conditions, "Across an ocean");

            report.Section("Comings and goings");
            JoiningAndLeaving(report, worlds, request);
            PartyTravels(report, worlds, request);
            TooManyHeroes(report, worlds, request);
            WrongVersion(report, worlds, request);

            report.Section("Things going wrong");
            ServerStalls(report, worlds, request);
            ConnectionDies(report, worlds, request);
            HostileClient(report, worlds, request, options.Int("fuzz", 20000));

            report.Section("Doing things");
            foreach ((string name, NetworkConditions conditions) in Scenarios.All)
            {
                PressesHappenOnce(report, worlds, request, name, conditions);
            }
            LatePressIsNotRepeated(report);
            ActionsAtPanels(report, worlds, request);
            UsingThings(report, worlds);
            NewComponentNeedsNoCode(report, worlds, request);

            return report.Finish();
        }

        // A code goes through a chat program and someone's hands. It has to come out the other side intact, or not at all.
        private static void InvitesSurviveBeingPasted(Report report)
        {
            var random = new Random(5);
            int wrong = 0;
            for (int i = 0; i < 2000; i++)
            {
                var code = new InviteCode
                {
                    Public = i % 3 == 0 ? null : new System.Net.IPAddress(BitConverter.GetBytes(random.Next())),
                    Local = i % 5 == 0 ? null : new System.Net.IPAddress(BitConverter.GetBytes(random.Next())),
                    Port = random.Next(1, 65536),
                    Secret = (uint)random.NextInt64(0, uint.MaxValue),
                };
                if (!InviteCode.TryParse(code.Text, out InviteCode? back) || back == null
                    || back.Port != code.Port || back.Secret != code.Secret
                    || !Equals(back.Public, code.Public) || !Equals(back.Local, code.Local))
                {
                    wrong++;
                }
            }
            report.Check("2000 invites read back exactly", wrong == 0, $"{wrong} wrong");

            var sample = new InviteCode { Public = System.Net.IPAddress.Parse("203.0.113.7"), Local = System.Net.IPAddress.Parse("192.168.1.20"), Port = 27815, Secret = 0xDEADBEEF };
            string text = sample.Text;
            report.Note(text);
            report.Check("Case and stray spaces don't matter", InviteCode.TryParse($"  {text.ToLowerInvariant()} \n", out _));
            report.Check("Nor dashes going missing", InviteCode.TryParse(text.Replace("-", ""), out _));

            int noticed = 0;
            int tried = 0;
            const string Letters = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
            for (int i = 0; i < 2000; i++)
            {
                char[] typo = text.ToCharArray();
                int at = random.Next(5, typo.Length);
                if (typo[at] == '-') continue;
                char replacement = Letters[random.Next(Letters.Length)];
                if (replacement == typo[at]) continue;
                typo[at] = replacement;
                tried++;

                if (!InviteCode.TryParse(new string(typo), out InviteCode? mistaken) || mistaken == null) noticed++;
                else if (mistaken.Port == sample.Port && mistaken.Secret == sample.Secret && Equals(mistaken.Public, sample.Public) && Equals(mistaken.Local, sample.Local)) noticed++;
            }
            report.Check("A mistyped code is nearly always refused rather than sending someone to a stranger's address", noticed >= tried * 0.98, $"{noticed} of {tried}");
            report.Check("Half a code is refused", !InviteCode.TryParse(text.Substring(0, text.Length / 2), out _));
            report.Check("Something else entirely is refused", !InviteCode.TryParse("hello there", out _));
            report.Check("Secrets differ from one game to the next", InviteCode.NewSecret() != InviteCode.NewSecret());
        }

        private static void WorldsAgree(Report report, WorldRequest request, WorldData world)
        {
            WorldData again = new Dino_Engine.World.Generation.WorldGenerator().Generate(request);
            var first = Protocol.WorldHash(world);
            var second = Protocol.WorldHash(again);
            report.Check("The same seed grows the same world, structure and ground", first == second, $"{first} vs {second}");

            var mover = new CharacterMover(world);
            bool threadsAgree = true;
            Parallel.For(0, 8, i =>
            {
                var own = new CharacterMover(world);
                var random = new Random(4);
                Vector2 at = world.Sites[0].Position;
                var other = new CharacterMover(world);
                Vector2 check = at;
                for (int step = 0; step < 20000; step++)
                {
                    Vector2 wanted = new Vector2(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f) * 20f;
                    at = own.Move(at, wanted, 1.1f, NetTime.TickSeconds);
                    check = other.Move(check, wanted, 1.1f, NetTime.TickSeconds);
                    if (at != check) threadsAgree = false;
                }
            });
            report.Check("Eight threads walking one world at once all agree, so a server and client can share it", threadsAgree);
        }

        private static void FiveHeroes(Report report, WorldCache worlds, WorldRequest request, string name, NetworkConditions conditions, int seconds)
        {
            using var lab = new GameLab(worlds, request);
            for (int i = 0; i < Protocol.MaxPlayers; i++) lab.AddPlayer($"Hero {i + 1}", conditions, 100 + i);

            lab.Run(8);
            bool allIn = lab.Players.All(p => p.Client.State == ClientState.Playing);
            report.Check($"{name}: all five reach the world", allIn, string.Join(", ", lab.Players.Select(p => p.Client.State)));
            if (!allIn) return;

            // Arriving has to be as clean as staying: nobody should start a game by being dragged about.
            int correctionsArriving = lab.Players.Sum(p => p.Client.Predictor.Corrections);
            int comparedArriving = lab.Players.Sum(p => p.Client.Predictor.Compared);
            if (conditions.SpikeChance == 0) report.Check($"{name}: arriving is as clean as staying", correctionsArriving <= comparedArriving / 200 + 1, $"{correctionsArriving} of {comparedArriving}");

            int correctionsBefore = lab.Players.Sum(p => p.Client.Predictor.Corrections);
            int comparedBefore = lab.Players.Sum(p => p.Client.Predictor.Compared);
            int lateBefore = lab.Server.Players.Sum(p => p.LateCommands);
            int filledBefore = lab.Server.Players.Sum(p => p.FilledCommands);
            foreach (LabPlayer player in lab.Players) player.Client.Predictor.ForgetWorst();

            int peakGhosts = 0;
            lab.Run(seconds, () => peakGhosts = Math.Max(peakGhosts, lab.Server.Ghosts.GhostCount));

            int mismatches = lab.Players.Sum(p => p.Client.Ghosts.Mismatches);
            int corrections = lab.Players.Sum(p => p.Client.Predictor.Corrections) - correctionsBefore;
            int compared = lab.Players.Sum(p => p.Client.Predictor.Compared) - comparedBefore;
            double worst = lab.Players.Max(p => p.Client.Predictor.WorstCorrection);
            double late = lab.Server.Players.Sum(p => p.LateCommands) - lateBefore;
            double filled = lab.Server.Players.Sum(p => p.FilledCommands) - filledBefore;
            double ticks = seconds * (double)NetTime.TicksPerSecond * lab.Players.Count;

            // Everyone stands still, and whatever happened, the pictures must come back together.
            foreach (LabPlayer player in lab.Players) player.Fights = false;
            lab.Run(12);

            double kbPerSecond = lab.Players.Average(p => p.Client.BytesReceived) / lab.SecondsRun / 1024.0;
            double kbUp = lab.Players.Average(p => p.Client.BytesSent) / lab.SecondsRun / 1024.0;

            var sorted = lab.Players.SelectMany(p => p.DivergenceSorted).OrderBy(e => e).ToList();
            double median = sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
            double p99 = sorted.Count == 0 ? 0 : sorted[(int)(sorted.Count * 0.99)];

            int kills = lab.Server.Session!.Sites.Sum(s => s.Dead.Count);
            bool heroesAgree = lab.Players.Zip(lab.Server.Players).All(pair => HeroesMatch(pair.First.Client.Hero, pair.Second.Hero));
            bool positionsAgree = true;
            foreach (LabPlayer player in lab.Players)
            {
                ServerPlayer truth = lab.Server.Players.First(p => p.Name == player.Name);
                positionsAgree &= (player.Client.Predictor.Current.Position - truth.Sim.Position).Length < 0.01f;
            }

            bool cleanLine = conditions.SpikeChance == 0;
            report.Check($"{name}: the fight really happened", kills > 0 && peakGhosts > 5, $"{kills} kills, {peakGhosts} ghosts");
            if (cleanLine) report.Check($"{name}: a hero is almost never corrected", corrections <= compared / 500, $"{corrections} of {compared}");
            report.Check($"{name}: no snapshot ever referred to state a client lacked", mismatches == 0, $"{mismatches}");
            report.Check($"{name}: every hero ends exactly where the server has them", positionsAgree);
            report.Check($"{name}: every client's copy of its hero matches the server's", heroesAgree);
            report.Check($"{name}: all clients stayed connected", lab.Players.All(p => p.DisconnectReason == null));
            report.Check($"{name}: snapshots stay inside one packet", lab.Server.LargestSnapshot <= Protocol.SnapshotBudget, $"{lab.Server.LargestSnapshot}");
            report.Note($"{name}: {kills} kills, {peakGhosts} ghosts at peak");
            report.Note($"  hero: {corrections} corrections in {compared} checks ({100.0 * corrections / Math.Max(compared, 1):0.00}%), worst {worst:0.00} m; commands {late / ticks * 100:0.00}% late, {filled / ticks * 100:0.00}% filled in");
            report.Note($"  others shown within {median:0.000} m of the truth (median), {p99:0.00} m (99th percentile), worst {lab.Players.Max(p => p.DivergenceWorst):0.00} m");
            report.Note($"  {kbPerSecond:0.0} KB/s down, {kbUp:0.0} KB/s up per player; largest snapshot {lab.Server.LargestSnapshot} bytes; round trip {lab.Players.Average(p => p.Client.RoundTrip):0} ms");
        }

        private static bool HeroesMatch(HeroState a, HeroState b)
        {
            return a.Level == b.Level && a.Experience == b.Experience && a.Gold == b.Gold && a.Flasks == b.Flasks
                && a.Inventory.Count == b.Inventory.Count && a.Equipped.Count == b.Equipped.Count && a.WorldsCleared == b.WorldsCleared;
        }

        // A host on fibre and a friend on bad wifi: one player's line mustn't become everyone's problem.
        private static void MixedParty(Report report, WorldCache worlds, WorldRequest request)
        {
            using var lab = new GameLab(worlds, request);
            LabPlayer host = lab.AddPlayer("Host", new NetworkConditions(), 1, frameRate: 144.0);
            LabPlayer good = lab.AddPlayer("Good line", new NetworkConditions { Latency = 30, Jitter = 5 }, 2, frameRate: 60.0);
            LabPlayer slowFrames = lab.AddPlayer("Old laptop", new NetworkConditions { Latency = 50, Jitter = 10 }, 3, frameRate: 24.0);
            LabPlayer bad = lab.AddPlayer("Bad wifi", Scenarios.All[4].conditions, 4, frameRate: 60.0);
            LabPlayer awful = lab.AddPlayer("Awful", Scenarios.All[5].conditions, 5, frameRate: 30.0);

            lab.Run(8);
            var settled = lab.Players.ToDictionary(p => p, p => p.Client.Predictor.Corrections);
            lab.Run(45);
            foreach (LabPlayer player in lab.Players) player.Fights = false;
            lab.Run(10);

            int Corrections(LabPlayer player) => player.Client.Predictor.Corrections - settled[player];
            report.Check("Someone on a clean line isn't corrected because a friend's line is bad", Corrections(host) == 0, $"{Corrections(host)}");
            report.Check("Nor someone on a merely decent one", Corrections(good) == 0, $"{Corrections(good)}");
            report.Check("24 frames a second predicts as cleanly as 60", Corrections(slowFrames) == 0, $"{Corrections(slowFrames)}");
            foreach (LabPlayer player in lab.Players)
            {
                report.Note($"{player.Name}: {player.Client.Predictor.Corrections} corrections of {player.Client.Predictor.Compared}, worst {player.Client.Predictor.WorstCorrection:0.00} m, {player.Client.RoundTrip:0} ms, {player.Client.BytesReceived / lab.SecondsRun / 1024:0.0} KB/s");
            }
        }

        // A screen full of everything at once: this is what a packet budget and a priority order are for.
        private static void Horde(Report report, WorldCache worlds, WorldRequest request, NetworkConditions conditions, string name)
        {
            const int Enemies = 300;
            const int ArrowsPerSecond = 40;

            using var lab = new GameLab(worlds, request);
            for (int i = 0; i < Protocol.MaxPlayers; i++) lab.AddPlayer($"Hero {i + 1}", conditions, 200 + i, hero: Unkillable());
            lab.Run(6);

            GameServer server = lab.Server;
            Vector2 centre = server.Heroes[0].Sim.Position;
            var random = new Random(9);
            for (int i = 0; i < Enemies; i++)
            {
                Vector2 at = centre + new Vector2(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f) * 90f;
                server.Ecs.CreateEntity(Bodies.Walking(server, "skeleton", new Vector3(at.X, 0f, at.Y), 1f, 1.5f,
                    new EnemyComponent { Name = "Restless Dead", Level = 1 },
                    new EnemyBrainComponent { SiteIndex = 0, SpawnIndex = -1, Home = at, AggroRadius = 200f, LeashRadius = 500f, MoveSpeed = 6f, AttackRange = 4.6f, WindUp = 0.5f, Damage = 0.1f, Experience = 0 },
                    new HealthComponent(100000f), new SwingComponent()));
            }

            int ticks = 0;
            int arrowsLoosed = 0;
            lab.TickCostTotal = 0.0;
            lab.TickCostWorst = 0.0;
            lab.TicksTimed = 0;
            long bytesBefore = lab.Players.Sum(p => p.Client.BytesReceived);

            lab.Run(30, () =>
            {
                if (++ticks % (NetTime.TicksPerSecond / ArrowsPerSecond + 1) != 0) return;
                Vector2 from = centre + new Vector2(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f) * 80f;
                Vector2 direction = new Vector2(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f).Normalized();
                server.Ecs.CreateEntity(
                    new NetIdentityComponent(server.Ghosts.NextId(), 2f),
                    new PositionComponent(new Vector3(from.X, 8f, from.Y)),
                    new RotationComponent(new Vector3(0f)),
                    new VisualComponent("arrow", VisualKind.Prop),
                    new BallisticComponent { Origin = new Vector3(from.X, 8f, from.Y), Velocity = new Vector3(direction.X, 0f, direction.Y) * 42f, StartTick = server.Tick },
                    new ProjectileComponent { Damage = 0.1f, Life = 1.6f });
                arrowsLoosed++;
            });

            double perPlayer = (lab.Players.Sum(p => p.Client.BytesReceived) - bytesBefore) / (double)lab.Players.Count / 30.0 / 1024.0;
            int peakSeen = lab.Players.Max(p => p.Client.Ghosts.Ghosts.Count);

            foreach (LabPlayer player in lab.Players) player.Fights = false;
            lab.Run(10);

            report.Check($"{name}: {Enemies} enemies and {arrowsLoosed} arrows, and a snapshot never outgrows a packet", server.LargestSnapshot <= Protocol.SnapshotBudget, $"{server.LargestSnapshot}");
            report.Check($"{name}: no client ever lost its footing", lab.Players.All(p => p.Client.Ghosts.Mismatches == 0 && p.DisconnectReason == null));
            report.Check($"{name}: each sees the crowd", peakSeen > Enemies / 2, $"{peakSeen} ghosts");
            report.Note($"{name}: {perPlayer:0.0} KB/s down per player, {perPlayer * lab.Players.Count * 8 / 1024:0.00} Mbit/s of the host's upload for four friends plus itself");
            report.Check($"{name}: the server keeps up, with time to spare", lab.TickCostTotal / lab.TicksTimed < 1000.0 / NetTime.TicksPerSecond * 0.5, $"{lab.TickCostTotal / lab.TicksTimed:0.00} ms a tick");
            report.Note($"  a server tick cost {lab.TickCostTotal / lab.TicksTimed:0.00} ms on average, worst {lab.TickCostWorst:0.0} ms, of the {1000.0 / NetTime.TicksPerSecond:0.0} ms it has");
        }

        // Nothing here is about the fight going well, and a fallen hero doesn't look at the horde.
        private static HeroState Unkillable()
        {
            HeroState hero = StrongHeroForHorde();
            return hero;
        }

        private static HeroState StrongHeroForHorde()
        {
            HeroState hero = GameLab.StrongHero();
            hero.Equipped[ItemSlot.Amulet] = new Item { Name = "Test Charm", Slot = ItemSlot.Amulet, Level = 1, Modifiers = { new Modifier { Stat = Stat.Health, Value = 1000000f } } };
            return hero;
        }

        private static void JoiningAndLeaving(Report report, WorldCache worlds, WorldRequest request)
        {
            using var lab = new GameLab(worlds, request);
            var line = new NetworkConditions { Latency = 60, Jitter = 10, LossChance = 0.01 };
            LabPlayer first = lab.AddPlayer("First", line, 1);
            lab.Run(20);
            int killsBefore = lab.Server.Session!.Sites.Sum(s => s.Dead.Count);

            LabPlayer late = lab.AddPlayer("Latecomer", line, 2);
            lab.Run(5);
            report.Check("Someone can join a game in progress", late.Client.State == ClientState.Playing);
            report.Check("They arrive beside their friend", (late.Client.Predictor.Current.Position - first.Client.Predictor.Current.Position).Length < 60f);
            report.Check("They hear what has already happened to the world", late.Client.Session!.Sites.Sum(s => s.Dead.Count) >= killsBefore, $"{late.Client.Session.Sites.Sum(s => s.Dead.Count)} of {killsBefore}");
            report.Check("Each sees the other", first.Client.Ghosts.Ghosts.Count > 0 && first.Client.Party.Count == 2 && late.Client.Party.Count == 2);

            int heroesSeen = CountHeroes(first);
            report.Check("Two heroes in the first client's world", heroesSeen == 2, $"{heroesSeen}");

            late.Client.Dispose();
            lab.Players.Remove(late);
            lab.Run(3);
            report.Check("When someone leaves, their hero goes from everyone's world", CountHeroes(first) == 1, $"{CountHeroes(first)}");
            report.Check("Everyone else plays on", first.Client.State == ClientState.Playing && lab.Server.Heroes.Count == 1);
        }

        private static int CountHeroes(LabPlayer player)
        {
            int count = 0;
            foreach (Archetype archetype in player.Ecs.QueryArchetypes(new BitMask(typeof(HeroComponent)), BitMask.Empty)) count += archetype.EntityCount;
            return count;
        }

        private static void PartyTravels(Report report, WorldCache worlds, WorldRequest request)
        {
            using var lab = new GameLab(worlds, request);
            var line = new NetworkConditions { Latency = 80, Jitter = 20, LossChance = 0.02 };
            for (int i = 0; i < 3; i++) lab.AddPlayer($"Hero {i + 1}", line, 10 + i);
            lab.Run(8);

            int goldBefore = lab.Players[0].Client.Hero.Gold;
            var next = new WorldRequest(request.Seed + 1, "isle", 2);
            lab.Players[1].Client.StepThroughWaygate(next);

            // A new world takes real time to grow, however fast this clock runs.
            for (int i = 0; i < 600 && lab.Players.Any(p => p.Arrivals < 2); i++)
            {
                lab.Run(0.1);
                if (lab.Server.Session == null) Thread.Sleep(10);
            }

            report.Check("One hero stepping through the waygate brings the party", lab.Players.All(p => p.Arrivals == 2), string.Join(",", lab.Players.Select(p => p.Arrivals)));
            lab.Run(6);
            report.Check("They all play on in the new world", lab.Players.All(p => p.Client.State == ClientState.Playing && p.Client.Session!.World.Plan.Seed == next.Seed));
            report.Check("Nothing from the old world came along", lab.Players.All(p => p.Client.Ghosts.Mismatches == 0));
            report.Check("What they carried came with them", lab.Players[0].Client.Hero.Gold >= goldBefore);
        }

        private static void TooManyHeroes(Report report, WorldCache worlds, WorldRequest request)
        {
            using var lab = new GameLab(worlds, request);
            for (int i = 0; i < Protocol.MaxPlayers + 1; i++) lab.AddPlayer($"Hero {i + 1}", new NetworkConditions(), 20 + i);
            lab.Run(4);
            LabPlayer sixth = lab.Players[^1];
            report.Check($"A sixth hero is turned away, and told why", sixth.DisconnectReason?.Contains("full") == true, sixth.DisconnectReason ?? "no reason");
            report.Check("The five already there don't notice", lab.Players.Take(Protocol.MaxPlayers).All(p => p.Client.State == ClientState.Playing));
        }

        private static void WrongVersion(Report report, WorldCache worlds, WorldRequest request)
        {
            using var lab = new GameLab(worlds, request);
            (LoopbackTransport serverEnd, LoopbackTransport clientEnd) = LoopbackTransport.CreatePair();
            lab.Server.AddTransport(serverEnd);
            var listener = new RawClient();
            clientEnd.Poll(listener);

            BitWriter hello = Protocol.Begin(Message.Hello);
            hello.WriteUInt(Protocol.Version);
            hello.WriteUInt(NetSchema.Hash ^ 1u);
            hello.WriteString("Stranger");
            Protocol.WriteHero(hello, HeroState.CreateNew());
            listener.Connection!.Send(hello.ToArray(), Delivery.Reliable);

            lab.Run(0.5);
            clientEnd.Poll(listener);
            report.Check("A different build is told so, in words, rather than reading rows wrongly", listener.Closed?.Contains("versions") == true, listener.Closed ?? "not closed");
        }

        private sealed class RawClient : INetListener
        {
            public INetConnection? Connection;
            public string? Closed;
            public void OnConnected(INetConnection connection) => Connection = connection;
            public void OnMessage(INetConnection connection, byte[] data, Delivery delivery) { }
            public void OnDisconnected(INetConnection connection, string reason) => Closed = reason;
        }

        private static void ServerStalls(Report report, WorldCache worlds, WorldRequest request)
        {
            using var lab = new GameLab(worlds, request);
            var line = new NetworkConditions { Latency = 50, Jitter = 10 };
            for (int i = 0; i < 3; i++) lab.AddPlayer($"Hero {i + 1}", line, 30 + i);
            lab.Run(10);

            // The host's machine hiccups for a second, again and again. Clients keep running ahead of a server that isn't there.
            for (int i = 0; i < 4; i++)
            {
                lab.StallServer(1.0);
                lab.Run(6);
            }
            foreach (LabPlayer player in lab.Players) player.Fights = false;
            lab.Run(8);

            bool together = true;
            foreach (LabPlayer player in lab.Players)
            {
                ServerPlayer truth = lab.Server.Players.First(p => p.Name == player.Name);
                together &= (player.Client.Predictor.Current.Position - truth.Sim.Position).Length < 0.01f;
            }
            report.Check("After the server stalls for a second, four times over, every hero is back in step", together);
            report.Check("Nobody was dropped", lab.Players.All(p => p.DisconnectReason == null));
            report.Check("And no snapshot was unreadable", lab.Players.All(p => p.Client.Ghosts.Mismatches == 0));
        }

        private static void ConnectionDies(Report report, WorldCache worlds, WorldRequest request)
        {
            var lab = new GameLab(worlds, request);
            LabPlayer player = lab.AddPlayer("Hero", new NetworkConditions { Latency = 40 }, 40);
            lab.Run(5);
            lab.Dispose();
            lab.Run(1);
            report.Check("When the host goes, a client hears about it and why", player.Client.State == ClientState.Disconnected && player.DisconnectReason != null, player.DisconnectReason ?? "");
        }

        // Someone sends rubbish at the server. Whatever it does to them, nobody else should know it happened.
        private static void HostileClient(Report report, WorldCache worlds, WorldRequest request, int rounds)
        {
            using var lab = new GameLab(worlds, request);
            LabPlayer honest = lab.AddPlayer("Honest", new NetworkConditions(), 50);
            lab.Run(3);

            var random = new Random(50);
            string? crash = null;
            int attackers = 0;
            for (int round = 0; round < rounds && crash == null; round++)
            {
                (LoopbackTransport serverEnd, LoopbackTransport clientEnd) = LoopbackTransport.CreatePair();
                lab.Server.AddTransport(serverEnd);
                var attacker = new RawClient();
                clientEnd.Poll(attacker);
                attackers++;

                try
                {
                    lab.Server.Step();
                    if (attacker.Connection == null) clientEnd.Poll(attacker);

                    // Some claim to be a real client first, so that the rubbish reaches the deeper handlers.
                    if (random.Next(2) == 0 && attacker.Connection != null)
                    {
                        BitWriter hello = Protocol.Begin(Message.Hello);
                        hello.WriteUInt(Protocol.Version);
                        hello.WriteUInt(NetSchema.Hash);
                        hello.WriteString("Rubbish");
                        Protocol.WriteHero(hello, HeroState.CreateNew());
                        attacker.Connection.Send(hello.ToArray(), Delivery.Reliable);
                    }

                    for (int i = 0; i < 6 && attacker.Connection != null; i++)
                    {
                        byte[] garbage = new byte[random.Next(0, 200)];
                        random.NextBytes(garbage);
                        if (garbage.Length > 0 && random.Next(3) != 0) garbage[0] = (byte)random.Next(0, 16);
                        attacker.Connection.Send(garbage, random.Next(2) == 0 ? Delivery.Reliable : Delivery.Unreliable);
                    }
                    lab.Server.Step();
                }
                catch (Exception e)
                {
                    crash = e.ToString();
                }
                clientEnd.Dispose();
                lab.Server.Step();
                if (lab.Server.PlayerCount > 3) lab.Server.Step();
            }

            report.Check($"{attackers} clients sending rubbish: the server never falls over", crash == null, crash ?? "");
            lab.Run(3);
            report.Check("And the honest player never noticed", honest.Client.State == ClientState.Playing && honest.DisconnectReason == null);
        }

        // A press is shown the moment it's made. Lost, the hero did something the server never heard of. Repeated, the hero does it
        // twice. Whatever the line does, one press has to be one action.
        private static void PressesHappenOnce(Report report, WorldCache worlds, WorldRequest request, string name, NetworkConditions conditions)
        {
            using var lab = new GameLab(worlds, request);
            LabPlayer player = lab.AddPlayer("Hero", conditions, 80);
            player.Fights = false;
            lab.Run(6);

            ServerPlayer truth = lab.Server.Players.First();
            player.WhirlEveryFrames = (int)(HeroRules.WhirlCooldown * 60.0) + 90;

            int happened = 0;
            float last = 0f;
            lab.Run(120, () =>
            {
                float cooldown = truth.Sim.WhirlCooldown;
                if (cooldown > last + 1f) happened++;
                last = cooldown;
            });
            player.WhirlEveryFrames = 0;
            lab.Run(3, () =>
            {
                float cooldown = truth.Sim.WhirlCooldown;
                if (cooldown > last + 1f) happened++;
                last = cooldown;
            });

            report.Check($"{name}: every press of a button happens exactly once", happened == player.WhirlsPressed, $"{player.WhirlsPressed} pressed, {happened} happened");
        }

        // Every packet repeats the commands before it, so one that missed its tick arrives again and again. Whirl and dash have
        // cooldowns that would hide a press happening twice. A flask has none: it would simply drink two.
        private static void LatePressIsNotRepeated(Report report)
        {
            var player = new ServerPlayer(1, null!);
            var press = new HeroCommand { Tick = 100, Buttons = HeroButtons.Flask };

            for (int i = 0; i < 12; i++) player.Receive(press, 110);
            bool first = player.Take(110).Has(HeroButtons.Flask);

            for (int i = 0; i < 12; i++) player.Receive(press, 111);
            bool second = player.Take(111).Has(HeroButtons.Flask);
            bool third = player.Take(112).Has(HeroButtons.Flask);

            report.Check("A press that came late still happens", first);
            report.Check("Repeats of it in later packets don't happen again", !second && !third);
        }

        private static void ActionsAtPanels(Report report, WorldCache worlds, WorldRequest request)
        {
            using var lab = new GameLab(worlds, request, startSite: "Landing");
            HeroState hero = LabHero();
            LabPlayer player = lab.AddPlayer("Hero", new NetworkConditions { Latency = 60, Jitter = 10 }, 60, hero: hero);
            player.Fights = false;
            lab.Run(4);
            ServerPlayer truth = lab.Server.Players.First();

            int might = truth.Hero.Might;
            player.Client.RaiseAttribute(HeroAttribute.Might);
            lab.Run(0.5);
            report.Check("Spending a point reaches the server and comes back", truth.Hero.Might == might + 1 && player.Client.Hero.Might == might + 1 && player.Client.Hero.UnspentPoints == 0);

            player.Client.RaiseAttribute(HeroAttribute.Might);
            lab.Run(0.5);
            report.Check("A point that isn't there can't be spent", truth.Hero.Might == might + 1);

            int carried = truth.Hero.Inventory.Count;
            player.Client.Equip(0);
            lab.Run(0.5);
            report.Check("Equipping moves the item, on both ends", truth.Hero.Equipped.ContainsKey(ItemSlot.Ring) && player.Client.Hero.Equipped.ContainsKey(ItemSlot.Ring) && truth.Hero.Inventory.Count == carried - 1);

            int gold = truth.Hero.Gold;
            player.Client.Sell(0);
            lab.Run(0.5);
            report.Check("Selling pays, on both ends", truth.Hero.Gold > gold && player.Client.Hero.Gold == truth.Hero.Gold);

            player.Client.Equip(999);
            player.Client.Sell(-5);
            player.Client.Buy(999, 0);
            player.Client.TravelToSite(99999);
            lab.Run(0.5);
            report.Check("Nonsense requests are ignored", player.Client.State == ClientState.Playing);

            Vector2 far = lab.Server.Session!.World.Sites[^1].Position;
            player.Client.TravelTo(far);
            lab.Run(1);
            report.Check("Being moved by the server is believed at once", (player.Client.Predictor.Current.Position - far).Length < 0.1f);
        }

        // Merchants, chests and shrines, all the way through: the hero walks up, presses use, the server decides, and the answer comes back.
        private static void UsingThings(Report report, WorldCache worlds)
        {
            var request = new WorldRequest(303, "isle");
            using var lab = new GameLab(worlds, request, startSite: "Landing");
            HeroState hero = LabHero();
            hero.Gold = 1000000;
            LabPlayer player = lab.AddPlayer("Hero", new NetworkConditions { Latency = 60, Jitter = 10 }, 90, hero: hero);
            player.Fights = false;
            lab.Run(4);
            ServerSession session = lab.Server.Session!;
            ServerPlayer truth = lab.Server.Players.First();

            Vector2? Find(InteractKind kind)
            {
                foreach (Archetype archetype in player.Ecs.QueryArchetypes(new BitMask(typeof(InteractableComponent), typeof(PositionComponent)), BitMask.Empty))
                {
                    foreach (EntityView entity in new ComponentAccessor(archetype))
                    {
                        if (entity.Get<InteractableComponent>().Kind == kind) return entity.Get<PositionComponent>().value.Xz;
                    }
                }
                return null;
            }

            void Use(Vector2 where)
            {
                player.Client.TravelTo(where);
                lab.Run(1);
                player.PressUse = true;
                lab.Run(1);
            }

            Vector2? merchant = Find(InteractKind.Merchant);
            report.Check("A merchant reaches the client as a ghost", merchant != null);
            if (merchant != null)
            {
                Use(merchant.Value);
                bool opened = player.PanelsOpened.Any(p => p.kind == InteractKind.Merchant);
                report.Check("Using a merchant opens their table", opened);
                if (opened)
                {
                    SiteState market = player.PanelsOpened.First(p => p.kind == InteractKind.Merchant).site;
                    int siteIndex = player.Client.Session!.Sites.IndexOf(market);
                    int stock = market.Stock.Count;
                    report.Check("And the server sends what's on it", stock > 0, $"{stock}");

                    int gold = truth.Hero.Gold;
                    int carried = truth.Hero.Inventory.Count;
                    Item wanted = market.Stock[0];
                    player.Client.Buy(siteIndex, 0);
                    lab.Run(1);
                    report.Check("Buying takes the gold, gives the item and clears it from the table", truth.Hero.Gold == gold - wanted.BuyValue && truth.Hero.Inventory.Count == carried + 1 && market.Stock.Count == stock - 1, $"gold {gold}->{truth.Hero.Gold}, table {stock}->{market.Stock.Count}");
                    report.Check("The hero the client holds shows it", player.Client.Hero.Gold == truth.Hero.Gold && player.Client.Hero.Inventory.Count == truth.Hero.Inventory.Count);
                }
            }

            Vector2? gate = Find(InteractKind.Waygate);
            if (gate != null)
            {
                Use(gate.Value);
                report.Check("Using a waygate offers the way", player.PanelsOpened.Any(p => p.kind == InteractKind.Waygate));
            }

            // A shrine and a chest are somewhere else in the world, and exist only once a hero is near.
            WorldSite shrine = session.World.Sites.First(s => s.Kind == SiteKind.Shrine);
            player.Client.TravelTo(shrine.Position);
            lab.Run(3);
            Vector2? altar = Find(InteractKind.Shrine);
            report.Check("Going to a site wakes it, and its shrine arrives", altar != null);
            if (altar != null)
            {
                int index = session.Sites.FindIndex(s => s.Site == shrine);
                Use(altar.Value);
                report.Check("Kindling a shrine lights it for the server", session.Sites[index].Kindled);
                lab.Run(1);
                report.Check("And for the client", player.Client.Session!.Sites[index].Kindled);
                report.Check("And that's where the hero wakes", (truth.Respawn - altar.Value).Length < 0.5f);
                report.Check("And the chronicle moves on, for both", session.Chronicle.Objectives.Any(o => o.Progress > 0) && player.Client.Session.Chronicle.Objectives.Any(o => o.Progress > 0));

                player.Client.TravelTo(session.World.Sites[0].Position);
                lab.Run(1);
                player.Client.TravelToSite(index);
                lab.Run(1);
                report.Check("A kindled shrine can be travelled to", (player.Client.Predictor.Current.Position - shrine.Position).Length < 1f);
            }

            foreach (SiteState state in session.Sites)
            {
                int spawn = state.Site.Spawns.FindIndex(s => s.Role == SpawnRole.Chest);
                if (spawn < 0 || state.Site.Kind == SiteKind.Keep) continue;

                player.Client.TravelTo(state.Site.Spawns[spawn].Position);
                lab.Run(3);
                Vector2? chest = Find(InteractKind.Chest);
                if (chest == null) continue;

                int siteIndex = session.Sites.IndexOf(state);
                int carried = truth.Hero.Inventory.Count;
                int goldBefore = truth.Hero.Gold;
                Use(chest.Value);
                report.Check("Opening a chest leaves it open", state.Opened.Count > 0);
                lab.Run(3);
                report.Check("And what's in it can be collected", truth.Hero.Gold > goldBefore || truth.Hero.Inventory.Count > carried, $"gold {goldBefore}->{truth.Hero.Gold}, items {carried}->{truth.Hero.Inventory.Count}");
                report.Check("The chest has gone from the client's world", Find(InteractKind.Chest) == null || (Find(InteractKind.Chest)!.Value - chest.Value).Length > 0.1f);
                break;
            }
        }

        private static HeroState LabHero()
        {
            HeroState hero = HeroState.CreateNew();
            hero.UnspentPoints = 1;
            hero.Gold = 500;
            hero.Inventory.Add(new Item { Name = "A Ring", Slot = ItemSlot.Ring, Level = 3 });
            hero.Inventory.Add(new Item { Name = "Spare Boots", Slot = ItemSlot.Feet, Level = 3, Armour = 2f });
            return hero;
        }

        // The promise that matters most for later: something added to the game reaches every client with no networking written for it.
        private static void NewComponentNeedsNoCode(Report report, WorldCache worlds, WorldRequest request)
        {
            using var lab = new GameLab(worlds, request);
            LabPlayer player = lab.AddPlayer("Hero", new NetworkConditions { Latency = 60, Jitter = 10, LossChance = 0.02 }, 70);
            lab.Run(4);

            Entity? victim = null;
            foreach (Archetype archetype in lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(EnemyComponent)), BitMask.Empty))
            {
                if (archetype.EntityCount > 0) victim = archetype.entities[0];
            }
            report.Check("There's an enemy to try it on", victim != null);
            if (victim == null) return;

            uint ghostId = lab.Server.Ecs.GetComponent<NetIdentityComponent>(victim.Value).Id;
            lab.Server.Ecs.AddComponentToEntity(victim.Value, new LabChilledComponent { Strength = 0.5f, Source = "A frost spell nobody wrote netcode for" });
            lab.Run(1.5);

            Ghost? ghost = player.Client.Ghosts.Ghosts.GetValueOrDefault(ghostId);
            bool arrived = ghost != null && player.Ecs.HasComponent<LabChilledComponent>(ghost.Entity);
            report.Check("A brand new component appears on clients, on an entity already there", arrived);
            if (!arrived) return;

            var chilled = player.Ecs.GetComponent<LabChilledComponent>(ghost!.Entity);
            report.Check("With its values", MathF.Abs(chilled.Strength - 0.5f) < 0.06f && chilled.Source == "A frost spell nobody wrote netcode for", $"{chilled.Strength} '{chilled.Source}'");

            lab.Server.Ecs.RemoveComponentFromEntity(victim.Value, typeof(LabChilledComponent));
            lab.Run(1.5);
            report.Check("And goes when it wears off", !player.Ecs.HasComponent<LabChilledComponent>(ghost.Entity));
        }
    }
}
