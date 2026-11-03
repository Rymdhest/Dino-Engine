using OpenTK.Mathematics;
using Util.Noise;

namespace Dino_Engine.World
{
    /// <summary>The fractal noises terrain and climate are built from. Safe to call from many threads at once.</summary>
    public sealed class WorldNoise
    {
        private readonly OpenSimplexNoise _noise;

        public WorldNoise(WorldRandom random)
        {
            _noise = new OpenSimplexNoise(random.NoiseSeed());
        }

        /// <summary>About -1..1. <paramref name="wavelength"/> is the size of the largest features in world units.</summary>
        public float Fbm(float x, float z, float wavelength, int octaves, float gain = 0.5f)
        {
            float value = 0f;
            float amplitude = 1f;
            float total = 0f;
            double frequency = 1.0 / wavelength;
            for (int i = 0; i < octaves; i++)
            {
                value += _noise.Evaluate(x * frequency, z * frequency) * amplitude;
                total += amplitude;
                amplitude *= gain;
                frequency *= 2.03;
            }
            // Simplex rarely leaves about +-0.8, and the octaves average out further still.
            return Math.Clamp(value / total * 1.75f, -1f, 1f);
        }

        /// <summary>0..1, see <see cref="Fbm"/>.</summary>
        public float Fbm01(float x, float z, float wavelength, int octaves, float gain = 0.5f)
        {
            return Fbm(x, z, wavelength, octaves, gain) * 0.5f + 0.5f;
        }

        /// <summary>
        /// 0..1 ridges (Musgrave's ridged multifractal): sharp crests along the zero lines of the noise, and each octave
        /// is weighted by the one before, so detail gathers on the crests while the valleys between stay smooth.
        /// </summary>
        public float Ridged(float x, float z, float wavelength, int octaves, float gain = 0.5f)
        {
            float value = 0f;
            float amplitude = 1f;
            float total = 0f;
            float weight = 1f;
            double frequency = 1.0 / wavelength;
            for (int i = 0; i < octaves; i++)
            {
                float signal = 1f - MathF.Abs(_noise.Evaluate(x * frequency, z * frequency) * 1.6f);
                signal = signal < 0f ? 0f : signal;
                signal *= signal;
                value += signal * weight * amplitude;
                total += amplitude;
                weight = Math.Clamp(signal * 1.8f, 0f, 1f);
                amplitude *= gain;
                frequency *= 2.07;
            }
            return Math.Clamp(value / total * 1.35f, 0f, 1f);
        }

        /// <summary>A smooth offset of up to <paramref name="amplitude"/> world units, for warping the domain of other noises.</summary>
        public Vector2 Warp(float x, float z, float wavelength, float amplitude, int octaves = 3)
        {
            return new Vector2(
                Fbm(x + 311.7f, z - 127.1f, wavelength, octaves),
                Fbm(x - 269.5f, z + 183.3f, wavelength, octaves)) * amplitude;
        }
    }

    public static class WorldMath
    {
        public static float Saturate(float x) => x < 0f ? 0f : x > 1f ? 1f : x;

        public static float Smoothstep(float edge0, float edge1, float x)
        {
            float t = Saturate((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>Soft maximum: like Math.Max, but rounds the crease where the two inputs cross over a width of <paramref name="k"/>.</summary>
        public static float SmoothMax(float a, float b, float k)
        {
            float h = Saturate(0.5f + 0.5f * (a - b) / k);
            return Lerp(b, a, h) + k * h * (1f - h);
        }

        public static float SmoothMin(float a, float b, float k) => -SmoothMax(-a, -b, k);

        /// <summary>The angle in [-PI, PI] that turns <paramref name="from"/> onto <paramref name="to"/>.</summary>
        public static float AngleDifference(float from, float to)
        {
            float difference = (to - from) % MathF.Tau;
            if (difference > MathF.PI) difference -= MathF.Tau;
            if (difference < -MathF.PI) difference += MathF.Tau;
            return difference;
        }

        public static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b, out float t)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.LengthSquared;
            t = lengthSquared > 1e-8f ? Saturate(Vector2.Dot(point - a, ab) / lengthSquared) : 0f;
            return (point - (a + ab * t)).Length;
        }
    }
}
