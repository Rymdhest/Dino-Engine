using Dino_Engine.Textures.Synthesis;
using OpenTK.Mathematics;
using static Dino_Engine.Textures.Synthesis.Colours;

namespace Dino_Engine.Textures
{
    // Bark, pale, as a tree's colour and its moss are in its vertices (BarkSkin): only light and dark here, and a little warmth in what's deep. v runs up the trunk.
    //
    // Bark is what's left of a skin that couldn't stretch as its tree thickened, and that's how each of these is built: something that was whole, broken by a
    // diagram of pieces that are long up the trunk (Canvas.LongCells), each break opened by its own amount (Seam) so that some are a hand deep and some never
    // opened and two pieces are still one, with what the pieces are made of, layers, fibres, scales, showing where they broke. Three things make it bark and not a
    // drawing of bark, each found by getting it wrong: pieces are flat-backed and steep-sided, as what split apart is (rounded, they're something poured); their
    // edges are ragged at two sizes, the size of a thumb and the size of a nail (smooth, however they wander, they're marbling, which is what the first of these
    // were, and were called cartoons); and what's deep is darker and warmer but still bark, never a line of ink. See Bark in TEXTURES.md.
    public static partial class MaterialRecipes
    {
        /// <summary>
        /// A random of each break between two pieces of a diagram, the same from either side of it, for how far that one has opened. Without it every crack of a bark
        /// is as wide and as deep as every other, which is what a diagram looks like and nothing that grew does.
        /// </summary>
        private static Field Seam(Cells cells, int seed) => ((cells.Id + cells.Neighbour) * 0.5f).Random(seed);

        /// <summary>
        /// How far in from a torn edge a point is: a diagram's distance to its border, less the edge's raggedness at the size of a thumb and again at the size of a
        /// nail, which are noise <paramref name="coarse"/> and <paramref name="fine"/> times across and up the tile.
        /// </summary>
        private static Field Torn(Canvas c, Field border, Vector2 coarse, Vector2 fine, float amount)
        {
            return border - (c.Noise(coarse, octaves: 2) - 0.5f) * amount - (c.Noise(fine, octaves: 2, kind: NoiseKind.Ridged) - 0.5f) * (amount * 0.7f);
        }

        // Ridges running up the trunk that lean into each other, part and end, with fissures between them, most a finger wide and some a hand, and breaks across:
        // oak, elm, ash, old maple. A ridge's flanks come down in the steps of the layers it was laid in, its back is split again by finer checks and combed by its
        // fibres, and it's weathered pale on top where what's in the fissures is darker, warmer and newer.
        private static Surface BarkFurrowed(Canvas c)
        {
            Cells ridges = c.LongCells(new Vector2(20f, 3f), new Vector2(1f, 1f), new Vector2(0.26f, 0.05f), new Vector2(7f, 9f));
            Field gape = Seam(ridges, 1).Smooth(0.05f, 0.75f);
            Field rise = (Torn(c, ridges.Border, new Vector2(26f, 10f), new Vector2(70f, 30f), 0.14f) / Field.Lerp(0.07f, 0.3f, gape)).Profile(1f, Rise.Round);
            // Its flanks, half in steps: bark is laid down in layers, and a ridge's side is their edges.
            Field stepped = (rise * 1.1f + (c.Noise(new Vector2(50f, 16f), octaves: 3) - 0.5f) * 0.35f).Clamp().Quantize(3f);
            Field flank = Field.Lerp(rise, stepped, 0.55f);
            Field top = flank.Smooth(0.6f, 0.95f);

            Field plateau = 0.68f + 0.28f * ridges.Id2;
            Field height = Field.Lerp(Field.Lerp(plateau * 0.75f, 0.02f, gape), plateau, flank);

            // What a ridge's back is: split again by checks, most of them open a little, pitted, and combed along by its fibres.
            Cells checks = c.LongCells(new Vector2(46f, 11f), new Vector2(1f, 1f), new Vector2(0.25f, 0.08f));
            Field opened = Seam(checks, 3).Smooth(0.3f, 0.85f);
            Field check = (1f - (checks.Border / Field.Lerp(0.08f, 0.22f, opened)).Profile(1f, Rise.Sine)) * opened;
            Field fibre = c.Noise(new Vector2(150f, 12f), octaves: 2, kind: NoiseKind.Ridged);
            Field pits = c.Noise(new Vector2(110f, 70f), octaves: 2);
            Field lumps = c.Noise(new Vector2(14f, 5f), octaves: 3);
            height = height + ((lumps - 0.5f) * 0.1f + (fibre - 0.5f) * 0.09f + (pits - 0.5f) * 0.05f) * (0.4f + 0.6f * top) - check * 0.2f * top;
            height = height.Clamp();

            // Pale and weathered on its backs, darker and warmer down its flanks, and nearly earth in the bottom of a fissure: not ink, as what's down there is still bark.
            Paint colour = c.Ramp(height, (0f, Rgb(46, 37, 30)), (0.25f, Rgb(86, 72, 62)), (0.55f, Rgb(130, 117, 105)), (0.8f, Rgb(162, 153, 142)), (1f, Rgb(184, 178, 168)));
            colour = colour.Jitter(ridges.Id, 0.012f, 0.25f, 0.16f);
            // Slow weather down it, and pale lichen where a ridge's back has been still long enough.
            colour = colour * Field.Lerp(0.84f, 1.1f, c.Noise(new Vector2(5f, 2f), octaves: 4));
            Field lichen = Patches(c, 9f, 0.14f, 0.05f, 0.08f) * top * (1f - check);
            colour = Paint.Lerp(colour, Rgb(172, 180, 156), lichen * 0.45f);
            colour = colour * Field.Lerp(0.88f, 1.08f, fibre) * Field.Lerp(0.92f, 1.06f, pits);
            return new Surface(colour, Field.Lerp(0.96f, 0.86f, top), height);
        }

        // The thick plates of an old pine, and what both of a pine's deep barks are made of. Plates a few times taller than wide in fissures a hand deep; a plate is a
        // stack of thin scales, and where they've come away its face goes down in their edges, a few to a plate, crisp as the lines of a chart, which is what a
        // pine's bark is known by. `level` is how many are left, 0 to 1, for a colour to go by.
        private static Field PinePlates(Canvas c, out Field level, out Field face, out Cells plates)
        {
            plates = c.LongCells(new Vector2(11f, 3f), new Vector2(1f, 0.9f), new Vector2(0.28f, 0.08f), new Vector2(5f, 8f));
            Field gape = Seam(plates, 1).Smooth(0f, 0.55f);
            Field into = Torn(c, plates.Border, new Vector2(24f, 12f), new Vector2(60f, 34f), 0.12f);
            Field rise = (into / Field.Lerp(0.09f, 0.24f, gape)).Profile(1f, Rise.Round);
            face = rise.Smooth(0.7f, 1f);

            // The scales. They lie as a plate's outline does, each a little smaller than the one under it, so a plate steps up from its fissures to its middle; how far in
            // each ends wanders, and thinner ones lie on those.
            Field lie = c.Noise(new Vector2(12f, 7f), octaves: 3, warp: 0.06f);
            Field broad = (into * 1.7f + (lie - 0.5f) * 1.2f + plates.Id2 * 0.25f).Clamp().Quantize(5f);
            Field thin = c.Noise(new Vector2(34f, 20f), octaves: 3, warp: 0.05f).Quantize(3f);
            level = (broad * 0.85f + thin * 0.2f).Clamp();
            Field grit = c.Noise(new Vector2(120f, 80f), octaves: 2);
            Field plateau = 0.48f + level * 0.46f + (grit - 0.5f) * 0.06f;
            Field bottom = 0.02f + c.Noise(new Vector2(60f, 8f), octaves: 3, kind: NoiseKind.Ridged) * 0.07f;
            return Field.Lerp(Field.Lerp(plateau * 0.55f, bottom, gape), plateau, rise).Clamp();
        }

        // The foot of an old pine: its plates weathered grey on their oldest scales and warm where scales have come away, the fissures' walls a pine's red-brown. The
        // tree's own colour is in its vertices. It gives way to BarkFlaking up the trunk (MaterialCatalog.GivesWay), which shows first where this is lowest, in the
        // fissures.
        private static Surface BarkFissured(Canvas c)
        {
            Field height = PinePlates(c, out Field level, out Field face, out Cells plates);
            Paint old = Paint.Lerp(Rgb(160, 120, 96), Rgb(176, 170, 164), level.Smooth(0.15f, 0.75f));
            Paint colour = Paint.Lerp(c.Ramp(height, (0f, Rgb(48, 36, 29)), (0.4f, Rgb(104, 77, 60)), (1f, Rgb(144, 111, 90))), old, face);
            colour = colour.Jitter(plates.Id, 0.01f, 0.2f, 0.12f) * Field.Lerp(0.88f, 1.08f, c.Noise(new Vector2(60f, 40f), octaves: 3));
            return new Surface(colour, Field.Lerp(0.95f, 0.88f, face), height);
        }

        // The bark of a pine log, for what's built of one: the same plates, in a pine's own red-brown, greyer where they're oldest.
        private static Surface PineBark(Canvas c)
        {
            Field height = PinePlates(c, out Field level, out Field face, out Cells plates);
            Paint old = Paint.Lerp(Rgb(95, 54, 34), Rgb(98, 77, 65), level.Smooth(0.2f, 0.8f));
            Paint colour = Paint.Lerp(c.Ramp(height, (0f, Rgb(19, 12, 9)), (0.4f, Rgb(51, 28, 18)), (1f, Rgb(80, 45, 28))), old, face);
            colour = colour.Jitter(plates.Id, 0.01f, 0.2f, 0.12f) * Field.Lerp(0.88f, 1.08f, c.Noise(new Vector2(60f, 40f), octaves: 3));
            return new Surface(colour, Field.Lerp(0.95f, 0.88f, face), height);
        }

        // Thin scales the size of a hand, lying on each other like the scales of a fish, each curling off at its lower edge, with here and there one gone and the
        // warmer bark under it showing: spruce, larch, an old sycamore.
        private static Surface BarkPlated(Canvas c)
        {
            Cells scales = c.LongCells(new Vector2(16f, 11f), new Vector2(1f, 1f), new Vector2(0.22f, 0.2f), new Vector2(7f, 7f));
            Field into = Torn(c, scales.Border, new Vector2(30f, 24f), new Vector2(80f, 60f), 0.1f);
            Field rim = (into / Field.Lerp(0.07f, 0.18f, Seam(scales, 1))).Profile(1f, Rise.Round);
            Field gone = scales.Id.Threshold(0.84f, 0f);
            // A scale stands highest at its lower edge, where it's curling off, and goes under its neighbour above; and its edge all round stands a little over its middle.
            Field lift = (scales.ToSiteY * 0.6f + 0.5f).Clamp();
            Field curl = 1f - (into / 0.42f).Profile(1f, Rise.Smooth);
            Field grain = c.Noise(new Vector2(90f, 60f), octaves: 2);
            Field body = 0.42f + 0.22f * scales.Id2 + lift * 0.24f + curl * 0.1f + (grain - 0.5f) * 0.07f;
            Field under = 0.18f + (grain - 0.5f) * 0.1f;
            Field height = (Field.Lerp(body, under, gone) * rim + (1f - rim) * 0.06f).Clamp();

            // Loose scales lying over those, each lifting at its edge: without them every scale meets its neighbours along a line, and the trunk is paved.
            Offsets torn = c.Offsets(16f, octaves: 3);
            Stones loose = c.Scatter(new Vector2(9f, 7f), count: 2, radius: 0.5f, radiusJitter: 0.4f, elongation: 1.2f, heightJitter: 0.3f, flatness: 0.9f, turn: 0f, turnJitter: 0.6f);
            Field over = loose.Cover.Warp(torn, 0.02f) * loose.Id2.Warp(torn, 0.02f).Threshold(0.45f, 0f);
            Field edge = loose.Out.Warp(torn, 0.02f).Smooth(0.6f, 0.98f) * over;
            Field onTop = 0.62f + (loose.Id.Warp(torn, 0.02f) - 0.5f) * 0.14f + edge * 0.16f + (grain - 0.5f) * 0.07f;
            height = Field.Lerp(height, height.Max(onTop), over).Clamp();

            // Each scale a grey of its own, paler where it's lifting: no two alike, but near enough that none is a patch on a soldier's jacket.
            Paint scale = Paint.Lerp(Rgb(138, 128, 119), Rgb(180, 172, 164), scales.Id2 * 0.6f + lift * 0.4f).Jitter(scales.Id, 0.02f, 0.3f, 0.12f);
            Paint colour = Paint.Lerp(scale, Paint.Lerp(Rgb(126, 92, 74), Rgb(156, 116, 94), grain), gone);
            colour = Paint.Lerp(Rgb(54, 44, 37), colour, rim.Smooth(0.1f, 0.85f));
            colour = Paint.Lerp(colour, Paint.Lerp(Rgb(150, 140, 131), Rgb(186, 178, 168), loose.Id.Warp(torn, 0.02f)), over);
            // A hairline of shadow under a loose scale's edge, and all of it darker the lower it lies.
            colour = Paint.Lerp(colour, Rgb(62, 51, 44), (over * (1f - over) * 4f).Clamp() * 0.7f);
            colour = colour * Field.Lerp(0.78f, 1.08f, height) * Field.Lerp(0.88f, 1.08f, c.Noise(new Vector2(6f, 4f), octaves: 4)) * Field.Lerp(0.92f, 1.06f, grain);
            return new Surface(colour, Field.Lerp(0.95f, 0.87f, rim), height);
        }

        // Birch: smooth and chalk-white, in thin sheets one over another whose edges run across the trunk and curl, with dark lenticels lying across it, most of them
        // short, and black rough scars, split through, where branches were.
        private static Surface BarkPapery(Canvas c)
        {
            // The sheets: each a little lower than the one that's peeled back over it, their edges running across.
            Field lay = c.Noise(new Vector2(1f, 9f), octaves: 3, warp: 0.02f);
            Field sheets = lay.Quantize(5f);
            Field edge = (lay * 5f).Fract();
            Field curl = edge.Smooth(0.8f, 1f) * (1f - edge.Smooth(0.97f, 1f));
            // Half the lenticels there could be, of every length from a dot to a dash: all of them, all alike, are the bars of a price tag.
            Stones lenticels = c.Scatter(new Vector2(11f, 34f), count: 2, radius: 0.3f, radiusJitter: 0.9f, elongation: 13f, heightJitter: 0.3f, turn: 0f, turnJitter: 0.012f);
            Field mark = lenticels.Cover.Smooth(0.15f, 0.7f) * lenticels.Id2.Threshold(0.5f, 0f);
            Field ribs = c.Noise(new Vector2(3f, 80f), octaves: 2);

            // The scars: a few dark eyes, their outlines torn, rough and split inside.
            Stones eyes = c.Scatter(new Vector2(3f, 3f), count: 1, radius: 0.3f, radiusJitter: 0.5f, elongation: 1.8f, heightJitter: 0.2f, turn: 0f, turnJitter: 0.04f);
            Offsets torn = c.Offsets(12f, octaves: 4);
            Field eye = (eyes.Cover * eyes.Id2.Threshold(0.5f, 0f)).Warp(torn, 0.03f);
            Field scar = (eye + (c.Noise(new Vector2(30f, 30f), octaves: 3) - 0.5f) * 0.5f).Smooth(0.3f, 0.6f);
            Cells split = c.LongCells(new Vector2(26f, 9f), new Vector2(1f, 1f), new Vector2(0.3f, 0.2f));
            Field rough = (Torn(c, split.Border, new Vector2(40f, 30f), new Vector2(90f, 60f), 0.12f) / 0.2f).Profile(1f, Rise.Round) * (0.55f + 0.45f * split.Id2);

            Field paperHeight = 0.72f + sheets * 0.14f + curl * 0.08f + (ribs - 0.5f) * 0.04f + mark * 0.05f;
            Field height = Field.Lerp(paperHeight, 0.2f + rough * 0.55f, scar).Clamp();

            Paint paper = Paint.Lerp(Rgb(222, 218, 208), Rgb(198, 190, 178), c.Noise(new Vector2(4f, 26f), octaves: 4));
            // Where a sheet has peeled back the one under it is newer, and pink with it.
            paper = Paint.Lerp(paper, Rgb(212, 182, 156), (1f - sheets.Smooth(0.2f, 0.5f)) * 0.55f);
            paper = Paint.Lerp(paper, Rgb(112, 100, 90), curl * 0.55f);
            paper = Paint.Lerp(paper, Paint.Lerp(Rgb(44, 38, 34), Rgb(110, 98, 88), lenticels.Id), mark * 0.9f);
            Paint colour = Paint.Lerp(paper, Paint.Lerp(Rgb(10, 9, 8), Rgb(44, 39, 36), rough), scar);
            // A grey bruise round a scar, where it runs into the white.
            colour = Paint.Lerp(colour, Rgb(126, 120, 114), eye.Smooth(0f, 0.3f) * (1f - scar) * 0.5f);
            return new Surface(colour, Field.Lerp(0.66f, 0.94f, scar), height);
        }

        // Tight and fine: a skin that stretched, with lenticels in rows lying across it, faint folds across where it bunches, the hair-thin lines of its stretching up
        // it, and blotches of lichen and damp. Beech, cherry, the young of most trees, and canes.
        private static Surface BarkSmooth(Canvas c)
        {
            Stones lenticels = c.Scatter(new Vector2(13f, 46f), count: 1, radius: 0.3f, radiusJitter: 0.6f, elongation: 5f, heightJitter: 0.3f, turn: 0f, turnJitter: 0.03f);
            Field mark = lenticels.Cover * lenticels.Id2.Threshold(0.5f, 0f);
            Field folds = c.Noise(new Vector2(2f, 26f), octaves: 3, warp: 0.03f);
            Field stretch = c.Noise(new Vector2(80f, 6f), octaves: 2, kind: NoiseKind.Ridged);
            Field swell = c.Noise(new Vector2(3f, 2f), octaves: 4);
            Field height = (0.66f + (swell - 0.5f) * 0.3f + (folds - 0.5f) * 0.14f + (stretch - 0.5f) * 0.06f + mark * lenticels.Height * 0.2f).Clamp();

            Paint colour = Paint.Lerp(Rgb(146, 139, 130), Rgb(180, 174, 166), c.Noise(new Vector2(4f, 7f), octaves: 5));
            // Lichen in pale blotches and damp in dark ones, both soft-edged and faint: with hard edges a smooth bark is a soldier's jacket.
            colour = Paint.Lerp(colour, Rgb(196, 198, 186), Patches(c, 6f, 0.2f, 0.12f, 0.1f) * 0.4f);
            colour = Paint.Lerp(colour, Rgb(116, 110, 102), Patches(c, 3f, 0.18f, 0.22f, 0.08f) * 0.4f);
            colour = Paint.Lerp(colour, Rgb(104, 94, 86), mark * 0.5f);
            colour = colour * Field.Lerp(0.94f, 1.05f, stretch);
            return new Surface(colour, Field.Lerp(0.8f, 0.9f, swell), height);
        }

        // Long soft strips running the length of the trunk, each lying over the next and fraying where it ends, stringy with its own fibres: redwood, cedar,
        // cypress, juniper.
        private static Surface BarkFibrous(Canvas c)
        {
            Cells strips = c.LongCells(new Vector2(22f, 1f), new Vector2(1f, 1f), new Vector2(0.2f, 0.03f), new Vector2(7f, 4f));
            Field gape = Seam(strips, 1).Smooth(0.05f, 0.85f);
            Field into = Torn(c, strips.Border, new Vector2(30f, 5f), new Vector2(90f, 14f), 0.16f);
            Field rise = (into / Field.Lerp(0.07f, 0.3f, gape)).Profile(1f, Rise.Round);
            Field fibre = c.Noise(new Vector2(140f, 5f), octaves: 2, kind: NoiseKind.Ridged);
            Field strand = c.Noise(new Vector2(50f, 2f), octaves: 3, kind: NoiseKind.Ridged, warp: 0.015f);
            Field lie = 0.6f + 0.26f * strips.Id2;
            Field height = Field.Lerp(Field.Lerp(lie * 0.7f, 0.03f, gape), lie, rise);
            height = (height + ((strand - 0.5f) * 0.22f + (fibre - 0.5f) * 0.1f) * (0.3f + 0.7f * rise)).Clamp();

            Paint colour = c.Ramp(height, (0f, Rgb(52, 43, 38)), (0.3f, Rgb(98, 87, 80)), (0.65f, Rgb(140, 132, 126)), (1f, Rgb(180, 175, 170)));
            colour = colour.Jitter(strips.Id, 0.012f, 0.25f, 0.16f) * Field.Lerp(0.86f, 1.08f, strand) * Field.Lerp(0.92f, 1.05f, fibre);
            return new Surface(colour, 0.94f, height);
        }

        // The scars of old leaves in rings round the trunk, each a lip that stands over the band below it, and the band between them split up and down by its
        // fibres: palms, tree ferns, cycads.
        private static Surface BarkRinged(Canvas c)
        {
            Field wander = (c.Noise(new Vector2(3f, 2f), octaves: 3) - 0.5f) * 0.6f;
            Field rings = (c.V * 11f + wander).Fract();
            // A band rises to the lip at its top and drops off it.
            Field band = rings.Smooth(0f, 0.86f) * (1f - rings.Smooth(0.93f, 1f));
            Cells splits = c.LongCells(new Vector2(34f, 11f), new Vector2(1f, 0.6f), new Vector2(0.15f, 0.05f));
            Field split = (Torn(c, splits.Border, new Vector2(60f, 20f), new Vector2(120f, 40f), 0.1f) / Field.Lerp(0.05f, 0.2f, Seam(splits, 1))).Profile(1f, Rise.Sine);
            Field fibre = c.Noise(new Vector2(110f, 7f), octaves: 2, kind: NoiseKind.Ridged);
            Field height = (0.2f + band * 0.6f) * (0.55f + 0.45f * split) + (fibre - 0.5f) * 0.09f + (c.Noise(new Vector2(8f, 5f), octaves: 3) - 0.5f) * 0.08f;
            height = height.Clamp();

            Paint colour = c.Ramp(height, (0f, Rgb(72, 63, 57)), (0.4f, Rgb(130, 121, 114)), (0.75f, Rgb(171, 164, 158)), (1f, Rgb(198, 194, 189)));
            colour = colour.Jitter(splits.Id, 0.012f, 0.2f, 0.12f) * Field.Lerp(0.9f, 1.06f, fibre);
            return new Surface(colour, 0.92f, height);
        }

        // Wood with the bark long gone: silvered, combed along its grain, its growth showing as long soft waves, and checked, with long cracks along the grain, some
        // of them deep, where it dried.
        private static Surface BarkBare(Canvas c)
        {
            Field grain = c.Noise(new Vector2(60f, 2f), octaves: 4, kind: NoiseKind.Ridged, warp: 0.015f);
            Field waves = c.Noise(new Vector2(9f, 1f), octaves: 3, warp: 0.03f);
            Cells checks = c.LongCells(new Vector2(18f, 2f), new Vector2(1f, 1f), new Vector2(0.16f, 0.03f), new Vector2(6f, 5f));
            // A check opens and closes along its length, so it's a slit that tapers out at both ends: open all the way round a piece of the diagram it's an outline
            // someone drew, which wood never dries into.
            Field opened = Seam(checks, 1).Smooth(0.25f, 0.8f) * c.Noise(new Vector2(5f, 4f), octaves: 2).Smooth(0.42f, 0.62f);
            Field check = (1f - (Torn(c, checks.Border, new Vector2(40f, 6f), new Vector2(100f, 12f), 0.05f) / Field.Lerp(0.04f, 0.14f, opened)).Profile(1f, Rise.Sine)) * opened;
            Field height = (0.76f + (waves - 0.5f) * 0.18f + (grain - 0.5f) * 0.2f - check * 0.7f).Clamp();

            Paint colour = Paint.Lerp(Rgb(146, 142, 136), Rgb(208, 204, 198), (grain * 0.65f + waves * 0.35f).Smooth(0.25f, 0.75f));
            colour = Paint.Lerp(colour, Rgb(48, 43, 39), check.Smooth(0.15f, 0.8f) * 0.9f);
            colour = colour * Field.Lerp(0.9f, 1.07f, c.Noise(new Vector2(5f, 3f), octaves: 4));
            return new Surface(colour, Field.Lerp(0.78f, 0.9f, grain), height);
        }

        // Burnt: char in blocks, as a log in a fire's embers is, cracked deep between and crazed across each block, black with a dull shine on what split clean and
        // a dusting of ash where it lies level.
        private static Surface BarkCharred(Canvas c)
        {
            Cells blocks = c.LongCells(new Vector2(9f, 7f), new Vector2(0.6f, 0.6f), new Vector2(0.16f, 0.14f), new Vector2(5f, 5f));
            Field crack = (Torn(c, blocks.Border, new Vector2(30f, 24f), new Vector2(70f, 60f), 0.08f) / Field.Lerp(0.1f, 0.26f, Seam(blocks, 1))).Profile(1f, Rise.Round);
            // The craze: finer cracks through every block, bent, each open by its own amount. All alike and straight they're wire netting, and only a few of them
            // open they're scratches.
            Cells craze = c.LongCells(new Vector2(30f, 22f), new Vector2(0.9f, 0.9f), new Vector2(0.22f, 0.2f), new Vector2(9f, 9f));
            Field crazed = Seam(craze, 2).Smooth(0.1f, 0.8f);
            Field fine = 1f - (1f - (craze.Border / Field.Lerp(0.04f, 0.12f, crazed)).Profile(1f, Rise.Round)) * crazed;
            Field pillow = (blocks.Centre * 1.5f).Clamp().Invert();
            Field height = (crack * (0.58f + 0.26f * blocks.Id2 + pillow * 0.14f) * (0.88f + 0.12f * fine) + (c.Noise(new Vector2(70f, 70f), octaves: 2) - 0.5f) * 0.05f).Clamp();

            Field face = crack * fine;
            Paint colour = Paint.Lerp(Rgb(8, 7, 7), Rgb(44, 41, 39), face * Field.Lerp(0.55f, 1f, blocks.Id2));
            Field ash = Patches(c, 11f, 0.12f, 0.1f, 0.06f) * crack.Smooth(0.6f, 1f);
            colour = Paint.Lerp(colour, Rgb(104, 100, 96), ash * 0.35f);
            return new Surface(colour, Field.Lerp(0.95f, 0.5f, face.Smooth(0.5f, 1f) * (1f - ash)), height);
        }

        // A pine's upper trunk and its limbs: thin bark, pale and smooth where it's new, under papery flakes that peel across it, each flat, torn at its edge and
        // lifting there, with a hairline of shadow where it stands off what it lies on. It's shallow, a sixth of the plates' depth, as the bark is thin.
        private static Surface BarkFlaking(Canvas c)
        {
            Field striae = c.Noise(new Vector2(4f, 48f), octaves: 3);
            Field undulation = (c.Noise(new Vector2(3f, 5f), octaves: 4) - 0.5f) * 0.16f;
            Field under = 0.46f + undulation + (striae - 0.5f) * 0.07f;
            // Torn, not cut: every flake's outline wanders, by the same warp for all it says, so they agree. Two sizes of them, the small on the large.
            Offsets torn = c.Offsets(18f, octaves: 3);
            Stones large = c.Scatter(new Vector2(5f, 8f), count: 3, radius: 0.55f, radiusJitter: 0.5f, elongation: 2.6f, heightJitter: 0.3f, flatness: 0.9f, turn: 0f, turnJitter: 0.3f);
            Stones small = c.Scatter(new Vector2(11f, 17f), count: 2, radius: 0.45f, radiusJitter: 0.6f, elongation: 2.2f, heightJitter: 0.4f, flatness: 0.9f, turn: 0f, turnJitter: 0.5f);
            Field coverLarge = large.Cover.Warp(torn, 0.02f), coverSmall = small.Cover.Warp(torn, 0.014f) * small.Id2.Warp(torn, 0.014f).Threshold(0.45f, 0f);
            Field liftLarge = large.Out.Warp(torn, 0.02f).Smooth(0.66f, 0.98f) * coverLarge, liftSmall = small.Out.Warp(torn, 0.014f).Smooth(0.6f, 0.98f) * coverSmall;
            Field height = Field.Lerp(under, 0.66f + (large.Id.Warp(torn, 0.02f) - 0.5f) * 0.12f + liftLarge * 0.14f + undulation, coverLarge);
            height = Field.Lerp(height, height + 0.1f + liftSmall * 0.12f, coverSmall).Clamp();

            // The fresh bark under the flakes is the palest of it; old flakes are greyer, each its own shade, so the trunk is mottled from far off and not one flat colour.
            Paint colour = Paint.Lerp(Rgb(236, 224, 210), Rgb(212, 200, 188), striae);
            Paint flake = Paint.Lerp(Rgb(196, 187, 178), Rgb(150, 141, 133), large.Id2.Warp(torn, 0.02f)) * Field.Lerp(0.92f, 1.05f, c.Noise(new Vector2(18f, 10f), octaves: 3));
            colour = Paint.Lerp(colour, flake, coverLarge);
            colour = Paint.Lerp(colour, Paint.Lerp(Rgb(206, 196, 186), Rgb(166, 156, 148), small.Id.Warp(torn, 0.014f)), coverSmall);
            Field rim = ((coverLarge * (1f - coverLarge) + coverSmall * (1f - coverSmall)) * 4f).Clamp();
            colour = Paint.Lerp(colour, Rgb(78, 66, 58), rim * 0.7f);
            return new Surface(colour, Field.Lerp(0.72f, 0.86f, coverLarge.Max(coverSmall)), height);
        }
    }
}
