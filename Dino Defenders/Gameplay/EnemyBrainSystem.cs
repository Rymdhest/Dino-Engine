using Dino_Engine.Animation;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Defenders.Gameplay
{
    /// <summary>
    /// What enemies do. They hold their ground until the hero comes close, run them down, and take a moment over each blow so that
    /// it can be seen coming and stepped away from. Stray too far from home and they give up and go back. When the straight
    /// way is blocked they find a path over the world's passable ground.
    /// </summary>
    public class EnemyBrainSystem : SystemBase
    {
        private const float RecoverTime = 0.75f;
        private const float PathRefresh = 0.8f;
        private const float WaypointReach = 2.2f;
        private const float ArrowSpeed = 42f;

        private readonly WorldRandom _random = new WorldRandom(Environment.TickCount);
        private readonly WorldPathfinder _paths = new();

        public EnemyBrainSystem()
            : base(new BitMask(typeof(EnemyComponent), typeof(VelocityComponent), typeof(HealthComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            GameSession session = GameSession.Current;
            var enemy = entity.Get<EnemyComponent>();
            if (enemy.State == EnemyState.Dead) return;

            Vector2 position = entity.Get<PositionComponent>().value.Xz;
            Vector2 hero = session.PlayerPosition;
            float toHero = (hero - position).Length;
            bool heroDown = session.Health <= 0f;

            enemy.StateTime += deltaTime;
            enemy.Cooldown -= deltaTime;

            Vector2 velocity = Vector2.Zero;
            switch (enemy.State)
            {
                case EnemyState.Idle:
                    velocity = Wander(ref enemy, position);
                    if (!heroDown && toHero < enemy.AggroRadius) Change(ref enemy, EnemyState.Chasing);
                    break;

                case EnemyState.Chasing:
                    if (heroDown || (position - enemy.Home).Length > enemy.LeashRadius)
                    {
                        Change(ref enemy, EnemyState.Returning);
                    }
                    else if (toHero <= enemy.AttackRange && enemy.Cooldown <= 0f)
                    {
                        Change(ref enemy, EnemyState.WindingUp);
                        PlaySwing(entity, enemy);
                    }
                    else if (toHero > enemy.AttackRange * 0.75f)
                    {
                        velocity = MoveTowards(ref enemy, session.World, position, hero) * enemy.MoveSpeed;
                    }
                    else if (enemy.Ranged)
                    {
                        // Archers keep their distance rather than stand and be cut down.
                        velocity = (position - hero).Normalized() * enemy.MoveSpeed * 0.6f;
                    }
                    CharacterFactory.Face(entity, hero - position, deltaTime);
                    break;

                case EnemyState.WindingUp:
                    CharacterFactory.Face(entity, hero - position, deltaTime, 6f);
                    if (enemy.StateTime >= enemy.WindUp)
                    {
                        Strike(world, session, entity, enemy, position, hero);
                        enemy.Cooldown = enemy.Rank == EnemyRank.Boss ? 1.1f : 1.5f;
                        Change(ref enemy, EnemyState.Recovering);
                    }
                    break;

                case EnemyState.Recovering:
                    if (enemy.StateTime >= RecoverTime) Change(ref enemy, EnemyState.Chasing);
                    break;

                case EnemyState.Returning:
                    if ((position - enemy.Home).Length < 3f)
                    {
                        Change(ref enemy, EnemyState.Idle);
                        var health = entity.Get<HealthComponent>();
                        health.Current = health.Max;
                        entity.Set(health);
                    }
                    else
                    {
                        velocity = MoveTowards(ref enemy, session.World, position, enemy.Home) * enemy.MoveSpeed;
                        CharacterFactory.Face(entity, velocity, deltaTime);
                    }
                    break;
            }

            entity.Set(new VelocityComponent(new Vector3(velocity.X, 0f, velocity.Y)));
            entity.Set(enemy);
        }

        private static void Change(ref EnemyComponent enemy, EnemyState state)
        {
            enemy.State = state;
            enemy.StateTime = 0f;
            enemy.Path = null;
        }

        private Vector2 Wander(ref EnemyComponent enemy, Vector2 position)
        {
            if (enemy.StateTime > 3f || enemy.Wander == default)
            {
                enemy.StateTime = 0f;
                enemy.Wander = enemy.Home + new Vector2(_random.Range(-6f, 6f), _random.Range(-6f, 6f));
            }
            Vector2 offset = enemy.Wander - position;
            if (offset.Length < 1f || enemy.Rank == EnemyRank.Boss) return Vector2.Zero;
            return offset.Normalized() * enemy.MoveSpeed * 0.3f;
        }

        // The straight line if it is clear; otherwise a path, refreshed now and then as the target moves.
        private Vector2 MoveTowards(ref EnemyComponent enemy, WorldData world, Vector2 position, Vector2 target)
        {
            if (WorldPathfinder.HasClearLine(world, position, target))
            {
                enemy.Path = null;
                return (target - position).Normalized();
            }

            if (enemy.Path == null || Engine.Time >= enemy.NextPathTime)
            {
                enemy.Path = _paths.Find(world, position, target);
                enemy.PathStep = 0;
                enemy.NextPathTime = Engine.Time + PathRefresh;
            }

            if (enemy.Path == null || enemy.Path.Count == 0) return (target - position).Normalized();

            while (enemy.PathStep < enemy.Path.Count - 1 && (enemy.Path[enemy.PathStep] - position).Length < WaypointReach) enemy.PathStep++;
            Vector2 offset = enemy.Path[enemy.PathStep] - position;
            return offset.LengthSquared > 0.01f ? offset.Normalized() : Vector2.Zero;
        }

        private static void PlaySwing(EntityView entity, in EnemyComponent enemy)
        {
            var animation = entity.Get<AnimationComponent>();
            animation.PlayLayer(AnimationId.Swing, loop: false, speed: 0.4f / MathF.Max(enemy.WindUp, 0.1f));
            entity.Set(animation);
        }

        private static void Strike(ECSWorld world, GameSession session, EntityView entity, in EnemyComponent enemy, Vector2 position, Vector2 hero)
        {
            if (enemy.Ranged)
            {
                FireArrow(world, entity, enemy, position, hero);
                return;
            }

            // Reach is checked now, when the blow lands: stepping back during the wind-up makes it miss.
            float reach = enemy.AttackRange + 1.2f;
            if ((hero - position).LengthSquared <= reach * reach) Combat.HurtHero(world, session, enemy.Damage, enemy.Level);
        }

        private static glModel? _arrowModel;

        private static void FireArrow(ECSWorld world, EntityView entity, in EnemyComponent enemy, Vector2 position, Vector2 hero)
        {
            if (_arrowModel == null)
            {
                var builder = new MeshBuilder();
                builder.Tube(new Vector3(0f, 0f, -0.9f), new Vector3(0f, 0f, 0.9f), 0.05f, 0.05f, 4, new VertexMaterial(TextureGenerator.wood, new Colour(150, 120, 90)));
                builder.Tube(new Vector3(0f, 0f, 0.9f), new Vector3(0f, 0f, 1.2f), 0.1f, 0.01f, 4, new VertexMaterial(TextureGenerator.iron));
                _arrowModel = glLoader.loadToVAO(builder.Build());
            }

            Vector2 direction = (hero - position).Normalized();
            float height = entity.Get<PositionComponent>().value.Y + 3.2f;
            float yaw = MathF.Atan2(direction.X, direction.Y);
            world.CreateEntity("Arrow",
                new PositionComponent(new Vector3(position.X + direction.X * 1.5f, height, position.Y + direction.Y * 1.5f)),
                new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitY, yaw)),
                new ScaleComponent(new Vector3(1f)),
                new ModelComponent(_arrowModel),
                new ModelRenderTag(),
                new LocalToWorldMatrixComponent(),
                new VelocityComponent(new Vector3(direction.X, 0f, direction.Y) * ArrowSpeed),
                new ProjectileComponent { Damage = enemy.Damage, Life = 1.6f });
        }
    }

    /// <summary>Flies arrows until they find the hero, something solid, or the end of their strength.</summary>
    public class ProjectileSystem : SystemBase
    {
        public ProjectileSystem()
            : base(new BitMask(typeof(ProjectileComponent), typeof(PositionComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            GameSession session = GameSession.Current;
            var projectile = entity.Get<ProjectileComponent>();
            projectile.Life -= deltaTime;
            entity.Set(projectile);

            Vector3 position = entity.Get<PositionComponent>().value;
            bool hitHero = (position.Xz - session.PlayerPosition).LengthSquared < 2.4f * 2.4f;
            bool blocked = session.World.Placement.OverlapsSolid(Footprint.Circle(position.Xz, 0.3f));

            if (hitHero) Combat.HurtHero(world, session, projectile.Damage, session.World.Plan.Level);
            if (hitHero || blocked || projectile.Life <= 0f) world.DestroyEntity(entity.Entity);
        }
    }

    /// <summary>Topples the fallen and clears them away.</summary>
    public class DyingSystem : SystemBase
    {
        private const float FallTime = 0.5f;
        private const float LieTime = 2.6f;
        private const float FadeTime = 0.6f;

        public DyingSystem()
            : base(new BitMask(typeof(EnemyComponent), typeof(RotationComponent), typeof(ScaleComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var enemy = entity.Get<EnemyComponent>();
            if (enemy.State != EnemyState.Dead) return;

            enemy.StateTime += deltaTime;
            entity.Set(enemy);

            float fall = WorldMath.Smoothstep(0f, FallTime, enemy.StateTime);
            var rotation = entity.Get<RotationComponent>();
            rotation.quaternion = Quaternion.FromAxisAngle(enemy.FallAxis, fall * MathF.PI * 0.5f) * enemy.DeathRotation;
            entity.Set(rotation);

            float fade = 1f - WorldMath.Smoothstep(FallTime + LieTime, FallTime + LieTime + FadeTime, enemy.StateTime);
            entity.Set(new ScaleComponent(enemy.DeathScale * MathF.Max(fade, 0.01f)));

            if (enemy.StateTime >= FallTime + LieTime + FadeTime) world.DestroyEntity(entity.Entity);
        }
    }

    /// <summary>
    /// A* over a world's passable ground, for short trips around what stands in the way. It gives up after a fixed amount of
    /// searching: a character that can't quickly find a way is better off pushing on than stalling the frame.
    /// </summary>
    public sealed class WorldPathfinder
    {
        private const float Step = 3f;
        private const int SearchLimit = 700;

        private readonly Dictionary<long, (long from, float cost)> _visited = new();
        private readonly PriorityQueue<long, float> _frontier = new();

        public static bool HasClearLine(WorldData world, Vector2 from, Vector2 to)
        {
            float length = (to - from).Length;
            int steps = Math.Max((int)(length / 1.5f), 1);
            for (int i = 1; i <= steps; i++)
            {
                if (!world.Passable.IsSet(Vector2.Lerp(from, to, (float)i / steps))) return false;
            }
            return true;
        }

        private static long Key(int x, int z) => ((long)x << 32) | (uint)z;
        private static Vector2 Position(long key) => new Vector2((int)(key >> 32), (int)(key & 0xFFFFFFFF)) * Step;

        public List<Vector2>? Find(WorldData world, Vector2 from, Vector2 to)
        {
            _visited.Clear();
            _frontier.Clear();

            int startX = (int)MathF.Round(from.X / Step);
            int startZ = (int)MathF.Round(from.Y / Step);
            long start = Key(startX, startZ);
            _visited[start] = (start, 0f);
            _frontier.Enqueue(start, 0f);

            long best = start;
            float bestDistance = (from - to).Length;
            int searched = 0;

            while (_frontier.TryDequeue(out long current, out _) && searched++ < SearchLimit)
            {
                Vector2 here = Position(current);
                float remaining = (here - to).Length;
                if (remaining < bestDistance)
                {
                    bestDistance = remaining;
                    best = current;
                }
                if (remaining < Step) break;

                int x = (int)(current >> 32);
                int z = (int)(current & 0xFFFFFFFF);
                float cost = _visited[current].cost;
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        long next = Key(x + dx, z + dz);
                        Vector2 there = Position(next);
                        if (!world.Passable.IsSet(there)) continue;

                        float total = cost + (dx != 0 && dz != 0 ? Step * 1.414f : Step);
                        if (_visited.TryGetValue(next, out var seen) && seen.cost <= total) continue;
                        _visited[next] = (current, total);
                        _frontier.Enqueue(next, total + (there - to).Length);
                    }
                }
            }

            if (best == start) return null;

            var path = new List<Vector2>();
            for (long node = best; node != start; node = _visited[node].from) path.Add(Position(node));
            path.Reverse();
            return path;
        }
    }
}
