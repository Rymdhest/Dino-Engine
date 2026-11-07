using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
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
        private const float ChestScale = 1.35f;     // A size bigger than life, like anything else that matters seen from up where the camera is.
        private const float WakeDistance = 100f;
        private const float SleepDistance = 160f;
        private const float GuardArcherChance = 0.45f;
        private const float CheckInterval = 0.25f;
        private const int MostWakings = 4;      // At a check. A hero stepping out of a waygate is surrounded by places, and they needn't all arrive in one tick.

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

        private static void Sleep(ECSWorld world, SiteState state)
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
                        // A place set free stays free, whoever a newer build would have put there.
                        if (!state.Dead.Contains(i) && !state.Cleared) SpawnEnemy(world, session, state, siteIndex, i, spawn, Orders(session, state, spawn, random, musters), spawnRandom);
                        break;
                    case SpawnRole.Villager:
                        SpawnVillager(world, session, state, siteIndex, spawn, spawnRandom, stationary: false);
                        break;
                    case SpawnRole.Merchant:
                        // The landing trades whatever happens: it's what a new arrival has to start from.
                        if (session.IsSafe(state) || state.Site.Kind == SiteKind.Landing) SpawnMerchant(world, session, state, siteIndex, i, spawn, spawnRandom);
                        break;
                    case SpawnRole.Elder:
                        SpawnElder(world, session, state, siteIndex, i, spawn, spawnRandom);
                        break;
                    case SpawnRole.Chest:
                        if (!state.Opened.Contains(i)) SpawnChest(world, session, state, siteIndex, i, spawn);
                        break;
                    case SpawnRole.Altar:
                        SpawnMarker(world, session, state, siteIndex, i, spawn, InteractKind.Shrine, state.Kindled ? "Rest at the shrine" : "Kindle the shrine", ShrineReach);
                        break;
                    case SpawnRole.Waygate:
                        SpawnMarker(world, session, state, siteIndex, i, spawn, InteractKind.Waygate, "Step through the waygate", 6f);
                        break;
                }
            }
        }

        // Everyone of a folk at one place answers to the same orders.
        private static PackOrders Orders(ServerSession session, SiteState state, in SiteSpawn spawn, WorldRandom random, Dictionary<SpawnRole, PackOrders> musters)
        {
            SpawnRole folk = spawn.Role is SpawnRole.Guard or SpawnRole.Boss ? SpawnRole.Raider : spawn.Role;
            if (!musters.TryGetValue(folk, out PackOrders? orders))
            {
                orders = new PackOrders(session.World, state.Site, folk, LevelAt(session, state), state.Wild, random.Fork("pack"), session.Offering);
                musters.Add(folk, orders);
            }
            return orders;
        }

        /// <summary>How strong what's found at a place is. Danger climbs with the distance from where a hero came in.</summary>
        public static int LevelAt(ServerSession session, SiteState state) => session.World.Plan.Level + (int)(state.Site.Remoteness * 3f);

        private void SpawnEnemy(ECSWorld world, ServerSession session, SiteState state, int siteIndex, int spawnIndex, SiteSpawn spawn, PackOrders orders, WorldRandom random)
        {
            int level = LevelAt(session, state);
            bool boss = spawn.Role == SpawnRole.Boss;
            WorldRandom kinds = random.Fork("kind");

            EnemyKind kind;
            Rarity rarity;
            int traits;
            if (boss) (kind, rarity, traits) = (Bestiary.Warlord, Rarity.Unique, Bestiary.NoTraits);
            else if (spawn.Role == SpawnRole.Guard) (kind, rarity, traits) = (kinds.Chance(GuardArcherChance) ? Bestiary.Archer : Bestiary.Sentry, Rarity.Normal, Bestiary.NoTraits);
            else (kind, rarity, traits) = orders.Draw(spawnIndex, kinds);

            // The people of a place have their hard cases, where a pack has its leader.
            if (!boss && !state.Wild)
            {
                if (kinds.Chance(session.Offering.Has(Omens.Warhost) ? Rulebook.WarhostRareChance : Rulebook.RareChance)) rarity = Rarity.Rare;
                else if (rarity == Rarity.Normal && kinds.Chance(Rulebook.MagicChance)) rarity = Rarity.Magic;
            }
            traits = Bestiary.Fill(traits, rarity, random.Fork("traits"));

            EnemyPower power = Bestiary.PowerOf(rarity);
            float scale = kind.Scale * (rarity == Rarity.Normal ? random.Range(0.94f, 1.06f) : power.Scale);
            string model = Bestiary.ModelOf(kind, random);

            Dino_Defenders.Progression.Item[]? gear = null;
            string? title = null;
            Loadout loadout = boss ? Bestiary.Boss(kind, level, traits, random.Fork("boss"), out gear, out title, session.Bestiary.Worn) : session.Bestiary.Shared(kind, level, rarity, traits);

            string name = boss ? session.Chronicle.Warlord : Bestiary.NameOf(kind, traits);
            if (title != null) name = $"{name}, {title}";

            var enemy = new EnemyComponent { Name = name, Rarity = rarity, Level = level };
            var brain = new EnemyBrainComponent
            {
                SiteIndex = siteIndex,
                SpawnIndex = spawnIndex,
                Home = spawn.Position,
                AggroRadius = kind.AggroRadius * session.Offering.Notice,
                LeashRadius = kind.LeashRadius,
                KeepsAway = kind.KeepsAway,
                Experience = Bestiary.ExperienceAt(level, power.Health * kind.Health),
                Abilities = Bestiary.AbilitiesOf(kind, traits),
                Cooldown = random.Range(0f, 1f),
            };

            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            var actor = new Actor(Team.Enemies, loadout, spawn.Position, WorldData.CharacterRadius * MathF.Min(scale, 1.4f))
            {
                Site = state,
                Body = model,
                Gear = gear,
                Height = position.Y,
                Nature = boss ? Condition.Boss : rarity == Rarity.Rare ? Condition.Rare : Condition.None,
            };

            actor.Entity = world.CreateEntity(name, Bodies.Walking(_server, model, position, scale, power.Importance, enemy, brain, new HealthComponent(actor.Sheet.MaxHealth), new ActionComponent(), new AfflictedComponent(), new ActorComponent(actor)));
            state.Live.Add(actor.Entity);
        }

        /// <summary>One of everything, standing still in rows, for looking at. They don't belong anywhere, so nothing is kept of them.</summary>
        public void Parade(ECSWorld world, ServerSession session, Vector2 at, string which, bool fighting = false)
        {
            const int perRow = 7;
            var random = new WorldRandom(1);
            (EnemyKind kind, Rarity rarity)[] line = which == "rarities"
                ? Enum.GetValues<Rarity>().Select(rarity => (Bestiary.Outlaw, rarity)).ToArray()
                : (which == "all" ? Bestiary.Everyone : Bestiary.Everyone.Where(kind => which.Split(',').Contains(kind.Id))).Select(kind => (kind, Rarity.Normal)).ToArray();
            for (int i = 0; i < line.Length; i++)
            {
                (EnemyKind kind, Rarity rarity) = line[i];
                int traits = Bestiary.Fill(Bestiary.NoTraits, rarity, random.Fork(i));
                float scale = kind.Scale * (rarity == Rarity.Normal ? 1f : Bestiary.PowerOf(rarity).Scale);
                Vector2 spot = at + new Vector2((i % perRow - (perRow - 1) * 0.5f) * 5.5f, -7f - i / perRow * 7f);
                Vector3 position = new Vector3(spot.X, session.World.Height.Sample(spot), spot.Y);
                Loadout loadout = session.Bestiary.Shared(kind, 1, rarity, traits);
                var actor = new Actor(Team.Enemies, loadout, spot, WorldData.CharacterRadius) { Body = kind.Look, Height = position.Y, Still = !fighting };
                var brain = new EnemyBrainComponent { SiteIndex = -1, SpawnIndex = -1, Home = spot, AggroRadius = 60f, LeashRadius = 200f, KeepsAway = kind.KeepsAway, Abilities = Bestiary.AbilitiesOf(kind, traits) };
                IComponent[] body = Bodies.Walking(_server, Bestiary.ModelOf(kind, random), position, scale, 2f,
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

        private void SpawnMerchant(ECSWorld world, ServerSession session, SiteState state, int siteIndex, int spawnIndex, SiteSpawn spawn, WorldRandom random)
        {
            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            var villager = new VillagerComponent { SiteIndex = siteIndex, Home = spawn.Position, Goal = spawn.Position, Stationary = true };
            var interactable = new InteractableComponent { Kind = InteractKind.Merchant, SiteIndex = siteIndex, SpawnIndex = spawnIndex, Radius = 5f, Prompt = "Trade" };
            state.Live.Add(world.CreateEntity("Merchant", Bodies.Walking(_server, $"villager{random.Int(12)}", position, 1f, 1f, villager, interactable)));
        }

        private void SpawnElder(ECSWorld world, ServerSession session, SiteState state, int siteIndex, int spawnIndex, SiteSpawn spawn, WorldRandom random)
        {
            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            var villager = new VillagerComponent { SiteIndex = siteIndex, Home = spawn.Position, Goal = spawn.Position, Stationary = true };
            var interactable = new InteractableComponent { Kind = InteractKind.Elder, SiteIndex = siteIndex, SpawnIndex = spawnIndex, Radius = 5f, Prompt = "Speak with the elder" };
            state.Live.Add(world.CreateEntity("Elder", Bodies.Walking(_server, $"villager{random.Int(12)}", position, 1.02f, 1f, villager, interactable)));
        }

        private void SpawnChest(ECSWorld world, ServerSession session, SiteState state, int siteIndex, int spawnIndex, SiteSpawn spawn)
        {
            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            state.Live.Add(world.CreateEntity("Chest",
                new NetIdentityComponent(_server.Ghosts.NextId(), importance: 0.5f),
                new PositionComponent(position),
                new RotationComponent(new Vector3(0f, spawn.Position.X, 0f)),
                new ScaleComponent(new Vector3(ChestScale)),
                new VisualComponent("chest", VisualKind.Prop),
                new InteractableComponent { Kind = InteractKind.Chest, SiteIndex = siteIndex, SpawnIndex = spawnIndex, Radius = 4.5f, Prompt = "Open the chest" }));
        }

        private void SpawnMarker(ECSWorld world, ServerSession session, SiteState state, int siteIndex, int spawnIndex, SiteSpawn spawn, InteractKind kind, string prompt, float radius)
        {
            float height = session.World.Height.Sample(spawn.Position);
            state.Live.Add(world.CreateEntity(prompt,
                new NetIdentityComponent(_server.Ghosts.NextId(), importance: 0.5f),
                new PositionComponent(new Vector3(spawn.Position.X, height, spawn.Position.Y)),
                new InteractableComponent { Kind = kind, SiteIndex = siteIndex, SpawnIndex = spawnIndex, Radius = radius, Prompt = prompt }));
        }
    }

    /// <summary>The components every walking character has on the server: where it is, how it moves, and what clients should draw.</summary>
    public static class Bodies
    {
        public static IComponent[] Walking(GameServer server, string model, Vector3 position, float scale, float importance, params IComponent[] extra)
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
                new CharacterMotorComponent(WorldData.CharacterRadius * MathF.Min(scale, 1.4f)),
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

        private static readonly string[] Afraid =
        {
            "Keep your voice down.", "They come at night now.", "Mind yourself on the road.", "There's nothing left to take.",
            "Someone ought to do something.", "You're not one of theirs, are you?", "We've buried too many.",
        };
        private static readonly string[] Grateful =
        {
            "The road's safe again!", "Bless you, traveller.", "We can sleep at last.", "My boy came home.",
            "They're saying it was you.", "The market's open again.", "There's bread for you here.",
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

            if (nearest != null && toNearest < SpeechReach && session.Time >= villager.NextSpeech)
            {
                bool safe = session.IsSafe(session.Sites[villager.SiteIndex]);
                _server.Broadcast(new TextEvent
                {
                    Position = position + Vector3.UnitY * (entity.Has<InteractableComponent>() ? SpeechOverMark : 5.6f),
                    Text = _random.Pick(safe ? Grateful : Afraid),
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
                EnemyBrainSystem.Face(entity, nearest != null && toNearest < 10f ? toHero : Vector2.Zero, deltaTime, 4f);
            }
            else if (villager.Wait > 0f)
            {
                villager.Wait -= deltaTime;
            }
            else
            {
                Vector2 offset = villager.Goal - position.Xz;
                if (offset.LengthSquared < 1.5f)
                {
                    PickGoal(ref villager, session.World);
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

        private void PickGoal(ref VillagerComponent villager, WorldData world)
        {
            villager.Wait = _random.Range(2f, 9f);
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector2 goal = villager.Home + new Vector2(_random.Range(-1f, 1f), _random.Range(-1f, 1f)) * villager.WanderRadius;
                if (!WorldPathfinder.HasClearLine(world, villager.Goal, goal)) continue;
                villager.Goal = goal;
                return;
            }
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
            switch (interactable.Kind)
            {
                case InteractKind.Shrine:
                    UseShrine(session, hero, state, view);
                    break;
                case InteractKind.Chest:
                    state.Opened.Add(interactable.SpawnIndex);
                    state.Changed = true;
                    state.Live.Remove(view.Entity);
                    LootTier tier = state.Wild ? LootTier.Cache : state.Site.Kind == SiteKind.Keep ? LootTier.Hoard : LootTier.Chest;
                    if (session.Gilded) tier = LootTables.Better(tier);
                    session.Combat.OpenChest(hero, view.Get<PositionComponent>().value, EncounterSystem.LevelAt(session, state), tier);
                    world.DestroyEntity(view.Entity);
                    break;
                default:
                    _server.OpenPanel(hero, interactable.Kind, interactable.SiteIndex);
                    break;
            }
        }

        private void UseShrine(ServerSession session, ServerPlayer hero, SiteState state, EntityView view)
        {
            Vector3 position = view.Get<PositionComponent>().value;
            hero.Actor.Vitals = Vitals.Full(hero.Actor.Sheet);
            hero.Hero.Flasks = HeroState.MaxFlasks;
            hero.SummaryChanged = true;
            hero.Respawn = position.Xz;
            _server.Broadcast(new SparkEvent { Position = position + Vector3.UnitY * 3f, Kind = SparkKind.Shrine, Count = 26, Speed = 7f }, position.Xz);

            if (state.Kindled)
            {
                _server.Note(hero, "You rest at the shrine.");
                _server.Broadcast(new SoundEvent { Position = position, Cue = SoundCue.ShrineRest }, position.Xz);
                return;
            }
            state.Kindled = true;
            _server.Broadcast(new SoundEvent { Position = position, Cue = SoundCue.ShrineKindled }, position.Xz);
            state.Changed = true;
            var interactable = view.Get<InteractableComponent>();
            interactable.Prompt = "Rest at the shrine";
            view.Set(interactable);
            _server.Announce(hero, "Shrine kindled", "Your flasks are full, and you will wake here if you fall.");
            foreach (ServerPlayer everyone in session.Heroes) session.GiveExperience(everyone, 25 * session.World.Plan.Level);
            if (state.Site.Kind == SiteKind.Shrine) session.Advance(ObjectiveKind.PrayAtShrines, SiteKind.Shrine);
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
