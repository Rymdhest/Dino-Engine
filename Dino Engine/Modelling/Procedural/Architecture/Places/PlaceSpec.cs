using System.Globalization;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// What a piece of a place with a story of its own is (PLACES.md). Each takes its own numbers, listed with it, all whole centimetres, degrees, per mille or choices, so that an id never
    /// holds a dot. Model +z is a piece's front, y = 0 where people stand at its foot, and what lies on the ground starts 0.15 m up, where the ground is drawn.
    /// </summary>
    public enum PlacePiece
    {
        // ---- A wreck (Ships.cs). A ship is one design from its seed (ShipDesign.Of), so its parts agree.
        Hull,           // A section of a ship's hull lying where it came ashore: part (0 the bow, 1 the stern), heel (degrees, + to starboard), sunk (cm), whose (1 the Reich's iron ship, 0 the land's own).
        Mast,           // A ship's mast fallen across the sand, its yards still on it and its sail torn: length, whose.
        Wreckage,       // What the sea took apart and threw up the beach: form (0 ribs standing out of the sand, 1 a run of planking, 2 a heap of timbers), length, whose.
        Anchor,         // An anchor dragged up the beach on its chain: chain length, whose.
        ShipBoat,       // A ship's boat, upturned on the sand: length, whose.
        BurstCage,      // One of the Reich's beast cages from a ship's hold, burst open from inside: width.
        Cargo,          // What came out of a hold: form (0 crates, 1 barrels, 2 amber in a split chest, 3 bales), count, whose.
        Driftwood,      // A bleached log the sea left: length.
        Salvage,        // The wreckers' sheerlegs and the tackle they drag cargo up with: height.

        // ---- A prison for beasts (Prison.cs). Black iron, oxblood and brass are the Reich's alone.
        Cage,           // A beast's cage of black iron bars on a stone footing, its door left out (it's a piece of its own): width, depth, height.
        CageDoor,       // A cage's door, hinged at x = 0 and closing along +x: width, height.
        BreakingPost,   // The post in a breaking yard a beast is chained to and goaded until it takes the collar, and the ring of mud it's trodden: radius.
        HarnessRack,    // A rack of the harness a broken beast is put in: collars, straps and a snout-mask.
        CollarForge,    // Where its collars are made: an anvil, a brazier and a quench tub.
        ChainStake,     // A stake with chains run out from it across the ground: length.

        // ---- The Amber Institute's outstation (Laboratory.cs).
        LabHall,        // Its house, of brick and black iron, with the blood-doctors' windows lit amber: width, depth.
        Hatchery,       // A glasshouse on an iron frame where eggs are warmed in sand: length, width.
        Vat,            // A vat the growing broth is kept in, glowing amber, banded in brass: height.
        Specimens,      // A rack of jars with what's growing, or failed to, in them.
        AmberBench,     // A bench where amber is cut and drilled for the blood in it, with the drills and the lamps.
        Pipes,          // Brass pipework from a vat into the house: length.

        // ---- The Ossuary's excavation (Excavation.cs).
        Fossil,         // One of the great beasts as it lies in the rock, in the death pose, partly freed: species (FossilForm), length, how much of it is freed (per cent).
        DigGrid,        // The strings of the grid the dig is measured by, on pegs: width, depth.
        Revetment,      // Boards shoring a pit's side: length, depth.
        Sieve,          // A sieve on legs the spoil is shaken through.
        Jackets,        // Bones jacketed in plaster for the journey, on a pallet: count.
        BoneCrates,     // Crates of bones packed in straw, stencilled with the Reich's mark.
        Spoil,          // A heap of what's been dug out: length.
        DrawingTable,   // A table under an awning where what's found is drawn and numbered.
        Hoist,          // A tripod of poles over the pit, a chain and a jacketed bone coming up on it: height.

        // ---- A nesting ground (Nests.cs).
        NestMound,      // A nest heaped up of earth and torn branches, its eggs in it: kind (NestKind), eggs, radius.
        Carcass,        // What's been dragged back to feed the young: form (0 a great beast's ribcage, 1 a smaller one's), length.
        Shells,         // Shells of eggs that hatched, broken, scattered.
        Web,            // A weaver's web strung between two points: width, height, form (0 an orb, 1 a sheet slung under one).
        EggSac,         // A weaver's egg sac hung from a branch: size.
        Cocoon,         // Something a weaver caught, wrapped up and hung: length.
        Wrap,           // Silk wound round a trunk a web is anchored to: the trunk's radius, how high it's wound.

        // ---- A logging camp (Logging.cs). Its wood is the wood round it (bark, per NestKind-like choice below).
        Stump,          // A tree's stump, its cut face pale and its bark the tree's: radius, height, bark (BarkKind, then its colour, three per mille).
        Log,            // A felled trunk, limbed: length, radius, bark (as a stump's).
        LogDeck,        // Logs decked at the landing, ends to the road: length, rows, bark (as a stump's).
        Sawpit,         // A sawpit: a trunk on trestles over a pit, the long saw in it.
        PlankStack,     // Sawn boards stacked to season with sticks between: length.
        ToolRack,       // Axes, saws and wedges on a rack, and a grindstone beside it.
        CharcoalClamp,  // A mound of wood under turf, smouldering into charcoal: radius.
        Skidder,        // A pair of high wheels a log is slung under to be dragged out: whose.

        // ---- A quarry (Quarry.cs). Its stone is the world's (Geology), as every stone in a land is.
        Face,           // A stepped face of cut stone, its benches up to the hill behind: width, rise of each bench, benches, shade, warmth.
        Block,          // A block of dressed stone: width, height, depth, shade, warmth, finish (0 split, 1 dressed).
        BlockStack,     // Blocks stacked to be carted: count, shade, warmth.
        Crane,          // A treadwheel crane with a block slung from its jib: height.
        Sledge,         // A block on a sledge on rollers: shade, warmth.
        Banker,         // A mason's banker with a block half worked on it, and the chips round it: shade, warmth.
        Rubble,         // A heap of spalls and chips: radius, shade, warmth.
        SplitLine,      // A block on the bench with wedges driven in along the line it'll split: length, shade, warmth.

        // ---- A fishing village (Fishing.cs).
        Jetty,          // A jetty of planks on posts out over the water: length, width, depth of water at its end (cm).
        Boat,           // A fishing boat: length, form (0 afloat, 1 drawn up on the shingle, 2 on its side).
        NetRack,        // Nets hung on poles to dry: length.
        FishRack,       // Fish split and hung to dry on a rack of poles: length.
        Smokehouse,     // A smokehouse of tarred boards with smoke from its roof.
        BoatStocks,     // A boat being built on the stocks, its ribs up and half its planks on: length.
        Pots,           // Creels and pots stacked by the water.
        Capstan,        // The capstan boats are hauled up the shingle by.

        // ---- Ruins of the people who raised the waygates (Ruins.cs), in the gates' own masonry.
        RuinWall,       // A stretch of wall standing to what's left of it: length, height, gap (0 none, 1 a doorway, 2 a window), shade, warmth.
        RuinColumn,     // A column standing to where it broke: height, fluted (0/1), shade, warmth.
        FallenColumn,   // A column lying where it fell, its drums rolled apart: length, shade, warmth.
        RuinArch,       // An arch still standing on its piers: span, shade, warmth.
        RuinSteps,      // A flight of steps up to nothing: width, rise, shade, warmth.
        RuneStone,      // A fallen block carved with the waygates' runes: shade, warmth.
        Statue,         // What's left of a figure: form (0 a head on its side, 1 a pair of feet on a plinth), shade, warmth.

        // ---- A trading post (Trading.cs).
        Scales,         // The post's beam balance, its pans and weights.
        Bales,          // Bales of wool and cloth, roped: count.
        Ledger,         // The turncoats' tally desk under an awning, the Reich's seal on its papers.
        HitchingRail,   // A rail to tie beasts to, and a trough: length.
        NoticeBoard,    // A board of notices under a little roof.

        // ---- A rearing farm (Farm.cs).
        FeedRack,       // A paddock's rack of hay over a trough: length.
        HatchingShed,   // A low shed where eggs are kept warm in straw over braziers: length.
        WaterTank,      // A trough fed from a cistern on legs.

        // ---- A castle of snow (Snow.cs).
        SnowWall,       // A wall of packed snow with merlons cut along its top: length, height.
        SnowTower,      // A tower of snow pressed out of buckets in tiers, a cone on top: radius, height, tiers.
        SnowGate,       // An arch of snow over the way in: width, height.
        Snowman,        // A snowman, of two balls or three: height, form.
        SnowLantern,    // A lantern of snowballs piled round a candle, as the north builds them: size.
        Sledges,        // Sledges, wooden spades and buckets the children left: count.
        Pennant,        // A pennant on a stick, stuck in the snow: height, colour.
        IceAltar,       // The frost cult's altar of ice, with the cold burning on it.

        // ---- Shared by anything (Kit.cs).
        Crates,         // Crates stacked: count, marked (1 with the Reich's mark).
        Barrels,        // Barrels, standing and lying: count.
        Tent,           // A ridge tent of canvas on poles, its flap open: length, whose.
        Campfire,       // A fire in a ring of stones, a pot over it on a tripod.
        Lantern,        // A lantern on a post: height, whose.
        Banner,         // The Reich's banner, as at a keep's gate, or a cloth of the land's: height, whose.
        Shelter,        // An open-fronted shelter of boards on posts, a lean-to roof: width, depth.
    }

    /// <summary>
    /// Everything it takes to build one piece of a place with a story, and its id, which is all that's kept: "place:hull:1f3a9c:0:-24:60:1" is a bow on its side, the Reich's. Ids have no dots
    /// and no hash, so that nothing reads one as a building's or a plant's. Read PLACES.md.
    /// </summary>
    public readonly struct PlaceSpec
    {
        public const string Prefix = "place:";

        public readonly PlacePiece Kind;
        public readonly uint Seed;
        private readonly int[] _values;

        public PlaceSpec(PlacePiece kind, uint seed, params int[] values)
        {
            Kind = kind;
            Seed = seed;
            _values = values;
        }

        public int Count => _values?.Length ?? 0;
        public int Int(int i) => _values != null && i < _values.Length ? _values[i] : 0;
        public float Metres(int i) => Int(i) / 100f;
        public float Share(int i) => Int(i) / 100f;
        public float Mille(int i) => Int(i) / 1000f;
        public float Radians(int i) => Int(i) * (MathF.PI / 180f);

        public string Id => $"{Prefix}{Kind.ToString().ToLowerInvariant()}:{Seed:x}:{string.Join(':', (_values ?? Array.Empty<int>()).Select(v => v.ToString(CultureInfo.InvariantCulture)))}";

        /// <summary>Metres as the whole centimetres an id keeps.</summary>
        public static int Cm(float metres) => (int)MathF.Round(metres * 100f);

        /// <summary>Radians as the whole degrees an id keeps.</summary>
        public static int Degrees(float radians) => (int)MathF.Round(radians * (180f / MathF.PI));

        /// <summary>A share, 0 to 1, as the per mille an id keeps.</summary>
        public static int PerMille(float share) => (int)MathF.Round(share * 1000f);

        public static bool IsPlace(string id) => id.StartsWith(Prefix, StringComparison.Ordinal);

        public static bool TryParse(string id, out PlaceSpec spec)
        {
            spec = default;
            if (!IsPlace(id)) return false;
            string[] parts = id.Substring(Prefix.Length).Split(':');
            if (parts.Length < 2 || !Enum.TryParse(parts[0], ignoreCase: true, out PlacePiece kind)) return false;
            if (!uint.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint seed)) return false;
            int count = parts.Length - 2;
            // A piece with no numbers writes an empty last part, which is no number at all.
            if (count == 1 && parts[2].Length == 0) count = 0;
            var values = new int[count];
            for (int i = 0; i < values.Length; i++)
            {
                if (!int.TryParse(parts[i + 2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out values[i])) return false;
            }
            spec = new PlaceSpec(kind, seed, values);
            return true;
        }
    }
}
