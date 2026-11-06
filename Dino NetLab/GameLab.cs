using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Skills;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
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
        public Vector2? Heading;        // With nothing to fight: where to walk to, in place of wandering.
        public Vector2? StrikeAt;       // Swings once at this place.
        public const int WhirlSlot = 2;
        public int WhirlEveryFrames;    // Instead of fighting: a press of whirl, this often.
        public int WhirlsPressed;
        public bool PressUse;           // Presses use, once.
        public int HeldSlots;           // Instead of fighting: the keys on these slots held down.
        public Func<HeroIntent>? Script;    // Instead of everything: exactly what's pressed, frame by frame.
        private int _heldLastFrame;
        public readonly int[] Uses = new int[HeroState.SkillSlots];
        public readonly List<(InteractKind kind, SiteState site)> PanelsOpened = new();
        private int _frames;
        public double DivergenceTotal;
        public double DivergenceWorst;
        public long DivergenceSamples;
        public readonly List<float> DivergenceSorted = new();
        public double DrawnBehindTotal;     // Seconds behind the server itself, as measured there.
        public long DrawnBehindSamples;
        public readonly List<double> FirstBlowsShownAfter = new();
        private readonly ImpairedTransport? _line;
        private double _stalled;
        private double _lastLanded = double.NegativeInfinity;
        private double _firstLanded = -1.0;

        public LabPlayer(string name, INetTransport transport, WorldCache worlds, HeroState hero, double frameSeconds, int seed)
        {
            Name = name;
            _worlds = worlds;
            _random = new Random(seed);
            FrameSeconds = frameSeconds;
            _line = transport as ImpairedTransport;
            Client = new GameClient(transport, this, Ecs, hero, name);
            Client.Events.On<DamageEvent>(e =>
            {
                if (!e.ToHero) HitsSeen++;

                // What someone sees when they start on an enemy: the time from their swing to the number coming up.
                if (e.ToHero || e.Note != HitNote.None || _firstLanded < 0.0) return;
                FirstBlowsShownAfter.Add(Client.Clock() / 1000.0 - _firstLanded);
                _firstLanded = -1.0;
            });
        }

        /// <summary>Holds up this client's next frame: nothing is heard or done until it ends, and it then lasts that long, as a stalled frame does.</summary>
        public void Stall(double seconds)
        {
            _nextFrame += seconds;
            _stalled += seconds;
        }

        /// <summary>Runs a frame if one is due.</summary>
        public void Advance(double now)
        {
            if (_stalled > 0.0) _line?.Carry();

            while (_nextFrame <= now)
            {
                _nextFrame += FrameSeconds * (0.9 + _random.NextDouble() * 0.2);
                Client.Update(FrameSeconds + _stalled);
                _stalled = 0.0;
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
            if (Script != null) return Script();

            if (Client.Session is { Fetch.Active: true } fetching)
            {
                var fetch = new HeroIntent();
                fetching.Fetch.Steer(fetching, ref fetch);
                return fetch;
            }

            if (StrikeAt is Vector2 target && Client.Session != null)
            {
                StrikeAt = null;
                var strike = new HeroIntent { Aim = target - Client.Predictor.Current.Position };
                strike.Use(0);
                return strike;
            }

            if (PressUse)
            {
                PressUse = false;
                return new HeroIntent { Interact = true };
            }

            if (HeldSlots != 0 && Client.Session != null)
            {
                var held = new HeroIntent();
                for (int slot = 2; slot < HeroState.SkillSlots; slot++)
                {
                    bool down = (HeldSlots & 1 << slot) != 0;
                    bool justPressed = down && (_heldLastFrame & 1 << slot) == 0;
                    if (HeroRules.KeyPresses(Client.Session.Sim, slot, down, justPressed)) held.Use(slot);
                }
                _heldLastFrame = HeldSlots;
                return held;
            }
            _heldLastFrame = 0;

            if (WhirlEveryFrames > 0 && Client.Session != null)
            {
                if (++_frames % WhirlEveryFrames != 0) return default;
                WhirlsPressed++;
                var press = new HeroIntent();
                press.Use(WhirlSlot);
                return press;
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
                    var enemy = entity.Get<EnemyComponent>();
                    if (enemy.State == EnemyState.Dead || enemy.Friendly) continue;
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

            if (best < (Heading != null ? 16f : 90f))
            {
                Vector2 toward = nearest - here;
                intent.Aim = toward;
                intent.Move = best > 4.5f ? toward : _wander;
                if (best < 6f)
                {
                    intent.Use(0);
                    Swings++;
                }
                if (best < 7f && _random.NextDouble() < 0.02) intent.Use(WhirlSlot);
                intent.Dash = best is > 10f and < 18f && _random.NextDouble() < 0.05;

                // Spells too, as what they do to mana and cooldowns has to be predicted like everything else.
                for (int slot = 1; slot < HeroState.SkillSlots; slot++)
                {
                    if (slot != WhirlSlot && Client.Kit.Slots[slot] != null && best < 24f && _random.NextDouble() < 0.03) intent.Use(slot);
                }
            }
            else
            {
                Vector2 way = Heading is Vector2 heading ? heading - here : _wander;
                intent.Aim = way;
                intent.Move = way;
                intent.Dash = _random.NextDouble() < 0.01;
            }
            intent.Flask = _random.NextDouble() < 0.004;
            intent.Interact = _random.NextDouble() < 0.01;
            return intent;
        }

        public void WorldNeeded(WorldRequest request, Offering offering) => _wanted = request;
        public void Arrived(Vector2 position) => Arrivals++;
        public void HeroActed(HeroStepEvents events, in HeroSimState hero)
        {
            if ((events & HeroStepEvents.Used) != 0 && hero.Using >= 0) Uses[hero.Using]++;
            if ((events & HeroStepEvents.Landed) != 0)
            {
                double now = Client.Clock() / 1000.0;
                if (now - _lastLanded > 1.0) _firstLanded = now;
                _lastLanded = now;
            }
        }
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

        public GameLab(WorldCache worlds, WorldRequest request, string startSite = "BanditCamp", Offering? offering = null, SaveGame? save = null)
        {
            _worlds = worlds;

            // Generation takes real time, and this clock doesn't wait for it.
            worlds.Get((offering ?? new Offering()).Shape(request)).Wait();
            Server = new GameServer(new ServerSettings { Request = request, Offering = offering, Save = save, StartSite = startSite, AllowTeleport = true }, worlds);
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
            hero.Equipped[WornSlot.MainHand].WeaponDamage = 34f;
            hero.Equipped[WornSlot.Neck] = new Item { Name = "Lab Charm", Slot = ItemSlot.Neck, Level = 12, Lines = { new ItemLine("health", 400f) } };
            Learn(hero, "pyre.firebolt", "forge.whirl", "winter.frost-nova", "storm.spark");
            return hero;
        }

        /// <summary>Walks a hero to these nodes the way a player would, and puts what they teach on the bar.</summary>
        public static void Learn(HeroState hero, params string[] nodes)
        {
            foreach (string id in nodes)
            {
                foreach (int step in TalentTree.PathTo(hero, TalentTree.Find(id)!.Index))
                {
                    TalentNode node = TalentTree.Nodes[step];
                    hero.Talents.Add(node.Id);
                    foreach (Mod mod in node.Mods)
                    {
                        if (mod.Kind == ModKind.GrantSkill) HeroBuild.OfferToBar(hero, SkillCatalog.Get(mod.Skill));
                    }
                }
            }
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

        /// <summary>How long ago, by the server's own clock, the moment this player is looking at was.</summary>
        public double DrawnBehind(LabPlayer player) => (Server.Tick - player.Client.Render.RenderTick) * NetTime.TickSeconds;

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
            player.DrawnBehindTotal += DrawnBehind(player);
            player.DrawnBehindSamples++;

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
            foreach ((string name, NetworkConditions conditions) in Scenarios.All)
            {
                ThePictureKeepsUp(report, worlds, request, name, conditions);
            }
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
            ClickingToUse(report, worlds);
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

            foreach (LabPlayer player in lab.Players) (player.DrawnBehindTotal, player.DrawnBehindSamples) = (0.0, 0);

            int peakGhosts = 0;
            lab.Run(seconds, () => peakGhosts = Math.Max(peakGhosts, lab.Server.Ghosts.GhostCount));

            double drawnBehind = lab.Players.Max(p => p.DrawnBehindTotal / Math.Max(p.DrawnBehindSamples, 1));
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
            report.Check($"{name}: nobody sees the fight further behind than the line explains", drawnBehind < AllowedBehind(conditions), $"{drawnBehind * 1000.0:0} ms at worst, of {AllowedBehind(conditions) * 1000.0:0}");
            if (cleanLine) report.Check($"{name}: a hero is almost never corrected", corrections <= compared / 500, $"{corrections} of {compared}");
            report.Check($"{name}: no snapshot ever referred to state a client lacked", mismatches == 0, $"{mismatches}");
            report.Check($"{name}: every hero ends exactly where the server has them", positionsAgree);
            report.Check($"{name}: every client's copy of its hero matches the server's", heroesAgree);
            report.Check($"{name}: every hero comes to exactly the same numbers on both ends", lab.Players.All(p => SheetsAgree(lab.Server.Players.First(s => s.Name == p.Name), p)));
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
                && a.Inventory.Count == b.Inventory.Count && a.Equipped.Count == b.Equipped.Count && a.WorldsCleared == b.WorldsCleared
                && a.Talents.SequenceEqual(b.Talents) && a.Bar.SequenceEqual(b.Bar);
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

            // All three hundred are one loadout, which is the point. They can't be killed in the time, or do much.
            var kind = new EnemyKind { Id = "lab-horde", Name = "Restless Dead", Skills = new[] { SkillCatalog.Strike, SkillCatalog.Shot }, Weapon = Tags.Bow, Health = 2000f, Damage = 0.01f, MoveSpeed = 6f };
            Loadout horde = server.Session!.Bestiary.Shared(kind, 1, EnemyRank.Normal, Bestiary.NoTrait);
            for (int i = 0; i < Enemies; i++)
            {
                Vector2 at = centre + new Vector2(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f) * 90f;
                var actor = new Actor(Team.Enemies, horde, at, 1.1f) { Body = "skeleton", Site = server.Session.Sites[0] };
                actor.Entity = server.Ecs.CreateEntity(Bodies.Walking(server, "skeleton", new Vector3(at.X, 0f, at.Y), 1f, 1.5f,
                    new EnemyComponent { Name = "Restless Dead", Level = 1 },
                    new EnemyBrainComponent { SiteIndex = 0, SpawnIndex = -1, Home = at, AggroRadius = 200f, LeashRadius = 500f, Experience = 0 },
                    new HealthComponent(actor.Sheet.MaxHealth), new ActionComponent(), new AfflictedComponent(), new ActorComponent(actor)));
            }

            var archer = new Actor(Team.Enemies, horde, centre, 1.1f) { Body = "skeleton" };
            SkillProfile shot = archer.Sheet.Profile(SkillCatalog.Shot)!;

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
                    new ProjectileComponent { State = new Projectile { Source = archer, Skill = shot, Life = 1.6f, Start = from } });
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
            hero.Equipped[WornSlot.Neck] = new Item { Name = "Test Charm", Slot = ItemSlot.Neck, Level = 1, Lines = { new ItemLine("health", 1000000f) } };
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
            report.Check("They arrive beside their friend", (late.Client.Predictor.Current.Position - first.Client.Predictor.Current.Position).Length < 60f, $"{late.Client.Predictor.Current.Position} and {first.Client.Predictor.Current.Position}");
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
            lab.Players[1].Client.StepThroughWaygate(next, new Offering());

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
            report.Check("A different build is told so, in words, rather than reading rows wrongly", ToldDifferentVersions(worlds, request, NetSchema.Hash ^ 1u, Protocol.Build, out string schemaAnswer), schemaAnswer);
            report.Check("A build with other rules but the same components is told so too", ToldDifferentVersions(worlds, request, NetSchema.Hash, Protocol.Build ^ 1u, out string buildAnswer), buildAnswer);
        }

        private static bool ToldDifferentVersions(WorldCache worlds, WorldRequest request, uint schema, uint build, out string answer)
        {
            using var lab = new GameLab(worlds, request);
            (LoopbackTransport serverEnd, LoopbackTransport clientEnd) = LoopbackTransport.CreatePair();
            lab.Server.AddTransport(serverEnd);
            var listener = new RawClient();
            clientEnd.Poll(listener);

            BitWriter hello = Protocol.Begin(Message.Hello);
            hello.WriteUInt(Protocol.Version);
            hello.WriteUInt(schema);
            hello.WriteUInt(build);
            hello.WriteString("Stranger");
            Protocol.WriteHero(hello, HeroState.CreateNew());
            listener.Connection!.Send(hello.ToArray(), Delivery.Reliable);

            lab.Run(0.5);
            clientEnd.Poll(listener);
            answer = listener.Closed ?? "not closed";
            return listener.Closed?.Contains("versions") == true;
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

        private static readonly Vector2 ParadeFirstPlace = new Vector2(-16.5f, -7f);    // From the hero, where the first of a parade stands.

        // Half the trip, the worst the line varies by, and the few ticks of margin that blending needs.
        private static double AllowedBehind(NetworkConditions conditions) => (conditions.Latency * 0.5 + conditions.Jitter * 1.5) / 1000.0 + 0.2;

        private static double AverageBehind(GameLab lab, LabPlayer player, double seconds)
        {
            double total = 0.0;
            int samples = 0;
            lab.Run(seconds, () =>
            {
                total += lab.DrawnBehind(player);
                samples++;
            });
            return total / samples;
        }

        // A stall is a moment, and has to cost only that. A client that takes one for what the line is like draws the world that far in the
        // past from then on, with every blow, health bar and enemy swing that late: a first frame of 3.7 s once made a game of it for good.
        private static void ThePictureKeepsUp(Report report, WorldCache worlds, WorldRequest request, string name, NetworkConditions conditions)
        {
            using var lab = new GameLab(worlds, request);
            LabPlayer player = lab.AddPlayer("Hero", conditions, 60);
            double allowed = AllowedBehind(conditions);

            // The first frames in a world are the longest of all, and come just as the clock has begun.
            while (!player.Client.Render.Started && lab.SecondsRun < 30.0) lab.Run(0.01);
            player.Stall(3.7);
            lab.Run(3.7 + 4.0);
            double afterArriving = AverageBehind(lab, player, 3.0);
            report.Check($"{name}: a long first frame in a world costs nothing once it is over", afterArriving < allowed, $"{afterArriving * 1000.0:0} ms, of {allowed * 1000.0:0}");

            for (int i = 0; i < 4; i++)
            {
                player.Stall(0.5 + i);
                lab.Run(0.5 + i + 4.0);
            }
            double afterFrames = AverageBehind(lab, player, 3.0);
            report.Check($"{name}: nor do long frames during a game, however many", afterFrames < allowed, $"{afterFrames * 1000.0:0} ms, of {allowed * 1000.0:0}");

            for (int i = 0; i < 4; i++)
            {
                lab.StallServer(1.0);
                lab.Run(4.0);
            }
            double afterServer = AverageBehind(lab, player, 3.0);
            report.Check($"{name}: nor does the server stalling", afterServer < allowed, $"{afterServer * 1000.0:0} ms, of {allowed * 1000.0:0}");

            // The same thing as a player meets it: a swing at something standing in reach, and how long the number takes to come up.
            player.Fights = false;
            player.FirstBlowsShownAfter.Clear();
            ServerPlayer truth = lab.Server.Players.First();
            for (int i = 0; i < 10; i++)
            {
                Vector2 target = truth.Sim.Position + ParadeFirstPlace;
                lab.Server.Parade("wolf");
                player.Client.TravelTo(target + new Vector2(2.5f, 0f));
                lab.Run(1.5);
                player.StrikeAt = target;
                lab.Run(2.0);
            }
            List<double> blows = player.FirstBlowsShownAfter.OrderBy(b => b).ToList();
            double blow = blows.Count == 0 ? double.PositiveInfinity : blows[blows.Count / 2];
            double allowedBlow = allowed + conditions.Latency * 0.5 / 1000.0 + 0.1;
            report.Check($"{name}: a blow shows as soon as the line allows", blows.Count >= 5 && blow < allowedBlow, $"{blow * 1000.0:0} ms in the middle of {blows.Count}, of {allowedBlow * 1000.0:0}");
            report.Check($"{name}: and the hero is still in step", player.DisconnectReason == null && player.Client.Ghosts.Mismatches == 0);
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
                        hello.WriteUInt(Protocol.Build);
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
            // At the landing, as this is about the line: a hero stunned in the middle of a camp has a press refused, rightly, and that would count against it.
            using var lab = new GameLab(worlds, request, "Landing");
            LabPlayer player = lab.AddPlayer("Hero", conditions, 80);
            player.Fights = false;
            lab.Run(6);

            ServerPlayer truth = lab.Server.Players.First();
            player.WhirlEveryFrames = (int)(SkillCatalog.Whirl.Cooldown * 60.0) + 90;

            int happened = 0;
            float last = 0f;
            lab.Run(120, () =>
            {
                float cooldown = truth.Sim.Cooldowns[LabPlayer.WhirlSlot];
                if (cooldown > last + 1f) happened++;
                last = cooldown;
            });
            player.WhirlEveryFrames = 0;
            lab.Run(3, () =>
            {
                float cooldown = truth.Sim.Cooldowns[LabPlayer.WhirlSlot];
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

            int Node(string id) => TalentTree.Find(id)!.Index;
            int points = truth.Hero.UnspentPoints;
            player.Client.Allocate(Node("pyre.t0"));
            lab.Run(0.5);
            report.Check("Spending a talent reaches the server and comes back", truth.Hero.Talents.Contains("pyre.t0") && player.Client.Hero.Talents.Contains("pyre.t0") && player.Client.Hero.UnspentPoints == points - 1);

            player.Client.Allocate(Node("key.tidewalker"));
            lab.Run(0.5);
            report.Check("A talent nowhere near what's taken can't be had", !truth.Hero.Talents.Contains("key.tidewalker"));

            player.Client.Allocate(Node("pyre.firebolt.0"));
            player.Client.Allocate(Node("pyre.firebolt"));
            lab.Run(0.5);
            report.Check("A skill learned on the tree goes on the bar, on both ends", truth.Hero.Bar.Contains("firebolt") && player.Client.Hero.Bar.Contains("firebolt"));
            report.Check("And there's a profile to use it with, on both ends", truth.Kit.Slots.Any(s => s?.Skill == SkillCatalog.Firebolt) && player.Client.Kit.Slots.Any(s => s?.Skill == SkillCatalog.Firebolt));

            player.Client.Allocate(Node("pyre.t1"));
            lab.Run(0.5);
            report.Check("A point that isn't there can't be spent", !truth.Hero.Talents.Contains("pyre.t1"));

            player.Client.Refund(Node("pyre.t0"));
            lab.Run(0.5);
            report.Check("A node others hang from can't be given back", truth.Hero.Talents.Contains("pyre.t0"));

            player.Client.Refund(Node("pyre.firebolt"));
            lab.Run(0.5);
            report.Check("A node at the end of a path can", !truth.Hero.Talents.Contains("pyre.firebolt") && !player.Client.Hero.Talents.Contains("pyre.firebolt"));
            report.Check("And what it taught goes with it", truth.Kit.Slots.All(s => s?.Skill != SkillCatalog.Firebolt));

            player.Client.ForgetTalents();
            lab.Run(0.5);
            report.Check("Forgetting everything gives it all back", truth.Hero.Talents.Count == 0 && player.Client.Hero.UnspentPoints == points);
            report.Check("And the numbers follow, the same on both ends", SheetsAgree(truth, player));

            int carried = truth.Hero.Inventory.Count;
            player.Client.Equip(0);
            lab.Run(0.5);
            report.Check("Equipping moves the item, on both ends", truth.Hero.Equipped.ContainsKey(WornSlot.Finger1) && player.Client.Hero.Equipped.ContainsKey(WornSlot.Finger1) && truth.Hero.Inventory.Count == carried - 1);

            int gold = truth.Hero.Gold;
            player.Client.Sell(0);
            lab.Run(0.5);
            report.Check("Selling pays, on both ends", truth.Hero.Gold > gold && player.Client.Hero.Gold == truth.Hero.Gold);

            player.Client.Equip(999);
            player.Client.Sell(-5);
            player.Client.Buy(999, 0);
            player.Client.TravelToSite(99999);
            player.Client.Allocate(99999);
            player.Client.Allocate(-1);
            player.Client.Refund(99999);
            player.Client.AssignSlot(6, null);
            lab.Run(0.5);
            report.Check("Nonsense requests are ignored", player.Client.State == ClientState.Playing);

            Vector2 far = lab.Server.Session!.World.Sites[^1].Position;
            player.Client.TravelTo(far);
            lab.Run(1);
            report.Check("Being moved by the server is believed at once", (player.Client.Predictor.Current.Position - far).Length < 0.1f);
        }

        private static bool SheetsAgree(ServerPlayer truth, LabPlayer player)
        {
            ActorSheet server = truth.Actor.Sheet;
            ActorSheet client = player.Client.Sheet;
            return server.MaxHealth == client.MaxHealth && server.MaxMana == client.MaxMana && server.MoveSpeed == client.MoveSpeed && server.Armour == client.Armour
                && server.Loadout.Skills.Length == client.Loadout.Skills.Length;
        }

        // Clicking a mark: the hero walks there on their own feet, and the press that ends it goes to the thing that was clicked and nothing else.
        private static void ClickingToUse(Report report, WorldCache worlds)
        {
            var request = new WorldRequest(303, "isle");
            using var lab = new GameLab(worlds, request, startSite: "Landing");
            LabPlayer player = lab.AddPlayer("Hero", new NetworkConditions { Latency = 60, Jitter = 10 }, 91, hero: LabHero());
            player.Fights = false;
            lab.Run(4);
            ServerSession session = lab.Server.Session!;
            ServerPlayer truth = lab.Server.Players.First();
            GameSession seen = player.Client.Session!;

            InteractableComponent? Find(InteractKind kind, out Vector2 at)
            {
                at = default;
                foreach (Archetype archetype in player.Ecs.QueryArchetypes(new BitMask(typeof(InteractableComponent), typeof(PositionComponent)), BitMask.Empty))
                {
                    foreach (EntityView entity in new ComponentAccessor(archetype))
                    {
                        var place = entity.Get<InteractableComponent>();
                        if (place.Kind != kind) continue;
                        at = entity.Get<PositionComponent>().value.Xz;
                        return place;
                    }
                }
                return null;
            }

            void Click(in InteractableComponent place, Vector2 at) => seen.Fetch.BeginUse(place.SiteIndex, place.SpawnIndex, at);

            if (Find(InteractKind.Merchant, out Vector2 stall) is InteractableComponent merchant)
            {
                player.Client.TravelTo(seen.World.Sites[0].Position + new Vector2(0f, -24f));
                lab.Run(1);
                Vector2 start = truth.Sim.Position;
                Click(merchant, stall);
                lab.Run(8);
                report.Check("Clicking a merchant walks the hero up and opens their table", player.PanelsOpened.Any(p => p.kind == InteractKind.Merchant), $"{(truth.Sim.Position - stall).Length:0.0} m away");
                report.Check("Having really walked there", (truth.Sim.Position - start).Length > 8f);
                report.Check("And that's the end of it", !seen.Fetch.Active);

                player.PanelsOpened.Clear();
                player.Client.TravelTo(stall + new Vector2(merchant.Radius - 0.1f, 0f));
                lab.Run(1);
                Click(merchant, stall);
                lab.Run(3);
                report.Check("From the very edge of its reach, a click still lands", player.PanelsOpened.Any(p => p.kind == InteractKind.Merchant));

                player.PanelsOpened.Clear();
                player.Client.TravelTo(stall + new Vector2(0f, 30f));
                lab.Run(1);
                foreach ((string what, HeroIntent input) in new[] { ("walking", new HeroIntent { Move = Vector2.UnitX }), ("dodging", new HeroIntent { Dash = true }) })
                {
                    Click(merchant, stall);
                    HeroIntent intent = input;
                    seen.Fetch.Steer(seen, ref intent);
                    report.Check($"Anything else the player does calls it off: {what}", !seen.Fetch.Active && !intent.Interact);
                }

                seen.Fetch.BeginUse(merchant.SiteIndex, 9999, stall);
                HeroIntent nothing = default;
                seen.Fetch.Steer(seen, ref nothing);
                report.Check("Something that's gone is given up on, with no press", !seen.Fetch.Active && !nothing.Interact);
            }

            if (Find(InteractKind.Waygate, out Vector2 gate) is InteractableComponent waygate)
            {
                player.Client.TravelTo(gate + new Vector2(0f, -22f));
                lab.Run(1);
                Click(waygate, gate);
                lab.Run(8);
                report.Check("Clicking a waygate walks up and offers the way", player.PanelsOpened.Any(p => p.kind == InteractKind.Waygate), $"{(truth.Sim.Position - gate).Length:0.0} m away");
            }

            // These stand seven metres apart, so the reach of each covers the way to the other: a press too soon goes to the wrong one.
            WorldSite stones = seen.World.Sites.First(s => s.Kind == SiteKind.StandingStones && s.Spawns.Any(p => p.Role == SpawnRole.Chest));
            SiteState state = session.Sites.First(s => s.Site.Id == stones.Id);
            Vector2 altarAt = stones.Spawns.First(s => s.Role == SpawnRole.Altar).Position;
            Vector2 chestAt = stones.Spawns.First(s => s.Role == SpawnRole.Chest).Position;
            Vector2 past = (chestAt - altarAt).Normalized();

            player.Client.TravelTo(chestAt + past * 6f);
            lab.Run(3);
            if (Find(InteractKind.Shrine, out Vector2 altarGhost) is InteractableComponent altar)
            {
                Click(altar, altarGhost);
                lab.Run(6);
                report.Check("Clicking something past something else uses what was clicked", state.Kindled);
                report.Check("And leaves alone what was walked past", state.Opened.Count == 0);
            }
            else report.Check("A shrine with a chest beside it reaches the client", false);

            player.Client.TravelTo(altarAt - past * 6f);
            lab.Run(2);
            if (Find(InteractKind.Chest, out Vector2 chestGhost) is InteractableComponent chest)
            {
                Click(chest, chestGhost);
                lab.Run(6);
                report.Check("And the other way round", state.Opened.Count == 1);
            }
            else report.Check("That chest reaches the client", false);
        }

        // Merchants, chests and shrines, all the way through: the hero walks up, presses use, the server decides, and the answer comes back.
        private static void UsingThings(Report report, WorldCache worlds)
        {
            var request = new WorldRequest(303, "isle");
            using var lab = new GameLab(worlds, request, startSite: "Landing");
            HeroState hero = LabHero();
            hero.Gold = 1000000;

            // Using things is what's on trial here, and a fall in a camp costs gold, which reads as a chest giving none.
            hero.Equipped[WornSlot.Neck] = new Item { Name = "Test Charm", Slot = ItemSlot.Neck, Level = 1, Lines = { new ItemLine("health", 1000000f) } };
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
                Vector2 arrived = player.Client.Predictor.Current.Position;
                report.Check("A kindled shrine can be travelled to", (arrived - shrine.Position).Length < 8f);
                report.Check("And the hero arrives beside its altar, not inside it", session.World.Passable.IsSet(arrived));
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
            hero.Gold = 500;
            hero.Inventory.Add(new Item { Name = "A Ring", Slot = ItemSlot.Finger, Level = 3 });
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
