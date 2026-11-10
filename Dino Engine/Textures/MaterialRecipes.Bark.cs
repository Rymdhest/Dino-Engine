using Dino_Engine.Textures.Synthesis;
using OpenTK.Mathematics;
using static Dino_Engine.Textures.Synthesis.Colours;

namespace Dino_Engine.Textures
{
    // Bark, pale, as a tree's colour and its moss are in its vertices (BarkSkin): only light and dark here, and a little warmth in what's deep. v runs up the trunk.
    public static partial class MaterialRecipes
    {
        private static Paint BarkShade(Canvas c, Field height, int deep = 54, int high = 198)
        {
            return c.Ramp(height, (0f, Rgb(deep, deep - 6, deep - 10)), (0.45f, Rgb((deep + high) / 2, (deep + high) / 2 - 4, (deep + high) / 2 - 10)), (1f, Rgb(high, high - 4, high - 10)));
        }

        // Ridges running up the trunk that meet and part, with deep fissures between them and breaks across the ridges: oak, elm, old maple. A fissure is where a
        // noise stretched up the trunk crosses its middle, so the fissures braid as an oak's do and never close round into cells, which read as crazy paving.
        private static Surface BarkFurrowed(Canvas c)
        {
            // Few octaves, so the fissures run long and smooth up the trunk instead of wriggling.
            Field lay = c.Noise(new Vector2(7f, 1f), octaves: 3, gain: 0.45f, warp: 0.02f);
            // A fissure is a V, its bottom narrow, and a ridge's top is broad.
            Field ridge = ((lay - 0.5f).Abs() - 0.015f).Profile(0.085f, Rise.Sine);
            Field breaks = c.Noise(new Vector2(3f, 14f), octaves: 2, kind: NoiseKind.Ridged).Threshold(0.82f, 0.05f) * c.Noise(new Vector2(5f, 3f), octaves: 2).Threshold(0.55f, 0.1f);
            Field crown = c.Noise(new Vector2(5f, 1f), octaves: 3);
            Field fibre = c.Noise(new Vector2(60f, 4f), octaves: 3, kind: NoiseKind.Ridged);
            Field flakes = c.Noise(new Vector2(18f, 8f), octaves: 3).Quantize(4f);
            Field height = ridge * (0.72f + 0.2f * crown) - breaks * ridge * 0.22f + (fibre * 0.06f + flakes * 0.04f) * ridge;
            return new Surface(BarkShade(c, height, 44, 200), 0.93f, height);
        }

        // Thick plates in flaking layers with deep dark furrows between: pine, spruce, larch. A plate's edges wander, its face is terraced where flakes came away,
        // and the furrows have a bottom of their own, or the plates read as crazy paving.
        private static Surface BarkPlated(Canvas c)
        {
            Offsets wobble = c.Offsets(7f, octaves: 3);
            Cells plates = c.Cells(new Vector2(7f, 3.5f), jitter: 0.95f);
            Field id = plates.Id.Warp(wobble, 0.02f);
            Field edge = (plates.Border - 0.03f).Warp(wobble, 0.02f).Profile(0.08f, Rise.Round);
            Field layers = (c.Noise(new Vector2(16f, 8f), octaves: 4, warp: 0.06f) + id * 0.4f).Quantize(4f);
            Field plateHeight = edge * (0.66f + 0.2f * id.Random(1) + (layers - 0.5f) * 0.16f) + (c.Noise(new Vector2(60f, 30f), octaves: 3) - 0.5f) * 0.04f;
            Surface plate = new Surface(BarkShade(c, plateHeight.Remap(0.3f, 1f, 0.35f, 1f), 48, 204), 0.9f, plateHeight);
            Field furrowHeight = 0.12f + c.Noise(new Vector2(40f, 4f), octaves: 3, kind: NoiseKind.Ridged) * 0.1f;
            Surface furrow = new Surface(Paint.Lerp(Rgb(40, 35, 31), Rgb(70, 63, 57), c.Noise(30f, octaves: 3)), 0.95f, furrowHeight);
            return furrow.Stack(plate, 0.03f);
        }

        // Smooth and papery, chalk-white, with dark lenticels lying across the trunk, a few long and most short, curls peeling, and rough dark scars where branches were.
        private static Surface BarkPapery(Canvas c)
        {
            Stones lenticels = c.Scatter(new Vector2(6f, 34f), count: 1, radius: 0.42f, radiusJitter: 0.8f, elongation: 11f, heightJitter: 0.3f, turn: 0f, turnJitter: 0.02f);
            Field some = lenticels.Id2.Threshold(0.45f, 0f);
            Field peel = Patches(c, 6f, 0.2f, 0.04f, 0.12f);
            Field height = 0.74f + (c.Noise(new Vector2(4f, 10f), octaves: 4) - 0.5f) * 0.1f - lenticels.Height * some * 0.4f + peel * 0.12f;
            Paint colour = Paint.Lerp(Rgb(214, 210, 200), Rgb(194, 188, 178), c.Noise(new Vector2(6f, 20f), octaves: 4));
            colour = Paint.Lerp(colour, Rgb(34, 30, 28), lenticels.Cover.Smooth(0.2f, 0.6f) * some);
            Cells scarCells = c.Cells(new Vector2(3f, 4f), jitter: 0.9f);
            Field scars = (1f - scarCells.Centre.Warp(c.Offsets(10f), 0.02f).Smooth(0.05f, 0.22f)) * scarCells.Id.Threshold(0.62f, 0f);
            colour = Paint.Lerp(colour, Rgb(30, 26, 24), scars * 0.95f);
            height = height - scars * 0.08f + scars * (c.Noise(60f, octaves: 3) - 0.5f) * 0.1f;
            return new Surface(colour, Field.Lerp(0.7f, 0.94f, scars), height);
        }

        // Tight and fine, with lenticels lying across it: cherry, beech, the young of most trees.
        private static Surface BarkSmooth(Canvas c)
        {
            Stones lenticels = c.Scatter(new Vector2(12f, 48f), count: 1, radius: 0.3f, radiusJitter: 0.4f, elongation: 4f, heightJitter: 0.3f, turn: 0f, turnJitter: 0.03f);
            Field wrinkles = c.Noise(new Vector2(3f, 40f), octaves: 3, warp: 0.04f);
            Field height = 0.7f + (c.Noise(new Vector2(4f, 6f), octaves: 5) - 0.5f) * 0.2f + (wrinkles - 0.5f) * 0.08f - lenticels.Height * 0.25f;
            Paint colour = BarkShade(c, height.Remap(0.4f, 0.95f), 120, 196);
            colour = Paint.Lerp(colour, Rgb(96, 88, 80), lenticels.Cover * 0.45f);
            return new Surface(colour, 0.86f, height);
        }

        // Long soft strips running the length of the trunk: redwood, cedar, cypress.
        private static Surface BarkFibrous(Canvas c)
        {
            Field strips = c.Noise(new Vector2(16f, 1f), octaves: 5, kind: NoiseKind.Ridged, warp: 0.04f);
            Field fibres = c.Noise(new Vector2(110f, 3f), octaves: 3, kind: NoiseKind.Ridged);
            Field height = strips * 0.72f + fibres * 0.22f + (c.Noise(new Vector2(8f, 4f), octaves: 3) - 0.5f) * 0.1f;
            return new Surface(BarkShade(c, height, 64, 202), 0.95f, height);
        }

        // The scars of old leaves, in rings round the trunk, fibre between them: palms.
        private static Surface BarkRinged(Canvas c)
        {
            Field wander = (c.Noise(new Vector2(3f, 2f), octaves: 3) - 0.5f) * 0.5f;
            Field rings = (c.V * 12f + wander).Fract();
            Field scar = rings.Smooth(0.7f, 0.9f) * (1f - rings.Smooth(0.92f, 1f));
            Field fibres = c.Noise(new Vector2(90f, 6f), octaves: 3, kind: NoiseKind.Ridged);
            Field height = 0.45f + scar * 0.45f + fibres * 0.12f + (c.Noise(10f, octaves: 3) - 0.5f) * 0.08f;
            return new Surface(BarkShade(c, height, 60, 190), 0.92f, height);
        }

        // Wood with the bark long gone, silvered and checked along its grain.
        private static Surface BarkBare(Canvas c)
        {
            Field grain = c.Noise(new Vector2(46f, 3f), octaves: 5, warp: 0.03f);
            Cells checks = c.Cells(new Vector2(12f, 2f), jitter: 0.8f);
            Field check = (1f - checks.Border.Smooth(0f, 0.02f)) * checks.Id.Threshold(0.5f, 0f);
            Field height = 0.75f + (grain - 0.5f) * 0.25f - check * 0.55f;
            Paint colour = Paint.Lerp(Rgb(170, 166, 158), Rgb(206, 202, 194), grain);
            colour = Paint.Lerp(colour, Rgb(50, 46, 42), check * 0.85f);
            return new Surface(colour, 0.84f, height);
        }

        // Burnt to blocks of black char, glossy where it split, cracked deep between.
        private static Surface BarkCharred(Canvas c)
        {
            Cells blocks = c.Cells(new Vector2(10f, 8f), jitter: 0.75f);
            Field block = blocks.Border.Profile(0.08f, Rise.Round);
            Field height = block * (0.7f + 0.25f * blocks.Id) + (c.Noise(50f, octaves: 3) - 0.5f) * 0.06f;
            Paint colour = Paint.Lerp(Rgb(10, 9, 9), Rgb(58, 54, 52), block * Field.Lerp(0.6f, 1f, blocks.Id));
            return new Surface(colour, Field.Lerp(0.95f, 0.62f, block), height);
        }

        // The bark of a pine log, for what's built of one: thick plates in dark furrows, reddish where flakes have come away and grey where they've weathered.
        private static Surface PineBark(Canvas c)
        {
            Offsets wobble = c.Offsets(7f, octaves: 3);
            Cells plates = c.Cells(new Vector2(6f, 3.5f), jitter: 0.95f);
            Field id = plates.Id.Warp(wobble, 0.02f);
            Field edge = (plates.Border - 0.05f).Warp(wobble, 0.02f).Profile(0.12f, Rise.Round);
            Field flaking = c.Noise(new Vector2(18f, 10f), octaves: 4, warp: 0.06f);
            Field layers = (flaking + id * 0.4f).Quantize(4f);
            Field plateHeight = edge * (0.66f + 0.2f * id.Random(1) + (layers - 0.5f) * 0.16f) + (c.Noise(new Vector2(60f, 30f), octaves: 3) - 0.5f) * 0.04f;
            Paint plateColour = c.Ramp(id, (0f, Rgb(84, 52, 36)), (1f, Rgb(116, 76, 54))) * Field.Lerp(0.82f, 1.1f, flaking);
            plateColour = Paint.Lerp(plateColour, Rgb(104, 96, 88), plateHeight.Smooth(0.8f, 1f) * 0.5f);
            Surface plate = new Surface(plateColour, 0.9f, plateHeight);
            Field furrowHeight = 0.12f + c.Noise(new Vector2(40f, 4f), octaves: 3, kind: NoiseKind.Ridged) * 0.1f;
            Surface furrow = new Surface(Paint.Lerp(Rgb(28, 20, 16), Rgb(48, 34, 26), c.Noise(30f, octaves: 3)), 0.95f, furrowHeight);
            return furrow.Stack(plate, 0.03f);
        }
    }
}
