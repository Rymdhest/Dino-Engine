using Dino_Defenders.Progression;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Gameplay
{
    /// <summary>
    /// Fills places with their people as the hero comes near, and empties them again when they leave. Who appears depends on what
    /// has happened there: the dead stay dead, opened chests stay open, and a village under threat keeps its merchants indoors.
    /// </summary>
    public class EncounterSystem : SystemBase
    {
        private const float WakeDistance = 100f;
        private const float SleepDistance = 160f;
        private const float EliteChance = 0.14f;

        private readonly CharacterFactory _characters;
        private float _nextCheck;
        private glModel? _chestModel;

        public EncounterSystem(CharacterFactory characters)
            : base(new BitMask(typeof(PlayerTag)))
        {
            _characters = characters;
        }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            if (GameSession.Current == null || Engine.Time < _nextCheck) return;
            _nextCheck = Engine.Time + 0.25f;

            GameSession session = GameSession.Current;
            session.UpdateDiscovery();
            Vector2 hero = session.PlayerPosition;

            for (int i = 0; i < session.Sites.Count; i++)
            {
                SiteState state = session.Sites[i];
                float distance = (state.Site.Position - hero).Length - state.Site.Radius;
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

        private void Wake(ECSWorld world, GameSession session, SiteState state, int siteIndex)
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
                        if (session.IsSafe(state)) SpawnMerchant(world, session, state, siteIndex, i, spawn, spawnRandom);
                        break;
                    case SpawnRole.Elder:
                        SpawnElder(world, session, state, siteIndex, i, spawn, spawnRandom);
                        break;
                    case SpawnRole.Chest:
                        if (!state.Opened.Contains(i)) SpawnChest(world, session, state, siteIndex, i, spawn);
                        break;
                    case SpawnRole.Altar:
                        SpawnMarker(world, state, siteIndex, i, spawn, InteractKind.Shrine, state.Kindled ? "Rest at the shrine" : "Kindle the shrine", 5f);
                        break;
                    case SpawnRole.Waygate:
                        SpawnMarker(world, state, siteIndex, i, spawn, InteractKind.Waygate, "Step through the waygate", 6f);
                        break;
                }
            }
        }

        private void SpawnEnemy(ECSWorld world, GameSession session, SiteState state, int siteIndex, int spawnIndex, SiteSpawn spawn, WorldRandom random)
        {
            int level = session.World.Plan.Level + (int)(state.Site.Remoteness * 3f);
            bool boss = spawn.Role == SpawnRole.Boss;
            bool undead = spawn.Role == SpawnRole.Undead;
            bool archer = !undead && !boss && random.Chance(spawn.Role == SpawnRole.Guard ? 0.45f : 0.22f);
            bool elite = !boss && random.Chance(EliteChance);

            float rankHealth = boss ? 11f : elite ? 2.6f : 1f;
            float rankDamage = boss ? 1.9f : elite ? 1.4f : 1f;
            float scale = boss ? 1.7f : elite ? 1.22f : random.Range(0.94f, 1.06f);

            // A level above yours hits harder and takes longer to bring down, and it adds up.
            float health = 26f * MathF.Pow(1.17f, level) * rankHealth;
            float damage = 7f * MathF.Pow(1.14f, level) * rankDamage * (archer ? 0.75f : 1f);

            string kind = boss ? session.Chronicle.Warlord : undead ? "Restless Dead" : archer ? "Outlaw Archer" : spawn.Role == SpawnRole.Guard ? "Sentry" : "Outlaw";
            string name = elite ? $"Fearsome {kind}" : kind;
            glModel model = boss ? _characters.Warlord : undead ? _characters.Skeleton : _characters.Bandit(random.Int(8), archer);

            var enemy = new EnemyComponent
            {
                Name = name,
                Rank = boss ? EnemyRank.Boss : elite ? EnemyRank.Elite : EnemyRank.Normal,
                Level = level,
                SiteIndex = siteIndex,
                SpawnIndex = spawnIndex,
                Home = spawn.Position,
                Damage = damage,
                Armour = 6f * level * (boss ? 2f : 1f),
                MoveSpeed = (undead ? 5.2f : 6.8f) * (elite ? 1.1f : 1f),
                AggroRadius = boss ? 26f : archer ? 30f : 21f,
                LeashRadius = boss ? 70f : 60f,
                AttackRange = archer ? 24f : 4.6f * scale,
                WindUp = boss ? 0.6f : archer ? 0.75f : 0.5f,
                Ranged = archer,
                Experience = (int)(14f * MathF.Pow(1.14f, level) * rankHealth),
                Cooldown = random.Range(0f, 1f),
            };

            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            state.Live.Add(world.CreateEntity(name, CharacterFactory.Body(name, model, position, scale, enemy, new HealthComponent(health))));
        }

        private void SpawnVillager(ECSWorld world, GameSession session, SiteState state, int siteIndex, SiteSpawn spawn, WorldRandom random, bool stationary)
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
            state.Live.Add(world.CreateEntity("Villager", CharacterFactory.Body("Villager", _characters.Villager(random.Int(12)), position, random.Range(0.92f, 1.04f), villager)));
        }

        private void SpawnMerchant(ECSWorld world, GameSession session, SiteState state, int siteIndex, int spawnIndex, SiteSpawn spawn, WorldRandom random)
        {
            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            var villager = new VillagerComponent { SiteIndex = siteIndex, Home = spawn.Position, Goal = spawn.Position, Stationary = true };
            var interactable = new InteractableComponent { Kind = InteractKind.Merchant, SiteIndex = siteIndex, SpawnIndex = spawnIndex, Radius = 5f, Prompt = "Trade" };
            state.Live.Add(world.CreateEntity("Merchant", CharacterFactory.Body("Merchant", _characters.Villager(random.Int(12)), position, 1f, villager, interactable)));
        }

        private void SpawnElder(ECSWorld world, GameSession session, SiteState state, int siteIndex, int spawnIndex, SiteSpawn spawn, WorldRandom random)
        {
            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            var villager = new VillagerComponent { SiteIndex = siteIndex, Home = spawn.Position, Goal = spawn.Position, Stationary = true };
            var interactable = new InteractableComponent { Kind = InteractKind.Elder, SiteIndex = siteIndex, SpawnIndex = spawnIndex, Radius = 5f, Prompt = "Speak with the elder" };
            state.Live.Add(world.CreateEntity("Elder", CharacterFactory.Body("Elder", _characters.Villager(random.Int(12)), position, 1.02f, villager, interactable)));
        }

        private void SpawnChest(ECSWorld world, GameSession session, SiteState state, int siteIndex, int spawnIndex, SiteSpawn spawn)
        {
            if (_chestModel == null)
            {
                var builder = new MeshBuilder();
                VertexMaterial boards = new VertexMaterial(TextureGenerator.wood, new Colour(120, 84, 56));
                VertexMaterial iron = new VertexMaterial(TextureGenerator.iron, new Colour(90, 90, 96));
                builder.Box(new Vector3(0f, 0.6f, 0f), new Vector3(1f, 0.6f, 0.66f), boards);
                builder.Surface(6, 1, (u, v) =>
                {
                    float angle = u * MathF.PI;
                    return new Vector3((v * 2f - 1f) * 1f, 1.2f + MathF.Sin(angle) * 0.34f, MathF.Cos(angle) * 0.66f);
                }, boards);
                foreach (float x in new[] { -0.7f, 0.7f }) builder.Box(new Vector3(x, 0.8f, 0.68f), new Vector3(0.09f, 0.76f, 0.03f), iron);
                builder.Box(new Vector3(0f, 1.1f, 0.7f), new Vector3(0.14f, 0.18f, 0.05f), iron);
                _chestModel = glLoader.loadToVAO(builder.Build());
            }

            Vector3 position = new Vector3(spawn.Position.X, session.World.Height.Sample(spawn.Position), spawn.Position.Y);
            state.Live.Add(world.CreateEntity("Chest",
                new PositionComponent(position),
                new RotationComponent(new Vector3(0f, spawn.Position.X, 0f)),
                new ScaleComponent(new Vector3(1.1f)),
                new ModelComponent(_chestModel),
                new ModelRenderTag(),
                new LocalToWorldMatrixComponent(),
                new InteractableComponent { Kind = InteractKind.Chest, SiteIndex = siteIndex, SpawnIndex = spawnIndex, Radius = 4.5f, Prompt = "Open the chest" }));
        }

        private static void SpawnMarker(ECSWorld world, SiteState state, int siteIndex, int spawnIndex, SiteSpawn spawn, InteractKind kind, string prompt, float radius)
        {
            float height = GameSession.Current.World.Height.Sample(spawn.Position);
            state.Live.Add(world.CreateEntity(prompt,
                new PositionComponent(new Vector3(spawn.Position.X, height, spawn.Position.Y)),
                new InteractableComponent { Kind = kind, SiteIndex = siteIndex, SpawnIndex = spawnIndex, Radius = radius, Prompt = prompt }));
        }
    }

    /// <summary>
    /// Villagers about their day: walking between spots near home and stopping a while, and speaking when the hero passes. What
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

        private readonly WorldRandom _random = new WorldRandom(Environment.TickCount);

        public VillagerSystem()
            : base(new BitMask(typeof(VillagerComponent), typeof(PositionComponent), typeof(VelocityComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            GameSession session = GameSession.Current;
            var villager = entity.Get<VillagerComponent>();
            Vector3 position = entity.Get<PositionComponent>().value;
            Vector2 toHero = session.PlayerPosition - position.Xz;

            if (toHero.LengthSquared < SpeechReach * SpeechReach && Engine.Time >= villager.NextSpeech)
            {
                bool safe = session.IsSafe(session.Sites[villager.SiteIndex]);
                session.Say(position + Vector3.UnitY * 5.6f, _random.Pick(safe ? Grateful : Afraid), new Vector3(0.92f, 0.9f, 0.8f), 14f, 3.2f);
                villager.NextSpeech = Engine.Time + _random.Range(9f, 18f);
            }

            Vector2 velocity = Vector2.Zero;
            if (villager.Stationary)
            {
                CharacterFactory.Face(entity, toHero.LengthSquared < 100f ? toHero : Vector2.Zero, deltaTime, 4f);
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
                    CharacterFactory.Face(entity, velocity, deltaTime, 6f);
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
    /// What the hero can pick up and use. Loot bobs where it fell and comes to them when they are close; E uses whatever they are
    /// standing by.
    /// </summary>
    public class InteractionSystem : SystemBase
    {
        private const float PickupReach = 3.4f;
        private const float MagnetReach = 7f;

        private static readonly BitMask PickupMask = new BitMask(typeof(PickupComponent), typeof(PositionComponent));

        public InteractionSystem()
            : base(new BitMask(typeof(InteractableComponent), typeof(PositionComponent)))
        {
        }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            GameSession session = GameSession.Current;
            if (session == null) return;
            UpdatePickups(world, session, deltaTime);

            // The nearest thing in reach is the one on offer.
            Entity nearest = default;
            float best = float.PositiveInfinity;
            foreach (var archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                foreach (var entity in new ComponentAccessor(archetype))
                {
                    var interactable = entity.Get<InteractableComponent>();
                    float distance = (entity.Get<PositionComponent>().value.Xz - session.PlayerPosition).Length;
                    if (distance > interactable.Radius || distance >= best) continue;
                    best = distance;
                    nearest = entity.Entity;
                }
            }

            session.InteractPrompt = null;
            if (!nearest.IsValid() || session.Health <= 0f) return;

            EntityView view = world.GetEntityView(nearest);
            var offered = view.Get<InteractableComponent>();
            session.InteractPrompt = offered.Prompt;
            if (!Engine.Gui.WantsKeyboard && Engine.WindowHandler.IsKeyPressed(OpenTK.Windowing.GraphicsLibraryFramework.Keys.E)) Use(world, session, view, offered);
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        private static void Use(ECSWorld world, GameSession session, EntityView view, in InteractableComponent interactable)
        {
            SiteState state = session.Sites[interactable.SiteIndex];
            switch (interactable.Kind)
            {
                case InteractKind.Shrine:
                    UseShrine(world, session, state, view);
                    break;
                case InteractKind.Chest:
                    state.Opened.Add(interactable.SpawnIndex);
                    state.Live.Remove(view.Entity);
                    Combat.OpenChest(world, session, view.Get<PositionComponent>().value, session.World.Plan.Level + (int)(state.Site.Remoteness * 3f));
                    world.DestroyEntity(view.Entity);
                    break;
                default:
                    session.InteractionOpened?.Invoke(interactable.Kind, interactable.SiteIndex);
                    break;
            }
        }

        private static void UseShrine(ECSWorld world, GameSession session, SiteState state, EntityView view)
        {
            Vector3 position = view.Get<PositionComponent>().value;
            session.Health = session.Stats.MaxHealth;
            session.Hero.Flasks = HeroState.MaxFlasks;
            session.Respawn = position.Xz;
            Combat.Spark(world, position + Vector3.UnitY * 3f, new Colour(0.5f, 1f, 0.75f, 2.5f), 26, 7f);

            if (state.Kindled)
            {
                session.Note("You rest at the shrine.");
                return;
            }
            state.Kindled = true;
            session.Announce("Shrine kindled", "Your flasks are full, and you will wake here if you fall.");
            session.GiveExperience(25 * session.World.Plan.Level);
            if (state.Site.Kind == SiteKind.Shrine) session.Advance(ObjectiveKind.PrayAtShrines, SiteKind.Shrine);
        }

        private static void UpdatePickups(ECSWorld world, GameSession session, float deltaTime)
        {
            foreach (var archetype in world.QueryArchetypes(PickupMask, BitMask.Empty))
            {
                foreach (var entity in new ComponentAccessor(archetype))
                {
                    var pickup = entity.Get<PickupComponent>();
                    pickup.Age += deltaTime;

                    Vector3 position = pickup.Rest + Vector3.UnitY * (0.3f + 0.2f * MathF.Sin(pickup.Age * 3f));
                    Vector2 toHero = session.PlayerPosition - pickup.Rest.Xz;
                    float distance = toHero.Length;

                    // Gold flies to the hero; items wait to be walked over, so that a full pack doesn't waste them.
                    if (pickup.Item == null && distance < MagnetReach && pickup.Age > 0.4f)
                    {
                        pickup.Rest += new Vector3(toHero.X, 0f, toHero.Y) / MathF.Max(distance, 0.1f) * 22f * deltaTime;
                    }

                    if (distance < PickupReach && pickup.Age > 0.4f && Collect(session, pickup, position))
                    {
                        world.DestroyEntity(entity.Entity);
                        continue;
                    }

                    entity.Set(new PositionComponent(position));
                    entity.Set(pickup);
                }
            }
        }

        private static bool Collect(GameSession session, in PickupComponent pickup, Vector3 position)
        {
            if (pickup.Item == null)
            {
                session.Hero.Gold += pickup.Gold;
                session.Say(position + Vector3.UnitY * 3f, $"+{pickup.Gold} gold", new Vector3(1f, 0.82f, 0.3f), 14f, 1.1f);
                return true;
            }

            if (!session.Hero.Pickup(pickup.Item)) return false;
            session.Note($"Found: {pickup.Item.Name}");
            return true;
        }
    }
}
