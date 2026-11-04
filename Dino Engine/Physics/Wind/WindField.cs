using Dino_Engine.ECS.Components;
using OpenTK.Mathematics;

namespace Dino_Engine.Physics
{
    public enum WindEmitterType
    {
        /// <summary>A blast wave expanding from <see cref="WindEmitter.Position"/> over <see cref="WindEmitter.Duration"/>.</summary>
        Blast = 0,
        /// <summary>Downwash: air pushed down onto the ground and spreading out (a rotor, a jet blowing down).</summary>
        Radial = 1,
        /// <summary>Blows along <see cref="WindEmitter.Direction"/> (a fan, a vehicle's wake).</summary>
        Directional = 2,
    }

    /// <summary>A local source of wind. Emitters act as vertical columns: only the horizontal distance to them counts.</summary>
    public struct WindEmitter
    {
        public WindEmitterType Type;
        public Vector3 Position;
        public Vector3 Direction;   // World space, Directional only.
        public float Radius;        // Metres the emitter reaches (for a blast: how far its front travels).
        public float Strength;      // Peak wind speed in m/s.
        public float Age;           // Seconds, Blast only.
        public float Duration;      // Seconds, Blast only.
    }

    public struct WindSample
    {
        /// <summary>Wind velocity in m/s (x, y = up, z).</summary>
        public Vector3 Velocity;
        /// <summary>How strong the local gust is, 0 (lull) to 1 (peak).</summary>
        public float Gust;
        /// <summary>How much of a wave is passing, 0 (trough) to 1 (crest). None without wind to speak of.</summary>
        public float Crest;
    }

    /// <summary>
    /// The engine's wind model, evaluated on the CPU. This is an exact mirror of Shaders/Wind.glsl (same hash, noise,
    /// constants and formulas), so gameplay code sees the same wind the GPU bakes into the wind map. Any change here
    /// must be made in Wind.glsl too, and the other way round.
    /// </summary>
    public static class WindField
    {
        public const float NoisePeriod = 256f;        // WIND_NOISE_PERIOD
        public const int GustOctaves = 2;             // WIND_GUST_OCTAVES
        public const int MaxGpuEmitters = 32;         // WIND_MAX_EMITTERS
        public const int WaveTrains = 4;              // WIND_WAVE_TRAINS
        public const float MaxVegetationBend = 1.5f;  // VEGETATION_MAX_TOTAL_BEND

        /// <summary>A whole number of waves of every family: their phases all land on a whole wave here, so wrapping never shows.</summary>
        public const float WaveCyclesPeriod = 10f;
        /// <summary>A whole number of flutters of every kind (WIND_FLUTTER_KINDS), likewise.</summary>
        public const float FlutterCyclesPeriod = 4f;

        // Waves come from a rose of fixed directions, and each family takes the two nearest to where it's headed: a wind that
        // turns fades one direction out and the next in. Turning the fronts themselves is no good, as far from the world's
        // origin they would slide past at hundreds of metres a second.
        private const float RoseStep = MathF.PI / 6f;
        // The two families, which meet in crests and part in lulls: how far off the wind they run, how much shorter than
        // WaveLength they are, how high they stand, and where their rose starts.
        private static readonly (float offAngle, float frequency, float amplitude, float roseStart)[] WaveFamilies =
        {
            (0.29f, 1f, 0.34f, 0f),
            (-0.38f, 1.7f, 0.2f, RoseStep * 0.5f),
        };

        /// <summary>
        /// How fast patterns travel over the land in m/s. A light wind carries them at about its own pace, and a strong one
        /// leaves them behind: strength shows in how far plants lean, and keeping up would only make the field frantic.
        /// </summary>
        public static float PatternSpeed(float speed) => speed / (1f + speed / 12f);

        /// <summary>Radians a crest leans plants over at this speed. It grows with the wind and levels off.</summary>
        public static float WaveLeanAt(float waveLean, float speed) => waveLean * 1.8f * MathF.Tanh(speed / 5.4f);

        /// <summary>How much of the waves there is at this speed, 0..1: they die away as the air falls still.</summary>
        public static float WaveStrength(float speed)
        {
            float t = Math.Clamp(speed / 2.5f, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// One of the wave trains: xy its wave vector (cycles per metre), z its phase (cycles, 0..1), w its height. Each family
        /// has a pair, faded between the two directions of the rose either side of where the family is headed.
        /// </summary>
        public static Vector4 WaveTrain(in WindSingleton wind, int index)
        {
            var (offAngle, frequency, amplitude, roseStart) = WaveFamilies[index / 2];
            float place = (wind.Heading + offAngle - roseStart) / RoseStep;
            float step = MathF.Floor(place);
            float towardsNext = MathF.Sin((place - step) * MathF.PI * 0.5f);
            float next = towardsNext * towardsNext;
            float previous = 1f - next;
            float height = amplitude * (index % 2 == 0 ? previous : next) / MathF.Sqrt(previous * previous + next * next);
            Vector2 direction = WindSingleton.Towards((step + index % 2) * RoseStep + roseStart);
            float phase = wind.WaveCycles * frequency;
            Vector2 waveVector = direction * (frequency * InverseScale(wind.WaveLength));
            return new Vector4(waveVector.X, waveVector.Y, phase - MathF.Floor(phase), height);
        }

        /// <summary>1 / scale, as uploaded to the GPU, so both sides divide by the same value.</summary>
        public static float InverseScale(float scale) => 1f / MathF.Max(scale, 0.01f);

        /// <summary>The ambient wind (no emitters) on the ground at <paramref name="xz"/>.</summary>
        public static WindSample SampleAmbient(in WindSingleton wind, Vector2 xz)
        {
            float gust = GustNoise(wind, xz);
            float speedScale = MathF.Max(1f + wind.Gustiness * gust, 0f);
            Vector2 velocity = wind.Velocity * speedScale + TurbulenceCurl(wind, xz) * (wind.LiveSpeed * wind.Turbulence * 0.5f);
            return new WindSample { Velocity = new Vector3(velocity.X, 0f, velocity.Y), Gust = gust * 0.5f + 0.5f, Crest = Crest(wind, xz) };
        }

        /// <summary>The full wind (ambient plus every emitter) on the ground at <paramref name="xz"/>.</summary>
        public static WindSample Sample(in WindSingleton wind, Vector2 xz)
        {
            WindSample sample = SampleAmbient(wind, xz);
            if (wind.Emitters != null)
            {
                foreach (WindEmitter emitter in wind.Emitters) sample.Velocity += EmitterVelocity(emitter, xz);
            }
            return sample;
        }

        public static Vector3 EmitterVelocity(in WindEmitter emitter, Vector2 xz)
        {
            float radius = emitter.Radius;
            float strength = emitter.Strength;
            if (radius <= 0f) return Vector3.Zero;

            Vector2 offset = xz - emitter.Position.Xz;
            float dist = offset.Length;
            Vector2 outward = dist > 1e-4f ? offset / dist : Vector2.Zero;

            switch (emitter.Type)
            {
                case WindEmitterType.Blast:
                {
                    float duration = emitter.Duration;
                    if (duration <= 0f) return Vector3.Zero;
                    float t = Math.Clamp(emitter.Age / duration, 0f, 1f);
                    float decay = (1f - t) * (1f - t);
                    float front = radius * MathF.Pow(t, 0.4f);
                    float width = MathF.Max(radius * 0.2f, 0.5f);
                    float frontProfile = (dist - front) / width;
                    float outflow = MathF.Exp(-frontProfile * frontProfile) + (dist < front ? 0.25f * dist / MathF.Max(front, 1e-4f) : 0f);
                    float centre = dist / width;
                    return new Vector3(outward.X * outflow, 0.4f * MathF.Exp(-centre * centre), outward.Y * outflow) * (strength * decay);
                }
                case WindEmitterType.Radial:
                {
                    float x = dist / radius;
                    if (x >= 1f) return Vector3.Zero;
                    float spread = MathF.Sin(3.14159265f * x);
                    return new Vector3(outward.X * spread, -(1f - x) * (1f - x), outward.Y * spread) * strength;
                }
                default:
                {
                    float x = dist / radius;
                    if (x >= 1f) return Vector3.Zero;
                    float falloff = (1f - x * x) * (1f - x * x);
                    return emitter.Direction * (strength * falloff);
                }
            }
        }

        /// <summary>The bend vegetation settles at in a steady wind (radians, leaning downwind); vegetationRestBend in Wind.glsl.</summary>
        public static Vector2 VegetationRestBend(in WindSingleton wind, Vector2 windXZ)
        {
            float speed = windXZ.Length;
            if (speed <= 1e-4f || wind.VegetationReferenceSpeed <= 0f) return Vector2.Zero;
            float relative = speed / wind.VegetationReferenceSpeed;
            return windXZ / speed * (wind.VegetationMaxBend * (1f - MathF.Exp(-relative * relative)));
        }

        /// <summary>How much of a wave is passing here, 0..1; windCrest in Wind.glsl.</summary>
        public static float Crest(in WindSingleton wind, Vector2 xz)
        {
            float wander = FrontWander(wind, xz);
            float sum = 0f;
            for (int i = 0; i < WaveTrains; i++)
            {
                Vector4 train = WaveTrain(wind, i);
                float shifted = i < 2 ? wander : -0.7f * wander;
                sum += train.W * MathF.Sin(MathF.Tau * (xz.X * train.X + xz.Y * train.Y + shifted - train.Z));
            }
            float wave = Math.Clamp(0.5f + sum, 0f, 1f);
            return wave * wave * WaveStrength(wind.LiveSpeed);
        }

        /// <summary>The bend a patch of plants is heading for at this moment; vegetationTargetBend in Wind.glsl.</summary>
        public static Vector2 VegetationTargetBend(in WindSingleton wind, Vector2 windXZ, Vector2 xz)
        {
            return VegetationRestBend(wind, windXZ) + WindSingleton.Towards(wind.Heading) * (WaveLeanAt(wind.WaveLean, wind.LiveSpeed) * Crest(wind, xz));
        }

        /// <summary>Wraps a pattern offset or phase into the noise period; the noise tiles, so this never shows.</summary>
        public static float WrapToPeriod(float value) => value - NoisePeriod * MathF.Floor(value / NoisePeriod);

        // ---------------------------------------------------------------- noise (mirrors Wind.glsl)

        private static float GustNoise(in WindSingleton wind, Vector2 xz)
        {
            Vector2 q = xz * InverseScale(wind.GustScale) - wind.GustOffset;
            float sum = 0f;
            float amplitude = 1f;
            float total = 0f;
            float frequency = 1f;
            for (int o = 0; o < GustOctaves; o++)
            {
                sum += amplitude * Noised(new Vector3(q.X, q.Y, wind.GustPhase) * frequency, NoisePeriod * frequency).X;
                total += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }
            float gust = Math.Clamp(sum / total * 1.8f, -1f, 1f);
            return gust * (1.5f - 0.5f * gust * gust);
        }

        private static Vector2 TurbulenceCurl(in WindSingleton wind, Vector2 xz)
        {
            Vector2 q = xz * InverseScale(wind.TurbulenceScale) - wind.TurbulenceOffset;
            Vector4 n = Noised(new Vector3(q.X, q.Y, wind.TurbulencePhase), NoisePeriod);
            return new Vector2(n.Z, -n.Y);
        }

        // The fronts of the waves waver as they come: a few long, slow sines, cheap enough for every texel of the simulation.
        private static float FrontWander(in WindSingleton wind, Vector2 xz)
        {
            float slow = MathF.Tau * wind.WaveWander;
            return 0.5f * MathF.Sin(xz.X * 0.083f + xz.Y * 0.047f + slow)
                 + 0.35f * MathF.Sin(xz.X * -0.061f + xz.Y * 0.109f - slow)
                 + 0.25f * MathF.Sin(xz.X * 0.137f + xz.Y * -0.094f + 2f * slow);
        }

        private static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return x;
        }

        private static Vector3 LatticeGradient(Vector3 cell, float period)
        {
            float wx = cell.X - period * MathF.Floor(cell.X / period);
            float wy = cell.Y - period * MathF.Floor(cell.Y / period);
            float wz = cell.Z - period * MathF.Floor(cell.Z / period);
            uint h = Hash((uint)wx + Hash((uint)wy + Hash((uint)wz)));
            Vector3 g = new Vector3(h & 1023u, (h >> 10) & 1023u, (h >> 20) & 1023u) * (2f / 1023f) - Vector3.One;
            return g / MathF.Sqrt(MathF.Max(Vector3.Dot(g, g), 1e-6f));
        }

        /// <summary>Gradient noise with analytic derivatives: (value, d/dx, d/dy, d/dz).</summary>
        private static Vector4 Noised(Vector3 x, float period)
        {
            Vector3 i = new Vector3(MathF.Floor(x.X), MathF.Floor(x.Y), MathF.Floor(x.Z));
            Vector3 f = x - i;
            Vector3 u = f * f * f * (f * (f * 6f - new Vector3(15f)) + new Vector3(10f));
            Vector3 du = 30f * f * f * (f * (f - new Vector3(2f)) + Vector3.One);

            Vector3 ga = LatticeGradient(i + new Vector3(0f, 0f, 0f), period);
            Vector3 gb = LatticeGradient(i + new Vector3(1f, 0f, 0f), period);
            Vector3 gc = LatticeGradient(i + new Vector3(0f, 1f, 0f), period);
            Vector3 gd = LatticeGradient(i + new Vector3(1f, 1f, 0f), period);
            Vector3 ge = LatticeGradient(i + new Vector3(0f, 0f, 1f), period);
            Vector3 gf = LatticeGradient(i + new Vector3(1f, 0f, 1f), period);
            Vector3 gg = LatticeGradient(i + new Vector3(0f, 1f, 1f), period);
            Vector3 gh = LatticeGradient(i + new Vector3(1f, 1f, 1f), period);

            float va = Vector3.Dot(ga, f - new Vector3(0f, 0f, 0f));
            float vb = Vector3.Dot(gb, f - new Vector3(1f, 0f, 0f));
            float vc = Vector3.Dot(gc, f - new Vector3(0f, 1f, 0f));
            float vd = Vector3.Dot(gd, f - new Vector3(1f, 1f, 0f));
            float ve = Vector3.Dot(ge, f - new Vector3(0f, 0f, 1f));
            float vf = Vector3.Dot(gf, f - new Vector3(1f, 0f, 1f));
            float vg = Vector3.Dot(gg, f - new Vector3(0f, 1f, 1f));
            float vh = Vector3.Dot(gh, f - new Vector3(1f, 1f, 1f));

            float value = va + u.X * (vb - va) + u.Y * (vc - va) + u.Z * (ve - va)
                        + u.X * u.Y * (va - vb - vc + vd) + u.Y * u.Z * (va - vc - ve + vg) + u.Z * u.X * (va - vb - ve + vf)
                        + u.X * u.Y * u.Z * (-va + vb + vc - vd + ve - vf - vg + vh);

            Vector3 uYZX = new Vector3(u.Y, u.Z, u.X);
            Vector3 uZXY = new Vector3(u.Z, u.X, u.Y);
            Vector3 derivative = ga + u.X * (gb - ga) + u.Y * (gc - ga) + u.Z * (ge - ga)
                               + u.X * u.Y * (ga - gb - gc + gd) + u.Y * u.Z * (ga - gc - ge + gg) + u.Z * u.X * (ga - gb - ge + gf)
                               + u.X * u.Y * u.Z * (-ga + gb + gc - gd + ge - gf - gg + gh)
                               + du * (new Vector3(vb - va, vc - va, ve - va)
                                     + uYZX * new Vector3(va - vb - vc + vd, va - vc - ve + vg, va - vb - ve + vf)
                                     + uZXY * new Vector3(va - vb - ve + vf, va - vb - vc + vd, va - vc - ve + vg)
                                     + uYZX * uZXY * (-va + vb + vc - vd + ve - vf - vg + vh));

            return new Vector4(value, derivative.X, derivative.Y, derivative.Z);
        }
    }
}
