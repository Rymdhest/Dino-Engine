using Dino_Engine.Textures.Synthesis;
using OpenTK.Mathematics;
using static Dino_Engine.Textures.Synthesis.Colours;

namespace Dino_Engine.Textures
{
    // What's built: walls, roofs, boards and iron. A wall is seen at 67 degrees or more from the game's camera, so its relief shows mostly as the parallax of its
    // joints and the shadows in them, and v runs up a wall and up a roof's slope (MeshBuilder).
    public static partial class MaterialRecipes
    {
        // Limestone laid in rough courses in deep lime mortar, Crimson Desert's: stones of every size and few of them square, each a pillow bulging out of the
        // wall with its arrises worn round and its face broken into the planes it was dressed in, standing a little proud of or back from its neighbours; the
        // mortar pressed well back, so every joint is a line of shadow. Plinths, chimneys, stone houses and walls. The relief is deep (MaterialCatalog.StoneWall):
        // it's the shadows in the joints and the light rolling over each stone's rounded edge that make a wall look built and not painted. From the game's
        // camera a stone is twenty pixels across, so its face stays big, pale and quiet and the joints carry the shape: grime and pits on every stone read as noise.
        private static Surface StoneWall(Canvas c)
        {
            Offsets wobble = c.Offsets(9f, octaves: 3);
            const float Wander = 0.008f;
            // Sites keep to their rows and wander along them, so the stones lie in courses that undulate, every stone its own length; some are split into smaller
            // ones, as a waller fills out a course with what comes to hand.
            Cells big = c.Cells(new Vector2(4f, 7f), new Vector2(0.9f, 0.3f));
            Cells small = c.Cells(new Vector2(8f, 14f), new Vector2(0.9f, 0.35f));
            Field split = big.Id.Threshold(0.85f, 0f).Warp(wobble, Wander);
            Field border = Field.Lerp(big.Border, big.Border.Min(small.Border * 0.5f), split);
            Field centre = Field.Lerp(big.Centre, small.Centre, split);
            Field toX = Field.Lerp(big.ToSiteX, small.ToSiteX, split);
            Field toY = Field.Lerp(big.ToSiteY, small.ToSiteY, split);
            Field id = Field.Lerp(big.Id, small.Id, split).Warp(wobble, Wander);
            // Set back from each cell's border by the joint, and the border's corners rounded off, so that no stone has a sharp corner.
            border = (border - 0.06f).Warp(wobble, Wander).Blur(0.003f);
            centre = centre.Warp(wobble, Wander);

            // Rounded, but not cliffs at their edges: the game sees a wall from high up, and a cliff in a height is a few texels stretched across it.
            Field pillow = border.Profile(0.24f, Rise.Sine);
            Field crown = (1f - (centre * 1.2f).Pow(2f)).Clamp(0f, 1f);
            Field lean = (toX.Warp(wobble, Wander) * (id.Random(1) - 0.5f) + toY.Warp(wobble, Wander) * (id.Random(2) - 0.5f)) * 0.18f;
            Field dressed = c.Facets(new Vector2(11f, 19f), tilt: 0.45f, relief: 0.35f, crack: 0.06f, width: 0.05f).Height - 0.5f;
            Field pits = c.Noise(110f, octaves: 3, kind: NoiseKind.Billow);
            Field grain = c.Noise(36f, octaves: 5, gain: 0.55f) - 0.5f;
            Field stoneHeight = pillow * (0.76f + 0.1f * crown + 0.1f * id.Random(3) + dressed * 0.08f + grain * 0.04f) - lean * pillow - pits * 0.015f;

            Paint limestone = c.Ramp(id, (0f, Rgb(100, 96, 88)), (0.3f, Rgb(116, 110, 100)), (0.6f, Rgb(128, 120, 106)), (0.8f, Rgb(118, 108, 94)), (1f, Rgb(136, 130, 120)))
                .Jitter(id, 0.02f, 0.16f, 0.1f) * Field.Lerp(0.92f, 1.05f, c.Noise(44f, octaves: 3)) * Field.Lerp(0.96f, 1.03f, dressed + 0.5f);
            // A little grime round each stone's edge, and the weather keeps its face paler.
            limestone = limestone * Field.Lerp(0.82f, 1f, pillow.Smooth(0.05f, 0.6f));
            // Lichen keeps to the stones, never the mortar: small grey-green colonies, a few yellow.
            Field lichen = Patches(c, 14f, 0.1f, 0.05f, 0.1f) * pillow.Smooth(0.3f, 0.8f);
            limestone = Paint.Lerp(limestone, Paint.Lerp(Rgb(96, 104, 80), Rgb(146, 128, 58), c.Noise(20f, octaves: 3).Threshold(0.7f, 0.05f)), lichen * 0.5f);
            Surface stone = new Surface(limestone, Field.Lerp(0.94f, 0.82f, pillow), stoneHeight);

            // The mortar: lime pressed well back between the stones, rough and pale, dirtied in patches.
            Field mortarHeight = 0.2f + (c.Noise(80f, octaves: 3) - 0.5f) * 0.08f;
            Paint mortarColour = Paint.Lerp(Rgb(96, 90, 80), Rgb(118, 112, 100), c.Noise(50f, octaves: 3)) * Field.Lerp(0.88f, 1f, c.Noise(9f, octaves: 3));
            Surface mortar = new Surface(mortarColour, 0.97f, mortarHeight);

            // Small stones pinned into the mortar where the joints run wide, set back from the wall's face.
            Stones pins = c.Scatter(new Vector2(16f, 28f), count: 2, radius: 0.3f, radiusJitter: 0.5f, elongation: 1.2f, heightJitter: 0.4f, flatness: 0.2f);
            Field pinHeight = (pins.Cover * 0.28f + pins.Height * 0.3f) * pins.Id2.Threshold(0.75f, 0f);
            Paint pinColour = c.Ramp(pins.Id, (0f, Rgb(84, 80, 74)), (1f, Rgb(116, 110, 100))) * Field.Lerp(0.7f, 1f, pins.Height);
            Surface pinned = new Surface(pinColour, 0.9f, pinHeight);

            return mortar.Stack(pinned, 0.02f).Stack(stone, 0.03f);
        }

        // Old brick in a running bond: every brick its own shade of fired clay, edges chipped, lime mortar pressed back from the face.
        private static Surface Brick(Canvas c)
        {
            Bricks bricks = c.Bricks(rows: 16, perRow: 5, gap: 0.0095f, bond: 0.5f, rowJitter: 0.04f);
            Field chips = c.Noise(70f, octaves: 4).Threshold(0.72f, 0.05f);
            Field edge = bricks.Edge - chips * 0.005f;
            Field face = edge.Profile(0.006f, Rise.Round);
            Field mottle = c.Noise(40f, octaves: 4);
            Field brickHeight = face * (0.86f + 0.14f * bricks.Id.Random(1)) + (mottle - 0.5f) * 0.05f + (c.Noise(160f, octaves: 2) - 0.5f) * 0.03f;

            Paint fired = c.Ramp(bricks.Id, (0f, Rgb(56, 32, 28)), (0.15f, Rgb(82, 40, 30)), (0.55f, Rgb(100, 46, 32)), (0.9f, Rgb(112, 56, 38)), (1f, Rgb(124, 74, 50)))
                .Jitter(bricks.Id, 0.012f, 0.1f, 0.08f);
            fired = fired * Field.Lerp(0.85f, 1.1f, mottle);
            // Salt comes out of a wall pale, nearest the mortar.
            Field salt = (1f - edge.Smooth(0f, 0.012f)) * Patches(c, 5f, 0.35f, 0.15f);
            fired = Paint.Lerp(fired, Rgb(176, 168, 152), salt * 0.45f);
            Surface brick = new Surface(fired, 0.88f, brickHeight);

            Surface mortar = new Surface(Paint.Lerp(Rgb(116, 110, 98), Rgb(138, 130, 116), c.Noise(80f, octaves: 3)), 0.95f, 0.16f + (c.Noise(120f, octaves: 2) - 0.5f) * 0.08f);
            return mortar.Stack(brick, 0.02f);
        }

        // Lime plaster: trowelled smooth but never flat, hairline cracks, water stains running down, and here and there it's fallen away from the stone behind.
        private static Surface Plaster(Canvas c)
        {
            Field trowel = c.Noise(5f, octaves: 6, gain: 0.55f, warp: 0.06f);
            Field sand = c.Noise(260f, octaves: 2);
            Cells crazing = c.Cells(new Vector2(7f), jitter: 0.9f);
            Field crack = (1f - crazing.Border.Warp(c.Offsets(14f), 0.004f).Smooth(0.003f, 0.012f)) * crazing.Id.Threshold(0.7f, 0f);
            Field plasterHeight = 0.72f + (trowel - 0.5f) * 0.3f + (sand - 0.5f) * 0.03f - crack * 0.18f;

            Field stains = c.Noise(new Vector2(16f, 2f), octaves: 4, warp: 0.04f).Threshold(0.66f, 0.12f);
            Paint lime = Paint.Lerp(Rgb(140, 132, 114), Rgb(160, 152, 134), trowel) * Field.Lerp(0.95f, 1.03f, sand);
            lime = Paint.Lerp(lime, Rgb(104, 94, 76), stains * 0.55f);
            lime = Paint.Lerp(lime, Rgb(96, 88, 74), crack);
            Surface plaster = new Surface(lime, Field.Lerp(0.9f, 0.96f, sand), plasterHeight);

            // Where it's fallen away: the rubble it was laid on, well behind its face.
            Field fallen = Patches(c, 4f, 0.07f, 0.02f, 0.1f);
            Stones rubble = c.Scatter(new Vector2(14f), count: 2, radius: 0.5f, radiusJitter: 0.4f, elongation: 0.6f, heightJitter: 0.3f, flatness: 0.4f);
            Field rubbleHeight = 0.12f + rubble.Height * 0.35f;
            Surface behind = new Surface(c.Ramp(rubble.Id, (0f, Rgb(90, 84, 76)), (1f, Rgb(126, 116, 100))) * Field.Lerp(0.5f, 1f, rubble.Cover), 0.92f, rubbleHeight);
            return plaster.Mix(behind, fallen.Smooth(0.3f, 0.7f));
        }

        // Boards running up the tile, sawn from logs of their own: the grain arches where the heart came near, knots, the gaps dark. Pale, as a building stains it with
        // its vertex colour.
        private static Surface Wood(Canvas c)
        {
            Bricks boards = c.Bricks(rows: 8, perRow: 2, gap: 0.0045f, bond: 0f, rowJitter: 1f, lengthJitter: 0.6f, upright: true);
            Grain grain = c.Grain(boards, rings: 9f, waviness: 0.4f, knots: 0.35f);
            Field board = boards.Edge.Profile(0.0035f, Rise.Round);
            // Weather wears the soft early wood back, leaving the late wood standing.
            Field weathered = grain.Late * 0.07f - (grain.Pores - 0.5f) * 0.03f - grain.Knots * 0.03f;
            Field cup = ((boards.Across - 0.5f) * (boards.Across - 0.5f) * 4f - 0.33f) * (boards.Id.Random(2) - 0.5f) * 0.16f;
            Field height = board * (0.8f + 0.14f * boards.Id.Random(1) + weathered + cup);

            Paint wood = c.Ramp(boards.Id, (0f, Rgb(92, 68, 46)), (0.5f, Rgb(108, 82, 56)), (1f, Rgb(118, 92, 64))).Jitter(boards.Id, 0.01f, 0.12f, 0.08f);
            wood = wood * Field.Lerp(1f, 0.72f, grain.Late) * Field.Lerp(1f, 0.55f, grain.Knots) * Field.Lerp(0.95f, 1.04f, grain.Pores);
            wood = Paint.Lerp(Rgb(30, 22, 16), wood, board.Smooth(0f, 0.3f));
            return new Surface(wood, Field.Lerp(0.66f, 0.78f, grain.Late), height);
        }

        // Bundled reeds laid in courses down the roof, butt ends out, the course above lying over the top of the one below. Weathered grey where it's old, moss in the
        // damp. Courses run across the tile and the reeds run up it.
        private static Surface Thatch(Canvas c)
        {
            Offsets sag = c.Offsets(6f, octaves: 3);
            Bricks courses = c.Bricks(rows: 7, perRow: 5, gap: 0f, bond: 0.5f, rowJitter: 0.6f, lengthJitter: 0.45f);
            // A course's lower edge is never straight: bundles sag and stand out, and the reeds of the course above hang over it, each its own length, so the
            // edge is a fringe and its shadow is broken.
            Field fringe = c.Noise(new Vector2(170f, 8f), octaves: 2) - 0.5f;
            Field across = (courses.Across.Warp(sag, 0.014f) + fringe * 0.25f).Fract();
            // Thickest at the butts along a course's lower edge, thinning up to where the next course covers it, and in a shadow of its own just under that. Thatch
            // is nearly one surface, each course only a little proud of the one below, so the step is soft: a cliff there drew a black line under every course.
            Field lay = 0.62f + 0.38f * (1f - across).Pow(0.7f);
            Field reeds = c.Noise(new Vector2(150f, 4f), octaves: 4, kind: NoiseKind.Ridged, warp: 0.01f);
            Field bundles = c.Noise(new Vector2(26f, 3f), octaves: 3, warp: 0.03f);
            // The cut ends: a band of stubby ends along each course's lower edge, the dense texture that makes thatch read as thatch.
            Field band = 1f - across.Smooth(0f, 0.2f);
            Field butts = c.Noise(new Vector2(210f, 90f), octaves: 2, kind: NoiseKind.Billow) * band;
            Field height = lay * 0.5f + reeds * 0.18f + bundles * 0.14f + butts * 0.18f;

            // Greyed in many small patches: a few big ones repeat down a roof as a pattern.
            Field weather = Patches(c, 6f, 0.4f, 0.25f, 0.08f);
            Paint straw = Paint.Lerp(Rgb(112, 90, 54), Rgb(94, 84, 66), weather);
            straw = straw * Field.Lerp(0.6f, 1.12f, reeds) * Field.Lerp(0.72f, 1f, lay.Smooth(0.62f, 0.85f)) * Field.Lerp(0.85f, 1.1f, bundles);
            // The ends are cut fresh and pale, and dark between them where the light doesn't reach.
            straw = straw * Field.Lerp(1f, Field.Lerp(0.6f, 1.2f, butts.Smooth(0.1f, 0.35f)), band);
            Field moss = Patches(c, 5f, 0.08f, 0.08f) * lay.Smooth(0.65f, 0.9f);
            straw = Paint.Lerp(straw, Rgb(56, 64, 30), moss * 0.5f);
            return new Surface(straw, 0.96f, height) { Subsurface = 0.12f };
        }

        // Riven wooden shingles in courses down the roof, each thickest at its butt, split along its grain, weathered silver-brown.
        private static Surface Shingle(Canvas c)
        {
            Bricks rows = c.Bricks(rows: 9, perRow: 8, gap: 0.006f, bond: 0.5f, rowJitter: 0.8f, lengthJitter: 0.4f);
            Field taper = (1f - rows.Across).Pow(0.85f);
            Field grain = c.Noise(new Vector2(90f, 5f), octaves: 4, kind: NoiseKind.Ridged);
            Field splits = c.Noise(new Vector2(70f, 1f), octaves: 2, kind: NoiseKind.Ridged).Threshold(0.9f, 0.02f);
            Field face = rows.Edge.Profile(0.004f, Rise.Round);
            Field height = face * (0.3f + taper * 0.6f + grain * 0.07f - splits * 0.15f);

            Paint wood = c.Ramp(rows.Id, (0f, Rgb(84, 78, 72)), (0.6f, Rgb(108, 100, 92)), (1f, Rgb(118, 104, 88))).Jitter(rows.Id, 0.01f, 0.15f, 0.1f);
            wood = wood * Field.Lerp(0.8f, 1.06f, grain) * Field.Lerp(0.62f, 1f, taper.Smooth(0f, 0.45f));
            wood = Paint.Lerp(wood, Rgb(40, 36, 32), splits * 0.7f);
            return new Surface(wood, 0.84f, height);
        }

        // Split slates hung in courses, their edges broken, blue-grey and a little glossy where the cleavage catches the light.
        private static Surface Slate(Canvas c)
        {
            Offsets broken = c.Offsets(18f, octaves: 3);
            Bricks rows = c.Bricks(rows: 10, perRow: 6, gap: 0.005f, bond: 0.5f, rowJitter: 0.25f, lengthJitter: 0.2f);
            Field edge = rows.Edge.Warp(broken, 0.003f);
            Field taper = (1f - rows.Across).Pow(0.9f);
            Field cleavage = c.Noise(new Vector2(6f, 30f), octaves: 4).Quantize(6f);
            Field height = edge.Profile(0.003f, Rise.Linear) * (0.34f + taper * 0.58f + cleavage * 0.06f);

            Paint stone = c.Ramp(rows.Id, (0f, Rgb(56, 60, 68)), (0.5f, Rgb(70, 76, 86)), (1f, Rgb(82, 84, 90))).Jitter(rows.Id, 0.01f, 0.2f, 0.08f);
            stone = stone * Field.Lerp(0.75f, 1f, taper.Smooth(0f, 0.4f));
            return new Surface(stone, Field.Lerp(0.6f, 0.45f, cleavage), height);
        }

        // Steel plates riveted down, scuffed bright where they're walked on.
        private static Surface MetalFloor(Canvas c)
        {
            Bricks plates = c.Bricks(rows: 4, perRow: 2, gap: 0.004f, bond: 0.5f);
            Stones rivets = c.Scatter(new Vector2(16f, 16f), count: 1, radius: 0.12f, radiusJitter: 0f, elongation: 0f, heightJitter: 0f);
            Field nearEdge = 1f - plates.Edge.Smooth(0.01f, 0.025f);
            Field height = plates.Edge.Profile(0.002f, Rise.Round) * 0.7f + rivets.Height * nearEdge * 0.3f;
            Field scuff = c.Noise(new Vector2(30f, 30f), octaves: 4).Threshold(0.6f, 0.1f);
            return new Surface(Paint.Lerp(Rgb(150, 150, 152), Rgb(196, 196, 198), scuff), Field.Lerp(0.5f, 0.32f, scuff), height) { Metallic = 0.9f };
        }

        // Wrought iron: hammered, pitted, rusting where water sits.
        private static Surface Iron(Canvas c)
        {
            Cells hammer = c.Cells(new Vector2(18f), jitter: 0.9f);
            Field dents = hammer.Centre.Profile(0.6f, Rise.Smooth);
            Field pits = c.Noise(140f, octaves: 3, kind: NoiseKind.Billow).Threshold(0.72f, 0.06f);
            Field rust = Patches(c, 9f, 0.12f, 0.08f, 0.12f);
            Field height = 0.6f + dents * 0.25f - pits * 0.12f + rust * (c.Noise(60f, octaves: 3) - 0.3f) * 0.1f;
            Paint colour = Paint.Lerp(Rgb(168, 168, 168) * Field.Lerp(0.85f, 1f, dents), Rgb(92, 50, 32) * Field.Lerp(0.7f, 1.15f, c.Noise(40f, octaves: 3)), rust);
            return new Surface(colour, Field.Lerp(0.42f, 0.9f, rust), height) { Metallic = Field.Lerp(0.8f, 0f, rust) };
        }
    }
}
