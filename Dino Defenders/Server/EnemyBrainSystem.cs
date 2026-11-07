using Dino_Defenders.Books;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Defenders.Server
{
    /// <summary>
    /// What creatures do. They hold their ground until a foe comes close, run them down, and take a moment over each blow so that it can be seen
    /// coming and stepped away from. Stray too far from home and they give up and go back. When the straight way is blocked they find a path over
    /// the world's passable ground. A minion is the same creature, whose home is at its owner's side.
    /// </summary>
    public class EnemyBrainSystem : SystemBase
    {
        private const float RecoverTime = 0.75f;
        private const float PathRefresh = 0.8f;
        private const float WaypointReach = 2.2f;
        private const float ShortestWindUp = 0.6f;      // However quick the book, there's always a moment to get out of the way. A mark reaches a player's clock some 0.13 s late on the best of lines, and what's left has to be enough to act on.
        private const float HeavyWindUp = 0.7f;
        private const float BossWindUp = 1.25f;
        private const float EscapeSpeed = 12f;          // Metres a second it's taken that someone can get out of an area at. An area gives them that long, near enough.
        private const float EscapeShare = 0.7f;
        private const float MostFollowThrough = 0.75f;
        public const float PushFade = 9f;               // Per second.
        private const float ReelingSpeed = 0.2f;        // What's left of how fast it was going.
        private const float MeleeSlack = 1.2f;
        private const float FollowDistance = 7f;
        private const float CallReach = 20f;            // How far a cry carries. Less than the gap between packs, so one pack doesn't bring the next.
        private const float AlarmSight = 60f;           // A cry only sends someone after a foe that's near enough to be what it's about.
        private const int NoticedAlarm = 2;             // Whoever notices calls, and those who hear pass it on once.
        public const int StruckAlarm = 3;
        private const float PersonalSpace = 2.5f;
        private const float CrowdReach = 16f;           // Only a crowd around a foe has to sort itself out.
        private const int SpreadEvery = 3;              // Ticks between each one looking around it.
        private const int MostNeighbours = 8;

        private readonly GameServer _server;
        private readonly WorldRandom _random = new WorldRandom(Environment.TickCount);
        private readonly WorldPathfinder _paths = new();
        private readonly List<Actor> _near = new();

        public EnemyBrainSystem(GameServer server)
            : base(new BitMask(typeof(EnemyComponent), typeof(EnemyBrainComponent), typeof(VelocityComponent), typeof(ActorComponent)))
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

            Actor actor = entity.Get<ActorComponent>().Actor;
            if (actor.Still) return;
            var brain = entity.Get<EnemyBrainComponent>();

            // Frozen or stunned, nothing happens, and what it was winding up is lost.
            if (actor.Afflictions.Held)
            {
                if (enemy.State == EnemyState.WindingUp)
                {
                    session.Combat.Withdraw(brain.Mark);
                    session.Combat.Stop(entity.Entity);
                    brain.Mark = default;
                    Change(ref enemy, ref brain, EnemyState.Recovering);
                }
                entity.Set(new VelocityComponent(Carried(actor, Vector2.Zero, deltaTime)));
                entity.Set(brain);
                entity.Set(enemy);
                return;
            }

            float rate = actor.Afflictions.ActionRate;
            Vector2 position = actor.Position;
            float speed = actor.Sheet.MoveSpeed * rate;

            if (actor.Owner is Actor owner)
            {
                brain.Home = owner.Position;
                if (owner.Dead || (actor.MinionLeft -= deltaTime) <= 0f)
                {
                    session.Combat.Dismiss(actor);
                    return;
                }
            }

            Actor? target = session.NearestFoe(actor, out float toTarget);
            Vector2 foe = target?.Position ?? position;
            bool noFoe = target == null;

            if (enemy.State != EnemyState.Idle) actor.Alarm = 0;

            brain.StateTime += deltaTime * rate;
            brain.Cooldown -= deltaTime * rate;

            Vector2 velocity = Vector2.Zero;
            switch (enemy.State)
            {
                case EnemyState.Idle:
                    velocity = actor.Owner != null ? Heel(ref brain, session, position, speed) : Wander(ref brain, enemy, position, speed);
                    if (!noFoe && (toTarget < Noticing(brain, target!) || (actor.Alarm > 0 && toTarget < AlarmSight)))
                    {
                        Call(session, actor);
                        Change(ref enemy, ref brain, EnemyState.Chasing);
                    }
                    break;

                case EnemyState.Chasing:
                    if (noFoe || (position - brain.Home).Length > brain.LeashRadius)
                    {
                        Change(ref enemy, ref brain, EnemyState.Returning);
                        break;
                    }

                    int ready = brain.Cooldown <= 0f ? Ready(actor, toTarget) : -1;
                    float wants = Comfort(actor);
                    if (ready >= 0)
                    {
                        brain.Book = ready;
                        BookProfile chosen = actor.Sheet.Profile(ready);
                        Commit(session, entity.Entity, actor, ref brain, enemy.Rarity, chosen, position, foe, toTarget);
                        Change(ref enemy, ref brain, EnemyState.WindingUp);
                    }
                    else if (toTarget > wants * 0.75f)
                    {
                        velocity = MoveTowards(ref brain, session, position, foe) * speed;
                    }
                    else if (brain.KeepsAway)
                    {
                        // Archers keep their distance rather than stand and be cut down.
                        velocity = (position - foe).Normalized() * speed * 0.6f;
                    }
                    Face(entity, foe - position, deltaTime);
                    velocity += Spread(session, actor, ref brain, entity.Entity.Id, toTarget) * speed;
                    break;

                case EnemyState.WindingUp:
                    Face(entity, brain.AimWay, deltaTime, 16f);
                    if (--brain.WindUpTicks <= 0)
                    {
                        Let(session, actor, brain);
                        brain.Mark = default;
                        brain.Cooldown = enemy.Rarity == Rarity.Unique ? 1.1f : 1.5f;
                        Change(ref enemy, ref brain, EnemyState.Recovering);
                    }
                    break;

                case EnemyState.Recovering:
                    velocity = Spread(session, actor, ref brain, entity.Entity.Id, toTarget) * speed * 0.6f;
                    if (brain.StateTime >= RecoverTime) Change(ref enemy, ref brain, EnemyState.Chasing);
                    break;

                case EnemyState.Returning:
                    if ((position - brain.Home).Length < 3f)
                    {
                        Change(ref enemy, ref brain, EnemyState.Idle);
                        if (actor.Owner == null) actor.Vitals = Vitals.Full(actor.Sheet);
                    }
                    else
                    {
                        velocity = MoveTowards(ref brain, session, position, brain.Home) * speed;
                        Face(entity, velocity, deltaTime);
                    }
                    break;
            }

            // A blow takes the legs out of what it was doing for a moment, and the shove carries on regardless.
            if (actor.Reeling > 0f)
            {
                actor.Reeling -= deltaTime;
                velocity *= ReelingSpeed;
            }

            entity.Set(new VelocityComponent(Carried(actor, velocity, deltaTime)));
            entity.Set(brain);
            entity.Set(enemy);
        }

        // A blow carries someone along whatever they're doing, and however little they can do about it. The heaviest stun what they hit, and those above all have to be seen to land.
        private static Vector3 Carried(Actor actor, Vector2 walking, float deltaTime)
        {
            Vector2 velocity = walking + actor.Push;
            actor.Push *= MathF.Exp(-PushFade * deltaTime);
            if (actor.Push.LengthSquared < 0.01f) actor.Push = Vector2.Zero;
            return new Vector3(velocity.X, 0f, velocity.Y);
        }

        // Fixes what's going to happen: where from, which way and how far, and says so to all. Nothing is checked again until it lands, so that whatever a player can see is what's coming.
        private static void Commit(ServerSession session, Entity who, Actor actor, ref EnemyBrainComponent brain, Rarity rarity, BookProfile book, Vector2 position, Vector2 foe, float toTarget)
        {
            Vector2 toward = foe - position;
            brain.AimFrom = position;
            brain.AimWay = toward.LengthSquared > 1e-4f ? toward.Normalized() : Vector2.UnitY;
            brain.AimReach = toTarget + MeleeSlack;
            brain.WindUpTicks = (int)MathF.Round(WindUpOf(book, rarity) / MathF.Max(actor.Afflictions.ActionRate, 0.1f) * NetTime.TicksPerSecond);
            float windUp = brain.WindUpTicks * NetTime.TickSeconds;

            float follow = MathF.Min(MathF.Max(book.UseTime - book.ImpactTime, 0.35f), MostFollowThrough);
            session.Combat.Perform(who, book.Sign, windUp, windUp + follow, brain.AimWay);

            // Those on the heroes' side have no need of telling anyone, and there can be a lot of them.
            if (actor.Team != Team.Enemies) return;
            GroundShape shape = GroundShape.Of(book, position, brain.AimWay, brain.AimReach);
            brain.Mark = session.Combat.Warn(shape, TierOf(rarity, book), true, windUp + book.Delay, from: rarity);
        }

        // Long enough to see it, and for anything with an area, long enough for someone in the middle of it to get to the edge.
        private static float WindUpOf(BookProfile book, Rarity rarity)
        {
            float windUp = MathF.Max(book.ImpactTime, ShortestWindUp);
            if (book.Sign.Form is PageForm.Nova or PageForm.Burst or PageForm.Zone) windUp = MathF.Max(windUp, GroundShape.Of(book, Vector2.Zero, Vector2.UnitY, 0f).Length / EscapeSpeed * EscapeShare + 0.2f);
            if (Fearsome(book)) windUp = MathF.Max(windUp, HeavyWindUp);
            return rarity == Rarity.Unique ? windUp * BossWindUp : windUp;
        }

        // A hero's eye goes to what can really hurt them, and a crowd of cuts from fodder would drown that out.
        private static MarkTier TierOf(Rarity rarity, BookProfile book)
        {
            if (rarity >= Rarity.Rare || Fearsome(book)) return MarkTier.Grave;
            return rarity == Rarity.Normal && book.Sign.Form is PageForm.Strike or PageForm.Bolt ? MarkTier.Slight : MarkTier.Plain;
        }

        // What's slow to come, or goes all the way round.
        private static bool Fearsome(BookProfile book) => book.Sign.Heavy || book.Sign.Form == PageForm.Nova && !book.Sign.Spell;

        // What's near hears it too. A pack is one thing to a hero who walks up to it, however far its members happen to be from them.
        private void Call(ServerSession session, Actor actor)
        {
            int left = actor.Alarm > 0 ? actor.Alarm - 1 : NoticedAlarm;
            actor.Alarm = 0;
            if (left <= 0 || actor.Team != Team.Enemies) return;

            _near.Clear();
            session.Combat.Grid.Foes(Team.Heroes, actor.Position, CallReach, _near);
            foreach (Actor other in _near)
            {
                if (other != actor && other.Alarm < left) other.Alarm = left;
            }
        }

        // Which way to step so as not to stand in someone. Without this a pack closes on a foe until it's one body with many heads. It's worked out now
        // and then, as a push that's a few ticks old does just as well, and anyone far from a fight has no one to be in the way of.
        private Vector2 Spread(ServerSession session, Actor actor, ref EnemyBrainComponent brain, uint id, float toTarget)
        {
            if (toTarget > CrowdReach) return Vector2.Zero;
            if ((session.Server.Tick + id) % SpreadEvery != 0) return brain.Apart;

            _near.Clear();
            session.Combat.Grid.Foes(actor.Team == Team.Enemies ? Team.Heroes : Team.Enemies, actor.Position, PersonalSpace, _near);

            Vector2 push = Vector2.Zero;
            int counted = 0;
            foreach (Actor other in _near)
            {
                if (other == actor) continue;
                if (++counted > MostNeighbours) break;

                Vector2 away = actor.Position - other.Position;
                float distance = away.Length;

                // Two in exactly one place have nothing to tell them apart but who they are.
                if (distance < 0.05f) away = new Vector2(MathF.Cos(id * 2.4f), MathF.Sin(id * 2.4f));
                else away /= distance;
                push += away * (1f - MathF.Min(distance / PersonalSpace, 1f));
            }

            brain.Apart = push.LengthSquared > 1f ? push.Normalized() : push;
            return brain.Apart;
        }

        // The quieter a hero, the closer they can get.
        private static float Noticing(in EnemyBrainComponent brain, Actor foe) => brain.AggroRadius * (1f - foe.Sheet.Stealth / 100f);

        // How close it wants to be before it's happy.
        private static float Comfort(Actor actor)
        {
            float best = 0f, waiting = 0f;
            for (int i = 0; i < actor.Loadout.Books.Length; i++)
            {
                if (actor.Cooldowns[i] > 0f) waiting = MathF.Max(waiting, Reach(actor.Sheet.Profile(i)));
                else best = MathF.Max(best, Reach(actor.Sheet.Profile(i)));
            }

            // What can't be used yet is no reason to hang back, or a swordsman with a thrown knife stands off between throws and does nothing.
            return best > 0f ? best : waiting;
        }

        private static float Reach(BookProfile book) => book.Sign.Form switch
        {
            PageForm.Strike => book.First.Length,
            PageForm.Nova => book.First.Length * 0.75f,
            _ => book.First.Range,
        };

        // The first thing it has that's ready and will reach. Books are listed best first, so that's also the one it wants.
        private static int Ready(Actor actor, float distance)
        {
            for (int i = 0; i < actor.Loadout.Books.Length; i++)
            {
                if (actor.Cooldowns[i] > 0f) continue;
                BookProfile book = actor.Sheet.Profile(i);
                if ((book.First.Tags & Tags.Triggered) != 0 || book.Sign.Form is PageForm.Buff or PageForm.Summon or PageForm.Leap) continue;
                if (distance <= Reach(book)) return i;
            }
            return -1;
        }

        private static void Let(ServerSession session, Actor actor, in EnemyBrainComponent brain)
        {
            BookProfile book = actor.Sheet.Profile(brain.Book);
            actor.Cooldowns[brain.Book] = brain.Book < brain.Abilities ? MathF.Max(book.Cooldown, Rulebook.AbilityCooldown) : book.Cooldown;

            // It goes where it said it would. Whether anyone's still there is their business: that's the whole of how a blow gets dodged.
            session.Combat.Release(actor, book, brain.AimFrom, brain.AimWay, brain.AimReach);
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

        private Vector2 Wander(ref EnemyBrainComponent brain, in EnemyComponent enemy, Vector2 position, float speed)
        {
            if (brain.StateTime > 3f || brain.Wander == default)
            {
                brain.StateTime = 0f;
                brain.Wander = brain.Home + new Vector2(_random.Range(-6f, 6f), _random.Range(-6f, 6f));
            }
            Vector2 offset = brain.Wander - position;
            if (offset.Length < 1f || enemy.Rarity == Rarity.Unique) return Vector2.Zero;
            return offset.Normalized() * speed * 0.3f;
        }

        private Vector2 Heel(ref EnemyBrainComponent brain, ServerSession session, Vector2 position, float speed)
        {
            if ((brain.Home - position).Length < FollowDistance) return Vector2.Zero;
            return MoveTowards(ref brain, session, position, brain.Home) * speed;
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

                // Each at its own moment, or a pack blocked by the same wall all search in the same tick.
                brain.NextPathTime = session.Time + PathRefresh * _random.Range(0.75f, 1.25f);
            }

            if (brain.Path == null || brain.Path.Count == 0) return (target - position).Normalized();

            while (brain.PathStep < brain.Path.Count - 1 && (brain.Path[brain.PathStep] - position).Length < WaypointReach) brain.PathStep++;
            Vector2 offset = brain.Path[brain.PathStep] - position;
            return offset.LengthSquared > 0.01f ? offset.Normalized() : Vector2.Zero;
        }
    }

    /// <summary>Clears away the fallen once they have had their time on the ground. How they fall is drawn on the client.</summary>
    public class DyingSystem : SystemBase
    {
        public const float FallTime = 0.5f;
        public const float LieTime = 5f;
        public const float FadeTime = 1f;

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
    /// searching: a character that can't quickly find a way is better off pushing on than stalling the frame. Left as it is it's coarse, which
    /// suits a crowd that's only ever on its way somewhere: it only looks at points, and a fence can pass between two of them. A hero sent somewhere by a click has to
    /// arrive, so <paramref name="exact"/> goes cell by cell of <see cref="WorldData.Passable"/> and doesn't cut corners, and nothing can pass between those.
    /// </summary>
    public sealed class WorldPathfinder
    {
        private const float ExactStep = 1f;     // WorldData.PassCell.
        private const int ExactSearch = 20000;

        private readonly float _step;
        private readonly int _searchLimit;
        private readonly bool _exact;
        private readonly Dictionary<long, (long from, float cost)> _visited = new();
        private readonly PriorityQueue<long, float> _frontier = new();

        public WorldPathfinder(bool exact = false)
        {
            _exact = exact;
            _step = exact ? ExactStep : 3f;
            _searchLimit = exact ? ExactSearch : 700;
        }

        public static bool HasClearLine(WorldData world, Vector2 from, Vector2 to, float spacing = 1.5f)
        {
            float length = (to - from).Length;
            int steps = Math.Max((int)(length / spacing), 1);
            for (int i = 1; i <= steps; i++)
            {
                if (!world.Passable.IsSet(Vector2.Lerp(from, to, (float)i / steps))) return false;
            }
            return true;
        }

        private static long Key(int x, int z) => ((long)x << 32) | (uint)z;
        private Vector2 Position(long key) => new Vector2((int)(key >> 32), (int)(key & 0xFFFFFFFF)) * _step;

        public List<Vector2>? Find(WorldData world, Vector2 from, Vector2 to)
        {
            _visited.Clear();
            _frontier.Clear();

            int startX = (int)MathF.Round(from.X / _step);
            int startZ = (int)MathF.Round(from.Y / _step);
            long start = Key(startX, startZ);
            _visited[start] = (start, 0f);
            _frontier.Enqueue(start, 0f);

            long best = start;
            float bestDistance = (from - to).Length;
            int searched = 0;

            while (_frontier.TryDequeue(out long current, out _) && searched++ < _searchLimit)
            {
                Vector2 here = Position(current);
                float remaining = (here - to).Length;
                if (remaining < bestDistance)
                {
                    bestDistance = remaining;
                    best = current;
                }
                if (remaining < _step) break;

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
                        if (_exact && dx != 0 && dz != 0 && !(world.Passable.IsSet(Position(Key(x + dx, z))) && world.Passable.IsSet(Position(Key(x, z + dz))))) continue;

                        float total = cost + (dx != 0 && dz != 0 ? _step * 1.414f : _step);
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
