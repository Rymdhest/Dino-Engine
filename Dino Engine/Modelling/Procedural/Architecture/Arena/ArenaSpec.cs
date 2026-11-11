using System.Globalization;

namespace Dino_Engine.Modelling.Procedural.Architecture.Arena
{
    /// <summary>
    /// What a piece of an arena is. Each takes its own numbers, listed with it, all whole centimetres, degrees or choices, so that an id never holds a dot. "Stone" is an <see cref="ArenaStoneKind"/>, and a
    /// wall's line is round an arc about the origin by bearing (clockwise from +z, seen from above) or straight along +x with its face towards +z. Read ARENAS.md.
    /// </summary>
    public enum ArenaShape
    {
        Arc,            // A wall round an arc: form, stone, radius, bearing it starts at, degrees it sweeps, top, thickness, ground held behind it (-1 none), facing (1 out, -1 in), trim.
        Run,            // A wall along +x: form, stone, length, top, thickness, ground held behind it (-1 none), trim.
        Gatehouse,      // An arched gate in a wall, its face on z = 0 towards +z: stone, opening's width, its height to the crown, depth of its passage, block's width, block's height.
        Portcullis,     // A grille that rises out of a gate: form (an ArenaGrille), width, height.
        Pylon,          // A square pylon of dressed stone: stone, width, height, what's on top (0 nothing, 1 a brazier, 2 a pole of the Reich's banner).
        Brazier,        // A brazier on its own: form (0 an iron tripod, 1 a bowl on a stone drum), size in per cent.
        Banner,         // The Reich's banner on a mast: mast's height, banner's length, its width, form (0 from a crossbar on a mast, 1 hung from a wall's top at y = 0).
        Bay,            // The waygate's bay in a wall: stone, width, depth, height: three walls round the back of a recess in the wall, facing into it.
        Stair,          // A flight of steps rising towards -z between cheeks, for looking at, not for walking: stone, width, steps, rise, tread.
        Lamp,           // A light held up on its own, as one inside a model over 6 m across is shut in by it (LocalShadowCasters): form (0 a brazier's fire, 1 a torch on a bracket, 2 an Ossuary lamp, 3 candles, 4 the Institute's amber lamp on a bracket, 5 the light of a window of oxblood and amber glass, thrown in along +z).
        Remains,        // What's left on a floor of the fights before: form (0 old blood soaked into the sand, 1 the bones of what was fed to the tyrant, 2 straw trodden into mud), size in per cent.
        Arcade,         // An arcade round an arc, open to the sky: stone, radius, bearing it starts at, degrees it sweeps, facing (1 out, -1 in), storeys, a storey's height, pier's width, depth.
        Tribune,        // The commander's box out over a podium whose face is at z = 0: stone, width, how far it stands out, its floor's height.
        Hatch,          // The frame of a lift's hatch in the floor, round a shaft into the dark: its size.
        Leaf,           // One leaf of a lift's hatch, hinged at the origin and reaching along +x: the hatch's size.
        Ballista,       // A bolt-thrower on its stand, facing +z.
        Bars,           // A cage's bars round an arc, on a footing whose top is at y = 0: radius, bearing it starts at, degrees it sweeps, height before they lean in, how far they lean, spacing.
        PenGate,        // A pen's gate in a cage's footing, its face on z = 0 towards +z: stone, opening's width, its height, depth, the block's height.
        Nest,           // A nest of straw for the Institute's eggs: its size.
        Gallery,        // The Institute's gallery round an arc outside a cage: radius, bearing it starts at, degrees it sweeps, its deck's height, its depth.
        Trough,         // A feeding trough of stone: stone, length.
        Gibbet,         // A gibbet with a carcass hung from it: height.
        Colonnade,      // A roofed colonnade along +x, its front row on z = 0: stone, length, height, depth.
        SunDisc,        // The land's sun, a disc of gold in a stone frame, facing +z: stone, the disc's radius.
        Portal,         // A portal in a wall, its face on z = 0 towards +z, for bronze doors: stone, opening's width, its height, depth.
        DoorLeaf,       // One leaf of a pair of bronze doors, hinged at the origin and reaching along +x: width, height.
        Pedestal,       // The pedestal a mirror turns on: stone.
        Mirror,         // A brass mirror on a yoke, which turns, its pivot at the origin: the disc's radius.
        Inlay,          // Rings inlaid flush in a court's paving round the origin: stone, the outer ring's radius.
        Statue,         // One of the Reich's charge turned to stone, facing +z: kind (OnslaughtPieces.Kinds), variant, how far into its stride (hundredths of a second), lean (degrees), how far sunk.
        Wagon,          // A war-wagon of the charge on its side, turned to stone, along +x.
        Standard,       // A standard of the charge with its pennant in stone: how far it leans (degrees).
        Fossil,         // The bones of an old beast in a cut face whose face is at z = 0: size in per cent.
        Scaffold,       // The Ossuary's scaffold against a face at z = 0: width, height.
        Crane,          // The Ossuary's crane, reaching out towards +z: reach.
        Vent,           // A crack in the ash that breathes fire: length.
        GothicPier,     // A clustered pier, its capital at the height given: stone, height.
        IronArch,       // A pointed arch of riveted iron across x at y = 0: span, rise.
        Chain,          // A chain hanging from y = 0, snapped at its foot: length.
        Chandelier,     // An iron wheel of candles hung from a hook at y = 0, which comes down: radius.
        Screen,         // A run of iron screen along +x on a wall's top at y = 0: length, height.
        ScreenGate,     // A pointed gate in a wall, its face on z = 0 towards +z: stone, opening's width, its springing, depth.
        Apse,           // An apse round an arc about the origin, facing in, with windows of glass: stone, radius, bearing it starts at, degrees it sweeps, height, windows.
        Throne,         // The altar and the throne-cage the tyrant is anointed at: stone, width.
        IronSkull,      // The Iron Apex, a tyrant's skull in black iron on a wall at z = 0: its length.
        Pews,           // Rows of pews along +x facing +z: length, rows.
        Clutch,         // The Institute's eggs in a nest, which can be broken: the nest's size.
        Shells,         // What's left of a clutch once it's hatched or been smashed: the nest's size.
    }

    /// <summary>What a wall of an arena is built as.</summary>
    public enum ArenaWallForm
    {
        Podium,         // The sheer wall round a floor: a plinth, coursed ashlar banded by string courses, pilasters between, a moulded cope, and the Reich's iron railing along it.
        Riser,          // The front of a tier of stands: a couple of courses under a broad bench of a cope, as people stand behind it.
        Parapet,        // A low wall standing on its own, with a cope.
        Footing,        // The stone a cage's bars stand in: a squat course under a sloped cap, with the bars' sockets.
        Quarry,         // A quarry's cut face: big blocks as the rock came off, leaning back as they rise, under a rough bench.
    }

    /// <summary>What a wall carries, added together.</summary>
    [Flags]
    public enum ArenaTrim
    {
        None = 0,
        Railing = 1,    // Black iron along its cope, posts and rails with spikes leaning out over the floor.
        Pilasters = 2,  // Flat piers standing out of its face at even steps, each with a brass boss at its head.
        Banners = 4,    // The Reich's banner hung down its face between the pilasters, every other bay.
        Torches = 8,    // A torch on a bracket at the head of every other pilaster.
    }

    /// <summary>How a grille that rises is made.</summary>
    public enum ArenaGrille
    {
        Pit,            // Black iron uprights through cross bars, spiked at the foot.
        Pen,            // A cage's door: heavier bars, riveted bands, a brass lock plate.
        Screen,         // A cathedral screen's gate: bars with pointed heads and quatrefoils of iron.
    }

    /// <summary>
    /// Everything it takes to build one piece of an arena, and its id, which is all that's kept: "arena:arc:1f3a9c:0:2:1800:0:45:520:170:430:-1:3". Ids have no dots and no hash, so that nothing reads one
    /// as a building's or a plant's. Read ARENAS.md.
    /// </summary>
    public readonly struct ArenaSpec
    {
        private const string Prefix = "arena:";

        public readonly ArenaShape Shape;
        public readonly uint Seed;
        private readonly int[] _values;

        public ArenaSpec(ArenaShape shape, uint seed, params int[] values)
        {
            Shape = shape;
            Seed = seed;
            _values = values;
        }

        public int Count => _values?.Length ?? 0;
        public int Int(int i) => _values != null && i < _values.Length ? _values[i] : 0;
        public float Metres(int i) => Int(i) / 100f;
        public float Degrees(int i) => Int(i);

        public string Id => _values == null || _values.Length == 0
            ? $"{Prefix}{Shape.ToString().ToLowerInvariant()}:{Seed:x}"
            : $"{Prefix}{Shape.ToString().ToLowerInvariant()}:{Seed:x}:{string.Join(':', _values.Select(v => v.ToString(CultureInfo.InvariantCulture)))}";

        /// <summary>Metres as the whole centimetres an id keeps.</summary>
        public static int Cm(float metres) => (int)MathF.Round(metres * 100f);

        public static bool IsArena(string id) => id.StartsWith(Prefix, StringComparison.Ordinal);

        public static bool TryParse(string id, out ArenaSpec spec)
        {
            spec = default;
            if (!IsArena(id)) return false;
            string[] parts = id.Substring(Prefix.Length).Split(':');
            if (parts.Length < 2 || !Enum.TryParse(parts[0], ignoreCase: true, out ArenaShape shape)) return false;
            if (!uint.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint seed)) return false;
            var values = new int[parts.Length - 2];
            for (int i = 0; i < values.Length; i++)
            {
                if (!int.TryParse(parts[i + 2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out values[i])) return false;
            }
            spec = new ArenaSpec(shape, seed, values);
            return true;
        }
    }
}
