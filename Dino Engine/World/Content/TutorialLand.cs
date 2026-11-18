using OpenTK.Mathematics;

namespace Dino_Engine.World.Content
{
    /// <summary>
    /// The tutorial: the one land every new hero starts in, before they've ever seen the Haven, and the same land every time, so that it can be tuned until it's the start a new player
    /// should have. It's one way through, a gorge of four clearings laid out here by hand rather than drawn: where heroes land, two places held by the Reich to free, and at the end of the way
    /// a clearing where one Rare dinosaur waits with a chest beside it and a dark waygate behind it, which opens once the beast is down and goes to the Haven. There's no keep, tyrant,
    /// village, shrine or other waygate in it, and nobody can call the waygates there (TUTORIAL.md).
    /// <para>
    /// Everything that makes it what it is is here, to tune: its seed, its size, its clearings and passes, and which place stands in which clearing. The beast, and what's said, are the game's
    /// (<c>Tutorial</c> in Dino Defenders). Changing anything here changes the land everyone starts in, which is the point, and a hero saved halfway through it comes back to the new one.
    /// </para>
    /// </summary>
    public static class TutorialLand
    {
        /// <summary>Its kind of world's id, which a request names it by.</summary>
        public const string Id = "tutorial";

        /// <summary>The seed it's always grown from: its name, biomes, mood, terrain and everything on it come from this and nothing else.</summary>
        public const int Seed = 4127;

        /// <summary>Metres a side: a little over half an ordinary land's, as it's four clearings and nothing besides.</summary>
        public const float Size = 640f;

        /// <summary>
        /// The clearings, from where heroes land to the beast's, in metres from the world's corner (+x is to the left on screen and +z up it, away from the camera, which looks north). The way
        /// bends north-east and then north, so that what's ahead is up the screen and the beast's gate stands behind it from where the camera is.
        /// </summary>
        public static readonly LandPath.Room[] Rooms =
        {
            new(new Vector2(170f, 150f), 72f),     // Where heroes land.
            new(new Vector2(318f, 212f), 62f),     // The first place to free.
            new(new Vector2(432f, 330f), 64f),     // The second.
            new(new Vector2(462f, 478f), 78f),     // The beast's clearing, with its chest and its waygate.
        };

        /// <summary>The half-width of each pass, from one clearing to the next, in metres.</summary>
        public static readonly float[] Passes = { 22f, 20f, 24f };

        /// <summary>Which kind of place stands in which clearing, by its index in <see cref="Rooms"/>. Every one is held, and freeing them is one of what the land asks.</summary>
        public static readonly (int Room, SiteDefinition Place)[] Places =
        {
            (1, SiteCatalog.Ruins),
            (2, SiteCatalog.BanditCamp),
        };

        /// <summary>How much further apart packs stand between its places than in an ordinary land: a new player meets a few, and not one round every bend.</summary>
        public const float PackSpacing = 1.7f;

        /// <summary>The way through it, as <see cref="Rooms"/> and <see cref="Passes"/> lay it out.</summary>
        public static LandPath Way => LandPath.Laid(Rooms, Passes);

        /// <summary>
        /// Its kind of world: a vale in summer, walled by its gorge, with meadow and birch along the way. Never drawn from a seed, as it isn't in <see cref="ArchetypeCatalog.All"/>, and always the same
        /// light and weather, so that the first land anyone sees looks as it was meant to.
        /// </summary>
        public static readonly WorldArchetype Archetype = new WorldArchetype
        {
            Id = Id,
            Name = "Landing Vale",
            Titles = new[] { "The Vale of {0}" },
            Landform = LandformKind.Vale,
            Size = new FloatRange(Size, Size),
            BaseHeight = new FloatRange(8f, 10f),
            HillHeight = new FloatRange(6f, 9f),
            MountainHeight = new FloatRange(30f, 40f),
            MountainCoverage = new FloatRange(0.15f, 0.2f),
            RimHeight = 85f,
            StillWater = true,
            Biomes = new[] { BiomeCatalog.Meadow, BiomeCatalog.BirchWood },
            BiomeCount = new IntRange(2, 2),
            Settlements = 0f,
            Wilds = 0f,
            Dangers = 0f,
            Tutorial = true,
            PackSpacing = PackSpacing,
            Light = "Clear morning",
            SunAzimuth = 3.9f,
            SunHeight = 0.55f,
            Weather = new WeatherSample { Gale = 0.8f },
        };

        /// <summary>Whether a request is for the tutorial.</summary>
        public static bool Is(in WorldRequest request) => request.ArchetypeId == Id;
    }
}
