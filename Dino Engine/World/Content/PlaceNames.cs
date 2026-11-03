namespace Dino_Engine.World.Content
{
    /// <summary>
    /// Generates place names. A world's places share a sound: its seed picks one language of syllables, and names are built
    /// from those plus endings that say what the place is, so a lake ends in "mere" and a wood in "holt".
    /// </summary>
    public static class PlaceNames
    {
        private sealed record Language(string[] Starts, string[] Middles, string[] Ends);

        private static readonly Language[] Languages =
        {
            // Old northern
            new Language(
                new[] { "Ash", "Bar", "Bel", "Cal", "Dun", "Eld", "Fen", "Gar", "Hal", "Ith", "Kel", "Lor", "Mor", "Nor", "Os", "Ran", "Sel", "Thor", "Ul", "Vor", "Wyr", "Yar" },
                new[] { "a", "e", "i", "o", "ar", "en", "il", "or" },
                new[] { "ath", "den", "dor", "dun", "gard", "grim", "heim", "lin", "mar", "nor", "rath", "ric", "stead", "wen" }),
            // Soft eastern
            new Language(
                new[] { "Aki", "Asa", "Hana", "Hiro", "Iso", "Kaze", "Kiri", "Kuro", "Mina", "Mizu", "Naga", "Oka", "Rin", "Saku", "Shiro", "Taka", "Tsu", "Yama", "Yuki" },
                new[] { "", "", "ra", "no", "ka", "mi", "shi", "to" },
                new[] { "gawa", "hama", "jima", "kawa", "mori", "mura", "saki", "shima", "tani", "yama", "zaka" }),
            // Weathered western
            new Language(
                new[] { "Aber", "Bryn", "Caer", "Dol", "Glyn", "Llan", "Mael", "Pen", "Rhos", "Tal", "Tre", "Ystr" },
                new[] { "", "a", "e", "wy", "y" },
                new[] { "ach", "an", "dwr", "fan", "gwyn", "llyn", "maen", "rhys", "wen", "ydd" }),
        };

        private static readonly string[] VillageEndings = { "wick", "ham", "ton", "by", "stead", "thorpe", "ford" };
        private static readonly string[] WoodEndings = { "holt", "wood", "shaw", "hurst" };
        private static readonly string[] HeightEndings = { "crag", "fell", "tor", "peak", "watch" };
        private static readonly string[] WaterEndings = { "mere", "water", "pool", "tarn" };

        public static string WorldName(WorldArchetype archetype, int worldSeed, WorldRandom random)
        {
            string title = random.Pick(archetype.Titles);
            return string.Format(title, Root(LanguageOf(worldSeed), random));
        }

        /// <summary>A name in the language of the world with <paramref name="worldSeed"/>.</summary>
        public static string Settlement(int worldSeed, WorldRandom random)
        {
            Language language = LanguageOf(worldSeed);
            if (random.Chance(0.45f))
            {
                string start = random.Pick(language.Starts);
                return start + random.Pick(VillageEndings);
            }
            return Root(language, random);
        }

        public static string Wood(int worldSeed, WorldRandom random) => random.Pick(LanguageOf(worldSeed).Starts) + random.Pick(WoodEndings);
        public static string Height(int worldSeed, WorldRandom random) => random.Pick(LanguageOf(worldSeed).Starts) + random.Pick(HeightEndings);
        public static string Water(int worldSeed, WorldRandom random) => random.Pick(LanguageOf(worldSeed).Starts) + random.Pick(WaterEndings);

        /// <summary>A bare name in the language of the world with <paramref name="worldSeed"/>, for people and old things.</summary>
        public static string Root(int worldSeed, WorldRandom random) => Root(LanguageOf(worldSeed), random);

        public static int LanguageIndex(int worldSeed)
        {
            return new WorldRandom(worldSeed).Fork("language").Int(Languages.Length);
        }

        private static Language LanguageOf(int worldSeed) => Languages[LanguageIndex(worldSeed)];

        private static string Root(Language language, WorldRandom random)
        {
            string start = random.Pick(language.Starts);
            string middle = random.Pick(language.Middles);
            string end = random.Pick(language.Ends);
            return start + middle + end;
        }
    }
}
