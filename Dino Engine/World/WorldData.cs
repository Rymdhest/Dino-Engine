using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>
    /// A generated world: every map, site, road and placed object. Stages of <see cref="Generation.WorldGenerator"/> fill it in
    /// order, and nothing here touches the GPU, so worlds can be generated on a background thread or in a test.
    /// World positions run from (0, 0) to (Size, Size) on the xz plane; sea level is y = 0.
    /// </summary>
    public sealed class WorldData
    {
        /// <summary>Metres between nodes of the maps terrain is shaped on.</summary>
        public const float CoarseCell = 4f;
        /// <summary>Metres between nodes of the climate and biome maps.</summary>
        public const float ClimateCell = 8f;
        /// <summary>Metres between nodes of the final height map.</summary>
        public const float DetailCell = 1f;
        /// <summary>Metres between nodes of the walkability map.</summary>
        public const float WalkCell = 2f;
        /// <summary>Metres between nodes of the passability map.</summary>
        public const float PassCell = 1f;
        /// <summary>Metres from a character's centre to its side, for what it can squeeze through.</summary>
        public const float CharacterRadius = 1.1f;
        /// <summary>Reads in <see cref="PondLevel"/> where there is no pond.</summary>
        public const float NoPond = -1e6f;
        /// <summary>Metres of water a character can wade through.</summary>
        public const float WadingDepth = 1.5f;
        /// <summary>Steepest ground a character can climb.</summary>
        public const float WalkableSlope = 0.85f;

        public readonly WorldPlan Plan;
        public readonly WorldRandom Random;

        public float Size => Plan.Size;

        // --- Shape, at CoarseCell ---
        /// <summary>Height of the land before roads and buildings shaped it.</summary>
        public ScalarField Elevation = null!;
        /// <summary>Metres to the nearest water, 0 at and under it.</summary>
        public ScalarField Inland = null!;
        /// <summary>How much water drains through each point, 0..1. High in gullies and valley floors.</summary>
        public ScalarField Flow = null!;
        /// <summary>1 inside the mountains closing off the world, where nothing can be built or walked.</summary>
        public ScalarField Boundary = null!;

        // --- Climate, at ClimateCell ---
        public ScalarField Temperature = null!;
        public ScalarField Moisture = null!;
        /// <summary>One map per biome of the plan; at every node they add up to 1.</summary>
        public ScalarField[] BiomeWeights = Array.Empty<ScalarField>();

        // --- Places, from SiteStage on ---
        /// <summary>1 on the land a character can walk to from the landing, at CoarseCell.</summary>
        public ScalarField Reachable = null!;
        /// <summary>Square metres of <see cref="Reachable"/> land.</summary>
        public float ReachableArea;
        /// <summary>Walking distance from the landing as a share of the longest walk, 0..1, at CoarseCell.</summary>
        public ScalarField Remoteness = null!;
        public readonly List<WorldSite> Sites = new();
        public readonly List<WorldRoad> Roads = new();
        /// <summary>Everywhere a pack has settled, apart from the sites. Camps are here from CampStage on, the rest from PackStage.</summary>
        public readonly List<WorldSite> Haunts = new();

        // --- What people built, from SettlementStage on ---
        public BuildingStyle Style;
        /// <summary>Every footprint in the world. What is solid here also blocks characters at run time.</summary>
        public PlacementIndex Placement = null!;
        public readonly List<PendingObject> Pending = new();
        public readonly List<WallRun> Walls = new();
        public readonly List<GroundPad> Pads = new();
        public readonly List<PaintPatch> Paint = new();
        public readonly List<Clearing> Clearings = new();

        // --- The final ground, from TerrainBakeStage on, at DetailCell ---
        /// <summary>The ground as it will be walked on and drawn.</summary>
        public ScalarField Height = null!;
        public ByteField PavedRoad = null!;
        public ByteField TrailRoad = null!;
        public ByteField Yard = null!;
        public ByteField Plaza = null!;
        public ByteField Farmland = null!;

        // --- Water and plants ---
        public readonly List<WorldPond> Ponds = new();
        /// <summary>Surface level of the pond at each CoarseCell node, or <see cref="NoPond"/>.</summary>
        public ScalarField PondLevel = null!;
        /// <summary>How thickly trees stand, 0..1, at CoarseCell.</summary>
        public ScalarField Canopy = null!;
        public WorldObjects Objects = null!;
        /// <summary>Ground a character can stand on, at <see cref="WalkCell"/>: slope and water, but not what stands on it.</summary>
        public ByteField Walkable = null!;
        /// <summary>Ground a character can get to on foot from the landing, around everything solid, at <see cref="PassCell"/>.</summary>
        public ByteField Passable = null!;

        /// <summary>Time each stage took, for the log and the WorldLab tool.</summary>
        public readonly List<(string stage, double milliseconds)> Timings = new();

        public WorldData(WorldPlan plan)
        {
            Plan = plan;
            Random = new WorldRandom(plan.Seed).Fork("world");
        }

        /// <summary>Height of the highest water surface over a point: a pond's, or else the sea's.</summary>
        public float WaterLevelAt(Vector2 position)
        {
            float pond = NoPond;
            if (PondLevel != null)
            {
                Vector2i node = PondLevel.NearestNode(position);
                pond = PondLevel[node.X, node.Y];
            }
            return MathF.Max(WorldPlan.SeaLevel, pond);
        }

        /// <summary>Metres of water over the ground at a point, 0 where it is dry.</summary>
        public float WaterDepthAt(Vector2 position)
        {
            return MathF.Max(WaterLevelAt(position) - Height.Sample(position), 0f);
        }

        /// <summary>
        /// What the ground is mostly made of at a point, as a foot would find it. Follows the rules of <see cref="TerrainSurface"/>, less its
        /// blending: what's painted there is a mix, and a sole lands on one thing.
        /// </summary>
        public SurfaceMaterial SurfaceAt(Vector2 position)
        {
            float x = position.X, z = position.Y;
            if (PavedRoad.Sample(x, z) > 0.5f) return SurfaceMaterial.Cobble;

            BiomeDefinition biome = BiomeAt(position);
            if (TrailRoad.Sample(x, z) > 0.5f) return biome.Path;
            if (Plaza.Sample(x, z) > 0.5f) return SurfaceMaterial.Gravel;
            if (Yard.Sample(x, z) > 0.5f) return SurfaceMaterial.Dirt;
            if (Farmland.Sample(x, z) > 0.5f) return SurfaceMaterial.Farmland;

            if (Height.Sample(position) - WaterLevelAt(position) < 1f) return biome.Shore;
            if (Height.SlopeAt(position) > 0.8f) return biome.Slope;
            return Canopy.Sample(x, z) > 0.5f ? biome.Understory : biome.Ground;
        }

        public bool InBounds(Vector2 position)
        {
            return position.X >= 0f && position.Y >= 0f && position.X <= Size && position.Y <= Size;
        }

        /// <summary>Index into <see cref="WorldPlan.Biomes"/> of the strongest biome at <paramref name="position"/>.</summary>
        public int DominantBiome(Vector2 position)
        {
            int best = 0;
            float bestWeight = -1f;
            for (int i = 0; i < BiomeWeights.Length; i++)
            {
                float weight = BiomeWeights[i].Sample(position);
                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    best = i;
                }
            }
            return best;
        }

        public BiomeDefinition BiomeAt(Vector2 position) => Plan.Biomes[DominantBiome(position)];
    }
}
