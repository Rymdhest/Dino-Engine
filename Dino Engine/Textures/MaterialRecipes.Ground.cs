using Dino_Engine.Modelling.Procedural.Architecture.Waygates;
using Dino_Engine.Textures.Synthesis;
using OpenTK.Mathematics;
using static Dino_Engine.Textures.Synthesis.Colours;

namespace Dino_Engine.Textures
{
    // The ground: what TerrainSurface paints the land with, seen from 33 m up at 67 degrees, where a tile of three metres is ninety pixels across.
    public static partial class MaterialRecipes
    {
        // Rounded stones set in packed earth and worn smooth on top by feet and wheels: the old road through a village.
        private static Surface Cobble(Canvas c)
        {
            Cells cells = c.Cells(new Vector2(7f), jitter: 0.85f);
            Offsets wobble = c.Offsets(9f, octaves: 3);
            const float Wander = 0.007f;
            // Set back from the cell's edge, so that the joints between are a few centimetres and not a line.
            Field border = (cells.Border - 0.035f).Warp(wobble, Wander).Blur(0.002f);
            Field centre = cells.Centre.Warp(wobble, Wander);
            Field id = cells.Id.Warp(wobble, Wander);

            // A stone is a loaf: a rounded rim, a gentle crown, a little higher or lower than its neighbours and leaning a little, pitted.
            Field rim = border.Profile(0.12f, Rise.Round);
            Field crown = (1f - (centre * 1.35f).Pow(2f)).Clamp(0f, 1f);
            Field lean = (cells.ToSiteX.Warp(wobble, Wander) * (id.Random(1) - 0.5f) + cells.ToSiteY.Warp(wobble, Wander) * (id.Random(2) - 0.5f)) * 0.22f;
            Field grain = c.Noise(30f, octaves: 5, gain: 0.55f) - 0.5f;
            Field pits = c.Noise(110f, octaves: 3, kind: NoiseKind.Billow);
            Field stoneHeight = rim * (0.66f + 0.3f * crown) * (0.84f + 0.16f * id.Random(3)) - lean * rim + grain * 0.07f - pits * 0.025f;

            Field speckle = c.Noise(150f, octaves: 2);
            Paint stoneColour = c.Ramp(id,
                (0.0f, Rgb(48, 46, 44)), (0.3f, Rgb(72, 68, 63)), (0.6f, Rgb(92, 86, 78)),
                (0.82f, Rgb(98, 80, 65)), (1.0f, Rgb(112, 106, 96)))
                .Jitter(id, 0.02f, 0.2f, 0.12f) * Field.Lerp(0.85f, 1.1f, speckle) * Field.Lerp(0.82f, 1.12f, c.Noise(18f, octaves: 4));
            // The tops are rubbed pale and smooth; what's sunk near the joint keeps the dirt.
            Field worn = (rim * crown).Smooth(0.55f, 0.95f);
            stoneColour = stoneColour * Field.Lerp(0.6f, 1f, rim.Smooth(0.15f, 0.9f)) * Field.Lerp(1f, 1.12f, worn);
            Surface stone = new Surface(stoneColour, Field.Lerp(0.84f, 0.56f, worn), stoneHeight);

            // The bed between: earth beaten hard, grit in it, and moss where the joints stay damp.
            Field bedHeight = 0.34f + (c.Noise(40f, octaves: 6) - 0.5f) * 0.14f + (c.Noise(4f, octaves: 3) - 0.5f) * 0.12f;
            Field grit = c.Noise(220f, octaves: 2).Threshold(0.64f, 0.05f);
            Paint earth = Paint.Lerp(Rgb(34, 26, 18), Rgb(58, 46, 32), c.Noise(14f, octaves: 4));
            earth = Paint.Lerp(earth, Rgb(110, 100, 84), grit * 0.5f);
            Field moss = Patches(c, 5f, 0.3f, 0.12f);
            Surface bed = new Surface(Paint.Lerp(earth, Rgb(46, 56, 26), moss * 0.75f), 0.95f, bedHeight);

            return bed.Stack(stone, softness: 0.025f);
        }

        // The waygates' own stone laid as paving (WAYGATES.md): slabs a metre deep in courses running across the tile, each its own length, sitting a little high or low
        // and leaning a little, and each a shade of its own, with every arris taken off in the same straight 5 cm chamfer as the gates' blocks (Masonry), so that every
        // joint is a V of light and shadow as theirs are. Their faces are the gates' stone itself, RockSurface's facets and grain at RockSurface's own size (its 3 m
        // tile to this one's 8), in the Haven gate's colour, plinth and all (WaygateForge.Stone's tint and its shade near the ground), so the steps run on into the
        // paving as one stone. Grit fills the bottom of each joint.
        private static Surface Flagstones(Canvas c)
        {
            Bricks slabs = c.Bricks(rows: 8, perRow: 6, gap: 0.0016f, bond: 0.5f, rowJitter: 0.35f, lengthJitter: 0.4f);
            Offsets wobble = c.Offsets(6f, octaves: 3);
            Field edge = slabs.Edge.Warp(wobble, 0.0012f);
            Field id = slabs.Id;
            Field bevel = edge.Profile(0.006f, Rise.Linear);

            Facets faces = c.Facets(new Vector2(13f, 11f), tilt: 0.5f, relief: 0.4f, crack: 0.35f, width: 0.03f);
            Field grain = c.Noise(107f, octaves: 6, gain: 0.55f);
            Field face = (faces.Height + grain * 0.22f).Normalize();
            Field settle = (id.Random(1) - 0.5f) * 0.07f + ((slabs.Along - 0.5f) * (id.Random(2) - 0.5f) + (slabs.Across - 0.5f) * (id.Random(3) - 0.5f)) * 0.05f;
            Field slabHeight = 0.18f + bevel * (0.66f + 0.1f * face + settle);

            Vector3 tint = WaygateDesign.GreatStone * WaygateDesign.GroundShade;
            Paint stone = c.Ramp(faces.Id, (0f, RockDark * tint), (1f, RockPale * tint)) * Field.Lerp(0.8f, 1.05f, face) * Field.Lerp(0.91f, 1.08f, id.Random(4));
            stone = stone.Jitter(id, 0.012f, 0.1f, 0.03f) * Field.Lerp(0.74f, 1f, bevel.Smooth(0f, 0.8f));
            Surface slab = new Surface(stone, 0.88f, slabHeight);

            Paint grit = Paint.Lerp(Rgb(46, 40, 32), Rgb(68, 60, 47), c.Noise(60f, octaves: 3));
            Surface joint = new Surface(grit, 0.95f, 0.25f + (c.Noise(150f, octaves: 2) - 0.5f) * 0.04f);
            return joint.Stack(slab, 0.01f);
        }

        // Dark loose earth: crumbs and clods, a few small stones half sunk in it, paler where it's dried.
        private static Surface Soil(Canvas c)
        {
            Stones crumbs = c.Scatter(new Vector2(26f), count: 4, radius: 0.36f, radiusJitter: 0.6f, elongation: 0.8f, heightJitter: 0.6f);
            Field crumb = crumbs.Height;
            Field lumps = c.Noise(5f, octaves: 5, gain: 0.5f);
            Field fines = c.Noise(90f, octaves: 3);
            Field earthHeight = lumps * 0.5f + crumb * 0.32f + fines * 0.1f;
            Field dry = Patches(c, 5f, 0.4f, 0.2f);
            Paint earth = c.Ramp(earthHeight, (0.15f, Rgb(36, 24, 16)), (0.45f, Rgb(66, 44, 28)), (0.8f, Rgb(98, 68, 44)));
            earth = Paint.Lerp(earth, earth.Saturation(0.7f) * 1.14f, dry * crumb.Smooth(0.3f, 0.8f));
            Surface ground = new Surface(earth, Field.Lerp(0.9f, 0.97f, dry), earthHeight);
            ground = ground.Stack(LooseStones(c, 7f, 0.16f, Rgb(84, 76, 66), lift: 0.3f), softness: 0.02f);
            return ground.Stack(Twigs(c, 9f, 0.38f), 0.015f);
        }

        // Small stones lying in loose ground: a Surface whose height is nothing where there's no stone, so it shows only where a stone stands out of what's around it.
        private static Surface LooseStones(Canvas c, float frequency, float radius, Vector3 colour, int count = 2, float lift = 0.42f)
        {
            Stones stones = c.Scatter(frequency, count: count, radius: radius, radiusJitter: 0.55f, elongation: 0.7f, heightJitter: 0.6f, flatness: 0.15f);
            Field height = stones.Cover * lift + stones.Height * 0.5f + (c.Noise(80f, octaves: 3) - 0.5f) * 0.04f;
            Paint shade = (Paint)colour * Field.Lerp(0.62f, 1.2f, stones.Id) * Field.Lerp(0.8f, 1.05f, stones.Height);
            return new Surface(shade.Jitter(stones.Id, 0.03f, 0.3f, 0.1f), 0.72f, height);
        }

        // Bits of dead twig and root lying on the earth.
        private static Surface Twigs(Canvas c, float frequency, float lift)
        {
            Stones twigs = c.Scatter(frequency, count: 2, radius: 0.45f, radiusJitter: 0.4f, elongation: 12f, heightJitter: 0.5f);
            Field show = twigs.Id2.Threshold(0.55f, 0f);
            Field height = (twigs.Cover * lift + twigs.Height * 0.25f) * show;
            return new Surface((Paint)Rgb(58, 42, 28) * Field.Lerp(0.7f, 1.2f, twigs.Id), 0.85f, height);
        }

        // Turf under the grass: matted dead blades over dark earth, moss in the gaps, green coming through. Most of it is under blades, and what shows reads as more grass.
        private static Surface GrassGround(Canvas c)
        {
            Field earthHeight = c.Noise(12f, octaves: 5, kind: NoiseKind.Billow).Remap(0.2f, 0.8f, 0.05f, 0.4f);
            Surface earth = new Surface(Paint.Lerp(Rgb(44, 34, 22), Rgb(66, 52, 32), earthHeight), 0.95f, earthHeight);
            Field mossHeight = c.Noise(18f, octaves: 6, kind: NoiseKind.Billow, warp: 0.06f).Remap(0.3f, 0.9f, 0.1f, 0.62f);
            Surface moss = new Surface(Paint.Lerp(Rgb(40, 50, 20), Rgb(64, 76, 30), c.Noise(40f, octaves: 3)), 0.92f, mossHeight) { Subsurface = 0.2f };
            Surface ground = earth.Stack(moss, 0.04f);
            ground = ground.Stack(Straws(c, 30f, Rgb(118, 100, 62), 0.35f), 0.02f);
            return ground.Stack(Straws(c, 24f, Rgb(58, 74, 26), 0.45f) with { Subsurface = 0.35f }, 0.02f);
        }

        // Blades or straws lying every way: long thin domes, the higher of two crossing showing.
        private static Surface Straws(Canvas c, float frequency, Vector3 colour, float lift)
        {
            Stones blades = c.Scatter(frequency, count: 4, radius: 0.32f, radiusJitter: 0.35f, elongation: 9f, heightJitter: 0.4f);
            Field height = blades.Cover * lift + blades.Height * 0.4f;
            Paint shade = (Paint)colour * Field.Lerp(0.7f, 1.2f, blades.Id) * Field.Lerp(0.85f, 1.05f, blades.LocalX.Abs().Invert());
            return new Surface(shade, 0.85f, height);
        }

        // Soil going over to grass: bare earth with tufts of turf and the litter of last year's blades.
        private static Surface GrassySoil(Canvas c)
        {
            Surface soil = Soil(c);
            Field turf = Patches(c, 6f, 0.45f, 0.1f, 0.08f);
            Field turfHeight = c.Noise(16f, octaves: 6, kind: NoiseKind.Billow, warp: 0.05f).Remap(0.3f, 0.9f, 0.3f, 0.85f) * turf;
            Surface green = new Surface(Paint.Lerp(Rgb(42, 52, 22), Rgb(70, 80, 34), c.Noise(36f, octaves: 3)), 0.92f, turfHeight) { Subsurface = 0.2f };
            Surface litter = Straws(c, 28f, Rgb(112, 96, 60), 0.4f);
            return soil.Stack(green, 0.04f).Stack(litter with { Height = litter.Height * turf.Max(0.35f) }, 0.02f);
        }

        // Fine sand rippled by the wind: a gentle slope up to each crest and a steep one down, the crests wandering, the darker grains gathered in the troughs.
        private static Surface Sand(Canvas c)
        {
            Field wander = c.Noise(new Vector2(3f, 2f), octaves: 4, warp: 0.08f) - 0.5f;
            Field phase = (c.V * 15f + wander * 2.2f + (c.Noise(9f, octaves: 3) - 0.5f) * 0.6f).Fract();
            // A wind ripple's long gentle side and short steep one.
            Field ripple = phase.Smooth(0f, 0.78f) * (1f - phase.Smooth(0.78f, 1f));
            Field grains = c.Noise(180f, octaves: 2);
            Field height = ripple * 0.62f + c.Noise(5f, octaves: 4) * 0.28f + grains * 0.08f;
            Paint colour = c.Ramp(ripple, (0f, Rgb(118, 96, 64)), (0.6f, Rgb(150, 125, 86)), (1f, Rgb(170, 146, 104)));
            colour = colour * Field.Lerp(0.9f, 1.07f, grains);
            return new Surface(colour, 0.95f, height) { Subsurface = 0.15f };
        }

        // Dunes seen close: long sharp-crested ridges with ripples running over them, the lee sides shadowed.
        private static Surface Dunes(Canvas c)
        {
            Field ridges = c.Noise(new Vector2(2f, 5f), octaves: 3, kind: NoiseKind.Ridged, warp: 0.05f);
            Field phase = (c.V * 26f + (c.Noise(new Vector2(4f, 3f), octaves: 3) - 0.5f) * 3f).Fract();
            Field ripples = phase.Smooth(0f, 0.75f) * (1f - phase.Smooth(0.75f, 1f));
            Field height = ridges * 0.75f + ripples * 0.18f + c.Noise(60f, octaves: 3) * 0.07f;
            Paint colour = c.Ramp(height, (0.1f, Rgb(126, 100, 66)), (0.55f, Rgb(152, 126, 86)), (0.95f, Rgb(176, 150, 108)));
            return new Surface(colour, 0.94f, height) { Subsurface = 0.15f };
        }

        // Mud dried into plates that curl up at their edges, with deep cracks between and finer ones across the bigger plates.
        private static Surface CrackedEarth(Canvas c)
        {
            Cells plates = c.Cells(new Vector2(6f), jitter: 0.9f);
            Cells fine = c.Cells(new Vector2(17f), jitter: 0.9f);
            Field crack = plates.Border.Smooth(0.008f, 0.045f);
            Field fineCrack = fine.Border.Smooth(0.01f, 0.05f).Max(fine.Id.Threshold(0.55f, 0f).Invert());
            Field curl = (1f - plates.Border.Smooth(0.03f, 0.3f)) * 0.14f;
            Field crust = 0.62f + curl + (c.Noise(40f, octaves: 4) - 0.5f) * 0.08f;
            Field height = crust * crack * Field.Lerp(0.55f, 1f, fineCrack);
            Paint colour = c.Ramp(c.Noise(5f, octaves: 4), (0f, Rgb(150, 120, 88)), (1f, Rgb(182, 152, 114)));
            colour = Paint.Lerp(Rgb(76, 58, 42), colour, crack.Pow(0.6f));
            return new Surface(colour * Field.Lerp(0.92f, 1.05f, curl * 7f), 0.97f, height);
        }

        // Volcanic ground: grey ash drifted over a dark crust of cooled lava, which breaks out of it in plates, cinders lying about, and here and there a crack
        // in the bare crust that still glows. Mostly ash and crust: a glow in every crack reads as a tiled floor. A tile is four metres and a dozen are in view, so
        // where the crust shows is many small patches a tile and not far off the ash in shade: a few big dark ones repeated as wallpaper.
        private static Surface Ash(Canvas c)
        {
            // Where the crust stands out of the ash, and where it's buried.
            Field bare = Patches(c, 8f, 0.4f, 0.15f, 0.12f);

            // The crust: plates with wandering cracks between them, ropy where it flowed, finer cracks across some of them.
            Offsets wobble = c.Offsets(10f, octaves: 3);
            Cells plates = c.Cells(new Vector2(6f), jitter: 0.95f);
            Field crackLine = plates.Border.Warp(wobble, 0.025f);
            Field plate = (crackLine - 0.02f).Profile(0.08f, Rise.Round);
            Cells fine = c.Cells(new Vector2(9f), jitter: 0.9f);
            Field fineCrack = (1f - fine.Border.Warp(wobble, 0.01f).Smooth(0f, 0.04f)) * fine.Id.Threshold(0.55f, 0f);
            Field ropes = c.Noise(new Vector2(12f, 5f), octaves: 4, kind: NoiseKind.Ridged, warp: 0.12f);
            Field crustHeight = plate * (0.62f + 0.18f * plates.Id + ropes * 0.12f) - fineCrack * plate * 0.2f + (c.Noise(60f, octaves: 3) - 0.5f) * 0.04f;
            Paint basalt = Paint.Lerp(Rgb(40, 38, 37), Rgb(62, 58, 55), ropes) * Field.Lerp(0.88f, 1.08f, c.Noise(30f, octaves: 3));
            // A few cracks deep in the bare crust still glow.
            Field embers = c.Noise(6f, octaves: 3).Threshold(0.7f, 0.06f) * bare.Smooth(0.6f, 0.95f);
            Field glow = (1f - crackLine.Smooth(0f, 0.02f)) * embers;
            basalt = Paint.Lerp(basalt, Rgb(255, 96, 28), glow);
            Surface crust = new Surface(basalt, Field.Lerp(0.92f, 0.7f, ropes), crustHeight) { Emission = glow * 0.9f };

            // The ash: soft drifts that fill the cracks and bury the crust where it lies deep, gone below the crust where it's bare.
            Field drifts = c.Noise(new Vector2(6f, 5f), octaves: 5, warp: 0.08f);
            Field ashHeight = Field.Lerp(0.92f, -0.05f, bare) + (drifts - 0.5f) * 0.16f + (c.Noise(120f, octaves: 2) - 0.5f) * 0.03f;
            Paint ashColour = Paint.Lerp(Rgb(70, 68, 66), Rgb(92, 90, 86), drifts) * Field.Lerp(0.94f, 1.04f, c.Noise(90f, octaves: 2));
            Surface ash = new Surface(ashColour, 0.97f, ashHeight);

            // Cinders lie on the ash, wherever it is: below it where there's no cinder, so that only a cinder ever shows.
            Stones cinders = c.Scatter(new Vector2(14f), count: 2, radius: 0.24f, radiusJitter: 0.6f, elongation: 0.8f, heightJitter: 0.6f, flatness: 0.3f);
            Field lift = (cinders.Cover * 0.2f + cinders.Height * 0.14f - c.Noise(150f, octaves: 2, kind: NoiseKind.Billow) * 0.03f) * cinders.Id2.Threshold(0.55f, 0f) - 0.1f;
            Paint cinderColour = Paint.Lerp(Rgb(30, 28, 28), Rgb(72, 44, 34), cinders.Id.Threshold(0.7f, 0.1f)) * Field.Lerp(0.8f, 1.1f, cinders.Height);
            Surface cinder = new Surface(cinderColour, 0.9f, ashHeight + lift);

            return crust.Stack(ash, 0.04f).Stack(cinder, 0.02f);
        }

        // Old snow: soft drifts, wind-cut ridges, blue in the hollows.
        private static Surface Snow(Canvas c)
        {
            Field drifts = c.Noise(new Vector2(3f, 2f), octaves: 5, warp: 0.1f);
            Field sastrugi = c.Noise(new Vector2(3f, 12f), octaves: 4, kind: NoiseKind.Ridged, warp: 0.06f);
            Field crust = c.Noise(50f, octaves: 3);
            Field height = drifts * 0.8f + sastrugi * 0.14f + crust * 0.04f;
            Paint colour = c.Ramp(height, (0.1f, Rgb(176, 192, 214)), (0.5f, Rgb(214, 222, 234)), (0.9f, Rgb(232, 236, 242)));
            return new Surface(colour, Field.Lerp(0.78f, 0.6f, crust), height) { Subsurface = 0.45f };
        }

        // Clear ice with cracks running through it and frost where its surface has gone white.
        private static Surface Ice(Canvas c)
        {
            Cells cracks = c.Cells(new Vector2(4f), jitter: 0.95f);
            Field crack = cracks.Border.Smooth(0.002f, 0.015f).Max(cracks.Id.Threshold(0.6f, 0f));
            Field frost = Patches(c, 8f, 0.18f, 0.2f);
            Field height = 0.7f + (c.Noise(3f, octaves: 4) - 0.5f) * 0.3f - (1f - crack) * 0.2f + frost * 0.15f;
            Paint colour = Paint.Lerp(Rgb(118, 158, 206), Rgb(184, 204, 226), frost);
            colour = colour * Field.Lerp(0.75f, 1f, crack);
            return new Surface(colour, Field.Lerp(0.08f, 0.5f, frost), height) { Subsurface = 0.85f };
        }

        // Churned wet mud: lumps, prints of feet and hooves pressed into it, ruts, and water lying smooth and dark only in what's lowest.
        private static Surface Mud(Canvas c)
        {
            Field lumps = c.Noise(7f, octaves: 6, gain: 0.5f, warp: 0.02f);
            Field ruts = (c.U * 3f + (c.Noise(new Vector2(2f, 3f), octaves: 3) - 0.5f) * 0.35f).Sine(1f, 0.25f).Pow(3f);
            Stones prints = c.Scatter(new Vector2(6f), count: 2, radius: 0.22f, radiusJitter: 0.25f, elongation: 0.65f, heightJitter: 0.2f, flatness: 0.6f);
            Field height = lumps * 0.62f + (1f - ruts) * 0.25f - prints.Height * 0.1f + (c.Noise(60f, octaves: 3) - 0.5f) * 0.05f;
            Field wet = height.Smooth(0.3f, 0.16f);   // Falling: wet where it's low.
            Paint colour = Paint.Lerp(Rgb(62, 48, 34), Rgb(36, 28, 20), wet);
            colour = colour * Field.Lerp(0.9f, 1.08f, c.Noise(30f, octaves: 3)) * Field.Lerp(1f, 0.9f, prints.Cover);
            return new Surface(colour, Field.Lerp(0.72f, 0.12f, wet), height);
        }

        // Cushions of moss crowding each other, round and soft, yellow-green where they catch the light and dark in the gaps, fuzzy close up, a stone showing
        // through. Domes and not cells: moss in cells reads as stained glass.
        private static Surface Moss(Canvas c)
        {
            Stones cushions = c.Scatter(new Vector2(8f), count: 4, radius: 0.5f, radiusJitter: 0.55f, elongation: 0.85f, heightJitter: 0.5f, flatness: 0.1f);
            Field swell = c.Noise(4f, octaves: 4);
            Field lumps = c.Noise(26f, octaves: 4, kind: NoiseKind.Billow);
            Field fuzz = c.Noise(200f, octaves: 2);
            Field height = cushions.Height * 0.5f + cushions.Cover * 0.12f + swell * 0.22f + lumps * 0.1f + (fuzz - 0.5f) * 0.06f;
            Paint colour = c.Ramp(height, (0.15f, Rgb(22, 26, 12)), (0.45f, Rgb(46, 60, 22)), (0.8f, Rgb(84, 98, 34)));
            colour = colour.Jitter(cushions.Id, 0.03f, 0.15f, 0.1f) * Field.Lerp(0.85f, 1.1f, fuzz);
            Surface moss = new Surface(colour, 0.95f, height) { Subsurface = 0.3f };
            return moss.Stack(LooseStones(c, 4f, 0.2f, Rgb(96, 92, 84), count: 1, lift: 0.5f), 0.03f);
        }

        // Angular gravel, packed, with grit between.
        private static Surface Gravel(Canvas c)
        {
            Stones stones = c.Scatter(new Vector2(16f), count: 4, radius: 0.42f, radiusJitter: 0.45f, elongation: 0.8f, heightJitter: 0.35f, flatness: 0.35f);
            Field facets = c.Noise(64f, octaves: 3, kind: NoiseKind.Ridged) - 0.5f;
            Field stoneHeight = stones.Cover * 0.32f + stones.Height * 0.62f + facets * 0.06f;
            Paint stoneColour = c.Ramp(stones.Id, (0f, Rgb(66, 60, 54)), (0.4f, Rgb(96, 88, 76)), (0.75f, Rgb(122, 112, 98)), (1f, Rgb(108, 92, 74)))
                .Jitter(stones.Id, 0.03f, 0.25f, 0.1f);
            Surface stone = new Surface(stoneColour * Field.Lerp(0.85f, 1.1f, stones.Height), 0.78f, stoneHeight);
            Field fines = 0.12f + c.Noise(90f, octaves: 3) * 0.22f;
            Surface grit = new Surface(Paint.Lerp(Rgb(60, 52, 42), Rgb(88, 78, 64), c.Noise(140f, octaves: 2)), 0.92f, fines);
            return grit.Stack(stone, 0.02f);
        }

        // River pebbles, rounded and smooth, of every colour a riverbed gathers, packed close.
        private static Surface Pebbles(Canvas c)
        {
            Stones stones = c.Scatter(new Vector2(9f), count: 3, radius: 0.5f, radiusJitter: 0.4f, elongation: 0.9f, heightJitter: 0.3f, flatness: 0.2f);
            Field stoneHeight = stones.Cover * 0.28f + stones.Height * 0.7f + (c.Noise(70f, octaves: 3) - 0.5f) * 0.02f;
            Paint stoneColour = c.Ramp(stones.Id,
                (0f, Rgb(54, 52, 50)), (0.25f, Rgb(92, 88, 82)), (0.5f, Rgb(132, 120, 102)),
                (0.7f, Rgb(116, 96, 78)), (0.88f, Rgb(150, 146, 138)), (1f, Rgb(176, 170, 160)))
                .Jitter(stones.Id, 0.02f, 0.2f, 0.12f);
            Field veins = c.Noise(new Vector2(26f, 26f), octaves: 4, kind: NoiseKind.Ridged).Threshold(0.86f, 0.03f) * stones.Id2.Threshold(0.7f);
            stoneColour = Paint.Lerp(stoneColour, Rgb(196, 190, 180), veins);
            Surface stone = new Surface(stoneColour, Field.Lerp(0.7f, 0.5f, stones.Height), stoneHeight);
            Surface sand = new Surface(Paint.Lerp(Rgb(84, 74, 58), Rgb(112, 100, 80), c.Noise(120f, octaves: 2)), 0.9f, 0.14f + c.Noise(40f, octaves: 3) * 0.14f);
            return sand.Stack(stone, 0.02f);
        }

        // Ploughed ground: furrows running across, wandering and deeper in some places than others, their ridges broken into clods, stubble pale against the dark.
        private static Surface Farmland(Canvas c)
        {
            Field wander = (c.Noise(new Vector2(4f, 2f), octaves: 3) - 0.5f) * 0.9f;
            Field furrow = (c.V * 9f + wander).Sine(1f, 0.25f);
            Field depth = Field.Lerp(0.5f, 1f, c.Noise(new Vector2(2f, 6f), octaves: 3));
            Field clods = c.Noise(20f, octaves: 5, kind: NoiseKind.Billow, warp: 0.08f);
            Field height = furrow.Pow(0.8f) * 0.55f * depth + clods * 0.35f + (c.Noise(80f, octaves: 3) - 0.5f) * 0.05f;
            Paint colour = c.Ramp(height, (0.1f, Rgb(44, 30, 20)), (0.55f, Rgb(66, 46, 30)), (0.9f, Rgb(96, 72, 48)));
            Surface earth = new Surface(colour, 0.97f, height);
            Surface stubble = Straws(c, 14f, Rgb(150, 130, 86), 0.55f);
            return earth.Stack(stubble with { Height = stubble.Height * Patches(c, 5f, 0.15f, 0.15f) }, 0.02f);
        }

        // What rock is made of at the scale of a hand and a stride: blocks with flat faces, the cracks between them, and a grain over all of it. It's read at two sizes
        // at once and from a long way up (Rock.glsl), so it's the large strong shapes that count. The grey is as bright as rock gets, as the world's layers multiply it.
        private static Surface Cliff(Canvas c)
        {
            Facets faces = c.Facets(new Vector2(3f, 6f), tilt: 0.42f, relief: 0.5f, crack: 0.28f, width: 0.022f);
            Facets smaller = c.Facets(new Vector2(8f, 13f), tilt: 0.5f, relief: 0.55f, crack: 0.3f, width: 0.04f);
            Facets chips = c.Facets(new Vector2(21f, 31f), tilt: 0.6f, relief: 0.5f, crack: 0.24f, width: 0.07f);
            Field grain = c.Noise(29f, octaves: 7, gain: 0.58f);
            Field height = (faces.Height + smaller.Height * 0.42f + chips.Height * 0.3f + grain * 0.16f).Normalize();
            Paint colour = Grey(232) * Field.Lerp(0.82f, 1f, height.Smooth(0.15f, 0.7f));
            return new Surface(colour, 0.86f, height);
        }

        // The darkest and palest faces of rough stone, before whatever's built of it colours it (the waygates do, and their paving with them).
        private static readonly Vector3 RockDark = Rgb(86, 80, 70);
        private static readonly Vector3 RockPale = Rgb(112, 104, 90);

        // Rough broken stone for what's carved or heaped from it.
        private static Surface RockSurface(Canvas c)
        {
            Facets faces = c.Facets(new Vector2(5f, 4f), tilt: 0.5f, relief: 0.4f, crack: 0.35f, width: 0.03f);
            Field grain = c.Noise(40f, octaves: 6, gain: 0.55f);
            Field height = (faces.Height + grain * 0.22f).Normalize();
            Paint colour = c.Ramp(faces.Id, (0f, RockDark), (1f, RockPale)) * Field.Lerp(0.8f, 1.05f, height);
            return new Surface(colour, 0.88f, height);
        }
    }
}
