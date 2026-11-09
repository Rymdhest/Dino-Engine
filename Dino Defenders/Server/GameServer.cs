using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.ECS.Systems;
using Dino_Engine.Networking;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Server
{
    /// <summary>What a server starts from.</summary>
    public sealed class ServerSettings
    {
        public WorldRequest Request;
        public LandTerms? Terms;        // What the land holds, past its seed and level.
        public SaveGame? Save;
        public bool AllowTeleport;      // For scripted runs: a client may ask to be put anywhere.
        public Vector2? Start;
        public string? StartSite;
        public string? StartBiome;
    }

    /// <summary>
    /// The game, with nobody watching. It holds one world and every hero in it, runs them at a fixed rate and tells clients how
    /// things stand, and it is the only place anything is decided. There is no other way to play: someone alone runs one of
    /// these beside them, a host opens theirs up to friends, and a dedicated machine can run one with no window at all. Nothing in
    /// here draws, or knows that anything does.
    /// </summary>
    public sealed class GameServer : INetListener, IDisposable
    {
        private const float EventReach = 110f;
        private const int HeroUpdateEvery = 6;      // Ticks.
        private const int ProgressEvery = 15;
        private const float LoadSearchRadius = 24f;
        private const int FlaskPrice = 12;
        private const int StockSize = 12;
        private const int FolioEvery = 4;   // One thing in this many on a table is for a book.
        private const int OrdersOnTable = 2;        // The last cells of the Haven's table: orders taken from the Reich, the one place besides a tyrant they come from for sure.
        private const float RareOrders = 0.15f;
        private const float MagicOrders = 0.45f;
        private const float ModerateLoss = 0.08f;
        private const float HeavyLoss = 0.2f;
        private const float GateReach = 9f;         // How near a waygate someone has to be to use one, a little more than the gate's own reach, as the server sees them a moment late.
        private const float ArriveBefore = 7f;      // Metres in front of a waygate that someone steps out of it.

        private readonly ServerSettings _settings;
        private readonly WorldCache _worlds;
        private readonly List<INetTransport> _transports = new();
        private readonly Dictionary<INetConnection, ServerPlayer> _players = new();
        private readonly List<ServerPlayer> _heroes = new();
        private readonly List<SystemBase> _systems = new();
        private readonly InteractionSystem _interaction;
        private readonly BitWriter _snapshot = new(2048);
        private readonly List<ServerPlayer> _due = new();

        private Task<WorldData>? _loading;
        private WorldRequest _request;
        private LandTerms _terms = new();
        private SaveGame? _saved;
        private CharacterMover? _mover;
        private uint _structureHash;
        private uint _groundHash;
        private int _epoch;
        private int _nextPlayerId = 1;

        public readonly ECSWorld Ecs = new();
        public readonly GhostServer Ghosts;
        public ServerSession? Session { get; private set; }

        /// <summary>Milliseconds, from any starting point.</summary>
        public Func<long> Clock = System.Diagnostics.Stopwatch.StartNew() is var watch ? () => watch.ElapsedMilliseconds : null!;

        /// <summary>The tick being run, or about to be.</summary>
        private readonly EncounterSystem _encounters;

        public uint Tick { get; private set; } = 1;

        public IReadOnlyList<ServerPlayer> Heroes => _heroes;
        public IEnumerable<ServerPlayer> Players => _players.Values;
        public int PlayerCount => _players.Count;
        public bool Paused { get; private set; }

        /// <summary>Largest snapshot sent, and how many bytes have gone out in all.</summary>
        public int LargestSnapshot { get; private set; }
        public long BytesSent { get; private set; }

        public GameServer(ServerSettings settings, WorldCache worlds)
        {
            _settings = settings;
            _worlds = worlds;
            Ghosts = new GhostServer(Ecs);

            _interaction = new InteractionSystem(this);
            _systems.Add(new ActorSystem(this));
            _systems.Add(new EnemyBrainSystem(this));
            _systems.Add(new VillagerSystem(this));
            _systems.Add(new ProjectileSystem(this));
            _systems.Add(new ZoneSystem(this));
            _systems.Add(new TelegraphSystem(this));
            _systems.Add(new DyingSystem());
            _systems.Add(_interaction);
            _systems.Add(_encounters = new EncounterSystem(this));
            _systems.Add(new ArenaSystem(this));
            _systems.Add(new BodySystem(this));
            _systems.Add(new CharacterMotorSystem());
            _systems.Add(new VelocitySystem());
            _systems.Add(new FixToTerrainHeightSystem());

            BeginWorld(settings.Request, settings.Terms ?? new LandTerms(), settings.Save);
        }

        public void AddTransport(INetTransport transport) => _transports.Add(transport);

        /// <summary>Runs one tick.</summary>
        public void Step()
        {
            Ecs.ApplyDeferredCommands();
            foreach (INetTransport transport in _transports) transport.Poll(this);
            FinishLoading();
            Ecs.ApplyDeferredCommands();

            if (Session != null)
            {
                StepHeroes();
                foreach (SystemBase system in _systems) system.Step(Ecs, NetTime.TickSeconds);
                Ecs.ApplyDeferredCommands();
                Ecs.DirtyEntities.Clear();
                if (Session.PortalChanged) ShowPortal(Session);
                if (Session.HomeAt is double home && Session.Time >= home) GoHome();
            }

            SendUpdates();
            Tick++;

            if (_next is var (request, terms))
            {
                _next = null;
                BeginWorld(request, terms, null);
            }
        }

        public void Dispose() => Close("The host has left.");

        /// <summary>Ends the game for everyone, telling them why.</summary>
        public void Close(string reason)
        {
            foreach (ServerPlayer player in _players.Values) player.Connection.Close(reason);
            foreach (INetTransport transport in _transports) transport.Dispose();
            _transports.Clear();
        }

        private void BeginWorld(WorldRequest request, LandTerms terms, SaveGame? save)
        {
            _epoch++;
            _heroes.Clear();
            foreach (ServerPlayer player in _players.Values)
            {
                player.InWorld = false;
                player.Tables.Clear();
                player.Asked.Clear();
            }

            Session = null;
            Ecs.ClearAllEntitiesExcept();
            Ecs.DirtyEntities.Clear();

            _terms = Lands.IsHaven(request) ? new LandTerms() : terms;
            _request = Lands.IsHaven(request) ? request : _terms.Shape(request);
            _saved = save;
            _loading = _worlds.Get(_request);

            foreach (ServerPlayer player in _players.Values)
            {
                if (player.Greeted) SendWorld(player, Message.WorldChange);
            }
        }

        private void FinishLoading()
        {
            if (_loading == null || !_loading.IsCompleted) return;

            Task<WorldData> finished = _loading;
            _loading = null;
            if (finished.IsFaulted)
            {
                Console.WriteLine($"World generation failed: {finished.Exception}");
                foreach (ServerPlayer player in _players.Values) player.Connection.Close("That land would not take shape.");
                return;
            }

            WorldData world = finished.Result;
            Session = new ServerSession(this, world, _request, _terms);
            if (_saved != null) Session.Restore(_saved);
            _mover = new CharacterMover(world);
            (_structureHash, _groundHash) = Protocol.WorldHash(world);

            Ecs.RegisterSingleton<WorldDataSingleton>(Ecs.CreateEntity(new WorldDataSingleton(world)));
            Ecs.ApplyDeferredCommands();

            foreach (ServerPlayer player in _players.Values)
            {
                if (player.ReadyEpoch == _epoch) Arrive(player);
            }
        }

        private void Arrive(ServerPlayer player)
        {
            ServerSession session = Session!;
            bool first = _heroes.Count == 0;
            Vector2 start = StartPosition(session.World, first);

            // There's one life to a land, and leaving and coming back in isn't another: whoever fell here arrives as they left, fallen, and with what it meant for their climb already settled.
            bool fallen = !session.IsHaven && session.Fallen.Contains(player.Name);
            player.LandMods = session.Terms.HeroMods();
            player.Out = fallen;
            player.Judged = fallen;
            var actor = new Actor(Team.Heroes, HeroBuild.Compile(player.Hero, null, player.LandMods), start, HeroRules.Radius) { Player = player, Body = "hero" };
            player.Actor = actor;
            player.Kit = new HeroKit(player.Hero, actor.Sheet);
            player.WatchBooks();
            if (first && _saved is { InLand: true } && !session.IsHaven && _saved.Health > 0f) actor.Vitals.Health = Math.Min(_saved.Health, actor.Sheet.MaxHealth);

            player.Sim = HeroSimState.At(start, actor.Vitals.Mana);
            if (fallen)
            {
                actor.Vitals.Health = 0f;
                actor.Dead = true;
                player.Sim.Down = true;
            }
            player.Sim.Health = actor.Vitals.Health;
            player.View = new GhostView { Focus = start };
            player.GhostId = Ghosts.NextId();
            player.DownFor = 0f;

            Vector3 position = new Vector3(start.X, session.World.Height.Sample(start), start.Y);
            player.Entity = Ecs.CreateEntity("Hero",
                new NetIdentityComponent(player.GhostId, importance: 4f, everywhere: true),
                new PositionComponent(position),
                new RotationComponent(new Vector3(0f)),
                new ScaleComponent(new Vector3(1f)),
                new VisualComponent($"hero{(player.Id - 1) % Protocol.MaxPlayers}", VisualKind.Character),
                new FixToTerrainHeightComponent(),
                new HeroComponent { Name = player.Name, Title = TalentTree.TitleOf(player.Hero), Player = player.Id, Level = player.Hero.Level },
                new HealthComponent(actor.Sheet.MaxHealth) { Current = actor.Vitals.Health },
                new ActionComponent(),
                new AfflictedComponent(),
                new ActorComponent(actor));
            actor.Entity = player.Entity;

            _heroes.Add(player);
            player.InWorld = true;
            if (fallen) session.SettleIfAllOut();

            BitWriter arrived = Protocol.Begin(Message.Arrived);
            arrived.WriteVarInt(_epoch);
            arrived.WriteUInt(Tick);
            arrived.WriteVarUInt(player.GhostId);
            arrived.WriteFloat(start.X);
            arrived.WriteFloat(start.Y);
            player.Connection.Send(arrived.ToArray(), Delivery.Reliable);

            SendProgress(player, everything: true);
            SendHero(player, gear: true);
            SendParty();
            session.Ground.Introduce(player);
            foreach (ServerPlayer hero in _heroes) SendWorn(hero);
        }

        private Vector2 StartPosition(WorldData world, bool first)
        {
            // Friends arrive beside whoever is already here, not a long walk away.
            if (!first && _heroes.Count > 0 && TryFindGround(world, _heroes[0].Sim.Position + new Vector2(3f, 3f), out Vector2 beside)) return beside;

            if (first && _saved is { InLand: true } && _saved.PlayerX >= 0f && !Session!.IsHaven)
            {
                // Someone can stand closer to a wall than the passable map allows for, so the spot itself may not be on it.
                if (TryFindGround(world, new Vector2(_saved.PlayerX, _saved.PlayerZ), out Vector2 ground)) return ground;
            }
            // A scripted run asks for a place to look at, which wins over where anyone would really arrive.
            if (_settings.Start.HasValue) return _settings.Start.Value;
            if (_settings.StartBiome != null && TryFindWood(world, _settings.StartBiome, out Vector2 wood)) return wood;
            if (_settings.StartSite != null)
            {
                foreach (WorldSite site in world.Sites)
                {
                    // The middle of a site can be built on.
                    if (site.Kind.ToString() == _settings.StartSite) return TryFindGround(world, site.Position, out Vector2 ground) ? ground : site.Position;
                }
            }

            // Out of the waygate a hero came through: in the Haven its great one, and in a land the one at the landing.
            if (WaygateSpawn(world, world.Sites[0]) is Vector2 gate)
            {
                Vector2 into = Session!.IsHaven ? -HavenNorth : (world.Sites[0].Position - gate).LengthSquared > 1f ? (world.Sites[0].Position - gate).Normalized() : Vector2.UnitY;
                if (TryFindGround(world, gate + into * ArriveBefore, out Vector2 stepped)) return stepped;
            }
            return world.Sites[0].Position;
        }

        // The heart of the thickest wood of a biome, for looking at it.
        private static bool TryFindWood(WorldData world, string biomeId, out Vector2 wood)
        {
            wood = default;
            int biome = -1;
            for (int i = 0; i < world.Plan.Biomes.Count; i++)
            {
                if (world.Plan.Biomes[i].Id == biomeId) biome = i;
            }
            if (biome < 0) return false;

            float best = 0f;
            bool found = false;
            for (float z = 32f; z < world.Size - 32f; z += 16f)
            {
                for (float x = 32f; x < world.Size - 32f; x += 16f)
                {
                    Vector2 at = new Vector2(x, z);
                    float score = world.BiomeWeights[biome].Sample(at) * (0.2f + world.Canopy.Sample(at));
                    if (score <= best || !world.Passable.IsSet(at) || world.Height.Sample(at) - world.WaterLevelAt(at) < 1.5f) continue;
                    best = score;
                    wood = at;
                    found = true;
                }
            }
            return found;
        }

        private static readonly Vector2 HavenNorth = new Vector2(0f, 1f);

        /// <summary>Where a place's waygate is used from, if it has one.</summary>
        public static Vector2? WaygateSpawn(WorldData world, WorldSite site)
        {
            foreach (SiteSpawn spawn in site.Spawns)
            {
                if (spawn.Role == SpawnRole.Waygate) return spawn.Position;
            }
            return null;
        }

        /// <summary>Somewhere to stand near a point, looking outward until there is.</summary>
        public static bool TryFindGround(WorldData world, Vector2 near, out Vector2 ground)
        {
            ground = near;
            if (world.Passable.IsSet(near)) return true;

            for (float radius = 1.5f; radius <= LoadSearchRadius; radius += 1.5f)
            {
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * (MathF.Tau / 16f);
                    ground = near + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                    if (world.Passable.IsSet(ground)) return true;
                }
            }
            return false;
        }

        private void StepHeroes()
        {
            ServerSession session = Session!;
            bool alone = _players.Count == 1;

            foreach (ServerPlayer player in _heroes)
            {
                HeroCommand command = player.Take(Tick);

                // With others in the world there's no stopping it for one of them.
                if (!alone) command.Buttons &= ~HeroButtons.Frozen;

                // Someone who's fallen in a land is out of it, and only watches.
                if (player.Out) command = new HeroCommand { Tick = command.Tick, ViewLag = command.ViewLag };
                Paused = alone && command.Has(HeroButtons.Frozen);

                Actor actor = player.Actor;
                if (!ReferenceEquals(player.Kit.Sheet, actor.Sheet)) player.Kit = new HeroKit(player.Hero, actor.Sheet);
                Brief(player);

                float healthBefore = player.Sim.Health;
                float manaBefore = player.Sim.Mana;
                Vector2 wasAt = player.Sim.Position;
                byte wasMoved = player.Sim.Moves;
                HeroStepEvents events = HeroRules.Step(ref player.Sim, command, player.Kit, _mover!);
                actor.Vitals.Mana = player.Sim.Mana;
                actor.Vitals.Health = player.Sim.Health;
                actor.Vitals.Barrier = player.Sim.Barrier;
                if (player.Sim.Health < healthBefore) actor.HealthSpent += (healthBefore - player.Sim.Health) / actor.Sheet.MaxHealth * 100f;

                if (!Paused)
                {
                    if (actor.Vitals.Health > 0f)
                    {
                        if (player.Sim.Moves == wasMoved) session.Combat.Watch(actor, BookWhen.Moved, (player.Sim.Position - wasAt).Length);
                        Act(session, player, command, events, manaBefore);
                        GoOff(session, player);
                        StepCalling(session, player);
                    }
                    else
                    {
                        CutShort(session, player);
                        UpdateDown(player);
                    }
                }

                EntityView view = Ecs.GetEntityView(player.Entity);
                Vector2 at = player.Sim.Position;
                view.Set(new PositionComponent(new Vector3(at.X, session.World.Height.Sample(at), at.Y)));
                view.Set(new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitY, player.Sim.Yaw)));

                var hero = view.Get<HeroComponent>();
                string title = TalentTree.TitleOf(player.Hero);
                if (hero.Level != player.Hero.Level || hero.Title != title)
                {
                    hero.Level = player.Hero.Level;
                    hero.Title = title;
                    view.Set(hero);
                }
            }
        }

        // What goes off with nobody pressing it: Trigger Books that have got there, and Toggle Books that are on. It's here and not in a blow, as a blow can't be what sets off the next.
        private static void GoOff(ServerSession session, ServerPlayer player)
        {
            Actor actor = player.Actor;
            Vector2 at = player.Sim.Position;
            foreach (BookWatch watch in actor.Watches)
            {
                if (watch.Progress < watch.Amount || player.Sim.Cooldowns[watch.Slot] > 0f || player.Kit.Slots[watch.Slot] is not BookProfile book) continue;

                // Nothing to pay with, it waits, and goes the moment there is.
                float manaBefore = actor.Vitals.Mana;
                if (!PayFor(session, player, book)) continue;

                player.Sim.Cooldowns[watch.Slot] = book.Cooldown;
                watch.Progress = 0f;
                actor.Spent = manaBefore - actor.Vitals.Mana;
                session.Combat.Release(actor, book, at, player.Sim.Aim, player.Sim.AimReach, 0, Worth(actor));
            }

            for (int slot = 0; slot < HeroState.BarSlots; slot++)
            {
                player.Pulses[slot] = HeroRules.IsOn(player.Sim, slot) ? player.Pulses[slot] - NetTime.TickSeconds : 0f;
                if (player.Pulses[slot] > 0f || !HeroRules.IsOn(player.Sim, slot) || player.Kit.Slots[slot] is not BookProfile aura) continue;
                session.Combat.Pulse(actor, aura, at, player.Sim.Aim, player.Sim.AimReach);
                player.Pulses[slot] = aura.Pulse;
            }
        }

        // What a book that goes by itself costs, out of whatever it's paid with. False, and nothing taken, if there isn't enough.
        private static bool PayFor(ServerSession session, ServerPlayer player, BookProfile book)
        {
            Actor actor = player.Actor;
            if (book.CostsMinion && session.Combat.Oldest(actor) == null) return false;
            if (book.CostsGold)
            {
                int gold = (int)MathF.Ceiling(book.Cost * Rulebook.GoldPerCost);
                if (player.Hero.Gold < gold) return false;
                player.Hero.Gold -= gold;
                player.SummaryChanged = true;
            }
            else if (book.CostsHealth)
            {
                if (actor.Vitals.Health <= book.Cost) return false;
                actor.Vitals.Health -= book.Cost;
            }
            else
            {
                float barrier = actor.Sheet.Has(Rule.BarrierPays) ? MathF.Max(actor.Vitals.Barrier, 0f) : 0f;
                if (actor.Vitals.Mana + barrier < book.Cost) return false;
                float fromBarrier = MathF.Min(barrier, book.Cost);
                actor.Vitals.Barrier -= fromBarrier;
                actor.Vitals.Mana = actor.Sheet.Has(Rule.SpendsAll) ? 0f : actor.Vitals.Mana - (book.Cost - fromBarrier);
            }
            if (book.CostsMinion) session.Combat.Dismiss(session.Combat.Oldest(actor)!);
            return true;
        }

        /// <summary>What someone's last use is worth, of what it says: some are worth what they spent, and some are weaker for being the same as the one before.</summary>
        public static float Worth(Actor actor)
        {
            float worth = 1f;
            if (actor.Sheet.Has(Rule.ManaPower)) worth += actor.Spent / Rulebook.ManaPowerStep / 100f;
            if (actor.Streak > 1 && actor.Sheet.Has(Rule.NoRepeat)) worth *= 1f - Rulebook.RepeatLess;
            return worth;
        }

        // What a use by hand costs that only the server can take, and the count it keeps of them. Mana and health were taken by the step, on both ends.
        private static void Used(ServerSession session, ServerPlayer player, BookProfile book, float manaBefore)
        {
            Actor actor = player.Actor;
            int slot = player.Sim.Using;
            actor.Streak = slot == actor.LastSlot ? actor.Streak + 1 : 1;
            actor.LastSlot = slot;
            actor.SinceSlot[slot] = 0f;
            actor.Spent = MathF.Max(manaBefore - player.Sim.Mana, 0f);

            if (book.CostsGold)
            {
                player.Hero.Gold = Math.Max(player.Hero.Gold - (int)MathF.Ceiling(book.Cost * Rulebook.GoldPerCost), 0);
                player.SummaryChanged = true;
            }
            if (book.CostsMinion && session.Combat.Oldest(actor) is Actor minion) session.Combat.Dismiss(minion);
            session.Combat.SetOff(actor, TriggerEvent.Used, actor.Position);
        }

        // What's happened to a hero since their last step that their step has to know about: only the server can say.
        private static void Brief(ServerPlayer player)
        {
            Actor actor = player.Actor;
            Afflictions afflictions = actor.Afflictions;
            player.Sim.Mana = actor.Vitals.Mana;
            player.Sim.Health = actor.Vitals.Health;
            player.Sim.Barrier = actor.Vitals.Barrier;
            player.Sim.Held =MathF.Max(afflictions.FreezeLeft, afflictions.StunLeft);
            player.Sim.ActionRate = afflictions.ActionRate;
            player.Sim.MoveRate = afflictions.ActionRate;
            player.Sim.State = actor.State;
            player.Sim.Counters = actor.Counters;
            player.Sim.Charged = default;
            foreach (BookWatch watch in actor.Watches) player.Sim.Charged[watch.Slot] = watch.Progress / MathF.Max(watch.Amount, 0.01f);
        }

        private void Act(ServerSession session, ServerPlayer player, in HeroCommand command, HeroStepEvents events, float manaBefore)
        {
            Vector3 position = HeroPosition(player);
            Vector2 at = player.Sim.Position;
            BookProfile? book = player.Sim.Using >= 0 ? player.Kit.Slots[player.Sim.Using] : null;
            bool landed = (events & HeroStepEvents.Landed) != 0;
            bool dashed = (events & HeroStepEvents.Dashed) != 0;

            // What's stopped comes first, as a dodge and a press in the same breath are a dodge and then a blow.
            if (landed)
            {
                player.WindingUp = false;
                player.Mark = default;
            }
            else if (dashed || player.Sim.ImpactIn < 0f) CutShort(session, player);
            if (dashed)
            {
                session.Combat.Stop(player.Entity);
                session.Combat.SetOff(player.Actor, TriggerEvent.Dash, at);
            }

            if ((events & HeroStepEvents.Used) != 0 && book != null)
            {
                session.Combat.Watch(player.Actor, BookWhen.Used, 1f);
                Used(session, player, book, manaBefore);
                float rate = MathF.Max(player.Sim.ActionRate, 0.1f);
                session.Combat.Perform(player.Entity, book.Sign, player.Sim.ImpactIn / rate, player.Sim.BusyFor / rate, player.Sim.Aim);
                player.Mark = MarkForFriends(session, player, book, rate);
                player.WindingUp = true;
            }

            if (landed && book != null)
            {
                session.Combat.Release(player.Actor, book, at, player.Sim.Aim, player.Sim.AimReach, command.ViewLag, Worth(player.Actor));
            }

            // Its owner saw all of this the moment they pressed; it's everyone else who needs telling.
            const HeroStepEvents visible = HeroStepEvents.Landed | HeroStepEvents.Used | HeroStepEvents.Dashed;
            if ((events & visible) != 0)
            {
                Broadcast(new HeroMoveEvent { Position = position, Aim = player.Sim.Aim, Move = events & visible, Sign = book?.Sign.Number ?? PageSign.None }, at, except: player);
            }

            if (command.Has(HeroButtons.Flask)) DrinkFlask(player, position);
            if (command.Has(HeroButtons.Interact)) _interaction.Interact(Ecs, session, player);
            session.Ground.Serve(player);
        }

        // A blow that was on its way and now won't land, for whatever reason: a dodge, a stun, or the end of them. Nobody should go on thinking it's coming.
        private static void CutShort(ServerSession session, ServerPlayer player)
        {
            if (!player.WindingUp) return;
            player.WindingUp = false;
            session.Combat.Withdraw(player.Mark);
            session.Combat.Stop(player.Entity);
            player.Mark = default;
        }

        // Whoever's casting has seen this since they pressed. Their friends are told, as anything of a hero's is worth knowing about if you're standing nearby.
        private Entity MarkForFriends(ServerSession session, ServerPlayer player, BookProfile book, float rate)
        {
            if (_heroes.Count < 2 || !book.ShowsArea) return default;
            GroundShape shape = GroundShape.Of(book, player.Sim.Position, player.Sim.Aim, player.Sim.AimReach);
            return session.Combat.Warn(shape, MarkTier.Plain, false, player.Sim.ImpactIn / rate + book.Delay, player.Id);
        }

        private void DrinkFlask(ServerPlayer player, Vector3 position)
        {
            Actor actor = player.Actor;
            ActorSheet sheet = actor.Sheet;
            // A flask that heals nothing can always be drunk, for whatever else it does.
            bool heals = !sheet.Has(Rule.FlaskNoHeal);
            if (player.Hero.Flasks <= 0 || heals && actor.Vitals.Health >= sheet.MaxHealth) return;
            player.Hero.Flasks--;
            player.SummaryChanged = true;
            float heal = heals ? sheet.MaxHealth * HeroRules.FlaskHeal * sheet.FlaskScale : 0f;
            Hits.Heal(sheet, ref actor.Vitals, heal);
            Session!.Combat.SetOff(actor, TriggerEvent.Flask, actor.Position);
            Broadcast(new SoundEvent { Position = position, Cue = SoundCue.Flask }, position.Xz);
            Broadcast(new FlourishEvent { Who = player.GhostId, Kind = Flourish.Drink }, position.Xz);
            if (heal > 0f) Broadcast(new TextEvent
            {
                Position = position + Vector3.UnitY * 5.4f,
                Text = $"+{heal:0}",
                Colour = new Vector3(0.4f, 1f, 0.5f),
                Size = 18f,
                Life = 1.2f,
            }, position.Xz);
        }

        // There's one life to a land: a fallen hero is out of it, a little poorer, and watches until everyone goes home. In the Haven, where nothing can hurt anyone, they're only stood back up.
        private void UpdateDown(ServerPlayer player)
        {
            if (!player.Sim.Down)
            {
                player.Sim.Down = true;
                player.DownFor = 0f;
                int lost = player.Actor.Sheet.Has(Rule.DeathTakesAll) ? player.Hero.Gold : player.Hero.Gold / 10;
                player.Actor.RaresSlain = 0;
                player.Hero.Gold -= lost;
                player.SummaryChanged = true;
                bool freed = Session!.Liberated;
                Announce(player, "You have fallen", Session.IsHaven ? "" : freed ? "The land is already free." : _heroes.Count > 1 && _heroes.Any(hero => hero != player && !hero.Out) ? "You're out of this land. Your friends fight on." : "The land is lost.");
                if (lost > 0) Note(player, $"{lost} gold lost.");
                Session.HeroFell(player);
            }

            player.DownFor += NetTime.TickSeconds;
            if (!Session!.IsHaven || player.DownFor < HeroRules.DeathDelay) return;

            player.Sim.Down = false;
            player.Actor.Dead = false;
            player.Actor.Afflictions.Clear();
            player.Actor.Vitals = Vitals.Full(player.Actor.Sheet);
            player.Hero.Flasks = HeroState.MaxFlasks;
            player.SummaryChanged = true;
        }

        // Back to the Haven, everyone, from wherever this is. What the land meant for each of them is settled first, and goes out before the world does.
        private void GoHome()
        {
            if (_next != null) return;
            Session?.Leave();
            foreach (ServerPlayer hero in _heroes) hero.SummaryChanged = true;
            SendHeroesNow();
            _next = (Lands.Haven, new LandTerms());
        }

        // Where everyone goes next, once this tick is over: a world can't change under what's stepping through this one.
        private (WorldRequest Request, LandTerms Terms)? _next;

        // Whatever's changed about a hero goes out before the world does, so that their machine has it to keep.
        private void SendHeroesNow()
        {
            foreach (ServerPlayer hero in _heroes)
            {
                if (hero.SummaryChanged || hero.GearChanged || hero.BuffsChanged || hero.StashChanged) SendHero(hero, hero.GearChanged || hero.StashChanged);
            }
        }

        // The way out of the Haven, as it stands: a light in the great gate's arch, which is what's stepped through, or nothing.
        private Entity _portal;

        private static int BiomeIndex(BiomeDefinition biome)
        {
            for (int i = 0; i < BiomeCatalog.All.Count; i++)
            {
                if (BiomeCatalog.All[i] == biome) return i;
            }
            return -1;
        }

        private void ShowPortal(ServerSession session)
        {
            session.PortalChanged = false;
            if (_portal.IsValid() && Ecs.TryGetEntityLocation(_portal, out _, out _)) Ecs.DestroyEntity(_portal);
            _portal = default;
            if (session.Portal is not var (request, _, name) || WaygateSpawn(session.World, session.World.Sites[0]) is not Vector2 spot) return;

            // The light hangs in the arch itself, which stands a little behind where it's used from.
            Vector3 at = new Vector3(spot.X, session.World.Height.Sample(spot), spot.Y);
            Quaternion facing = Quaternion.FromAxisAngle(Vector3.UnitY, MathF.PI);
            if (session.World.Objects.TryNearest(StructureCatalog.GreatWaygate.Id, spot, 12f, out WorldObject gate))
            {
                at = gate.Position;
                facing = gate.Rotation;
            }
            WorldPlan plan = WorldPlan.Create(request);
            _portal = Ecs.CreateEntity("Portal",
                new NetIdentityComponent(Ghosts.NextId(), importance: 3f, everywhere: true),
                new PositionComponent(at),
                new RotationComponent(facing),
                new ScaleComponent(new Vector3(1f)),
                new VisualComponent($"portal:{BiomeIndex(plan.Biomes[0])}", VisualKind.Prop),
                new PortalComponent { Biome = BiomeIndex(plan.Biomes[0]) },
                new InteractableComponent { Kind = InteractKind.Portal, SiteIndex = 0, SpawnIndex = session.World.Sites[0].Spawns.FindIndex(s => s.Role == SpawnRole.Waygate), Radius = 6.5f, Prompt = $"Step through to {name}" });
        }

        /// <summary>Puts a hero as near somewhere as there's ground to stand on, for a page that says so.</summary>
        public void Carry(ServerPlayer player, Vector2 to)
        {
            if (TryFindGround(Session!.World, to, out Vector2 ground)) Teleport(player, ground);
        }

        private void Teleport(ServerPlayer player, Vector2 to)
        {
            player.Sim.Position = to;
            player.Sim.Moves++;
            player.Sim.DashLeft = 0f;
        }

        public Vector3 HeroPosition(ServerPlayer player)
        {
            Vector2 at = player.Sim.Position;
            return new Vector3(at.X, Session!.World.Height.Sample(at), at.Y);
        }

        /// <summary>Tells everyone close enough to see it. What it is for is show: anything that matters is in the state.</summary>
        public void Broadcast<T>(in T value, Vector2 near, ServerPlayer? except = null) where T : struct
        {
            NetEventPacket? packet = null;
            foreach (ServerPlayer hero in _heroes)
            {
                if (hero == except || (hero.Sim.Position - near).LengthSquared > EventReach * EventReach) continue;
                packet ??= Ghosts.Encode(value, Tick);
                Ghosts.Queue(hero.View, packet);
            }
        }

        public void Announce(ServerPlayer? to, string title, string subtitle = "", float life = 4f)
        {
            BitWriter writer = Protocol.Begin(Message.Notice);
            writer.WriteByte((byte)NoticeKind.Banner);
            writer.WriteString(title);
            writer.WriteString(subtitle);
            writer.WriteFloat(life);
            SendTo(to, writer);
        }

        public void Note(ServerPlayer? to, string text)
        {
            BitWriter writer = Protocol.Begin(Message.Notice);
            writer.WriteByte((byte)NoticeKind.Log);
            writer.WriteString(text);
            SendTo(to, writer);
        }

        private void SendTo(ServerPlayer? to, BitWriter writer)
        {
            byte[] data = writer.ToArray();
            if (to != null)
            {
                to.Connection.Send(data, Delivery.Reliable);
                return;
            }
            foreach (ServerPlayer player in _players.Values)
            {
                if (player.InWorld) player.Connection.Send(data, Delivery.Reliable);
            }
        }

        public void OpenPanel(ServerPlayer hero, InteractKind kind, int site)
        {
            if (kind == InteractKind.Merchant) SendMarket(hero, site);
            BitWriter writer = Protocol.Begin(Message.OpenPanel);
            writer.WriteByte((byte)kind);
            writer.WriteVarInt(site);
            hero.Connection.Send(writer.ToArray(), Delivery.Reliable);
        }

        // Each hero has their own table: what one buys isn't taken from another. In a land it's stocked a level above the land, and turns over as they free more lands. In the Haven it's stocked
        // for the deepest land they can open, with orders among it, and turns over every time they free one.
        private MerchantTable TableFor(ServerPlayer hero, int siteIndex)
        {
            if (hero.Tables.TryGetValue(siteIndex, out MerchantTable? table)) return table;

            ServerSession session = Session!;
            var stock = new Item?[StockSize];
            int turnover = hero.Hero.WorldsCleared;
            var random = new WorldRandom(session.World.Plan.Seed).Fork("market").Fork(session.Sites[siteIndex].Site.Id).Fork(turnover);
            int level = session.IsHaven ? hero.Hero.LandLevel : session.World.Plan.Level + 1;
            for (int i = 0; i < StockSize; i++) stock[i] = i % FolioEvery == FolioEvery - 1 ? ItemGenerator.Folio(level, random.Fork(i), random.Chance(0.3f) ? Rarity.Magic : Rarity.Normal) : ItemGenerator.Roll(level, random.Fork(i), rarityBonus: 0.4f);
            if (session.IsHaven)
            {
                for (int i = StockSize - OrdersOnTable; i < StockSize; i++)
                {
                    WorldRandom orders = random.Fork("orders").Fork(i);
                    float roll = orders.Float();
                    stock[i] = OrdersMaker.Roll(level, orders, roll < RareOrders ? Rarity.Rare : roll < RareOrders + MagicOrders ? Rarity.Magic : Rarity.Normal);
                }
            }
            table = new MerchantTable { Stock = stock };
            hero.Tables[siteIndex] = table;
            return table;
        }

        private void SendMarket(ServerPlayer hero, int siteIndex)
        {
            MerchantTable table = TableFor(hero, siteIndex);
            BitWriter writer = Protocol.Begin(Message.Market);
            writer.WriteVarInt(siteIndex);
            writer.WriteVarInt(hero.GearAnswered);
            Protocol.WriteCells(writer, table.Stock);
            Protocol.WriteCells(writer, table.Sold);
            hero.Connection.Send(writer.ToArray(), Delivery.Reliable);
        }

        void INetListener.OnConnected(INetConnection connection)
        {
            _players[connection] = new ServerPlayer(_nextPlayerId++, connection);
        }

        void INetListener.OnDisconnected(INetConnection connection, string reason)
        {
            if (!_players.Remove(connection, out ServerPlayer? player)) return;
            Console.WriteLine($"{player.Name} left: {reason}");

            if (_heroes.Remove(player))
            {
                // What followed them goes with them.
                player.Actor.Dead = true;
                Ecs.DestroyEntity(player.Entity);
                Note(null, $"{player.Name} has gone their own way.");
                if (_heroes.Count > 0) Session?.SettleIfAllOut();
            }
            SendParty();
        }

        void INetListener.OnMessage(INetConnection connection, byte[] data, Delivery delivery)
        {
            if (!_players.TryGetValue(connection, out ServerPlayer? player)) return;
            try
            {
                var reader = new BitReader(data);
                var type = (Message)reader.ReadByte();
                switch (type)
                {
                    case Message.Hello:
                        OnHello(player, reader);
                        break;
                    case Message.WorldReady when player.Greeted:
                        OnWorldReady(player, reader);
                        break;
                    case Message.Commands when player.InWorld:
                        OnCommands(player, reader);
                        break;
                    case Message.Action when player.InWorld:
                        OnAction(player, reader);
                        break;
                }
            }
            catch (NetReadException e)
            {
                // Whoever this is, they're not a client of ours. Everyone else plays on.
                Console.WriteLine($"Dropping {connection.Address}: {e.Message}");
                Refuse(player, "A message from you made no sense.");
            }
        }

        private void Refuse(ServerPlayer player, string reason)
        {
            BitWriter writer = Protocol.Begin(Message.Refused);
            writer.WriteString(reason);
            player.Connection.Send(writer.ToArray(), Delivery.Reliable);
            player.Connection.Close(reason);
        }

        private void OnHello(ServerPlayer player, BitReader reader)
        {
            if (player.Greeted) return;

            uint protocol = reader.ReadUInt();
            uint schema = reader.ReadUInt();
            uint build = reader.ReadUInt();
            if (protocol != Protocol.Version || schema != NetSchema.Hash || build != Protocol.Build)
            {
                Refuse(player, "You and the host are running different versions of the game. Close it and open the launcher to update, both of you.");
                return;
            }

            string name = reader.ReadString(Protocol.LongestName).Trim();
            HeroState hero = Protocol.ReadHero(reader);
            Stash? stash = reader.ReadBool() ? Protocol.ReadStash(reader) : null;

            if (_players.Values.Count(p => p.Greeted) >= Protocol.MaxPlayers)
            {
                Refuse(player, $"This game is full: {Protocol.MaxPlayers} heroes at most.");
                return;
            }

            player.Name = name.Length > 0 ? name : $"Hero {player.Id}";
            player.Hero = hero;
            player.Stash = stash;
            TalentTree.Sanitize(hero);
            player.Greeted = true;
            SendWorld(player, Message.Welcome);
            Console.WriteLine($"{player.Name} joined from {player.Connection.Address}.");
        }

        private void SendWorld(ServerPlayer player, Message type)
        {
            BitWriter writer = Protocol.Begin(type);
            writer.WriteVarInt(_epoch);
            writer.WriteVarInt(player.Id);
            writer.WriteUInt(Tick);
            Protocol.WriteRequest(writer, _request);
            Protocol.WriteTerms(writer, _terms);
            player.Connection.Send(writer.ToArray(), Delivery.Reliable);
        }

        private void OnWorldReady(ServerPlayer player, BitReader reader)
        {
            int epoch = reader.ReadVarInt();
            uint structure = reader.ReadUInt();
            uint ground = reader.ReadUInt();
            if (epoch != _epoch) return;

            player.ReadyEpoch = epoch;
            if (Session == null) return;

            if (structure != _structureHash)
            {
                Refuse(player, "Your world came out differently from the host's. Are you both on the same version?");
                return;
            }

            // Worlds are grown with floating point, and the C runtime's sines may round differently from one machine to the next. A
            // few cells of ground out only means an occasional correction to that hero, which is what corrections are for.
            if (ground != _groundHash) Console.WriteLine($"{player.Name}'s ground differs slightly from the host's.");

            if (!player.InWorld) Arrive(player);
        }

        private void OnCommands(ServerPlayer player, BitReader reader)
        {
            if (reader.ReadVarInt() != _epoch) return;

            uint latest = reader.ReadUInt();
            uint earlier = reader.ReadUInt();
            player.View.Acknowledge(latest, earlier);
            if (reader.ReadBool()) player.View.ResetBaselines();

            player.LastClientTime = reader.ReadUInt();
            player.LastClientTimeArrived = Clock();
            player.RoundTrip = (int)reader.ReadVarUInt();

            uint newest = reader.ReadUInt();
            int count = reader.ReadByte();
            if (count > Protocol.MostCommands) throw new NetReadException($"{count} commands in one packet.");

            for (int i = 0; i < count; i++)
            {
                HeroCommand command = HeroCommand.Read(reader, newest - (uint)i);
                player.Receive(command, Tick);
            }
        }

        private void OnAction(ServerPlayer player, BitReader reader)
        {
            HeroState hero = player.Hero;
            var action = (HeroAction)reader.ReadByte();
            switch (action)
            {
                case HeroAction.Allocate:
                    int allocate = reader.ReadVarInt();
                    if (!TalentTree.CanAllocate(hero, allocate)) break;
                    TalentNode taken = TalentTree.Nodes[allocate];
                    hero.Talents.Add(taken.Id);
                    player.RefreshLoadout();
                    player.SummaryChanged = true;
                    break;

                case HeroAction.Refund:
                    int refund = reader.ReadVarInt();
                    if (!TalentTree.CanRefund(hero, refund)) break;
                    hero.Talents.Remove(TalentTree.Nodes[refund].Id);
                    player.RefreshLoadout();
                    player.SummaryChanged = true;
                    break;

                case HeroAction.ForgetTalents:
                    hero.Talents.Clear();
                    player.RefreshLoadout();
                    player.SummaryChanged = true;
                    break;

                // What comes next all changes what's carried, which a client has already done by these same rules: see NETWORKING.md.
                case HeroAction.Equip:
                    player.Heard(reader.ReadVarInt());
                    int equip = reader.ReadVarInt();
                    WornSlot? place = reader.ReadBool() ? (WornSlot)reader.ReadByte() : null;
                    if (!ItemPlace.Cell(equip).IsCell || hero.Inventory[equip] == null) break;
                    if (!hero.Equip(equip, place))
                    {
                        Note(player, "Your pack is full.");
                        break;
                    }
                    player.RefreshLoadout();
                    break;

                case HeroAction.Unequip:
                    player.Heard(reader.ReadVarInt());
                    var slot = (WornSlot)reader.ReadByte();
                    if (hero.At(ItemPlace.Worn(slot)) == null) break;
                    if (!hero.Unequip(slot))
                    {
                        Note(player, "Your pack is full.");
                        break;
                    }
                    player.RefreshLoadout();
                    break;

                case HeroAction.Move:
                    player.Heard(reader.ReadVarInt());
                    var moving = new ItemPlace(reader.ReadVarInt());
                    var movingTo = new ItemPlace(reader.ReadVarInt());
                    if (hero.Move(moving, movingTo) && !(moving.IsCell && movingTo.IsCell)) player.RefreshLoadout();
                    break;

                case HeroAction.Title:
                    player.Heard(reader.ReadVarInt());
                    var titling = new ItemPlace(reader.ReadVarInt());
                    if (hero.Title(titling, reader.ReadString(Protocol.LongestName)) && titling.IsBar) player.RefreshLoadout();
                    break;

                case HeroAction.Sort:
                    player.Heard(reader.ReadVarInt());
                    hero.Sort();
                    break;

                case HeroAction.Drop:
                    player.Heard(reader.ReadVarInt());
                    var dropping = new ItemPlace(reader.ReadVarInt());
                    if (hero.Take(dropping) is not Item dropped) break;
                    Session!.Ground.Lay(player, dropped);
                    if (!dropping.IsCell) player.RefreshLoadout();
                    break;

                case HeroAction.Sell:
                    player.Heard(reader.ReadVarInt());
                    int sellingAt = reader.ReadVarInt();
                    var selling = new ItemPlace(reader.ReadVarInt());
                    if (sellingAt < 0 || sellingAt >= Session!.Sites.Count) break;
                    if (TableFor(player, sellingAt).Sell(hero, selling) && !selling.IsCell) player.RefreshLoadout();
                    player.SummaryChanged = true;
                    SendMarket(player, sellingAt);
                    break;

                case HeroAction.Buy:
                    player.Heard(reader.ReadVarInt());
                    int site = reader.ReadVarInt();
                    bool buyingBack = reader.ReadBool();
                    int buy = reader.ReadVarInt();
                    int? into = reader.ReadBool() ? reader.ReadVarInt() : null;
                    if (site < 0 || site >= Session!.Sites.Count) break;
                    TableFor(player, site).Buy(hero, buyingBack, buy, into);
                    player.SummaryChanged = true;
                    SendMarket(player, site);
                    break;

                case HeroAction.TakeOffer:
                    player.Heard(reader.ReadVarInt());
                    int offerTab = reader.ReadVarInt();
                    int offerRoll = reader.ReadInt();
                    int offerLevel = reader.ReadVarInt();
                    int offer = reader.ReadVarInt();
                    int? offerInto = reader.ReadBool() ? reader.ReadVarInt() : null;
                    if (BetaVendor.GivesIn(Session!.IsHaven)) BetaVendor.Take(hero, offerTab, offerRoll, offerLevel, offer, offerInto);
                    break;

                case HeroAction.BuyFlasks:
                    while (hero.Flasks < HeroState.MaxFlasks && hero.Gold >= FlaskPrice)
                    {
                        hero.Gold -= FlaskPrice;
                        hero.Flasks++;
                    }
                    player.SummaryChanged = true;
                    break;

                case HeroAction.TravelToSite:
                    int to = reader.ReadVarInt();
                    if (to < 0 || to >= Session!.Sites.Count || Session.IsHaven || player.Out || !AtWaygate(player)) break;
                    SiteState destination = Session.Sites[to];
                    if (!WorldProgress.CanTravelTo(destination) || WaygateSpawn(Session.World, destination.Site) is not Vector2 arrive) break;
                    player.RoadsOpenAt = null;

                    // Out in front of the gate, on the side it's used from, as the middle of a site can be built on.
                    Vector2 outward = (destination.Site.Position - arrive).LengthSquared > 1f ? (destination.Site.Position - arrive).Normalized() : Vector2.UnitY;
                    Teleport(player, TryFindGround(Session.World, arrive + outward * 2f, out Vector2 ground) ? ground : arrive);
                    Broadcast(new SoundEvent { Position = HeroPosition(player), Cue = SoundCue.Waygate }, player.Sim.Position);
                    break;

                case HeroAction.ReturnHome:
                    if (Session!.IsHaven || player.Out || !AtWaygate(player)) break;
                    GoHome();
                    break;

                case HeroAction.CallRoads:
                    if (Session!.IsHaven || player.Out || player.Sim.Health <= 0f || player.Sim.BusyFor > 0f || player.CallingSince != null) break;
                    player.CallingSince = Session.Time;
                    player.CallingFrom = player.Sim.Position;
                    player.CallingHealth = player.Sim.Health;
                    Broadcast(new FlourishEvent { Who = player.GhostId, Kind = Flourish.Use }, player.Sim.Position);
                    break;

                case HeroAction.OpenWay:
                    player.Heard(reader.ReadVarInt());
                    int wayLevel = reader.ReadVarInt();
                    int choice = reader.ReadVarInt();
                    int given = reader.ReadCount(Lands.MostOrderSlots);
                    var ordersCells = new int[given];
                    for (int i = 0; i < given; i++) ordersCells[i] = reader.ReadVarInt();
                    OpenWay(player, wayLevel, choice, ordersCells);
                    break;

                case HeroAction.Reroll:
                    Reroll(player, reader.ReadVarInt());
                    break;

                case HeroAction.CloseWay:
                    if (!Session!.IsHaven || Session.Portal == null) break;
                    Session.Portal = null;
                    Session.PortalChanged = true;
                    SendChangedProgress();
                    Note(null, $"{player.Name} has closed the way.");
                    break;

                case HeroAction.Stow:
                    player.Heard(reader.ReadVarInt());
                    var stowing = new ItemPlace(reader.ReadVarInt());
                    int stowTab = reader.ReadVarInt();
                    int stowCell = reader.ReadVarInt();
                    if (player.Stash != null && player.Stash.Stow(hero, stowing, stowTab, stowCell))
                    {
                        player.StashChanged = true;
                        if (!stowing.IsCell) player.RefreshLoadout();
                    }
                    break;

                case HeroAction.Unstow:
                    player.Heard(reader.ReadVarInt());
                    int fromTab = reader.ReadVarInt();
                    int fromCell = reader.ReadVarInt();
                    int? intoCell = reader.ReadBool() ? reader.ReadVarInt() : null;
                    if (player.Stash != null && player.Stash.Take(hero, fromTab, fromCell, intoCell)) player.StashChanged = true;
                    break;

                case HeroAction.Shift:
                    player.Heard(reader.ReadVarInt());
                    int shiftTab = reader.ReadVarInt();
                    int shiftCell = reader.ReadVarInt();
                    int shiftToTab = reader.ReadVarInt();
                    int shiftToCell = reader.ReadVarInt();
                    if (player.Stash != null && player.Stash.Shift(shiftTab, shiftCell, shiftToTab, shiftToCell)) player.StashChanged = true;
                    break;

                case HeroAction.TravelToPoint:
                    Vector2 point = new Vector2(reader.ReadFloat(), reader.ReadFloat());
                    if (_settings.AllowTeleport && Session!.World.InBounds(point)) Teleport(player, point);
                    break;

                case HeroAction.ThankElder:
                    int elderSite = reader.ReadVarInt();
                    if (elderSite < 0 || elderSite >= Session!.Sites.Count) break;
                    SiteState elder = Session.Sites[elderSite];
                    if (elder.Rewarded || !Session.IsSafe(elder) || Session.IsHaven) break;
                    elder.Rewarded = true;
                    elder.Changed = true;
                    hero.Gold += 45 * Session.World.Plan.Level;
                    Session.GiveExperience(player, 60 * Session.World.Plan.Level);
                    break;

                case HeroAction.PickUp:
                    Session!.Ground.Ask(player, reader.ReadVarInt());
                    break;

                default:
                    throw new NetReadException($"No such action as {action}.");
            }
        }

        // A call to the old roads goes on while the hero stands still and unhurt, and opens them where they stand once it's gone on long enough. They stay open there until they step away.
        private void StepCalling(ServerSession session, ServerPlayer player)
        {
            if (player.RoadsOpenAt is Vector2 open && (player.Sim.Position - open).LengthSquared > Rulebook.RoadsStray * Rulebook.RoadsStray) player.RoadsOpenAt = null;
            if (player.CallingSince is not double since) return;
            if (player.Out || Roads.Broken(player.Sim, player.CallingFrom, player.CallingHealth, player.Actor.Sheet.MaxHealth))
            {
                player.CallingSince = null;
                return;
            }
            player.CallingHealth = MathF.Max(player.CallingHealth, player.Sim.Health);
            if (session.Time - since < Rulebook.CallRoads) return;

            player.CallingSince = null;
            player.RoadsOpenAt = player.Sim.Position;
            OpenPanel(player, InteractKind.Waygate, 0);
        }

        // Whether someone can take the land's roads from where they stand: at a waygate, or where they've called them open.
        private bool AtWaygate(ServerPlayer player)
        {
            if (player.RoadsOpenAt is Vector2 open && (player.Sim.Position - open).LengthSquared <= Rulebook.RoadsStray * Rulebook.RoadsStray) return true;
            foreach (SiteState state in Session!.Sites)
            {
                if (!WorldProgress.HasWaygate(state) || WaygateSpawn(Session.World, state.Site) is not Vector2 gate) continue;
                if ((gate - player.Sim.Position).Length <= GateReach) return true;
            }
            return false;
        }

        // The map table has been asked to open the way to one of the lands it shows this hero at a level they can open, with as many orders given for it as they have slots for, or none. It's
        // worked out here from their own hero, as their machine worked it out, and the orders only come out of their pack if the way opens: either way they're told the truth about what they carry.
        private void OpenWay(ServerPlayer player, int level, int choice, IReadOnlyList<int> ordersCells)
        {
            ServerSession session = Session!;
            if (!session.IsHaven || !player.Hero.CanOpen(level)) return;
            IReadOnlyList<LandOffer> offers = Lands.Offer(player.Hero, level);
            if (choice < 0 || choice >= offers.Count) return;
            if (ordersCells.Count > Lands.OrderSlots(player.Hero) || ordersCells.Distinct().Count() != ordersCells.Count) return;
            foreach (int cell in ordersCells)
            {
                if (!ItemPlace.Cell(cell).IsCell || player.Hero.Inventory[cell] is not { IsOrders: true }) return;
            }

            LandOffer offer = offers[choice];
            LandTerms terms = offer.Terms.Copy();
            foreach (int cell in ordersCells)
            {
                Item orders = player.Hero.Inventory[cell]!;
                player.Hero.Take(ItemPlace.Cell(cell));
                terms.Give(orders);
            }
            terms.Sanitize();

            session.Portal = (offer.Request, terms, offer.Plan.Name);
            session.PortalChanged = true;
            Console.WriteLine($"{player.Name} opened the way to {offer.Plan.Name}, level {offer.Request.Level}, with {terms.Count} omens{(terms.Orders.Count > 0 ? $" and {string.Join(", ", terms.Orders)}" : "")}.");
            SendChangedProgress();
            Broadcast(new SoundEvent { Position = HeroPosition(player), Cue = SoundCue.Waygate }, player.Sim.Position);
            Announce(null, "The way is open", $"{offer.Plan.Name}, level {offer.Request.Level}. Step through the waygate when you're ready.", 5f);
        }

        // Three other lands for a level, for gold that grows with it. What the map table showed for it before is gone for this hero, though a way already open stays open.
        private void Reroll(ServerPlayer player, int level)
        {
            ServerSession session = Session!;
            if (!session.IsHaven || !player.Hero.CanOpen(level)) return;
            int cost = Lands.RerollCost(level);
            if (player.Hero.Gold < cost)
            {
                Note(player, "You can't afford other lands for that level.");
                return;
            }
            player.Hero.Gold -= cost;
            player.Hero.Reroll(level);
            player.SummaryChanged = true;
            Console.WriteLine($"{player.Name} paid {cost} gold for other lands at level {level}.");
        }

        /// <summary>Someone has stepped through the open way: everyone goes, to the land it leads to.</summary>
        public void StepThrough()
        {
            if (_next != null || Session is not { IsHaven: true, Portal: var (request, terms, _) }) return;
            SendHeroesNow();
            _next = (request, terms);
        }

        private void SendUpdates()
        {
            if (Session != null && Tick % ProgressEvery == 0) SendChangedProgress();

            _due.Clear();
            foreach (ServerPlayer player in _players.Values)
            {
                if (!player.InWorld) continue;
                if (Tick % HeroUpdateEvery == 0 && (player.SummaryChanged || player.GearChanged || player.BuffsChanged || player.StashChanged)) SendHero(player, player.GearChanged || player.StashChanged);
                if (Tick % player.SendInterval == 0) _due.Add(player);
            }

            if (_due.Count == 0 || Session == null) return;
            Ghosts.Capture();
            foreach (ServerPlayer player in _due) SendSnapshot(player);
        }

        private void SendSnapshot(ServerPlayer player)
        {
            GhostView view = player.View;
            view.Focus = player.Sim.Position;
            view.ResendGap = Math.Max(player.RoundTrip * NetTime.TicksPerSecond / 1000 / player.SendInterval + 2, 3);

            // A line that is dropping packets is often a line that has too much on it. Sending less is what gives it room.
            player.SendInterval = view.Loss > HeavyLoss ? 4 : view.Loss > ModerateLoss ? 3 : 2;

            BitWriter writer = _snapshot;
            writer.Clear();
            writer.WriteByte((byte)Message.Snapshot);
            writer.WriteVarInt(_epoch);
            writer.WriteUInt(Tick);
            writer.WriteByte((byte)player.SendInterval);
            writer.WriteBool(_players.Count == 1);
            writer.WriteVarInt(Math.Clamp(player.Spare, -100000, 100000));
            player.Sim.Health = player.Actor.Vitals.Health;
            player.Sim.Mana = player.Actor.Vitals.Mana;
            player.Sim.Write(writer);
            writer.WriteFloat(player.Actor.Vitals.Barrier);
            writer.WriteFloat(player.Actor.Vitals.Debt);
            writer.WriteUInt(player.LastClientTime);
            writer.WriteVarUInt((uint)Math.Clamp(Clock() - player.LastClientTimeArrived, 0, 100000));
            writer.WriteUInt(player.NewestCommand);

            int budget = Math.Min(player.Connection.MaxUnreliableSize, Protocol.SnapshotBudget);
            Ghosts.WriteSnapshot(view, writer, budget * 8, Tick);

            byte[] data = writer.ToArray();
            LargestSnapshot = Math.Max(LargestSnapshot, data.Length);
            BytesSent += data.Length;
            player.Connection.Send(data, Delivery.Unreliable);
        }

        private void SendHero(ServerPlayer player, bool gear)
        {
            BitWriter writer = Protocol.Begin(Message.HeroUpdate);
            Protocol.WriteHeroSummary(writer, player.Hero);
            Protocol.WriteBuffs(writer, player.Actor.Buffs);
            writer.WriteBool(gear);
            if (gear)
            {
                writer.WriteVarInt(player.GearAnswered);
                Protocol.WriteGear(writer, player.Hero);
                writer.WriteBool(player.Stash != null);
                if (player.Stash != null) Protocol.WriteStash(writer, player.Stash);
            }
            player.Connection.Send(writer.ToArray(), Delivery.Reliable);
            if (gear) SendWorn(player);
            player.SummaryChanged = false;
            player.GearChanged = false;
            player.BuffsChanged = false;
            player.StashChanged = false;
        }

        // Everyone sees what a hero has on, and it's rarely said, so everyone is told. Whoever arrives later is told as they arrive.
        private void SendWorn(ServerPlayer wearer)
        {
            BitWriter writer = Protocol.Begin(Message.Worn);
            Protocol.WriteWorn(writer, wearer.Id, wearer.Hero);
            byte[] data = writer.ToArray();
            foreach (ServerPlayer hero in _heroes) hero.Connection.Send(data, Delivery.Reliable);
        }

        private void SendChangedProgress()
        {
            ServerSession session = Session!;
            bool any = session.ObjectivesChanged || session.PortalChanged;
            foreach (SiteState state in session.Sites) any |= state.Changed;
            if (!any) return;

            foreach (ServerPlayer player in _players.Values)
            {
                if (player.InWorld) SendProgress(player, everything: false);
            }
            session.ObjectivesChanged = false;
            foreach (SiteState state in session.Sites) state.Changed = false;
        }

        private void SendProgress(ServerPlayer player, bool everything)
        {
            ServerSession session = Session!;
            BitWriter writer = Protocol.Begin(Message.Progress);
            writer.WriteBool(session.Liberated);
            writer.WriteVarInt(session.Falls);
            writer.WriteBool(session.Lost);
            writer.WriteBool(player.Out);
            writer.WriteBool(session.Portal != null);
            if (session.Portal is var (portalRequest, portalTerms, portalName))
            {
                writer.WriteString(portalName);
                Protocol.WriteRequest(writer, portalRequest);
                Protocol.WriteTerms(writer, portalTerms);
            }

            writer.WriteCount(session.Chronicle.Objectives.Count);
            foreach (Objective objective in session.Chronicle.Objectives) writer.WriteVarInt(objective.Progress);

            // Everything means everything that has happened: a world is mostly packs no one has met, and a client starts out knowing those.
            int count = 0;
            foreach (SiteState state in session.Sites) count += Tells(state, everything) ? 1 : 0;
            writer.WriteCount(count);
            for (int i = 0; i < session.Sites.Count; i++)
            {
                SiteState state = session.Sites[i];
                if (!Tells(state, everything)) continue;
                writer.WriteVarInt(i);
                writer.WriteBool(state.Discovered);
                writer.WriteBool(state.Cleared);
                writer.WriteBool(state.Kindled);
                writer.WriteBool(state.Rewarded);
                writer.WriteBool(state.Lit);
                writer.WriteCount(state.Dead.Count);
                foreach (int dead in state.Dead) writer.WriteVarInt(dead);
                writer.WriteCount(state.Opened.Count);
                foreach (int opened in state.Opened) writer.WriteVarInt(opened);
            }
            player.Connection.Send(writer.ToArray(), Delivery.Reliable);
        }

        private static bool Tells(SiteState state, bool everything)
        {
            return state.Changed || (everything && (state.Discovered || state.Cleared || state.Kindled || state.Rewarded || state.Lit || state.Dead.Count > 0 || state.Opened.Count > 0));
        }

        private void SendParty()
        {
            BitWriter writer = Protocol.Begin(Message.Party);
            var greeted = _players.Values.Where(p => p.Greeted).ToList();
            writer.WriteCount(greeted.Count);
            foreach (ServerPlayer player in greeted)
            {
                writer.WriteVarInt(player.Id);
                writer.WriteString(player.Name);
            }
            byte[] data = writer.ToArray();
            foreach (ServerPlayer player in greeted) player.Connection.Send(data, Delivery.Reliable);
        }

        /// <summary>For a scripted run: one of everything, in front of the first hero, standing still or not.</summary>
        public void Parade(string which, bool fighting = false)
        {
            if (Session != null && _heroes.Count > 0) _encounters.Parade(Ecs, Session, _heroes[0].Sim.Position, which, fighting);
        }

        /// <summary>For a scripted run: home to the Haven from wherever this is, as if through a waygate.</summary>
        public void SendHome()
        {
            if (Session != null && !Session.IsHaven) GoHome();
        }

        /// <summary>For a scripted run: out of the Haven to one of the lands the map table shows the first hero at the deepest level they can open, at once, or only opening the way to it.</summary>
        public void Venture(int choice, bool step = true)
        {
            if (Session is not { IsHaven: true } || _heroes.Count == 0) return;
            OpenWay(_heroes[0], _heroes[0].Hero.LandLevel, choice, Array.Empty<int>());
            if (step) StepThrough();
        }

        /// <summary>For a scripted run: loot in front of the first hero.</summary>
        public void ShowLoot(string which)
        {
            if (Session != null && _heroes.Count > 0) Session.Ground.Show(_heroes[0].Sim.Position, which);
        }

        /// <summary>Writes the world, and where the first hero stands in it, into a save. The hero's own record is added on their machine.</summary>
        public void CaptureWorld(SaveGame save)
        {
            if (Session == null) return;
            Session.Capture(save);
            if (_heroes.Count == 0) return;

            // A land the one who keeps it has fallen in is over for them, whatever's still going on in it.
            ServerPlayer host = _heroes[0];
            if (host.Out) save.InLand = false;
            if (!save.InLand) return;
            save.PlayerX = host.Sim.Position.X;
            save.PlayerZ = host.Sim.Position.Y;
            save.Health = host.Actor.Vitals.Health;
        }
    }
}
