using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Raises the mountains. Noise can't make a mountain: what makes one look like one is that every slope is the side of a valley, and every valley drains
    /// into the next, so that ridges branch and a crest has a valley either side of it. So this finds what the stream power law settles into (Braun and Willett,
    /// Cordonnier et al.): water leaves the mountain by a tree of channels, and where uplift and erosion have come to balance a channel's slope falls off
    /// as the land draining through it grows. That's solved directly rather than simulated, by turns: route the water over what's there, then rebuild every height from the
    /// channels up. This is on the coarse map and comes before anything looks for ground to stand on, so sites and roads simply find a wall where a mountain is.
    /// <see cref="RockSculpt"/> does the rest, once it's known where people go.
    /// </summary>
    public sealed class MountainStage : IWorldStage
    {
        /// <summary>The share of <see cref="WorldPlan.MountainCoverage"/> that stands as rock nobody can walk on. The rest is the foothills around it.</summary>
        private const float BareShare = 0.42f;
        private const int Turns = 12;
        private const float SteepestSlope = 1.15f;   // Of ground that only drains itself: the side of a crest.
        private const float AreaExponent = 0.22f;   // How fast slope falls off as a channel grows. Real rivers go nearer 0.5, but those aren't 100 m hills.
        private const float PassRadius = 15f;
        private const float SmallestApart = 0.01f;  // Share of the land that's worth a pass through the mountains.

        public string Description => "Raising the mountains";
        public float Cost => 2f;

        public void Run(WorldData world)
        {
            WorldRandom random = world.Random.Fork("mountains");
            world.Geology = Geology.Draw(world.Plan, random.Fork("rock"));
            var noise = new WorldNoise(random.Fork("outline"));

            ScalarField massif = Outline(world, noise);
            KeepLandWhole(world, massif);
            world.Massif = massif;

            Raise(world, massif, new WorldNoise(random.Fork("hardness")));
        }

        // Where rock stands: the wall around the world, and the mountains inside it, as much of them as the plan asks for.
        private static ScalarField Outline(WorldData world, WorldNoise noise)
        {
            WorldPlan plan = world.Plan;
            ScalarField elevation = world.Elevation;
            ScalarField highland = world.Highland;
            ScalarField boundary = world.Boundary;

            // Wandering at two scales: bays and spurs, and then what makes their edges look broken rather than drawn.
            ScalarField score = ScalarField.ForWorld(world.Size, WorldData.CoarseCell);
            score.Generate((x, z) => highland.Sample(x, z) + noise.Fbm(x, z, 150f, 4) * 0.11f + noise.Ridged(x - 310f, z + 77f, 240f, 3) * 0.07f);

            float threshold = plan.Archetype.Landform == LandformKind.Marsh ? float.MaxValue : Quantile(score, elevation, 1f - plan.MountainCoverage * BareShare);

            ScalarField massif = ScalarField.ForWorld(world.Size, WorldData.CoarseCell);
            massif.Generate((x, z) =>
            {
                if (elevation.Sample(x, z) <= WorldPlan.SeaLevel) return 0f;
                float wall = Smoothstep(0.2f, 0.25f, boundary.Sample(x, z) + noise.Fbm(x + 90f, z - 40f, 210f, 5, 0.6f) * 0.2f);
                float inner = Smoothstep(threshold, threshold + 0.012f, score.Sample(x, z));
                return MathF.Max(wall, inner);
            });
            return massif;
        }

        private static float Quantile(ScalarField score, ScalarField elevation, float share)
        {
            var land = new List<float>(score.Data.Length);
            for (int i = 0; i < score.Data.Length; i++)
            {
                if (elevation.Data[i] > WorldPlan.SeaLevel + 0.2f) land.Add(score.Data[i]);
            }
            if (land.Count == 0) return float.MaxValue;
            land.Sort();
            return land[Math.Clamp((int)(land.Count * share), 0, land.Count - 1)];
        }

        // A range that cuts the land in two would strand half of it. Wherever one has, the lowest way through is opened up as a pass.
        private static void KeepLandWhole(WorldData world, ScalarField massif)
        {
            int width = massif.Width;
            int depth = massif.Depth;
            int cells = width * depth;
            float[] data = massif.Data;
            float[] ground = world.Elevation.Data;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                int[] part = new int[cells];
                int parts = 0;
                var sizes = new List<int> { 0 };
                var stack = new Stack<int>();
                int land = 0;
                for (int start = 0; start < cells; start++)
                {
                    if (!IsOpen(start)) continue;
                    land++;
                    if (part[start] != 0) continue;
                    part[start] = ++parts;
                    sizes.Add(0);
                    stack.Push(start);
                    while (stack.Count > 0)
                    {
                        int i = stack.Pop();
                        sizes[parts]++;
                        int x = i % width;
                        int z = i / width;
                        if (x > 0) Spread(i - 1);
                        if (x < width - 1) Spread(i + 1);
                        if (z > 0) Spread(i - width);
                        if (z < depth - 1) Spread(i + width);
                    }

                    void Spread(int next)
                    {
                        if (!IsOpen(next) || part[next] != 0) return;
                        part[next] = parts;
                        stack.Push(next);
                    }
                }

                int main = 1;
                for (int i = 2; i <= parts; i++) if (sizes[i] > sizes[main]) main = i;

                bool stranded = false;
                for (int i = 1; i <= parts; i++) if (i != main && sizes[i] > land * SmallestApart) stranded = true;
                if (!stranded) return;

                // Out from the main land through the rock, until something worth reaching is found. A breadth-first search finds the shortest way.
                int[] from = new int[cells];
                Array.Fill(from, -1);
                var queue = new Queue<int>();
                for (int i = 0; i < cells; i++)
                {
                    if (part[i] != main) continue;
                    from[i] = i;
                    queue.Enqueue(i);
                }

                int reached = -1;
                while (queue.Count > 0 && reached < 0)
                {
                    int i = queue.Dequeue();
                    int x = i % width;
                    int z = i / width;
                    if (x > 0) Walk(i, i - 1);
                    if (x < width - 1) Walk(i, i + 1);
                    if (z > 0) Walk(i, i - width);
                    if (z < depth - 1) Walk(i, i + width);
                }
                if (reached < 0) return;

                void Walk(int here, int next)
                {
                    if (from[next] >= 0 || reached >= 0 || ground[next] <= WorldPlan.SeaLevel + 0.2f) return;
                    from[next] = here;
                    if (part[next] != 0 && part[next] != main && sizes[part[next]] > land * SmallestApart) reached = next;
                    else if (part[next] == 0) queue.Enqueue(next);
                }

                for (int i = reached; from[i] != i; i = from[i]) Clear(massif, i % width, i / width);
            }

            bool IsOpen(int i) => data[i] < 0.5f && ground[i] > WorldPlan.SeaLevel + 0.2f;
        }

        private static void Clear(ScalarField massif, int centreX, int centreZ)
        {
            int reach = (int)MathF.Ceiling(PassRadius / massif.CellSize);
            for (int z = Math.Max(centreZ - reach, 0); z <= Math.Min(centreZ + reach, massif.Depth - 1); z++)
            {
                for (int x = Math.Max(centreX - reach, 0); x <= Math.Min(centreX + reach, massif.Width - 1); x++)
                {
                    float distance = new Vector2(x - centreX, z - centreZ).Length * massif.CellSize;
                    massif[x, z] = MathF.Min(massif[x, z], Smoothstep(PassRadius * 0.7f, PassRadius, distance));
                }
            }
        }

        private static void Raise(WorldData world, ScalarField massif, WorldNoise noise)
        {
            ScalarField elevation = world.Elevation;
            int width = elevation.Width;
            int depth = elevation.Depth;
            int cells = width * depth;
            float[] height = elevation.Data;
            float[] rock = massif.Data;
            float cell = WorldData.CoarseCell;

            Geology geology = world.Geology;
            Vector2 across = new Vector2(-MathF.Sin(geology.Strike), MathF.Cos(geology.Strike));

            // Hard rock stands steeper, so the land isn't one smooth cone. The harder bands run along the strike, which is what lines up a range.
            ScalarField uplift = ScalarField.ForWorld(world.Size, cell);
            uplift.Generate((x, z) =>
            {
                float patches = 0.62f + 0.76f * noise.Fbm01(x, z, 130f, 3);
                float line = x * across.X + z * across.Y + noise.Fbm(x, z, 240f, 2) * 90f;
                return patches * (1f + 0.32f * MathF.Sin(line * MathF.Tau / 170f));
            });

            // Something for water to follow on the first turn. It settles into its own shape from there.
            var ruffle = new WorldNoise(world.Random.Fork("mountain ruffle"));
            for (int i = 0; i < cells; i++)
            {
                if (rock[i] <= 0f) continue;
                float x = i % width * cell;
                float z = i / width * cell;
                height[i] += rock[i] * (ruffle.Ridged(x, z, 150f, 4) * 24f + ruffle.Fbm(x + 40f, z, 60f, 4) * 9f);
            }

            int[] receiver = new int[cells];
            int[] order = new int[cells];
            float[] area = new float[cells];
            float[] run = new float[cells];
            var queue = new CellQueue(cells / 4);

            int count = 0;
            for (int turn = 0; turn < Turns; turn++)
            {
                count = Route(height, rock, width, depth, cell, receiver, order, run, queue);

                Array.Clear(area);
                for (int n = count - 1; n >= 0; n--)
                {
                    int i = order[n];
                    area[i] += 1f;
                    int down = receiver[i];
                    if (down >= 0 && rock[down] > 0f) area[down] += area[i];
                }

                float settle = turn == Turns - 1 ? 1f : 0.75f;
                for (int n = 0; n < count; n++)
                {
                    int i = order[n];
                    float slope = SteepestSlope * uplift.Data[i] * Smoothstep(0f, 0.7f, rock[i]) * MathF.Pow(area[i], -AreaExponent);
                    float settled = height[receiver[i]] + run[i] * slope;
                    height[i] += (settled - height[i]) * settle;
                }
            }

            Settle(height, rock, width, depth);
            KeepRecord(world, area, order, count);
        }

        // Water here can only leave a cell by one of eight ways, so neighbours on an even slope carry unequal shares of it and settle at unequal heights: a comb, a cell wide.
        // That's the grid and not the land, and taking it out leaves every ridge where it is.
        private static void Settle(float[] height, float[] rock, int width, int depth)
        {
            float[] source = new float[height.Length];
            for (int pass = 0; pass < 2; pass++)
            {
                Array.Copy(height, source, height.Length);
                Parallel.For(1, depth - 1, z =>
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        int i = z * width + x;
                        if (rock[i] <= 0f) continue;
                        float around = source[i - 1] + source[i + 1] + source[i - width] + source[i + width];
                        float corners = source[i - width - 1] + source[i - width + 1] + source[i + width - 1] + source[i + width + 1];
                        height[i] = source[i] * 0.25f + around * 0.125f + corners * 0.0625f;
                    }
                });
            }
        }

        // Which cell water leaves each cell by, and an order that has every cell after the one it drains to. A priority flood from the mountains' edge
        // gives both at once and can't leave a pit: the lowest cell yet reached is taken next, so water finds a way out of a hollow over its lowest rim.
        private static int Route(float[] height, float[] rock, int width, int depth, float cell, int[] receiver, int[] order, float[] run, CellQueue queue)
        {
            int cells = width * depth;
            Array.Fill(receiver, -2);
            queue.Clear();

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = z * width + x;
                    if (rock[i] > 0f) continue;
                    bool edge = (x > 0 && rock[i - 1] > 0f) || (x < width - 1 && rock[i + 1] > 0f) || (z > 0 && rock[i - width] > 0f) || (z < depth - 1 && rock[i + width] > 0f)
                        || (x > 0 && z > 0 && rock[i - width - 1] > 0f) || (x < width - 1 && z > 0 && rock[i - width + 1] > 0f)
                        || (x > 0 && z < depth - 1 && rock[i + width - 1] > 0f) || (x < width - 1 && z < depth - 1 && rock[i + width + 1] > 0f);
                    receiver[i] = -1;
                    if (edge) queue.Push(height[i], i);
                }
            }

            float diagonal = cell * 1.41421356f;
            int count = 0;
            while (queue.Count > 0)
            {
                int i = queue.Pop();
                if (rock[i] > 0f) order[count++] = i;
                int x = i % width;
                int z = i / width;
                float here = height[i];

                if (x > 0) Reach(i - 1, cell);
                if (x < width - 1) Reach(i + 1, cell);
                if (z > 0) Reach(i - width, cell);
                if (z < depth - 1) Reach(i + width, cell);
                if (x > 0 && z > 0) Reach(i - width - 1, diagonal);
                if (x < width - 1 && z > 0) Reach(i - width + 1, diagonal);
                if (x > 0 && z < depth - 1) Reach(i + width - 1, diagonal);
                if (x < width - 1 && z < depth - 1) Reach(i + width + 1, diagonal);

                void Reach(int next, float distance)
                {
                    if (receiver[next] != -2) return;
                    receiver[next] = i;
                    run[next] = distance;
                    queue.Push(MathF.Max(height[next], here), next);
                }
            }
            return count;
        }

        // What the rest of generation wants to know: where water gathers, for plants and for the shape of the rock.
        private static void KeepRecord(WorldData world, float[] area, int[] order, int count)
        {
            ScalarField crests = ScalarField.ForWorld(world.Size, WorldData.CoarseCell);
            float[] flow = world.Flow.Data;
            float scale = 1f / MathF.Log(1f + 4096f);
            for (int n = 0; n < count; n++)
            {
                int i = order[n];
                float gathered = MathF.Log(1f + area[i]);
                flow[i] = MathF.Max(flow[i], gathered * scale);
                crests.Data[i] = 1f - 2f * Saturate(gathered / MathF.Log(90f));
            }
            crests.Blur(1, 1);
            world.Crests = crests;
        }

        /// <summary>A binary heap of cells by height, lowest first, and by index between equals: a seed has to make the same world on every machine.</summary>
        private sealed class CellQueue
        {
            private float[] _keys;
            private int[] _cells;

            public int Count { get; private set; }

            public CellQueue(int capacity)
            {
                _keys = new float[capacity];
                _cells = new int[capacity];
            }

            public void Clear() => Count = 0;

            public void Push(float key, int cell)
            {
                if (Count == _keys.Length)
                {
                    Array.Resize(ref _keys, Count * 2);
                    Array.Resize(ref _cells, Count * 2);
                }
                int i = Count++;
                while (i > 0)
                {
                    int parent = (i - 1) >> 1;
                    if (!Before(key, cell, _keys[parent], _cells[parent])) break;
                    _keys[i] = _keys[parent];
                    _cells[i] = _cells[parent];
                    i = parent;
                }
                _keys[i] = key;
                _cells[i] = cell;
            }

            public int Pop()
            {
                int top = _cells[0];
                float key = _keys[--Count];
                int cell = _cells[Count];
                int i = 0;
                while (true)
                {
                    int child = i * 2 + 1;
                    if (child >= Count) break;
                    if (child + 1 < Count && Before(_keys[child + 1], _cells[child + 1], _keys[child], _cells[child])) child++;
                    if (!Before(_keys[child], _cells[child], key, cell)) break;
                    _keys[i] = _keys[child];
                    _cells[i] = _cells[child];
                    i = child;
                }
                _keys[i] = key;
                _cells[i] = cell;
                return top;
            }

            private static bool Before(float keyA, int cellA, float keyB, int cellB) => keyA < keyB || (keyA == keyB && cellA < cellB);
        }
    }
}
