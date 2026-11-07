namespace Dino_Engine.World.Content
{
    /// <summary>The overall shape of a world's land.</summary>
    public enum LandformKind
    {
        Island,         // One landmass in open sea.
        Archipelago,    // Several islands joined by sand bars.
        Peninsula,      // Sea on one side, a wall of mountains on the other.
        Vale,           // Lowland ringed by mountains.
        Lakeland,       // A great lake in the middle, hills all around it.
        Highlands,      // Ridges and high ground, ringed by higher mountains.
        Marsh,          // Barely above the water table: pools everywhere.
        Badlands,       // Dry terraced mesas cut by flat valleys.
    }

    /// <summary>
    /// A kind of world. This holds no layout at all, only ranges: <see cref="WorldPlan.Create"/> draws concrete values from
    /// them for a seed, and the generation stages build everything else from those, so one archetype gives endless worlds.
    /// </summary>
    public sealed class WorldArchetype
    {
        public string Id = "";
        public string Name = "";
        public string[] Titles = { "{0}" };     // How the place is named; {0} is a generated name.
        public LandformKind Landform;

        public FloatRange Size = new FloatRange(1536f, 2048f);

        // --- Relief, in metres above the water ---
        public FloatRange BaseHeight = new FloatRange(6f, 10f);
        public FloatRange HillHeight = new FloatRange(10f, 20f);
        public FloatRange MountainHeight = new FloatRange(30f, 60f);
        public FloatRange MountainCoverage = new FloatRange(0.15f, 0.3f);   // Share of the land that rises into mountains.
        public FloatRange Cliffs = new FloatRange(0.1f, 0.35f);             // Share of the coast that is cliff instead of beach.
        public FloatRange Terraces = new FloatRange(0f, 0f);                // 0..1, how strongly heights step like mesas.
        public FloatRange Erosion = new FloatRange(0.7f, 1.1f);

        public float RimHeight = 0f;    // Mountains closing off the edge of the world, where the sea doesn't.
        public float RimWidth = 170f;

        /// <summary>The water at sea level is a still lake or mire, not an open sea with surf.</summary>
        public bool StillWater;

        // --- Biomes: the first few are likelier, and always at least <see cref="BiomeCount"/>.Min of them are drawn ---
        public BiomeDefinition[] Biomes = Array.Empty<BiomeDefinition>();
        public IntRange BiomeCount = new IntRange(3, 4);

        // --- Sites: multipliers on how many of each are placed for the world's size ---
        public float Settlements = 1f;
        public float Wilds = 1f;
        public float Dangers = 1f;
    }

    /// <summary>Every kind of world.</summary>
    public static class ArchetypeCatalog
    {
        public static readonly WorldArchetype Isle = new WorldArchetype
        {
            Id = "isle",
            Name = "Isle",
            Titles = new[] { "The Isle of {0}", "{0} Isle", "The Isle of {0}" },
            Landform = LandformKind.Island,
            Size = new FloatRange(1792f, 2304f),
            Biomes = new[] { BiomeCatalog.Meadow, BiomeCatalog.PineForest, BiomeCatalog.CrimsonWood, BiomeCatalog.GoldenSteppe, BiomeCatalog.BirchWood, BiomeCatalog.BlossomGrove, BiomeCatalog.Highland, BiomeCatalog.CedarWood, BiomeCatalog.GoldenWood },
            BiomeCount = new IntRange(3, 5),
        };

        public static readonly WorldArchetype Archipelago = new WorldArchetype
        {
            Id = "archipelago",
            Name = "Archipelago",
            Titles = new[] { "The {0} Isles", "The Shoals of {0}", "The {0} Reach" },
            Landform = LandformKind.Archipelago,
            Size = new FloatRange(2048f, 2560f),
            HillHeight = new FloatRange(8f, 16f),
            MountainHeight = new FloatRange(20f, 40f),
            MountainCoverage = new FloatRange(0.1f, 0.2f),
            Biomes = new[] { BiomeCatalog.BlossomGrove, BiomeCatalog.Meadow, BiomeCatalog.BambooThicket, BiomeCatalog.GoldenSteppe, BiomeCatalog.Dunes, BiomeCatalog.PineForest, BiomeCatalog.Jungle },
            BiomeCount = new IntRange(3, 4),
            Settlements = 0.9f,
        };

        public static readonly WorldArchetype Peninsula = new WorldArchetype
        {
            Id = "coast",
            Name = "Coast",
            Titles = new[] { "The {0} Coast", "The Cape of {0}", "{0} Strand" },
            Landform = LandformKind.Peninsula,
            Size = new FloatRange(1792f, 2304f),
            RimHeight = 75f,
            Biomes = new[] { BiomeCatalog.GoldenSteppe, BiomeCatalog.Meadow, BiomeCatalog.CrimsonWood, BiomeCatalog.PineForest, BiomeCatalog.Dunes, BiomeCatalog.Highland, BiomeCatalog.Savanna, BiomeCatalog.Jungle },
            BiomeCount = new IntRange(3, 4),
        };

        public static readonly WorldArchetype Vale = new WorldArchetype
        {
            Id = "vale",
            Name = "Forest Vale",
            Titles = new[] { "The Vale of {0}", "{0} Vale", "The Forest of {0}", "{0} Weald" },
            Landform = LandformKind.Vale,
            Size = new FloatRange(1536f, 2048f),
            BaseHeight = new FloatRange(8f, 13f),
            RimHeight = 85f,
            StillWater = true,
            Biomes = new[] { BiomeCatalog.PineForest, BiomeCatalog.BirchWood, BiomeCatalog.CrimsonWood, BiomeCatalog.Meadow, BiomeCatalog.BambooThicket, BiomeCatalog.RedwoodGrove, BiomeCatalog.CedarWood, BiomeCatalog.DarkWood, BiomeCatalog.GoldenWood },
            BiomeCount = new IntRange(3, 4),
            Wilds = 1.2f,
        };

        public static readonly WorldArchetype Lakeland = new WorldArchetype
        {
            Id = "lakeland",
            Name = "Lakeland",
            Titles = new[] { "{0} Mere", "The Lakes of {0}", "Lake {0}" },
            Landform = LandformKind.Lakeland,
            Size = new FloatRange(1792f, 2304f),
            RimHeight = 70f,
            StillWater = true,
            Biomes = new[] { BiomeCatalog.BirchWood, BiomeCatalog.PineForest, BiomeCatalog.Meadow, BiomeCatalog.CrimsonWood, BiomeCatalog.BlossomGrove, BiomeCatalog.GoldenWood, BiomeCatalog.RedwoodGrove },
            BiomeCount = new IntRange(3, 4),
        };

        public static readonly WorldArchetype Highlands = new WorldArchetype
        {
            Id = "highlands",
            Name = "Highlands",
            Titles = new[] { "The {0} Heights", "The {0} Fells", "{0} Pass" },
            Landform = LandformKind.Highlands,
            Size = new FloatRange(1792f, 2304f),
            BaseHeight = new FloatRange(14f, 22f),
            HillHeight = new FloatRange(14f, 26f),
            MountainHeight = new FloatRange(45f, 80f),
            MountainCoverage = new FloatRange(0.3f, 0.45f),
            RimHeight = 100f,
            StillWater = true,
            Biomes = new[] { BiomeCatalog.Highland, BiomeCatalog.PineForest, BiomeCatalog.Snowfield, BiomeCatalog.GoldenSteppe, BiomeCatalog.BirchWood, BiomeCatalog.CedarWood },
            BiomeCount = new IntRange(3, 4),
            Settlements = 0.75f,
            Dangers = 1.15f,
        };

        public static readonly WorldArchetype Frostmark = new WorldArchetype
        {
            Id = "frostmark",
            Name = "Frozen March",
            Titles = new[] { "The {0} Wastes", "{0} Hold", "The Frozen March of {0}" },
            Landform = LandformKind.Island,
            Size = new FloatRange(1792f, 2304f),
            MountainCoverage = new FloatRange(0.25f, 0.4f),
            Biomes = new[] { BiomeCatalog.Snowfield, BiomeCatalog.Highland, BiomeCatalog.PineForest, BiomeCatalog.DarkWood },
            BiomeCount = new IntRange(3, 3),
            Settlements = 0.7f,
            Dangers = 1.2f,
        };

        public static readonly WorldArchetype Mire = new WorldArchetype
        {
            Id = "mire",
            Name = "Mire",
            Titles = new[] { "The {0} Fens", "{0} Mire", "The Drowned Lands of {0}" },
            Landform = LandformKind.Marsh,
            Size = new FloatRange(1536f, 2048f),
            RimHeight = 60f,
            StillWater = true,
            Biomes = new[] { BiomeCatalog.Marsh, BiomeCatalog.BirchWood, BiomeCatalog.Meadow, BiomeCatalog.BambooThicket, BiomeCatalog.DarkWood },
            BiomeCount = new IntRange(2, 3),
            Settlements = 0.7f,
            Dangers = 1.2f,
        };

        public static readonly WorldArchetype Badlands = new WorldArchetype
        {
            Id = "badlands",
            Name = "Badlands",
            Titles = new[] { "The {0} Badlands", "The {0} Mesas", "The Red Reach of {0}" },
            Landform = LandformKind.Badlands,
            Size = new FloatRange(1792f, 2304f),
            BaseHeight = new FloatRange(10f, 16f),
            HillHeight = new FloatRange(10f, 16f),
            MountainHeight = new FloatRange(28f, 46f),
            MountainCoverage = new FloatRange(0.3f, 0.45f),
            Terraces = new FloatRange(0.6f, 0.95f),
            Erosion = new FloatRange(0.5f, 0.8f),
            RimHeight = 70f,
            StillWater = true,
            Biomes = new[] { BiomeCatalog.Badlands, BiomeCatalog.GoldenSteppe, BiomeCatalog.Dunes, BiomeCatalog.Highland, BiomeCatalog.Savanna },
            BiomeCount = new IntRange(2, 3),
            Settlements = 0.7f,
        };

        public static readonly WorldArchetype AshenIsle = new WorldArchetype
        {
            Id = "ashen",
            Name = "Ashen Isle",
            Titles = new[] { "The Ashes of {0}", "{0} Cinder", "The Burning Isle of {0}" },
            Landform = LandformKind.Island,
            Size = new FloatRange(1536f, 2048f),
            MountainCoverage = new FloatRange(0.3f, 0.4f),
            Biomes = new[] { BiomeCatalog.Ashland, BiomeCatalog.GoldenSteppe, BiomeCatalog.Highland, BiomeCatalog.Dunes, BiomeCatalog.DarkWood },
            BiomeCount = new IntRange(2, 3),
            Settlements = 0.6f,
            Dangers = 1.3f,
        };

        public static readonly IReadOnlyList<WorldArchetype> All = new[]
        {
            Isle, Archipelago, Peninsula, Vale, Lakeland, Highlands, Frostmark, Mire, Badlands, AshenIsle,
        };

        public static WorldArchetype Get(string id)
        {
            foreach (WorldArchetype archetype in All)
            {
                if (archetype.Id == id) return archetype;
            }
            throw new ArgumentException($"Unknown world archetype '{id}'.");
        }
    }
}
