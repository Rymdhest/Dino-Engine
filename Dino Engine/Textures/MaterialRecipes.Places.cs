using Dino_Engine.Textures.Synthesis;
using OpenTK.Mathematics;
using static Dino_Engine.Textures.Synthesis.Colours;

namespace Dino_Engine.Textures
{
    // What the places with a story of their own are made of (PLACES.md): the Reich's riveted iron, stone fresh out of a quarry, a fishing net and snow packed into blocks. Each is pale, as
    // its colour is in the vertices, and tiles: every lattice is a whole number of cells across the tile, and U and V are only read through something that repeats a whole number of times.
    public static partial class MaterialRecipes
    {
        // The Reich's iron in plates, four courses to a tile, each lapped over the one below so its lower edge stands proud, the plates of a course butted end to end with the joints
        // stepped course to course, and a row of rivets along every lap and up every butt. The sea and the rain run rust down from each rivet, as they do on any iron hull.
        private static Surface RivetedPlate(Canvas c)
        {
            const int Courses = 4;
            const int RivetsAcross = 20;        // Along a course, over the tile: ten to a plate.
            Bricks plates = c.Bricks(rows: Courses, perRow: 2, gap: 0.0025f, bond: 0.5f);
            // A course rises from where the one above laps over it to its own lapped edge at its foot.
            Field lap = (1f - plates.Across).Pow(0.7f);
            Field seam = plates.Edge.Profile(0.0025f, Rise.Round);
            Field dents = c.Cells(new Vector2(12f), jitter: 0.9f).Centre.Profile(0.6f, Rise.Smooth);

            // Rivets: little domes, in a row just above each lap and in a column just past each butt.
            Field alongLap = (c.U * RivetsAcross).Fract() - 0.5f;
            Field aboveLap = (plates.Across - 0.12f) * (RivetsAcross / (float)Courses * 0.5f);
            Field lapRivets = (1f - (alongLap * alongLap + aboveLap * aboveLap) * (1f / (0.3f * 0.3f))).Clamp(0f, 1f).Sqrt();
            Field upButt = (c.V * (Courses * 5)).Fract() - 0.5f;
            Field pastButt = (plates.Along - 0.035f) * (RivetsAcross * 0.5f);
            Field buttRivets = (1f - (upButt * upButt + pastButt * pastButt) * (1f / (0.3f * 0.3f))).Clamp(0f, 1f).Sqrt();
            Field rivets = lapRivets.Max(buttRivets);

            Field height = seam * (0.5f + lap * 0.32f + dents * 0.04f) + rivets * 0.14f;

            // Rust: running down from the rivets in streaks, gathering along the laps, and in patches where the paint and the scale have gone.
            Field streaks = c.Noise(new Vector2(36f, 3f), octaves: 4, kind: NoiseKind.Ridged).Threshold(0.62f, 0.12f);
            Field underRivets = lapRivets.Blur(0.004f).Smooth(0.02f, 0.3f);
            Field rust = (streaks * 0.55f + underRivets * 0.4f + Patches(c, 7f, 0.1f, 0.1f, 0.12f) * 0.8f + (1f - seam.Smooth(0f, 0.4f)) * 0.3f).Clamp(0f, 1f);
            Paint iron = Rgb(176, 176, 178) * Field.Lerp(0.86f, 1.02f, dents) * Field.Lerp(1f, 1.1f, rivets) * plates.Id.Random(4).Remap(0f, 1f, 0.93f, 1.04f);
            Paint colour = Paint.Lerp(iron, Rgb(118, 70, 44) * Field.Lerp(0.75f, 1.1f, c.Noise(40f, octaves: 3)), rust);
            return new Surface(colour, Field.Lerp(0.45f, 0.88f, rust), height) { Metallic = Field.Lerp(0.75f, 0f, rust) };
        }

        // Stone split out of its bed: beds stepped back one over the next, the faces between them flat where they split and rippled where they broke, the half-round channels of the holes
        // the splitting line was drilled along, in patches where a block came away, and the short scars of picks. Dust lies pale in the channels and the iron in the stone stains it.
        private static Surface CutStone(Canvas c)
        {
            const int Beds = 3;
            const int Holes = 22;               // Drill holes across the tile, where they show.
            Offsets wobble = c.Offsets(4f, octaves: 3);
            Field bed = (c.V * Beds).Fract().Warp(wobble, 0.01f);
            // Each bed set back a little from the one below it, and broken off at its foot.
            Field step = bed.Pow(0.6f);
            Facets splits = c.Facets(new Vector2(3f, 5f), tilt: 0.22f, relief: 0.35f, crack: 0.12f, width: 0.025f);
            Field ripple = c.Noise(new Vector2(9f, 6f), octaves: 4, kind: NoiseKind.Ridged, warp: 0.05f);

            // The channels of the drill holes: half-round grooves running up the face, where a line of them split a block away.
            Field acrossHole = ((c.U * Holes).Fract() - 0.5f).Abs() * 2f;
            Field groove = 1f - (1f - acrossHole * acrossHole).Clamp(0f, 1f).Sqrt();
            Field drilled = Patches(c, 3f, 0.4f, 0.04f, 0.08f);
            Field channels = Field.Lerp(1f, groove, drilled);

            Stones scars = c.Scatter(new Vector2(14f, 14f), count: 2, radius: 0.32f, radiusJitter: 0.4f, elongation: 3.2f, heightJitter: 0.3f, turn: 0.7f, turnJitter: 0.3f);
            // The channels cut into whatever face they run up, and the foot of each bed breaks off a little, so its edge is never a cliff in the height, which tears at this camera's slant.
            Field height = (0.5f + step * 0.22f + (splits.Height - 0.5f) * 0.2f + (ripple - 0.5f) * 0.06f) * Field.Lerp(1f, 0.82f, (1f - channels) * 0.9f) - scars.Cover * 0.05f;
            height = height - (1f - bed.Smooth(0f, 0.06f)) * 0.08f;

            Paint stone = Rgb(192, 188, 180) * Field.Lerp(0.9f, 1.04f, splits.Id.Random(1)) * Field.Lerp(0.94f, 1.03f, ripple);
            // Dust pale in the channels and the scars, iron staining down from the beds.
            stone = Paint.Lerp(stone, Rgb(210, 206, 196), ((1f - channels) * 0.7f + scars.Cover * 0.5f).Clamp(0f, 1f));
            Field stain = c.Noise(new Vector2(10f, 2f), octaves: 4).Threshold(0.7f, 0.1f) * (1f - bed).Pow(2f);
            stone = Paint.Lerp(stone, Rgb(150, 112, 78), stain * 0.6f);
            return new Surface(stone, Field.Lerp(0.82f, 0.94f, channels), height.Clamp(0f, 1f));
        }

        // A net of twine knotted in a diamond mesh, a dozen meshes across the tile, each knot a little lump where four strands meet, the twine wandering a little, over the dark of
        // whatever it's hung against.
        private static Surface Netting(Canvas c)
        {
            const int Meshes = 12;
            Offsets wander = c.Offsets(6f, octaves: 3);
            Field a = ((c.U + c.V) * Meshes).Fract().Warp(wander, 0.004f);
            Field b = ((c.U - c.V + 1f) * Meshes).Fract().Warp(wander, 0.004f);
            // How far each point is from the nearest strand of each set, a half in the middle of a mesh.
            Field toA = 0.5f - (a - 0.5f).Abs();
            Field toB = 0.5f - (b - 0.5f).Abs();
            Field strandA = 1f - toA.Smooth(0.035f, 0.075f);
            Field strandB = 1f - toB.Smooth(0.035f, 0.075f);
            Field knot = (1f - (toA * toA + toB * toB).Sqrt().Smooth(0.06f, 0.14f));
            Field twine = strandA.Max(strandB).Max(knot);
            Field lay = c.Noise(new Vector2(80f, 80f), octaves: 2);
            Field height = twine * (0.78f + knot * 0.2f + lay * 0.04f) + 0.02f;

            Paint cord = Rgb(196, 186, 168) * Field.Lerp(0.85f, 1.05f, lay) * Field.Lerp(1f, 0.9f, knot);
            Paint colour = Paint.Lerp(Rgb(14, 13, 12), cord, twine);
            return new Surface(colour, 0.95f, height) { Occlusion = Field.Lerp(0.35f, 1f, twine) };
        }

        // Snow pressed hard and smoothed by hand, as a bucket's tower, a wall's cut blocks or anything else the north's children make is: no pattern of its own, as a
        // block is a block of the model and a texture of blocks over it, or over a tower pressed from a bucket, read as noise. The long strokes of the mitten or spade that
        // smoothed it running across it, soft swells where it was patted in, a few dents of fingers, and a sparkling crust where it's had a frost on it.
        private static Surface PackedSnow(Canvas c)
        {
            Field strokes = c.Noise(new Vector2(3f, 22f), octaves: 3, warp: 0.03f);
            Field pats = c.Cells(new Vector2(9f), jitter: 0.9f).Centre.Profile(0.8f, Rise.Smooth);
            Field dents = c.Cells(new Vector2(7f), jitter: 0.9f).Centre.Profile(0.12f, Rise.Smooth);
            Field crust = c.Noise(60f, octaves: 3);
            Field height = 0.5f + (strokes - 0.5f) * 0.3f + pats * 0.2f - dents * 0.07f + (crust - 0.5f) * 0.06f;

            Paint colour = c.Ramp(height, (0.1f, Rgb(178, 192, 212)), (0.5f, Rgb(214, 222, 234)), (0.9f, Rgb(232, 236, 242)));
            colour = colour * Field.Lerp(0.97f, 1.02f, strokes);
            return new Surface(colour, Field.Lerp(0.8f, 0.62f, crust), height.Clamp(0f, 1f)) { Subsurface = 0.45f };
        }
    }
}
