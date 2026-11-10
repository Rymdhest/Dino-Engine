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
