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
        SilkFloor,      // A weaver's floor spun over the ground: radius, form (0 a sheet at a trunk's roots, 1 its lair's, round the dark mouth of its burrow).

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
        Face,           // A stepped face of cut stone, its benches up to the hill behind: width, height, benches, ledge, how far its top runs back past its lip, stone (r, g, b per mille).
        Block,          // A block of the land's stone: width, height, depth, stone (r, g, b), finish (0 split, 1 dressed), daubed for the Reich (0, 1).
        BlockStack,     // Blocks stacked on bearers to be carted: count, stone (r, g, b).
        Crane,          // A treadwheel crane with a block slung from its jib: height, stone (r, g, b).
        Sledge,         // A block roped on a sledge on rollers: stone (r, g, b).
        Banker,         // A mason's banker with a block half worked on it, and the chips round it: stone (r, g, b).
        Rubble,         // A heap of spalls and chips: radius, stone (r, g, b).
        SplitLine,      // A block on the floor with wedges driven in along the line it'll split on: length, stone (r, g, b).

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
        FarmGate,       // A paddock's timber gate, hinged at x = 0 and shutting along +x, chained by the Reich: width, height.

        // ---- A castle of snow (Snow.cs).
        SnowWall,       // A wall of packed snow with merlons cut along its top: length, height.
        SnowTower,      // A tower of snow pressed out of buckets in tiers, a cone on top: radius, height, tiers.
        SnowGate,       // An arch of snow over the way in: width, height.
        Snowman,        // A snowman, of two balls or three: height, form.
        SnowLantern,    // A lantern of snowballs piled round a candle, as the north builds them: size.
        Sledges,        // Sledges, wooden spades and buckets the children left: count.
        Pennant,        // A pennant on a stick, stuck in the snow: height, colour (one of the land's, or 9 the frost cult's violet, longer).
        IceAltar,       // The frost cult's altar of ice, with the cold burning on it.

        // ---- A shrine of the land's (Shrine.cs), its altar the catalog's, which a hero kindles.
        ShrineHall,     // The hall behind the altar, open to it: width, depth, roof (0 hipped, 1 gabled, 2 eaves swept up), damage (0 to 2), stone (r, g, b).
        ShrineGate,     // The gateway its path comes in by, hung with ribbons and bells: span, height, stone (r, g, b).
        ShrineLantern,  // A tall lantern of stone, kept out: height, form (0 standing, 1 knocked down, 2 its roof knocked off), stone (r, g, b).
        Offerings,      // The table offerings are left on: form (0 kept, 1 kicked over), stone (r, g, b).
        Basin,          // A basin cut from one block with its spout and ladles: stone (r, g, b).
        Bell,           // The shrine's bell in its frame: form (0 hung, 1 cut down and lying cracked).
        Ribbons,        // Ribbons and tokens wound round a trunk: radius, how high.
        Cairns,         // Little cairns of stones left by whoever came: count, spread, stone (r, g, b).
        PrayerLine,     // Flags of the land's colours strung on a line along +z: length, height at its start, at its end, sag, a pole at its end (0, 1), at its start (0, 1).

        // ---- A ring of old stones (Stones.cs), held by one of the land's cults (Cult).
        Menhir,         // A standing stone of the land's rock: height, width, form (0 plain, 1 carved and humming, 2 broken, 3 fallen), lean (degrees), stone (r, g, b).
        Trilithon,      // Two stones with a lintel across them: height, span, form (0 standing, 1 its lintel fallen), stone (r, g, b).
        Barrow,         // The barrow the ring was raised over, its kerb and portal: length, width, height, opened by the Ossuary (0, 1), stone (r, g, b).
        Totem,          // A totem of the cult that holds the stones: height, cult (Cult).
        CultHeart,      // The cult's heart in the ring's middle, its fire, ice, struck stone or black pool: cult, radius.

        // ---- A watchtower of the land's (Beacon.cs), its beacon cold since the ships came.
        BeaconTower,    // The beacon tower, its basket heaped with wood never lit: height, radius, form (0 round, 1 square), stone (r, g, b).
        BeaconPyre,     // The beacon's wood stacked at the tower's foot under the Reich's sealed tarp: length.
        SightStone,     // The stone the keepers sighted the next beacon by, along its +z: stone (r, g, b).
        Horn,           // The beacon's horn on its tripod.

        // ---- A brood's pen (Pen.cs).
        Kennel,         // A row of kennels for war-raptors, barred and roofed at the back: stalls, a stall's width, depth.
        ButcherBlock,   // The block, its cleavers, offal and the rack of meat over it.
        BiteDummy,      // Straw dummies in the land's clothes, gashed by claws: count.
        Midden,         // The heap of dung and straw with a fork in it: radius.

        // ---- What the works and the farms run on (Works.cs).
        Boiler,         // The Institute's boiler on its brick firebox, its chimney smoking and its coal heaped by it.
        Haystack,       // Hay stacked round a pole under a cap of thatch: radius.
        MendingNet,     // A net spread on the shingle to be mended, pegged, floats along its edge, the mender's stool by it: length, width.
        GuttingTable,   // A table where the catch is gutted, its buckets and basket under it: length.
        Slash,          // The boughs a felling lops off, heaped in a brown tangle: radius, bark (as a stump's).

        // ---- What lies on the ground (Underfoot.cs).
        Litter,         // Litter of a kind over a disc: kind (LitterKind), radius, count, and a stone's colour (r, g, b) for spalls and rubble.
        Puddle,         // Water standing in a yard's ruts, with a rim of churned mud: radius.
        Duckboards,     // Boards laid over the mud on runners: length, width.

        // ---- What whoever holds a place lives and works with (Camp.cs).
        WeaponRack,     // A rack of weapons: width, whose (1 the Reich's spears, goads and shields; 0 the land's pitchforks, axes, a scythe and a bow).
        Torch,          // A torch on a pole, burning: height, form (0 the land's, 1 the Reich's cresset, 2 a fire cult's, 3 the frost cult's cold flame, 4 the Ossuary's cold lamp).
        Brazier,        // The Reich's brazier by itself, logs by it: height.
        Firewood,       // Split wood stacked with its ends to the front, a chopping block and its axe: length, height.
        Wagon,          // A four-wheeled wagon: form (0 loaded, 1 broken down), whose (1 the Reich's tilted wagon), length.
        Gibbet,         // The Reich's gibbet, its cage hung empty and its notice on the post: height.
        Barricade,      // Stakes through a log, sharpened outwards: length, whose.
        CommandTent,    // A brood's wall tent, its fly out over a table of maps, its standard at the door: length.
        Signpost,       // A signpost, the Reich's boards nailed over the land's names: arms, whose.
        MessTable,      // A trestle table with benches, bowls and a lantern over it: length.

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
