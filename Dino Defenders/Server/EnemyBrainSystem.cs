using Dino_Defenders.Gameplay;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Defenders.Server
{
    /// <summary>
    /// What enemies do. They hold their ground until a hero comes close, run them down, and take a moment over each blow so that
    /// it can be seen coming and stepped away from. Stray too far from home and they give up and go back. When the straight
    /// way is blocked they find a path over the world's passable ground.
    /// </summary>
    public class EnemyBrainSystem : SystemBase
    {
        private const float RecoverTime = 0.75f;
        private const float PathRefresh = 0.8f;
        private const float WaypointReach = 2.2f;
        private const float ArrowSpeed = 42f;

        private readonly GameServer _server;
        private readonly WorldRandom _random = new WorldRandom(Environment.TickCount);
        private readonly WorldPathfinder _paths = new();

        public EnemyBrainSystem(GameServer server)
            : base(new BitMask(typeof(EnemyComponent), typeof(EnemyBrainComponent), typeof(VelocityComponent), typeof(HealthComponent)))
        {
            _server = server;
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            ServerSession session = _server.Session!;
            var enemy = entity.Get<EnemyComponent>();
            if (enemy.State == EnemyState.Dead) return;
            if (session.Paused)
            {
                entity.Set(new VelocityComponent());
                return;
            }

            var brain = entity.Get<EnemyBrainComponent>();
            Vector2 position = entity.Get<PositionComponent>().value.Xz;
            ServerPlayer? target = session.NearestHero(position, out float toHero);
            Vector2 hero = target?.Sim.Position ?? position;
            bool heroDown = target == null;

            brain.StateTime += deltaTime;
            brain.Cooldown -= deltaTime;

            Vector2 velocity = Vector2.Zero;
            switch (enemy.State)
            {
                case EnemyState.Idle:
                    velocity = Wander(ref brain, enemy, position);
                    if (!heroDown && toHero < brain.AggroRadius) Change(ref enemy, ref brain, EnemyState.Chasing);
                    break;

                case EnemyState.Chasing:
                    if (heroDown || (position - brain.Home).Length > brain.LeashRadius)
                    {
                        Change(ref enemy, ref brain, EnemyState.Returning);
                    }
                    else if (toHero <= brain.AttackRange && brain.Cooldown <= 0f)
                    {
                        Change(ref enemy, ref brain, EnemyState.WindingUp);
                        PlaySwing(entity, brain);
                    }
                    else if (toHero > brain.AttackRange * 0.75f)
                    {
                        velocity = MoveTowards(ref brain, session, position, hero) * brain.MoveSpeed;
                    }
                    else if (brain.Ranged)
                    {
                        // Archers keep their distance rather than stand and be cut down.
                        velocity = (position - hero).Normalized() * brain.MoveSpeed * 0.6f;
                    }
                    Face(entity, hero - position, deltaTime);
                    break;

                case EnemyState.WindingUp:
                    Face(entity, hero - position, deltaTime, 6f);
                    if (brain.StateTime >= brain.WindUp)
                    {
                        if (target != null) Strike(world, session, entity, enemy, brain, position, target);
                        brain.Cooldown = enemy.Rank == EnemyRank.Boss ? 1.1f : 1.5f;
                        Change(ref enemy, ref brain, EnemyState.Recovering);
                    }
                    break;

                case EnemyState.Recovering:
                    if (brain.StateTime >= RecoverTime) Change(ref enemy, ref brain, EnemyState.Chasing);
                    break;

                case EnemyState.Returning:
                    if ((position - brain.Home).Length < 3f)
                    {
                        Change(ref enemy, ref brain, EnemyState.Idle);
                        var health = entity.Get<HealthComponent>();
                        health.Current = health.Max;
                        entity.Set(health);
                    }
                    else
                    {
                        velocity = MoveTowards(ref brain, session, position, brain.Home) * brain.MoveSpeed;
                        Face(entity, velocity, deltaTime);
                    }
                    break;
            }

            entity.Set(new VelocityComponent(new Vector3(velocity.X, 0f, velocity.Y)));
            entity.Set(brain);
            entity.Set(enemy);
        }

        public static void Face(EntityView entity, Vector2 direction, float deltaTime, float speed = 14f)
        {
            if (direction.LengthSquared < 1e-5f) return;
            var rotation = entity.Get<RotationComponent>();
            Quaternion target = Quaternion.FromAxisAngle(Vector3.UnitY, MathF.Atan2(direction.X, direction.Y));
            rotation.quaternion = Quaternion.Slerp(rotation.quaternion, target, Math.Clamp(speed * deltaTime, 0f, 1f));
            entity.Set(rotation);
        }

        private static void Change(ref EnemyComponent enemy, ref EnemyBrainComponent brain, EnemyState state)
        {
            enemy.State = state;
            brain.StateTime = 0f;
            brain.Path = null;
        }

        private Vector2 Wander(ref EnemyBrainComponent brain, in EnemyComponent enemy, Vector2 position)
        {
            if (brain.StateTime > 3f || brain.Wander == default)
            {
                brain.StateTime = 0f;
                brain.Wander = brain.Home + new Vector2(_random.Range(-6f, 6f), _random.Range(-6f, 6f));
            }
            Vector2 offset = brain.Wander - position;
            if (offset.Length < 1f || enemy.Rank == EnemyRank.Boss) return Vector2.Zero;
            return offset.Normalized() * brain.MoveSpeed * 0.3f;
        }

        // The straight line if it is clear; otherwise a path, refreshed now and then as the target moves.
        private Vector2 MoveTowards(ref EnemyBrainComponent brain, ServerSession session, Vector2 position, Vector2 target)
        {
            WorldData world = session.World;
            if (WorldPathfinder.HasClearLine(world, position, target))
            {
                brain.Path = null;
                return (target - position).Normalized();
            }

            if (brain.Path == null || session.Time >= brain.NextPathTime)
            {
                brain.Path = _paths.Find(world, position, target);
                brain.PathStep = 0;
                brain.NextPathTime = session.Time + PathRefresh;
            }

            if (brain.Path == null || brain.Path.Count == 0) return (target - position).Normalized();

            while (brain.PathStep < brain.Path.Count - 1 && (brain.Path[brain.PathStep] - position).Length < WaypointReach) brain.PathStep++;
            Vector2 offset = brain.Path[brain.PathStep] - position;
            return offset.LengthSquared > 0.01f ? offset.Normalized() : Vector2.Zero;
        }

        private static void PlaySwing(EntityView entity, in EnemyBrainComponent brain)
        {
            var swing = entity.Get<SwingComponent>();
            swing.Serial++;
            swing.Speed = 0.4f / MathF.Max(brain.WindUp, 0.1f);
            entity.Set(swing);
        }

        private void Strike(ECSWorld world, ServerSession session, EntityView entity, in EnemyComponent enemy, in EnemyBrainComponent brain, Vector2 position, ServerPlayer target)
        {
            Vector2 hero = target.Sim.Position;
            if (brain.Ranged)
            {
                FireArrow(world, entity, brain, position, hero);
                return;
            }

            // Reach is checked now, when the blow lands: stepping back during the wind-up makes it miss.
            float reach = brain.AttackRange + 1.2f;
            if ((hero - position).LengthSquared <= reach * reach) session.Combat.HurtHero(target, brain.Damage, enemy.Level);
        }

        private void FireArrow(ECSWorld world, EntityView entity, in EnemyBrainComponent brain, Vector2 position, Vector2 hero)
        {
            Vector2 direction = (hero - position).Normalized();
            float height = entity.Get<PositionComponent>().value.Y + 3.2f;
            float yaw = MathF.Atan2(direction.X, direction.Y);
            var origin = new Vector3(position.X + direction.X * 1.5f, height, position.Y + direction.Y * 1.5f);

            world.CreateEntity("Arrow",
                new NetIdentityComponent(_server.Ghosts.NextId(), importance: 2f),
                new PositionComponent(origin),
                new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitY, yaw)),
                new VisualComponent("arrow", VisualKind.Prop),
                new BallisticComponent { Origin = origin, Velocity = new Vector3(direction.X, 0f, direction.Y) * ArrowSpeed, StartTick = _server.Tick },
                new ProjectileComponent { Damage = brain.Damage, Life = 1.6f });
        }
    }

    /// <summary>Flies arrows until they find a hero, something solid, or the end of their strength.</summary>
    public class ProjectileSystem : SystemBase
    {
        private const float HitReach = 2.4f;

        private readonly GameServer _server;

        public ProjectileSystem(GameServer server)
            : base(new BitMask(typeof(ProjectileComponent), typeof(BallisticComponent), typeof(PositionComponent)))
        {
            _server = server;
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            ServerSession session = _server.Session!;
            var projectile = entity.Get<ProjectileComponent>();
            projectile.Life -= deltaTime;
            entity.Set(projectile);

            Vector3 position = entity.Get<BallisticComponent>().PositionAt(_server.Tick);
            entity.Set(new PositionComponent(position));

            ServerPlayer? struck = null;
            foreach (ServerPlayer hero in session.Heroes)
            {
                if (hero.Sim.Down || (position.Xz - hero.Sim.Position).LengthSquared >= HitReach * HitReach) continue;
                struck = hero;
                break;
            }

            bool blocked = position.Y < session.World.Height.Sample(position.Xz) || session.World.Placement.OverlapsSolid(Footprint.Circle(position.Xz, 0.3f));

            if (struck != null) session.Combat.HurtHero(struck, projectile.Damage, session.World.Plan.Level);
            if (struck != null || blocked || projectile.Life <= 0f) world.DestroyEntity(entity.Entity);
        }
    }

    /// <summary>Clears away the fallen once they have had their time on the ground. How they fall is drawn on the client.</summary>
    public class DyingSystem : SystemBase
    {
        public const float FallTime = 0.5f;
        public const float LieTime = 2.6f;
        public const float FadeTime = 0.6f;

        public DyingSystem()
            : base(new BitMask(typeof(EnemyComponent), typeof(EnemyBrainComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            if (entity.Get<EnemyComponent>().State != EnemyState.Dead) return;

            var brain = entity.Get<EnemyBrainComponent>();
            brain.StateTime += deltaTime;
            entity.Set(brain);

            if (brain.StateTime >= FallTime + LieTime + FadeTime) world.DestroyEntity(entity.Entity);
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
