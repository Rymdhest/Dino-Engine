using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// What gear can be coloured with: what a smith, a tanner, a dyer and a carver could really have worked, each as the albedo it comes to under the game's sun, with how dear it is. A look draws
    /// from these and never from the whole wheel, which is what makes a piece look made of something rather than painted (GEAR.md). Everything is deep, as anything pale goes white under a sun of
    /// about 13. Nothing here is the Reich's: black iron, oxblood and brass are its colours and nothing else wears them (LORE.md), which <see cref="IsReichs"/> says and GearLab holds.
    /// </summary>
    public static class GearSwatches
    {
        /// <summary>One colour something can be: what it's called, its albedo, and how dear it is, from what anyone has to what only the finest work gets.</summary>
        public readonly record struct Swatch(string Name, Vector3 Albedo, float Cost);

        private static Swatch S(string name, float r, float g, float b, float cost) => new Swatch(name, new Vector3(r, g, b), cost);

        // Worked metal, as it looks once it's made. The darkest is worn iron, well clear of black, and nothing yellow is dull enough to be brass: gold is bright, bronze is red.
        public static readonly Swatch[] Metals =
        {
            S("iron", 0.34f, 0.34f, 0.35f, 0f),
            S("steel", 0.52f, 0.53f, 0.56f, 0.15f),
            S("bright steel", 0.68f, 0.69f, 0.72f, 0.35f),
            S("bronze", 0.58f, 0.38f, 0.21f, 0.25f),
            S("copper", 0.62f, 0.35f, 0.24f, 0.3f),
            S("blued steel", 0.30f, 0.36f, 0.52f, 0.6f),
            S("silver", 0.85f, 0.85f, 0.86f, 0.7f),
            S("rose gold", 0.88f, 0.58f, 0.46f, 0.85f),
            S("gold", 0.96f, 0.74f, 0.34f, 0.9f),
        };

        // Hide, tanned and sometimes dyed. Never red: an oxblood strap is the Reich's.
        public static readonly Swatch[] Leathers =
        {
            S("tan", 0.40f, 0.25f, 0.13f, 0f),
            S("brown", 0.24f, 0.14f, 0.075f, 0f),
            S("rawhide", 0.48f, 0.38f, 0.26f, 0.1f),
            S("dark brown", 0.12f, 0.075f, 0.045f, 0.15f),
            S("russet", 0.34f, 0.17f, 0.075f, 0.2f),
            S("forest dyed", 0.07f, 0.12f, 0.07f, 0.45f),
            S("blue dyed", 0.06f, 0.08f, 0.15f, 0.5f),
            // More of what tanning gives, from buckskin's pale to leather smoked nearly to bark: on the end, as a crafted look names its hide by its place here.
            S("buckskin", 0.52f, 0.4f, 0.26f, 0.05f),
            S("chestnut", 0.3f, 0.15f, 0.07f, 0.1f),
            S("tobacco", 0.22f, 0.15f, 0.09f, 0.1f),
            S("smoked", 0.15f, 0.12f, 0.1f, 0.15f),
        };

        // What dyers had: woad, madder and weld, walnut, and from far away kermes, indigo and lichen purple, which only the finest work gets. A red is a madder scarlet or a
        // kermes crimson, both bright: a deep red reads as oxblood. Nothing is black, which is the greycoats' charcoal.
        public static readonly Swatch[] Dyes =
        {
            S("undyed", 0.50f, 0.46f, 0.38f, 0f),
            S("oatmeal", 0.42f, 0.37f, 0.28f, 0f),
            S("grey wool", 0.27f, 0.26f, 0.25f, 0.05f),
            S("walnut", 0.30f, 0.17f, 0.09f, 0.05f),
            S("russet", 0.40f, 0.17f, 0.08f, 0.15f),
            S("woad", 0.17f, 0.26f, 0.40f, 0.15f),
            S("weld", 0.58f, 0.45f, 0.10f, 0.2f),
            S("ochre", 0.46f, 0.31f, 0.09f, 0.15f),
            S("olive", 0.24f, 0.25f, 0.10f, 0.15f),
            S("deep woad", 0.05f, 0.11f, 0.27f, 0.45f),
            S("madder", 0.60f, 0.13f, 0.06f, 0.5f),
            S("rose madder", 0.58f, 0.26f, 0.27f, 0.45f),
            S("lincoln green", 0.10f, 0.23f, 0.09f, 0.45f),
            S("teal", 0.05f, 0.21f, 0.21f, 0.55f),
            S("indigo", 0.04f, 0.06f, 0.20f, 0.8f),
            S("kermes", 0.66f, 0.05f, 0.08f, 0.9f),
            S("purple", 0.24f, 0.07f, 0.34f, 0.9f),
            S("violet", 0.33f, 0.14f, 0.48f, 0.85f),
            S("emerald", 0.03f, 0.30f, 0.14f, 0.85f),
        };

        // What hafts, bows and staves are cut from, and a lacquer the finest are finished in.
        public static readonly Swatch[] Woods =
        {
            S("ash", 0.50f, 0.38f, 0.25f, 0f),
            S("oak", 0.36f, 0.25f, 0.15f, 0f),
            S("elm", 0.30f, 0.19f, 0.11f, 0.1f),
            S("blackthorn", 0.16f, 0.10f, 0.08f, 0.3f),
            S("yew", 0.46f, 0.24f, 0.11f, 0.3f),
            S("walnut", 0.19f, 0.11f, 0.065f, 0.4f),
            S("bog oak", 0.12f, 0.10f, 0.085f, 0.5f),
            S("vermilion lacquer", 0.62f, 0.12f, 0.05f, 0.85f),
        };

        public static readonly Swatch[] Furs =
        {
            S("wolf", 0.34f, 0.32f, 0.29f, 0f),
            S("bear", 0.21f, 0.14f, 0.09f, 0f),
            S("fox", 0.46f, 0.21f, 0.08f, 0.3f),
            S("hare", 0.46f, 0.42f, 0.36f, 0.2f),
            S("sable", 0.12f, 0.08f, 0.06f, 0.7f),
            S("ermine", 0.55f, 0.53f, 0.50f, 0.9f),
        };

        public static readonly Swatch[] Gems =
        {
            S("ruby", 0.72f, 0.04f, 0.08f, 0.7f),
            S("sapphire", 0.06f, 0.15f, 0.65f, 0.7f),
            S("emerald", 0.04f, 0.48f, 0.18f, 0.7f),
            S("amethyst", 0.40f, 0.11f, 0.60f, 0.5f),
            S("topaz", 0.86f, 0.56f, 0.10f, 0.5f),
            S("moonstone", 0.62f, 0.68f, 0.80f, 0.6f),
            S("jade", 0.24f, 0.52f, 0.34f, 0.4f),
            S("turquoise", 0.10f, 0.52f, 0.55f, 0.4f),
        };

        /// <summary>The light a power gives off, kept saturated so that tone mapping doesn't bleach it. The poison is a living green, never the Ossuary's yellow-green rot or the cold cyan of its bones.</summary>
        public static Vector3 GlowOf(GearTheme theme) => theme switch
        {
            GearTheme.Fire => new Vector3(1f, 0.42f, 0.08f),
            GearTheme.Cold => new Vector3(0.38f, 0.78f, 1f),
            GearTheme.Lightning => new Vector3(0.95f, 0.9f, 0.45f),
            GearTheme.Magic => new Vector3(0.7f, 0.3f, 1f),
            GearTheme.Poison => new Vector3(0.35f, 1f, 0.25f),
            _ => new Vector3(0.95f, 0.8f, 0.55f),
        };

        /// <summary>Which stones belong to a power, as a fire blade is set with something red or golden.</summary>
        public static string[] GemsOf(GearTheme theme) => theme switch
        {
            GearTheme.Fire => new[] { "ruby", "topaz" },
            GearTheme.Cold => new[] { "sapphire", "moonstone" },
            GearTheme.Lightning => new[] { "topaz", "moonstone" },
            GearTheme.Magic => new[] { "amethyst", "sapphire" },
            GearTheme.Poison => new[] { "emerald", "jade" },
            _ => Array.Empty<string>(),
        };

        /// <summary>Which dyes belong to a power, so that a robe of the frost is a blue one more often than not.</summary>
        public static string[] DyesOf(GearTheme theme) => theme switch
        {
            GearTheme.Fire => new[] { "madder", "kermes", "ochre", "russet" },
            GearTheme.Cold => new[] { "woad", "deep woad", "indigo", "teal" },
            GearTheme.Lightning => new[] { "weld", "ochre", "violet", "deep woad" },
            GearTheme.Magic => new[] { "purple", "violet", "indigo" },
            GearTheme.Poison => new[] { "lincoln green", "emerald", "olive", "teal" },
            _ => Array.Empty<string>(),
        };

        public static Swatch Find(Swatch[] list, string name) => Array.Find(list, s => s.Name == name);

        /// <summary>
        /// Whether a colour is one of the Reich's: black iron, an oxblood red, a brass, or the greycoats' charcoal. A metal darker than worn iron is black iron, and one that's yellow but dull is brass;
        /// anything else that's a deep, strong red is oxblood, and a dark grey with no colour is charcoal.
        /// </summary>
        public static bool IsReichs(Vector3 albedo, bool metal)
        {
            (float hue, float saturation, float value) = Hsv(albedo);
            if (metal) return value < 0.25f || hue is >= 30f and <= 50f && saturation > 0.35f && value < 0.72f;
            bool red = hue >= 345f || hue <= 15f;
            return red && saturation > 0.55f && value < 0.42f || value < 0.1f && saturation < 0.25f;
        }

        /// <summary>Hue in degrees, saturation and value, of a linear colour.</summary>
        public static (float Hue, float Saturation, float Value) Hsv(Vector3 c)
        {
            float max = MathF.Max(c.X, MathF.Max(c.Y, c.Z)), min = MathF.Min(c.X, MathF.Min(c.Y, c.Z));
            float delta = max - min;
            float hue = 0f;
            if (delta > 1e-5f)
            {
                if (max == c.X) hue = 60f * ((c.Y - c.Z) / delta % 6f);
                else if (max == c.Y) hue = 60f * ((c.Z - c.X) / delta + 2f);
                else hue = 60f * ((c.X - c.Y) / delta + 4f);
            }
            if (hue < 0f) hue += 360f;
            return (hue, max > 1e-5f ? delta / max : 0f, max);
        }
    }

    /// <summary>
    /// What one choice about a look was, which crafting will one day let a person make for themselves: a place in one of the lists above, or along a scale, or nothing to leave it to the seed. Nothing
    /// sets any of these yet, and every one that's left at nothing is drawn as it always was, so a piece keeps its look until someone chooses otherwise. See GEAR.md.
    /// </summary>
    /// <param name="Metal">One more than a place in <see cref="GearSwatches.Metals"/>, for the main metal.</param>
    /// <param name="Fitting">The same, for the metal of its fittings.</param>
    /// <param name="Leather">One more than a place in <see cref="GearSwatches.Leathers"/>.</param>
    /// <param name="Dye">One more than a place in <see cref="GearSwatches.Dyes"/>, for its cloth.</param>
    /// <param name="Trim">The same, for what picks the cloth out.</param>
    /// <param name="Ornament">How much is on it, from 1 for nothing to 11 for everything.</param>
    /// <param name="Wear">How worn it is, from 1 for new to 11 for battered.</param>
    public readonly record struct GearTailoring(byte Metal = 0, byte Fitting = 0, byte Leather = 0, byte Dye = 0, byte Trim = 0, byte Ornament = 0, byte Wear = 0)
    {
        public static float? Scale(byte step) => step == 0 ? null : Math.Clamp((step - 1) / 10f, 0f, 1f);
    }

    /// <summary>
    /// Every colour one piece of armour or one weapon is made of, drawn together so that they belong together, each as a material and a colour on it. A colour is chosen as the albedo it should come
    /// to and divided by what its material averages (<see cref="GearMaterials.AlbedoOf"/>), so a tan strap is the same tan on leather as it would be anywhere. How dear the choices are follows how fine
    /// the work is, and a legendary piece is made of what only the finest have.
    /// </summary>
    public sealed class GearColours
    {
        public readonly VertexMaterial Metal;           // Most of the metal on it.
        public readonly VertexMaterial MetalDark;       // The same in a groove, an inset or a shadow of its own.
        public readonly VertexMaterial Edge;            // Metal that's been ground to an edge or polished by use.
        public readonly VertexMaterial Mail;            // The same metal in rings.
        public readonly VertexMaterial Scale;           // The same metal in scales.
        public readonly VertexMaterial Fitting;         // Its other metal: hilts, rivets, buckles, rims and trim.
        public readonly VertexMaterial FittingDark;
        public readonly VertexMaterial Precious;        // Gilding or silver, inlaid or laid over, on what was made with care.
        public readonly VertexMaterial Leather;
        public readonly VertexMaterial LeatherDark;
        public readonly VertexMaterial LeatherContrast; // A second hide, lighter or darker than the first, for what's made of two.     // Edging, welts and the underside.
        public readonly VertexMaterial Strap;           // What straps and wraps are cut from, often another hide.
        public readonly VertexMaterial Cloth;           // Its dye.
        public readonly VertexMaterial ClothDark;       // The same in its folds and lining.
        public readonly VertexMaterial Trim;            // What picks the cloth out: a band, an edge, an embroidery.
        public readonly VertexMaterial Lining;          // Undyed cloth, where it shows from underneath.
        public readonly VertexMaterial Padding;         // A quilted coat under the rest.
        public readonly VertexMaterial Fur;
        public readonly VertexMaterial Wood;
        public readonly VertexMaterial WoodDark;
        public readonly VertexMaterial Bone;
        public readonly VertexMaterial Horn;
        public readonly VertexMaterial Gem;
        public readonly VertexMaterial Glow;            // What's lit from inside.

        public readonly Vector3 GlowColour;
        public readonly string MetalName, FittingName, LeatherName, DyeName, TrimName, WoodName, GemName;

        /// <param name="budget">How dear what it's made of is, from 0 for what anyone has to 1 for what only the finest work gets.</param>
        public GearColours(Genes genes, in GearSpec spec, float budget, float wear, bool legendary)
        {
            GearTailoring tailor = spec.Tailoring;

            // The main metal is what blades and plate are made of, which is steel or iron however fine the work, now and then bronze, blued on the finest, and silvered or gilded only on what's one of a kind.
            // Its fittings are often the other sort, warm on cold or cold on warm.
            GearSwatches.Swatch metal = Pick(genes, "metal", GearSwatches.Metals, budget * 0.6f, tailor.Metal, s => s.Name switch
            {
                "iron" => 1f,
                "steel" => 1.3f,
                "bright steel" => 1.1f,
                "blued steel" => legendary ? 0.6f : 0.25f,
                "bronze" => 0.2f,
                "silver" => legendary ? 0.35f : 0f,
                "gold" => legendary ? 0.2f : 0f,
                _ => 0f,
            });
            bool warm = metal.Name is "bronze" or "copper" or "gold" or "rose gold";
            GearSwatches.Swatch fitting = Pick(genes, "fitting", GearSwatches.Metals, budget, tailor.Fitting, s => (s.Name is "bronze" or "copper" or "gold" or "rose gold") != warm ? 2.2f : s.Name == metal.Name ? 1.2f : 0.7f);
            GearSwatches.Swatch precious = legendary
                ? GearSwatches.Metals[genes.Pick("precious", 0f, 0f, 0f, 0f, 0f, 0f, 3f, 1f, 6f)]
                : budget > 0.55f ? GearSwatches.Find(GearSwatches.Metals, genes.Chance("precious.silver", 0.5f) ? "silver" : "gold") : fitting;
            MetalName = metal.Name;
            FittingName = fitting.Name;

            // Wear takes the shine off, and a little of the colour with it.
            float dull = 1f - 0.22f * wear;
            Vector3 metalColour = metal.Albedo * dull * genes.Range("metal.shade", 0.94f, 1.04f);
            Metal = Of(GearMaterials.Metal, metalColour);
            MetalDark = Of(GearMaterials.Metal, Darker(metalColour, 0.55f));
            Edge = Of(GearMaterials.Metal, Vector3.Lerp(metalColour, new Vector3(0.86f, 0.87f, 0.9f), 0.55f * (1f - wear)));
            Mail = Of(GearMaterials.Mail, metalColour * 0.92f);
            Scale = Of(GearMaterials.Scale, metalColour);
            Vector3 fittingColour = fitting.Albedo * (1f - 0.15f * wear);
            Fitting = Of(GearMaterials.Metal, fittingColour);
            FittingDark = Of(GearMaterials.Metal, Darker(fittingColour, 0.6f));
            Precious = Of(GearMaterials.Metal, precious.Albedo);

            // Hide is its own colour, any of the tanner's browns about as likely as another: a dyed one is the exception, and the better the work the likelier.
            bool dyedHide = genes.Chance("leather.dyed", 0.03f + budget * 0.12f);
            float Natural(GearSwatches.Swatch s) => s.Name.EndsWith("dyed") == dyedHide ? 1f : 0.01f;
            GearSwatches.Swatch leather = Pick(genes, "leather", GearSwatches.Leathers, budget * 0.8f, tailor.Leather, Natural, 0.6f);
            LeatherName = leather.Name;
            Vector3 hide = leather.Albedo * genes.Range("leather.shade", 0.84f, 1.14f);
            Leather = Of(GearMaterials.Leather, hide);
            LeatherDark = Of(GearMaterials.Leather, Darker(hide, 0.55f));
            // Straps are often a different hide from what they hold, a shade or two either way.
            GearSwatches.Swatch strapHide = genes.Chance("strap.same", 0.4f) ? leather : Pick(genes, "strap", GearSwatches.Leathers, budget * 0.6f, 0, s => s.Name == leather.Name ? 0.2f : s.Name.EndsWith("dyed") ? 0.01f : 1f, 0.6f);
            Strap = Of(GearMaterials.Leather, strapHide.Albedo * genes.Range("strap.shade", 0.8f, 1.05f));
            // The second hide is one of the tanner's others, a step lighter or darker in value than the first, as two hides sewn together are.
            float value = GearSwatches.Hsv(leather.Albedo).Value;
            GearSwatches.Swatch second = Pick(genes, "leather.second", GearSwatches.Leathers, budget * 0.8f, 0, s => s.Name.EndsWith("dyed") ? 0.01f : MathF.Abs(GearSwatches.Hsv(s.Albedo).Value - value) > 0.1f ? 1f : 0.05f, 0.8f);
            LeatherContrast = Of(GearMaterials.Leather, second.Albedo * genes.Range("leather.second.shade", 0.88f, 1.1f));

            // A power shows in what it's dyed with, more often than not. Otherwise most cloth is its own colour or a homely dye, and a strong one is dear: the better the work the likelier it
            // is, and a robe likelier than what's worn under armour, so that a hero in five pieces is a few colours and not a festival. The trim goes with the main dye by one of the old rules:
            // a neighbour on the wheel, its opposite, or a plain one.
            string[] themed = GearSwatches.DyesOf(spec.Theme);
            float total = spec.Heavy + spec.Leather + spec.Cloth;
            float robe = total > 0f ? spec.Cloth / total : 0f;
            bool strong = genes.Chance("dye.strong", 0.06f + budget * 0.3f + robe * 0.1f);
            GearSwatches.Swatch dye = themed.Length > 0 && tailor.Dye == 0 && genes.Chance("dye.themed", 0.65f)
                ? GearSwatches.Find(GearSwatches.Dyes, themed[genes.Pick("dye.theme", themed.Select(_ => 1f).ToArray())])
                : Pick(genes, "dye", GearSwatches.Dyes, budget, tailor.Dye, s => (s.Cost > 0.3f) == strong ? 1f : 0.01f, 0.5f);
            DyeName = dye.Name;
            (float dyeHue, float dyeSaturation, _) = GearSwatches.Hsv(dye.Albedo);
            // A neighbour or a plain one most of the time: an opposite is loud, and on a piece worn with others from other hands it's louder.
            int harmony = genes.Pick("trim.harmony", 3.5f, 0.7f, 3.5f);
            GearSwatches.Swatch trim = Pick(genes, "trim", GearSwatches.Dyes, budget, tailor.Trim, s =>
            {
                if (s.Name == dye.Name) return 0.05f;
                (float hue, float saturation, _) = GearSwatches.Hsv(s.Albedo);
                float apart = MathF.Abs(((hue - dyeHue) % 360f + 540f) % 360f - 180f);  // 0 opposite, 180 the same hue.
                bool plain = saturation < 0.45f || dyeSaturation < 0.45f;
                return harmony switch
                {
                    0 => plain ? 1.2f : apart > 120f && apart < 165f ? 2.5f : 0.3f,     // Beside it.
                    1 => plain ? 0.8f : apart < 50f ? 2.5f : 0.3f,                       // Across the wheel.
                    _ => plain ? 2.5f : 0.4f,                                            // Plain, against a strong one.
                };
            });
            TrimName = trim.Name;
            Vector3 dyed = dye.Albedo * (1f - 0.18f * wear) * genes.Range("dye.shade", 0.88f, 1.1f);
            Cloth = Of(GearMaterials.Cloth, dyed);
            ClothDark = Of(GearMaterials.Cloth, Darker(dyed, 0.6f));
            Trim = Of(GearMaterials.Cloth, trim.Albedo * (1f - 0.12f * wear));
            Lining = Of(GearMaterials.Cloth, GearSwatches.Find(GearSwatches.Dyes, genes.Chance("lining", 0.6f) ? "undyed" : "oatmeal").Albedo * 0.85f);
            // Padding is quilted from undyed cloth mostly, now and then from the coat's own, faded.
            Vector3 padded = genes.Chance("padding.dyed", 0.12f + budget * 0.2f) ? Vector3.Lerp(dyed, GearSwatches.Find(GearSwatches.Dyes, "oatmeal").Albedo, 0.45f) : GearSwatches.Find(GearSwatches.Dyes, genes.Chance("padding.pale", 0.5f) ? "undyed" : "oatmeal").Albedo;
            Padding = Of(GearMaterials.Quilt, padded * (1f - 0.15f * wear));

            GearSwatches.Swatch fur = Pick(genes, "fur", GearSwatches.Furs, budget, 0, _ => 1f);
            Fur = Of(GearMaterials.Fur, fur.Albedo);

            GearSwatches.Swatch wood = Pick(genes, "wood", GearSwatches.Woods, budget * 0.85f, 0, s => s.Name == "vermilion lacquer" ? (legendary ? 0.7f : 0.25f) : 1f);
            WoodName = wood.Name;
            Wood = Of(GearMaterials.Timber, wood.Albedo);
            WoodDark = Of(GearMaterials.Timber, Darker(wood.Albedo, 0.6f));

            Vector3 bone = Vector3.Lerp(new Vector3(0.62f, 0.56f, 0.44f), new Vector3(0.5f, 0.42f, 0.3f), genes.Unit("bone.age")) * (1f - 0.1f * wear);
            Bone = Of(GearMaterials.Bone, bone);
            Horn = Of(GearMaterials.Bone, Vector3.Lerp(new Vector3(0.14f, 0.1f, 0.07f), new Vector3(0.26f, 0.19f, 0.12f), genes.Unit("horn.tone")));

            string[] stones = GearSwatches.GemsOf(spec.Theme);
            GearSwatches.Swatch gem = stones.Length > 0 && genes.Chance("gem.themed", 0.8f)
                ? GearSwatches.Find(GearSwatches.Gems, stones[genes.Pick("gem.theme", 1f, 1f)])
                : GearSwatches.Gems[genes.Pick("gem", GearSwatches.Gems.Select(s => 1f).ToArray())];
            GemName = gem.Name;
            Gem = Of(GearMaterials.Gem, gem.Albedo);

            // Colour above 1 on something emissive is how hard it glows, and its hue stays in the albedo. A power's own light, or the stone's, brighter the better the thing.
            GlowColour = spec.Theme != GearTheme.None ? GearSwatches.GlowOf(spec.Theme) : Vector3.Lerp(gem.Albedo, Vector3.One, 0.25f);
            float strength = 1.6f + spec.Grade * 0.45f + (legendary ? 0.8f : 0f);
            Glow = new VertexMaterial(GearMaterials.Glow, new Colour(GlowColour.X, GlowColour.Y, GlowColour.Z, strength));
        }

        /// <summary>
        /// Something on this material that should come to this albedo, as materials that have a colour of their own multiply it into whatever's drawn on them. Whatever's asked for is brought up
        /// out of the Reich's colours first (<see cref="ClearOfReich"/>), so that a shade or a wear can never darken a hero's gear into them.
        /// </summary>
        public static VertexMaterial Of(int material, Vector3 albedo)
        {
            albedo = ClearOfReich(albedo, GearMaterials.IsMetal(material));
            Vector3 average = GearMaterials.AlbedoOf(material);
            return new VertexMaterial(material, new Colour(albedo.X / average.X, albedo.Y / average.Y, albedo.Z / average.Z));
        }

        /// <summary>
        /// A colour lightened just out of the Reich's, if a shade or a wear had darkened or dulled it into one (<see cref="GearSwatches.IsReichs"/>): worn iron's shadow until it isn't black
        /// iron, a red's fold until it isn't oxblood, a dull gold until it's gold and not brass.
        /// </summary>
        public static Vector3 ClearOfReich(Vector3 albedo, bool metal)
        {
            for (int i = 0; i < 24 && GearSwatches.IsReichs(albedo, metal); i++) albedo *= 1.06f;
            return albedo;
        }

        /// <summary>What it comes to on its material, which is what a palette chose: for a check, or a picture with no textures.</summary>
        public static Vector3 AlbedoOf(in VertexMaterial made) => made.colour.ToVector3() * GearMaterials.AlbedoOf(made.materialTextureIndex);

        // Darker, as a shadow of its own is, and a little stronger in colour, as a deep fold is.
        private static Vector3 Darker(Vector3 colour, float by)
        {
            float grey = (colour.X + colour.Y + colour.Z) / 3f;
            return (colour + (colour - new Vector3(grey)) * 0.15f) * by;
        }

        // A choice whose cost is near what the work can afford, by about <spread> either way, weighted by whatever else matters to this choice. What a person chose, if anything, wins.
        private static GearSwatches.Swatch Pick(Genes genes, string name, GearSwatches.Swatch[] list, float budget, byte chosen, Func<GearSwatches.Swatch, float> weight, float spread = 0.28f)
        {
            if (chosen > 0 && chosen <= list.Length) return list[chosen - 1];
            var weights = new float[list.Length];
            for (int i = 0; i < list.Length; i++)
            {
                float apart = (list[i].Cost - budget) / spread;
                weights[i] = MathF.Exp(-apart * apart) * weight(list[i]) + 1e-4f;
            }
            return list[genes.Pick(name, weights)];
        }
    }
}
