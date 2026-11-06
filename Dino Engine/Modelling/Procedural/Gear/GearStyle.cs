using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// Which materials gear is made of, by their place in the material arrays. It's here and not read from <see cref="Textures.TextureGenerator"/>, as just touching that class compiles
    /// shaders, and generators have to run where there's no GPU. The game fills these in once its textures exist. Anywhere else they stay as they are, which tells them apart.
    /// </summary>
    public static class GearMaterials
    {
        public static int Metal = 1;
        public static int Mail = 2;
        public static int Leather = 3;
        public static int Cloth = 4;
        public static int Wood = 5;
        public static int Bone = 6;
        public static int Gem = 7;
        public static int Glow = 8;
        public static int Skin = 9;
        public static int Straw = 10;
    }

    /// <summary>
    /// Every colour one item is made of, drawn together so that they belong together. All of them are deep, as everything in the game is: under a sun of about 13 anything pale goes white.
    /// </summary>
    public sealed class GearPalette
    {
        public readonly VertexMaterial Metal;       // What most of the metal on it is.
        public readonly VertexMaterial Fitting;     // What's on top of that: hilts, rivets, trim.
        public readonly VertexMaterial Dark;        // Metal in shadow, or blued, for grooves and insets.
        public readonly VertexMaterial Mail;        // The same metal, in rings.
        public readonly VertexMaterial Edge;        // Metal that's been sharpened.
        public readonly VertexMaterial Leather;
        public readonly VertexMaterial Binding;     // Leather that's a different colour to the rest, for wraps and straps.
        public readonly VertexMaterial Cloth;
        public readonly VertexMaterial Trim;        // Cloth or thread that picks out the main one.
        public readonly VertexMaterial Wood;
        public readonly VertexMaterial Bone;
        public readonly VertexMaterial Stone;       // Set into things.
        public readonly VertexMaterial Glow;        // What's lit from inside.

        public GearPalette(Genes genes, in GearSpec spec, float wear)
        {
            // A metal is a place between cold steel and warm bronze, and then how bright it was kept. Wear takes the shine off and rust is further on from there.
            float warmth = MathF.Pow(genes.Unit("metal.warmth"), 2.4f);
            float bright = genes.Range("metal.bright", 0.42f, 0.78f) * (1f - 0.35f * wear);
            float blackened = genes.Chance("metal.blackened", 0.14f) ? genes.Range("metal.blackened.by", 0.3f, 0.55f) : 1f;
            Vector3 metal = MetalAt(warmth) * (bright * blackened);
            Metal = new VertexMaterial(GearMaterials.Metal, new Colour(metal));
            Dark = new VertexMaterial(GearMaterials.Metal, new Colour(metal * 0.42f));
            Mail = new VertexMaterial(GearMaterials.Mail, new Colour(metal * 0.8f));
            Edge = new VertexMaterial(GearMaterials.Metal, new Colour(Vector3.Lerp(metal, new Vector3(0.84f, 0.86f, 0.9f), 0.65f * (1f - wear))));

            // Fittings are often the other sort of metal to the blade, and finer work gets the better metal.
            float fine = (spec.Grade + genes.Range("fitting.fine", 0f, 1.5f)) / 5.5f;
            float fitWarmth = genes.Chance("fitting.same", 0.3f) ? warmth : genes.Range("fitting.warmth", 0.55f, 1f);
            Vector3 fitting = MetalAt(fitWarmth) * Lerp(0.36f, 0.82f, fine);
            Fitting = new VertexMaterial(GearMaterials.Metal, new Colour(fitting));

            float hide = genes.Unit("leather.tone");
            float dyed = genes.Chance("leather.dyed", 0.34f) ? 1f : 0f;
            Vector3 leather = dyed > 0f
                ? Hsv(genes.Unit("leather.hue"), genes.Range("leather.sat", 0.4f, 0.7f), genes.Range("leather.value", 0.1f, 0.24f))
                : Hsv(Lerp(8f, 40f, hide) / 360f, genes.Range("leather.sat.plain", 0.38f, 0.8f), genes.Range("leather.value.plain", 0.08f, 0.42f));
            Leather = new VertexMaterial(GearMaterials.Leather, new Colour(leather));
            Binding = new VertexMaterial(GearMaterials.Leather, new Colour(genes.Chance("binding.contrast", 0.65f)
                ? Hsv(genes.Unit("binding.hue"), genes.Range("binding.sat", 0.45f, 0.8f), genes.Range("binding.value", 0.12f, 0.3f))
                : leather * genes.Range("binding.shade", 0.45f, 0.8f)));

            float hue = ThemeHue(spec.Theme) is float themed ? themed + genes.Around("cloth.hue.near", 0f, 0.03f) : genes.Unit("cloth.hue");
            hue -= MathF.Floor(hue);
            float saturation = genes.Range("cloth.sat", 0.42f, 0.9f) * (1f - 0.4f * wear);
            float value = genes.Range("cloth.value", 0.13f, 0.4f);
            Cloth = new VertexMaterial(GearMaterials.Cloth, new Colour(Hsv(hue, saturation, value)));
            float away = genes.Pick("trim.harmony", 3f, 2f, 2f) switch
            {
                0 => 0.5f,                                                  // Across the wheel.
                1 => genes.Sign("trim.side") * genes.Range("trim.near", 0.06f, 0.14f),   // Beside it.
                _ => genes.Sign("trim.side") * 0.33f,
            };
            float trimHue = hue + away;
            Trim = new VertexMaterial(GearMaterials.Cloth, new Colour(Hsv(trimHue - MathF.Floor(trimHue), MathF.Min(saturation + 0.15f, 0.95f), MathF.Min(value * genes.Range("trim.bright", 1.2f, 2f), 0.62f))));

            Wood = new VertexMaterial(GearMaterials.Wood, new Colour(Hsv(genes.Range("wood.hue", 18f, 34f) / 360f, genes.Range("wood.sat", 0.42f, 0.7f), genes.Range("wood.value", 0.16f, 0.42f))));
            Bone = new VertexMaterial(GearMaterials.Bone, new Colour(Hsv(genes.Range("bone.hue", 30f, 48f) / 360f, genes.Range("bone.sat", 0.12f, 0.34f), genes.Range("bone.value", 0.5f, 0.72f))));

            float stoneHue = ThemeHue(spec.Theme) ?? genes.Unit("stone.hue");
            Vector3 stone = Hsv(stoneHue, genes.Range("stone.sat", 0.7f, 1f), genes.Range("stone.value", 0.4f, 0.8f));
            Stone = new VertexMaterial(GearMaterials.Gem, new Colour(stone));

            // Colour above 1 on something emissive is how hard it glows, and its hue stays in the albedo. Kept saturated, or tone mapping bleaches it.
            Glow = new VertexMaterial(GearMaterials.Glow, new Colour(stone.X, stone.Y, stone.Z, 1.4f + spec.Grade * 0.5f));
        }

        // Mixed as colours and not as hues: halfway round the wheel from steel to bronze is green, and halfway between them as colours is a neutral grey.
        private static Vector3 MetalAt(float warmth)
        {
            Vector3 steel = Hsv(214f / 360f, 0.08f, 1f);
            Vector3 bronze = Hsv(34f / 360f, 0.62f, 1f);
            return Vector3.Lerp(steel, bronze, warmth);
        }

        private static float? ThemeHue(GearTheme theme) => theme switch
        {
            GearTheme.Fire => 0.045f,
            GearTheme.Cold => 0.56f,
            GearTheme.Lightning => 0.15f,
            GearTheme.Magic => 0.78f,
            GearTheme.Poison => 0.32f,
            _ => null,
        };

        public static Vector3 Hsv(float hue, float saturation, float value)
        {
            float h = (hue - MathF.Floor(hue)) * 6f;
            float c = value * saturation;
            float x = c * (1f - MathF.Abs(h % 2f - 1f));
            float m = value - c;
            Vector3 rgb = (int)h switch
            {
                0 => new Vector3(c, x, 0f),
                1 => new Vector3(x, c, 0f),
                2 => new Vector3(0f, c, x),
                3 => new Vector3(0f, x, c),
                4 => new Vector3(x, 0f, c),
                _ => new Vector3(c, 0f, x),
            };
            return rgb + new Vector3(m);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    /// <summary>
    /// What one item is like all over, drawn once and read by all of its parts. It's what makes a thing look like a thing and not a pile of unrelated random parts: a heavy, angular, spiked
    /// sword has a heavy, angular, spiked guard as well as a blade to match. Each of these is 0 to 1.
    /// </summary>
    public sealed class GearStyle
    {
        public readonly GearSpec Spec;
        public readonly Genes Genes;
        public readonly GearPalette Palette;

        public readonly float Heft;         // Thick and massive, against light and fine.
        public readonly float Angular;      // Straight lines and corners, against flowing curves.
        public readonly float Ornate;       // How much is on it.
        public readonly float Wear;         // What's happened to it.
        public readonly float Flare;        // How much its ends spread, against drawing in.
        public readonly float Spikes;       // How much of it comes to a point.

        public GearStyle(in GearSpec spec)
        {
            Spec = spec;
            Genes = new Genes(spec.Seed).Part(spec.Kind.ToString());
            Genes style = Genes.Part("style");

            Heft = Clamp01(style.Around("heft", 0.5f + (0.5f - spec.Form.Slender) * 0.7f, 0.17f));
            Angular = Clamp01(style.Around("angular", 0.5f, 0.24f));

            // A plain thing can still be well made, and a great thing is never plain.
            float grade = spec.Grade / 4f;
            Ornate = Clamp01(style.Around("ornate", 0.1f + spec.Form.Fancy * 0.45f + grade * 0.5f, 0.1f));
            Wear = Clamp01(style.Around("wear", spec.Form.Worn * (1f - grade * 0.6f), 0.08f));
            Flare = Clamp01(style.Around("flare", 0.45f, 0.24f));
            Spikes = Clamp01(style.Around("spikes", 0.25f + Ornate * 0.2f, 0.2f));

            Palette = new GearPalette(Genes.Part("palette"), spec, Wear);
        }

        /// <summary>Whether it has a stone or anything lit: the better a thing, the likelier.</summary>
        public bool Shines(string name) => Genes.Chance(name, 0.08f + Spec.Grade * 0.2f + (Spec.Theme != GearTheme.None ? 0.2f : 0f));

        private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
    }
}
