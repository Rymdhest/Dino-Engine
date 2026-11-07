using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Shapes the land. First a signed outline says what is land and what is water (positive is land), then the height
    /// follows from how far inland a point is: a beach or cliff at the shore rising into lowland, with hills and ridged
    /// mountains on top. Working from true distance to the shore, not from raw noise, keeps coasts walkable and gives
    /// every world the same readable structure of shore, lowland and heights.
    /// </summary>
    public sealed class LandformStage : IWorldStage
    {
        private const float ShoreWidth = 46f;       // Metres of beach before the land starts to rise.
        private const float SeaFloorSlope = 0.07f;
        private const float MaxSeaDepth = 26f;

        private readonly record struct Blob(Vector2 Centre, float Radius);

        public string Description => "Raising the land";
        public float Cost => 2f;

        public void Run(WorldData world)
        {
            WorldPlan plan = world.Plan;
            WorldRandom random = world.Random.Fork("landform");
            float size = plan.Size;

            var coastNoise = new WorldNoise(random.Fork("coast"));
            var reliefNoise = new WorldNoise(random.Fork("relief"));
            var mountainNoise = new WorldNoise(random.Fork("mountains"));
            Func<float, float, float> outline = BuildOutline(plan, random.Fork("outline"), coastNoise);

            ScalarField shape = ScalarField.ForWorld(size, WorldData.CoarseCell);
            shape.Generate(outline);

            int width = shape.Width;
            int depth = shape.Depth;
            float[] land = shape.Data;
            ScalarField inland = ScalarField.DistanceTo(width, depth, WorldData.CoarseCell, i => land[i] <= 0f);
            ScalarField offshore = ScalarField.DistanceTo(width, depth, WorldData.CoarseCell, i => land[i] > 0f);

            float rimHeight = plan.Archetype.RimHeight;
            float rimWidth = plan.Archetype.RimWidth;
            bool hasRim = rimHeight > 0f;
            bool marsh = plan.Archetype.Landform == LandformKind.Marsh;
            float mountainThreshold = 1f - plan.MountainCoverage * 1.6f;
            float terraceStep = 7f;

            ScalarField boundary = ScalarField.ForWorld(size, WorldData.CoarseCell);
            ScalarField highland = ScalarField.ForWorld(size, WorldData.CoarseCell);
            ScalarField elevation = ScalarField.ForWorld(size, WorldData.CoarseCell);
            Parallel.For(0, depth, z =>
            {
                float worldZ = z * WorldData.CoarseCell;
                for (int x = 0; x < width; x++)
                {
                    float worldX = x * WorldData.CoarseCell;
                    int i = z * width + x;

                    // Open water closes off its side of the world by itself.
                    float rim = hasRim && land[i] > 0f ? RimAt(worldX, worldZ, size, rimWidth, coastNoise) : 0f;
                    boundary.Data[i] = rim;

                    float height;
                    if (land[i] <= 0f)
                    {
                        height = -0.15f - MathF.Min(offshore.Data[i] * SeaFloorSlope, MaxSeaDepth);
                    }
                    else if (marsh)
                    {
                        height = MarshHeight(worldX, worldZ, reliefNoise);
                    }
                    else
                    {
                        height = LandHeight(plan, worldX, worldZ, inland.Data[i], reliefNoise, mountainNoise, coastNoise, mountainThreshold, out highland.Data[i]);
                        if (plan.Terraces > 0f) height = Terrace(height, terraceStep, plan.Terraces);
                    }

                    if (hasRim) height += MathF.Pow(rim, 2.2f) * rimHeight;
                    elevation.Data[i] = height;
                }
            });

            world.Elevation = elevation;
            world.Boundary = boundary;
            world.Highland = highland;
            // Refined by the erosion stage once the shape is final.
            world.Inland = inland;
        }

        // 0 inside the world, rising to 1 at its edge, along a wandering line.
        private static float RimAt(float x, float z, float size, float rimWidth, WorldNoise noise)
        {
            float border = MathF.Min(MathF.Min(x, size - x), MathF.Min(z, size - z));
            float wander = noise.Fbm(x + 900f, z - 400f, 240f, 3) * rimWidth * 0.55f;
            return 1f - Smoothstep(0f, rimWidth * 1.5f, border + wander - rimWidth * 0.1f);
        }

        private static float LandHeight(WorldPlan plan, float x, float z, float inland, WorldNoise relief, WorldNoise mountains, WorldNoise coast, float mountainThreshold, out float highland)
        {
            // Stretches of the coast rise straight up as cliffs; the rest are beaches.
            float cliffNoise = coast.Fbm01(x - 1300f, z + 700f, 300f, 3);
            float cliff = Smoothstep(1f - plan.Cliffs - 0.06f, 1f - plan.Cliffs + 0.06f, cliffNoise);

            float beach = 0.055f * MathF.Min(inland, ShoreWidth);
            float cliffFace = 11f * Smoothstep(0f, 16f, inland);
            float shore = Lerp(beach, MathF.Max(cliffFace, beach), cliff);

            float rising = Smoothstep(ShoreWidth * 0.5f, 300f, inland);
            float lowland = plan.BaseHeight * rising;

            float rolling = relief.Fbm(x, z, 260f, 5) * 0.5f + relief.Fbm(x + 77f, z - 31f, 95f, 3) * 0.14f;
            float hills = plan.HillHeight * (rolling + 0.25f) * Smoothstep(ShoreWidth * 0.4f, 170f, inland);

            Vector2 warp = mountains.Warp(x, z, 340f, 90f);
            float massif = mountains.Fbm01(x + 500f, z + 120f, 720f, 3);
            float mask = Smoothstep(mountainThreshold, mountainThreshold + 0.26f, massif) * Smoothstep(60f, 320f, inland);
            highland = massif * Smoothstep(60f, 320f, inland);
            float ridges = mountains.Ridged(x + warp.X, z + warp.Y, 430f, 6);
            float mountain = plan.MountainHeight * mask * (0.32f + 0.68f * ridges);

            return shore + lowland + hills + mountain;
        }

        // Barely above the water: pools fill whatever dips below zero.
        private static float MarshHeight(float x, float z, WorldNoise relief)
        {
            float broad = relief.Fbm(x, z, 300f, 4);
            float pools = relief.Fbm(x - 311f, z + 91f, 70f, 4);
            return 0.55f + broad * 1.6f + pools * 0.9f;
        }

        // Pulls heights towards flat steps with steep faces between, as in layered rock.
        private static float Terrace(float height, float step, float strength)
        {
            float level = height / step;
            float floor = MathF.Floor(level);
            float stepped = (floor + Smoothstep(0.62f, 0.86f, level - floor)) * step;
            return Lerp(height, stepped, strength);
        }

        private Func<float, float, float> BuildOutline(WorldPlan plan, WorldRandom random, WorldNoise noise)
        {
            float size = plan.Size;
            switch (plan.Archetype.Landform)
            {
                case LandformKind.Island:
                    return IslandOutline(size, BuildIslandBlobs(size, random, random.Int(3, 6), 0.27f, 0.36f, 0.24f), random, noise, barWidth: 30f);
                case LandformKind.Archipelago:
                    return IslandOutline(size, BuildArchipelagoBlobs(size, random), random, noise, barWidth: 16f);
                case LandformKind.Peninsula:
                    return PeninsulaOutline(size, random, noise);
                case LandformKind.Lakeland:
                    return LakeOutline(size, random, noise, count: random.Int(1, 2), radius: new FloatRange(0.2f, 0.27f), spread: 0.14f);
                case LandformKind.Vale:
                case LandformKind.Highlands:
                    return LakeOutline(size, random, noise, count: random.Int(1, 3), radius: new FloatRange(0.04f, 0.08f), spread: 0.3f);
                case LandformKind.Badlands:
                    return LakeOutline(size, random, noise, count: random.Int(0, 1), radius: new FloatRange(0.03f, 0.06f), spread: 0.3f);
                default:
                    return (x, z) => 1f;
            }
        }

        private static List<Blob> BuildIslandBlobs(float size, WorldRandom random, int count, float minRadius, float maxRadius, float spread)
        {
            var blobs = new List<Blob>(count);
            Vector2 centre = new Vector2(size * 0.5f);
            for (int i = 0; i < count; i++)
            {
                float angle = random.Float() * MathF.Tau;
                float distance = i == 0 ? 0f : random.Range(0.45f, 1f) * spread * size;
                blobs.Add(new Blob(
                    centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance,
                    random.Range(minRadius, maxRadius) * size * (i == 0 ? 1f : 0.72f)));
            }
            return blobs;
        }

        // Islands spread around the world, kept apart by rejecting spots that sit on top of an earlier island.
        private static List<Blob> BuildArchipelagoBlobs(float size, WorldRandom random)
        {
            int count = random.Int(4, 6);
            var blobs = new List<Blob>(count);
            for (int attempt = 0; attempt < 400 && blobs.Count < count; attempt++)
            {
                float radius = random.Range(0.12f, 0.2f) * size;
                float margin = radius + 0.09f * size;
                Vector2 position = new Vector2(random.Range(margin, size - margin), random.Range(margin, size - margin));

                bool clear = true;
                foreach (Blob other in blobs)
                {
                    float gap = (position - other.Centre).Length - radius - other.Radius;
                    if (gap < 0.03f * size) clear = false;
                }
                if (clear) blobs.Add(new Blob(position, radius));
            }
            return blobs;
        }

        private static Func<float, float, float> IslandOutline(float size, List<Blob> blobs, WorldRandom random, WorldNoise noise, float barWidth)
        {
            List<(Vector2 a, Vector2 b)> bars = BuildBars(blobs);
            float edgeMargin = 0.055f * size;
            float blend = 0.08f * size;
            float wobble = 0.1f * size;

            return (x, z) =>
            {
                Vector2 warp = noise.Warp(x, z, 0.3f * size, 0.08f * size, 4);
                Vector2 p = new Vector2(x, z) + warp;

                float field = float.NegativeInfinity;
                foreach (Blob blob in blobs)
                {
                    float value = blob.Radius - (p - blob.Centre).Length;
                    field = float.IsNegativeInfinity(field) ? value : SmoothMax(field, value, blend);
                }
                field += noise.Fbm(x + 41f, z - 83f, 0.14f * size, 5) * wobble;

                // Bars are added after the noise, so nothing can break the land's connection.
                Vector2 unwarped = new Vector2(x, z);
                foreach ((Vector2 a, Vector2 b) in bars)
                {
                    float wander = noise.Fbm(x - 73f, z + 311f, 120f, 2) * barWidth * 1.4f;
                    field = MathF.Max(field, barWidth + wander * 0.35f - DistanceToBar(unwarped, a, b, noise));
                }

                float border = MathF.Min(MathF.Min(x, size - x), MathF.Min(z, size - z));
                return MathF.Min(field, border - edgeMargin);
            };
        }

        private static float DistanceToBar(Vector2 p, Vector2 a, Vector2 b, WorldNoise noise)
        {
            float distance = DistanceToSegment(p, a, b, out float t);
            // Sand bars bow out from a straight line, most of all in the middle.
            float bow = noise.Fbm(a.X + t * 200f, b.Y + t * 170f, 300f, 2) * 60f * MathF.Sin(t * MathF.PI);
            return MathF.Abs(distance - MathF.Abs(bow) * 0.4f);
        }

        // A minimum spanning tree over the blobs: the shortest set of bars that joins all of them.
        private static List<(Vector2 a, Vector2 b)> BuildBars(List<Blob> blobs)
        {
            var bars = new List<(Vector2 a, Vector2 b)>();
            if (blobs.Count < 2) return bars;

            var joined = new List<int> { 0 };
            var remaining = new List<int>();
            for (int i = 1; i < blobs.Count; i++) remaining.Add(i);

            while (remaining.Count > 0)
            {
                float best = float.PositiveInfinity;
                int bestFrom = 0;
                int bestTo = 0;
                foreach (int from in joined)
                {
                    foreach (int to in remaining)
                    {
                        float gap = (blobs[from].Centre - blobs[to].Centre).Length - blobs[from].Radius - blobs[to].Radius;
                        if (gap < best)
                        {
                            best = gap;
                            bestFrom = from;
                            bestTo = to;
                        }
                    }
                }
                bars.Add((blobs[bestFrom].Centre, blobs[bestTo].Centre));
                joined.Add(bestTo);
                remaining.Remove(bestTo);
            }
            return bars;
        }

        private static Func<float, float, float> PeninsulaOutline(float size, WorldRandom random, WorldNoise noise)
        {
            float angle = random.Pick(new[] { 0f, 0.5f, 1f, 1.5f }) * MathF.PI + random.Range(-0.3f, 0.3f);
            Vector2 towardsSea = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            float coastline = size * random.Range(0.24f, 0.36f);
            Vector2 centre = new Vector2(size * 0.5f);

            return (x, z) =>
            {
                Vector2 p = new Vector2(x, z) - centre;
                float distanceToSeaSide = size * 0.5f - Vector2.Dot(p, towardsSea);
                float bays = noise.Fbm(x + 19f, z - 57f, 0.22f * size, 5) * 0.14f * size;
                return distanceToSeaSide - coastline + bays;
            };
        }

        // Land everywhere but for a few lakes.
        private static Func<float, float, float> LakeOutline(float size, WorldRandom random, WorldNoise noise, int count, FloatRange radius, float spread)
        {
            var lakes = new List<Blob>(count);
            for (int i = 0; i < count; i++)
            {
                Vector2 position = new Vector2(size * 0.5f) + new Vector2(random.Range(-spread, spread), random.Range(-spread, spread)) * size;
                lakes.Add(new Blob(position, random.Range(radius) * size));
            }

            return (x, z) =>
            {
                float field = size;
                Vector2 warp = noise.Warp(x, z, 0.2f * size, 0.06f * size, 4);
                Vector2 p = new Vector2(x, z) + warp;
                foreach (Blob lake in lakes)
                {
                    field = MathF.Min(field, (p - lake.Centre).Length - lake.Radius);
                }
                return field;
            };
        }
    }
}
