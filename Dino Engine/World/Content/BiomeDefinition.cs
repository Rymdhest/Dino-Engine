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
    }

    /// <summary>Every biome worlds can be built from.</summary>
    public static class BiomeCatalog
    {
        private static Species[] List(params Species[] species) => species;
        private static Species S(WorldAsset asset, float weight = 1f) => new Species(asset, weight);

        public static readonly BiomeDefinition Meadow = new BiomeDefinition
        {
            Id = "meadow",
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
            Canopy = List(S(NatureCatalog.Oak, 3f), S(NatureCatalog.Birch, 1f)),
            Shrubs = List(S(NatureCatalog.BigLeafBush)),
            Deadwood = List(S(NatureCatalog.Stump)),
            Boulders = List(S(NatureCatalog.BrownRock, 1f), S(NatureCatalog.Boulder, 1f)),
            ForestFloor = List(S(NatureCatalog.Fern)),
            OpenGround = List(S(NatureCatalog.FlowerYellow), S(NatureCatalog.FlowerWhite), S(NatureCatalog.FlowerRed, 0.6f), S(NatureCatalog.FlowerPurple, 0.6f)),
            Waterside = List(S(NatureCatalog.Reeds)),
            OpenDensity = 0.3f,
            Motes = AmbientMotes.Pollen,
        };

        public static readonly BiomeDefinition GoldenSteppe = new BiomeDefinition
        {
            Id = "steppe",
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
            Canopy = List(S(NatureCatalog.Oak)),
            Shrubs = List(S(NatureCatalog.Scrub)),
            Deadwood = List(S(NatureCatalog.Stump)),
            Boulders = List(S(NatureCatalog.BrownRock)),
            OpenGround = List(S(NatureCatalog.Pampas, 3f), S(NatureCatalog.FlowerYellow, 0.3f)),
            Waterside = List(S(NatureCatalog.Reeds)),
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
            Canopy = List(S(NatureCatalog.PineTall, 3f), S(NatureCatalog.PineMid, 3f), S(NatureCatalog.PineSparse, 1f)),
            Edge = List(S(NatureCatalog.PineSparse, 2f), S(NatureCatalog.PineMid, 1f)),
            Shrubs = List(S(NatureCatalog.BigLeafBush)),
            Deadwood = List(S(NatureCatalog.FallenPine, 2f), S(NatureCatalog.Stump, 2f), S(NatureCatalog.PineDead, 1f)),
            Boulders = List(S(NatureCatalog.Boulder)),
            ForestFloor = List(S(NatureCatalog.Fern, 3f), S(NatureCatalog.Mushrooms, 1f)),
            OpenGround = List(S(NatureCatalog.Fern), S(NatureCatalog.FlowerWhite, 0.4f)),
            Waterside = List(S(NatureCatalog.Reeds)),
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
            Canopy = List(S(NatureCatalog.Birch)),
            Shrubs = List(S(NatureCatalog.BigLeafBush)),
            Deadwood = List(S(NatureCatalog.Stump)),
            Boulders = List(S(NatureCatalog.Boulder)),
            ForestFloor = List(S(NatureCatalog.Fern, 2f), S(NatureCatalog.FlowerWhite, 1f)),
            OpenGround = List(S(NatureCatalog.FlowerWhite, 2f), S(NatureCatalog.FlowerPurple, 1f)),
            Waterside = List(S(NatureCatalog.Reeds)),
            FloorDensity = 0.6f,
            FogDensity = 0.022f,
            Motes = AmbientMotes.Pollen,
        };

        public static readonly BiomeDefinition CrimsonWood = new BiomeDefinition
        {
            Id = "crimson",
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
            Canopy = List(S(NatureCatalog.Maple, 4f), S(NatureCatalog.BirchGold, 1f)),
            Shrubs = List(S(NatureCatalog.BigLeafBush)),
            Deadwood = List(S(NatureCatalog.Stump), S(NatureCatalog.FallenPine, 0.5f)),
            Boulders = List(S(NatureCatalog.BrownRock)),
            ForestFloor = List(S(NatureCatalog.FlowerRed, 1f), S(NatureCatalog.Mushrooms, 1f)),
            OpenGround = List(S(NatureCatalog.FlowerRed, 2f), S(NatureCatalog.FlowerYellow, 1f)),
            Waterside = List(S(NatureCatalog.Reeds)),
            FloorDensity = 0.35f,
            FogDensity = 0.026f,
            FogColour = new Vector3(0.86f, 0.72f, 0.62f),
            Motes = AmbientMotes.FallingLeaves,
        };

        public static readonly BiomeDefinition BlossomGrove = new BiomeDefinition
        {
            Id = "blossom",
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
            Canopy = List(S(NatureCatalog.Blossom)),
            Shrubs = List(S(NatureCatalog.BigLeafBush)),
            Boulders = List(S(NatureCatalog.Boulder)),
            ForestFloor = List(S(NatureCatalog.FlowerWhite, 2f), S(NatureCatalog.FlowerPurple, 1f)),
            OpenGround = List(S(NatureCatalog.FlowerWhite, 2f), S(NatureCatalog.FlowerRed, 1f)),
            Waterside = List(S(NatureCatalog.Reeds)),
            FloorDensity = 0.45f,
            OpenDensity = 0.35f,
            FogDensity = 0.02f,
            FogColour = new Vector3(0.92f, 0.82f, 0.86f),
            Motes = AmbientMotes.Petals,
        };

        public static readonly BiomeDefinition BambooThicket = new BiomeDefinition
        {
            Id = "bamboo",
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
            Canopy = List(S(NatureCatalog.Bamboo)),
            Shrubs = List(S(NatureCatalog.BigLeafBush)),
            Deadwood = List(S(NatureCatalog.Stump, 0.5f)),
            Boulders = List(S(NatureCatalog.Boulder)),
            ForestFloor = List(S(NatureCatalog.Fern)),
            OpenGround = List(S(NatureCatalog.Fern), S(NatureCatalog.FlowerWhite, 0.5f)),
            Waterside = List(S(NatureCatalog.Reeds)),
            FogDensity = 0.03f,
            FogColour = new Vector3(0.72f, 0.82f, 0.74f),
            Motes = AmbientMotes.Fireflies,
        };

        public static readonly BiomeDefinition Marsh = new BiomeDefinition
        {
            Id = "marsh",
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
            Canopy = List(S(NatureCatalog.PineDead, 2f), S(NatureCatalog.Birch, 1f)),
            Edge = List(S(NatureCatalog.PineDead)),
            Shrubs = List(S(NatureCatalog.Scrub)),
            Deadwood = List(S(NatureCatalog.FallenPine), S(NatureCatalog.Stump)),
            Boulders = List(S(NatureCatalog.Boulder, 0.4f)),
            ForestFloor = List(S(NatureCatalog.Reeds, 2f), S(NatureCatalog.Mushrooms, 1f)),
            OpenGround = List(S(NatureCatalog.Reeds, 3f), S(NatureCatalog.FlowerPurple, 0.3f)),
            Waterside = List(S(NatureCatalog.Reeds)),
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
            Canopy = List(S(NatureCatalog.PineSparse), S(NatureCatalog.PineDead, 0.3f)),
            Shrubs = List(S(NatureCatalog.Scrub)),
            Deadwood = List(S(NatureCatalog.Stump)),
            Boulders = List(S(NatureCatalog.Boulder, 2f), S(NatureCatalog.BrownRock, 1f)),
            OpenGround = List(S(NatureCatalog.Heather, 4f), S(NatureCatalog.Pebbles, 1f)),
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
            Canopy = List(S(NatureCatalog.PineFrosted, 3f), S(NatureCatalog.PineDead, 1f)),
            Deadwood = List(S(NatureCatalog.Stump), S(NatureCatalog.FallenPine, 0.5f)),
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
            Canopy = List(S(NatureCatalog.PineDead)),
            Shrubs = List(S(NatureCatalog.Scrub)),
            Boulders = List(S(NatureCatalog.BrownRock)),
            OpenGround = List(S(NatureCatalog.Pampas, 2f), S(NatureCatalog.Pebbles, 1f)),
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
            Canopy = List(S(NatureCatalog.PineDead)),
            Shrubs = List(S(NatureCatalog.Scrub)),
            Boulders = List(S(NatureCatalog.BrownRock)),
            OpenGround = List(S(NatureCatalog.Pebbles)),
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
            Canopy = List(S(NatureCatalog.PineCharred)),
            Deadwood = List(S(NatureCatalog.Stump)),
            Boulders = List(S(NatureCatalog.Boulder)),
            ShrubDensity = 0f,
            BoulderDensity = 0.6f,
            FloorDensity = 0f,
            OpenDensity = 0f,
            FogDensity = 0.05f,
            FogColour = new Vector3(0.5f, 0.42f, 0.38f),
            Motes = AmbientMotes.Embers,
        };

        public static readonly IReadOnlyList<BiomeDefinition> All = new[]
        {
            Meadow, GoldenSteppe, PineForest, BirchWood, CrimsonWood, BlossomGrove, BambooThicket, Marsh, Highland, Snowfield, Dunes, Badlands, Ashland,
        };
    }
}
