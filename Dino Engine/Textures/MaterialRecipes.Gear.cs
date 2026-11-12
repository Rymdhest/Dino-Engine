using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Textures.Synthesis;
using OpenTK.Mathematics;
using static Dino_Engine.Textures.Synthesis.Colours;

namespace Dino_Engine.Textures
{
    // What's worn and carried. What colour it is goes in its vertices (GEAR.md): metal is white, and leather, cloth and bone each average exactly the colour that
    // GearMaterials.AlbedoOf says, as a colour meant as an albedo is divided by it. So their colour only moves round that average, by noise whose own mean is a half,
    // and everything else they are goes into their height, where it's light and shadow.
    public static partial class MaterialRecipes
    {
        // Steel beaten out by hand: shallow hammer marks, polished along its length, scratched.
        private static Surface Plate(Canvas c)
        {
            Cells hammer = c.Cells(new Vector2(16f), jitter: 0.9f);
            Field dents = hammer.Centre.Profile(0.62f, Rise.Smooth);
            Field polish = c.Noise(new Vector2(4f, 40f), octaves: 4);
            Field scratches = c.Noise(new Vector2(3f, 160f), octaves: 2, kind: NoiseKind.Ridged).Threshold(0.93f, 0.02f);
            Field height = 0.7f + dents * 0.22f + (polish - 0.5f) * 0.04f - scratches * 0.05f;
            Paint colour = Grey(255) * Field.Lerp(0.9f, 1f, polish);
            return new Surface(colour, Field.Lerp(0.42f, 0.28f, polish) + scratches * 0.15f, height) { Metallic = 0.9f };
        }

        // Riveted rings, each through four others, the gaps between them deep.
        private static Surface Mail(Canvas c)
        {
            Links links = c.Mail(new Vector2i(16, 26), ring: 0.66f, wire: 0.17f, lean: 0.6f);
            Field height = links.Height * 0.9f + links.Cover * 0.1f;
            Paint colour = Grey(255) * Field.Lerp(0.55f, 1f, links.Cover) * Field.Lerp(0.9f, 1f, links.Id);
            return new Surface(colour, Field.Lerp(0.6f, 0.36f, links.Cover), height) { Metallic = Field.Lerp(0.5f, 0.9f, links.Cover) };
        }

        // Tanned hide: a pebbled grain, creases where it's flexed, smoother where it's rubbed.
        private static Surface Leather(Canvas c)
        {
            Cells pebbles = c.Cells(new Vector2(46f), jitter: 0.9f);
            Field grain = pebbles.Border.Profile(0.25f, Rise.Round);
            Field creases = c.Noise(new Vector2(10f, 14f), octaves: 4, kind: NoiseKind.Ridged, warp: 0.08f);
            Field rubbed = c.Noise(4f, octaves: 4);
            Field height = 0.55f + grain * 0.28f * Field.Lerp(1f, 0.45f, rubbed) - creases.Threshold(0.78f, 0.08f) * 0.3f;
            Paint colour = (Paint)GearMaterials.LeatherAlbedo * Field.Lerp(0.82f, 1.18f, rubbed) * Field.Lerp(0.94f, 1.06f, c.Noise(30f, octaves: 3));
            return new Surface(colour, Field.Lerp(0.72f, 0.5f, rubbed), height);
        }

        // Plain-woven homespun, the threads uneven.
        private static Surface Cloth(Canvas c)
        {
            Weave weave = c.Weave(new Vector2i(40, 40), thickness: 0.86f, dip: 0.45f);
            Field slubs = c.Noise(new Vector2(40f, 40f), octaves: 3);
            Field fuzz = c.Noise(260f, octaves: 2);
            Field height = weave.Height * 0.85f + (slubs - 0.5f) * 0.1f + (fuzz - 0.5f) * 0.05f;
            Paint colour = (Paint)GearMaterials.ClothAlbedo * Field.Lerp(0.9f, 1.1f, slubs) * Field.Lerp(0.95f, 1.05f, fuzz);
            return new Surface(colour, 0.96f, height) { Subsurface = 0.3f };
        }

        // Wood cut along its grain, for a haft, a bow or a staff: long fibres and no board in them, the growth rings showing as a flame where the cut crosses them, and fine pores. The grain
        // runs up the tile, along whatever's lofted. Its late wood is a dark band a quarter as strong on average, so the shade it takes averages out to nothing.
        private static Surface Timber(Canvas c)
        {
            Field figure = c.Noise(new Vector2(5f, 1f), octaves: 4, warp: 0.05f);
            Field ring = (figure * 11f).Fract();
            Field late = ((ring - 0.5f).Abs() * 2f).Pow(3f);
            Field fibres = c.Noise(new Vector2(140f, 5f), octaves: 3);
            Field streaks = c.Noise(new Vector2(24f, 2f), octaves: 3);
            Field pores = c.Noise(new Vector2(300f, 18f), octaves: 2, kind: NoiseKind.Ridged).Threshold(0.82f, 0.06f);
            Field height = 0.78f + (fibres - 0.5f) * 0.16f - late * 0.1f - pores * 0.08f;
            Paint colour = (Paint)GearMaterials.TimberAlbedo * Field.Lerp(1.06f, 0.82f, late) * Field.Lerp(0.9f, 1.1f, streaks) * Field.Lerp(0.95f, 1.05f, fibres);
            return new Surface(colour, Field.Lerp(0.62f, 0.78f, late), height);
        }

        // Metal scales in rows, each row lapping the one below as scale armour is laced: rounded at the foot, a ridge down the middle, thickest where it laps, and the corners between
        // the feet of two scales in the shadow of the row they're laced over.
        private static Surface Scales(Canvas c)
        {
            Bricks rows = c.Bricks(rows: 10, perRow: 8, gap: 0.0015f, bond: 0.5f, rowJitter: 0.04f, lengthJitter: 0.02f);
            Field across = (rows.Along - 0.5f) * 2f;
            Field foot = 0.5f - (1f - across * across).Clamp(0f, 1f).Sqrt() * 0.5f;
            Field rim = (rows.Across - foot).Smooth(0f, 0.12f);
            Field lap = (1f - rows.Across).Pow(0.6f);
            Field keel = (1f - across.Abs()).Pow(2f) * 0.12f;
            Field height = rim * (0.35f + lap * 0.45f + keel) + (c.Noise(70f, octaves: 3) - 0.5f) * 0.03f;
            Paint colour = Grey(255) * Field.Lerp(0.88f, 1f, rows.Id) * Field.Lerp(0.62f, 1f, rim);
            return new Surface(colour, Field.Lerp(0.55f, 0.34f, rim), height) { Metallic = Field.Lerp(0.6f, 0.92f, rim) };
        }

        // Cloth quilted over padding in channels down its length, as a gambeson is: each channel puffed up between its seams and a little lumpy where the stuffing settled, with the weave
        // over all of it. The seams are dark by their shadow alone, so the colour averages what cloth's does, and a dye means the same on both.
        private static Surface Quilt(Canvas c)
        {
            const int Channels = 10;
            Field wander = (c.Noise(new Vector2(4f, 2f), octaves: 3) - 0.5f) * 0.06f;
            Field across = (c.U * Channels + wander).Fract();
            Field puff = 1f - ((across - 0.5f).Abs() * 2f).Pow(2.2f);
            Field lumps = c.Noise(new Vector2(Channels, 6f), octaves: 3);
            Weave weave = c.Weave(new Vector2i(80, 80), thickness: 0.86f, dip: 0.4f);
            Field height = puff.Clamp(0f, 1f).Sqrt() * (0.72f + (lumps - 0.5f) * 0.2f) + weave.Height * 0.08f;
            Paint colour = (Paint)GearMaterials.ClothAlbedo * Field.Lerp(0.9f, 1.1f, c.Noise(40f, octaves: 3)) * Field.Lerp(0.96f, 1.04f, lumps);
            return new Surface(colour, 0.95f, height) { Subsurface = 0.3f };
        }

        // Fur lying down its length: strands gathered into locks that clump and part, deeper at the partings. Pale, as its colour is in the vertices.
        private static Surface Fur(Canvas c)
        {
            Field locks = c.Noise(new Vector2(14f, 4f), octaves: 4, kind: NoiseKind.Billow, warp: 0.08f);
            Field strands = c.Noise(new Vector2(160f, 9f), octaves: 3, kind: NoiseKind.Ridged, warp: 0.03f);
            Field partings = c.Noise(new Vector2(30f, 6f), octaves: 3, kind: NoiseKind.Ridged).Threshold(0.84f, 0.06f);
            Field height = (locks * 0.55f + strands * 0.35f - partings * 0.25f + 0.1f).Clamp(0f, 1f);
            Paint colour = (Paint)GearMaterials.FurAlbedo * Field.Lerp(0.8f, 1.2f, strands) * Field.Lerp(0.88f, 1.12f, locks);
            return new Surface(colour, 0.97f, height) { Subsurface = 0.2f };
        }

        // Old bone: fine pores, the lines it grew in, yellowed in patches.
        private static Surface Bone(Canvas c)
        {
            Cells pores = c.Cells(new Vector2(60f), jitter: 0.95f);
            Field pore = (1f - pores.Centre.Smooth(0.05f, 0.18f)) * pores.Id.Threshold(0.75f, 0f);
            Field growth = c.Noise(new Vector2(3f, 30f), octaves: 4);
            Field height = 0.8f + (growth - 0.5f) * 0.15f - pore * 0.3f;
            Paint colour = (Paint)GearMaterials.BoneAlbedo * Field.Lerp(0.88f, 1.12f, c.Noise(5f, octaves: 4)) * Field.Lerp(0.96f, 1.04f, growth);
            return new Surface(colour, 0.9f, height) { Subsurface = 0.15f };
        }
    }
}
