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
        private const float WakeDistance = 100f;
        private const float SleepDistance = 160f;
        private const float EliteChance = 0.14f;
        private const float CasterChance = 0.18f;
        private const float MournerChance = 0.2f;
        private const float CheckInterval = 0.25f;

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

            for (int i = 0; i < session.Sites.Count; i++)
            {
                SiteState state = session.Sites[i];
                float distance = session.DistanceToAnyHero(state.Site.Position) - state.Site.Radius;
                if (!state.Active && distance < WakeDistance) Wake(world, session, state, i);
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
                        if (!state.Dead.Contains(i)) SpawnEnemy(world, session, state, siteIndex, i, spawn, spawnRandom);
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
                        SpawnMarker(world, session, state, siteIndex, i, spawn, InteractKind.Shrine, state.Kindled ? "Rest at the shrine" : "Kindle the shrine", 5f);
                        break;
                    case SpawnRole.Waygate:
                        SpawnMarker(world, session, state, siteIndex, i, spawn, InteractKind.Waygate, "Step through the waygate", 6f);
                        break;
                }
            }
        }

        private void SpawnEnemy(ECSWorld world, ServerSession session, SiteState state, int siteIndex, int spawnIndex, SiteSpawn spawn, WorldRandom random)
        {
            int level = session.World.Plan.Level + (int)(state.Site.Remoteness * 3f);
            bool boss = spawn.Role == SpawnRole.Boss;
            bool undead = spawn.Role == SpawnRole.Undead;
            bool archer = !undead && !boss && random.Chance(spawn.Role == SpawnRole.Guard ? 0.45f : 0.22f);
            bool elite = !boss && random.Chance(EliteChance);

            float rankHealth = boss ? 11f : elite ? 2.6f : 1f;
            float scale = boss ? 1.7f : elite ? 1.22f : random.Range(0.94f, 1.06f);
            string model = boss ? "warlord" : undead ? "skeleton" : $"bandit{random.Int(8)}:{archer}";

            // New draws come from a new fork, so that what's already decided about a world stays decided.
            WorldRandom kinds = random.Fork("kind");
            EnemyKind kind = boss ? Bestiary.Warlord
                : undead ? (kinds.Chance(MournerChance) ? Bestiary.Mourner : Bestiary.Restless)
                : archer ? Bestiary.Archer
                : spawn.Role == SpawnRole.Guard ? Bestiary.Sentry
                : kinds.Chance(CasterChance) ? kinds.Pick(Bestiary.Casters) : Bestiary.Outlaw;
            int trait = elite ? kinds.Int(Bestiary.Traits.Length) : Bestiary.NoTrait;

            var rank = boss ? EnemyRank.Boss : elite ? EnemyRank.Elite : EnemyRank.Normal;
            Dino_Defenders.Progression.Item[]? gear = null;
            string? title = null;
            Loadout loadout = boss ? Bestiary.Boss(kind, level, random.Fork("boss"), out gear, out title) : session.Bestiary.Shared(kind, level, rank, trait);

            string name = boss ? session.Chronicle.Warlord : kind.Name;
            if (title != null) name = $"{name}, {title}";
            if (elite) name = $"{Bestiary.Traits[trait].Name} {kind.Name}";

            var enemy = new EnemyComponent { Name = name, Rank = rank, Level = level };
            var brain = new EnemyBrainComponent
            {
                SiteIndex = siteIndex,
                SpawnIndex = spawnIndex,
                Home = spawn.Position,
                AggroRadius = kind.AggroRadius,
                LeashRadius = kind.LeashRadius,
                KeepsAway = kind.KeepsAway,
                Experience = Bestiary.ExperienceAt(level, rankHealth),
                Cooldown = random.Range(0f, 1f),
            };

            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            var actor = new Actor(Team.Enemies, loadout, spawn.Position, WorldData.CharacterRadius * MathF.Min(scale, 1.4f))
            {
                Site = state,
                Body = model,
                Gear = gear,
                Height = position.Y,
                Nature = boss ? Condition.Boss : elite ? Condition.Elite : Condition.None,
            };

            float importance = boss ? 4f : elite ? 2f : 1.5f;
            actor.Entity = world.CreateEntity(name, Bodies.Walking(_server, model, position, scale, importance, enemy, brain, new HealthComponent(actor.Sheet.MaxHealth), new SwingComponent(), new AfflictedComponent(), new ActorComponent(actor)));
            state.Live.Add(actor.Entity);
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
                new ScaleComponent(new Vector3(1.1f)),
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
                    Position = position + Vector3.UnitY * 5.6f,
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
    /// What heroes can pick up and use. Loot lies where it fell and comes to the nearest hero when they are close; E uses whatever
    /// they are standing by.
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
                    session.Combat.OpenChest(hero, view.Get<PositionComponent>().value, session.World.Plan.Level + (int)(state.Site.Remoteness * 3f));
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
                    pickup.Age += deltaTime;

                    ServerPlayer? hero = session.NearestHero(pickup.Rest.Xz, out float distance);
                    if (hero == null)
                    {
                        entity.Set(pickup);
                        continue;
                    }

                    // Gold flies to the hero; items wait to be walked over, so that a full pack doesn't waste them.
                    if (pickup.Item == null && distance < MagnetReach && pickup.Age > 0.4f)
                    {
                        Vector2 toHero = hero.Sim.Position - pickup.Rest.Xz;
                        pickup.Rest += new Vector3(toHero.X, 0f, toHero.Y) / MathF.Max(distance, 0.1f) * 22f * deltaTime;
                        entity.Set(new PositionComponent(pickup.Rest));
                    }

                    if (distance < PickupReach && pickup.Age > 0.4f && Collect(hero, pickup))
                    {
                        world.DestroyEntity(entity.Entity);
                        continue;
                    }
                    entity.Set(pickup);
                }
            }
        }

        private bool Collect(ServerPlayer hero, in PickupComponent pickup)
        {
            Vector3 position = pickup.Rest;
            if (pickup.Item == null)
            {
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
                return true;
            }

            if (!hero.Hero.Pickup(pickup.Item)) return false;
            hero.GearChanged = true;
            _server.Broadcast(new SoundEvent { Position = position, Cue = SoundCue.Find, Value = (int)pickup.Item.Rarity }, position.Xz);
            _server.Note(hero, $"Found: {pickup.Item.Name}");
            return true;
        }
    }
}
