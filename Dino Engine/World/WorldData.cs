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
        /// <summary>Metres from a person's centre to their side, for the room a body keeps from another body and what a blow has to reach.</summary>
        public const float CharacterRadius = 1.1f;

        /// <summary>
        /// Metres less than how far it reaches that a body is to the things of the world: a person meets a cart or a wall with their shoulders and the arms hanging by them
        /// (BodyForm.ShoulderX and an arm, 0.8 m from their middle), and keeps a little more room than that from another person, as a crowd does. So a gap that looks wide
        /// enough to walk through is.
        /// </summary>
        public const float WorldSlack = 0.3f;

        /// <summary>Metres from a person's middle to their side as the things of the world meet them, which is what they squeeze between and passable ground is worked out for.</summary>
        public const float ShoulderRadius = CharacterRadius - WorldSlack;

        /// <summary>How far into the things of the world a body that reaches <paramref name="radius"/> from its middle goes: as much less than its reach as a person's, and never less than half of it.</summary>
        public static float WorldRadius(float radius) => MathF.Max(radius - WorldSlack, radius * 0.5f);
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
        /// <summary>How much the land here wants to be a mountain, 0..1. Foothills rise around it, and it's where <see cref="Massif"/> is drawn from.</summary>
        public ScalarField Highland = null!;
        /// <summary>1 where mountains stand, from MountainStage on: the wall around the world and whatever rises within it.</summary>
        public ScalarField Massif = null!;
        /// <summary>1 on the crest of a ridge and -1 down a valley, in the mountains, from MountainStage on.</summary>
        public ScalarField Crests = null!;
        /// <summary>The bedrock, from MountainStage on.</summary>
        public Geology Geology = null!;
        /// <summary>The ways water leaves the mountains, from MountainStage on, which RockSculpt cuts its ravines along.</summary>
        public Drainage Drainage = new();

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
        public readonly List<FenceRun> Fences = new();
        public readonly List<GroundPad> Pads = new();
        /// <summary>
        /// Ground built up with walls round it, as a raised bed, a terrace or a flight of steps is: laid after the roads, as their shoulders would otherwise pull it down
        /// towards them, and kept exactly, as the walls hide where it meets the rest. Read SQUARE.md.
        /// </summary>
        public readonly List<GroundPad> Terraces = new();
        public readonly List<PaintPatch> Paint = new();
        public readonly List<Clearing> Clearings = new();
        public readonly List<Planting> Plantings = new();
        /// <summary>The buildings that can be walked into. Read HALLS.md.</summary>
        public readonly List<WorldHall> Halls = new();

        // --- The final ground, from TerrainBakeStage on, at DetailCell ---
        /// <summary>
        /// The ground as it's walked on: where people stand, and what anything standing upright is set on, so it stands a little way into the drawn ground's relief. The ground is drawn
        /// <see cref="Rendering.Renderers.Geometry.TerrainRenderer.SurfaceOffset"/> higher (Terrain.vert), as the relief can only go down from what's drawn: something small that lies on the
        /// ground, a find, coins, a body, a broken piece, is half buried on this. Lay those on <see cref="DrawnGroundAt"/>.
        /// </summary>
        public ScalarField Height = null!;

        /// <summary>
        /// The top of the ground as Terrain.vert draws it, <see cref="Rendering.Renderers.Geometry.TerrainRenderer.SurfaceOffset"/> over the cubic height map: what anything lying on the ground
        /// rests on. Anything standing goes on <see cref="Height"/>, which is where feet are.
        /// </summary>
        public float DrawnGroundAt(float x, float z) => DrawnGround(Height, x, z);

        /// <inheritdoc cref="DrawnGroundAt"/>
        public static float DrawnGround(ScalarField height, float x, float z) => height.SampleCubic(x, z) + Rendering.Renderers.Geometry.TerrainRenderer.SurfaceOffset;
        public ByteField PavedRoad = null!;
        public ByteField TrailRoad = null!;
        public ByteField Yard = null!;
        public ByteField Plaza = null!;
        public ByteField Farmland = null!;
        /// <summary>Where the waygates' stone is laid as paving (GroundPaint.Paving), which nothing grows through and which every road running into it becomes.</summary>
        public ByteField Paving = null!;
        /// <summary>Whether anything here is paved, so that a world with none keeps the slot in its palette for something it has.</summary>
        public bool Paved;
        /// <summary>Beds someone tends (GroundPaint.Garden), whose grass is short, green and in flower whatever the land around grows.</summary>
        public ByteField Garden = null!;

        /// <summary>A churchyard's turf (GroundPaint.Sward), short, dark and in no flower, so that its stones show (CHURCHYARD.md).</summary>
        public ByteField Sward = null!;
        /// <summary>
        /// Whether what's paved here was laid by masons to a line, as a town's is: its cobbles and flags end where they were laid, at a kerb or a wall, and don't fray out into
        /// the grass as a camp's trodden ground does (TerrainSurface, HAVEN.md).
        /// </summary>
        public bool Trim;
        /// <summary>A town's turf (GroundPaint.Green), short, fresh and daisied, so that a town isn't waist deep in the meadow it stands in (HAVEN.md). Null in a world with none.</summary>
        public ByteField? Green;
        /// <summary>An arena's floor of sand, a pen's of mud and straw, and the ash of the Onslaught (GroundPaint.Sand, Mud and Ash), laid over whatever was there, which nothing grows through.</summary>
        public ByteField Sand = null!;
        public ByteField Mud = null!;
        public ByteField Ash = null!;
        /// <summary>A quarry's floor (GroundPaint.Chippings), its stone broken small over the bedrock, which nothing grows through either.</summary>
        public ByteField Chippings = null!;
        /// <summary>Which of those anything is laid with, so that a world with none keeps their slots in its palette for what it has, and doesn't read what it hasn't got.</summary>
        public bool Sanded, Mudded, Ashen, Chipped;
        /// <summary>1 on bare mountain, where nobody goes. Everything else is ground as it always was.</summary>
        public ByteField Rock = null!;
        /// <summary>1 where broken rock has gathered, down the creases of a mountain.</summary>
        public ByteField Scree = null!;
        /// <summary>How far bare rock stands out of what's around it: half is level, 1 a sharp crest and 0 the bottom of a crack.</summary>
        public ByteField Proud = null!;
        /// <summary>The rock in the land people walk on, its pavements and knolls (GroundRock), from TerrainBakeStage on.</summary>
        public GroundRock? GroundRock;
        /// <summary>Metres to the nearest bare rock, at CoarseCell, from TerrainBakeStage on: what's fallen off a wall lies near its foot.</summary>
        public ScalarField RockNear = null!;

        // --- Water and plants ---
        public readonly List<WorldPond> Ponds = new();
        /// <summary>Surface level of the pond at each CoarseCell node, or <see cref="NoPond"/>.</summary>
        public ScalarField PondLevel = null!;
        /// <summary>The level of the nearest pond, at CoarseCell, out past its rectangle as far as its shore can reach, or <see cref="NoPond"/>: read through <see cref="ShoreHeightAt"/>.</summary>
        public ScalarField ShoreLevel = null!;
        /// <summary>How much a shore reads a pond's level rather than the sea's, 1 near its water and 0 well away from it, at CoarseCell.</summary>
        public ScalarField PondShore = null!;
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

        /// <summary>
        /// How far a point stands above the water's edge, as a shore, wet sand or a tree's roots would have it: above the sea, or near a pond's water above the pond, fading from one to the other
        /// over a few metres. Not <see cref="WaterLevelAt"/>, which holds a pond's level over the whole rectangle round it, and laid every shore in one with a straight edge.
        /// </summary>
        public float ShoreHeightAt(Vector2 position)
        {
            float height = Height.Sample(position);
            // Under water is under water: nothing in a pond's rectangle but the pond lies below its level, however little of it the coarse grid caught.
            float under = height - WaterLevelAt(position);
            if (under < 0f || PondShore == null) return under;
            float near = PondShore.Sample(position);
            if (near <= 0f) return height - WorldPlan.SeaLevel;
            Vector2i node = ShoreLevel.NearestNode(position);
            float pond = MathF.Max(ShoreLevel[node.X, node.Y], WorldPlan.SeaLevel);
            return height - (WorldPlan.SeaLevel + (pond - WorldPlan.SeaLevel) * near);
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
            if (HallAt(position) is WorldHall hall) return hall.Design.Floor;

            float x = position.X, z = position.Y;
            if (Paved && Paving.Sample(x, z) > 0.5f) return SurfaceMaterial.Flagstones;
            if (Sanded && Sand.Sample(x, z) > 0.5f) return SurfaceMaterial.Sand;
            if (Mudded && Mud.Sample(x, z) > 0.5f) return SurfaceMaterial.Mud;
            if (Ashen && Ash.Sample(x, z) > 0.5f) return SurfaceMaterial.Ash;
            if (Chipped && Chippings.Sample(x, z) > 0.5f) return SurfaceMaterial.Gravel;
            if (PavedRoad.Sample(x, z) > 0.5f) return SurfaceMaterial.Cobble;

            BiomeDefinition biome = BiomeAt(position);
            if (TrailRoad.Sample(x, z) > 0.5f) return biome.Path;
            if (Plaza.Sample(x, z) > 0.5f) return SurfaceMaterial.Gravel;
            if (Yard.Sample(x, z) > 0.5f) return SurfaceMaterial.Dirt;
            if (Farmland.Sample(x, z) > 0.5f) return SurfaceMaterial.Farmland;
            // Bedrock showing through, as TerrainSurface paints it.
            if (GroundRock != null && GroundRock.Pavement(x, z) > 0.5f) return SurfaceMaterial.Rock;

            if (ShoreHeightAt(position) < 1f) return biome.Shore;
            if (Height.SlopeAt(position) > 0.8f) return biome.Slope;
            return Canopy.Sample(x, z) > 0.5f ? biome.Understory : biome.Ground;
        }

        /// <summary>The hall whose floor a point is on, if it's in one: inside its walls, grown by <paramref name="grow"/> metres.</summary>
        public WorldHall? HallAt(Vector2 position, float grow = 0f)
        {
            foreach (WorldHall hall in Halls)
            {
                if (hall.Contains(position, grow)) return hall;
            }
            return null;
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

        /// <summary>
        /// How a site builds: as the land it stands in decides, where its biome does (<see cref="BiomeDefinition.Builds"/>), and otherwise as the world's people do
        /// (<see cref="Style"/>), so that one world's villages can be logs up in its pinewoods and tiles down in its cypress hills. The Haven builds as its halls do.
        /// </summary>
        public BuildingStyle StyleOf(WorldSite site)
        {
            if (Plan.Archetype.Peaceful || site.Biome < 0 || site.Biome >= Plan.Biomes.Count) return Style;
            return Plan.Biomes[site.Biome].Builds ?? Style;
        }
    }
}
