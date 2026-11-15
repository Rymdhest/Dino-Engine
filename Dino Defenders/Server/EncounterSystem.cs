using Dino_Defenders.Books;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Server
{
    /// <summary>
    /// Fills places with their people as a hero comes near, and empties them again when every hero has left. Who appears depends on
    /// what has happened there: the dead stay dead, opened chests stay open, and a village under threat keeps its merchants indoors.
    /// </summary>
    public class EncounterSystem : SystemBase
    {
        private const float ShrineReach = 6.5f;     // It's used from one side of the altar, and has to reach anyone standing at the far side.
        private const float GateFromSpawn = 14f;    // Metres from where a waygate is used to the gate itself, at the most.
        private const float TableFromSpawn = 8f;    // Metres from the map table's spawn to the table, at the most.
        private const float TableReach = 3.2f;      // Past its corners: anyone who can stand beside it, kept a body's width off it, is well inside, with a click's margin to spare.
        private const float ChestScale = 1.35f;     // A size bigger than life, like anything else that matters seen from up where the camera is.
        private const float WakeDistance = 100f;
        private const float SleepDistance = 160f;
        private const float GuardArcherChance = 0.45f;
        private const float CheckInterval = 0.25f;
        private const int MostWakings = 4;      // At a check. A hero stepping out of a waygate is surrounded by places, and they needn't all arrive in one tick.
        private const int FirstSteps = 2;       // Land levels whose places are led by something Magic, not Rare, as that's where someone is still learning what a land is.
        private const float DummyScale = 1.25f;
        private const float DummyTurn = 0.35f;  // Radians either side of facing the camera, which looks north, so that a row of them doesn't stand to attention.
        private const int SmallGroup = 5;       // The training ground's dummies come one alone, then this many, then the rest.

        private readonly GameServer _server;
        private double _nextCheck;

        public EncounterSystem(GameServer server)
            : base(new BitMask(typeof(HealthComponent)))
        {
            _server = server;
        }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            ServerSession? session = _server.Session;
            if (session == null || session.Time < _nextCheck || session.Heroes.Count == 0) return;
            _nextCheck = session.Time + CheckInterval;

            session.UpdateDiscovery();
            session.Tend();

            int wakings = 0;
            for (int i = 0; i < session.Sites.Count; i++)
            {
                SiteState state = session.Sites[i];
                float distance = session.DistanceToAnyHero(state.Site.Position) - state.Site.Radius;
                if (!state.Active && distance < WakeDistance && wakings < MostWakings)
                {
                    wakings++;
                    Wake(world, session, state, i);
                }
                else if (state.Active && distance > SleepDistance) Sleep(world, state);
            }
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        private void Sleep(ECSWorld world, SiteState state)
        {
            foreach (Entity entity in state.Live) world.DestroyEntity(entity);
            state.Live.Clear();
            state.Active = false;
        }

        private void Wake(ECSWorld world, ServerSession session, SiteState state, int siteIndex)
        {
            state.Active = true;
            var random = new WorldRandom(session.World.Plan.Seed).Fork("people").Fork(siteIndex);
            var musters = new Dictionary<SpawnRole, PackOrders>();

            int dummies = 0;
            for (int i = 0; i < state.Site.Spawns.Count; i++)
            {
                SiteSpawn spawn = state.Site.Spawns[i];
                WorldRandom spawnRandom = random.Fork(i);
                switch (spawn.Role)
                {
                    case SpawnRole.Raider:
                    case SpawnRole.Guard:
                    case SpawnRole.Undead:
                    case SpawnRole.Boss:
                    case SpawnRole.Beast:
                    case SpawnRole.Cultist:
                    case SpawnRole.Rival:
                        // Whoever stands here by the rule the map table counted the land by: an arena's second tyrant is only let in on a land whose orders asked for two.
                        // A place set free stays free, whoever a newer build would have put there.
                        if (!LandCensus.Stands(spawn.Role, session.Terms) || state.Dead.Contains(i) || state.Cleared) break;
                        SpawnEnemy(world, session, state, siteIndex, i, spawn, Orders(session, state, spawn, random, musters), spawnRandom);
                        break;
                    case SpawnRole.Villager:
                        SpawnVillager(world, session, state, siteIndex, spawn, spawnRandom, stationary: false);
                        break;
                    case SpawnRole.Spectator:
                        SpawnSpectator(world, session, state, siteIndex, spawn, spawnRandom);
                        break;
                    case SpawnRole.Merchant:
                        // A land's people hide from the Reich, and trade with whoever isn't it.
                        SpawnMerchant(world, session, state, siteIndex, i, spawn, spawnRandom);
                        break;
                    case SpawnRole.Elder:
                        SpawnElder(world, session, state, siteIndex, i, spawn, spawnRandom);
                        break;
                    case SpawnRole.BetaVendor:
                        if (BetaVendor.GivesIn(session.IsHaven)) SpawnBetaVendor(world, session, state, siteIndex, i, spawn, spawnRandom);
                        break;
                    case SpawnRole.Chest:
                        PlaceChest(world, _server, session, state, siteIndex, i, opening: false);
                        break;
                    case SpawnRole.Altar:
                        // A shrine blesses once, and is spent: nothing is left to use.
                        if (state.Kindled) break;
                        SpawnMarker(world, session, state, siteIndex, i, spawn, InteractKind.Shrine, ShrinePrompt(session, state), ShrineReach);
                        break;
                    case SpawnRole.Waygate:
                        // The Haven's is only ever stepped through, once the way is open, and that's a portal of its own.
                        if (session.IsHaven) break;
                        SpawnMarker(world, session, state, siteIndex, i, spawn, InteractKind.Waygate, "Travel the old roads", 6f);
                        if (state.Lit) LightGate(world, _server, session, state, spawn.Position);
                        break;
                    case SpawnRole.Stash:
                        SpawnMarker(world, session, state, siteIndex, i, spawn, InteractKind.Stash, "Open your stash", 4.5f);
                        break;
                    case SpawnRole.MapTable:
                        SpawnTableMarker(world, session, state, siteIndex, i, spawn);
                        break;
                    case SpawnRole.Workbench:
                        SpawnMarker(world, session, state, siteIndex, i, spawn, InteractKind.Workbench, "Work on your gear", 4.6f);
                        break;
                    case SpawnRole.GamingTable:
                        SpawnMarker(world, session, state, siteIndex, i, spawn, InteractKind.GamingTable, "Try your luck", 4.6f);
                        break;
                    case SpawnRole.Keeper:
                        SpawnKeeper(world, session, state, siteIndex, spawn, spawnRandom);
                        break;
                    case SpawnRole.Dummy:
                        SpawnDummy(world, session, state, spawn, dummies == 0 ? 0 : dummies <= SmallGroup ? 1 : 2);
                        dummies++;
                        break;
                    case SpawnRole.Captive:
                        // What was held at a place is gone once it's been freed: only its cage's open door is left.
                        if (!state.Cleared) SpawnCaptive(world, session, state, siteIndex, spawn, spawnRandom);
                        break;
                    case SpawnRole.Returning:
                        if (state.Cleared) SpawnVillager(world, session, state, siteIndex, spawn, spawnRandom, stationary: false);
                        break;
                    case SpawnRole.Trader:
                        if (state.Cleared) SpawnMerchant(world, session, state, siteIndex, i, spawn, spawnRandom);
                        break;
                }
            }
            HangDoors(world, session, state, siteIndex);
        }

        /// <summary>
        /// A place has just been freed (PLACES.md): whoever of it comes back once it is comes out now, and anyone who trades there opens up. What was caged there goes by itself, as its cage
        /// opens (CaptiveSystem).
        /// </summary>
        public void Freed(ECSWorld world, ServerSession session, SiteState state)
        {
            if (!state.Active) return;
            int siteIndex = session.Sites.IndexOf(state);
            if (siteIndex < 0) return;
            var random = new WorldRandom(session.World.Plan.Seed).Fork("people").Fork(siteIndex);
            for (int i = 0; i < state.Site.Spawns.Count; i++)
            {
                SiteSpawn spawn = state.Site.Spawns[i];
                if (spawn.Role == SpawnRole.Returning) SpawnVillager(world, session, state, siteIndex, spawn, random.Fork(i), stationary: false);
                else if (spawn.Role == SpawnRole.Trader) SpawnMerchant(world, session, state, siteIndex, i, spawn, random.Fork(i));
            }
        }

        // A beast in a cage or chained in the open (PlacePlan.Cages): it isn't anyone's to fight, so it has no body in the rules, only its look, and it stands where it's held until the
        // place is freed.
        private void SpawnCaptive(ECSWorld world, ServerSession session, SiteState state, int siteIndex, SiteSpawn spawn, WorldRandom random)
        {
            PlacePlan? plan = state.Site.Place;
            if (plan == null) return;
            int cage = -1;
            for (int k = 0; k < plan.Cages.Count; k++)
            {
                if ((plan.Cages[k].Captive - spawn.Position).LengthSquared < 0.36f) { cage = k; break; }
            }
            if (cage < 0) return;
            PlaceCage held = plan.Cages[cage];
            // Penned rather than caged, it wanders its paddock.
            int paddock = -1;
            for (int k = 0; k < plan.Paddocks.Count; k++)
            {
                PlacePaddock pen = plan.Paddocks[k];
                Vector2 offset = spawn.Position - pen.Centre;
                Vector2 ax = new Vector2(MathF.Cos(pen.Yaw), -MathF.Sin(pen.Yaw)), az = new Vector2(MathF.Sin(pen.Yaw), MathF.Cos(pen.Yaw));
                if (MathF.Abs(Vector2.Dot(offset, ax)) <= pen.Half.X && MathF.Abs(Vector2.Dot(offset, az)) <= pen.Half.Y) { paddock = k; break; }
            }
            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            state.Live.Add(world.CreateEntity("Captive",
                new NetIdentityComponent(_server.Ghosts.NextId(), importance: 0.6f),
                new PositionComponent(position),
                new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitY, held.Facing)),
                new ScaleComponent(new Vector3(held.Scale)),
                new VisualComponent(held.Look, VisualKind.Character),
                new VelocityComponent(),
                new FixToTerrainHeightComponent(),
                new CaptiveComponent { SiteIndex = siteIndex, Cage = cage, Wait = random.Range(0f, 4f), Paddock = paddock }));
        }

        // Every cage's door, shut while the place is held and open once it's been freed; they swing open as it is (CaptiveSystem).
        private void HangDoors(ECSWorld world, ServerSession session, SiteState state, int siteIndex)
        {
            if (state.Site.Place is not PlacePlan plan) return;
            foreach (PlaceCage cage in plan.Cages)
            {
                if (cage.Door.Length == 0) continue;
                float height = session.World.Height.Sample(cage.Middle);
                float open = state.Cleared ? 1f : 0f;
                state.Live.Add(world.CreateEntity("Cage door",
                    new NetIdentityComponent(_server.Ghosts.NextId(), importance: 0.5f),
                    new PositionComponent(new Vector3(cage.Hinge.X, height, cage.Hinge.Y)),
                    new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitY, cage.Yaw + cage.Swing * open)),
                    new ScaleComponent(new Vector3(1f)),
                    new VisualComponent(cage.Door, VisualKind.Prop),
                    new CageDoorComponent { SiteIndex = siteIndex, Shut = cage.Yaw, Swing = cage.Swing, Open = open }));
            }
        }

        /// <summary>What a shrine offers: kindling, if the Reich put it out, and its blessing in a village, where it never went out. Either is once, and then it's spent.</summary>
        public static string ShrinePrompt(ServerSession session, SiteState state)
        {
            BuffDefinition blessing = BlessingOf(session, state);
            if (Keeps(state.Site.Kind) || state.Kindled) return $"Pray for {blessing.Name.Replace("Blessing of ", "")}";
            return "Kindle the shrine";
        }

        /// <summary>Whether a place's shrine was never put out: a village's, or the Haven's.</summary>
        public static bool Keeps(SiteKind kind) => kind is SiteKind.Village or SiteKind.Haven;

        /// <summary>Which blessing a shrine gives, which is always the same one.</summary>
        public static BuffDefinition BlessingOf(ServerSession session, SiteState state)
        {
            var random = new WorldRandom(session.World.Plan.Seed).Fork("blessing").Fork(state.Site.Id);
            return random.Pick(BuffCatalog.Blessings);
        }

        /// <summary>What a blessing gives and for how long, as one line under its name.</summary>
        public static string BlessingText(BuffDefinition blessing) =>
            $"{string.Join(", ", blessing.Mods.Select(mod => ModText.Describe(mod)))} for {Say.Seconds(blessing.Duration)}. Your flasks are full.";

        // Everyone of a folk at one place answers to the same orders.
        private static PackOrders Orders(ServerSession session, SiteState state, in SiteSpawn spawn, WorldRandom random, Dictionary<SpawnRole, PackOrders> musters)
        {
            SpawnRole folk = spawn.Role is SpawnRole.Guard or SpawnRole.Boss ? SpawnRole.Raider : spawn.Role;
            if (!musters.TryGetValue(folk, out PackOrders? orders))
            {
                orders = new PackOrders(session.World, state.Site, folk, LevelAt(session, state), state.Wild, random.Fork("pack"), session.Terms);
                musters.Add(folk, orders);
            }
            return orders;
        }

        /// <summary>How strong what's found at a place is, which is the land's level everywhere in it: a land's level is the whole of what makes one harder than another. In the Haven, the land the strongest climber there would go to next.</summary>
        public static int LevelAt(ServerSession session, SiteState state) => session.IsHaven ? Math.Max(1, session.Heroes.Count > 0 ? session.Heroes.Max(hero => hero.Hero.LandLevel) : 1) : session.World.Plan.Level;

        private void SpawnEnemy(ECSWorld world, ServerSession session, SiteState state, int siteIndex, int spawnIndex, SiteSpawn spawn, PackOrders orders, WorldRandom random)
        {
            int level = LevelAt(session, state);
            bool twin = LandCensus.IsTwin(session.World, session.Terms, state.Site, spawnIndex);
            bool boss = LandCensus.IsTyrant(session.World, session.Terms, state.Site, spawnIndex);
            WorldRandom kinds = random.Fork("kind");

            EnemyKind kind;
            Rarity rarity;
            int traits;
            // What holds a land is the tyrant its story names, and what stands watch over what the Reich has taken is its soldiers.
            if (twin) (kind, rarity, traits) = (Bestiary.Find(session.Chronicle.SecondKind) ?? Bestiary.Tyrant, Rarity.Unique, Bestiary.NoTraits);
            else if (boss) (kind, rarity, traits) = (Bestiary.Find(session.Chronicle.BossKind) ?? Bestiary.Tyrant, Rarity.Unique, Bestiary.NoTraits);
            else if (spawn.Role == SpawnRole.Guard) (kind, rarity, traits) = (kinds.Chance(GuardArcherChance) ? Bestiary.Marksman : Bestiary.Legionary, Rarity.Normal, Bestiary.NoTraits);
            else (kind, rarity, traits) = orders.Draw(spawnIndex, kinds);

            // The people of a place are what it's worth stopping for: one of them leads, and is more than the rest, and they have their hard cases.
            if (!boss && !state.Wild)
            {
                float bred = 1f + session.Terms.ValueOf(Omens.Bloodlines) / 100f;
                if (spawnIndex == FirstHostile(state.Site)) rarity = session.World.Plan.Level <= FirstSteps ? Rarity.Magic : Rarity.Rare;
                else if (kinds.Chance((session.Terms.Has(Omens.Warhost) ? Rulebook.WarhostRareChance : Rulebook.RareChance) * bred)) rarity = Rarity.Rare;
                else if (rarity == Rarity.Normal && kinds.Chance(Rulebook.MagicChance * bred)) rarity = Rarity.Magic;
                if (rarity == Rarity.Normal && session.Terms.Has(OrdersCatalog.Bred)) rarity = Rarity.Magic;
            }
            traits = Bestiary.Fill(traits, rarity, random.Fork("traits"));
            if (rarity == Rarity.Rare && session.Terms.Has(OrdersCatalog.Ledger)) traits = Bestiary.OneMore(traits, random.Fork("ledger"));

            EnemyPower power = Bestiary.PowerOf(rarity);
            float scale = kind.Scale * (rarity == Rarity.Normal ? random.Range(0.94f, 1.06f) : power.Scale);
            string model = Bestiary.ModelOf(kind, random);

            Dino_Defenders.Progression.Item[]? gear = null;
            string? title = null;
            Loadout loadout = boss ? Bestiary.Boss(kind, level, traits, random.Fork("boss"), out gear, out title, session.Bestiary.Worn.Concat(session.TyrantMods()).ToList()) : session.Bestiary.Shared(kind, level, rarity, traits);

            string name = twin ? session.Chronicle.SecondBeast : boss ? session.Chronicle.Beast : Bestiary.NameOf(kind, traits);
            if (title != null) name = $"{name}, {title}";

            var enemy = new EnemyComponent { Name = name, Rarity = rarity, Level = level, Holds = siteIndex + 1 };
            var brain = new EnemyBrainComponent
            {
                SiteIndex = siteIndex,
                SpawnIndex = spawnIndex,
                Home = spawn.Position,
                AggroRadius = kind.AggroRadius * session.Terms.Notice,
                LeashRadius = kind.LeashRadius,
                KeepsAway = kind.KeepsAway,
                Experience = Bestiary.ExperienceAt(level, power.Health * kind.Health),
                Abilities = Bestiary.AbilitiesOf(kind, traits),
                Cooldown = random.Range(0f, 1f),
            };

            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            float radius = kind.RadiusAt(scale);
            var actor = new Actor(Team.Enemies, loadout, spawn.Position, radius)
            {
                Site = state,
                Body = model,
                Gear = gear,
                Height = position.Y,
                Nature = boss ? Condition.Boss : rarity == Rarity.Rare ? Condition.Rare : Condition.None,
                Tyrant = boss,
                Still = _server.Calm,
            };

            actor.Entity = world.CreateEntity(name, Bodies.Walking(_server, model, position, scale, power.Importance, radius, enemy, brain, new HealthComponent(actor.Sheet.MaxHealth), new ActionComponent(), new AfflictedComponent(), new ActorComponent(actor)));
            state.Live.Add(actor.Entity);
        }

        private static int FirstHostile(WorldSite site) => LandCensus.FirstHostile(site);

        // Straw on a post, to try a build on: as strong as what the land the strongest climber here would go to next, and never down for long. It belongs to no place, as there's nothing to free.
        private void SpawnDummy(ECSWorld world, ServerSession session, SiteState state, SiteSpawn spawn, int group)
        {
            int level = LevelAt(session, state);
            Loadout loadout = session.Bestiary.Shared(Bestiary.Dummy, level, Rarity.Normal, Bestiary.NoTraits);
            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            float radius = Bestiary.Dummy.RadiusAt(DummyScale);
            var actor = new Actor(Team.Enemies, loadout, spawn.Position, radius) { Body = Bestiary.Dummy.Look, Height = position.Y, Still = true, Dummy = true };
            actor.Entity = world.CreateEntity("Dummy",
                new NetIdentityComponent(_server.Ghosts.NextId(), importance: 1f),
                new PositionComponent(position),
                new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitY, MathF.PI + MathF.Sin(spawn.Position.X * 1.7f + spawn.Position.Y) * DummyTurn)),
                new ScaleComponent(new Vector3(DummyScale)),
                new VisualComponent(Bestiary.Dummy.Look, VisualKind.Prop),
                new EnemyComponent { Name = Bestiary.Dummy.Name, Rarity = Rarity.Normal, Level = level },
                new DummyComponent { Group = group },
                new HealthComponent(actor.Sheet.MaxHealth),
                new AfflictedComponent(),
                new ActorComponent(actor));
            state.Live.Add(actor.Entity);
        }

        /// <summary>One of everything, standing still in rows, for looking at. They don't belong anywhere, so nothing is kept of them.</summary>
        public void Parade(ECSWorld world, ServerSession session, Vector2 at, string which, bool fighting = false)
        {
            const int perRow = 7;
            const float slot = 5.5f, rowDepth = 7f;
            var random = new WorldRandom(1);
            (EnemyKind kind, Rarity rarity)[] line = which == "rarities"
                ? Enum.GetValues<Rarity>().Select(rarity => (Bestiary.Outlaw, rarity)).ToArray()
                : (which == "all" ? Bestiary.Everyone : Bestiary.Everyone.Where(kind => which.Split(',').Contains(kind.Id))).Select(kind => (kind, Rarity.Normal)).ToArray();
            float ScaleOf(int index) => line[index].kind.Scale * (line[index].rarity == Rarity.Normal ? 1f : Bestiary.PowerOf(line[index].rarity).Scale);
            // Whatever fits a slot stands where it always has, on a grid centred on the hero, as a test swings at the first of a parade without looking. Anything bigger takes a space as wide
            // as it is and puts more room between its row and the next, as a tyrant is longer than a row of outlaws is apart. The first row stays where it is, as the screen ends not far behind it.
            float SpaceOf(int index) => MathF.Max(slot, line[index].kind.RadiusAt(ScaleOf(index)) * 4.5f);
            IEnumerable<int> Row(int first) => Enumerable.Range(first, Math.Min(perRow, line.Length - first));
            float DepthOf(int first) => rowDepth + (Row(first).Max(SpaceOf) - slot) * 2f;
            float rowAt = 0f, across = 0f;
            for (int i = 0; i < line.Length; i++)
            {
                (EnemyKind kind, Rarity rarity) = line[i];
                int traits = Bestiary.Fill(Bestiary.NoTraits, rarity, random.Fork(i));
                float scale = ScaleOf(i);
                if (i % perRow == 0)
                {
                    rowAt -= i == 0 ? rowDepth : (DepthOf(i - perRow) + DepthOf(i)) * 0.5f;
                    across = MathF.Min(-slot * perRow, -Row(i).Sum(SpaceOf)) * 0.5f;
                }
                Vector2 spot = at + new Vector2(across + SpaceOf(i) * 0.5f, rowAt);
                across += SpaceOf(i);
                Vector3 position = new Vector3(spot.X, session.World.Height.Sample(spot), spot.Y);
                Loadout loadout = session.Bestiary.Shared(kind, 1, rarity, traits);
                string model = Bestiary.ModelOf(kind, random);
                var actor = new Actor(Team.Enemies, loadout, spot, kind.RadiusAt(scale)) { Body = model, Height = position.Y, Still = !fighting };
                var brain = new EnemyBrainComponent { SiteIndex = -1, SpawnIndex = -1, Home = spot, AggroRadius = 60f, LeashRadius = 200f, KeepsAway = kind.KeepsAway, Abilities = Bestiary.AbilitiesOf(kind, traits) };
                IComponent[] body = Bodies.Walking(_server, model, position, scale, 2f, kind.RadiusAt(scale),
                    new EnemyComponent { Name = Bestiary.NameOf(kind, traits), Rarity = rarity, Level = 1 }, brain, new HealthComponent(actor.Sheet.MaxHealth), new ActionComponent(), new AfflictedComponent(), new ActorComponent(actor));
                for (int part = 0; part < body.Length; part++)
                {
                    if (body[part] is RotationComponent) body[part] = new RotationComponent(new Vector3(0f, MathF.PI * 0.8f, 0f));
                }
                actor.Entity = world.CreateEntity(kind.Name, body);
            }
        }

        private void SpawnVillager(ECSWorld world, ServerSession session, SiteState state, int siteIndex, SiteSpawn spawn, WorldRandom random, bool stationary)
        {
            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            var villager = new VillagerComponent
            {
                SiteIndex = siteIndex,
                Home = spawn.Position,
                Goal = spawn.Position,
                Wait = random.Range(0f, 5f),
                WanderRadius = random.Range(8f, 20f),
                Stationary = stationary,
            };
            state.Live.Add(world.CreateEntity("Villager", Bodies.Walking(_server, $"villager{random.Int(12)}", position, random.Range(0.92f, 1.04f), 0.5f, villager)));
        }

        // Someone who keeps one of the Haven's halls: the smith at the anvil, the dealer at the table, the archivist among the shelves. They stand at their work, look up at whoever comes near,
        // and look back into their hall when they've gone.
        private void SpawnKeeper(ECSWorld world, ServerSession session, SiteState state, int siteIndex, SiteSpawn spawn, WorldRandom random)
        {
            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            WorldHall? hall = session.World.HallAt(spawn.Position, 0.5f);
            Vector2 rest = hall != null && (hall.Centre - spawn.Position).LengthSquared > 0.01f ? (hall.Centre - spawn.Position).Normalized() : Vector2.UnitY;
            var villager = new VillagerComponent
            {
                SiteIndex = siteIndex,
                Home = spawn.Position,
                Goal = spawn.Position,
                Stationary = true,
                Keeps = hall != null ? (int)hall.Kind + 1 : 0,
                Rest = rest,
                NextSpeech = session.Time + random.Range(2f, 8f),
            };
            state.Live.Add(world.CreateEntity("Keeper", Bodies.Walking(_server, $"villager{random.Int(12)}", position, random.Range(0.97f, 1.04f), 0.5f, villager)));
        }

        // The people of the land, marched to an arena's stands to watch: they stay where they're put and look to the fight.
        private void SpawnSpectator(ECSWorld world, ServerSession session, SiteState state, int siteIndex, SiteSpawn spawn, WorldRandom random)
        {
            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            var villager = new VillagerComponent
            {
                SiteIndex = siteIndex,
                Home = spawn.Position,
                Goal = spawn.Position,
                Stationary = true,
                Crowd = true,
                Watch = state.Site.Arena?.Centre ?? state.Site.Position,
                NextSpeech = session.Time + random.Range(4f, 30f),
            };
            Vector2 facing = (state.Site.Arena?.Centre ?? state.Site.Position) - spawn.Position;
            Entity entity = world.CreateEntity("Spectator", Bodies.Walking(_server, $"villager{random.Int(12)}", position, random.Range(0.92f, 1.04f), 0.4f, villager));
            if (facing.LengthSquared > 0.01f && world.TryGetEntityLocation(entity, out _, out _)) world.GetEntityView(entity).Set(new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitY, MathF.Atan2(facing.X, facing.Y))));
            state.Live.Add(entity);
        }

        private void SpawnMerchant(ECSWorld world, ServerSession session, SiteState state, int siteIndex, int spawnIndex, SiteSpawn spawn, WorldRandom random)
        {
            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            var villager = new VillagerComponent { SiteIndex = siteIndex, Home = spawn.Position, Goal = spawn.Position, Stationary = true };
            var interactable = new InteractableComponent { Kind = InteractKind.Merchant, SiteIndex = siteIndex, SpawnIndex = spawnIndex, Radius = 5f, Prompt = "Trade" };
            state.Live.Add(world.CreateEntity("Merchant", Bodies.Walking(_server, $"villager{random.Int(12)}", position, 1f, 1f, villager, interactable)));
        }

        private void SpawnBetaVendor(ECSWorld world, ServerSession session, SiteState state, int siteIndex, int spawnIndex, SiteSpawn spawn, WorldRandom random)
        {
            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            var villager = new VillagerComponent { SiteIndex = siteIndex, Home = spawn.Position, Goal = spawn.Position, Stationary = true };
            var interactable = new InteractableComponent { Kind = InteractKind.BetaVendor, SiteIndex = siteIndex, SpawnIndex = spawnIndex, Radius = 5f, Prompt = "See what's free" };
            state.Live.Add(world.CreateEntity("Beta Vendor", Bodies.Walking(_server, $"villager{random.Int(12)}", position, 1f, 1f, villager, interactable)));
        }

        private void SpawnElder(ECSWorld world, ServerSession session, SiteState state, int siteIndex, int spawnIndex, SiteSpawn spawn, WorldRandom random)
        {
            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            var villager = new VillagerComponent { SiteIndex = siteIndex, Home = spawn.Position, Goal = spawn.Position, Stationary = true };
            var interactable = new InteractableComponent { Kind = InteractKind.Elder, SiteIndex = siteIndex, SpawnIndex = spawnIndex, Radius = 5f, Prompt = "Speak with the elder" };
            state.Live.Add(world.CreateEntity("Elder", Bodies.Walking(_server, $"villager{random.Int(12)}", position, 1.02f, 1f, villager, interactable)));
        }

        /// <summary>What stands in a lit waygate, where the world has the gate, for as long as the place does: a way home, which turns in a way of its own for every gate (WayKeys).</summary>
        internal static void LightGate(ECSWorld world, GameServer server, ServerSession session, SiteState state, Vector2 spawn)
        {
            if (!session.World.Objects.TryNearest(StructureCatalog.Waygate.Id, spawn, GateFromSpawn, out WorldObject gate)) return;
            state.Live.Add(world.CreateEntity("Lit waygate",
                new NetIdentityComponent(server.Ghosts.NextId(), importance: 0.5f),
                new PositionComponent(gate.Position),
                new RotationComponent(gate.Rotation),
                new ScaleComponent(gate.Scale),
                new VisualComponent(WayKeys.Lit(WayKeys.GateSeed(session.World.Plan.Seed, gate.Position)), VisualKind.Prop)));
        }

        /// <summary>
        /// A chest where it stands: shut, to be used, or once it's been opened, a body and a lid on its hinge with nothing to use, which stays for as long as the place does. Only a yaw is ever sent,
        /// so a client raises the lid: swinging it up if the chest is <paramref name="opening"/> this moment, and thrown back already if not.
        /// </summary>
        internal static void PlaceChest(ECSWorld world, GameServer server, ServerSession session, SiteState state, int siteIndex, int spawnIndex, bool opening)
        {
            SiteSpawn spawn = state.Site.Spawns[spawnIndex];
            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            Quaternion yaw = new Quaternion(new Vector3(0f, spawn.Position.X, 0f));
            if (!state.Opened.Contains(spawnIndex))
            {
                state.Live.Add(world.CreateEntity("Chest",
                    new NetIdentityComponent(server.Ghosts.NextId(), importance: 0.5f),
                    new PositionComponent(position),
                    new RotationComponent(yaw),
                    new ScaleComponent(new Vector3(ChestScale)),
                    new VisualComponent(ChestLook.Closed, VisualKind.Prop),
                    new InteractableComponent { Kind = InteractKind.Chest, SiteIndex = siteIndex, SpawnIndex = spawnIndex, Radius = 4.5f, Prompt = "Open the chest" }));
                return;
            }

            Vector3 hinge = position + Vector3.Transform(ChestLook.Hinge * ChestScale, yaw);
            state.Live.Add(world.CreateEntity("Open chest",
                new NetIdentityComponent(server.Ghosts.NextId(), importance: 0.5f),
                new PositionComponent(position),
                new RotationComponent(yaw),
                new ScaleComponent(new Vector3(ChestScale)),
                new VisualComponent(ChestLook.Open, VisualKind.Prop)));
            state.Live.Add(world.CreateEntity("Chest lid",
                new NetIdentityComponent(server.Ghosts.NextId(), importance: 0.5f),
                new PositionComponent(hinge),
                new RotationComponent(yaw),
                new ScaleComponent(new Vector3(ChestScale)),
                new VisualComponent(opening ? ChestLook.Opening : ChestLook.Lid, VisualKind.Prop)));
        }

        private void SpawnMarker(ECSWorld world, ServerSession session, SiteState state, int siteIndex, int spawnIndex, SiteSpawn spawn, InteractKind kind, string prompt, float radius) =>
            SpawnMarker(world, session, state, siteIndex, spawnIndex, spawn.Position, kind, prompt, radius);

        private void SpawnMarker(ECSWorld world, ServerSession session, SiteState state, int siteIndex, int spawnIndex, Vector2 at, InteractKind kind, string prompt, float radius)
        {
            float height = session.World.Height.Sample(at);
            state.Live.Add(world.CreateEntity(prompt,
                new NetIdentityComponent(_server.Ghosts.NextId(), importance: 0.5f),
                new PositionComponent(new Vector3(at.X, height, at.Y)),
                new InteractableComponent { Kind = kind, SiteIndex = siteIndex, SpawnIndex = spawnIndex, Radius = radius, Prompt = prompt }));
        }

        // The map table is come to from every side, so it's used from the middle of the table, as far out as reaches anyone standing at any side or corner of it, and not from a
        // spot in front of it, which someone coming down from the gate had to walk round to.
        private void SpawnTableMarker(ECSWorld world, ServerSession session, SiteState state, int siteIndex, int spawnIndex, SiteSpawn spawn)
        {
            StructureDefinition table = StructureCatalog.MapTable;
            Vector2 at = session.World.Objects.TryNearest(table.Id, spawn.Position, TableFromSpawn, out WorldObject found) ? found.Position.Xz : spawn.Position;
            SpawnMarker(world, session, state, siteIndex, spawnIndex, at, InteractKind.MapTable, "Choose your next land", table.HalfExtents.Length + TableReach);
        }
    }

    /// <summary>The components every walking character has on the server: where it is, how it moves, and what clients should draw.</summary>
    public static class Bodies
    {
        public static IComponent[] Walking(GameServer server, string model, Vector3 position, float scale, float importance, params IComponent[] extra) =>
            Walking(server, model, position, scale, importance, WorldData.CharacterRadius * MathF.Min(scale, 1.4f), extra);

        /// <param name="radius">How far its body reaches, which is what's solid about it: a person's, or a dinosaur's, which is far more.</param>
        public static IComponent[] Walking(GameServer server, string model, Vector3 position, float scale, float importance, float radius, params IComponent[] extra)
        {
            var components = new List<IComponent>
            {
                new NetIdentityComponent(server.Ghosts.NextId(), importance),
                new PositionComponent(position),
                new RotationComponent(new Vector3(0f)),
                new ScaleComponent(new Vector3(scale)),
                new VisualComponent(model, VisualKind.Character),
                new VelocityComponent(),
                new FixToTerrainHeightComponent(),
                new CharacterMotorComponent(radius),
            };
            components.AddRange(extra);
            return components.ToArray();
        }
    }

    /// <summary>
    /// Villagers about their day: walking between spots near home and stopping a while, and speaking when a hero passes. What
    /// they say depends on whether the people preying on them are still out there.
    /// </summary>
    public class VillagerSystem : SystemBase
    {
        private const float WalkSpeed = 3.6f;
        private const float SpeechReach = 9f;
        private const float SpeechOverMark = 11.5f;     // Whoever can be used has a mark over their head, and the camera looks so far down that it takes this to clear it.

        // What people say under the Reich, and once it's gone from their land. Read LORE.md.
        private static readonly string[] Afraid =
        {
            "Keep your voice down. They hear everything.", "The roaring comes at night now.", "Mind yourself on the road. The raptors hunt in packs.", "There's nothing left to take.",
            "Someone ought to do something.", "You're not one of theirs, are you?", "They took my brother for the pens.", "We've buried too many.",
            "Did you hear it, up at the keep? Every night.", "The greycoats came for the harvest.",
        };
        private static readonly string[] Grateful =
        {
            "The road's safe again!", "Bless you, Defender.", "We can sleep at last.", "My boy came home from the pens.",
            "They're saying it was you.", "The market's open again.", "No more roaring in the night.", "There's bread for you here.",
        };
        // What the people made to watch from an arena's stands say, before anyone's on the floor and once someone is.
        private static readonly string[] Waiting =
        {
            "Don't look away. They're counting.", "Quiet. The greycoats are watching.", "Another one for the tyrant.", "Not like the last one. Please, not like the last one.",
            "Come on, Defender.", "They say this one walked here on purpose.",
        };
        private static readonly string[] Cheering =
        {
            "Get up! Get up!", "Mind the gates!", "It's bleeding!", "Behind you!", "Come on, Defender.", "Don't let it corner you!", "It's slowing!",
        };
        private const float CrowdReach = 60f;           // Metres from a hero inside which those watching from the stands call out.

        // What the Haven's hall keepers say to whoever comes in: the smith, the dealer and the archivist. Read LORE.md.
        private static readonly string[][] Keeping =
        {
            new[] { "Mind the sparks.", "Bring me something worth the iron.", "Leave it on the bench. I'll get to it.", "The fire's been in since dawn.", "That edge won't last another land. Let me see it." },
            new[] { "Cards or dice, Defender?", "The table's open whenever you are.", "Gold's no use to anyone in a pocket.", "Sit down. Rest your feet.", "Nobody leaves here poorer than they can stand." },
            new[] { "Everything you've done is written here.", "Mind the shelves. Some of these are older than the Haven.", "Every tyrant you bring down goes on the wall.", "Read what you like. Just put it back.", "We keep the names of the fallen too." },
        };
        private const float VisitChance = 0.45f;        // Of a walk in the Haven being to one of its halls rather than about the village.
        private const float StuckAfter = 5f;            // Seconds without getting nearer that a walk is given up.

        private readonly WorldPathfinder _paths = new WorldPathfinder(exact: true);

        private static readonly string[] AtHome =
        {
            "Rest a while. Nothing reaches us here.", "Back again? Good.", "The gate's been humming all morning.", "Mind the dummies, they bite back. They don't, really.",
            "We keep a lamp lit for whoever's out there.", "Every road comes here first.", "Sit down before you fall down.", "The boats are in, if you want fish.",
            "Another land? Already?", "You look like you've seen a tyrant.",
        };

        private readonly GameServer _server;
        private readonly WorldRandom _random = new WorldRandom(Environment.TickCount);

        public VillagerSystem(GameServer server)
            : base(new BitMask(typeof(VillagerComponent), typeof(PositionComponent), typeof(VelocityComponent)))
        {
            _server = server;
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            ServerSession session = _server.Session!;
            var villager = entity.Get<VillagerComponent>();
            Vector3 position = entity.Get<PositionComponent>().value;
            if (session.Paused)
            {
                entity.Set(new VelocityComponent());
                return;
            }

            ServerPlayer? nearest = session.NearestHero(position.Xz, out float toNearest);
            Vector2 toHero = nearest != null ? nearest.Sim.Position - position.Xz : Vector2.Zero;

            if (villager.Crowd)
            {
                // Those watching look to the fight, or to where it will be, and call down to it now and then.
                EnemyBrainSystem.Face(entity, nearest != null && toNearest < CrowdReach ? toHero : villager.Watch - position.Xz, deltaTime, 2f);
                if (nearest != null && toNearest < CrowdReach && session.Time >= villager.NextSpeech)
                {
                    // Fighting is anyone down on the floor.
                    bool fighting = session.Sites[villager.SiteIndex].Site.Arena is ArenaPlan floor ? floor.Contains(nearest.Sim.Position, 2f) : (nearest.Sim.Position - villager.Watch).Length < 30f;
                    _server.Broadcast(new TextEvent
                    {
                        Position = position + Vector3.UnitY * 5.6f,
                        Text = _random.Pick(fighting ? Cheering : Waiting),
                        Colour = new Vector3(0.92f, 0.9f, 0.8f),
                        Size = 14f,
                        Life = 2.6f,
                        Speech = true,
                    }, position.Xz);
                    villager.NextSpeech = session.Time + _random.Range(25f, 60f);
                }
                entity.Set(new VelocityComponent());
                entity.Set(villager);
                return;
            }

            if (nearest != null && toNearest < SpeechReach && session.Time >= villager.NextSpeech)
            {
                bool safe = session.IsSafe(session.Sites[villager.SiteIndex]);
                string[] lines = villager.Keeps > 0 && villager.Keeps <= Keeping.Length ? Keeping[villager.Keeps - 1] : session.IsHaven ? AtHome : safe ? Grateful : Afraid;
                _server.Broadcast(new TextEvent
                {
                    Position = position + Vector3.UnitY * (entity.Has<InteractableComponent>() ? SpeechOverMark : 5.6f),
                    Text = _random.Pick(lines),
                    Colour = new Vector3(0.92f, 0.9f, 0.8f),
                    Size = 14f,
                    Life = 3.2f,
                    Speech = true,
                }, position.Xz);
                villager.NextSpeech = session.Time + _random.Range(9f, 18f);
            }

            Vector2 velocity = Vector2.Zero;
            if (villager.Stationary)
            {
                // A keeper looks back to their work once nobody's near.
                Vector2 look = nearest != null && toNearest < 10f ? toHero : villager.Rest;
                EnemyBrainSystem.Face(entity, look, deltaTime, 4f);
            }
            else if (villager.Wait > 0f)
            {
                villager.Wait -= deltaTime;
            }
            else if (villager.Route != null)
            {
                velocity = Walk(ref villager, position.Xz, deltaTime);
                if (velocity != Vector2.Zero) EnemyBrainSystem.Face(entity, velocity, deltaTime, 6f);
            }
            else
            {
                Vector2 offset = villager.Goal - position.Xz;
                if (offset.LengthSquared < 1.5f)
                {
                    PickGoal(ref villager, session, position.Xz);
                }
                else
                {
                    velocity = offset.Normalized() * WalkSpeed;
                    EnemyBrainSystem.Face(entity, velocity, deltaTime, 6f);
                }
            }

            entity.Set(new VelocityComponent(new Vector3(velocity.X, 0f, velocity.Y)));
            entity.Set(villager);
        }

        private void PickGoal(ref VillagerComponent villager, ServerSession session, Vector2 from)
        {
            WorldData world = session.World;
            villager.Wait = _random.Range(2f, 9f);

            // In the Haven, as often as not, somewhere in one of its halls: the way there goes through its door.
            if (session.IsHaven && world.Halls.Count > 0 && _random.Chance(VisitChance))
            {
                WorldHall hall = _random.Pick(world.Halls);
                if (hall.Design.Visits.Count > 0)
                {
                    Vector2 visit = hall.ToWorld(_random.Pick(hall.Design.Visits));
                    if (Route(ref villager, world, from, visit)) return;
                }
            }

            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector2 goal = villager.Home + new Vector2(_random.Range(-1f, 1f), _random.Range(-1f, 1f)) * villager.WanderRadius;
                if (!world.Passable.IsSet(goal)) continue;
                if (WorldPathfinder.HasClearLine(world, from, goal))
                {
                    villager.Goal = goal;
                    return;
                }
                if (session.IsHaven && Route(ref villager, world, from, goal)) return;
            }
        }

        // Works out the way to a place, round whatever's in the way and through doors, and pulls it straight wherever it can see further along.
        private bool Route(ref VillagerComponent villager, WorldData world, Vector2 from, Vector2 to)
        {
            List<Vector2>? path = _paths.Find(world, from, to);
            if (path == null || path.Count == 0 || (path[^1] - to).LengthSquared > 4f) return false;
            var route = new List<Vector2>();
            Vector2 at = from;
            int i = 0;
            while (i < path.Count)
            {
                int furthest = i;
                for (int j = Math.Min(path.Count - 1, i + 24); j > i; j--)
                {
                    if (WorldPathfinder.HasClearLine(world, at, path[j], spacing: 0.7f))
                    {
                        furthest = j;
                        break;
                    }
                }
                at = path[furthest];
                route.Add(at);
                i = furthest + 1;
            }
            villager.Route = route;
            villager.RouteStep = 0;
            villager.Goal = to;
            villager.StuckFor = 0f;
            villager.Closest = float.PositiveInfinity;
            return true;
        }

        // A step along the way being walked, and on to the next point of it once this one's reached. A way that stops getting anywhere is given up.
        private Vector2 Walk(ref VillagerComponent villager, Vector2 at, float deltaTime)
        {
            List<Vector2> route = villager.Route!;
            while (villager.RouteStep < route.Count && (route[villager.RouteStep] - at).LengthSquared < 1.4f)
            {
                villager.RouteStep++;
                villager.Closest = float.PositiveInfinity;
            }
            if (villager.RouteStep >= route.Count)
            {
                villager.Route = null;
                villager.Goal = at;
                villager.Wait = _random.Range(4f, 12f);
                return Vector2.Zero;
            }

            Vector2 offset = route[villager.RouteStep] - at;
            float distance = offset.Length;
            if (distance < villager.Closest - 0.05f)
            {
                villager.Closest = distance;
                villager.StuckFor = 0f;
            }
            else if ((villager.StuckFor += deltaTime) > StuckAfter)
            {
                villager.Route = null;
                villager.Goal = at;
                villager.Wait = _random.Range(1f, 4f);
                return Vector2.Zero;
            }
            return offset / MathF.Max(distance, 1e-4f) * WalkSpeed;
        }
    }

    /// <summary>
    /// What heroes can use. E uses whatever they are standing by, and gold comes to whoever is near, as nobody should have to stop for it. Everything else on the ground
    /// has to be asked for: see <see cref="GroundLoot"/>.
    /// </summary>
    public class InteractionSystem : SystemBase
    {
        private const float PickupReach = 3.4f;
        private const float MagnetReach = 7f;

        private static readonly BitMask PickupMask = new BitMask(typeof(PickupComponent), typeof(PositionComponent));

        private readonly GameServer _server;

        public InteractionSystem(GameServer server)
            : base(new BitMask(typeof(InteractableComponent), typeof(PositionComponent)))
        {
            _server = server;
        }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            ServerSession? session = _server.Session;
            if (session == null) return;
            UpdatePickups(world, session, deltaTime);
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        /// <summary>A hero has pressed use: the nearest thing in reach is what they meant.</summary>
        public void Interact(ECSWorld world, ServerSession session, ServerPlayer hero)
        {
            if (hero.Actor.Vitals.Health <= 0f) return;

            Entity nearest = default;
            float best = float.PositiveInfinity;
            foreach (var archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                foreach (var entity in new ComponentAccessor(archetype))
                {
                    var interactable = entity.Get<InteractableComponent>();
                    float distance = (entity.Get<PositionComponent>().value.Xz - hero.Sim.Position).Length;
                    if (distance > interactable.Radius || distance >= best) continue;
                    best = distance;
                    nearest = entity.Entity;
                }
            }

            if (!nearest.IsValid()) return;
            EntityView view = world.GetEntityView(nearest);
            _server.Broadcast(new FlourishEvent { Who = hero.GhostId, Kind = Flourish.Use }, hero.Sim.Position);
            Use(world, session, hero, view, view.Get<InteractableComponent>());
        }

        private void Use(ECSWorld world, ServerSession session, ServerPlayer hero, EntityView view, in InteractableComponent interactable)
        {
            SiteState state = session.Sites[interactable.SiteIndex];
            // What's in one of the Haven's halls is only for a hero the hall is open to.
            if (Halls.Of(interactable.Kind) is HallKind hall && !Halls.IsOpen(hero.Hero, hall))
            {
                _server.Note(hero, Halls.Shut(hall));
                return;
            }
            switch (interactable.Kind)
            {
                case InteractKind.Shrine:
                    UseShrine(world, session, hero, state, view);
                    break;
                case InteractKind.Chest:
                    state.Opened.Add(interactable.SpawnIndex);
                    state.Changed = true;
                    state.Live.Remove(view.Entity);
                    session.Combat.OpenChest(hero, view.Get<PositionComponent>().value, EncounterSystem.LevelAt(session, state), ChestTier(session, state));
                    world.DestroyEntity(view.Entity);
                    EncounterSystem.PlaceChest(world, _server, session, state, interactable.SiteIndex, interactable.SpawnIndex, opening: true);
                    break;
                case InteractKind.Portal:
                    _server.StepThrough();
                    break;
                default:
                    _server.OpenPanel(hero, interactable.Kind, interactable.SiteIndex);
                    break;
            }
        }

        // What a chest holds goes by where it is, and what the land holds can make it better: a gilded land now and then, and every place's on a land held as the last garrison.
        private static LootTier ChestTier(ServerSession session, SiteState state)
        {
            LootTier tier = state.Wild ? LootTier.Cache : state.Site.Kind == SiteKind.Keep ? LootTier.Hoard : LootTier.Chest;
            if (!state.Wild && session.Terms.Has(OrdersCatalog.Garrison)) tier = LootTier.Hoard;
            if (session.Terms.Has(Omens.Gilded) && session.Combat.Luck(session.Terms.Chance(Omens.Gilded))) tier = LootTables.Better(tier);
            return tier;
        }

        private const float BlessingBanner = 6f;   // Seconds the banner stays: long enough to read a few lines of bonuses.

        private void UseShrine(ECSWorld world, ServerSession session, ServerPlayer hero, SiteState state, EntityView view)
        {
            if (state.Kindled) return;
            Vector3 position = view.Get<PositionComponent>().value;

            BuffDefinition blessing = EncounterSystem.BlessingOf(session, state);
            hero.Actor.Vitals = Vitals.Full(hero.Actor.Sheet);
            hero.Hero.Flasks = HeroState.MaxFlasks;
            hero.SummaryChanged = true;
            session.Combat.AddBuff(hero.Actor, blessing, blessing.Duration);
            _server.Broadcast(new SparkEvent { Position = position + Vector3.UnitY * 3f, Kind = SparkKind.Shrine, Count = 26, Speed = 7f }, position.Xz);

            // Used once, and spent: its mark goes with it, and it isn't made again when the place wakes.
            bool keeps = EncounterSystem.Keeps(state.Site.Kind);
            state.Kindled = true;
            state.Changed = true;
            state.Live.Remove(view.Entity);
            world.DestroyEntity(view.Entity);

            // The blessing and what it gives, in the middle of the screen as a place's name is, in the words its card uses.
            _server.Announce(hero, blessing.Name, EncounterSystem.BlessingText(blessing), BlessingBanner);
            if (keeps)
            {
                _server.Broadcast(new SoundEvent { Position = position, Cue = SoundCue.Blessed }, position.Xz);
                return;
            }
            _server.Broadcast(new SoundEvent { Position = position, Cue = SoundCue.ShrineKindled }, position.Xz);
            foreach (ServerPlayer everyone in session.Heroes) session.GiveExperience(everyone, 25 * session.World.Plan.Level);
        }

        private void UpdatePickups(ECSWorld world, ServerSession session, float deltaTime)
        {
            foreach (var archetype in world.QueryArchetypes(PickupMask, BitMask.Empty))
            {
                foreach (var entity in new ComponentAccessor(archetype))
                {
                    var pickup = entity.Get<PickupComponent>();
                    if (pickup.Gold <= 0 || _server.Tick < pickup.Born + (uint)(Loot.GoldFlight * NetTime.TicksPerSecond)) continue;

                    ServerPlayer? hero = session.NearestHero(pickup.Rest.Xz, out float distance);
                    if (hero == null) continue;

                    if (distance < PickupReach)
                    {
                        Collect(hero, pickup);
                        world.DestroyEntity(entity.Entity);
                        continue;
                    }

                    if (distance >= MagnetReach) continue;
                    Vector2 toHero = hero.Sim.Position - pickup.Rest.Xz;
                    pickup.Rest += new Vector3(toHero.X, 0f, toHero.Y) / MathF.Max(distance, 0.1f) * 22f * deltaTime;
                    entity.Set(new PositionComponent(pickup.Rest));
                    entity.Set(pickup);
                }
            }
        }

        private void Collect(ServerPlayer hero, in PickupComponent pickup)
        {
            Vector3 position = pickup.Rest;
            hero.Hero.Gold += pickup.Gold;
            hero.Hero.Records.GoldGathered += pickup.Gold;
            hero.SummaryChanged = true;
            _server.Broadcast(new SoundEvent { Position = position, Cue = SoundCue.Coins, Value = pickup.Gold }, position.Xz);
            _server.Broadcast(new TextEvent
            {
                Position = position + Vector3.UnitY * 3f,
                Text = $"+{pickup.Gold} gold",
                Colour = new Vector3(1f, 0.82f, 0.3f),
                Size = 14f,
                Life = 1.1f,
            }, position.Xz);
        }
    }
}
