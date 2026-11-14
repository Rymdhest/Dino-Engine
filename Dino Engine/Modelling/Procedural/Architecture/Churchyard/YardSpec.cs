using System.Globalization;

namespace Dino_Engine.Modelling.Procedural.Architecture.Churchyard
{
    /// <summary>
    /// What a piece of a churchyard is. Each takes its own numbers, listed with it, all whole centimetres, degrees, per mille or choices, so that an id never holds a dot. "Shade" and
    /// "warmth" are the churchyard's stone, per mille, which is its wall's (<see cref="Fence.StoneFences.ChurchyardStone"/>), so the church, its gates and its wall are one stone. Read CHURCHYARD.md.
    /// </summary>
    public enum YardKind
    {
        Church,     // The church, all of it grown from its seed (ChurchDesign.Of): shade, warmth.
        Stone,      // A grave and the stone at its head, facing +z with the grave in front of it: form (StoneForm), dressing (GraveDress), age (per cent). Each its own quarry's stone.
        Tomb,       // A chest tomb along z: form (TombForm), opened (1 if the Ossuary has slid its lid off), age (per cent), shade, warmth.
        Mausoleum,  // A family's house of the dead, its door towards +z: form (MausoleumForm), opened (1 if its door is broken in), shade, warmth.
        Railing,    // A family plot's iron railing on a kerb round a box centred on the origin: width along x, depth along z, where its gap is (0 none, 1 the +z side).
        Lamp,       // A light on a path: form (LampForm), height.
        Cross,      // The churchyard's cross on its steps: form (0 a tall cross on a calvary, 1 a ringed high cross), height, shade, warmth.
        Gate,       // A gateway in the wall, the way through along z: form (GateForm), width of its opening, shade, warmth.
        Dug,        // A grave the Ossuary has opened, its pit along z with the spoil heaped on +x: form (0 freshly dug, 1 dug and left), age of the stone it took (per cent).
        Candles,    // Candles left on a grave: form (0 a few on the ground, 1 grave lights in glass, 2 a bank on a stand at a door), how many.
        Bench,      // A bench along x facing +z, of oak on stone ends: length.
    }

    /// <summary>
    /// Everything it takes to build one piece of a churchyard, and its id, which is all that's kept: "yard:stone:1f3a9c:1:2:40:655:500" is a pointed stone over a kerbed grave, forty
    /// years weathered. Ids have no dots and no hash, so that nothing reads one as a building's or a plant's.
    /// </summary>
    public readonly struct YardSpec
    {
        private const string Prefix = "yard:";

        public readonly YardKind Kind;
        public readonly uint Seed;
        private readonly int[] _values;

        public YardSpec(YardKind kind, uint seed, params int[] values)
        {
            Kind = kind;
            Seed = seed;
            _values = values;
        }

        public int Count => _values.Length;
        public int Int(int i) => i < _values.Length ? _values[i] : 0;
        public float Metres(int i) => Int(i) / 100f;
        public float Share(int i) => Int(i) / 100f;
        public float Mille(int i) => Int(i) / 1000f;

        public string Id => $"{Prefix}{Kind.ToString().ToLowerInvariant()}:{Seed:x}:{string.Join(':', _values.Select(v => v.ToString(CultureInfo.InvariantCulture)))}";

        /// <summary>Metres as the whole centimetres an id keeps.</summary>
        public static int Cm(float metres) => (int)MathF.Round(metres * 100f);

        public static bool IsYard(string id) => id.StartsWith(Prefix, StringComparison.Ordinal);

        public static bool TryParse(string id, out YardSpec spec)
        {
            spec = default;
            if (!IsYard(id)) return false;
            string[] parts = id.Substring(Prefix.Length).Split(':');
            if (parts.Length < 2 || !Enum.TryParse(parts[0], ignoreCase: true, out YardKind kind)) return false;
            if (!uint.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint seed)) return false;
            var values = new int[parts.Length - 2];
            for (int i = 0; i < values.Length; i++)
            {
                if (!int.TryParse(parts[i + 2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out values[i])) return false;
            }
            spec = new YardSpec(kind, seed, values);
            return true;
        }
    }

    /// <summary>The shape of a gravestone. A churchyard's oldest stones are tablets and slabs, its newer ones crosses and pointed stones, and a family with money raises an obelisk.</summary>
    public enum StoneForm
    {
        Round,      // A tablet with a rounded head.
        Pointed,    // A tablet with a pointed head, a cross cut into its face.
        Shouldered, // A tablet with shoulders and a raised middle, as a bed's head.
        Cross,      // A plain cross on a stepped base.
        Ringed,     // A cross with a ring round its head, on a tapering shaft.
        Obelisk,    // A tapering shaft with a point, on two steps.
        Slab,       // A thick old slab with a moulded edge, square-headed.
        Broken,     // What's left of a tablet, its head lying in the grass in front of it.
        Wooden,     // A cross of oak gone grey, for someone nobody paid a mason for.
        Book,       // An open book of stone on a plinth, low.
    }

    /// <summary>What lies in front of a stone: the grave itself.</summary>
    public enum GraveDress
    {
        Grass,      // Nothing: the turf grew over it long ago.
        Mound,      // A mound of earth, grassed over.
        Kerb,       // A stone kerb round it, filled with pale chippings.
        Ledger,     // A slab laid over all of it on a low plinth.
        Sunken,     // A kerb whose ground has fallen in, its sides tipped in towards it.
    }

    public enum TombForm
    {
        Panelled,   // A chest with sunk panels between corner piers, under a moulded lid.
        Table,      // A slab on six short columns.
        Coped,      // A chest under a ridged lid, as a little roof.
    }

    public enum MausoleumForm
    {
        Classical,  // A temple front: steps, columns, a pediment, a flat roof behind.
        Gothic,     // A steep stone roof, buttresses with pinnacles and a pointed door.
    }

    public enum LampForm
    {
        Crook,      // A lantern hung from the crook of a post of weathered iron.
        Pillar,     // A lantern on a pillar of the churchyard's stone.
        Hook,       // A lantern on a hook from a leaning post of old oak.
    }

    public enum GateForm
    {
        Piers,      // Two piers with caps, a pair of iron gates standing open, and an overthrow with a lantern hung from it.
        Lychgate,   // A roofed gateway of oak on low stone walls, where the dead were set down on the way in.
    }
}
