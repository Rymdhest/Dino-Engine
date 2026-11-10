using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Particles
{
    /// <summary>The land under the particles, for those that land on it or lie on it. Particles only ever ask about where they were born.</summary>
    public interface IParticleGround
    {
        float HeightAt(float x, float z);
    }

    /// <summary>One particle to be born: where, how it sets off, how big, and for how long.</summary>
    public struct ParticleSpawn
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 Axis;        // Where a Segment ends, from Position.
        public float Size;          // Metres across.
        public float Life;          // Seconds.
        public float Age;           // Seconds it has already lived, for what has to be there at once (a smoke column coming into view).
        public float Angle;
        public float Spin;
        public Vector3 Tint;
        public float Strength;

        public ParticleSpawn(Vector3 position, float size, float life)
        {
            Position = position;
            Velocity = Vector3.Zero;
            Axis = Vector3.Zero;
            Size = size;
            Life = life;
            Age = 0f;
            Angle = 0f;
            Spin = 0f;
            Tint = Vector3.One;
            Strength = 1f;
        }
    }

    /// <summary>
    /// The engine's particles, as the game sees them (read PARTICLES.md). Particles live on the GPU and are only ever born here: <see cref="Spawn"/> one, <see cref="Play"/> an effect
    /// once, or put a <see cref="ECS.Components.ParticleEffectComponent"/> on an entity for one that goes on. Everything is the client's alone and only for show: nothing in the rules may
    /// read a particle, and none can be read back.
    /// </summary>
    public static class Particles
    {
        public const int Capacity = 1 << 16;
        public const int SpawnCapacity = 1 << 14;

        public static readonly ParticlePool Pool = new ParticlePool(Capacity, SpawnCapacity);

        /// <summary>The particles stand still, as the world does while the game is paused.</summary>
        public static bool Paused;

        /// <summary>Share of what effects would emit that they do: the graphics quality's say.</summary>
        public static float Density = 1f;

        /// <summary>The land, while there is one. ParticleEffectSystem keeps it up to date.</summary>
        public static IParticleGround? Ground;

        public static readonly WorldRandom Random = new WorldRandom(Environment.TickCount);

        private static readonly List<EffectRun> _playing = new();

        /// <summary>Effects playing on their own, as <see cref="Play"/> started them. ParticleEffectSystem runs these.</summary>
        public static List<EffectRun> Playing => _playing;

        public static bool Spawn(ParticleStyle style, in ParticleSpawn spawn, ParticlePriority priority = ParticlePriority.Normal)
        {
            // Smoke only comes out where there's room for it.
            float strength = spawn.Strength;
            if (style.Sprite == ParticleSprite.Smoke)
            {
                strength *= Haze.Admit(style, spawn.Position, spawn.Size, spawn.Strength, Pool.Clock);
                if (!(strength > 0f)) return false;
            }

            var particle = new GpuParticle
            {
                Position = spawn.Position,
                Age = spawn.Age,
                Velocity = spawn.Velocity,
                Life = spawn.Life,
                Axis = spawn.Axis,
                Size = spawn.Size,
                Angle = spawn.Angle,
                Spin = spawn.Spin,
                Seed = Random.Float(),
                Style = (uint)ParticleStyles.IndexOf(style),
                Ground = NeedsGround(style) ? GroundPlane(spawn.Position.X, spawn.Position.Z) : Vector4.Zero,
                Tint = new Vector4(spawn.Tint, strength),
            };
            return Pool.TrySpawn(particle, priority);
        }

        /// <summary>Starts an effect that happens once, here. It runs itself until it's over.</summary>
        public static EffectRun Play(ParticleEffect effect, Vector3 position, float scale = 1f, EffectShape? shape = null, Vector3? forward = null, Vector3? tint = null, float strength = 1f)
        {
            var run = new EffectRun(effect)
            {
                Position = position,
                Scale = scale,
                Shape = shape ?? EffectShape.Point,
                Forward = forward ?? Vector3.UnitZ,
                Tint = tint ?? Vector3.One,
                Strength = strength,
            };
            _playing.Add(run);
            return run;
        }

        /// <summary>Starts an effect that goes on until <see cref="Stop"/>: one on something the caller moves about itself, as <see cref="EffectRun.Position"/>.</summary>
        public static EffectRun Begin(ParticleEffect effect, Vector3 position, EffectShape shape, float scale = 1f, bool prewarm = false)
        {
            var run = new EffectRun(effect) { Position = position, Shape = shape, Scale = scale, Prewarm = prewarm };
            _playing.Add(run);
            return run;
        }

        /// <summary>Stops an effect playing on its own. What it has made goes on to the end of its life.</summary>
        public static void Stop(EffectRun run) => _playing.Remove(run);

        /// <summary>Forgets every particle and every effect playing, as when a world is left behind.</summary>
        public static void Clear()
        {
            _playing.Clear();
            Pool.Clear();
            Haze.Clear();
        }

        /// <summary>The height of the land, or of the sea over it.</summary>
        public static float GroundAt(float x, float z)
        {
            float land = Ground?.HeightAt(x, z) ?? 0f;
            return MathF.Max(land, WorldPlan.SeaLevel);
        }

        private static bool NeedsGround(ParticleStyle style) => style.Contact != ParticleContact.None || style.Facing == ParticleFacing.Ground;

        // The ground about here as a plane, as particles only go a few metres from where they're born.
        private static Vector4 GroundPlane(float x, float z)
        {
            if (Ground == null) return Vector4.Zero;
            const float step = 0.5f;
            float height = GroundAt(x, z);
            float slopeX = (GroundAt(x + step, z) - GroundAt(x - step, z)) / (2f * step);
            float slopeZ = (GroundAt(x, z + step) - GroundAt(x, z - step)) / (2f * step);
            Vector3 normal = new Vector3(-slopeX, 1f, -slopeZ).Normalized();
            return new Vector4(normal, Vector3.Dot(normal, new Vector3(x, height, z)));
        }
    }

    /// <summary>Lightning between two points: a jagged line as midpoint displacement makes one, with forks off it, each piece a Segment particle. Its finest jags are drawn by the shader.</summary>
    public static class Arcs
    {
        private static readonly List<Vector3> _points = new();
        private static readonly List<Vector3> _next = new();

        public static void Strike(ParticleStyle style, Vector3 from, Vector3 to, float width, float life, Vector3 tint, float strength = 1f, float jag = 0.2f, int forks = 2, ParticlePriority priority = ParticlePriority.Normal)
        {
            WorldRandom random = Particles.Random;
            float length = (to - from).Length;
            if (length < 1e-3f) return;

            _points.Clear();
            _points.Add(from);
            _points.Add(to);
            int levels = Math.Clamp((int)MathF.Ceiling(MathF.Log2(length / 0.35f)), 1, 6);
            for (int level = 0; level < levels; level++)
            {
                _next.Clear();
                for (int i = 0; i < _points.Count - 1; i++)
                {
                    Vector3 a = _points[i];
                    Vector3 b = _points[i + 1];
                    Vector3 along = b - a;
                    float span = along.Length;
                    Vector3 aside = Across(along / MathF.Max(span, 1e-5f), random);
                    _next.Add(a);
                    _next.Add((a + b) * 0.5f + aside * (span * jag * random.Range(-1f, 1f)));
                }
                _next.Add(_points[^1]);
                _points.Clear();
                _points.AddRange(_next);
            }

            Vector3[] path = _points.ToArray();
            for (int i = 0; i < path.Length - 1; i++)
            {
                var spawn = new ParticleSpawn(path[i], width, life) { Axis = path[i + 1] - path[i], Tint = tint, Strength = strength };
                Particles.Spawn(style, spawn, priority);
            }

            for (int f = 0; f < forks && path.Length > 3; f++)
            {
                int at = random.Int(1, path.Length - 2);
                Vector3 way = (path[at + 1] - path[at - 1]).Normalized();
                Vector3 bent = (way + Across(way, random) * random.Range(0.5f, 1.1f)).Normalized();
                Strike(style, path[at], path[at] + bent * (length * random.Range(0.15f, 0.35f)), width * 0.6f, life * 0.8f, tint, strength * 0.7f, jag, forks - 1 - f, priority);
            }
        }

        private static Vector3 Across(Vector3 along, WorldRandom random)
        {
            Vector3 any = new Vector3(random.Range(-1f, 1f), random.Range(-1f, 1f), random.Range(-1f, 1f));
            Vector3 aside = any - along * Vector3.Dot(any, along);
            return aside.LengthSquared > 1e-6f ? aside.Normalized() : Vector3.Cross(along, Vector3.UnitY).Normalized();
        }
    }
}
