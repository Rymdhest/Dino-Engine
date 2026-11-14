using Dino_Defenders.Gameplay;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Defenders.Server
{
    /// <summary>A beast held at a place, in a cage or chained in the open, which isn't anyone's to fight: it's held until its place is freed, and then it goes (PLACES.md).</summary>
    public struct CaptiveComponent : IComponent
    {
        public int SiteIndex;
        public int Cage;                // Which of its place's cages (PlacePlan.Cages).
        public bool Leaving;
        public float Wait;              // Seconds before it does anything more: it looks about now and then while it's held, and each takes its own time going once it's free.
        public List<Vector2>? Route;
        public int Step;
        public float Gone;              // Seconds since it set off.
        public Vector2 Looking;         // Where it's looking while it's held.
    }

    /// <summary>A cage's door: shut while its place is held, swinging open on its hinge once it's freed.</summary>
    public struct CageDoorComponent : IComponent
    {
        public int SiteIndex;
        public float Shut;              // Its yaw shut.
        public float Swing;             // How far it turns about its hinge to open.
        public float Open;              // 0 shut to 1 open.
    }

    /// <summary>
    /// What a place with cages does once it's freed (PLACES.md): every cage's door swings open, and what was in it walks out of the door and the gate and on into the wild, where it's gone. While its
    /// place is held a beast stands in its cage and looks about, at the yard and at whoever comes near. Nothing it does is anyone's to fight, and nothing in the rules reads it.
    /// </summary>
    public sealed class CaptiveSystem : SystemBase
    {
        private const float OpenSeconds = 1.6f;
        private const float WalkSpeed = 3.4f;       // Out of its cage, as something that's been in one does.
        private const float TrotSpeed = 7.5f;       // And off, once it's clear of the place.
        private const float LongestGone = 50f;      // Seconds after which it's out in the wild whether it got there or not.
        private const float Reached = 1.6f;

        private readonly GameServer _server;
        private readonly WorldPathfinder _paths = new WorldPathfinder(exact: true);
        private readonly WorldRandom _random = new WorldRandom(Environment.TickCount);
        private static readonly BitMask DoorMask = new BitMask(typeof(CageDoorComponent), typeof(RotationComponent));

        public CaptiveSystem(GameServer server)
            : base(new BitMask(typeof(CaptiveComponent), typeof(PositionComponent), typeof(VelocityComponent)))
        {
            _server = server;
        }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            ServerSession? session = _server.Session;
            if (session == null || session.Paused) return;
            foreach (var archetype in world.QueryArchetypes(DoorMask, BitMask.Empty))
            {
                foreach (var entity in new ComponentAccessor(archetype))
                {
                    var door = entity.Get<CageDoorComponent>();
                    if (door.Open >= 1f || door.SiteIndex < 0 || door.SiteIndex >= session.Sites.Count || !session.Sites[door.SiteIndex].Cleared) continue;
                    door.Open = MathF.Min(1f, door.Open + deltaTime / OpenSeconds);
                    entity.Set(door);
                    float eased = door.Open * door.Open * (3f - 2f * door.Open);
                    entity.Set(new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitY, door.Shut + door.Swing * eased)));
                }
            }
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            ServerSession? session = _server.Session;
            if (session == null) return;
            var captive = entity.Get<CaptiveComponent>();
            if (session.Paused || captive.SiteIndex < 0 || captive.SiteIndex >= session.Sites.Count)
            {
                entity.Set(new VelocityComponent());
                return;
            }
            SiteState state = session.Sites[captive.SiteIndex];
            PlacePlan? plan = state.Site.Place;
            if (plan == null || captive.Cage < 0 || captive.Cage >= plan.Cages.Count) return;
            PlaceCage cage = plan.Cages[captive.Cage];
            Vector2 position = entity.Get<PositionComponent>().value.Xz;

            if (!captive.Leaving)
            {
                entity.Set(new VelocityComponent());
                captive.Wait -= deltaTime;
                if (state.Cleared)
                {
                    // Freed: each goes in its own time, once its door's open.
                    captive.Leaving = true;
                    captive.Wait = OpenSeconds * 0.8f + _random.Range(0.2f, 2.6f);
                    captive.Route = RouteFor(session.World, position, cage);
                    captive.Step = 0;
                }
                else
                {
                    // Held: it looks about, out of its cage towards whoever's near, or at the yard.
                    if (captive.Wait <= 0f)
                    {
                        captive.Wait = _random.Range(2.5f, 7f);
                        ServerPlayer? hero = session.NearestHero(position, out float near);
                        Vector2 front = new Vector2(MathF.Sin(cage.Facing), MathF.Cos(cage.Facing));
                        captive.Looking = hero != null && near < 22f ? hero.Sim.Position - position : front + new Vector2(_random.Range(-0.8f, 0.8f), _random.Range(-0.8f, 0.8f));
                    }
                    if (captive.Looking.LengthSquared > 1e-4f) EnemyBrainSystem.Face(entity, captive.Looking, deltaTime, 2.2f);
                }
                entity.Set(captive);
                return;
            }

            if (captive.Wait > 0f)
            {
                captive.Wait -= deltaTime;
                entity.Set(new VelocityComponent());
                entity.Set(captive);
                return;
            }
            captive.Gone += deltaTime;
            List<Vector2> route = captive.Route ?? new List<Vector2> { cage.Out, cage.Away };
            while (captive.Step < route.Count && (route[captive.Step] - position).LengthSquared < Reached * Reached) captive.Step++;
            if (captive.Step >= route.Count || captive.Gone > LongestGone)
            {
                // Out in the wild: it's gone, and nothing more is kept of it.
                state.Live.Remove(entity.Entity);
                world.DestroyEntity(entity.Entity);
                return;
            }
            Vector2 toward = route[captive.Step] - position;
            float speed = (position - cage.Captive).Length > 12f ? TrotSpeed : WalkSpeed;
            Vector2 velocity = toward / MathF.Max(toward.Length, 1e-4f) * speed;
            entity.Set(new VelocityComponent(new Vector3(velocity.X, 0f, velocity.Y)));
            EnemyBrainSystem.Face(entity, velocity, deltaTime, 5f);
            entity.Set(captive);
        }

        // Out of the door, then the way out of the place to the wild round whatever stands in the way, pulled straight wherever it can see further along.
        private List<Vector2> RouteFor(WorldData world, Vector2 from, PlaceCage cage)
        {
            var route = new List<Vector2> { cage.Out };
            List<Vector2>? path = _paths.Find(world, cage.Out, cage.Away);
            if (path == null || path.Count == 0)
            {
                route.Add(cage.Away);
                return route;
            }
            Vector2 at = cage.Out;
            int i = 0;
            while (i < path.Count)
            {
                int furthest = i;
                for (int j = Math.Min(path.Count - 1, i + 30); j > i; j--)
                {
                    if (WorldPathfinder.HasClearLine(world, at, path[j], spacing: 0.8f))
                    {
                        furthest = j;
                        break;
                    }
                }
                at = path[furthest];
                route.Add(at);
                i = furthest + 1;
            }
            return route;
        }
    }
}
