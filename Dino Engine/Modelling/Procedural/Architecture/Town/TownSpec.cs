using System.Globalization;

namespace Dino_Engine.Modelling.Procedural.Architecture.Town
{
    /// <summary>What a piece of the Haven's town is (HAVEN.md). Each takes its own numbers, listed with it, all whole centimetres or choices, so that an id never holds a dot.</summary>
    public enum TownKind
    {
        House,      // A home, front towards +z: width (x), depth (z), design (HouseDesign), roofing (Roofing), walling (Walling), features (HouseFeature, as bits), colour (of its shutters and door).
        Stepping,   // Flat stones trodden into the ground along +x from the origin, as a path to a door: length, width.
        Stall,      // A market stall under an awning, its counter towards +z: width, trade (StallTrade), colour (of its awning's stripes).
        Bed,        // A kitchen garden's bed between boards, its rows along x: length, width, crop (BedCrop).
        Sign,       // A sign hung from a post by the way, read from +z: emblem (SignEmblem), height.
        Banner,     // The Haven's banner on a pole, hanging towards +z: height, colour.
        Trellis,    // An arbour over a garden's gate, grown over: width (of the way under it), height, bloom (a colour).
        Yardwork,   // What a household's work leaves by its door, as a group standing on the ground: trade (YardTrade).
        Bench,      // A bench with a back, facing +z: length.
        Lamp,       // A lantern on a timber post with an arm out towards +z: height.
        Trough,     // A stone trough with a spout, fed by a pipe from a wall behind it (-z): length.
        Memorial,   // The lanterns hung for those who haven't come yet: a frame of posts by the water with a lantern for each, and ribbons: width, lanterns.
        Edging,     // A low kerb of rough stones along +x, round a bed or a lawn: length.
        Canopy,     // A striped fly over somewhere to sit, its ridge along x between two tall poles: width (x), depth (z), height (of its eaves), colour.
    }

    /// <summary>How a house is laid out.</summary>
    public enum HouseDesign
    {
        Cottage,    // One storey and a loft, the usual home.
        Townhouse,  // Two storeys, the upper one standing out over the street, with a shop below if it's asked for.
        Longhouse,  // One long storey under one roof, two doors and two fires: where those who've just come are taken in.
        Fisher,     // A low cottage boarded against the weather.
        Workshop,   // A house whose near end stands open as a shop to work in.
    }

    public enum Roofing
    {
        Thatch,
        Shingle,
        Slate,
        Tile,
    }

    public enum Walling
    {
        Framed,     // Timber and lime from the plinth up.
        StoneFoot,  // Its ground floor of stone, timber and lime over it.
        Stone,
        Boarded,    // Weatherboards over its frame.
    }

    [Flags]
    public enum HouseFeature
    {
        None = 0,
        Wing = 1,           // A gabled wing standing out of its front.
        Porch = 2,          // A roof on posts over its door.
        LeanLeft = 4,       // An open lean-to against its -x end.
        LeanRight = 8,      // And against its +x end.
        Dormers = 16,
        Jetty = 32,         // Its upper storey out over its front.
        Awning = 64,        // A shop's awning and counter beside its door.
        EndStack = 128,     // Its chimney built up outside a gable; otherwise through its ridge.
        Boxes = 256,        // Flowers under its front windows.
        Bench = 512,        // A bench by its door.
        Climber = 1024,     // Something in flower grown up its front.
        TwoStacks = 2048,
        Mended = 4096,      // Part of its roof new, lighter than the rest, and the ladder still up.
    }

    public enum StallTrade
    {
        Produce,
        Bread,
        Fish,
        Cloth,
        Pots,
        Wares,      // The merchant's: arms, flasks and books.
        Gifts,      // The beta vendor's: a bit of everything, wrapped.
    }

    public enum BedCrop
    {
        Cabbages,
        Leeks,
        Beans,      // On poles.
        Pumpkins,
        Herbs,
        Flowers,
    }

    public enum SignEmblem
    {
        Loaf,
        Fish,
        Shears,
        Mug,
        Hammer,
        Blossom,
        Boat,
        Key,
    }

    public enum YardTrade
    {
        Washing,    // Tubs, a washboard and a mangle by a line of washing.
        Baking,     // Sacks of flour, a peel, trays of loaves cooling, a stack of faggots for the oven.
        Fishing,    // Creels, floats, a barrel of salt and an oar or two.
        Joinery,    // Trestles with a plank on them, shavings, a half-made chair.
        Gardening,  // A barrow, pots, a watering can, a spade stuck in the ground.
        Arriving,   // Bundles, chests and a handcart, as those who've just come put them down.
        Smithing,   // An anvil on its stump out in the light, a quench tub, a heap of coal, bar stock and a tyre waiting for its wheel.
        Tanning,    // Hides laced out in frames to dry, and a vat of bark liquor.
        Dyeing,     // Lengths of cloth in the Haven's dyes hung on lines between posts, still dripping, and the tub they came out of.
        Drying,     // The same lines with a household's linen on them.
        Brewing,    // Casks on a stillage, and a table and two benches where it's drunk.
        Thatching,  // Reed in sheaves, stooked on end and laid in a pile, a bundle of hazel spars and the bat it's all dressed with.
    }

    /// <summary>
    /// Everything it takes to build one piece of the Haven's town, and its id, which is all that's kept: "town:house:1f3a9c:1300:1000:0:0:0:275:2". Ids have no dots and no hash, so
    /// that nothing reads one as a building's or a plant's. Read HAVEN.md.
    /// </summary>
    public readonly struct TownSpec
    {
        public const string Prefix = "town:";

        public readonly TownKind Kind;
        public readonly uint Seed;
        private readonly int[] _values;

        public TownSpec(TownKind kind, uint seed, params int[] values)
        {
            Kind = kind;
            Seed = seed;
            _values = values;
        }

        public int Count => _values?.Length ?? 0;
        public int Int(int i) => _values != null && i < _values.Length ? _values[i] : 0;
        public float Metres(int i) => Int(i) / 100f;

        public string Id => $"{Prefix}{Kind.ToString().ToLowerInvariant()}:{Seed:x}:{string.Join(':', (_values ?? Array.Empty<int>()).Select(v => v.ToString(CultureInfo.InvariantCulture)))}";

        /// <summary>Metres as the whole centimetres an id keeps.</summary>
        public static int Cm(float metres) => (int)MathF.Round(metres * 100f);

        public static bool IsTown(string id) => id.StartsWith(Prefix, StringComparison.Ordinal);

        public static bool TryParse(string id, out TownSpec spec)
        {
            spec = default;
            if (!IsTown(id)) return false;
            string[] parts = id.Substring(Prefix.Length).Split(':');
            if (parts.Length < 2 || !Enum.TryParse(parts[0], ignoreCase: true, out TownKind kind)) return false;
            if (!uint.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint seed)) return false;
            int count = parts.Length - 2;
            if (count == 1 && parts[2].Length == 0) count = 0;
            var values = new int[count];
            for (int i = 0; i < values.Length; i++)
            {
                if (!int.TryParse(parts[i + 2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out values[i])) return false;
            }
            spec = new TownSpec(kind, seed, values);
            return true;
        }

        /// <summary>A house's spec from what it is.</summary>
        public static TownSpec House(uint seed, float width, float depth, HouseDesign design, Roofing roofing, Walling walling, HouseFeature features, int colour)
        {
            return new TownSpec(TownKind.House, seed, Cm(width), Cm(depth), (int)design, (int)roofing, (int)walling, (int)features, colour);
        }
    }
}
