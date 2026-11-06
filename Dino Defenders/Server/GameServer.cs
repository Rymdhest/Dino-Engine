using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Skills;
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
        public Offering? Offering;      // What was paid to make that world what it is.
        public SaveGame? Save;
        public bool AllowTeleport;      // For scripted runs: a client may ask to be put anywhere.
        public Vector2? Start;
        public string? StartSite;
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
        private const float ModerateLoss = 0.08f;
        private const float HeavyLoss = 0.2f;

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
        private Offering _offering = new();
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
            _systems.Add(new CharacterMotorSystem());
            _systems.Add(new VelocitySystem());
            _systems.Add(new FixToTerrainHeightSystem());

            BeginWorld(settings.Request, settings.Offering ?? new Offering(), settings.Save);
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
            }

            SendUpdates();
            Tick++;
        }

        public void Dispose() => Close("The host has left.");

        /// <summary>Ends the game for everyone, telling them why.</summary>
        public void Close(string reason)
        {
            foreach (ServerPlayer player in _players.Values) player.Connection.Close(reason);
            foreach (INetTransport transport in _transports) transport.Dispose();
            _transports.Clear();
        }

        private void BeginWorld(WorldRequest request, Offering offering, SaveGame? save)
        {
            _epoch++;
            _heroes.Clear();
            foreach (ServerPlayer player in _players.Values)
            {
                player.InWorld = false;
                player.Stocks.Clear();
                player.Asked.Clear();
            }

            Session = null;
            Ecs.ClearAllEntitiesExcept();
            Ecs.DirtyEntities.Clear();

            _offering = offering;
            _request = offering.Shape(request);
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
            Session = new ServerSession(this, world, _request, _offering);
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

            var actor = new Actor(Team.Heroes, HeroBuild.Compile(player.Hero), start, HeroRules.Radius) { Player = player, Body = "hero" };
            player.Actor = actor;
            player.Stolen = null;
            player.Kit = new HeroKit(player.Hero, actor.Sheet);
            player.Respawn = session.World.Sites[0].Position;
            if (first && _saved != null)
            {
                if (_saved.Health > 0f) actor.Vitals.Health = Math.Min(_saved.Health, actor.Sheet.MaxHealth);
                if (_saved.RespawnX >= 0f) player.Respawn = new Vector2(_saved.RespawnX, _saved.RespawnZ);
            }

            player.Sim = HeroSimState.At(start, actor.Vitals.Mana);
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

            if (first && _saved != null && _saved.PlayerX >= 0f)
            {
                // Someone can stand closer to a wall than the passable map allows for, so the spot itself may not be on it.
                if (TryFindGround(world, new Vector2(_saved.PlayerX, _saved.PlayerZ), out Vector2 ground)) return ground;
            }
            if (_settings.Start.HasValue) return _settings.Start.Value;
            if (_settings.StartSite != null)
            {
                foreach (WorldSite site in world.Sites)
                {
                    // The middle of a site can be built on.
                    if (site.Kind.ToString() == _settings.StartSite) return TryFindGround(world, site.Position, out Vector2 ground) ? ground : site.Position;
                }
            }
            return world.Sites[0].Position;
        }

        private static bool TryFindGround(WorldData world, Vector2 near, out Vector2 ground)
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
                Paused = alone && command.Has(HeroButtons.Frozen);

                Actor actor = player.Actor;
                if (!ReferenceEquals(player.Kit.Sheet, actor.Sheet)) player.Kit = new HeroKit(player.Hero, actor.Sheet);
                Brief(player);

                float healthBefore = player.Sim.Health;
                HeroStepEvents events = HeroRules.Step(ref player.Sim, command, player.Kit, _mover!);
                actor.Vitals.Mana = player.Sim.Mana;
                actor.Vitals.Health = player.Sim.Health;
                if (player.Sim.Health < healthBefore) actor.HealthSpent += (healthBefore - player.Sim.Health) / actor.Sheet.MaxHealth * 100f;

                if (!Paused)
                {
                    if (actor.Vitals.Health > 0f) Act(session, player, command, events);
                    else UpdateDown(player);
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

        // What's happened to a hero since their last step that their step has to know about: only the server can say.
        private static void Brief(ServerPlayer player)
        {
            Actor actor = player.Actor;
            Afflictions afflictions = actor.Afflictions;
            player.Sim.Mana = actor.Vitals.Mana;
            player.Sim.Health = actor.Vitals.Health;
            player.Sim.Held = MathF.Max(afflictions.FreezeLeft, afflictions.StunLeft);
            player.Sim.ActionRate = afflictions.ActionRate;
            player.Sim.MoveRate = afflictions.ActionRate;
            player.Sim.State = actor.State;
            player.Sim.Counters = actor.Counters;
        }

        private void Act(ServerSession session, ServerPlayer player, in HeroCommand command, HeroStepEvents events)
        {
            Vector3 position = HeroPosition(player);
            Vector2 at = player.Sim.Position;
            SkillProfile? skill = player.Sim.Using >= 0 ? player.Kit.Slots[player.Sim.Using] : null;

            if ((events & HeroStepEvents.Used) != 0 && skill != null)
            {
                float rate = MathF.Max(player.Sim.ActionRate, 0.1f);
                session.Combat.Perform(player.Entity, skill.Skill, player.Sim.ImpactIn / rate, player.Sim.BusyFor / rate, player.Sim.Aim);
                MarkForFriends(session, player, skill, rate);
            }
            if ((events & HeroStepEvents.Dashed) != 0) session.Combat.Stop(player.Entity);

            if ((events & HeroStepEvents.Landed) != 0 && skill != null)
            {
                session.Combat.Release(player.Actor, skill, at, player.Sim.Aim, player.Sim.AimReach, command.ViewLag);
            }

            // Its owner saw all of this the moment they pressed; it's everyone else who needs telling.
            const HeroStepEvents visible = HeroStepEvents.Landed | HeroStepEvents.Used | HeroStepEvents.Dashed;
            if ((events & visible) != 0)
            {
                Broadcast(new HeroMoveEvent { Position = position, Aim = player.Sim.Aim, Move = events & visible, Skill = skill?.Skill.Number ?? 0 }, at, except: player);
            }

            if (command.Has(HeroButtons.Flask)) DrinkFlask(player, position);
            if (command.Has(HeroButtons.Interact)) _interaction.Interact(Ecs, session, player);
            session.Ground.Serve(player);
        }

        // Whoever's casting has seen this since they pressed. Their friends are told, as anything of a hero's is worth knowing about if you're standing nearby.
        private void MarkForFriends(ServerSession session, ServerPlayer player, SkillProfile skill, float rate)
        {
            if (_heroes.Count < 2 || !HeroRules.ShowsArea(skill)) return;
            GroundShape shape = GroundShape.Of(skill, player.Sim.Position, player.Sim.Aim, player.Sim.AimReach);
            session.Combat.Warn(shape, skill.Skill.Number, MarkTier.Plain, false, player.Sim.ImpactIn / rate + skill.Delay, player.Id);
        }

        private void DrinkFlask(ServerPlayer player, Vector3 position)
        {
            Actor actor = player.Actor;
            ActorSheet sheet = actor.Sheet;
            if (player.Hero.Flasks <= 0 || actor.Vitals.Health >= sheet.MaxHealth) return;
            player.Hero.Flasks--;
            player.SummaryChanged = true;
            float heal = sheet.MaxHealth * HeroRules.FlaskHeal * sheet.FlaskScale;
            Hits.Heal(sheet, ref actor.Vitals, heal);
            Session!.Combat.SetOff(actor, TriggerEvent.Flask, actor.Position);
            Broadcast(new SoundEvent { Position = position, Cue = SoundCue.Flask }, position.Xz);
            Broadcast(new TextEvent
            {
                Position = position + Vector3.UnitY * 5.4f,
                Text = $"+{heal:0}",
                Colour = new Vector3(0.4f, 1f, 0.5f),
                Size = 18f,
                Life = 1.2f,
            }, position.Xz);
        }

        // A fallen hero wakes at the last shrine they lit, a little poorer.
        private void UpdateDown(ServerPlayer player)
        {
            if (!player.Sim.Down)
            {
                player.Sim.Down = true;
                player.DownFor = 0f;
                int lost = player.Hero.Gold / 10;
                player.Hero.Gold -= lost;
                player.SummaryChanged = true;
                Announce(player, "You have fallen", lost > 0 ? $"{lost} gold lost on the road" : "The land goes on without you");
                Session!.HeroFell();
            }

            player.DownFor += NetTime.TickSeconds;
            if (player.DownFor < HeroRules.DeathDelay) return;

            player.Sim.Down = false;
            player.Actor.Dead = false;
            player.Actor.Afflictions.Clear();
            player.Actor.Vitals = Vitals.Full(player.Actor.Sheet);
            player.Hero.Flasks = HeroState.MaxFlasks;
            player.SummaryChanged = true;
            Teleport(player, player.Respawn);
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

        // Each hero has their own table: what one buys isn't taken from another, and it turns over as they free more lands.
        private List<Item> StockFor(ServerPlayer hero, int siteIndex)
        {
            if (hero.Stocks.TryGetValue(siteIndex, out List<Item>? stock)) return stock;

            ServerSession session = Session!;
            stock = new List<Item>();
            var random = new WorldRandom(session.World.Plan.Seed).Fork("market").Fork(session.Sites[siteIndex].Site.Id).Fork(hero.Hero.WorldsCleared);
            int level = session.World.Plan.Level + 1;
            for (int i = 0; i < StockSize; i++) stock.Add(ItemGenerator.Roll(level, random.Fork(i), rarityBonus: 0.4f));
            hero.Stocks[siteIndex] = stock;
            return stock;
        }

        private void SendMarket(ServerPlayer hero, int siteIndex)
        {
            BitWriter writer = Protocol.Begin(Message.Market);
            writer.WriteVarInt(siteIndex);
            Protocol.WriteItems(writer, StockFor(hero, siteIndex));
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

            if (_players.Values.Count(p => p.Greeted) >= Protocol.MaxPlayers)
            {
                Refuse(player, $"This game is full: {Protocol.MaxPlayers} heroes at most.");
                return;
            }

            player.Name = name.Length > 0 ? name : $"Hero {player.Id}";
            player.Hero = hero;
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
            Protocol.WriteOffering(writer, _offering);
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
                    foreach (Mod mod in taken.Mods)
                    {
                        if (mod.Kind == ModKind.GrantSkill) HeroBuild.OfferToBar(hero, SkillCatalog.Get(mod.Skill));
                    }
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

                case HeroAction.AssignSlot:
                    int slotIndex = reader.ReadByte();
                    int skillNumber = reader.ReadVarInt();
                    if (slotIndex >= HeroState.SkillSlots) break;
                    hero.Bar[slotIndex] = SkillCatalog.IsNumber(skillNumber) ? SkillCatalog.Get(skillNumber).Id : null;
                    player.Kit = new HeroKit(hero, player.Actor.Sheet);
                    player.SummaryChanged = true;
                    break;

                case HeroAction.Equip:
                    int equip = reader.ReadVarInt();
                    WornSlot? place = reader.ReadBool() ? (WornSlot)reader.ReadByte() : null;
                    if (equip < 0 || equip >= hero.Inventory.Count) break;
                    if (!hero.Equip(hero.Inventory[equip], place))
                    {
                        Note(player, "Your pack is full.");
                        break;
                    }
                    player.RefreshLoadout();
                    player.GearChanged = true;
                    break;

                case HeroAction.Unequip:
                    int slot = reader.ReadByte();
                    if (!Enum.IsDefined((WornSlot)slot)) break;
                    hero.Unequip((WornSlot)slot);
                    player.RefreshLoadout();
                    player.GearChanged = true;
                    break;

                case HeroAction.Sell:
                    int sell = reader.ReadVarInt();
                    if (sell < 0 || sell >= hero.Inventory.Count) break;
                    Item sold = hero.Inventory[sell];
                    hero.Inventory.RemoveAt(sell);
                    hero.Gold += sold.SellValue;
                    player.GearChanged = true;
                    player.SummaryChanged = true;
                    break;

                case HeroAction.Buy:
                    int site = reader.ReadVarInt();
                    int buy = reader.ReadVarInt();
                    if (site < 0 || site >= Session!.Sites.Count) break;
                    List<Item> stock = StockFor(player, site);
                    if (buy < 0 || buy >= stock.Count) break;
                    Item bought = stock[buy];
                    if (hero.Gold < bought.BuyValue || !hero.Pickup(bought)) break;
                    hero.Gold -= bought.BuyValue;
                    stock.RemoveAt(buy);
                    player.GearChanged = true;
                    player.SummaryChanged = true;
                    SendMarket(player, site);
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
                    if (to < 0 || to >= Session!.Sites.Count) break;
                    SiteState destination = Session.Sites[to];
                    if (destination.Kindled || destination.Site.Kind == SiteKind.Landing)
                    {
                        // The middle of a site can be built on, and a shrine's altar stands there.
                        Teleport(player, TryFindGround(Session.World, destination.Site.Position, out Vector2 ground) ? ground : destination.Site.Position);
                    }
                    break;

                case HeroAction.TravelToPoint:
                    Vector2 point = new Vector2(reader.ReadFloat(), reader.ReadFloat());
                    if (_settings.AllowTeleport && Session!.World.InBounds(point)) Teleport(player, point);
                    break;

                case HeroAction.Waygate:
                    WorldRequest road = Protocol.ReadRequest(reader);
                    Offering offering = Protocol.ReadOffering(reader);
                    if (Pay(player, offering, road.Level)) BeginWorld(road, offering, null);
                    break;

                case HeroAction.ThankElder:
                    int elderSite = reader.ReadVarInt();
                    if (elderSite < 0 || elderSite >= Session!.Sites.Count) break;
                    SiteState elder = Session.Sites[elderSite];
                    if (elder.Rewarded || !Session.IsSafe(elder)) break;
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

        // Whoever steps through pays, and for everyone going with them. It's asked here and nowhere else: what a client shows is only what it thinks this will say.
        private bool Pay(ServerPlayer player, Offering offering, int level)
        {
            if (!offering.Any) return true;

            if (offering.Peril > Offering.Reach(player.Hero))
            {
                Note(player, "The gate won't bear that much peril from you yet.");
                return false;
            }

            long price = offering.Price(level);
            if (price > player.Hero.Gold)
            {
                Note(player, $"That offering costs {price} gold.");
                return false;
            }

            player.Hero.Gold -= (int)price;
            player.SummaryChanged = true;
            return true;
        }

        private void SendUpdates()
        {
            if (Session != null && Tick % ProgressEvery == 0) SendChangedProgress();

            _due.Clear();
            foreach (ServerPlayer player in _players.Values)
            {
                if (!player.InWorld) continue;
                if (Tick % HeroUpdateEvery == 0 && (player.SummaryChanged || player.GearChanged || player.BuffsChanged)) SendHero(player, player.GearChanged);
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
            Protocol.WriteBuffs(writer, player.Actor.Buffs, player.Stolen);
            writer.WriteBool(gear);
            if (gear) Protocol.WriteGear(writer, player.Hero);
            player.Connection.Send(writer.ToArray(), Delivery.Reliable);
            if (gear) SendWorn(player);
            player.SummaryChanged = false;
            player.GearChanged = false;
            player.BuffsChanged = false;
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
            bool any = session.ObjectivesChanged;
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
            writer.WriteBool(session.Broken);

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
                writer.WriteCount(state.Dead.Count);
                foreach (int dead in state.Dead) writer.WriteVarInt(dead);
                writer.WriteCount(state.Opened.Count);
                foreach (int opened in state.Opened) writer.WriteVarInt(opened);
            }
            player.Connection.Send(writer.ToArray(), Delivery.Reliable);
        }

        private static bool Tells(SiteState state, bool everything)
        {
            return state.Changed || (everything && (state.Discovered || state.Cleared || state.Kindled || state.Rewarded || state.Dead.Count > 0 || state.Opened.Count > 0));
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

        /// <summary>Writes the world, and where the first hero stands in it, into a save. The hero's own record is added on their machine.</summary>
        /// <summary>For a scripted run: one of everything, in front of the first hero, standing still or not.</summary>
        public void Parade(string which, bool fighting = false)
        {
            if (Session != null && _heroes.Count > 0) _encounters.Parade(Ecs, Session, _heroes[0].Sim.Position, which, fighting);
        }

        /// <summary>For a scripted run: loot in front of the first hero.</summary>
        public void ShowLoot(string which)
        {
            if (Session != null && _heroes.Count > 0) Session.Ground.Show(_heroes[0].Sim.Position, which);
        }

        public void CaptureWorld(SaveGame save)
        {
            if (Session == null) return;
            Session.Capture(save);
            if (_heroes.Count == 0) return;

            ServerPlayer host = _heroes[0];
            save.PlayerX = host.Sim.Position.X;
            save.PlayerZ = host.Sim.Position.Y;
            save.Health = host.Actor.Vitals.Health;
            save.RespawnX = host.Respawn.X;
            save.RespawnZ = host.Respawn.Y;
        }
    }
}
