using Dino_Engine.Textures.Synthesis;
using OpenTK.Mathematics;
using static Dino_Engine.Textures.Synthesis.Colours;

namespace Dino_Engine.Textures
{
    // What roads are made of. Both are read in the road's own frame (World/RoadFrames.cs, Terrain.frag): u across it, v along it, so what wheels and feet wear
    // runs along the road round every bend, and a street's courses lie square across it.
    public static partial class MaterialRecipes
    {
        // Columns of the courses a street's edge is laid in, across the tile: three to a kerb, two to a gutter, so that both meet the field of setts on a joint.
        private const int StreetCourses = 40;
        private const float StreetKerb = 3f / StreetCourses;
        private const float StreetGutter = 2f / StreetCourses;

        // A village street, Crimson Desert's: granite setts in courses square across it, half stones where a course runs into the gutter, the wheel tracks worn
        // smooth; a gutter of smaller setts laid along it each side; and long dressed kerbs at its edges, standing proudest of all. The tile is one street wide
        // (u from kerb to kerb, MaterialCatalog.Setts), so nothing of it repeats across the street.
        private static Surface Setts(Canvas c)
        {
            Field u = c.U;
            // From the street's nearer edge, 0 at the back of a kerb to a half at the crown: both sides are built alike.
            Field inward = u.Min(1f - u);
            Offsets wobble = c.Offsets(14f, octaves: 3);

            // The joints: sand and grit packed between the stones, dark with what's washed into them, moss where they stay damp.
            Field jointHeight = 0.46f + (c.Noise(140f, octaves: 2) - 0.5f) * 0.08f;
            Paint jointColour = Paint.Lerp(Rgb(50, 43, 34), Rgb(74, 64, 50), c.Noise(60f, octaves: 3));
            jointColour = Paint.Lerp(jointColour, Rgb(44, 54, 24), Patches(c, 7f, 0.22f, 0.1f) * 0.8f);
            Surface street = new Surface(jointColour, 0.96f, jointHeight);

            // The setts: courses across the street, a sett half a course long, each its own grey of the same granite, domed a little and worn smooth on top.
            Bricks setts = c.Bricks(rows: 18, perRow: 16, gap: 0.0055f, bond: 0.5f, rowJitter: 0.22f, lengthJitter: 0.3f);
            Field settEdge = setts.Edge.Warp(wobble, 0.0025f).Min(inward - (StreetKerb + StreetGutter));
            Field settId = setts.Id.Warp(wobble, 0.0025f);
            Field dome = 1f - ((setts.Along - 0.5f) * (setts.Along - 0.5f) + (setts.Across - 0.5f) * (setts.Across - 0.5f)) * 1.6f;
            Field chisel = c.Noise(70f, octaves: 4) - 0.5f;
            Field settHeight = settEdge.Profile(0.007f, Rise.Round) * (0.78f + 0.08f * dome + 0.06f * settId.Random(1) + chisel * 0.04f);
            // The wheels run in two tracks, and polish what they run over.
            Field tracks = (1f - ((u - 0.5f).Abs() - 0.2f).Abs().Smooth(0.01f, 0.07f)) * c.Noise(new Vector2(3f, 9f), octaves: 3).Smooth(0.3f, 0.7f);
            Field speckle = c.Noise(240f, octaves: 2);
            Paint granite = c.Ramp(settId, (0f, Rgb(76, 74, 70)), (0.35f, Rgb(96, 92, 86)), (0.68f, Rgb(110, 104, 94)), (0.84f, Rgb(104, 92, 80)), (1f, Rgb(120, 116, 108)))
                .Jitter(settId, 0.015f, 0.18f, 0.1f) * Field.Lerp(0.88f, 1.08f, speckle) * Field.Lerp(0.92f, 1.04f, chisel + 0.5f);
            Field worn = (dome.Smooth(0.5f, 1f) * 0.6f + tracks * 0.5f).Clamp(0f, 1f);
            granite = granite * Field.Lerp(0.72f, 1f, settEdge.Smooth(0f, 0.012f)) * Field.Lerp(1f, 1.12f, worn);
            street = street.Stack(new Surface(granite, Field.Lerp(0.86f, 0.62f, worn), settHeight), 0.02f);

            // The gutters: two courses each of smaller setts laid along the street, set lower so that water runs down them.
            Bricks courses = c.Bricks(rows: StreetCourses, perRow: 15, gap: 0.0045f, bond: 0.5f, rowJitter: 0.4f, lengthJitter: 0.35f, upright: true);
            Field inGutter = inward.Smooth(StreetKerb - 0.002f, StreetKerb + 0.002f) * (1f - inward.Smooth(StreetKerb + StreetGutter - 0.002f, StreetKerb + StreetGutter + 0.002f));
            Field gutterEdge = courses.Edge.Warp(wobble, 0.002f);
            Field gutterHeight = gutterEdge.Profile(0.006f, Rise.Round) * (0.64f + 0.05f * courses.Id.Random(2) + chisel * 0.03f) * inGutter;
            Paint gutterStone = c.Ramp(courses.Id, (0f, Rgb(70, 68, 64)), (0.6f, Rgb(88, 84, 78)), (1f, Rgb(96, 88, 78))).Jitter(courses.Id, 0.01f, 0.15f, 0.08f)
                * Field.Lerp(0.85f, 1.05f, speckle) * Field.Lerp(0.7f, 1f, gutterEdge.Smooth(0f, 0.01f));
            street = street.Stack(new Surface(gutterStone, 0.8f, gutterHeight), 0.02f);

            // The kerbs: long dressed stones, each a course of three wide, their tops flat and their arrises round, standing proudest of all.
            Bricks kerbs = c.Bricks(rows: 1, perRow: 5, gap: 0.004f, bond: 0f, rowJitter: 1f, lengthJitter: 0.4f, upright: true);
            Field inKerb = 1f - inward.Smooth(StreetKerb - 0.002f, StreetKerb + 0.002f);
            Field kerbEdge = kerbs.Edge.Min(StreetKerb - inward);
            Field dressed = c.Noise(new Vector2(30f, 90f), octaves: 3, kind: NoiseKind.Ridged) - 0.5f;
            Field kerbHeight = kerbEdge.Profile(0.008f, Rise.Round) * (0.96f + 0.03f * kerbs.Id.Random(3) + dressed * 0.015f) * inKerb;
            Paint kerbStone = c.Ramp(kerbs.Id, (0f, Rgb(104, 100, 94)), (1f, Rgb(126, 120, 110))) * Field.Lerp(0.9f, 1.06f, speckle)
                * Field.Lerp(0.75f, 1f, kerbEdge.Smooth(0f, 0.01f));
            street = street.Stack(new Surface(kerbStone, 0.78f, kerbHeight), 0.015f);
            return street;
        }

        // A dirt road as carts and feet keep it, Kingdom Come's: earth packed hard and dusted pale on what stands up, stones of every size trodden into it and
        // standing proud of it, grit, and fine grooves worn along it. The ruts, the crown between them and the frayed verge are the terrain's, laid by where on
        // the road a point is (Terrain.frag's roadShape), so they follow the road whatever this does. Trodden ground in a village is the same earth.
        private static Surface RoadDirt(Canvas c)
        {
            Field packed = c.Noise(new Vector2(4f, 3f), octaves: 6, gain: 0.5f, warp: 0.05f);
            // Grooves along the road, left by wheels and feet: v runs along it.
            Field grooves = c.Noise(new Vector2(36f, 2f), octaves: 3, kind: NoiseKind.Ridged, warp: 0.01f);
            Field crumbs = c.Noise(120f, octaves: 3);
            Field earthHeight = 0.52f + (packed - 0.5f) * 0.14f - grooves.Threshold(0.72f, 0.12f) * 0.05f + (crumbs - 0.5f) * 0.05f;
            Field dust = Patches(c, 6f, 0.45f, 0.25f);
            // Darker and damper in broad patches, pale with dust on what stands up: at the game's distance that, and the stones, is all that reads.
            Field damp = Patches(c, 4f, 0.35f, 0.25f, 0.1f);
            Paint earth = Paint.Lerp(Rgb(84, 66, 46), Rgb(112, 90, 64), packed) * Field.Lerp(0.92f, 1.06f, crumbs) * Field.Lerp(1f, 0.8f, damp);
            earth = Paint.Lerp(earth, Rgb(132, 114, 88), dust * earthHeight.Smooth(0.45f, 0.6f) * 0.4f);
            earth = earth * Field.Lerp(1f, 0.9f, grooves.Threshold(0.72f, 0.12f));
            Surface ground = new Surface(earth, Field.Lerp(0.88f, 0.96f, dust), earthHeight);

            // Stones trodden in, the earth showing between them: grit, small ones, some a fist across, a few large; pale limestone and grey, each sunk as far as
            // it's been driven, so only its crown stands out of the earth.
            ground = ground.Stack(TroddenStones(c, 26f, 0.28f, Rgb(132, 126, 116), count: 2, share: 0.3f), 0.012f);
            ground = ground.Stack(TroddenStones(c, 12f, 0.38f, Rgb(122, 118, 110), count: 1, share: 0.5f), 0.02f);
            ground = ground.Stack(TroddenStones(c, 4f, 0.24f, Rgb(126, 120, 110), count: 1, share: 0.4f), 0.02f);
            return ground;
        }

        // Stones driven into packed earth, only <paramref name="share"/> of the places they might be holding one: as high as the earth at their rims, so they're
        // sunk in it and not lying on it, and standing a few centimetres proud at their crowns.
        private static Surface TroddenStones(Canvas c, float frequency, float radius, Vector3 colour, int count, float share)
        {
            Stones stones = c.Scatter(frequency, count: count, radius: radius, radiusJitter: 0.55f, elongation: 0.7f, heightJitter: 0.6f, flatness: 0.25f);
            Field shown = stones.Id2.Threshold(1f - share, 0f);
            Field height = (stones.Cover * 0.3f + stones.Height * 0.36f + (c.Noise(80f, octaves: 3) - 0.5f) * 0.04f) * shown;
            Paint shade = (Paint)colour * Field.Lerp(0.65f, 1.15f, stones.Id) * Field.Lerp(0.82f, 1.05f, stones.Height);
            return new Surface(shade.Jitter(stones.Id, 0.03f, 0.3f, 0.1f), 0.7f, height);
        }
    }
}
