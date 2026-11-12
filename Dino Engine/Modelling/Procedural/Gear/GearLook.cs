using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// The decorative language a piece is worked in, shared by every part of it, so that a pommel, a guard and a scabbard's chape are one hand's work and not three. What each means is what it does to
    /// the ends of things, to edges and to bands: a terminal is a knob, a ring, a curl, a facet, a fang or a flute.
    /// </summary>
    public enum Motif : byte
    {
        Plain,      // Clean bevels and round ends: honest work.
        Rope,       // Cord, twist and knot: bands twisted, ends ringed, edges corded.
        Scroll,     // Curls and leaves: ends scrolled back on themselves, edges in waves.
        Facet,      // Cut faces and steps: ends faceted, edges stepped, chevrons.
        Fang,       // What a Defender took from the Reich's beasts: teeth, claws and quills, ends that come to a hook.
        Flute,      // Ridges that run together, as the finest plate is fluted: ends ribbed and crowned.
    }

    /// <summary>
    /// What one weapon or piece of armour is like all over, drawn once from its seed and read by every part of it: how fine the work is, how much is on it, how it's been used, the line it's drawn in, the
    /// motif it's worked in, and what it's made of (<see cref="GearColours"/>). Everything comes from <see cref="Genes"/> by a name of its own, so a new parameter moves nothing else, and anything a person
    /// chooses one day (<see cref="GearTailoring"/>) replaces only what it names. See GEAR.md.
    /// </summary>
    public sealed class GearLook
    {
        public readonly GearSpec Spec;
        public readonly Genes Genes;
        public readonly GearColours Colours;

        /// <summary>How much of each family it is, adding up to 1. What gives none of them, a ring or anything older than families, is leather.</summary>
        public readonly float Heavy, Leather, Cloth;

        /// <summary>
        /// How far up the climb it's from (<see cref="GearSpec.Level"/>), which is the most of what it looks like: low, it's plain, worn and small, the gear of someone starting out, and high it's
        /// finely made, clean, and big in its lines, with more on it, so that a hero looks more powerful as their gear goes up. Rarity says how fine it is for its level.
        /// </summary>
        public readonly float Power;

        public readonly float Craft;        // How fine the work is: the hand that made it, from a village smith to a master.
        public readonly float Ornament;     // How much is on it beyond what it needs.
        public readonly float Wear;         // What it's been through.
        public readonly float Heft;         // Thick and massive, against light and fine.
        public readonly float Angular;      // Cut straight and sharp, against drawn round.
        public readonly float Flare;        // How far its ends spread, against drawing in.
        public readonly float Spikes;       // How much of it comes to a point.
        public readonly float Curl;         // How much its lines curve back on themselves.
        public readonly Motif Motif;
        public readonly GearTheme Theme;

        /// <summary>Whether it's one of a kind: everything on it is the finest of what it could be, and it has what nothing else has (<see cref="Flourish"/>).</summary>
        public readonly bool Legendary;

        public GearLook(GearSpec spec)
        {
            // Every part reads its base's form through what the climb makes of it: bigger and covering more, finer and less worn the higher up, and the reverse low down, within the base's own range.
            Power = Math.Clamp(spec.Level, 0f, 1f);
            Spec = spec with { Form = Climbed(spec.Form, Power) };
            spec = Spec;
            Theme = spec.Theme;
            Genes = new Genes(spec.Seed).Part("look").Part(spec.Kind.ToString());
            Genes style = Genes.Part("style");
            Legendary = spec.Grade >= 4;

            float total = spec.Heavy + spec.Leather + spec.Cloth;
            Heavy = total > 0f ? spec.Heavy / total : 0f;
            Leather = total > 0f ? spec.Leather / total : 1f;
            Cloth = total > 0f ? spec.Cloth / total : 0f;

            // What it's worth shows first: the climb it's from, and then how rare it is for that. A plain thing can still be well made, and a great thing is never plain. A legendary piece is a
            // master's, whatever its base says, and still grows with the climb.
            float grade = Math.Clamp(spec.Grade / 4f, 0f, 1f);
            Craft = Legendary ? Math.Clamp(style.Around("craft", 0.9f + Power * 0.08f, 0.03f), 0.86f, 1f) : Clamp01(style.Around("craft", 0.02f + Power * 0.42f + grade * 0.4f + spec.Form.Fancy * 0.18f, 0.07f));
            float? ornament = GearTailoring.Scale(spec.Tailoring.Ornament);
            Ornament = ornament ?? (Legendary ? 1f : Clamp01(style.Around("ornament", Craft * 0.7f + Power * 0.18f + spec.Form.Fancy * 0.2f - 0.08f, 0.1f)));
            float? wear = GearTailoring.Scale(spec.Tailoring.Wear);
            Wear = wear ?? Clamp01(style.Around("wear", spec.Form.Worn * (1f - Craft * 0.6f) + (1f - Power) * 0.3f - Power * 0.1f, 0.07f));

            Heft = Clamp01(style.Around("heft", 0.42f + Power * 0.18f + (0.5f - spec.Form.Slender) * 0.75f + Heavy * 0.1f, 0.13f));
            Angular = Clamp01(style.Around("angular", 0.45f + Heavy * 0.15f - Cloth * 0.15f, 0.22f));
            Flare = Clamp01(style.Around("flare", 0.25f + Power * 0.3f + Ornament * 0.15f, 0.18f));
            Spikes = Clamp01(style.Around("spikes", 0.18f + Ornament * 0.18f + (Legendary ? 0.15f : 0f), 0.16f));
            Curl = Clamp01(style.Around("curl", 0.25f + spec.Form.Curve * 0.4f + Cloth * 0.1f, 0.18f));

            // The motif a family leans to: plate is cut or fluted, hide is corded or hung with trophies, cloth is scrolled or corded. Plain work is plain.
            float plain = 3.2f * (1f - Craft) + 0.4f;
            Motif = (Motif)style.Pick("motif",
                plain,
                0.4f + Leather * 1.6f + Cloth * 1.2f,
                (0.3f + Cloth * 1.8f + Heavy * 0.6f) * (0.4f + Craft),
                0.5f + Heavy * 1.8f + Angular * 0.8f,
                (0.2f + Leather * 1.8f) * (0.3f + Craft),
                (Heavy * 1.6f + Cloth * 0.3f) * Craft * Craft * 2f);

            float budget = Legendary ? 1f : Clamp01(0.08f + Craft * 0.9f);
            Colours = new GearColours(Genes.Part("colours"), spec, budget, Wear, Legendary);
        }

        /// <summary>Whether it has a stone or anything lit, by name: the better a thing and the more power in it, the likelier, and a legendary piece always does.</summary>
        public bool Shines(string name) => Legendary || Genes.Chance(name, 0.03f + Spec.Grade * 0.1f + (Theme != GearTheme.None ? 0.12f : 0f) + Ornament * 0.08f);

        /// <summary>Whether it has something, going by how likely each family is to.</summary>
        public bool Likely(string name, float heavy, float leather, float cloth) => Genes.Chance(name, Heavy * heavy + Leather * leather + Cloth * cloth);

        /// <summary>A number that depends on the family, drawn from within what that family's range comes to.</summary>
        public float Blend(string name, Vector2 heavy, Vector2 leather, Vector2 cloth)
        {
            float low = Heavy * heavy.X + Leather * leather.X + Cloth * cloth.X;
            float high = Heavy * heavy.Y + Leather * leather.Y + Cloth * cloth.Y;
            return Genes.Range(name, low, high);
        }

        private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

        // A base's form as the climb has it.
        private static GearForm Climbed(GearForm form, float power) => form with
        {
            Size = Clamp01(form.Size + (power - 0.5f) * 0.26f),
            Cover = Clamp01(form.Cover + (power - 0.4f) * 0.3f),
            Fancy = Clamp01(form.Fancy + (power - 0.3f) * 0.45f),
            Worn = Clamp01(form.Worn + (0.4f - power) * 0.45f),
        };
    }
}
