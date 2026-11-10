using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Wildlife;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Content
{
    /// <summary>Ground materials a world can paint with. The renderer maps them to textures.</summary>
    public enum SurfaceMaterial : byte
    {
        Soil,
        Grass,
        GrassySoil,
        Rock,
        Sand,
        Dunes,
        CrackedEarth,
        Ash,
        Snow,
        Ice,
        Mud,
        Moss,
        Gravel,
        Cobble,
        Dirt,
        Farmland,
        Planks,     // A wooden floor, as a hall's: never on the ground outside.
        Setts,      // A village street's dressed stones between kerbs, laid across it: read along the road (RoadFrames), so never anywhere else.
        Road,       // A dirt road's packed earth and trodden stones, with ruts: read along the road, so never anywhere else.
        Flagstones, // The waygates' own stone laid in slabs, where it's paved (GroundPaint.Paving).
    }

    /// <summary>Small things drifting through the air of a biome.</summary>
    public enum AmbientMotes
    {
        None,
        Pollen,
        Petals,
        FallingLeaves,
        Snow,
        Embers,
        Fireflies,
        Dust,
        Wisps,
    }

    /// <summary>One entry of a biome's species list: an object and how common it is against the others in that list.</summary>
    public readonly struct Species
    {
        public readonly WorldAsset Asset;
        public readonly float Weight;

        public Species(WorldAsset asset, float weight = 1f)
        {
            Asset = asset;
            Weight = weight;
        }
    }

    /// <summary>
    /// A biome, in the sense of the "ecotope" of Horizon Zero Dawn's placement system: one description of a kind of place
    /// that decides the ground, the grass, the forest structure, the undergrowth, the air and the life in it. World
    /// generation only reads these, so a new biome is a new entry in <see cref="BiomeCatalog"/>, not new code.
    /// </summary>
    public sealed class BiomeDefinition
    {
        public string Id = "";
        public string Name = "";
        public Vector3 MapColour;

        // --- Where it occurs. Temperature and moisture are 0..1 within a world, whose climate is stretched to fill that range. ---
        public float Temperature = 0.5f;
        public float Moisture = 0.5f;
        public float Altitude = 0f;         // 0 anywhere; towards 1 it prefers the heights, towards -1 the low ground.
        public float Coastal = 0f;          // Above 0 it prefers land near water.

        // --- Ground ---
        public SurfaceMaterial Ground = SurfaceMaterial.Grass;
        public SurfaceMaterial Understory = SurfaceMaterial.Soil;   // Under trees, where little light reaches the ground.
        public SurfaceMaterial Slope = SurfaceMaterial.Rock;
        public SurfaceMaterial Shore = SurfaceMaterial.Sand;
        public SurfaceMaterial Path = SurfaceMaterial.Dirt;

        /// <summary>What the land here stands on, and so what a world of this biome is likely to be made of.</summary>
        public RockKind Bedrock = RockKind.Granite;

        // --- Grass ---
        public float GrassCover = 1f;       // 0..1, how much of the open ground grows grass.
        public float GrassHeight = 0.6f;    // 0..1 of the world's longest blades.
        public Vector3 GrassColour = new Vector3(0.13f, 0.2f, 0.05f);
        public float GrassDryness = 0f;     // 0..1, how much of it has turned to straw.
        public float GrassPlumes = 0f;      // 0..1, share of its tufts carrying seed heads.
        public float GrassBloom = 0f;       // 0..1, share of its blades that are flowers, where flowers grow.
        public Vector3 GrassBloomColour = new Vector3(0.512f, 0.512f, 0.512f);    // Flowers grow in drifts of this colour
        public Vector3 GrassBloomColourTwo = new Vector3(0.512f, 0.512f, 0.512f); // and of this one.

        // --- Forest structure ---
        public float ForestCover = 0.3f;    // 0..1, share of the biome's land covered by forest.
        public float ForestPatchSize = 170f;// Metres, size of woods and clearings.
        public float TreeSpacing = 1.15f;   // Multiplier on tree footprints: below 1 crowns interlock, above it they stand apart.
        public float LoneTrees = 0.05f;     // 0..1, trees standing alone out in the open.

        public Species[] Canopy = Array.Empty<Species>();
        public Species[] Edge = Array.Empty<Species>();         // Young trees and shrubs at forest edges. Empty: the canopy species.
        public Species[] Shrubs = Array.Empty<Species>();
        public Species[] Deadwood = Array.Empty<Species>();
        public Species[] Boulders = Array.Empty<Species>();
        public Species[] ForestFloor = Array.Empty<Species>();  // Small plants in the shade.
        public Species[] OpenGround = Array.Empty<Species>();   // Small plants in the open.
        public Species[] Waterside = Array.Empty<Species>();    // Small plants at the water's edge.

        public float ShrubDensity = 0.5f;
        public float DeadwoodDensity = 0.2f;
        public float BoulderDensity = 0.3f;
        public float FloorDensity = 0.5f;
        public float OpenDensity = 0.2f;

        // --- Air ---
        public float FogDensity = 0.02f;
        public Vector3 FogColour = new Vector3(0.75f, 0.8f, 0.85f);
        public float WindStrength = 1f;     // Multiplier on the world's wind.
        public AmbientMotes Motes = AmbientMotes.None;
        public Vector3? MoteColour;         // What it's falling from, if that's not the usual: the leaves of a ginkgo wood are gold.

        /// <summary>What lives here, whichever parts of it suit each. None of it has a say in how the land grows.</summary>
        public Creature[] Life = Array.Empty<Creature>();
    }

    /// <summary>Every biome worlds can be built from.</summary>
    public static class BiomeCatalog
    {
        private static Species[] List(params Species[] species) => species;
        private static Species S(WorldAsset asset, float weight = 1f) => new Species(asset, weight);

        public static readonly BiomeDefinition Meadow = new BiomeDefinition
        {
            Id = "meadow",
            Life = Fauna.Temperate,
            Bedrock = RockKind.Limestone,
            Name = "Meadowland",
            MapColour = new Vector3(0.46f, 0.62f, 0.26f),
            Temperature = 0.55f,
            Moisture = 0.5f,
            Ground = SurfaceMaterial.Grass,
            GrassCover = 1f,
            GrassHeight = 0.7f,
            GrassColour = new Vector3(0.17f, 0.3f, 0.055f),
            GrassDryness = 0.12f,
            GrassPlumes = 0.05f,
            GrassBloom = 0.1f,
            GrassBloomColour = new Vector3(0.556f, 0.367f, 0.019f),
            GrassBloomColourTwo = new Vector3(0.529f, 0.529f, 0.556f),
            ForestCover = 0.2f,
            ForestPatchSize = 120f,
            LoneTrees = 0.12f,
            Canopy = List(S(Broadleaves.Oak, 3f), S(Broadleaves.Beech, 1f), S(Broadleaves.Birch, 1f), S(Broadleaves.AncientOak, 0.35f)),
            Shrubs = List(S(Undergrowth.BroadShrub, 3f), S(Undergrowth.Boxwood, 1f), S(Undergrowth.Azalea, 0.6f)),
            Deadwood = List(S(Wilds.OakStump, 2f), S(Wilds.OakLog, 1f)),
            Boulders = List(S(NatureCatalog.BrownRock, 1f), S(NatureCatalog.Boulder, 1f)),
            ForestFloor = List(S(Undergrowth.LadyFernPlant, 3f), S(Undergrowth.BrackenPlant, 2f), S(Undergrowth.Bluebell, 2f), S(Undergrowth.BrownCaps, 1f), S(Broadleaves.OakLeaves, 2f)),
            OpenGround = List(S(Undergrowth.YellowFlower), S(Undergrowth.WhiteDaisy), S(Undergrowth.RedPoppy, 0.6f), S(Undergrowth.PurpleAster, 0.6f), S(Undergrowth.BlueLupine, 0.5f), S(Undergrowth.Yarrow, 0.6f), S(Undergrowth.Sedge, 0.5f)),
            Waterside = List(S(Undergrowth.Reeds, 2f), S(Undergrowth.Bulrush, 1f)),
            OpenDensity = 0.3f,
            Motes = AmbientMotes.Pollen,
        };

        public static readonly BiomeDefinition GoldenSteppe = new BiomeDefinition
        {
            Id = "steppe",
            Life = Fauna.Grassland,
            Bedrock = RockKind.Limestone,
            Name = "Golden Steppe",
            MapColour = new Vector3(0.78f, 0.63f, 0.28f),
            Temperature = 0.72f,
            Moisture = 0.22f,
            Ground = SurfaceMaterial.GrassySoil,
            Understory = SurfaceMaterial.GrassySoil,
            GrassCover = 0.95f,
            GrassHeight = 0.85f,
            GrassColour = new Vector3(0.38f, 0.24f, 0.05f),
            GrassDryness = 0.35f,
            GrassPlumes = 0.3f,
            GrassBloom = 0.012f,
            GrassBloomColour = new Vector3(0.512f, 0.245f, 0.025f),
            GrassBloomColourTwo = new Vector3(0.512f, 0.47f, 0.351f),
            ForestCover = 0.04f,
            ForestPatchSize = 90f,
            LoneTrees = 0.1f,
            Canopy = List(S(Broadleaves.Olive, 2f), S(Broadleaves.Oak, 1f), S(Broadleaves.AncientOak, 0.3f)),
            Shrubs = List(S(Undergrowth.Sagebrush, 3f), S(Undergrowth.Gorse, 1f)),
            Deadwood = List(S(Wilds.SilverStump, 2f), S(Wilds.SilverLog, 1f)),
            Boulders = List(S(NatureCatalog.BrownRock)),
            OpenGround = List(S(Undergrowth.PampasGrass, 3f), S(Undergrowth.SilverGrass, 2f), S(Undergrowth.YellowFlower, 0.3f), S(Undergrowth.Yarrow, 0.3f)),
            Waterside = List(S(Undergrowth.Reeds)),
            ShrubDensity = 0.25f,
            OpenDensity = 0.3f,
            WindStrength = 1.5f,
            FogDensity = 0.012f,
            FogColour = new Vector3(0.9f, 0.82f, 0.65f),
            Motes = AmbientMotes.Dust,
        };

        public static readonly BiomeDefinition PineForest = new BiomeDefinition
        {
            Id = "pine",
            Life = Fauna.Conifer,
            Name = "Pinewood",
            MapColour = new Vector3(0.14f, 0.32f, 0.2f),
            Temperature = 0.35f,
            Moisture = 0.62f,
            Ground = SurfaceMaterial.GrassySoil,
            Understory = SurfaceMaterial.Moss,
            GrassCover = 0.75f,
            GrassHeight = 0.5f,
            GrassColour = new Vector3(0.09f, 0.19f, 0.07f),
            GrassPlumes = 0.02f,
            GrassBloom = 0.012f,
            GrassBloomColour = new Vector3(0.512f, 0.512f, 0.556f),
            GrassBloomColourTwo = new Vector3(0.351f, 0.39f, 0.556f),
            ForestCover = 0.86f,
            ForestPatchSize = 240f,
            TreeSpacing = 1.05f,
            Canopy = List(S(Conifers.ScotsPine, 4f), S(Conifers.BlackPine, 2f), S(Conifers.Spruce, 2f)),
            Edge = List(S(Conifers.Spruce, 2f), S(Conifers.BlackPine, 1f)),
            Shrubs = List(S(Undergrowth.Juniper, 2f), S(Undergrowth.BroadShrub, 1f), S(Undergrowth.Boxwood, 1f)),
            Deadwood = List(S(Wilds.PineLog, 2f), S(Wilds.PineStump, 2f), S(Conifers.DeadPine, 1f)),
            Boulders = List(S(NatureCatalog.Boulder)),
            ForestFloor = List(S(Undergrowth.SwordFernPlant, 3f), S(Undergrowth.BrackenPlant, 2f), S(Undergrowth.MossCushion, 2f), S(Undergrowth.FlyAgaric, 0.25f), S(Undergrowth.BrownCaps, 0.5f)),
            OpenGround = List(S(Undergrowth.BrackenPlant), S(Undergrowth.Sedge), S(Undergrowth.WhiteDaisy, 0.4f), S(Undergrowth.Heather, 0.6f)),
            Waterside = List(S(Undergrowth.Reeds)),
            DeadwoodDensity = 0.5f,
            BoulderDensity = 0.45f,
            FloorDensity = 0.75f,
            FogDensity = 0.03f,
            FogColour = new Vector3(0.68f, 0.76f, 0.76f),
            WindStrength = 0.7f,
        };

        public static readonly BiomeDefinition BirchWood = new BiomeDefinition
        {
            Id = "birch",
            Life = Fauna.Temperate,
            Bedrock = RockKind.Granite,
            Name = "Birchwood",
            MapColour = new Vector3(0.55f, 0.68f, 0.42f),
            Temperature = 0.45f,
            Moisture = 0.72f,
            Ground = SurfaceMaterial.Grass,
            Understory = SurfaceMaterial.GrassySoil,
            GrassCover = 0.9f,
            GrassHeight = 0.62f,
            GrassColour = new Vector3(0.18f, 0.31f, 0.07f),
            GrassPlumes = 0.04f,
            GrassBloom = 0.05f,
            GrassBloomColour = new Vector3(0.529f, 0.529f, 0.556f),
            GrassBloomColourTwo = new Vector3(0.245f, 0.152f, 0.47f),
            ForestCover = 0.62f,
            ForestPatchSize = 150f,
            TreeSpacing = 1.2f,
            Canopy = List(S(Broadleaves.Birch, 4f), S(Broadleaves.Beech, 1f), S(Broadleaves.GoldBirch, 0.4f)),
            Shrubs = List(S(Undergrowth.BroadShrub, 2f), S(Undergrowth.Boxwood, 1f)),
            Deadwood = List(S(Wilds.BirchLog, 2f), S(Wilds.OakStump, 1f)),
            Boulders = List(S(NatureCatalog.Boulder)),
            ForestFloor = List(S(Undergrowth.LadyFernPlant, 2f), S(Undergrowth.Bluebell, 2f), S(Undergrowth.WhiteDaisy, 1f), S(Undergrowth.MossCushion, 1f)),
            OpenGround = List(S(Undergrowth.WhiteDaisy, 2f), S(Undergrowth.PurpleAster, 1f), S(Undergrowth.Yarrow, 1f), S(Undergrowth.BlueLupine, 1f)),
            Waterside = List(S(Undergrowth.Reeds, 2f), S(Undergrowth.Bulrush, 1f)),
            FloorDensity = 0.6f,
            FogDensity = 0.022f,
            Motes = AmbientMotes.Pollen,
        };

        public static readonly BiomeDefinition CrimsonWood = new BiomeDefinition
        {
            Id = "crimson",
            Life = Fauna.Temperate,
            Bedrock = RockKind.Sandstone,
            Name = "Crimson Wood",
            MapColour = new Vector3(0.72f, 0.2f, 0.14f),
            Temperature = 0.48f,
            Moisture = 0.42f,
            Ground = SurfaceMaterial.GrassySoil,
            Understory = SurfaceMaterial.Soil,
            GrassCover = 0.8f,
            GrassHeight = 0.6f,
            GrassColour = new Vector3(0.42f, 0.15f, 0.05f),
            GrassDryness = 0.2f,
            GrassPlumes = 0.08f,
            GrassBloom = 0.07f,
            GrassBloomColour = new Vector3(0.47f, 0.014f, 0.007f),
            GrassBloomColourTwo = new Vector3(0.556f, 0.279f, 0.019f),
            ForestCover = 0.72f,
            ForestPatchSize = 190f,
            Canopy = List(S(Broadleaves.CrimsonMaple, 4f), S(Broadleaves.GoldMaple, 2f), S(Broadleaves.JapaneseMaple, 2f), S(Broadleaves.GoldBirch, 0.6f), S(Broadleaves.CopperBeech, 0.8f)),
            Edge = List(S(Broadleaves.JapaneseMaple, 2f), S(Broadleaves.GoldMaple, 1f)),
            Shrubs = List(S(Undergrowth.BroadShrub, 1f), S(Undergrowth.Azalea, 0.5f)),
            Deadwood = List(S(Wilds.OakStump), S(Wilds.OakLog, 0.6f)),
            Boulders = List(S(NatureCatalog.BrownRock)),
            ForestFloor = List(S(Broadleaves.CrimsonLeaves, 4f), S(Broadleaves.AmberLeaves, 2f), S(Undergrowth.BrackenPlant, 1f), S(Undergrowth.RedSpiderLily, 1f), S(Undergrowth.BrownCaps, 0.4f), S(Undergrowth.FlyAgaric, 0.2f)),
            OpenGround = List(S(Undergrowth.RedSpiderLily, 2f), S(Undergrowth.RedPoppy, 1f), S(Undergrowth.YellowFlower, 1f), S(Broadleaves.CrimsonLeaves, 1f), S(Undergrowth.SilverGrass, 1f)),
            Waterside = List(S(Undergrowth.Reeds)),
            FloorDensity = 0.35f,
            FogDensity = 0.026f,
            FogColour = new Vector3(0.86f, 0.72f, 0.62f),
            Motes = AmbientMotes.FallingLeaves,
            MoteColour = new Vector3(0.74f, 0.18f, 0.08f),
        };

        public static readonly BiomeDefinition BlossomGrove = new BiomeDefinition
        {
            Id = "blossom",
            Life = Fauna.Blossom,
            Bedrock = RockKind.Limestone,
            Name = "Blossom Grove",
            MapColour = new Vector3(0.9f, 0.68f, 0.76f),
            Temperature = 0.62f,
            Moisture = 0.62f,
            Coastal = 0.1f,
            Ground = SurfaceMaterial.Grass,
            Understory = SurfaceMaterial.Grass,
            GrassCover = 1f,
            GrassHeight = 0.6f,
            GrassColour = new Vector3(0.2f, 0.33f, 0.09f),
            GrassPlumes = 0.03f,
            GrassBloom = 0.1f,
            GrassBloomColour = new Vector3(0.556f, 0.245f, 0.367f),
            GrassBloomColourTwo = new Vector3(0.556f, 0.512f, 0.529f),
            ForestCover = 0.42f,
            ForestPatchSize = 110f,
            TreeSpacing = 1.3f,
            LoneTrees = 0.15f,
            Canopy = List(S(Broadleaves.Sakura, 5f), S(Broadleaves.WhiteCherry, 2f), S(Broadleaves.WisteriaTree, 1f), S(Broadleaves.JapaneseMaple, 0.5f)),
            Shrubs = List(S(Undergrowth.Azalea, 2f), S(Undergrowth.WhiteAzalea, 1f), S(Undergrowth.Boxwood, 1f)),
            Deadwood = List(S(Wilds.OakStump, 0.5f)),
            Boulders = List(S(NatureCatalog.Boulder)),
            ForestFloor = List(S(Broadleaves.FallenPetals, 4f), S(Undergrowth.WhiteDaisy, 2f), S(Undergrowth.PurpleAster, 1f), S(Undergrowth.HostaPlant, 1f)),
            OpenGround = List(S(Broadleaves.FallenPetals, 1f), S(Undergrowth.WhiteDaisy, 2f), S(Undergrowth.PinkLupine, 1f), S(Undergrowth.RedPoppy, 1f), S(Undergrowth.Lavender, 1f), S(Undergrowth.WhiteSpiderLily, 0.6f)),
            Waterside = List(S(Undergrowth.Reeds), S(Undergrowth.HostaPlant, 0.5f)),
            FloorDensity = 0.45f,
            OpenDensity = 0.35f,
            FogDensity = 0.02f,
            FogColour = new Vector3(0.92f, 0.82f, 0.86f),
            Motes = AmbientMotes.Petals,
        };

        public static readonly BiomeDefinition BambooThicket = new BiomeDefinition
        {
            Id = "bamboo",
            Life = Fauna.Blossom,
            Bedrock = RockKind.Limestone,
            Name = "Bamboo Thicket",
            MapColour = new Vector3(0.3f, 0.55f, 0.3f),
            Temperature = 0.74f,
            Moisture = 0.78f,
            Ground = SurfaceMaterial.GrassySoil,
            Understory = SurfaceMaterial.Soil,
            GrassCover = 0.75f,
            GrassHeight = 0.5f,
            GrassColour = new Vector3(0.13f, 0.27f, 0.08f),
            GrassPlumes = 0.03f,
            GrassBloom = 0.012f,
            GrassBloomColour = new Vector3(0.512f, 0.512f, 0.429f),
            GrassBloomColourTwo = new Vector3(0.47f, 0.39f, 0.099f),
            ForestCover = 0.75f,
            ForestPatchSize = 140f,
            TreeSpacing = 1.25f,
            Canopy = List(S(Wilds.GreenBamboo, 5f), S(Wilds.BlackBamboo, 2f)),
            Shrubs = List(S(Undergrowth.BroadShrub), S(Undergrowth.Laurel)),
            Deadwood = List(S(Wilds.OakStump, 0.5f)),
            Boulders = List(S(NatureCatalog.Boulder)),
            ForestFloor = List(S(Undergrowth.SwordFernPlant, 2f), S(Undergrowth.HostaPlant, 1f), S(Undergrowth.MossCushion, 1f)),
            OpenGround = List(S(Undergrowth.SwordFernPlant), S(Undergrowth.WhiteDaisy, 0.5f), S(Undergrowth.Sedge)),
            Waterside = List(S(Undergrowth.Reeds)),
            FogDensity = 0.03f,
            FogColour = new Vector3(0.72f, 0.82f, 0.74f),
            Motes = AmbientMotes.Fireflies,
        };

        public static readonly BiomeDefinition Marsh = new BiomeDefinition
        {
            Id = "marsh",
            Life = Fauna.Mire,
            Bedrock = RockKind.Limestone,
            Name = "Mire",
            MapColour = new Vector3(0.36f, 0.4f, 0.27f),
            Temperature = 0.52f,
            Moisture = 0.96f,
            Altitude = -0.4f,
            Ground = SurfaceMaterial.Mud,
            Understory = SurfaceMaterial.Mud,
            Shore = SurfaceMaterial.Mud,
            Path = SurfaceMaterial.Gravel,
            GrassCover = 0.8f,
            GrassHeight = 0.8f,
            GrassColour = new Vector3(0.15f, 0.22f, 0.08f),
            GrassDryness = 0.2f,
            GrassPlumes = 0.15f,
            GrassBloom = 0.025f,
            GrassBloomColour = new Vector3(0.279f, 0.124f, 0.429f),
            GrassBloomColourTwo = new Vector3(0.529f, 0.47f, 0.099f),
            ForestCover = 0.22f,
            ForestPatchSize = 90f,
            TreeSpacing = 1.5f,
            LoneTrees = 0.08f,
            Canopy = List(S(Wilds.SwampCypress, 4f), S(Broadleaves.Willow, 2f), S(Wilds.SilverSnag, 1f)),
            Edge = List(S(Broadleaves.Willow), S(Wilds.SilverSnag)),
            Shrubs = List(S(Undergrowth.DeadBush), S(Undergrowth.BroadShrub)),
            Deadwood = List(S(Wilds.SilverLog), S(Wilds.BlackLog, 0.6f), S(Wilds.SilverStump)),
            Boulders = List(S(NatureCatalog.Boulder, 0.4f)),
            ForestFloor = List(S(Undergrowth.Bulrush, 2f), S(Undergrowth.Reeds, 2f), S(Undergrowth.BrownCaps, 1f), S(Undergrowth.Sedge, 1f)),
            OpenGround = List(S(Undergrowth.Reeds, 3f), S(Undergrowth.Bulrush, 2f), S(Undergrowth.Sedge, 2f), S(Undergrowth.PurpleAster, 0.3f)),
            Waterside = List(S(Undergrowth.Reeds, 2f), S(Undergrowth.Bulrush, 2f)),
            DeadwoodDensity = 0.45f,
            BoulderDensity = 0.1f,
            OpenDensity = 0.35f,
            FogDensity = 0.07f,
            FogColour = new Vector3(0.66f, 0.7f, 0.6f),
            WindStrength = 0.5f,
            Motes = AmbientMotes.Fireflies,
        };

        public static readonly BiomeDefinition Highland = new BiomeDefinition
        {
            Id = "highland",
            Life = Fauna.Upland,
            Bedrock = RockKind.Schist,
            Name = "High Moor",
            MapColour = new Vector3(0.52f, 0.5f, 0.42f),
            Temperature = 0.25f,
            Moisture = 0.36f,
            Altitude = 0.55f,
            Ground = SurfaceMaterial.GrassySoil,
            Understory = SurfaceMaterial.Gravel,
            Shore = SurfaceMaterial.Gravel,
            Path = SurfaceMaterial.Gravel,
            GrassCover = 0.85f,
            GrassHeight = 0.42f,
            GrassColour = new Vector3(0.2f, 0.25f, 0.08f),
            GrassDryness = 0.25f,
            GrassPlumes = 0.04f,
            GrassBloom = 0.09f,
            GrassBloomColour = new Vector3(0.336f, 0.109f, 0.367f),
            GrassBloomColourTwo = new Vector3(0.495f, 0.212f, 0.351f),
            ForestCover = 0.1f,
            ForestPatchSize = 80f,
            TreeSpacing = 1.5f,
            LoneTrees = 0.04f,
            Canopy = List(S(Conifers.BlackPine, 3f), S(Conifers.ScotsPine, 0.5f), S(Conifers.DeadPine, 0.4f)),
            Shrubs = List(S(Undergrowth.Gorse, 3f), S(Undergrowth.Juniper, 2f)),
            Deadwood = List(S(Wilds.PineStump), S(Wilds.SilverStump)),
            Boulders = List(S(NatureCatalog.Boulder, 2f), S(NatureCatalog.BrownRock, 1f)),
            OpenGround = List(S(Undergrowth.Heather, 4f), S(Undergrowth.Sedge, 1f), S(NatureCatalog.Pebbles, 1f), S(Undergrowth.MossCushion, 0.5f)),
            ShrubDensity = 0.2f,
            BoulderDensity = 0.75f,
            OpenDensity = 0.4f,
            FogDensity = 0.035f,
            FogColour = new Vector3(0.74f, 0.76f, 0.8f),
            WindStrength = 1.7f,
        };

        public static readonly BiomeDefinition Snowfield = new BiomeDefinition
        {
            Id = "snow",
            Life = Fauna.Frozen,
            Name = "Snowfield",
            MapColour = new Vector3(0.9f, 0.93f, 0.96f),
            Temperature = 0.04f,
            Moisture = 0.5f,
            Altitude = 0.75f,
            Ground = SurfaceMaterial.Snow,
            Understory = SurfaceMaterial.Snow,
            Shore = SurfaceMaterial.Ice,
            Path = SurfaceMaterial.Gravel,
            GrassCover = 0f,
            ForestCover = 0.22f,
            ForestPatchSize = 120f,
            TreeSpacing = 1.4f,
            LoneTrees = 0.03f,
            Canopy = List(S(Conifers.FrostedSpruce, 3f), S(Wilds.SilverSnag, 1f)),
            Deadwood = List(S(Wilds.PineStump), S(Wilds.SilverLog, 0.5f)),
            Boulders = List(S(NatureCatalog.Boulder)),
            ShrubDensity = 0f,
            BoulderDensity = 0.5f,
            FloorDensity = 0f,
            OpenDensity = 0f,
            FogDensity = 0.05f,
            FogColour = new Vector3(0.84f, 0.88f, 0.94f),
            WindStrength = 1.5f,
            Motes = AmbientMotes.Snow,
        };

        public static readonly BiomeDefinition Dunes = new BiomeDefinition
        {
            Id = "dunes",
            Life = Fauna.Sands,
            Bedrock = RockKind.Sandstone,
            Name = "Dunes",
            MapColour = new Vector3(0.88f, 0.76f, 0.5f),
            Temperature = 0.9f,
            Moisture = 0.08f,
            Coastal = 0.3f,
            Ground = SurfaceMaterial.Dunes,
            Understory = SurfaceMaterial.Sand,
            Path = SurfaceMaterial.Gravel,
            GrassCover = 0.2f,
            GrassHeight = 0.55f,
            GrassColour = new Vector3(0.5f, 0.42f, 0.2f),
            GrassDryness = 0.7f,
            GrassPlumes = 0.2f,
            ForestCover = 0.02f,
            ForestPatchSize = 70f,
            LoneTrees = 0.02f,
            Canopy = List(S(Wilds.DatePalm, 2f), S(Wilds.SilverSnag, 1f)),
            Shrubs = List(S(Undergrowth.Sagebrush, 2f), S(Undergrowth.DeadBush, 2f), S(Wilds.PricklyPear, 1f)),
            Boulders = List(S(NatureCatalog.BrownRock)),
            OpenGround = List(S(Undergrowth.DuneGrass, 3f), S(Wilds.Agave, 1f), S(Wilds.BarrelCactus, 1f), S(NatureCatalog.Pebbles, 1f)),
            Waterside = List(S(Undergrowth.Reeds)),
            ShrubDensity = 0.2f,
            BoulderDensity = 0.2f,
            OpenDensity = 0.1f,
            FogDensity = 0.012f,
            FogColour = new Vector3(0.94f, 0.86f, 0.7f),
            WindStrength = 1.4f,
            Motes = AmbientMotes.Dust,
        };

        public static readonly BiomeDefinition Badlands = new BiomeDefinition
        {
            Id = "badlands",
            Life = Fauna.Scorched,
            Bedrock = RockKind.Sandstone,
            Name = "Badlands",
            MapColour = new Vector3(0.68f, 0.44f, 0.3f),
            Temperature = 0.86f,
            Moisture = 0.14f,
            Altitude = 0.2f,
            Ground = SurfaceMaterial.CrackedEarth,
            Understory = SurfaceMaterial.CrackedEarth,
            Path = SurfaceMaterial.Gravel,
            GrassCover = 0.25f,
            GrassHeight = 0.4f,
            GrassColour = new Vector3(0.42f, 0.34f, 0.16f),
            GrassDryness = 0.7f,
            GrassPlumes = 0.08f,
            ForestCover = 0.02f,
            ForestPatchSize = 70f,
            LoneTrees = 0.015f,
            Canopy = List(S(Wilds.Saguaro, 3f), S(Wilds.SilverSnag, 1f)),
            Shrubs = List(S(Undergrowth.DeadBush, 2f), S(Wilds.PricklyPear, 2f), S(Undergrowth.Sagebrush, 2f)),
            Boulders = List(S(NatureCatalog.BrownRock)),
            OpenGround = List(S(NatureCatalog.Pebbles, 2f), S(Wilds.BarrelCactus, 1f), S(Wilds.Agave, 1f), S(Undergrowth.DuneGrass, 1f)),
            ShrubDensity = 0.3f,
            BoulderDensity = 0.7f,
            OpenDensity = 0.1f,
            FogDensity = 0.01f,
            FogColour = new Vector3(0.88f, 0.72f, 0.56f),
            Motes = AmbientMotes.Dust,
        };

        public static readonly BiomeDefinition Ashland = new BiomeDefinition
        {
            Id = "ash",
            Life = Fauna.Burnt,
            Bedrock = RockKind.Basalt,
            Name = "Ashlands",
            MapColour = new Vector3(0.24f, 0.2f, 0.2f),
            Temperature = 0.96f,
            Moisture = 0.2f,
            Altitude = 0.3f,
            Ground = SurfaceMaterial.Ash,
            Understory = SurfaceMaterial.Ash,
            Shore = SurfaceMaterial.Gravel,
            Path = SurfaceMaterial.Gravel,
            GrassCover = 0f,
            ForestCover = 0.14f,
            ForestPatchSize = 100f,
            TreeSpacing = 1.6f,
            LoneTrees = 0.03f,
            Canopy = List(S(Wilds.CharredTree)),
            Deadwood = List(S(Wilds.CharredStump), S(Wilds.CharredLog)),
            Boulders = List(S(NatureCatalog.Boulder)),
            ShrubDensity = 0f,
            BoulderDensity = 0.6f,
            FloorDensity = 0f,
            OpenDensity = 0f,
            FogDensity = 0.05f,
            FogColour = new Vector3(0.5f, 0.42f, 0.38f),
            Motes = AmbientMotes.Embers,
        };

        public static readonly BiomeDefinition RedwoodGrove = new BiomeDefinition
        {
            Id = "redwood",
            Life = Fauna.Conifer,
            Name = "Redwood Grove",
            MapColour = new Vector3(0.4f, 0.22f, 0.16f),
            Temperature = 0.42f,
            Moisture = 0.84f,
            Ground = SurfaceMaterial.GrassySoil,
            Understory = SurfaceMaterial.Moss,
            GrassCover = 0.55f,
            GrassHeight = 0.42f,
            GrassColour = new Vector3(0.08f, 0.17f, 0.06f),
            GrassPlumes = 0.01f,
            GrassBloom = 0.01f,
            GrassBloomColour = new Vector3(0.512f, 0.512f, 0.529f),
            GrassBloomColourTwo = new Vector3(0.39f, 0.351f, 0.47f),
            ForestCover = 0.82f,
            ForestPatchSize = 220f,
            TreeSpacing = 1.15f,
            LoneTrees = 0.03f,
            Canopy = List(S(Conifers.Redwood, 5), S(Conifers.Cedar, 1)),
            Edge = List(S(Conifers.Cedar, 1), S(Conifers.Spruce, 1)),
            Shrubs = List(S(Undergrowth.Laurel, 2), S(Undergrowth.BroadShrub, 1)),
            Deadwood = List(S(Wilds.RedwoodLog, 3), S(Wilds.RedwoodStump, 1)),
            Boulders = List(S(NatureCatalog.Boulder)),
            ForestFloor = List(S(Undergrowth.SwordFernPlant, 4), S(Undergrowth.LadyFernPlant, 2), S(Undergrowth.MossCushion, 2), S(Undergrowth.BrownCaps, 0.4f)),
            OpenGround = List(S(Undergrowth.SwordFernPlant), S(Undergrowth.BrackenPlant), S(Undergrowth.WhiteDaisy, 0.3f)),
            Waterside = List(S(Undergrowth.Reeds), S(Undergrowth.HostaPlant, 0.5f)),
            DeadwoodDensity = 0.35f,
            BoulderDensity = 0.25f,
            FloorDensity = 0.95f,
            FogDensity = 0.045f,
            FogColour = new Vector3(0.7f, 0.74f, 0.7f),
            WindStrength = 0.5f,
            Motes = AmbientMotes.Pollen,
            MoteColour = new Vector3(0.7f, 0.74f, 0.5f),
        };

        public static readonly BiomeDefinition DarkWood = new BiomeDefinition
        {
            Id = "darkwood",
            Life = Fauna.Haunted,
            Bedrock = RockKind.Schist,
            Name = "The Blackwood",
            MapColour = new Vector3(0.14f, 0.13f, 0.17f),
            Temperature = 0.3f,
            Moisture = 0.56f,
            Ground = SurfaceMaterial.Soil,
            Understory = SurfaceMaterial.Mud,
            Shore = SurfaceMaterial.Mud,
            GrassCover = 0.3f,
            GrassHeight = 0.35f,
            GrassColour = new Vector3(0.09f, 0.1f, 0.07f),
            GrassDryness = 0.5f,
            GrassPlumes = 0.02f,
            ForestCover = 0.68f,
            ForestPatchSize = 170f,
            TreeSpacing = 1.1f,
            LoneTrees = 0.06f,
            Canopy = List(S(Wilds.BlackThorn, 5), S(Wilds.SilverSnag, 1)),
            Shrubs = List(S(Undergrowth.DeadBush)),
            Deadwood = List(S(Wilds.BlackLog, 2), S(Wilds.SilverStump, 1)),
            Boulders = List(S(NatureCatalog.Boulder)),
            ForestFloor = List(S(Undergrowth.GhostCaps, 0.4f), S(Undergrowth.InkCaps, 2), S(Undergrowth.MossCushion, 2), S(Undergrowth.Sedge, 2)),
            OpenGround = List(S(Undergrowth.InkCaps, 1), S(Undergrowth.Sedge, 1)),
            Waterside = List(S(Undergrowth.Bulrush)),
            ShrubDensity = 0.35f,
            DeadwoodDensity = 0.75f,
            BoulderDensity = 0.35f,
            FloorDensity = 0.42f,
            OpenDensity = 0.12f,
            FogDensity = 0.06f,
            FogColour = new Vector3(0.42f, 0.42f, 0.48f),
            WindStrength = 0.6f,
            Motes = AmbientMotes.Wisps,
        };

        public static readonly BiomeDefinition Jungle = new BiomeDefinition
        {
            Id = "jungle",
            Life = Fauna.Tropics,
            Bedrock = RockKind.Limestone,
            Name = "The Green Deep",
            MapColour = new Vector3(0.1f, 0.44f, 0.22f),
            Temperature = 0.84f,
            Moisture = 0.9f,
            Coastal = 0.15f,
            Ground = SurfaceMaterial.GrassySoil,
            Understory = SurfaceMaterial.Moss,
            GrassCover = 0.7f,
            GrassHeight = 0.6f,
            GrassColour = new Vector3(0.1f, 0.28f, 0.07f),
            GrassPlumes = 0.02f,
            GrassBloom = 0.02f,
            GrassBloomColour = new Vector3(0.556f, 0.2f, 0.25f),
            GrassBloomColourTwo = new Vector3(0.556f, 0.4f, 0.03f),
            ForestCover = 0.8f,
            ForestPatchSize = 200f,
            TreeSpacing = 0.95f,
            LoneTrees = 0.08f,
            Canopy = List(S(Broadleaves.Camphor, 3), S(Wilds.CoconutPalm, 2), S(Wilds.TreeFern, 3), S(Wilds.GreenBamboo, 0.7f)),
            Edge = List(S(Wilds.TreeFern, 2), S(Wilds.CoconutPalm, 2)),
            Shrubs = List(S(Wilds.Banana, 3), S(Undergrowth.Laurel, 1), S(Undergrowth.BroadShrub, 1)),
            Deadwood = List(S(Wilds.OakLog), S(Wilds.OakStump)),
            Boulders = List(S(NatureCatalog.Boulder)),
            ForestFloor = List(S(Undergrowth.SwordFernPlant, 3), S(Undergrowth.HostaPlant, 2), S(Undergrowth.LadyFernPlant, 2), S(Undergrowth.MossCushion, 1), S(Undergrowth.FlyAgaric, 0.1f)),
            OpenGround = List(S(Undergrowth.HostaPlant, 1), S(Undergrowth.RedPoppy, 0.25f), S(Undergrowth.SwordFernPlant, 1), S(Undergrowth.Sedge, 1)),
            Waterside = List(S(Undergrowth.Reeds), S(Undergrowth.HostaPlant, 1f)),
            ShrubDensity = 0.9f,
            FloorDensity = 0.9f,
            OpenDensity = 0.3f,
            FogDensity = 0.04f,
            FogColour = new Vector3(0.72f, 0.82f, 0.72f),
            WindStrength = 0.7f,
            Motes = AmbientMotes.Fireflies,
        };

        public static readonly BiomeDefinition Savanna = new BiomeDefinition
        {
            Id = "savanna",
            Life = Fauna.Plains,
            Bedrock = RockKind.Sandstone,
            Name = "The Long Grass",
            MapColour = new Vector3(0.74f, 0.66f, 0.3f),
            Temperature = 0.82f,
            Moisture = 0.28f,
            Ground = SurfaceMaterial.GrassySoil,
            Understory = SurfaceMaterial.GrassySoil,
            GrassCover = 0.96f,
            GrassHeight = 0.9f,
            GrassColour = new Vector3(0.42f, 0.3f, 0.08f),
            GrassDryness = 0.55f,
            GrassPlumes = 0.25f,
            GrassBloom = 0.005f,
            GrassBloomColour = new Vector3(0.512f, 0.245f, 0.025f),
            GrassBloomColourTwo = new Vector3(0.512f, 0.47f, 0.351f),
            ForestCover = 0.08f,
            ForestPatchSize = 80f,
            TreeSpacing = 2.2f,
            LoneTrees = 0.2f,
            Canopy = List(S(Broadleaves.Acacia, 5), S(Broadleaves.Baobab, 1)),
            Shrubs = List(S(Undergrowth.Sagebrush, 2), S(Undergrowth.DeadBush, 1)),
            Deadwood = List(S(Wilds.SilverLog), S(Wilds.SilverStump)),
            Boulders = List(S(NatureCatalog.BrownRock)),
            OpenGround = List(S(Undergrowth.DuneGrass, 2), S(Undergrowth.PampasGrass, 1), S(Undergrowth.YellowFlower, 0.2f)),
            Waterside = List(S(Undergrowth.Reeds)),
            ShrubDensity = 0.3f,
            BoulderDensity = 0.25f,
            OpenDensity = 0.22f,
            FogDensity = 0.012f,
            FogColour = new Vector3(0.92f, 0.84f, 0.66f),
            WindStrength = 1.4f,
            Motes = AmbientMotes.Dust,
        };

        public static readonly BiomeDefinition GoldenWood = new BiomeDefinition
        {
            Id = "ginkgo",
            Life = Fauna.Temperate,
            Bedrock = RockKind.Limestone,
            Name = "Gold Wood",
            MapColour = new Vector3(0.86f, 0.68f, 0.14f),
            Temperature = 0.5f,
            Moisture = 0.5f,
            Ground = SurfaceMaterial.GrassySoil,
            Understory = SurfaceMaterial.Soil,
            GrassCover = 0.8f,
            GrassHeight = 0.5f,
            GrassColour = new Vector3(0.34f, 0.26f, 0.06f),
            GrassDryness = 0.2f,
            GrassPlumes = 0.06f,
            GrassBloom = 0.03f,
            GrassBloomColour = new Vector3(0.556f, 0.43f, 0.02f),
            GrassBloomColourTwo = new Vector3(0.529f, 0.5f, 0.42f),
            ForestCover = 0.66f,
            ForestPatchSize = 170f,
            TreeSpacing = 1.2f,
            LoneTrees = 0.1f,
            Canopy = List(S(Broadleaves.Ginkgo, 5), S(Broadleaves.GoldBirch, 1), S(Broadleaves.GoldMaple, 1)),
            Shrubs = List(S(Undergrowth.BroadShrub), S(Undergrowth.Boxwood)),
            Deadwood = List(S(Wilds.OakStump), S(Wilds.OakLog, 0.5f)),
            Boulders = List(S(NatureCatalog.Boulder)),
            ForestFloor = List(S(Broadleaves.GinkgoLeaves, 6), S(Undergrowth.BrownCaps, 1), S(Undergrowth.BrackenPlant, 1)),
            OpenGround = List(S(Broadleaves.GinkgoLeaves, 2), S(Undergrowth.YellowFlower, 1), S(Undergrowth.SilverGrass, 1)),
            Waterside = List(S(Undergrowth.Reeds)),
            FloorDensity = 0.75f,
            OpenDensity = 0.25f,
            FogDensity = 0.022f,
            FogColour = new Vector3(0.9f, 0.82f, 0.62f),
            Motes = AmbientMotes.FallingLeaves,
            MoteColour = new Vector3(0.9f, 0.7f, 0.1f),
        };

        public static readonly BiomeDefinition CedarWood = new BiomeDefinition
        {
            Id = "cedar",
            Life = Fauna.Conifer,
            Name = "Old Cedars",
            MapColour = new Vector3(0.16f, 0.26f, 0.2f),
            Temperature = 0.48f,
            Moisture = 0.72f,
            Ground = SurfaceMaterial.GrassySoil,
            Understory = SurfaceMaterial.Moss,
            GrassCover = 0.6f,
            GrassHeight = 0.45f,
            GrassColour = new Vector3(0.09f, 0.18f, 0.07f),
            GrassPlumes = 0.01f,
            GrassBloom = 0.01f,
            GrassBloomColour = new Vector3(0.512f, 0.512f, 0.556f),
            GrassBloomColourTwo = new Vector3(0.47f, 0.2f, 0.25f),
            ForestCover = 0.78f,
            ForestPatchSize = 200f,
            TreeSpacing = 1.05f,
            LoneTrees = 0.04f,
            Canopy = List(S(Conifers.Cedar, 5), S(Broadleaves.Camphor, 1), S(Broadleaves.JapaneseMaple, 0.6f)),
            Edge = List(S(Broadleaves.JapaneseMaple, 1), S(Conifers.Cedar, 1)),
            Shrubs = List(S(Undergrowth.Laurel, 2), S(Undergrowth.Azalea, 0.7f)),
            Deadwood = List(S(Wilds.PineLog, 2), S(Wilds.PineStump, 2)),
            Boulders = List(S(NatureCatalog.Boulder)),
            ForestFloor = List(S(Undergrowth.SwordFernPlant, 3), S(Undergrowth.MossCushion, 3), S(Undergrowth.HostaPlant, 1), S(Undergrowth.BrownCaps, 1)),
            OpenGround = List(S(Undergrowth.Sedge, 1), S(Undergrowth.RedSpiderLily, 0.5f), S(Undergrowth.WhiteDaisy, 0.5f)),
            Waterside = List(S(Undergrowth.Reeds)),
            DeadwoodDensity = 0.5f,
            FloorDensity = 0.85f,
            FogDensity = 0.04f,
            FogColour = new Vector3(0.68f, 0.74f, 0.72f),
            WindStrength = 0.55f,
        };

        public static readonly IReadOnlyList<BiomeDefinition> All = new[]
        {
            Meadow, GoldenSteppe, PineForest, BirchWood, CrimsonWood, BlossomGrove, BambooThicket, Marsh, Highland, Snowfield, Dunes, Badlands, Ashland,
            RedwoodGrove, DarkWood, Jungle, Savanna, GoldenWood, CedarWood,
        };
    }
}
