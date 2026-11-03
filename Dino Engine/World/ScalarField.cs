using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>
    /// A grid of floats laid over the world: the value at node (x, z) belongs to world position (x, z) * <see cref="CellSize"/>.
    /// Reads outside the grid clamp to its edge. Every map of a generated world is one of these.
    /// </summary>
    public sealed class ScalarField
    {
        public readonly int Width;
        public readonly int Depth;
        public readonly float CellSize;
        public readonly float[] Data;

        /// <summary>Distance reported when there is nothing to measure to. Finite, because interpolating infinities gives NaN.</summary>
        public const float NoSource = 1e6f;

        private readonly float _inverseCell;

        public ScalarField(int width, int depth, float cellSize)
        {
            Width = width;
            Depth = depth;
            CellSize = cellSize;
            Data = new float[width * depth];
            _inverseCell = 1f / cellSize;
        }

        /// <summary>A field covering a square world with a node on both edges.</summary>
        public static ScalarField ForWorld(float worldSize, float cellSize)
        {
            int nodes = (int)MathF.Ceiling(worldSize / cellSize) + 1;
            return new ScalarField(nodes, nodes, cellSize);
        }

        public float this[int x, int z]
        {
            get => Data[z * Width + x];
            set => Data[z * Width + x] = value;
        }

        public int Index(int x, int z) => z * Width + x;

        public bool Contains(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Depth;

        public Vector2 WorldPosition(int x, int z) => new Vector2(x * CellSize, z * CellSize);

        public Vector2i NearestNode(Vector2 position)
        {
            return new Vector2i(
                Math.Clamp((int)MathF.Round(position.X * _inverseCell), 0, Width - 1),
                Math.Clamp((int)MathF.Round(position.Y * _inverseCell), 0, Depth - 1));
        }

        public float Clamped(int x, int z)
        {
            x = x < 0 ? 0 : x >= Width ? Width - 1 : x;
            z = z < 0 ? 0 : z >= Depth ? Depth - 1 : z;
            return Data[z * Width + x];
        }

        public float Sample(Vector2 position) => Sample(position.X, position.Y);

        /// <summary>Bilinear.</summary>
        public float Sample(float worldX, float worldZ)
        {
            float fx = worldX * _inverseCell;
            float fz = worldZ * _inverseCell;
            int x = (int)MathF.Floor(fx);
            int z = (int)MathF.Floor(fz);
            float tx = fx - x;
            float tz = fz - z;

            if (x < 0) { x = 0; tx = 0f; }
            else if (x >= Width - 1) { x = Width - 2; tx = 1f; }
            if (z < 0) { z = 0; tz = 0f; }
            else if (z >= Depth - 1) { z = Depth - 2; tz = 1f; }

            int i = z * Width + x;
            float near = Data[i] + (Data[i + 1] - Data[i]) * tx;
            float far = Data[i + Width] + (Data[i + Width + 1] - Data[i + Width]) * tx;
            return near + (far - near) * tz;
        }

        /// <summary>Catmull-Rom: unlike <see cref="Sample(float, float)"/> its slope is continuous, so upsampled terrain has no facets.</summary>
        public float SampleCubic(float worldX, float worldZ)
        {
            float fx = worldX * _inverseCell;
            float fz = worldZ * _inverseCell;
            int x = (int)MathF.Floor(fx);
            int z = (int)MathF.Floor(fz);
            float tx = fx - x;
            float tz = fz - z;

            float r0 = CatmullRom(Clamped(x - 1, z - 1), Clamped(x, z - 1), Clamped(x + 1, z - 1), Clamped(x + 2, z - 1), tx);
            float r1 = CatmullRom(Clamped(x - 1, z), Clamped(x, z), Clamped(x + 1, z), Clamped(x + 2, z), tx);
            float r2 = CatmullRom(Clamped(x - 1, z + 1), Clamped(x, z + 1), Clamped(x + 1, z + 1), Clamped(x + 2, z + 1), tx);
            float r3 = CatmullRom(Clamped(x - 1, z + 2), Clamped(x, z + 2), Clamped(x + 1, z + 2), Clamped(x + 2, z + 2), tx);
            return CatmullRom(r0, r1, r2, r3, tz);
        }

        private static float CatmullRom(float p0, float p1, float p2, float p3, float t)
        {
            return p1 + 0.5f * t * (p2 - p0 + t * (2f * p0 - 5f * p1 + 4f * p2 - p3 + t * (3f * (p1 - p2) + p3 - p0)));
        }

        /// <summary>Slope (rise over run) at a node, from central differences.</summary>
        public float SlopeAt(int x, int z)
        {
            float dx = (Clamped(x + 1, z) - Clamped(x - 1, z)) / (2f * CellSize);
            float dz = (Clamped(x, z + 1) - Clamped(x, z - 1)) / (2f * CellSize);
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Slope (rise over run) at a world position.</summary>
        public float SlopeAt(Vector2 position)
        {
            float step = CellSize;
            float dx = (Sample(position.X + step, position.Y) - Sample(position.X - step, position.Y)) / (2f * step);
            float dz = (Sample(position.X, position.Y + step) - Sample(position.X, position.Y - step)) / (2f * step);
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        public void Fill(float value)
        {
            Array.Fill(Data, value);
        }

        /// <summary>Sets every node from its world position, in parallel: <paramref name="value"/> must be pure.</summary>
        public void Generate(Func<float, float, float> value)
        {
            Parallel.For(0, Depth, z =>
            {
                int row = z * Width;
                float worldZ = z * CellSize;
                for (int x = 0; x < Width; x++)
                {
                    Data[row + x] = value(x * CellSize, worldZ);
                }
            });
        }

        public (float min, float max) Range()
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int i = 0; i < Data.Length; i++)
            {
                float v = Data[i];
                if (v < min) min = v;
                if (v > max) max = v;
            }
            return (min, max);
        }

        /// <summary>Separable box blur, repeated <paramref name="passes"/> times (three passes come close to a Gaussian).</summary>
        public void Blur(int radius, int passes = 1)
        {
            if (radius <= 0) return;
            float[] scratch = new float[Data.Length];
            for (int pass = 0; pass < passes; pass++)
            {
                BlurLines(Data, scratch, Width, Depth, 1, Width, radius);
                BlurLines(scratch, Data, Depth, Width, Width, 1, radius);
            }
        }

        private static void BlurLines(float[] source, float[] target, int length, int lines, int step, int lineStep, int radius)
        {
            float inverse = 1f / (2 * radius + 1);
            Parallel.For(0, lines, line =>
            {
                int start = line * lineStep;
                float sum = source[start] * (radius + 1);
                for (int i = 1; i <= radius; i++) sum += source[start + Math.Min(i, length - 1) * step];
                for (int i = 0; i < length; i++)
                {
                    target[start + i * step] = sum * inverse;
                    sum += source[start + Math.Min(i + radius + 1, length - 1) * step] - source[start + Math.Max(i - radius, 0) * step];
                }
            });
        }

        /// <summary>
        /// Exact distance, in world units, from every node to the nearest node where <paramref name="isSource"/> holds
        /// (Felzenszwalb and Huttenlocher). Sources read 0; without any source everything reads <see cref="NoSource"/>.
        /// </summary>
        public static ScalarField DistanceTo(int width, int depth, float cellSize, Func<int, bool> isSource)
        {
            const float Far = 1e12f;
            var field = new ScalarField(width, depth, cellSize);
            float[] d = field.Data;
            bool any = false;
            for (int i = 0; i < d.Length; i++)
            {
                bool source = isSource(i);
                any |= source;
                d[i] = source ? 0f : Far;
            }
            if (!any)
            {
                Array.Fill(d, NoSource);
                return field;
            }

            Parallel.For(0, depth, z => DistanceLine(d, z * width, 1, width));
            Parallel.For(0, width, x => DistanceLine(d, x, width, depth));
            Parallel.For(0, d.Length, i => d[i] = MathF.Sqrt(d[i]) * cellSize);
            return field;
        }

        // One dimension of the squared distance transform: the lower envelope of parabolas rooted at each node.
        private static void DistanceLine(float[] data, int start, int step, int length)
        {
            float[] f = new float[length];
            float[] boundary = new float[length + 1];
            int[] root = new int[length];
            for (int i = 0; i < length; i++) f[i] = data[start + i * step];

            int k = 0;
            root[0] = 0;
            boundary[0] = float.NegativeInfinity;
            boundary[1] = float.PositiveInfinity;
            for (int q = 1; q < length; q++)
            {
                float s;
                while (true)
                {
                    int r = root[k];
                    s = ((f[q] + (float)q * q) - (f[r] + (float)r * r)) / (2f * (q - r));
                    if (s > boundary[k] || k == 0) break;
                    k--;
                }
                k++;
                root[k] = q;
                boundary[k] = s;
                boundary[k + 1] = float.PositiveInfinity;
            }

            k = 0;
            for (int q = 0; q < length; q++)
            {
                while (boundary[k + 1] < q) k++;
                float offset = q - root[k];
                data[start + q * step] = offset * offset + f[root[k]];
            }
        }
    }
}
