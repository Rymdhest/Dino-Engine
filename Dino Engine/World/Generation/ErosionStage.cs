using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Weathers the land with water droplets (after Hans Theobald Beyer): each one runs downhill, picks up sediment where it
    /// speeds up and drops it where it slows down. That cuts the gullies and fans that noise alone never makes, and the
    /// paths the droplets took become the <see cref="WorldData.Flow"/> map that climate and vegetation read: as in Horizon
    /// Zero Dawn, plants follow where water drains.
    /// </summary>
    public sealed class ErosionStage : IWorldStage
    {
        private const float DropletsPerCell = 0.65f;
        private const int Lifetime = 42;
        private const int BrushRadius = 2;
        private const float Inertia = 0.06f;
        private const float CapacityFactor = 3.5f;
        private const float MinCapacity = 0.012f;
        private const float ErodeRate = 0.28f;
        private const float DepositRate = 0.28f;
        private const float Evaporation = 0.02f;
        private const float Gravity = 4f;

        private readonly record struct BrushCell(int X, int Z, float Weight);

        public string Description => "Weathering the mountains";
        public float Cost => 3f;

        public void Run(WorldData world)
        {
            ScalarField height = world.Elevation;
            ScalarField flow = ScalarField.ForWorld(world.Size, WorldData.CoarseCell);
            WorldRandom random = world.Random.Fork("erosion");
            float strength = world.Plan.Erosion;

            BrushCell[] brush = BuildBrush();
            int width = height.Width;
            int depth = height.Depth;
            int droplets = (int)(width * depth * DropletsPerCell);

            for (int droplet = 0; droplet < droplets; droplet++)
            {
                RunDroplet(height, flow, brush, random, strength);
            }

            // One droplet's trail is noisy; what matters is where trails gather.
            flow.Blur(1, 2);
            float[] data = flow.Data;
            float peak = 1f;
            for (int i = 0; i < data.Length; i++) peak = MathF.Max(peak, data[i]);
            float scale = 1f / MathF.Log(1f + peak);
            for (int i = 0; i < data.Length; i++) data[i] = MathF.Log(1f + data[i]) * scale;

            // The droplets leave cell-sized pits behind; this rounds them off without touching the larger forms.
            SoftenPits(height);

            world.Flow = flow;
            world.Inland = ScalarField.DistanceTo(width, depth, WorldData.CoarseCell, i => height.Data[i] <= WorldPlan.SeaLevel);
        }

        private static void RunDroplet(ScalarField height, ScalarField flow, BrushCell[] brush, WorldRandom random, float strength)
        {
            int width = height.Width;
            int depth = height.Depth;
            float[] map = height.Data;

            float x = random.Float() * (width - 3) + 1f;
            float z = random.Float() * (depth - 3) + 1f;

            // Sea level stops the droplets, so the sea floor keeps its smooth shelf.
            if (Lookup(map, width, x, z, out _, out _) <= WorldPlan.SeaLevel) return;

            float directionX = 0f;
            float directionZ = 0f;
            float speed = 1f;
            float water = 1f;
            float sediment = 0f;

            for (int step = 0; step < Lifetime; step++)
            {
                int cellX = (int)x;
                int cellZ = (int)z;
                float currentHeight = Lookup(map, width, x, z, out float gradientX, out float gradientZ);

                directionX = directionX * Inertia - gradientX * (1f - Inertia);
                directionZ = directionZ * Inertia - gradientZ * (1f - Inertia);
                float length = MathF.Sqrt(directionX * directionX + directionZ * directionZ);
                if (length < 1e-6f) break;
                directionX /= length;
                directionZ /= length;

                float nextX = x + directionX;
                float nextZ = z + directionZ;
                if (nextX < 1f || nextZ < 1f || nextX >= width - 2 || nextZ >= depth - 2) break;

                float nextHeight = Lookup(map, width, nextX, nextZ, out _, out _);
                float drop = currentHeight - nextHeight;
                flow.Data[cellZ * width + cellX] += water;

                if (nextHeight <= WorldPlan.SeaLevel)
                {
                    Deposit(map, width, cellX, cellZ, x - cellX, z - cellZ, sediment * 0.5f);
                    break;
                }

                float capacity = MathF.Max(drop, MinCapacity) * speed * water * CapacityFactor;
                if (sediment > capacity || drop < 0f)
                {
                    // Uphill means the droplet fills the hollow it has run into.
                    float amount = drop < 0f ? MathF.Min(-drop, sediment) : (sediment - capacity) * DepositRate;
                    sediment -= amount;
                    Deposit(map, width, cellX, cellZ, x - cellX, z - cellZ, amount);
                }
                else
                {
                    float amount = MathF.Min((capacity - sediment) * ErodeRate, drop) * strength;
                    for (int i = 0; i < brush.Length; i++)
                    {
                        BrushCell cell = brush[i];
                        int bx = cellX + cell.X;
                        int bz = cellZ + cell.Z;
                        if (bx < 0 || bz < 0 || bx >= width || bz >= depth) continue;
                        map[bz * width + bx] -= amount * cell.Weight;
                    }
                    sediment += amount;
                }

                speed = MathF.Sqrt(MathF.Max(speed * speed + drop * Gravity, 0f));
                water *= 1f - Evaporation;
                x = nextX;
                z = nextZ;
            }
        }

        private static void Deposit(float[] map, int width, int cellX, int cellZ, float tx, float tz, float amount)
        {
            int i = cellZ * width + cellX;
            map[i] += amount * (1f - tx) * (1f - tz);
            map[i + 1] += amount * tx * (1f - tz);
            map[i + width] += amount * (1f - tx) * tz;
            map[i + width + 1] += amount * tx * tz;
        }

        private static float Lookup(float[] map, int width, float x, float z, out float gradientX, out float gradientZ)
        {
            int cellX = (int)x;
            int cellZ = (int)z;
            float tx = x - cellX;
            float tz = z - cellZ;
            int i = cellZ * width + cellX;
            float h00 = map[i];
            float h10 = map[i + 1];
            float h01 = map[i + width];
            float h11 = map[i + width + 1];
            gradientX = (h10 - h00) * (1f - tz) + (h11 - h01) * tz;
            gradientZ = (h01 - h00) * (1f - tx) + (h11 - h10) * tx;
            return h00 * (1f - tx) * (1f - tz) + h10 * tx * (1f - tz) + h01 * (1f - tx) * tz + h11 * tx * tz;
        }

        private static BrushCell[] BuildBrush()
        {
            var cells = new List<BrushCell>();
            float total = 0f;
            for (int z = -BrushRadius; z <= BrushRadius; z++)
            {
                for (int x = -BrushRadius; x <= BrushRadius; x++)
                {
                    float distance = MathF.Sqrt(x * x + z * z);
                    if (distance > BrushRadius) continue;
                    float weight = 1f - distance / (BrushRadius + 0.5f);
                    cells.Add(new BrushCell(x, z, weight));
                    total += weight;
                }
            }
            for (int i = 0; i < cells.Count; i++) cells[i] = cells[i] with { Weight = cells[i].Weight / total };
            return cells.ToArray();
        }

        private static void SoftenPits(ScalarField height)
        {
            int width = height.Width;
            int depth = height.Depth;
            float[] source = (float[])height.Data.Clone();
            Parallel.For(1, depth - 1, z =>
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int i = z * width + x;
                    float average = (source[i - 1] + source[i + 1] + source[i - width] + source[i + width]) * 0.25f;
                    height.Data[i] = source[i] * 0.5f + average * 0.5f;
                }
            });
        }
    }
}
