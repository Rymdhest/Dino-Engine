namespace Dino_Engine.World.Content
{
    /// <summary>How a land's tyrant came to be: bred by the Amber Institute, or what the Ossuary made of one (LORE.md).</summary>
    public enum TyrantMake
    {
        Bred,       // Grown from blood in amber, and in its commander's regalia.
        Bones,      // Raised out of the rock by the Ossuary, with its cold light where the heart was.
        Rotting,    // One the Institute failed with, kept on its feet with iron and bile.
        Ghost,      // Called up in light, round bones too far gone to stand.
    }

    /// <summary>
    /// What a commander's seat is, where a land's tyrant waits (SEATS.md): each a place of its own with its own story, and a land has whichever its seed says. The Ossuary's three are where
    /// its three kinds of tyrant were made, so the place and the animal always tell the same story; the rest are the Reich's own, and hold a bred tyrant.
    /// </summary>
    public enum SeatKind
    {
        Citadel,    // The land's old castle, taken: its lord's statue pulled down, the Reich's iron on its walls, and the tyrant stabled against its hall.
        Hatchery,   // The Amber Institute's great hatchery, under a dome of glass: where this land's tyrant was grown, and the next one is growing.
        Foundry,    // The ironworks where the land's bells, ploughs and gates are melted down, and a tyrant's regalia is cast from them.
        Triumph,    // The Reich's monument to its conquest: a tyrant in black iron on a plinth of the land's broken stones, and the parade ground before it.
        Warcamp,    // A brood on the march: its war-wagons chained in a ring, its commander's pavilion, and the cage the tyrant travels in.
        Boneyard,   // The Ossuary's great dig, round the hollow in the rock a bone tyrant was raised out of.
        Bileworks,  // Where the Ossuary keeps a rotting tyrant on its feet: the vats of bile, the frame it's hooked into, and what it's fed.
        Barrow,     // A great barrow of the land's, opened: the tyrant's bones still lie in it, and its ghost walks the ring of cold fires that called it up.
    }

    /// <summary>
    /// Which seat a land has, and what its tyrant is, from its seed alone, so that the map table can tell of a land before it's grown, and the story, the place and the animal can never disagree
    /// (the game's <c>Chronicle.BossKindOf</c> reads <see cref="TyrantOf"/>). Headless.
    /// </summary>
    public static class SeatCatalog
    {
        // Seven tyrants in ten are bred, and one in ten each is what the Ossuary made of one (LORE.md).
        private const float BredShare = 0.7f, BonesShare = 0.1f, RottingShare = 0.1f;

        /// <summary>The Reich's own seats, which hold a bred tyrant, each as likely as the next. A new one goes on the end.</summary>
        public static readonly SeatKind[] OfTheReich = { SeatKind.Citadel, SeatKind.Hatchery, SeatKind.Foundry, SeatKind.Triumph, SeatKind.Warcamp };

        /// <summary>Every kind there is, in the order of <see cref="SeatKind"/>.</summary>
        public static readonly SeatKind[] All = Enum.GetValues<SeatKind>();

        /// <summary>What a land's tyrant is. The roll is the one the game has always made for it, so a seed's tyrant is what it was.</summary>
        public static TyrantMake TyrantOf(int seed)
        {
            float roll = new WorldRandom((ulong)(uint)seed).Fork("boss").Float();
            return roll < BredShare ? TyrantMake.Bred : roll < BredShare + BonesShare ? TyrantMake.Bones : roll < BredShare + BonesShare + RottingShare ? TyrantMake.Rotting : TyrantMake.Ghost;
        }

        /// <summary>Which seat a land's commander holds it from: the Ossuary's place for the Ossuary's tyrant, and otherwise one of the Reich's, on a fork of its own.</summary>
        public static SeatKind KindOf(int seed) => TyrantOf(seed) switch
        {
            TyrantMake.Bones => SeatKind.Boneyard,
            TyrantMake.Rotting => SeatKind.Bileworks,
            TyrantMake.Ghost => SeatKind.Barrow,
            _ => OfTheReich[new WorldRandom((ulong)(uint)seed).Fork("seat").Int(OfTheReich.Length)],
        };

        /// <summary>Whether a seat is the Ossuary's, held by the dead it raised, rather than the Reich's own, held by its greycoats and their beasts.</summary>
        public static bool IsOssuary(SeatKind kind) => kind is SeatKind.Boneyard or SeatKind.Bileworks or SeatKind.Barrow;

        /// <summary>How much room there is for a seat of a radius somewhere, 0 to 1: land a person can walk outside its three ways in, under its walls and where what it's built round stands.</summary>
        public static float RoomAt(WorldData world, OpenTK.Mathematics.Vector2 centre, float radius) => Generation.Seats.SeatBuilder.RoomAt(world, centre, radius);

        /// <summary>What the tyrant of a kind of seat is, for a seat that was chosen rather than drawn (the asset lexicon's).</summary>
        public static TyrantMake TyrantAt(SeatKind kind) => kind switch
        {
            SeatKind.Boneyard => TyrantMake.Bones,
            SeatKind.Bileworks => TyrantMake.Rotting,
            SeatKind.Barrow => TyrantMake.Ghost,
            _ => TyrantMake.Bred,
        };

        // What the Reich names each kind in its own tongue (PlaceNames.Reich): a castle it took keeps the land's name, as the land's people still say it.
        private static readonly string[] HatcheryEndings = { "wiege", "horst", "brut" };
        private static readonly string[] FoundryEndings = { "hammer", "schmiede", "esse", "werk" };
        private static readonly string[] TriumphEndings = { "mal", "thron", "wacht" };
        private static readonly string[] WarcampEndings = { "lager", "schanze", "wall" };
        private static readonly string[] BoneyardEndings = { "grube", "gruft", "bruch" };
        private static readonly string[] BileworksEndings = { "sud", "kessel", "galle" };

        /// <summary>A seat's name: the land's own for the castle the Reich took and the barrow it opened, and the Reich's for what it built.</summary>
        public static string NameOf(SeatKind kind, int worldSeed, WorldRandom random) => kind switch
        {
            SeatKind.Citadel => "Fort " + PlaceNames.Root(worldSeed, random),
            SeatKind.Hatchery => PlaceNames.Reich(random, HatcheryEndings),
            SeatKind.Foundry => PlaceNames.Reich(random, FoundryEndings),
            SeatKind.Triumph => PlaceNames.Reich(random, TriumphEndings),
            SeatKind.Warcamp => PlaceNames.Reich(random, WarcampEndings),
            SeatKind.Boneyard => PlaceNames.Reich(random, BoneyardEndings),
            SeatKind.Bileworks => PlaceNames.Reich(random, BileworksEndings),
            _ => PlaceNames.Root(worldSeed, random) + " Barrow",
        };
    }
}
