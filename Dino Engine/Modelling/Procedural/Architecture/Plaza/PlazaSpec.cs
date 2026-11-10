using System.Globalization;

namespace Dino_Engine.Modelling.Procedural.Architecture.Plaza
{
    /// <summary>What a piece of a town square is. Each takes its own numbers, listed with it, all whole centimetres or degrees so that an id never holds a dot.</summary>
    public enum PlazaKind
    {
        Arc,        // A wall along an arc round the origin: form, radius, bearing it starts at, degrees it sweeps (clockwise from above), top, soil (-1 for none), thickness, facing (1 out, -1 in).
        Run,        // A wall along +x from the origin, its face on z = 0 towards +z: form, length, top, soil (-1 for none), thickness.
        Steps,      // A flight rising towards -z from its bottom riser on z = 0, between cheek walls: width, steps, rise, tread, cheek, top, landing, piers out in front.
        Pier,       // A square pier: width, top, what stands on it (0 nothing, 1 a lantern, 2 an urn).
        Pergola,    // Stone columns under timber, half shaded by a sail: length along x, depth along z, bays, height to the beams, share of it shaded (per cent).
        Arch,       // A gateway over a street running along z: span of its opening, height it springs at, depth, width of each pier.
        Table,      // A trestle table set for eating, with a bench each side: length along x.
        Pot,        // A terracotta pot: form (0 flowerpot, 1 urn, 2 bowl, 3 jar), height.
        Mast,       // A pole that bunting hangs from: height, whether it carries a lantern out towards +z (1) or not (0).
        Bunting,    // A line of pennants from the origin to span along x and drop up y, hanging sag in the middle: span, drop, sag.
        Line,       // Stones laid flush in the paving along +x: form (0 a band, 1 a gutter), length, width.
        Ring,       // Stones laid flush in the paving round an arc: form, radius, bearing it starts at, degrees it sweeps, width.
        Well,       // A well with a windlass under a little roof: radius of its wall.
    }

    /// <summary>
    /// Everything it takes to build one piece of a town square, and its id, which is all that's kept: "plaza:arc:1f3a9c:0:1700:118:40:130:95:120:-1" is a wall of field stone
    /// 17 m round an arc, facing in. Ids have no dots and no hash, so that nothing reads one as a building's or a plant's. Read SQUARE.md.
    /// </summary>
    public readonly struct PlazaSpec
    {
        private const string Prefix = "plaza:";

        public readonly PlazaKind Kind;
        public readonly uint Seed;
        private readonly int[] _values;

        public PlazaSpec(PlazaKind kind, uint seed, params int[] values)
        {
            Kind = kind;
            Seed = seed;
            _values = values;
        }

        public int Count => _values.Length;
        public int Int(int i) => i < _values.Length ? _values[i] : 0;
        public float Metres(int i) => Int(i) / 100f;
        public float Degrees(int i) => Int(i);

        public string Id => $"{Prefix}{Kind.ToString().ToLowerInvariant()}:{Seed:x}:{string.Join(':', _values.Select(v => v.ToString(CultureInfo.InvariantCulture)))}";

        /// <summary>Metres as the whole centimetres an id keeps.</summary>
        public static int Cm(float metres) => (int)MathF.Round(metres * 100f);

        public static bool IsPlaza(string id) => id.StartsWith(Prefix, StringComparison.Ordinal);

        public static bool TryParse(string id, out PlazaSpec spec)
        {
            spec = default;
            if (!IsPlaza(id)) return false;
            string[] parts = id.Substring(Prefix.Length).Split(':');
            if (parts.Length < 2 || !Enum.TryParse(parts[0], ignoreCase: true, out PlazaKind kind)) return false;
            if (!uint.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint seed)) return false;
            var values = new int[parts.Length - 2];
            for (int i = 0; i < values.Length; i++)
            {
                if (!int.TryParse(parts[i + 2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out values[i])) return false;
            }
            spec = new PlazaSpec(kind, seed, values);
            return true;
        }
    }
}
