using Dino_Engine.Animation;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Particles;
using Dino_Engine.Particles.Content;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Simulation;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Presses whatever walks through snow into it (SnowTrailRenderer): each foot its own shape where it really stands, a boot, three toes, a pad or a paw, by what it's
    /// built as, and in deep snow the body's furrow as it ploughs on, banked either side. A foot coming down throws up powder. Falling snow fills it all in again. It's
    /// all this machine's, and only looked at, like the grass a body parts: nothing in the rules reads it. Read SNOW.md.
    /// </summary>
    public class SnowTrailSystem : SystemBase
    {
        private const float RegionAhead = 16f;      // Metres along where the camera looks that the trail is centred: about where the hero stands.
        private const float PersonFoot = 0.54f;     // Metres a person's foot is, heel to toe (Rig.Humanoid), which everything else is measured against.
        private const float PrintDepth = 0.34f;     // Metres a person's foot goes into the Snowfield's snow. Bigger feet go deeper, though not by as much as they're bigger.
        private const float DeepestPrint = 0.58f;
        private const float FurrowDepth = 0.2f;     // Metres a person's body ploughs down through deep snow, between the banks it pushes up.
        private const float FurrowWidth = 0.19f;    // Of a body's plant contact radius, either side of its middle.
        private const float RunSpeed = 6.5f;        // Metres a second, for a person, at which feet are running rather than walking.
        private const float PressIn = 0.12f;        // Seconds a foot takes to press all the way in.
        private const float FillTime = 80f;         // Seconds the heaviest snowfall takes to fill a print to the top.
        private const float SettleTime = 40f;       // Seconds kicked-up clumps take to settle into the snow, snowing or not.
        private const float DeepSnow = 0.9f;        // How much snow there has to be underfoot for a body to plough through it, which only the Snowfield's own is.
        private const float LeastSnow = 0.05f;      // Less than this, and nothing's pressed at all.
        private const float KickSnow = 0.25f;       // And this little throws nothing up.
        private const float GiantFoot = 1.4f;       // Metres of foot past which one coming down bursts the snow out all round.

        private readonly Dictionary<uint, Vector2> _lastAt = new();     // Where each body was last frame, for its furrow.
        private readonly Dictionary<uint, float> _lastWay = new();      // And the way it was going, for when it stops.
        private readonly HashSet<uint> _seen = new();
        private readonly List<uint> _gone = new();
        private WorldData? _world;
        private float _cover;                       // How much snow the weather has settled at the camera's focus.

        public SnowTrailSystem()
            : base(new BitMask(typeof(PositionComponent), typeof(PlantContactComponent)))
        {
        }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            SnowTrailRenderer trail = Engine.RenderEngine._snowTrailRenderer;
            WorldData? land = world.TryGetSingleton<WorldStreamSingleton>(out Entity stream) ? world.GetComponent<WorldStreamSingleton>(stream).World : null;
            if (land != _world)
            {
                _world = land;
                trail.Clear();
                _lastAt.Clear();
                _lastWay.Clear();
            }
            if (_world == null || !world.Camera.IsValid()) return;

            trail.Follow(RegionCenter(world));
            WeatherSample here = Weather.Here(world);
            _cover = here.SnowCover;
            float snowing = here.Fall * here.Snow / FillTime;
            trail.Fill(new Vector4(snowing, snowing, snowing, 1f / SettleTime + 2f * snowing), deltaTime);

            _seen.Clear();
            base.UpdateFrame(world, deltaTime);

            // Forget bodies that have gone, as their ids are given out again.
            _gone.Clear();
            foreach (uint id in _lastAt.Keys) if (!_seen.Contains(id)) _gone.Add(id);
            foreach (uint id in _gone)
            {
                _lastAt.Remove(id);
                _lastWay.Remove(id);
            }
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            WorldData land = _world!;
            SnowTrailRenderer trail = Engine.RenderEngine._snowTrailRenderer;
            uint id = entity.Entity.Id;
            _seen.Add(id);

            Vector2 position = entity.Get<PositionComponent>().value.Xz;
            var contact = entity.Get<PlantContactComponent>();
            Vector2 velocity = entity.Has<VelocityComponent>() ? entity.Get<VelocityComponent>().value.Xz : Vector2.Zero;
            Vector2 from = _lastAt.TryGetValue(id, out Vector2 last) ? last : position;
            _lastAt[id] = position;

            CharacterAnimator? animator = entity.Has<AnimatorComponent>() ? entity.Get<AnimatorComponent>().Animator : null;
            float scale = animator?.Orders.Scale ?? 1f;
            float runSpeed = RunSpeed * MathF.Max(scale, 0.3f);
            float speed = velocity.Length;
            Vector2 push = speed > 0.3f ? velocity / runSpeed : Vector2.Zero;
            if (push.LengthSquared > 1f) push.Normalize();

            // The body ploughs through deep snow, one frame's way at a time, so the furrow has no gaps however fast it goes.
            if (!contact.feetOnly)
            {
                float snow = SnowAt(land, position);
                if (snow >= DeepSnow)
                {
                    Vector2 way = position - from;
                    float moved = way.Length;
                    if (moved > 3f) moved = 0f;     // Moved by something other than its legs.
                    float yaw = moved > 1e-3f ? MathF.Atan2(way.X, way.Y) : _lastWay.GetValueOrDefault(id, animator?.Orders.Yaw ?? 0f);
                    _lastWay[id] = yaw;
                    float furrowWidth = contact.radius * FurrowWidth;
                    float size = MathF.Pow(MathF.Max(scale, 0.2f), 0.6f);
                    trail.Stamp(new SnowStamp
                    {
                        Center = moved > 1e-3f ? (position + from) * 0.5f : position,
                        Yaw = yaw,
                        HalfWidth = furrowWidth,
                        HalfLength = moved * 0.5f + furrowWidth,
                        Kind = SnowPrint.Trench,
                        Depth = FurrowDepth * size,
                        Press = 0.6f,
                        Rim = 0.16f * size,
                        Churn = 0.4f,
                        Push = push,
                    });
                }
            }

            if (animator == null) return;
            Rig rig = animator.Rig;
            SnowPrint kind = PrintOf(rig);
            float foot = (rig.FootForward + rig.FootBack + (rig.Frame?.ToeLength ?? 0f)) * scale;
            float grown = foot / PersonFoot;
            float depth = MathF.Min(PrintDepth * MathF.Pow(MathF.Max(grown, 0.1f), 0.6f), DeepestPrint);
            (float halfLength, float halfWidth) = kind switch
            {
                SnowPrint.ThreeToed => (foot * 0.78f, foot * 0.66f),
                SnowPrint.Pad => (foot * 0.5f, foot * 0.46f),
                SnowPrint.Paw => (foot * 0.5f, foot * 0.42f),
                _ => (foot * 0.62f, foot * 0.27f),
            };

            Gait gait = animator.Gait;
            int landed = animator.Footfalls;
            for (int i = 0; i < gait.Feet.Length; i++)
            {
                ref Foot f = ref gait.Feet[i];
                if (f.Swinging) continue;
                Vector2 at = f.At.Xz;
                float snow = SnowAt(land, at);
                if (snow < LeastSnow) continue;

                // Pressed again every frame it stands, which changes nothing once it's all the way in, and follows it if it turns where it stands.
                Vector2 forward = new Vector2(MathF.Sin(f.Yaw), MathF.Cos(f.Yaw));
                trail.Stamp(new SnowStamp
                {
                    Center = at + forward * (kind == SnowPrint.Boot ? 0.08f * foot : 0.12f * foot),
                    Yaw = f.Yaw,
                    HalfWidth = halfWidth,
                    HalfLength = halfLength,
                    Kind = kind,
                    Depth = depth * MathF.Min(f.Planted / PressIn, 1f),
                    Press = 1f,
                    Rim = depth * 0.5f,
                    Churn = 0.35f + 0.5f * push.Length,
                    Push = push,
                });

                if ((landed & (1 << i)) != 0 && snow >= KickSnow) Kick(f.At, foot, speed, runSpeed, velocity, forward, snow);
            }
        }

        // A foot coming down throws snow up, the way it was going.
        private static void Kick(Vector3 where, float foot, float speed, float runSpeed, Vector2 velocity, Vector2 forward, float snow)
        {
            Vector2 way = speed > 0.3f ? velocity / speed : forward;
            Vector3 ahead = new Vector3(way.X, 0f, way.Y);
            float size = Math.Clamp(MathF.Pow(foot / PersonFoot, 0.7f), 0.6f, 3.5f);
            ParticleEffect effect = foot > GiantFoot ? Powder.Stomp : speed > runSpeed * 0.85f ? Powder.Spray : Powder.Kick;
            Particles.Particles.Play(effect, where, size, forward: ahead, strength: Math.Clamp(snow, 0f, 1f));
        }

        private static SnowPrint PrintOf(Rig rig)
        {
            if (rig.Saurian) return rig.Plan == BodyPlan.Biped ? SnowPrint.ThreeToed : SnowPrint.Pad;
            return rig.Plan == BodyPlan.Quadruped ? SnowPrint.Paw : SnowPrint.Boot;
        }

        // How much snow lies at a point, 0..1: all of it on the Snowfield's own ground, some on its paths and edges, and otherwise what the weather has settled.
        private float SnowAt(WorldData land, Vector2 at)
        {
            SurfaceMaterial surface = land.SurfaceAt(at);
            if (surface == SurfaceMaterial.Snow) return 1f;
            float snow = _cover;
            if (land.BiomeAt(at).Ground == SurfaceMaterial.Snow) snow = MathF.Max(snow, 0.5f);
            return snow;
        }

        // Ahead of the camera along the ground, where it looks.
        private static Vector2 RegionCenter(ECSWorld world)
        {
            Vector3 camera = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();
            Matrix4 invView = Matrix4.Invert(world.GetComponent<ViewMatrixComponent>(world.Camera).value);
            Vector3 forward = (new Vector4(0f, 0f, -1f, 0f) * invView).Xyz;
            Vector2 along = forward.Xz;
            return along.LengthSquared > 1e-4f ? camera.Xz + along.Normalized() * RegionAhead : camera.Xz;
        }
    }
}
