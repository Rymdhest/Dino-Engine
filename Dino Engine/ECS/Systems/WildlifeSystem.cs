using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Physics;
using Dino_Engine.Rendering;
using Dino_Engine.Rendering.Renderers.Simulation;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Util;
using Dino_Engine.Wildlife;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Everything the rest of a client has to say to what's alive in a world. It's only ever a request, as nothing about wildlife can matter to anyone: <see cref="WildlifeSystem"/> reads these next time it runs.
    /// </summary>
    public static class Nature
    {
        private const int MostWaiting = 32;

        private static readonly Queue<(Vector2 at, float radius)> Startles = new();
        private static readonly Queue<(string id, Vector2 at)> Brought = new();

        /// <summary>Off, for measuring what the rest of the game costs.</summary>
        public static bool Off;

        /// <summary>The world's stopped, so what's in it stops too.</summary>
        public static bool Paused;

        /// <summary>Where the guiding wind is going this frame, if it's going anywhere.</summary>
        public static Vector2? Guiding;

        /// <summary>Frightens whatever's within <paramref name="radius"/>: a blow, a shout, anything that can be heard.</summary>
        public static void Startle(Vector2 at, float radius)
        {
            // Whatever isn't running this has nobody to read them, and a fight goes on for a long time.
            if (!Off && Startles.Count < MostWaiting) Startles.Enqueue((at, radius));
        }

        /// <summary>Writes down what's alive and what it's doing once a second, for a scripted run to be read by.</summary>
        public static string? LogFile;

        /// <summary>Puts some of a creature somewhere, for looking at it.</summary>
        public static void Bring(string id, Vector2 at) => Brought.Enqueue((id, at));

        internal static bool TakeStartle(out (Vector2 at, float radius) startle) => Startles.TryDequeue(out startle);

        internal static bool TakeBrought(out (string id, Vector2 at) brought) => Brought.TryDequeue(out brought);
    }

    /// <summary>
    /// What's alive, for this client alone. It reads what a world is doing, runs <see cref="WildlifeSim"/> and hands what it draws to the renderer, and nothing else touches either. Whatever walks through plants frightens
    /// animals, so that's all it asks of the game: heroes and enemies alike, with no list of who's who.
    /// </summary>
    public sealed class WildlifeSystem : SystemBase
    {
        private const float RunningSpeed = 6f;      // Metres a second that's as frightening as moving gets.
        private const float MostReach = 150f;       // Metres from the focus that a view's taken to end, however level it is.
        private const float GuideRate = 80f;
        private const float BlastFright = 1.5f;     // How much further than its wind a blast is heard.
        private const float WingsGap = 0.9f;        // Seconds between sounds of one kind.
        private const float CallGap = 2.4f;
        private const float PlopGap = 0.2f;

        private static readonly BitMask Bodies = new BitMask(typeof(PlantContactComponent), typeof(PositionComponent));

        private readonly WildlifeView _view = new WildlifeView();
        private WildlifeSim? _sim;
        private WorldData? _world;
        private double _nextWings, _nextCall, _nextPlop, _nextLog;
        private double _clock;

        public WildlifeSystem() : base(new BitMask(typeof(WorldStreamSingleton)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            Engine.RenderEngine._wildlifeRenderer.Begin();
            if (Nature.Off || !world.TryGetSingleton<WorldStreamSingleton>(out Entity stream)) return;

            WorldData land = world.GetComponent<WorldStreamSingleton>(stream).World;
            if (land != _world)
            {
                _world = land;
                _sim = new WildlifeSim(land);
            }

            if (_sim == null || !ReadView(world)) return;
            _clock += deltaTime;

            while (Nature.TakeStartle(out var startle)) _sim.Startle(startle.at, startle.radius);
            while (Nature.TakeBrought(out var brought))
            {
                if (Fauna.Named(brought.id) is Creature creature) _sim.Bring(creature, brought.at);
            }
            if (Nature.Guiding is Vector2 way)
            {
                _sim.Leaves.Guide(way, GuideRate);
                Nature.Guiding = null;
            }

            if (!Nature.Paused) _sim.Step(_view, deltaTime);
            Answer(world);

            _sim.Draw(Engine.RenderEngine._wildlifeRenderer);
            Engine.RenderEngine._wildlifeRenderer.Finish();
            if (Nature.LogFile != null) WriteLog(Nature.LogFile);
        }

        private void WriteLog(string file)
        {
            if (_clock < _nextLog) return;
            _nextLog = _clock + 1.0;

            Matrix4 viewProjection = Engine.RenderEngine.context.viewMatrix * Engine.RenderEngine.context.projectionMatrix;
            using var log = new StreamWriter(file, append: true);
            log.WriteLine($"t {_clock:0.0} focus {_view.Focus.X:0.0},{_view.Focus.Y:0.0},{_view.Focus.Z:0.0} view {_view.Least.X:0},{_view.Least.Y:0} to {_view.Most.X:0},{_view.Most.Y:0} creatures {_sim!.Creatures} leaves {_sim.Leaves.Count} falling {_sim.Leaves.Airborne} trees {_sim.Leaves.Trees} scares {_view.Scares.Count} moving {(_view.Scares.Count > 0 ? _view.Scares.Max(scare => scare.Moving) : 0f):0.00}");
            foreach (Critter critter in _sim.Living)
            {
                Vector4 clip = new Vector4(critter.Position, 1f) * viewProjection;
                string screen = clip.W > 0f ? $"{clip.X / clip.W * 0.5f + 0.5f:0.000},{0.5f - clip.Y / clip.W * 0.5f:0.000}" : "behind";
                float high = critter.Position.Y - _sim.Land.Ground(critter.Position.Xz);
                log.WriteLine($"  {critter.Shape.Id} {critter.Doing} at {critter.Position.X:0.0},{critter.Position.Z:0.0} high {high:0.0} screen {screen}");
            }
            foreach (Vector3 leaf in _sim.Leaves.InTheAir)
            {
                Vector4 clip = new Vector4(leaf, 1f) * viewProjection;
                if (clip.W > 0f) log.WriteLine($"  leaf Falling at {leaf.X:0.0},{leaf.Z:0.0} high {leaf.Y - _sim.Land.Ground(leaf.Xz):0.0} screen {clip.X / clip.W * 0.5f + 0.5f:0.000},{0.5f - clip.Y / clip.W * 0.5f:0.000}");
            }
        }

        // What's being looked at. The camera's corners are taken down to the ground, as a wide view sees far more of it to the sides than up and down the screen.
        private bool ReadView(ECSWorld world)
        {
            if (!world.Camera.IsValid()) return false;

            Matrix4 cameraToWorld = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value;
            Vector3 camera = cameraToWorld.ExtractTranslation();
            Matrix4 viewProjection = world.GetComponent<ViewMatrixComponent>(world.Camera).value * world.GetComponent<PerspectiveProjectionComponent>(world.Camera).ProjectionMatrix;
            Matrix4 toWorld = Matrix4.Invert(viewProjection);

            Vector4 focusPoint = Engine.RenderEngine.context.focusPoint;
            Vector3 focus = focusPoint.W > 0f ? focusPoint.Xyz : GroundAhead(camera, Ray(toWorld, camera, 0f, 0f));

            Vector2 least = new Vector2(float.PositiveInfinity), most = new Vector2(float.NegativeInfinity);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 ray = Ray(toWorld, camera, corner % 2 == 0 ? -1f : 1f, corner < 2 ? -1f : 1f);
                float down = -ray.Y;
                float reach = down > 0.02f ? (camera.Y - focus.Y) / down : float.PositiveInfinity;
                Vector2 at = camera.Xz + ray.Xz * MathF.Min(reach, MostReach * 2f);
                at = focus.Xz + ClampLength(at - focus.Xz, MostReach);
                least = Vector2.ComponentMin(least, at);
                most = Vector2.ComponentMax(most, at);
            }

            _view.Camera = camera;
            _view.Focus = focus;
            _view.Least = least;
            _view.Most = most;
            _view.Sight = new Frustum(viewProjection);
            if (Wind.TryGet(world, out WindSingleton wind)) _view.Wind = wind;
            _view.Weather = Weather.Here(world);
            _view.Night = SkySYstem.NightOf(world).Amount;

            QualityPreset quality = GraphicsPresets.Of(Engine.RenderEngine.Quality);
            _view.MostCreatures = quality.Creatures;
            _view.MostLeaves = quality.Leaves;

            ReadScares(world, wind);
            return true;
        }

        private static Vector2 ClampLength(Vector2 offset, float most)
        {
            float length = offset.Length;
            return length > most ? offset * (most / length) : offset;
        }

        private static Vector3 Ray(in Matrix4 toWorld, Vector3 camera, float x, float y)
        {
            Vector4 far = new Vector4(x, y, 1f, 1f) * toWorld;
            return (far.Xyz / far.W - camera).Normalized();
        }

        private Vector3 GroundAhead(Vector3 camera, Vector3 ray)
        {
            float distance = 40f;
            Vector3 at = camera + ray * distance;
            float ground = _world!.Height.Sample(at.Xz);
            if (ray.Y < -0.02f) at = camera + ray * MathF.Min((camera.Y - ground) / -ray.Y, MostReach);
            return new Vector3(at.X, ground, at.Z);
        }

        private void ReadScares(ECSWorld world, in WindSingleton wind)
        {
            _view.Scares.Clear();
            foreach (var archetype in world.QueryArchetypes(Bodies, BitMask.Empty))
            {
                var positions = archetype.GetComponentArray<PositionComponent>();
                var velocities = archetype.GetComponentArray<VelocityComponent>();
                int count = archetype.EntityCount;
                for (int i = 0; i < count; i++)
                {
                    float speed = velocities != null ? velocities[i].value.Xz.Length : 0f;
                    _view.Scares.Add(new Scare { At = positions[i].value.Xz, Moving = MathF.Min(speed / RunningSpeed, 1f) });
                }
            }

            // A blast is heard a long way off, and nothing stays to see what it was.
            if (wind.Blasts == null) return;
            foreach (WindEmitter blast in wind.Blasts) _view.Scares.Add(new Scare { At = blast.Position.Xz, Moving = 1f, Radius = blast.Radius * BlastFright });
        }

        // Whatever it's asked for that isn't its own to do.
        private void Answer(ECSWorld world)
        {
            WildlifeSim sim = _sim!;
            foreach (LifeSplash splash in sim.Splashes) Water.AddSplash(world, splash.At, splash.Radius, splash.Strength);
            sim.Splashes.Clear();

            WindRenderer wind = Engine.RenderEngine._windRenderer;
            foreach (LifeContact contact in sim.Contacts)
            {
                wind.AddVegetationContact(new VegetationContact
                {
                    center = contact.At,
                    previous = contact.Was,
                    radius = contact.Radius,
                    lean = contact.Lean,
                    press = contact.Press,
                    drag = contact.Drag,
                });
            }
            sim.Contacts.Clear();

            foreach (LifeNoise noise in sim.Noises) Sound(noise);
            sim.Noises.Clear();
        }

        // Each kind is kept from piling up on itself, as a hero going through a village shouldn't sound like a storm of pigeons.
        private void Sound(in LifeNoise noise)
        {
            switch (noise.Kind)
            {
                case LifeNoiseKind.Wings:
                    if (_clock < _nextWings) return;
                    _nextWings = _clock + WingsGap;
                    Audio.Play(noise.Size > 6f ? Life.WingsFlock : Life.WingsFew, noise.At);
                    break;
                case LifeNoiseKind.Caw:
                    if (_clock < _nextCall) return;
                    _nextCall = _clock + CallGap;
                    Audio.Play(Life.Caw, noise.At);
                    break;
                case LifeNoiseKind.Cry:
                    if (_clock < _nextCall) return;
                    _nextCall = _clock + CallGap;
                    Audio.Play(Life.GullCry, noise.At);
                    break;
                case LifeNoiseKind.Plop:
                case LifeNoiseKind.Splash:
                    if (_clock < _nextPlop) return;
                    _nextPlop = _clock + PlopGap;
                    Audio.Play(noise.Kind == LifeNoiseKind.Plop ? Life.Plop : Splashes.Wade, noise.At, noise.Size < 1f ? -6f : -2f);
                    break;
            }
        }
    }
}
