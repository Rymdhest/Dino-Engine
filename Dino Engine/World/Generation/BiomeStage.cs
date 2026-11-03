using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Divides the land between the world's biomes. Each biome scores a place by how close the climate is to the one it likes
    /// (a Whittaker diagram with soft edges), plus its taste for height and coast. The scores are biased until every biome
    /// holds about the share of the land the plan gave it, so no biome a world promises ends up as a sliver. Stored as
    /// blend weights rather than one biome per cell: everything drawn from them fades across a border instead of stopping at it.
    /// </summary>
    public sealed class BiomeStage : IWorldStage
    {
        private const float ClimateWidth = 0.3f;    // How far from its ideal climate a biome's score falls off.
        private const float BlendSharpness = 15f;   // Softmax steepness; higher makes borders narrower.
        private const int BalanceIterations = 14;

        public string Description => "Sowing the biomes";
        public float Cost => 1f;

        public void Run(WorldData world)
        {
            IReadOnlyList<BiomeDefinition> biomes = world.Plan.Biomes;
            int count = biomes.Count;
            ScalarField temperature = world.Temperature;
            ScalarField moisture = world.Moisture;
            int width = temperature.Width;
            int depth = temperature.Depth;
            int cells = width * depth;

            Vector2[] anchors = StretchAnchors(biomes);
            float[] altitude = RankAltitude(world);
            var noise = new WorldNoise(world.Random.Fork("biomes"));

            // Scores without bias, one row per biome, computed once and reused by every balancing pass.
            float[][] baseScore = new float[count][];
            for (int b = 0; b < count; b++) baseScore[b] = new float[cells];
            bool[] land = new bool[cells];

            Parallel.For(0, depth, z =>
            {
                for (int x = 0; x < width; x++)
                {
                    int i = z * width + x;
                    float worldX = x * WorldData.ClimateCell;
                    float worldZ = z * WorldData.ClimateCell;
                    land[i] = world.Elevation.Sample(worldX, worldZ) > WorldPlan.SeaLevel;

                    // A wandering offset gives borders the shape of living things rather than of the climate's smooth contours.
                    Vector2 wander = noise.Warp(worldX, worldZ, 170f, 0.1f, 4);
                    float t = temperature.Data[i] + wander.X;
                    float m = moisture.Data[i] + wander.Y;
                    float coast = MathF.Exp(-world.Inland.Sample(worldX, worldZ) / 110f);

                    for (int b = 0; b < count; b++)
                    {
                        BiomeDefinition biome = biomes[b];
                        float dt = (t - anchors[b].X) / ClimateWidth;
                        float dm = (m - anchors[b].Y) / ClimateWidth;
                        float score = -(dt * dt + dm * dm);
                        score += biome.Altitude * (altitude[i] - 0.5f) * 5f;
                        score += biome.Coastal * coast * 3f;
                        baseScore[b][i] = score;
                    }
                }
            });

            float[] bias = Balance(baseScore, land, world.Plan.BiomeShares);

            ScalarField[] weights = new ScalarField[count];
            for (int b = 0; b < count; b++) weights[b] = ScalarField.ForWorld(world.Size, WorldData.ClimateCell);

            Parallel.For(0, cells, i =>
            {
                float peak = float.NegativeInfinity;
                for (int b = 0; b < count; b++) peak = MathF.Max(peak, baseScore[b][i] + bias[b]);
                float total = 0f;
                for (int b = 0; b < count; b++)
                {
                    float weight = MathF.Exp((baseScore[b][i] + bias[b] - peak) * BlendSharpness);
                    weights[b].Data[i] = weight;
                    total += weight;
                }
                float inverse = 1f / total;
                for (int b = 0; b < count; b++) weights[b].Data[i] *= inverse;
            });

            foreach (ScalarField field in weights) field.Blur(1, 2);
            world.BiomeWeights = weights;
        }

        // Moves the biomes' ideal climates apart until they span the range, since the land spans all of it.
        private static Vector2[] StretchAnchors(IReadOnlyList<BiomeDefinition> biomes)
        {
            float minT = float.PositiveInfinity, maxT = float.NegativeInfinity;
            float minM = float.PositiveInfinity, maxM = float.NegativeInfinity;
            foreach (BiomeDefinition biome in biomes)
            {
                minT = MathF.Min(minT, biome.Temperature);
                maxT = MathF.Max(maxT, biome.Temperature);
                minM = MathF.Min(minM, biome.Moisture);
                maxM = MathF.Max(maxM, biome.Moisture);
            }

            var anchors = new Vector2[biomes.Count];
            for (int i = 0; i < anchors.Length; i++)
            {
                float t = maxT - minT > 0.02f ? (biomes[i].Temperature - minT) / (maxT - minT) : 0.5f;
                float m = maxM - minM > 0.02f ? (biomes[i].Moisture - minM) / (maxM - minM) : 0.5f;
                anchors[i] = new Vector2(0.14f + 0.72f * t, 0.14f + 0.72f * m);
            }
            return anchors;
        }

        // Height of every climate cell as a rank among the land, 0 lowest and 1 highest.
        private static float[] RankAltitude(WorldData world)
        {
            ScalarField climate = world.Temperature;
            int cells = climate.Width * climate.Depth;
            float[] heights = new float[cells];
            var land = new List<float>(cells);
            for (int z = 0; z < climate.Depth; z++)
            {
                for (int x = 0; x < climate.Width; x++)
                {
                    float height = world.Elevation.Sample(x * climate.CellSize, z * climate.CellSize);
                    heights[z * climate.Width + x] = height;
                    if (height > WorldPlan.SeaLevel) land.Add(height);
                }
            }
            if (land.Count == 0) return heights;

            land.Sort();
            float[] ranks = new float[cells];
            for (int i = 0; i < cells; i++)
            {
                int index = land.BinarySearch(heights[i]);
                if (index < 0) index = ~index;
                ranks[i] = (float)index / land.Count;
            }
            return ranks;
        }

        // Finds biases that give each biome about its share of the land.
        private static float[] Balance(float[][] score, bool[] land, IReadOnlyList<float> shares)
        {
            int count = score.Length;
            float[] bias = new float[count];
            if (count < 2) return bias;

            int[] area = new int[count];
            for (int iteration = 0; iteration < BalanceIterations; iteration++)
            {
                Array.Clear(area);
                int total = 0;
                for (int i = 0; i < land.Length; i++)
                {
                    if (!land[i]) continue;
                    int best = 0;
                    float bestScore = float.NegativeInfinity;
                    for (int b = 0; b < count; b++)
                    {
                        float value = score[b][i] + bias[b];
                        if (value > bestScore)
                        {
                            bestScore = value;
                            best = b;
                        }
                    }
                    area[best]++;
                    total++;
                }
                if (total == 0) return bias;

                for (int b = 0; b < count; b++)
                {
                    float share = MathF.Max((float)area[b] / total, 0.002f);
                    bias[b] += Math.Clamp(MathF.Log(shares[b] / share) * 0.22f, -0.5f, 0.5f);
                }
            }
            return bias;
        }
    }
}
