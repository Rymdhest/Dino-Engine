namespace Dino_Engine.Rendering.Renderers.PosGeometry
{
    /// <summary>
    /// Generates the water's textures at startup, on the CPU, from seeded noise and wave sums. Everything tiles.
    /// <para>Ripples (3D: x, z, time): a field of small waves with integer wave vectors over the tile, so it tiles, and
    /// whole numbers of periods over the animation loop, so it loops. Longer waves run slower, roughly as sqrt(wavenumber)
    /// like real deep-water waves. Stored as (dh/dx, dh/dz, (dh/dx)^2, (dh/dz)^2) with a mean square slope of 1: mipmaps then
    /// average slopes into the surface normal and squared slopes into roughness (LEAN mapping), so ripples too small to
    /// see widen the sun's glint instead of sparkling.</para>
    /// <para>Pattern (2D, RGBA8): r foam lace, g caustics (intensity / 3), b and a smooth noise for large-scale variation.</para>
    /// </summary>
    internal static class WaterTextureBaker
    {
        public const int RippleResolution = 128;
        public const int RippleFrames = 32;
        public const int RippleMinCycles = 2;   // The longest ripples fit this many times into a tile.
        public const int RippleMaxCycles = 22;
        private const int RippleWaveCount = 40;

        public const int PatternResolution = 512;
        public const float CausticEncodeScale = 3f;   // Caustic intensity is stored divided by this (it clips above).
        // Second caustic layer, rotated and scaled against the first. Must match CAUSTIC_ROTATION / CAUSTIC_SCALE in WaterCommon.glsl.
        public const float CausticRotation = 0.8f;
        public const float CausticScale = 1.37f;

        private const int CausticRays = 1536;       // Per axis: 9 rays per output texel.
        private const int CausticWaveCount = 24;

        private struct Wave
        {
            public int Kx;
            public int Kz;
            public float Amplitude;
            public int Cycles;     // Periods per animation loop.
            public float Phase;
        }

        // ---------------------------------------------------------------- ripples

        public static float[] BakeRipples()
        {
            var rng = new Random(7331);
            Wave[] waves = RandomWaves(rng, RippleWaveCount, RippleMinCycles, RippleMaxCycles, 1.75f);
            for (int w = 0; w < waves.Length; w++)
            {
                float k = MathF.Sqrt(waves[w].Kx * waves[w].Kx + waves[w].Kz * waves[w].Kz);
                waves[w].Cycles = Math.Max(1, (int)MathF.Round(MathF.Sqrt(k / RippleMinCycles)));
            }

            int n = RippleResolution;
            int frames = RippleFrames;
            int k0 = waves.Length;
            float[] data = new float[n * n * frames * 4];

            // Separable phases: sin(a + b) with a along x only and b along z (and time) only.
            float[] sinX = new float[k0 * n], cosX = new float[k0 * n];
            for (int w = 0; w < k0; w++)
            {
                for (int x = 0; x < n; x++)
                {
                    float a = MathF.Tau * waves[w].Kx * x / n;
                    sinX[w * n + x] = MathF.Sin(a);
                    cosX[w * n + x] = MathF.Cos(a);
                }
            }

            double[] meanSquare = new double[frames];
            Parallel.For(0, frames, f =>
            {
                float[] sinZ = new float[k0 * n], cosZ = new float[k0 * n];
                float[] slopeX = new float[k0], slopeZ = new float[k0];
                for (int w = 0; w < k0; w++)
                {
                    // h = a cos(theta) travels along k; d/du of it is -2 pi a k sin(theta).
                    slopeX[w] = -MathF.Tau * waves[w].Amplitude * waves[w].Kx;
                    slopeZ[w] = -MathF.Tau * waves[w].Amplitude * waves[w].Kz;
                    float time = MathF.Tau * waves[w].Cycles * f / frames;
                    for (int z = 0; z < n; z++)
                    {
                        float b = MathF.Tau * waves[w].Kz * z / n - time + waves[w].Phase;
                        sinZ[w * n + z] = MathF.Sin(b);
                        cosZ[w * n + z] = MathF.Cos(b);
                    }
                }

                double sum = 0.0;
                for (int z = 0; z < n; z++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float sx = 0f, sz = 0f;
                        for (int w = 0; w < k0; w++)
                        {
                            float s = sinX[w * n + x] * cosZ[w * n + z] + cosX[w * n + x] * sinZ[w * n + z];
                            sx += slopeX[w] * s;
                            sz += slopeZ[w] * s;
                        }
                        int i = ((f * n + z) * n + x) * 4;
                        data[i] = sx;
                        data[i + 1] = sz;
                        sum += sx * sx + sz * sz;
                    }
                }
                meanSquare[f] = sum;
            });

            double total = 0.0;
            foreach (double m in meanSquare) total += m;
            float scale = (float)(1.0 / Math.Sqrt(Math.Max(total / (n * n * frames), 1e-12)));
            for (int i = 0; i < data.Length; i += 4)
            {
                float sx = data[i] * scale;
                float sz = data[i + 1] * scale;
                data[i] = sx;
                data[i + 1] = sz;
                data[i + 2] = sx * sx;
                data[i + 3] = sz * sz;
            }
            return data;
        }

        /// <summary>Distinct integer wave vectors with lengths log-uniform in [minCycles, maxCycles], height ~ |k|^-falloff.</summary>
        private static Wave[] RandomWaves(Random rng, int count, int minCycles, int maxCycles, float falloff)
        {
            var waves = new List<Wave>(count);
            var used = new HashSet<(int, int)>();
            float logMin = MathF.Log(minCycles), logMax = MathF.Log(maxCycles);
            int guard = 0;
            while (waves.Count < count && guard++ < count * 200)
            {
                float k = MathF.Exp(logMin + (logMax - logMin) * rng.NextSingle());
                float angle = MathF.Tau * rng.NextSingle();
                int kx = (int)MathF.Round(k * MathF.Cos(angle));
                int kz = (int)MathF.Round(k * MathF.Sin(angle));
                float length = MathF.Sqrt(kx * kx + kz * kz);
                if (length < minCycles * 0.8f || length > maxCycles * 1.1f) continue;
                // A wave and its opposite would add up to a standing wave; keep only one of each pair.
                if (!used.Add((kx, kz)) || used.Contains((-kx, -kz))) continue;
                waves.Add(new Wave { Kx = kx, Kz = kz, Amplitude = MathF.Pow(length, -falloff), Phase = MathF.Tau * rng.NextSingle() });
            }
            return waves.ToArray();
        }

        // ---------------------------------------------------------------- pattern

        public static byte[] BakePattern(out float causticMinMean)
        {
            int n = PatternResolution;
            float[] caustics = BakeCaustics(n);
            byte[] data = new byte[n * n * 4];

            Parallel.For(0, n, z =>
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (z + 0.5f) / n;

                    Worley(u, v, 10, 101u, out float f1, out float f2);
                    float lace = 1f - SmoothStep(0f, 0.18f, f2 - f1);
                    Worley(u, v, 26, 202u, out float b1, out _);
                    float bubbles = 1f - SmoothStep(0.1f, 0.55f, b1);
                    float detail = Fbm(u, v, 16, 3, 303u);
                    float foam = Math.Clamp(0.55f * lace + 0.35f * bubbles * detail * 1.6f + 0.25f * detail, 0f, 1f);

                    int i = (z * n + x) * 4;
                    data[i] = ToByte(foam);
                    data[i + 1] = ToByte(caustics[z * n + x] / CausticEncodeScale);
                    data[i + 2] = ToByte(Fbm(u, v, 4, 4, 404u));
                    data[i + 3] = ToByte(Fbm(u, v, 4, 4, 505u));
                }
            });

            causticMinMean = MeanOfCrossedCaustics(data, n);
            return data;
        }

        /// <summary>
        /// Light from straight above refracted through a wavy surface onto a floor below it: rays on a regular grid are
        /// bent by the local slope and splatted where they land, so the floor gets bright where the waves focus them.
        /// Mean intensity 1.
        /// </summary>
        private static float[] BakeCaustics(int n)
        {
            var rng = new Random(9157);
            Wave[] waves = RandomWaves(rng, CausticWaveCount, 2, 7, 2f);
            int m = CausticRays;
            int k0 = waves.Length;

            float[] sinU = new float[k0 * m], cosU = new float[k0 * m], sinV = new float[k0 * m], cosV = new float[k0 * m];
            for (int w = 0; w < k0; w++)
            {
                for (int i = 0; i < m; i++)
                {
                    float a = MathF.Tau * waves[w].Kx * i / m;
                    float b = MathF.Tau * waves[w].Kz * i / m + waves[w].Phase;
                    sinU[w * m + i] = MathF.Sin(a);
                    cosU[w * m + i] = MathF.Cos(a);
                    sinV[w * m + i] = MathF.Sin(b);
                    cosV[w * m + i] = MathF.Cos(b);
                }
            }

            // Floor depth: chosen so the strongest-curving patches focus their light just above the floor, which gives a
            // network of sharp lines rather than either a faint shimmer or chaos.
            double curvatureSquares = 0.0;
            int probes = 0;
            for (int j = 0; j < m; j += 16)
            {
                for (int i = 0; i < m; i += 16)
                {
                    float laplacian = 0f;
                    for (int w = 0; w < k0; w++)
                    {
                        float k2 = waves[w].Kx * waves[w].Kx + waves[w].Kz * waves[w].Kz;
                        float c = cosU[w * m + i] * cosV[w * m + j] - sinU[w * m + i] * sinV[w * m + j];
                        laplacian -= MathF.Tau * MathF.Tau * k2 * waves[w].Amplitude * c;
                    }
                    curvatureSquares += laplacian * laplacian;
                    probes++;
                }
            }
            float depth = 0.75f / MathF.Sqrt((float)(curvatureSquares / probes));

            float weight = (float)n * n / ((float)m * m);
            var partials = new System.Collections.Concurrent.ConcurrentBag<float[]>();
            Parallel.For(0, m, () => new float[n * n], (j, _, accumulator) =>
            {
                for (int i = 0; i < m; i++)
                {
                    float gx = 0f, gz = 0f;
                    for (int w = 0; w < k0; w++)
                    {
                        float s = sinU[w * m + i] * cosV[w * m + j] + cosU[w * m + i] * sinV[w * m + j];
                        float g = -MathF.Tau * waves[w].Amplitude * s;
                        gx += g * waves[w].Kx;
                        gz += g * waves[w].Kz;
                    }
                    // Landing point in texels; the ray tilts away from the slope's uphill side.
                    float px = ((float)i / m - depth * gx) * n - 0.5f;
                    float pz = ((float)j / m - depth * gz) * n - 0.5f;
                    Splat(accumulator, n, px, pz, weight);
                }
                return accumulator;
            }, accumulator => partials.Add(accumulator));

            float[] sum = new float[n * n];
            foreach (float[] partial in partials)
            {
                for (int i = 0; i < sum.Length; i++) sum[i] += partial[i];
            }

            // Soften the splat grid a little (binomial 3x3, wrapping).
            float[] blurred = new float[n * n];
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    float total = 0f;
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            float k = (dx == 0 ? 2f : 1f) * (dz == 0 ? 2f : 1f);
                            total += k * sum[Wrap(z + dz, n) * n + Wrap(x + dx, n)];
                        }
                    }
                    blurred[z * n + x] = total / 16f;
                }
            }
            return blurred;
        }

        private static void Splat(float[] target, int n, float px, float pz, float weight)
        {
            float fx = MathF.Floor(px), fz = MathF.Floor(pz);
            float tx = px - fx, tz = pz - fz;
            int x0 = Wrap((int)fx, n), z0 = Wrap((int)fz, n);
            int x1 = Wrap(x0 + 1, n), z1 = Wrap(z0 + 1, n);
            target[z0 * n + x0] += weight * (1f - tx) * (1f - tz);
            target[z0 * n + x1] += weight * tx * (1f - tz);
            target[z1 * n + x0] += weight * (1f - tx) * tz;
            target[z1 * n + x1] += weight * tx * tz;
        }

        /// <summary>The average of min(layer A, layer B) as the shader crosses two caustic layers, for normalising it to 1.</summary>
        private static float MeanOfCrossedCaustics(byte[] data, int n)
        {
            var rng = new Random(1234);
            float cosR = MathF.Cos(CausticRotation), sinR = MathF.Sin(CausticRotation);
            double sum = 0.0;
            const int samples = 65536;
            for (int s = 0; s < samples; s++)
            {
                float u = rng.NextSingle() * 8f, v = rng.NextSingle() * 8f;
                float ru = (cosR * u - sinR * v) * CausticScale + rng.NextSingle();
                float rv = (sinR * u + cosR * v) * CausticScale + rng.NextSingle();
                float a = data[(Wrap((int)MathF.Floor(v * n), n) * n + Wrap((int)MathF.Floor(u * n), n)) * 4 + 1];
                float b = data[(Wrap((int)MathF.Floor(rv * n), n) * n + Wrap((int)MathF.Floor(ru * n), n)) * 4 + 1];
                sum += MathF.Min(a, b) / 255f * CausticEncodeScale;
            }
            return (float)Math.Max(sum / samples, 1e-3);
        }

        // ---------------------------------------------------------------- tileable noise

        /// <summary>Distances to the nearest and second nearest feature point of a cells x cells grid tiling the unit square.</summary>
        private static void Worley(float u, float v, int cells, uint seed, out float f1, out float f2)
        {
            float px = u * cells, pz = v * cells;
            int cx = (int)MathF.Floor(px), cz = (int)MathF.Floor(pz);
            f1 = 10f;
            f2 = 10f;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int ix = cx + dx, iz = cz + dz;
                    uint h = Hash((uint)Wrap(ix, cells) + Hash((uint)Wrap(iz, cells) + seed));
                    float fx = ix + (h & 0xffffu) / 65535f;
                    float fz = iz + (h >> 16) / 65535f;
                    float d = MathF.Sqrt((fx - px) * (fx - px) + (fz - pz) * (fz - pz));
                    if (d < f1)
                    {
                        f2 = f1;
                        f1 = d;
                    }
                    else if (d < f2)
                    {
                        f2 = d;
                    }
                }
            }
        }

        /// <summary>Value noise fBm tiling the unit square, about 0..1.</summary>
        private static float Fbm(float u, float v, int period, int octaves, uint seed)
        {
            float sum = 0f, amplitude = 1f, total = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += amplitude * TileableValueNoise(u * period, v * period, period, seed + (uint)o * 7919u);
                total += amplitude;
                amplitude *= 0.5f;
                period *= 2;
            }
            return Math.Clamp((sum / total - 0.5f) * 1.7f + 0.5f, 0f, 1f);
        }

        private static float TileableValueNoise(float x, float z, int period, uint seed)
        {
            int ix = (int)MathF.Floor(x), iz = (int)MathF.Floor(z);
            float tx = x - ix, tz = z - iz;
            float ux = tx * tx * tx * (tx * (tx * 6f - 15f) + 10f);
            float uz = tz * tz * tz * (tz * (tz * 6f - 15f) + 10f);
            float a = Lattice(ix, iz, period, seed), b = Lattice(ix + 1, iz, period, seed);
            float c = Lattice(ix, iz + 1, period, seed), d = Lattice(ix + 1, iz + 1, period, seed);
            return a + (b - a) * ux + (c - a) * uz + (a - b - c + d) * ux * uz;
        }

        private static float Lattice(int x, int z, int period, uint seed)
        {
            uint h = Hash((uint)Wrap(x, period) + Hash((uint)Wrap(z, period) + seed));
            return (h & 0xffffffu) / 16777215f;
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

        private static int Wrap(int value, int period) => ((value % period) + period) % period;

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private static byte ToByte(float value) => (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
    }
}
