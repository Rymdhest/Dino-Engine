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

        /// <summary>
        /// How large a land of this kind usually is, in metres a side, and the most it can be is <see cref="Size"/>, which only orders asking for a larger land reach. Measured to leave each kind about
        /// the same ground to walk, about 0.3 km2, as water, mountains and coasts don't shrink with a land: a kind with more of them is larger. Nothing here means its <see cref="Size"/> is all it is.
        /// </summary>
        public FloatRange UsualSize;

        // --- Relief, in metres above the water ---
        public FloatRange BaseHeight = new FloatRange(6f, 10f);
        public FloatRange HillHeight = new FloatRange(10f, 20f);
        public FloatRange MountainHeight = new FloatRange(30f, 60f);
        public FloatRange MountainCoverage = new FloatRange(0.15f, 0.3f);   // Share of the land that rises into mountains.
        public FloatRange Cliffs = new FloatRange(0.1f, 0.35f);             // Share of the coast that is cliff instead of beach.
        public FloatRange Terraces = new FloatRange(0f, 0f);                // 0..1, how strongly heights step like mesas.
        public FloatRange Erosion = new FloatRange(0.7f, 1.1f);

        public RockKind? Bedrock;       // What the land stands on, if that's what this kind of world is known for. Otherwise it goes by the biomes.

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

        /// <summary>
        /// A world of one village and nobody hostile: the Haven. It has no landing, keep or packs, and its one place is <see cref="SiteKind.Haven"/>, built
        /// by <see cref="Generation.HavenBuilder"/>. Never drawn from a seed, as it isn't in <see cref="ArchetypeCatalog.All"/>.
        /// </summary>
        public bool Peaceful;

        /// <summary>
        /// A world that's nothing but one arena, where a land's boss is fought every tenth level, and which kind: its floor sealed round, its stands, and what takes part in the fight, built by
        /// <see cref="Generation.ArenaBuilder"/>. Never drawn from a seed: they're in <see cref="ArenaCatalog"/>, as biomes are. Read ARENAS.md.
        /// </summary>
        public ArenaKind? ArenaKind;

        /// <summary>Whether it's a boss map, nothing but one arena.</summary>
        public bool Arena => ArenaKind.HasValue;

        /// <summary>A world of one place and nothing else, which is either of the two above.</summary>
        public bool Enclosed => Peaceful || Arena;

        /// <summary>The light it's always seen in, by name, and where the sun stands, in place of what's drawn from the seed. Only for a place that has to look one way.</summary>
        public string? Light;
        public float SunAzimuth;
        public float SunHeight;

        /// <summary>
        /// A world whose sun goes round (<see cref="DayCycle"/>) and whose weather runs on its own, through every mood a day can have, rather than coming from the climate
        /// (<see cref="WeatherField"/>): the Haven, which is visited often and never twice the same. Its <see cref="Light"/> is where the sun goes down.
        /// </summary>
        public bool DayAndNight;

        /// <summary>
        /// The weather it's always seen in, in place of what its climate would bring: an arena's, which is one place seen for one fight, and has to look as it was meant to (ARENAS.md). The same on
        /// every machine, as the weather is a pure function of the world.
        /// </summary>
        public WeatherSample? Weather;
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
            UsualSize = new FloatRange(830f, 960f),
            Biomes = new[] { BiomeCatalog.Meadow, BiomeCatalog.PineForest, BiomeCatalog.CrimsonWood, BiomeCatalog.GoldenSteppe, BiomeCatalog.BirchWood, BiomeCatalog.BlossomGrove, BiomeCatalog.Highland, BiomeCatalog.CedarWood, BiomeCatalog.GoldenWood, BiomeCatalog.Wildwood, BiomeCatalog.FlowerDowns, BiomeCatalog.WindwardCoast, BiomeCatalog.CypressHills },
            BiomeCount = new IntRange(3, 5),
        };

        public static readonly WorldArchetype Archipelago = new WorldArchetype
        {
            Id = "archipelago",
            Name = "Archipelago",
            Titles = new[] { "The {0} Isles", "The Shoals of {0}", "The {0} Reach" },
            Landform = LandformKind.Archipelago,
            Size = new FloatRange(2048f, 2560f),
            UsualSize = new FloatRange(1090f, 1260f),
            HillHeight = new FloatRange(8f, 16f),
            MountainHeight = new FloatRange(20f, 40f),
            MountainCoverage = new FloatRange(0.1f, 0.2f),
            Biomes = new[] { BiomeCatalog.BlossomGrove, BiomeCatalog.Meadow, BiomeCatalog.BambooThicket, BiomeCatalog.GoldenSteppe, BiomeCatalog.Dunes, BiomeCatalog.PineForest, BiomeCatalog.Jungle, BiomeCatalog.Fernland, BiomeCatalog.Tidewood, BiomeCatalog.CypressHills, BiomeCatalog.WindwardCoast, BiomeCatalog.Mistwood },
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
            UsualSize = new FloatRange(900f, 1050f),
            RimHeight = 75f,
            Biomes = new[] { BiomeCatalog.GoldenSteppe, BiomeCatalog.Meadow, BiomeCatalog.CrimsonWood, BiomeCatalog.PineForest, BiomeCatalog.Dunes, BiomeCatalog.Highland, BiomeCatalog.Savanna, BiomeCatalog.Jungle, BiomeCatalog.WindwardCoast, BiomeCatalog.CypressHills, BiomeCatalog.RedScrub, BiomeCatalog.Fernland, BiomeCatalog.Tidewood, BiomeCatalog.FlowerDowns },
            BiomeCount = new IntRange(3, 4),
        };

        public static readonly WorldArchetype Vale = new WorldArchetype
        {
            Id = "vale",
            Name = "Forest Vale",
            Titles = new[] { "The Vale of {0}", "{0} Vale", "The Forest of {0}", "{0} Weald" },
            Landform = LandformKind.Vale,
            Size = new FloatRange(1536f, 2048f),
            UsualSize = new FloatRange(870f, 1010f),
            BaseHeight = new FloatRange(8f, 13f),
            RimHeight = 85f,
            StillWater = true,
            Biomes = new[] { BiomeCatalog.PineForest, BiomeCatalog.BirchWood, BiomeCatalog.CrimsonWood, BiomeCatalog.Meadow, BiomeCatalog.BambooThicket, BiomeCatalog.RedwoodGrove, BiomeCatalog.CedarWood, BiomeCatalog.DarkWood, BiomeCatalog.GoldenWood, BiomeCatalog.Wildwood, BiomeCatalog.ElderPines, BiomeCatalog.Larchwood, BiomeCatalog.Mistwood, BiomeCatalog.WaterMeadows },
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
            UsualSize = new FloatRange(1040f, 1200f),
            RimHeight = 70f,
            StillWater = true,
            Biomes = new[] { BiomeCatalog.BirchWood, BiomeCatalog.PineForest, BiomeCatalog.Meadow, BiomeCatalog.CrimsonWood, BiomeCatalog.BlossomGrove, BiomeCatalog.GoldenWood, BiomeCatalog.RedwoodGrove, BiomeCatalog.WaterMeadows, BiomeCatalog.Wildwood, BiomeCatalog.Larchwood, BiomeCatalog.FlowerDowns, BiomeCatalog.ElderPines },
            BiomeCount = new IntRange(3, 4),
        };

        public static readonly WorldArchetype Highlands = new WorldArchetype
        {
            Id = "highlands",
            Name = "Highlands",
            Titles = new[] { "The {0} Heights", "The {0} Fells", "{0} Pass" },
            Landform = LandformKind.Highlands,
            Size = new FloatRange(1792f, 2304f),
            UsualSize = new FloatRange(880f, 1020f),
            BaseHeight = new FloatRange(14f, 22f),
            HillHeight = new FloatRange(14f, 26f),
            MountainHeight = new FloatRange(45f, 80f),
            MountainCoverage = new FloatRange(0.3f, 0.45f),
            RimHeight = 100f,
            StillWater = true,
            Biomes = new[] { BiomeCatalog.Highland, BiomeCatalog.PineForest, BiomeCatalog.Snowfield, BiomeCatalog.GoldenSteppe, BiomeCatalog.BirchWood, BiomeCatalog.CedarWood, BiomeCatalog.HighPastures, BiomeCatalog.Larchwood, BiomeCatalog.Bogland, BiomeCatalog.Mistwood },
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
            UsualSize = new FloatRange(850f, 990f),
            MountainCoverage = new FloatRange(0.25f, 0.4f),
            Biomes = new[] { BiomeCatalog.Snowfield, BiomeCatalog.Highland, BiomeCatalog.PineForest, BiomeCatalog.DarkWood, BiomeCatalog.Larchwood, BiomeCatalog.Bogland, BiomeCatalog.HighPastures },
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
            UsualSize = new FloatRange(920f, 1080f),
            RimHeight = 60f,
            StillWater = true,
            Biomes = new[] { BiomeCatalog.Marsh, BiomeCatalog.BirchWood, BiomeCatalog.Meadow, BiomeCatalog.BambooThicket, BiomeCatalog.DarkWood, BiomeCatalog.Tidewood, BiomeCatalog.WaterMeadows, BiomeCatalog.Bogland, BiomeCatalog.Mistwood },
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
            Bedrock = RockKind.Sandstone,
            Size = new FloatRange(1792f, 2304f),
            UsualSize = new FloatRange(880f, 1030f),
            BaseHeight = new FloatRange(10f, 16f),
            HillHeight = new FloatRange(10f, 16f),
            MountainHeight = new FloatRange(28f, 46f),
            MountainCoverage = new FloatRange(0.3f, 0.45f),
            Terraces = new FloatRange(0.6f, 0.95f),
            Erosion = new FloatRange(0.5f, 0.8f),
            RimHeight = 70f,
            StillWater = true,
            Biomes = new[] { BiomeCatalog.Badlands, BiomeCatalog.GoldenSteppe, BiomeCatalog.Dunes, BiomeCatalog.Highland, BiomeCatalog.Savanna, BiomeCatalog.RedScrub, BiomeCatalog.CypressHills },
            BiomeCount = new IntRange(2, 3),
            Settlements = 0.7f,
        };

        public static readonly WorldArchetype AshenIsle = new WorldArchetype
        {
            Id = "ashen",
            Name = "Ashen Isle",
            Titles = new[] { "The Ashes of {0}", "{0} Cinder", "The Burning Isle of {0}" },
            Landform = LandformKind.Island,
            Bedrock = RockKind.Basalt,
            Size = new FloatRange(1536f, 2048f),
            UsualSize = new FloatRange(850f, 980f),
            MountainCoverage = new FloatRange(0.3f, 0.4f),
            Biomes = new[] { BiomeCatalog.Ashland, BiomeCatalog.GoldenSteppe, BiomeCatalog.Highland, BiomeCatalog.Dunes, BiomeCatalog.DarkWood, BiomeCatalog.Fernland, BiomeCatalog.ElderPines, BiomeCatalog.Mistwood },
            BiomeCount = new IntRange(2, 3),
            Settlements = 0.6f,
            Dangers = 1.3f,
        };

        /// <summary>
        /// The Haven: a small isle of blossom and gold in the last light of the day, with one village on it and the great waygate above the village. It's a world like any other, grown from its own seed,
        /// so nothing about it is drawn by hand, but its seed never changes, so it's the same place every time a hero comes home.
        /// </summary>
        public static readonly WorldArchetype Haven = new WorldArchetype
        {
            Id = "haven",
            Name = "Haven",
            Titles = new[] { "The Haven" },
            Landform = LandformKind.Island,
            Size = new FloatRange(416f, 416f),     // Little more than the village and the woods round it, so that nobody goes looking for a fight that isn't there.
            BaseHeight = new FloatRange(5f, 6f),
            HillHeight = new FloatRange(7f, 9f),
            MountainHeight = new FloatRange(24f, 28f),
            MountainCoverage = new FloatRange(0.1f, 0.12f),
            Cliffs = new FloatRange(0.35f, 0.45f),
            Erosion = new FloatRange(0.8f, 0.9f),
            Biomes = new[] { BiomeCatalog.BlossomGrove, BiomeCatalog.GoldenWood, BiomeCatalog.Meadow },
            BiomeCount = new IntRange(3, 3),
            Settlements = 0f,
            Wilds = 0f,
            Dangers = 0f,
            Peaceful = true,
            Light = "Golden evening",
            SunAzimuth = 2.45f,
            SunHeight = 0.3f,
            DayAndNight = true,
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
            if (id == Haven.Id) return Haven;
            foreach (WorldArchetype arena in ArenaCatalog.All)
            {
                if (arena.Id == id) return arena;
            }
            throw new ArgumentException($"Unknown world archetype '{id}'.");
        }
    }

    /// <summary>
    /// Every kind of arena, where a land's boss is fought every tenth level, as there are kinds of world for every other land. A kind is a world of one place: what it looks like is its builder's
    /// (<see cref="Generation.ArenaBuilder"/>), and what's here is the land it stands in: an isle, its light, its stone and what grows round it. More kinds go on the end. Read ARENAS.md.
    /// </summary>
    public static class ArenaCatalog
    {
        // Every arena stands on a small isle: enough that its heart is well in from the shore, with room for the whole of it, and level ground to dig into.
        private static WorldArchetype Isle(ArenaKind kind, string id, string name, string[] titles, BiomeDefinition[] biomes, RockKind? rock, string light, float azimuth, float height, WeatherSample weather) => new WorldArchetype
        {
            Id = id,
            Name = name,
            Titles = titles,
            Landform = LandformKind.Island,
            Size = new FloatRange(384f, 384f),
            BaseHeight = new FloatRange(13f, 14f),
            HillHeight = new FloatRange(3f, 4f),
            MountainHeight = new FloatRange(10f, 12f),
            MountainCoverage = new FloatRange(0f, 0f),
            Cliffs = new FloatRange(0.3f, 0.45f),
            Erosion = new FloatRange(0.7f, 0.8f),
            Bedrock = rock,
            Biomes = biomes,
            BiomeCount = new IntRange(2, 3),
            Settlements = 0f,
            Wilds = 0f,
            Dangers = 0f,
            ArenaKind = kind,
            Light = light,
            SunAzimuth = azimuth,
            SunHeight = height,
            Weather = weather,
        };

        private static readonly WeatherSample Fair = new WeatherSample { Gale = 1f };

        /// <summary>The Reich's oldest kind: a round pit dug into a hill, its floor sand, a sheer wall round it and three tiers of stone stands, in the glare of high summer.</summary>
        public static readonly WorldArchetype Pit = Isle(ArenaKind.Pit, "pit", "The Pit", new[] { "The Pit of {0}", "{0} Pit", "The Blood Pit of {0}" },
            new[] { BiomeCatalog.GoldenSteppe, BiomeCatalog.Savanna, BiomeCatalog.Badlands, BiomeCatalog.Meadow }, RockKind.Sandstone, "High summer", 4.2f, 0.82f, Fair);

        /// <summary>The Reich's great amphitheatre: an oval of sand under a cavea of pale stone seats, its people packed in, lifts under the floor and bolt-throwers in the commander's box, at evening.</summary>
        public static readonly WorldArchetype Colosseum = Isle(ArenaKind.Colosseum, "colosseum", "The Colosseum", new[] { "The Colosseum of {0}", "{0} Colosseum", "The Great Arena of {0}" },
            new[] { BiomeCatalog.Meadow, BiomeCatalog.GoldenSteppe, BiomeCatalog.CedarWood, BiomeCatalog.Savanna }, RockKind.Limestone, "Golden evening", 3.75f, 0.3f, new WeatherSample { Cloud = 0.2f, Gale = 0.9f });

        /// <summary>The Amber Institute's breeding cage: black iron bars round a pen of mud and straw, the brood's pens round it and its eggs in the straw, under a grey, close sky.</summary>
        public static readonly WorldArchetype Cage = Isle(ArenaKind.Cage, "cage", "The Jurassic Cage", new[] { "The Jurassic Cage of {0}", "The Cage at {0}", "The Breeding Cage of {0}" },
            new[] { BiomeCatalog.Jungle, BiomeCatalog.Marsh, BiomeCatalog.BambooThicket, BiomeCatalog.DarkWood }, RockKind.Granite, "Grey overcast", 5.6f, 0.62f, new WeatherSample { Cloud = 0.6f, Mist = 0.12f, Wet = 0.2f, Gale = 0.7f });

        /// <summary>A temple of the land's own sun the Reich made its seat of assembly: a sunken court of golden stone, its great disc, and the brass mirrors that burn the floor, under a sun near overhead.</summary>
        public static readonly WorldArchetype SunTemple = Isle(ArenaKind.SunTemple, "suntemple", "The Sun-Temple", new[] { "The Sun-Temple of {0}", "The Sun-Temple at {0}" },
            new[] { BiomeCatalog.Savanna, BiomeCatalog.Dunes, BiomeCatalog.GoldenSteppe, BiomeCatalog.Badlands }, RockKind.Sandstone, "High summer", 4.71f, 0.9f, new WeatherSample { Gale = 0.8f });

        /// <summary>The Ossuary's dig where the Reich's first charge on the land was buried in ash: a quarry bowl, the charge turned to stone on its floor, under a grey sky.</summary>
        public static readonly WorldArchetype Onslaught = Isle(ArenaKind.Onslaught, "onslaught", "The Fossilized Onslaught", new[] { "The Fossilized Onslaught of {0}", "The Fossil Field of {0}", "The Ash Field of {0}" },
            new[] { BiomeCatalog.Ashland, BiomeCatalog.Highland, BiomeCatalog.Badlands, BiomeCatalog.DarkWood }, RockKind.Basalt, "Grey overcast", 4.4f, 0.6f, new WeatherSample { Cloud = 0.85f, Fall = 0.3f, Ash = 1f, Mist = 0.2f, Gale = 1.1f });

        /// <summary>The Cathedral of the Iron Apex: a nave of dark stone under ribs of black iron, iron wheels of candles over it and windows of oxblood and amber, always seen by night.</summary>
        public static readonly WorldArchetype Cathedral = Isle(ArenaKind.Cathedral, "cathedral", "The Cathedral of the Iron Apex", new[] { "The Cathedral of the Iron Apex at {0}", "The Iron Apex of {0}" },
            new[] { BiomeCatalog.DarkWood, BiomeCatalog.PineForest, BiomeCatalog.Highland, BiomeCatalog.CedarWood }, RockKind.Granite, "Golden evening", 3.9f, 0.18f, new WeatherSample { Cloud = 0.3f, Gale = 0.8f });

        public static readonly IReadOnlyList<WorldArchetype> All = new[] { Pit, Colosseum, Cage, SunTemple, Onslaught, Cathedral };

        public static bool Is(string? id) => id != null && All.Any(arena => arena.Id == id);

        /// <summary>The arena of a kind.</summary>
        public static WorldArchetype Of(ArenaKind kind) => All.First(arena => arena.ArenaKind == kind);
    }
}
