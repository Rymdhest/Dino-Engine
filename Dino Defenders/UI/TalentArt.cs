using Dino_Defenders.Talents;
using Dino_Engine.GUI.Rendering;
using static Dino_Engine.GUI.Rendering.SdfPicture;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// What every picture on the tree looks like, as shapes in a square from -1 to 1 with y running down. A small node's is a bold silhouette that still reads
    /// at a few pixels; a keystone's is drawn bigger and with more to it. It's headless, so BuildLab can lay them all out on a sheet to look at.
    /// </summary>
    public static class TalentArt
    {
        public static SdfPicture Picture(TalentGlyph glyph)
        {
            var g = new SdfPicture();
            Draw(glyph, g);
            return g;
        }

        // ------------------------------------------------------------ pieces several pictures share

        private static void Heart(SdfPicture g, float x, float y, float s)
        {
            g.Add(Circle(x - 0.33f * s, y - 0.2f * s, 0.42f * s)).Add(Circle(x + 0.33f * s, y - 0.2f * s, 0.42f * s));
            g.Add(Polygon(x - 0.72f * s, y - 0.02f * s, x + 0.72f * s, y - 0.02f * s, x, y + 0.82f * s));
        }

        private static void Drop(SdfPicture g, float x, float y, float s)
        {
            g.Add(Circle(x, y + 0.25f * s, 0.55f * s));
            g.Add(Polygon(x - 0.5f * s, y + 0.05f * s, x + 0.5f * s, y + 0.05f * s, x, y - 0.9f * s));
        }

        private static void CutDrop(SdfPicture g, float x, float y, float s)
        {
            g.Cut(Circle(x, y + 0.25f * s, 0.55f * s));
            g.Cut(Polygon(x - 0.5f * s, y + 0.05f * s, x + 0.5f * s, y + 0.05f * s, x, y - 0.9f * s));
        }

        // A heater shield, point down.
        private static Func<OpenTK.Mathematics.Vector2, float> ShieldShape(float x, float y, float s) =>
            Polygon(x - 0.72f * s, y - 0.78f * s, x + 0.72f * s, y - 0.78f * s, x + 0.72f * s, y - 0.1f * s, x + 0.55f * s, y + 0.4f * s,
                x, y + 0.88f * s, x - 0.55f * s, y + 0.4f * s, x - 0.72f * s, y - 0.1f * s);

        private static void ShieldOutline(SdfPicture g, float x, float y, float s, float w)
        {
            g.Add(ShieldShape(x, y, s));
            g.Cut(ShieldShape(x, y + 0.02f * s, s - w));
        }

        private static void Hexagon(SdfPicture g, float x, float y, float r, float w)
        {
            g.Add(Regular(x, y, 6, r, 30f)).Cut(Regular(x, y, 6, r - w, 30f));
        }

        private static void Flame(SdfPicture g, float x, float y, float s)
        {
            g.Add(Polygon(
                x, y - 0.95f * s, x + 0.22f * s, y - 0.55f * s, x + 0.5f * s, y - 0.35f * s, x + 0.62f * s, y + 0.05f * s, x + 0.6f * s, y + 0.42f * s,
                x + 0.35f * s, y + 0.78f * s, x, y + 0.9f * s, x - 0.35f * s, y + 0.78f * s, x - 0.6f * s, y + 0.42f * s, x - 0.62f * s, y + 0.02f * s,
                x - 0.45f * s, y - 0.28f * s, x - 0.3f * s, y - 0.05f * s, x - 0.18f * s, y - 0.45f * s));
            g.Cut(Polygon(x, y - 0.15f * s, x + 0.28f * s, y + 0.25f * s, x + 0.25f * s, y + 0.58f * s, x, y + 0.7f * s, x - 0.25f * s, y + 0.58f * s, x - 0.28f * s, y + 0.25f * s));
        }

        private static void Snowflake(SdfPicture g, float x, float y, float s, float w)
        {
            for (int i = 0; i < 3; i++)
            {
                float a = MathF.PI / 3f * i;
                float dx = MathF.Sin(a) * 0.88f * s, dy = -MathF.Cos(a) * 0.88f * s;
                g.Add(Line(x - dx, y - dy, x + dx, y + dy, w));
            }
            for (int i = 0; i < 6; i++)
            {
                float a = MathF.PI / 3f * i;
                float ex = x + MathF.Sin(a) * 0.55f * s, ey = y - MathF.Cos(a) * 0.55f * s;
                for (int side = -1; side <= 1; side += 2)
                {
                    float b = a + side * 0.75f;
                    g.Add(Line(ex, ey, ex + MathF.Sin(b) * 0.28f * s, ey - MathF.Cos(b) * 0.28f * s, w * 0.85f));
                }
            }
        }

        private static void Bolt(SdfPicture g, float x, float y, float s)
        {
            g.Add(Polygon(x + 0.18f * s, y - 0.95f * s, x - 0.48f * s, y + 0.1f * s, x - 0.04f * s, y + 0.1f * s, x - 0.22f * s, y + 0.95f * s,
                x + 0.5f * s, y - 0.18f * s, x + 0.06f * s, y - 0.18f * s, x + 0.3f * s, y - 0.95f * s));
        }

        private static void Sparkle(SdfPicture g, float x, float y, float s)
        {
            g.Add(Star(x, y, 4, 0.95f * s, 0.24f * s));
        }

        private static void Book(SdfPicture g, float x, float y, float s)
        {
            g.Add(Polygon(x - 0.9f * s, y - 0.55f * s, x - 0.05f * s, y - 0.4f * s, x - 0.05f * s, y + 0.7f * s, x - 0.9f * s, y + 0.55f * s));
            g.Add(Polygon(x + 0.9f * s, y - 0.55f * s, x + 0.05f * s, y - 0.4f * s, x + 0.05f * s, y + 0.7f * s, x + 0.9f * s, y + 0.55f * s));
        }

        private static void Blade(SdfPicture g, float x, float y, float s, float degrees, float length = 1f)
        {
            // A sword pointing up, turned: blade, guard, grip and pommel.
            float a = OpenTK.Mathematics.MathHelper.DegreesToRadians(degrees);
            float c = MathF.Cos(a), n = MathF.Sin(a);
            (float, float) P(float px, float py) => (x + (px * c - py * n) * s, y + (px * n + py * c) * s);
            var tip = P(0f, -0.95f * length);
            var l1 = P(-0.16f, -0.7f * length);
            var l2 = P(-0.16f, 0.3f);
            var r2 = P(0.16f, 0.3f);
            var r1 = P(0.16f, -0.7f * length);
            g.Add(Polygon(tip.Item1, tip.Item2, r1.Item1, r1.Item2, r2.Item1, r2.Item2, l2.Item1, l2.Item2, l1.Item1, l1.Item2));
            g.Add(Box(P(0f, 0.38f).Item1, P(0f, 0.38f).Item2, 0.42f * s, 0.08f * s, 0.04f * s, degrees));
            g.Add(Box(P(0f, 0.62f).Item1, P(0f, 0.62f).Item2, 0.08f * s, 0.2f * s, 0.03f * s, degrees));
            g.Add(Circle(P(0f, 0.86f).Item1, P(0f, 0.86f).Item2, 0.11f * s));
        }

        private static void Arrow(SdfPicture g, float x1, float y1, float x2, float y2, float w)
        {
            g.Add(Line(x1, y1, x2, y2, w));
            float dx = x2 - x1, dy = y2 - y1;
            float length = MathF.Sqrt(dx * dx + dy * dy);
            float ux = dx / length, uy = dy / length;
            float head = w * 2.6f;
            g.Add(Polygon(x2 + ux * head * 0.6f, y2 + uy * head * 0.6f, x2 - ux * head + uy * head * 0.7f, y2 - uy * head - ux * head * 0.7f, x2 - ux * head - uy * head * 0.7f, y2 - uy * head + ux * head * 0.7f));
        }

        private static void Speed(SdfPicture g, float x, float y, float s)
        {
            g.Add(Line(x - 0.9f * s, y - 0.35f * s, x - 0.35f * s, y - 0.35f * s, 0.11f * s));
            g.Add(Line(x - 1f * s, y, x - 0.3f * s, y, 0.11f * s));
            g.Add(Line(x - 0.9f * s, y + 0.35f * s, x - 0.35f * s, y + 0.35f * s, 0.11f * s));
        }

        private static void Cycle(SdfPicture g, float x, float y, float r, float w)
        {
            g.Add(Arc(x, y, r, 30f, 300f, w));
            float a = OpenTK.Mathematics.MathHelper.DegreesToRadians(300f);
            float ex = x + MathF.Sin(a) * r, ey = y - MathF.Cos(a) * r;
            g.Add(Polygon(ex - w * 1.6f, ey - w * 1.2f, ex + w * 1.9f, ey - w * 0.2f, ex - w * 0.2f, ey + w * 2f));
        }

        private static void Figure(SdfPicture g, float x, float y, float s)
        {
            g.Add(Circle(x, y - 0.45f * s, 0.3f * s));
            g.Add(Polygon(x - 0.55f * s, y + 0.75f * s, x - 0.45f * s, y + 0.05f * s, x - 0.2f * s, y - 0.12f * s, x + 0.2f * s, y - 0.12f * s, x + 0.45f * s, y + 0.05f * s, x + 0.55f * s, y + 0.75f * s));
        }

        private static void Skull(SdfPicture g, float x, float y, float s)
        {
            g.Add(Circle(x, y - 0.15f * s, 0.7f * s));
            g.Add(Box(x, y + 0.5f * s, 0.38f * s, 0.25f * s, 0.08f * s));
            g.Cut(Circle(x - 0.27f * s, y - 0.1f * s, 0.18f * s)).Cut(Circle(x + 0.27f * s, y - 0.1f * s, 0.18f * s));
            g.Cut(Polygon(x, y + 0.1f * s, x - 0.09f * s, y + 0.3f * s, x + 0.09f * s, y + 0.3f * s));
            g.Cut(Line(x - 0.13f * s, y + 0.55f * s, x - 0.13f * s, y + 0.8f * s, 0.07f * s)).Cut(Line(x + 0.13f * s, y + 0.55f * s, x + 0.13f * s, y + 0.8f * s, 0.07f * s));
        }

        private static void Mountain(SdfPicture g, float x, float y, float s)
        {
            g.Add(Polygon(x - 0.95f * s, y + 0.7f * s, x - 0.25f * s, y - 0.7f * s, x + 0.12f * s, y, x + 0.4f * s, y - 0.35f * s, x + 0.95f * s, y + 0.7f * s));
        }

        private static void Crosshair(SdfPicture g, float s)
        {
            g.Add(Ring(0f, 0f, 0.62f * s, 0.14f * s));
            g.Add(Line(0f, -0.95f * s, 0f, -0.4f * s, 0.13f * s)).Add(Line(0f, 0.4f * s, 0f, 0.95f * s, 0.13f * s));
            g.Add(Line(-0.95f * s, 0f, -0.4f * s, 0f, 0.13f * s)).Add(Line(0.4f * s, 0f, 0.95f * s, 0f, 0.13f * s));
        }

        // ------------------------------------------------------------ every picture

        private static void Draw(TalentGlyph glyph, SdfPicture g)
        {
            switch (glyph)
            {
                // Attributes.
                case TalentGlyph.Strength:
                    g.Add(Line(-0.75f, 0.42f, 0.18f, 0.42f, 0.44f)).Add(Line(0.18f, 0.42f, 0.32f, -0.42f, 0.38f));
                    g.Add(Circle(-0.22f, 0.14f, 0.34f)).Add(Circle(0.36f, -0.62f, 0.28f));
                    break;
                case TalentGlyph.Dexterity:
                    g.Add(Lens(-0.62f, 0.8f, 0.62f, -0.85f, 0.34f));
                    g.Cut(Line(-0.5f, 0.66f, 0.5f, -0.7f, 0.07f));
                    g.Cut(Line(0.02f, -0.02f, 0.38f, 0.1f, 0.06f)).Cut(Line(-0.16f, 0.24f, 0.18f, 0.38f, 0.06f));
                    g.Add(Line(-0.62f, 0.8f, -0.82f, 0.98f, 0.09f));
                    break;
                case TalentGlyph.Intellect:
                    g.Add(Lens(-0.9f, 0.18f, 0.9f, 0.18f, 0.5f)).Cut(Lens(-0.68f, 0.18f, 0.68f, 0.18f, 0.32f));
                    g.Add(Circle(0f, 0.18f, 0.24f));
                    g.Add(Line(0f, -0.72f, 0f, -0.48f, 0.12f)).Add(Line(-0.48f, -0.58f, -0.34f, -0.4f, 0.12f)).Add(Line(0.48f, -0.58f, 0.34f, -0.4f, 0.12f));
                    break;

                // The pools.
                case TalentGlyph.Health:
                    Heart(g, 0f, -0.02f, 1f);
                    break;
                case TalentGlyph.Mana:
                    Drop(g, 0f, 0.05f, 1f);
                    g.Cut(Lens(-0.32f, 0.08f, -0.12f, 0.55f, 0.07f));
                    break;
                case TalentGlyph.Barrier:
                    Hexagon(g, 0f, 0f, 0.92f, 0.2f);
                    g.Add(Regular(0f, 0f, 6, 0.42f, 30f));
                    break;
                case TalentGlyph.HealthRegen:
                    Heart(g, 0f, 0f, 0.48f);
                    Cycle(g, 0f, 0f, 0.82f, 0.13f);
                    break;
                case TalentGlyph.ManaRegen:
                    Drop(g, 0f, 0.03f, 0.48f);
                    Cycle(g, 0f, 0f, 0.82f, 0.13f);
                    break;
                case TalentGlyph.BarrierRecharge:
                    g.Add(Regular(0f, 0f, 6, 0.42f, 30f));
                    Cycle(g, 0f, 0f, 0.82f, 0.13f);
                    break;
                case TalentGlyph.Recovery:
                    g.Add(Arc(0f, 0f, 0.7f, 20f, 150f, 0.18f)).Add(Arc(0f, 0f, 0.7f, 200f, 330f, 0.18f));
                    g.Add(Polygon(0.42f, 0.42f, 0.86f, 0.32f, 0.62f, 0.75f));
                    g.Add(Polygon(-0.42f, -0.42f, -0.86f, -0.32f, -0.62f, -0.75f));
                    g.Add(Line(-0.2f, 0f, 0.2f, 0f, 0.14f)).Add(Line(0f, -0.2f, 0f, 0.2f, 0.14f));
                    break;
                case TalentGlyph.Leech:
                    Heart(g, 0.1f, 0.25f, 0.62f);
                    g.Cut(Polygon(-0.6f, -0.75f, -0.05f, -0.75f, -0.32f, 0.05f));
                    g.Add(Polygon(-0.55f, -0.95f, -0.1f, -0.95f, -0.32f, -0.2f));
                    break;
                case TalentGlyph.OnHit:
                    g.Add(Star(0f, 0f, 8, 0.95f, 0.42f, 22.5f)).Cut(Circle(0f, 0f, 0.26f));
                    break;
                case TalentGlyph.OnKill:
                    Skull(g, 0f, -0.05f, 0.95f);
                    break;
                case TalentGlyph.Flask:
                    g.Add(Circle(0f, 0.32f, 0.6f)).Add(Box(0f, -0.4f, 0.2f, 0.34f, 0.05f)).Add(Box(0f, -0.8f, 0.28f, 0.1f, 0.05f));
                    g.Cut(Box(0f, 0.38f, 0.62f, 0.08f));
                    break;

                // Not being hurt.
                case TalentGlyph.Armour:
                    g.Add(Polygon(-0.7f, 0.85f, -0.75f, -0.15f, -0.55f, -0.65f, 0f, -0.9f, 0.55f, -0.65f, 0.75f, -0.15f, 0.7f, 0.85f));
                    g.Cut(Box(-0.33f, -0.12f, 0.24f, 0.07f)).Cut(Box(0.33f, -0.12f, 0.24f, 0.07f));
                    g.Cut(Line(0f, 0.15f, 0f, 0.72f, 0.1f));
                    break;
                case TalentGlyph.Evasion:
                    g.Add(Arc(-0.1f, 0.1f, 0.75f, 200f, 320f, 0.16f)).Add(Arc(0.15f, 0.25f, 0.55f, 210f, 330f, 0.15f)).Add(Arc(0.35f, 0.4f, 0.35f, 220f, 340f, 0.14f));
                    g.Add(Circle(0.55f, -0.55f, 0.18f));
                    break;
                case TalentGlyph.Block:
                    g.Add(Circle(0f, 0f, 0.9f)).Cut(Ring(0f, 0f, 0.66f, 0.1f)).Cut(Circle(0f, 0f, 0.32f)).Add(Circle(0f, 0f, 0.2f));
                    break;
                case TalentGlyph.Resist:
                    ShieldOutline(g, 0f, 0f, 1f, 0.24f);
                    break;
                case TalentGlyph.MaxResist:
                    ShieldOutline(g, 0f, 0f, 1f, 0.24f);
                    g.Add(Path(0.16f, -0.32f, 0.2f, 0f, -0.15f, 0.32f, 0.2f));
                    break;
                case TalentGlyph.Reduction:
                    g.Add(ShieldShape(0f, 0f, 1f));
                    g.Cut(Box(0f, 0.02f, 0.4f, 0.09f));
                    break;
                case TalentGlyph.TakenFromMana:
                    ShieldOutline(g, 0f, 0f, 1f, 0.22f);
                    Drop(g, 0f, 0.05f, 0.42f);
                    break;
                case TalentGlyph.Avoid:
                    g.Add(Ring(0f, 0f, 0.75f, 0.2f)).Add(Line(-0.52f, 0.52f, 0.52f, -0.52f, 0.2f));
                    break;
                case TalentGlyph.StunThreshold:
                    g.Add(Box(0f, -0.72f, 0.62f, 0.13f, 0.03f)).Add(Box(0f, 0.75f, 0.68f, 0.14f, 0.03f)).Add(Box(0f, 0f, 0.38f, 0.62f));
                    g.Cut(Line(-0.12f, -0.45f, -0.12f, 0.45f, 0.07f)).Cut(Line(0.12f, -0.45f, 0.12f, 0.45f, 0.07f));
                    break;
                case TalentGlyph.Reflect:
                    g.Add(Star(0f, 0f, 9, 0.95f, 0.55f)).Cut(Circle(0f, 0f, 0.4f));
                    break;

                // Kinds of harm.
                case TalentGlyph.Physical:
                    g.Add(Polygon(-0.8f, 0.35f, -0.55f, -0.45f, 0.05f, -0.8f, 0.7f, -0.42f, 0.85f, 0.3f, 0.35f, 0.78f, -0.35f, 0.75f));
                    g.Cut(Path(0.09f, -0.2f, -0.65f, 0f, -0.2f, -0.25f, 0.15f, 0.05f, 0.55f)).Cut(Path(0.08f, 0f, -0.2f, 0.45f, -0.05f));
                    break;
                case TalentGlyph.Fire:
                    Flame(g, 0f, 0f, 1f);
                    g.Add(Polygon(0f, 0.05f, 0.18f, 0.32f, 0.15f, 0.55f, 0f, 0.62f, -0.15f, 0.55f, -0.18f, 0.32f));
                    break;
                case TalentGlyph.Cold:
                    Snowflake(g, 0f, 0f, 1f, 0.15f);
                    break;
                case TalentGlyph.Lightning:
                    Bolt(g, 0f, 0f, 1f);
                    break;
                case TalentGlyph.Magic:
                    Sparkle(g, -0.12f, 0.08f, 0.85f);
                    Sparkle(g, 0.58f, -0.58f, 0.36f);
                    break;
                case TalentGlyph.Poison:
                    g.Add(Circle(-0.18f, 0.32f, 0.5f)).Cut(Circle(-0.18f, 0.32f, 0.22f));
                    g.Add(Circle(0.48f, -0.25f, 0.3f)).Add(Circle(-0.2f, -0.62f, 0.2f)).Add(Circle(0.62f, 0.55f, 0.16f));
                    break;
                case TalentGlyph.Elemental:
                    Flame(g, -0.45f, 0.35f, 0.5f);
                    Snowflake(g, 0.45f, 0.35f, 0.48f, 0.12f);
                    Bolt(g, 0f, -0.45f, 0.48f);
                    break;
                case TalentGlyph.DamageOverTime:
                    Drop(g, -0.42f, -0.25f, 0.42f);
                    Drop(g, 0.4f, -0.05f, 0.35f);
                    Drop(g, -0.05f, 0.5f, 0.3f);
                    break;

                // What a blow leaves behind.
                case TalentGlyph.Burn:
                    Flame(g, 0f, -0.15f, 0.82f);
                    g.Add(Box(0f, 0.86f, 0.62f, 0.07f, 0.05f));
                    break;
                case TalentGlyph.Bleed:
                    Drop(g, 0f, 0.05f, 1f);
                    g.Cut(Line(-0.6f, 0.15f, 0.6f, -0.25f, 0.12f));
                    break;
                case TalentGlyph.Venom:
                    g.Add(Arc(0f, -0.95f, 0.85f, 120f, 240f, 0.16f));
                    g.Add(Polygon(-0.55f, -0.32f, -0.25f, -0.32f, -0.4f, 0.25f)).Add(Polygon(0.25f, -0.32f, 0.55f, -0.32f, 0.4f, 0.25f));
                    Drop(g, -0.4f, 0.62f, 0.22f);
                    Drop(g, 0.4f, 0.62f, 0.22f);
                    break;
                case TalentGlyph.Chill:
                    Snowflake(g, 0f, 0f, 0.9f, 0.1f);
                    g.Add(Ring(0f, 0f, 0.92f, 0.08f));
                    break;
                case TalentGlyph.Freeze:
                    g.Add(Regular(0f, 0f, 6, 0.95f, 0f)).Cut(Regular(0f, 0f, 6, 0.75f, 0f));
                    Snowflake(g, 0f, 0f, 0.6f, 0.13f);
                    break;
                case TalentGlyph.Shock:
                    g.Add(Ring(0f, 0f, 0.82f, 0.13f));
                    Bolt(g, 0f, 0f, 0.62f);
                    break;
                case TalentGlyph.Hex:
                    g.Add(Ring(0f, 0f, 0.82f, 0.14f));
                    g.Add(Path(0.13f, -0.6f, -0.38f, 0.6f, -0.38f, 0f, 0.62f, -0.6f, -0.38f));
                    g.Add(Circle(0f, -0.02f, 0.13f));
                    break;
                case TalentGlyph.Stun:
                    g.Add(Star(-0.5f, 0.15f, 5, 0.38f, 0.15f)).Add(Star(0.05f, -0.35f, 5, 0.42f, 0.17f)).Add(Star(0.55f, 0.25f, 5, 0.34f, 0.13f));
                    g.Add(Arc(0f, 0.9f, 0.85f, 300f, 60f, 0.1f));
                    break;

                // What a blow is.
                case TalentGlyph.Damage:
                    g.Add(Star(0f, 0f, 8, 0.95f, 0.38f));
                    break;
                case TalentGlyph.Attack:
                    Blade(g, 0f, 0f, 0.95f, 45f);
                    Blade(g, 0f, 0f, 0.95f, -45f);
                    break;
                case TalentGlyph.Spell:
                    g.Add(Ring(0f, 0f, 0.85f, 0.12f));
                    g.Add(Star(0f, 0f, 4, 0.62f, 0.16f));
                    g.Add(Circle(0.42f, -0.42f, 0.08f)).Add(Circle(-0.42f, -0.42f, 0.08f)).Add(Circle(0.42f, 0.42f, 0.08f)).Add(Circle(-0.42f, 0.42f, 0.08f));
                    break;
                case TalentGlyph.Crit:
                    Crosshair(g, 1f);
                    g.Add(Star(0f, 0f, 4, 0.36f, 0.1f));
                    break;
                case TalentGlyph.CritMulti:
                    g.Add(Star(-0.18f, 0.12f, 4, 0.8f, 0.2f)).Add(Star(0.55f, -0.5f, 4, 0.4f, 0.1f)).Add(Star(0.5f, 0.55f, 4, 0.3f, 0.08f));
                    break;
                case TalentGlyph.Accuracy:
                    Crosshair(g, 1f);
                    g.Add(Circle(0f, 0f, 0.14f));
                    break;
                case TalentGlyph.Penetration:
                    ShieldOutline(g, 0.12f, 0.08f, 0.82f, 0.2f);
                    Arrow(g, -0.9f, 0.9f, 0.72f, -0.72f, 0.12f);
                    break;
                case TalentGlyph.ArmourIgnore:
                    g.Add(ShieldShape(0f, 0f, 1f));
                    g.Cut(Path(0.13f, -0.1f, -0.85f, 0.12f, -0.3f, -0.15f, 0.1f, 0.08f, 0.95f));
                    break;
                case TalentGlyph.AttackSpeed:
                    Blade(g, 0.25f, 0.05f, 0.88f, 45f);
                    Speed(g, -0.05f, 0.25f, 0.55f);
                    break;
                case TalentGlyph.CastSpeed:
                    g.Add(Star(0.32f, 0f, 4, 0.68f, 0.17f));
                    Speed(g, -0.05f, 0f, 0.65f);
                    break;
                case TalentGlyph.UseSpeed:
                    g.Add(Polygon(-0.85f, -0.65f, 0.05f, 0f, -0.85f, 0.65f)).Add(Polygon(-0.05f, -0.65f, 0.85f, 0f, -0.05f, 0.65f));
                    break;
                case TalentGlyph.Execute:
                    g.Add(Line(-0.55f, 0.9f, 0.35f, -0.65f, 0.13f));
                    g.Add(Polygon(0.35f, -0.65f, 0.9f, -0.45f, 0.75f, 0.05f, 0.55f, -0.3f, 0.2f, -0.45f));
                    break;
                case TalentGlyph.Explode:
                    g.Add(Star(0f, 0f, 10, 0.95f, 0.55f, 9f)).Cut(Star(0f, 0f, 10, 0.55f, 0.32f, 27f));
                    g.Add(Circle(0f, 0f, 0.2f));
                    break;
                case TalentGlyph.Conversion:
                    g.Add(Arc(0f, 0f, 0.62f, 300f, 60f, 0.16f)).Add(Arc(0f, 0f, 0.62f, 120f, 240f, 0.16f));
                    g.Add(Polygon(0.42f, -0.62f, 0.78f, -0.18f, 0.25f, -0.2f)).Add(Polygon(-0.42f, 0.62f, -0.78f, 0.18f, -0.25f, 0.2f));
                    g.Add(Circle(0f, 0f, 0.18f));
                    break;

                // How it gets there.
                case TalentGlyph.Melee:
                    Blade(g, 0f, 0f, 1f, 0f);
                    break;
                case TalentGlyph.Projectile:
                    Arrow(g, -0.75f, 0.75f, 0.65f, -0.65f, 0.12f);
                    g.Add(Line(-0.75f, 0.75f, -0.95f, 0.45f, 0.1f)).Add(Line(-0.75f, 0.75f, -0.45f, 0.95f, 0.1f));
                    break;
                case TalentGlyph.Area:
                    g.Add(Circle(0f, 0f, 0.22f)).Add(Ring(0f, 0f, 0.48f, 0.11f));
                    for (int i = 0; i < 4; i++)
                    {
                        float a = MathF.PI / 2f * i + MathF.PI / 4f;
                        Arrow(g, MathF.Sin(a) * 0.62f, -MathF.Cos(a) * 0.62f, MathF.Sin(a) * 0.86f, -MathF.Cos(a) * 0.86f, 0.09f);
                    }
                    break;
                case TalentGlyph.Zone:
                    for (int i = 0; i < 8; i++) g.Add(Arc(0f, 0f, 0.75f, i * 45f + 8f, i * 45f + 32f, 0.16f));
                    g.Add(Circle(0f, 0f, 0.3f));
                    break;
                case TalentGlyph.Chain:
                    g.Add(Box(-0.3f, -0.3f, 0.38f, 0.22f, 0.22f, -45f)).Cut(Box(-0.3f, -0.3f, 0.22f, 0.07f, 0.07f, -45f));
                    g.Add(Box(0.3f, 0.3f, 0.38f, 0.22f, 0.22f, -45f)).Cut(Box(0.3f, 0.3f, 0.22f, 0.07f, 0.07f, -45f));
                    g.Add(Line(-0.1f, -0.1f, 0.1f, 0.1f, 0.13f));
                    break;
                case TalentGlyph.Pierce:
                    g.Add(Ring(0.05f, -0.05f, 0.42f, 0.14f));
                    Arrow(g, -0.9f, 0.9f, 0.72f, -0.72f, 0.11f);
                    break;
                case TalentGlyph.ProjectileSpeed:
                    Arrow(g, -0.3f, 0.25f, 0.8f, -0.55f, 0.11f);
                    Speed(g, -0.2f, 0.35f, 0.5f);
                    break;
                case TalentGlyph.ProjectileCount:
                    Arrow(g, 0f, 0.85f, 0f, -0.6f, 0.1f);
                    Arrow(g, -0.12f, 0.85f, -0.7f, -0.4f, 0.1f);
                    Arrow(g, 0.12f, 0.85f, 0.7f, -0.4f, 0.1f);
                    break;
                case TalentGlyph.MeleeReach:
                    Blade(g, -0.2f, 0.1f, 0.85f, 0f);
                    Arrow(g, 0.55f, 0.1f, 0.55f, -0.75f, 0.09f);
                    Arrow(g, 0.55f, 0.1f, 0.55f, 0.85f, 0.09f);
                    break;

                // What it's done with.
                case TalentGlyph.Sword:
                    Blade(g, 0f, 0f, 1f, 30f);
                    break;
                case TalentGlyph.Axe:
                    g.Add(Line(-0.55f, 0.9f, 0.35f, -0.7f, 0.13f));
                    g.Add(Polygon(0.05f, -0.55f, 0.35f, -0.92f, 0.85f, -0.35f, 0.75f, 0.12f, 0.42f, 0.02f, 0.2f, -0.18f));
                    break;
                case TalentGlyph.Mace:
                    g.Add(Line(-0.6f, 0.85f, 0.2f, -0.15f, 0.14f));
                    g.Add(Star(0.38f, -0.38f, 8, 0.58f, 0.38f));
                    break;
                case TalentGlyph.Dagger:
                    Blade(g, 0f, 0.1f, 0.85f, 35f, 0.65f);
                    break;
                case TalentGlyph.Spear:
                    g.Add(Line(-0.85f, 0.85f, 0.35f, -0.35f, 0.11f));
                    g.Add(Lens(0.25f, -0.25f, 0.88f, -0.88f, 0.18f));
                    break;
                case TalentGlyph.Bow:
                    g.Add(Arc(-0.9f, 0f, 1.3f, 40f, 140f, 0.16f));
                    g.Add(Line(0.08f, -0.83f, 0.08f, 0.83f, 0.06f));
                    Arrow(g, -0.6f, 0f, 0.62f, 0f, 0.08f);
                    break;
                case TalentGlyph.Staff:
                    g.Add(Line(0.05f, -0.35f, -0.3f, 0.95f, 0.13f));
                    g.Add(Circle(0.15f, -0.62f, 0.26f)).Add(Ring(0.15f, -0.62f, 0.42f, 0.08f));
                    break;
                case TalentGlyph.Wand:
                    g.Add(Line(-0.65f, 0.75f, 0.2f, -0.1f, 0.13f));
                    g.Add(Star(0.45f, -0.4f, 5, 0.42f, 0.18f));
                    break;
                case TalentGlyph.Unarmed:
                    for (int i = 0; i < 4; i++) g.Add(Box(-0.45f + i * 0.3f, -0.22f, 0.13f, 0.3f, 0.12f));
                    g.Add(Box(0.02f, 0.38f, 0.6f, 0.34f, 0.2f));
                    g.Add(Line(-0.62f, 0.15f, -0.25f, 0.02f, 0.2f));
                    g.Cut(Line(-0.4f, 0.12f, 0.45f, 0.12f, 0.06f));
                    break;
                case TalentGlyph.TwoHanded:
                    Blade(g, 0f, 0.05f, 1f, 0f, 1.05f);
                    g.Add(Box(0f, 0.38f, 0.55f, 0.07f));
                    break;
                case TalentGlyph.DualWield:
                    Blade(g, -0.18f, 0f, 0.9f, -35f, 0.7f);
                    Blade(g, 0.18f, 0f, 0.9f, 35f, 0.7f);
                    break;
                case TalentGlyph.Shield:
                    g.Add(ShieldShape(0f, 0f, 1f));
                    g.Cut(Line(0f, -0.65f, 0f, 0.7f, 0.12f)).Cut(Line(-0.6f, -0.25f, 0.6f, -0.25f, 0.12f));
                    break;

                // Books.
                case TalentGlyph.Potency:
                    Book(g, 0f, 0.25f, 0.85f);
                    g.Add(Star(0f, -0.55f, 4, 0.42f, 0.12f));
                    break;
                case TalentGlyph.Opening:
                    Book(g, 0f, 0.05f, 1f);
                    g.Cut(Polygon(-0.62f, -0.2f, -0.25f, 0.08f, -0.62f, 0.36f));
                    break;
                case TalentGlyph.Turned:
                    Book(g, 0f, 0.1f, 1f);
                    g.Cut(Arc(0.05f, 0.55f, 0.75f, 300f, 20f, 0.1f));
                    break;
                case TalentGlyph.Closing:
                    g.Add(Box(0.05f, 0f, 0.62f, 0.85f, 0.08f)).Cut(Box(0.15f, 0f, 0.44f, 0.7f, 0.04f));
                    g.Add(Box(-0.6f, 0f, 0.1f, 0.85f, 0.04f));
                    break;
                case TalentGlyph.Wild:
                    g.Add(Box(0f, 0f, 0.8f, 0.8f, 0.2f, 12f));
                    g.Cut(Circle(-0.36f, -0.3f, 0.14f)).Cut(Circle(0.05f, 0.02f, 0.14f)).Cut(Circle(0.42f, 0.36f, 0.14f));
                    break;
                case TalentGlyph.Falling:
                    g.Add(Circle(0.3f, 0.32f, 0.45f));
                    g.Add(Line(-0.1f, -0.1f, -0.75f, -0.75f, 0.13f)).Add(Line(0.32f, -0.2f, -0.15f, -0.85f, 0.11f)).Add(Line(-0.2f, 0.3f, -0.85f, -0.15f, 0.11f));
                    break;
                case TalentGlyph.Cost:
                    g.Add(Polygon(-0.85f, -0.4f, 0.35f, -0.4f, 0.9f, 0f, 0.35f, 0.4f, -0.85f, 0.4f)).Cut(Circle(0.35f, 0f, 0.13f));
                    break;
                case TalentGlyph.Cooldown:
                    g.Add(Ring(0f, 0.08f, 0.75f, 0.16f)).Add(Line(0f, 0.08f, 0f, -0.4f, 0.13f)).Add(Line(0f, 0.08f, 0.32f, 0.3f, 0.13f));
                    g.Add(Box(0f, -0.88f, 0.25f, 0.08f, 0.04f));
                    break;
                case TalentGlyph.Duration:
                    g.Add(Polygon(-0.55f, -0.75f, 0.55f, -0.75f, 0.1f, 0f, 0.55f, 0.75f, -0.55f, 0.75f, -0.1f, 0f));
                    g.Add(Box(0f, -0.85f, 0.7f, 0.1f, 0.04f)).Add(Box(0f, 0.85f, 0.7f, 0.1f, 0.04f));
                    break;

                // Company.
                case TalentGlyph.Minion:
                    Figure(g, 0f, 0f, 1f);
                    break;
                case TalentGlyph.MinionCount:
                    Figure(g, -0.5f, 0.15f, 0.55f);
                    Figure(g, 0.5f, 0.15f, 0.55f);
                    Figure(g, 0f, -0.05f, 0.7f);
                    break;
                case TalentGlyph.Raise:
                    Figure(g, 0f, -0.1f, 0.7f);
                    g.Add(Box(0f, 0.8f, 0.9f, 0.07f));
                    g.Add(Path(0.1f, -0.75f, 0.45f, -0.55f, 0.25f, -0.35f, 0.45f));
                    break;

                // Getting about.
                case TalentGlyph.Move:
                    g.Add(Polygon(-0.35f, -0.85f, 0.15f, -0.85f, 0.15f, 0.2f, 0.85f, 0.45f, 0.85f, 0.8f, -0.4f, 0.8f));
                    Speed(g, -0.35f, 0.25f, 0.45f);
                    break;
                case TalentGlyph.Dodge:
                    g.Add(Arc(0.1f, 0.45f, 0.75f, 270f, 30f, 0.15f));
                    g.Add(Polygon(0.42f, -0.48f, 0.78f, -0.02f, 0.2f, 0.02f));
                    g.Add(Circle(-0.45f, 0.55f, 0.2f));
                    break;
                case TalentGlyph.Stealth:
                    g.Add(Arc(0f, -0.45f, 0.95f, 125f, 235f, 0.16f));
                    for (int i = -2; i <= 2; i++)
                    {
                        float a = OpenTK.Mathematics.MathHelper.DegreesToRadians(180f + i * 22f);
                        float ex = MathF.Sin(a) * 0.95f, ey = -0.45f - MathF.Cos(a) * 0.95f;
                        g.Add(Line(ex, ey, ex * 1.25f, ey + 0.28f, 0.1f));
                    }
                    break;

                // The land.
                case TalentGlyph.Water:
                    g.Add(Wave(-0.85f, 0.85f, -0.45f, 0.13f, 1.5f, 0.15f)).Add(Wave(-0.85f, 0.85f, 0f, 0.13f, 1.5f, 0.15f)).Add(Wave(-0.85f, 0.85f, 0.45f, 0.13f, 1.5f, 0.15f));
                    break;
                case TalentGlyph.Trees:
                    g.Add(Polygon(0f, -0.95f, 0.45f, -0.3f, 0.25f, -0.3f, 0.7f, 0.35f, -0.7f, 0.35f, -0.25f, -0.3f, -0.45f, -0.3f));
                    g.Add(Box(0f, 0.62f, 0.12f, 0.3f));
                    break;
                case TalentGlyph.Road:
                    g.Add(Polygon(-0.2f, -0.85f, 0.2f, -0.85f, 0.8f, 0.85f, -0.8f, 0.85f));
                    g.Cut(Line(0f, -0.6f, 0f, -0.35f, 0.08f)).Cut(Line(0f, -0.05f, 0f, 0.25f, 0.1f)).Cut(Line(0f, 0.5f, 0f, 0.82f, 0.12f));
                    break;
                case TalentGlyph.Darkness:
                    g.Add(Box(0f, 0.2f, 0.55f, 0.7f, 0f)).Add(Circle(0f, -0.35f, 0.55f));
                    g.Cut(Box(0f, -0.1f, 0.3f, 0.06f)).Add(Box(0f, 0.86f, 0.8f, 0.08f));
                    break;
                case TalentGlyph.HotLand:
                    g.Add(Circle(0f, 0.15f, 0.45f)).Cut(Box(0f, 0.6f, 1f, 0.45f));
                    for (int i = 0; i < 5; i++)
                    {
                        float a = OpenTK.Mathematics.MathHelper.DegreesToRadians(-70f + i * 35f);
                        g.Add(Line(MathF.Sin(a) * 0.6f, 0.15f - MathF.Cos(a) * 0.6f, MathF.Sin(a) * 0.85f, 0.15f - MathF.Cos(a) * 0.85f, 0.1f));
                    }
                    g.Add(Wave(-0.9f, 0.9f, 0.45f, 0.08f, 1f, 0.12f)).Add(Wave(-0.7f, 0.7f, 0.75f, 0.06f, 1f, 0.1f));
                    break;
                case TalentGlyph.ColdLand:
                    Mountain(g, 0f, 0.1f, 1f);
                    g.Cut(Path(0.1f, -0.62f, -0.12f, -0.38f, -0.25f, -0.2f, -0.12f, 0.02f, -0.22f));
                    break;
                case TalentGlyph.WetLand:
                    g.Add(Line(-0.4f, 0.6f, -0.4f, -0.6f, 0.08f)).Add(Lens(-0.4f, -0.75f, -0.4f, -0.25f, 0.12f));
                    g.Add(Line(0.1f, 0.6f, 0.1f, -0.85f, 0.08f)).Add(Lens(0.1f, -0.95f, 0.1f, -0.45f, 0.12f));
                    g.Add(Line(0.55f, 0.6f, 0.55f, -0.4f, 0.08f));
                    g.Add(Wave(-0.9f, 0.9f, 0.72f, 0.08f, 1.5f, 0.12f));
                    break;
                case TalentGlyph.HighLand:
                    Mountain(g, 0f, 0.15f, 1f);
                    break;
                case TalentGlyph.Rain:
                    g.Add(Circle(-0.35f, -0.25f, 0.35f)).Add(Circle(0.2f, -0.4f, 0.42f)).Add(Box(0f, -0.08f, 0.68f, 0.2f, 0.18f));
                    g.Add(Line(-0.45f, 0.4f, -0.55f, 0.7f, 0.1f)).Add(Line(0f, 0.35f, -0.1f, 0.65f, 0.1f)).Add(Line(0.45f, 0.4f, 0.35f, 0.7f, 0.1f));
                    break;

                // Fortune.
                case TalentGlyph.Gold:
                    for (int i = 0; i < 3; i++) g.Add(Box(0f, 0.55f - i * 0.38f, 0.62f, 0.15f, 0.15f));
                    g.Add(Circle(0f, -0.62f, 0.3f)).Cut(Circle(0f, -0.62f, 0.14f));
                    break;
                case TalentGlyph.Rarity:
                    g.Add(Polygon(-0.85f, -0.25f, -0.45f, -0.75f, 0.45f, -0.75f, 0.85f, -0.25f, 0f, 0.88f));
                    g.Cut(Line(-0.85f, -0.25f, 0.85f, -0.25f, 0.06f)).Cut(Path(0.06f, -0.45f, -0.75f, -0.25f, -0.25f, 0f, 0.88f, 0.25f, -0.25f, 0.45f, -0.75f));
                    break;
                case TalentGlyph.Experience:
                    g.Add(Star(0f, 0.12f, 5, 0.75f, 0.32f));
                    g.Add(Polygon(0f, -0.98f, 0.25f, -0.7f, -0.25f, -0.7f));
                    break;
                case TalentGlyph.Reaction:
                    g.Add(Circle(-0.45f, 0.3f, 0.36f)).Add(Circle(0.45f, 0.3f, 0.36f));
                    g.Add(Star(0f, -0.35f, 6, 0.48f, 0.18f));
                    break;

                // Small marks in a corner.
                case TalentGlyph.Chance:
                    g.Add(Box(0f, 0f, 0.8f, 0.8f, 0.2f)).Cut(Circle(-0.35f, -0.35f, 0.17f)).Cut(Circle(0.35f, 0.35f, 0.17f)).Cut(Circle(0f, 0f, 0.17f));
                    break;
                case TalentGlyph.Longer:
                    g.Add(Polygon(-0.65f, -0.8f, 0.65f, -0.8f, 0.12f, 0f, 0.65f, 0.8f, -0.65f, 0.8f, -0.12f, 0f));
                    break;
                case TalentGlyph.Stronger:
                    g.Add(Path(0.28f, -0.7f, 0.05f, 0f, -0.6f, 0.7f, 0.05f)).Add(Path(0.28f, -0.7f, 0.75f, 0f, 0.1f, 0.7f, 0.75f));
                    break;
                case TalentGlyph.Up:
                    g.Add(Line(-0.75f, 0f, 0.75f, 0f, 0.36f)).Add(Line(0f, -0.75f, 0f, 0.75f, 0.36f));
                    break;

                // The root and the waygates.
                case TalentGlyph.Root:
                    g.Add(Star(0f, 0f, 12, 0.95f, 0.6f)).Cut(Circle(0f, 0f, 0.52f)).Add(Circle(0f, 0f, 0.36f));
                    break;
                case TalentGlyph.Waygate:
                    g.Add(Box(0f, 0.25f, 0.68f, 0.65f, 0f)).Add(Circle(0f, -0.4f, 0.68f));
                    g.Cut(Box(0f, 0.32f, 0.44f, 0.62f, 0f)).Cut(Circle(0f, -0.4f, 0.44f));
                    g.Add(Arc(0f, 0f, 0.26f, 0f, 270f, 0.1f));
                    break;

                default:
                    Keystone(glyph, g);
                    break;
            }
        }

        // Each keystone's picture is its own, drawn bigger than a small node's, with more to it.
        private static void Keystone(TalentGlyph glyph, SdfPicture g)
        {
            switch (glyph)
            {
                case TalentGlyph.Mask:
                    g.Add(Lens(0f, -0.92f, 0f, 0.92f, 0.72f));
                    g.Cut(Lens(-0.62f, -0.12f, -0.08f, -0.12f, 0.16f)).Cut(Lens(0.08f, -0.12f, 0.62f, -0.12f, 0.16f));
                    g.Cut(Arc(0f, 0.1f, 0.42f, 135f, 225f, 0.1f));
                    g.Add(Line(-0.15f, -0.5f, -0.45f, -0.65f, 0.06f)).Cut(Line(0.15f, -0.5f, 0.45f, -0.65f, 0.06f));
                    break;
                case TalentGlyph.Bell:
                    g.Add(Polygon(-0.2f, -0.72f, 0.2f, -0.72f, 0.45f, -0.3f, 0.52f, 0.35f, 0.72f, 0.55f, -0.72f, 0.55f, -0.52f, 0.35f, -0.45f, -0.3f));
                    g.Add(Circle(0f, -0.8f, 0.12f)).Add(Circle(0f, 0.72f, 0.16f));
                    g.Add(Arc(0f, -0.1f, 0.95f, 55f, 85f, 0.08f)).Add(Arc(0f, -0.1f, 0.95f, 275f, 305f, 0.08f));
                    break;
                case TalentGlyph.Quill:
                    g.Add(Lens(-0.25f, 0.35f, 0.7f, -0.85f, 0.28f));
                    g.Cut(Line(-0.1f, 0.15f, 0.6f, -0.72f, 0.05f));
                    g.Add(Polygon(-0.25f, 0.35f, -0.12f, 0.45f, -0.62f, 0.95f));
                    g.Add(Path(0.07f, -0.95f, 0.85f, -0.5f, 0.75f, -0.15f, 0.88f, 0.25f, 0.78f));
                    break;
                case TalentGlyph.Feather:
                    g.Add(Lens(-0.45f, 0.75f, 0.55f, -0.85f, 0.3f));
                    g.Cut(Line(-0.38f, 0.65f, 0.48f, -0.75f, 0.06f));
                    g.Cut(Line(0.0f, 0.0f, 0.32f, 0.1f, 0.05f)).Cut(Line(-0.15f, 0.25f, 0.15f, 0.35f, 0.05f)).Cut(Line(0.12f, -0.25f, -0.15f, -0.35f, 0.05f));
                    Speed(g, -0.1f, 0.15f, 0.42f);
                    break;
                case TalentGlyph.Seal:
                    g.Add(Star(0f, 0f, 14, 0.95f, 0.8f)).Cut(Circle(0f, 0f, 0.62f));
                    g.Add(Path(0.12f, 0f, -0.42f, 0.38f, 0.25f, -0.38f, 0.25f, 0f, -0.42f)).Add(Circle(0f, 0.04f, 0.1f));
                    break;
                case TalentGlyph.Prism:
                    g.Add(Polygon(0f, -0.62f, 0.55f, 0.38f, -0.55f, 0.38f)).Cut(Polygon(0f, -0.3f, 0.3f, 0.22f, -0.3f, 0.22f));
                    g.Add(Line(-0.98f, -0.05f, -0.3f, -0.08f, 0.09f));
                    g.Add(Line(0.3f, -0.08f, 0.98f, -0.4f, 0.08f)).Add(Line(0.32f, 0f, 0.98f, 0f, 0.08f)).Add(Line(0.3f, 0.08f, 0.98f, 0.4f, 0.08f));
                    break;
                case TalentGlyph.Comet:
                    g.Add(Circle(0.42f, -0.42f, 0.34f));
                    g.Add(Line(0.2f, -0.2f, -0.85f, 0.85f, 0.14f)).Add(Line(0.12f, -0.45f, -0.6f, 0.3f, 0.09f)).Add(Line(0.45f, -0.12f, -0.3f, 0.62f, 0.09f));
                    break;
                case TalentGlyph.Fork:
                    g.Add(Path(0.13f, 0.1f, -0.95f, -0.2f, -0.3f, 0.12f, -0.2f, -0.15f, 0.4f, 0.05f, 0.95f));
                    g.Add(Path(0.1f, 0.12f, -0.2f, 0.55f, 0.1f, 0.4f, 0.4f, 0.8f, 0.75f));
                    g.Add(Path(0.09f, -0.15f, 0.4f, -0.6f, 0.62f, -0.5f, 0.85f));
                    break;
                case TalentGlyph.Tower:
                    g.Add(Box(0f, 0.35f, 0.32f, 0.6f, 0f)).Add(Box(0f, -0.25f, 0.45f, 0.08f, 0f));
                    g.Add(Box(-0.32f, -0.4f, 0.1f, 0.1f)).Add(Box(0f, -0.4f, 0.1f, 0.1f)).Add(Box(0.32f, -0.4f, 0.1f, 0.1f));
                    g.Add(Line(0f, -0.5f, 0f, -0.85f, 0.06f));
                    Bolt(g, 0.42f, -0.62f, 0.36f);
                    g.Cut(Box(0f, 0.6f, 0.1f, 0.18f, 0.08f));
                    break;
                case TalentGlyph.Hourglass:
                    g.Add(Polygon(-0.48f, -0.7f, 0.48f, -0.7f, 0.1f, 0f, 0.48f, 0.7f, -0.48f, 0.7f, -0.1f, 0f)).Cut(Polygon(-0.3f, -0.6f, 0.3f, -0.6f, 0f, -0.15f));
                    g.Add(Box(0f, -0.82f, 0.72f, 0.1f, 0.04f)).Add(Box(0f, 0.82f, 0.72f, 0.1f, 0.04f));
                    g.Add(Line(-0.62f, -0.75f, -0.62f, 0.75f, 0.08f)).Add(Line(0.62f, -0.75f, 0.62f, 0.75f, 0.08f));
                    break;
                case TalentGlyph.Skull:
                    Skull(g, 0f, 0.05f, 0.85f);
                    g.Add(Arc(-0.55f, -0.55f, 0.3f, 180f, 360f, 0.07f)).Add(Arc(0.6f, -0.62f, 0.25f, 0f, 180f, 0.07f)).Add(Arc(0.75f, 0.5f, 0.18f, 90f, 270f, 0.06f));
                    break;
                case TalentGlyph.Web:
                    for (int i = 0; i < 8; i++)
                    {
                        float a = MathF.PI / 4f * i;
                        g.Add(Line(0f, 0f, MathF.Sin(a) * 0.95f, -MathF.Cos(a) * 0.95f, 0.06f));
                    }
                    g.Add(Ring(0f, 0f, 0.3f, 0.06f)).Add(Ring(0f, 0f, 0.58f, 0.06f)).Add(Ring(0f, 0f, 0.86f, 0.06f));
                    g.Add(Circle(0.3f, 0.22f, 0.14f));
                    break;
                case TalentGlyph.Moon:
                    g.Add(Circle(-0.1f, 0f, 0.82f)).Cut(Circle(0.28f, -0.18f, 0.7f));
                    g.Add(Star(0.55f, 0.35f, 4, 0.28f, 0.08f)).Add(Star(0.62f, -0.55f, 4, 0.18f, 0.05f));
                    break;
                case TalentGlyph.Eye:
                    g.Add(Lens(-0.95f, 0f, 0.95f, 0f, 0.5f)).Cut(Lens(-0.72f, 0f, 0.72f, 0f, 0.32f));
                    g.Add(Ring(0f, 0f, 0.26f, 0.08f)).Add(Circle(0f, 0f, 0.08f));
                    g.Add(Line(0f, -0.92f, 0f, -0.62f, 0.07f)).Add(Line(0f, 0.62f, 0f, 0.92f, 0.07f));
                    break;
                case TalentGlyph.Peak:
                    Mountain(g, 0f, 0.2f, 1f);
                    g.Cut(Path(0.09f, -0.5f, -0.15f, -0.32f, -0.05f, -0.15f, -0.22f));
                    g.Add(Line(-0.25f, -0.5f, -0.25f, -0.95f, 0.06f)).Add(Polygon(-0.22f, -0.95f, 0.15f, -0.85f, -0.22f, -0.72f));
                    break;
                case TalentGlyph.Sun:
                    g.Add(Circle(0f, 0f, 0.36f));
                    for (int i = 0; i < 12; i++)
                    {
                        float a = MathF.PI / 6f * i;
                        Arrow(g, MathF.Sin(a) * 0.48f, -MathF.Cos(a) * 0.48f, MathF.Sin(a) * 0.86f, -MathF.Cos(a) * 0.86f, 0.06f);
                    }
                    break;
                case TalentGlyph.Wave:
                    g.Add(Arc(-0.1f, 0.05f, 0.62f, 200f, 90f, 0.22f));
                    g.Add(Arc(0.2f, -0.22f, 0.25f, 300f, 160f, 0.14f));
                    g.Add(Wave(-0.95f, 0.95f, 0.72f, 0.08f, 2f, 0.12f));
                    break;
                case TalentGlyph.Leaf:
                    g.Add(Lens(-0.7f, 0.7f, 0.7f, -0.7f, 0.42f));
                    g.Cut(Line(-0.55f, 0.55f, 0.55f, -0.55f, 0.06f));
                    for (int i = 0; i < 3; i++)
                    {
                        float t = -0.3f + i * 0.3f;
                        g.Cut(Line(t, -t, t + 0.25f, -t + 0.12f, 0.05f)).Cut(Line(t, -t, t - 0.12f, -t - 0.25f, 0.05f));
                    }
                    g.Add(Line(-0.7f, 0.7f, -0.92f, 0.92f, 0.08f));
                    break;
                case TalentGlyph.Spiral:
                    g.Add(Arc(0f, 0f, 0.15f, 0f, 180f, 0.11f)).Add(Arc(0.08f, 0f, 0.3f, 180f, 360f, 0.11f)).Add(Arc(-0.03f, 0f, 0.48f, 0f, 180f, 0.11f));
                    g.Add(Arc(0.1f, 0f, 0.68f, 180f, 360f, 0.11f)).Add(Arc(-0.05f, 0f, 0.88f, 0f, 150f, 0.11f));
                    break;
                case TalentGlyph.Wing:
                    g.Add(Polygon(-0.85f, 0.6f, -0.6f, -0.2f, -0.1f, -0.75f, 0.55f, -0.9f, 0.9f, -0.75f, 0.45f, -0.45f, 0.75f, -0.3f, 0.3f, -0.05f, 0.55f, 0.15f, 0.05f, 0.25f, 0.2f, 0.5f, -0.35f, 0.55f));
                    break;
                case TalentGlyph.Dice:
                    g.Add(Box(-0.32f, 0.2f, 0.5f, 0.5f, 0.12f, -12f)).Cut(Circle(-0.32f, 0.2f, 0.11f)).Cut(Circle(-0.55f, 0f, 0.1f)).Cut(Circle(-0.1f, 0.42f, 0.1f));
                    g.Add(Box(0.45f, -0.4f, 0.4f, 0.4f, 0.1f, 20f)).Cut(Circle(0.32f, -0.52f, 0.09f)).Cut(Circle(0.58f, -0.28f, 0.09f));
                    break;
                case TalentGlyph.Scythe:
                    g.Add(Line(0.35f, -0.85f, -0.35f, 0.95f, 0.11f));
                    g.Add(Arc(-0.3f, -0.5f, 0.68f, 30f, 120f, 0.18f));
                    g.Add(Polygon(0.3f, -0.85f, 0.42f, -0.62f, -0.95f, -0.35f));
                    break;
                case TalentGlyph.Star:
                    g.Add(Star(0f, 0f, 5, 0.8f, 0.34f));
                    g.Add(Arc(0f, 0f, 0.92f, 20f, 70f, 0.07f)).Add(Arc(0f, 0f, 0.92f, 160f, 210f, 0.07f)).Add(Arc(0f, 0f, 0.92f, 290f, 330f, 0.07f));
                    break;
                case TalentGlyph.Hand:
                    g.Add(Box(0f, 0.35f, 0.45f, 0.45f, 0.18f));
                    for (int i = 0; i < 4; i++) g.Add(Line(-0.33f + i * 0.22f, 0.05f, -0.33f + i * 0.22f, -0.65f + MathF.Abs(i - 1.5f) * 0.12f, 0.17f));
                    g.Add(Line(-0.42f, 0.4f, -0.75f, 0.0f, 0.18f));
                    break;
                case TalentGlyph.Crack:
                    g.Add(Circle(0f, 0f, 0.88f));
                    g.Cut(Path(0.1f, 0.05f, -0.95f, -0.1f, -0.4f, 0.15f, -0.1f, -0.08f, 0.3f, 0.1f, 0.95f));
                    g.Cut(Path(0.07f, 0.15f, -0.1f, 0.55f, -0.2f, 0.85f, 0f)).Cut(Path(0.07f, -0.08f, 0.3f, -0.55f, 0.4f));
                    break;
                case TalentGlyph.Quake:
                    g.Add(Polygon(-0.95f, 0.3f, -0.15f, 0.3f, -0.05f, 0.5f, 0.1f, 0.35f, 0.95f, 0.35f, 0.95f, 0.9f, -0.95f, 0.9f));
                    g.Cut(Path(0.08f, -0.05f, 0.4f, -0.12f, 0.6f, 0.05f, 0.7f, -0.05f, 0.9f));
                    g.Add(Arc(0f, 0.3f, 0.45f, 290f, 70f, 0.09f)).Add(Arc(0f, 0.3f, 0.75f, 295f, 65f, 0.09f));
                    break;
                case TalentGlyph.Book:
                    g.Add(Box(0.05f, 0f, 0.62f, 0.85f, 0.08f)).Cut(Box(0.15f, 0f, 0.44f, 0.7f, 0.04f)).Add(Box(-0.6f, 0f, 0.1f, 0.85f, 0.04f));
                    g.Add(Polygon(0.25f, -0.85f, 0.45f, -0.85f, 0.45f, 0.95f, 0.35f, 0.82f, 0.25f, 0.95f));
                    g.Add(Star(0.15f, -0.05f, 4, 0.3f, 0.08f));
                    break;
                case TalentGlyph.Anvil:
                    g.Add(Polygon(-0.95f, -0.45f, 0.35f, -0.45f, 0.85f, -0.6f, 0.75f, -0.2f, 0.35f, -0.05f, 0.2f, 0.3f, 0.45f, 0.62f, -0.55f, 0.62f, -0.3f, 0.3f, -0.4f, -0.05f, -0.75f, -0.15f));
                    g.Add(Box(0f, 0.75f, 0.62f, 0.1f, 0.03f));
                    break;
                case TalentGlyph.Mirror:
                    g.Add(Circle(0.1f, -0.25f, 0.62f)).Cut(Circle(0.1f, -0.25f, 0.46f));
                    g.Add(Line(-0.22f, 0.25f, -0.62f, 0.9f, 0.16f));
                    g.Add(Line(-0.1f, -0.1f, 0.35f, -0.5f, 0.08f));
                    break;
                case TalentGlyph.Roots:
                    g.Add(Box(0f, -0.55f, 0.16f, 0.42f)).Add(Line(-0.9f, -0.12f, 0.9f, -0.12f, 0.07f));
                    g.Add(Path(0.11f, 0f, -0.15f, -0.25f, 0.25f, -0.62f, 0.45f, -0.85f, 0.85f)).Add(Path(0.11f, 0f, -0.15f, 0.25f, 0.3f, 0.55f, 0.45f, 0.8f, 0.85f));
                    g.Add(Path(0.09f, 0f, -0.15f, 0f, 0.5f, -0.1f, 0.9f)).Add(Path(0.07f, -0.25f, 0.25f, -0.15f, 0.62f)).Add(Path(0.07f, 0.3f, 0.3f, 0.2f, 0.65f));
                    break;
                case TalentGlyph.Aegis:
                    g.Add(ShieldShape(-0.25f, 0.05f, 0.8f)).Cut(ShieldShape(0.3f, -0.05f, 0.92f));
                    g.Add(ShieldShape(0.3f, -0.05f, 0.8f));
                    g.Cut(Line(0.3f, -0.55f, 0.3f, 0.55f, 0.1f));
                    break;
                case TalentGlyph.Fang:
                    g.Add(Polygon(-0.6f, -0.85f, 0.6f, -0.85f, 0.3f, -0.05f, 0.05f, 0.5f, -0.2f, -0.1f));
                    Drop(g, 0.25f, 0.65f, 0.28f);
                    g.Add(Box(0f, -0.88f, 0.75f, 0.08f, 0.04f));
                    break;
                case TalentGlyph.Coin:
                    g.Add(Circle(0f, 0f, 0.85f)).Cut(Ring(0f, 0f, 0.65f, 0.08f));
                    g.Cut(Path(0.09f, 0.05f, -0.95f, -0.1f, -0.3f, 0.1f, 0.1f, -0.05f, 0.95f));
                    break;
                case TalentGlyph.Chalice:
                    g.Add(Polygon(-0.65f, -0.55f, 0.65f, -0.55f, 0.5f, -0.05f, 0.12f, 0.15f, 0.12f, 0.55f, 0.45f, 0.75f, -0.45f, 0.75f, -0.12f, 0.55f, -0.12f, 0.15f, -0.5f, -0.05f));
                    Drop(g, -0.62f, -0.15f, 0.2f);
                    Drop(g, 0.7f, 0.05f, 0.18f);
                    g.Add(Wave(-0.6f, 0.6f, -0.72f, 0.06f, 1.5f, 0.1f));
                    break;
                case TalentGlyph.Ember:
                    g.Add(Polygon(-0.75f, 0.55f, -0.35f, 0.1f, 0.3f, 0.05f, 0.8f, 0.5f, 0.35f, 0.88f, -0.4f, 0.9f));
                    g.Cut(Path(0.07f, -0.35f, 0.45f, 0f, 0.6f, 0.35f, 0.45f));
                    Flame(g, 0f, -0.42f, 0.5f);
                    break;
                case TalentGlyph.Sparks:
                    float[] at = { -0.6f, -0.5f, 0.2f, -0.75f, 0.65f, -0.3f, -0.2f, 0f, 0.4f, 0.3f, -0.65f, 0.45f, 0.05f, 0.75f, 0.75f, 0.7f };
                    for (int i = 0; i < at.Length; i += 2) g.Add(Star(at[i], at[i + 1], 4, 0.2f + (i % 3) * 0.05f, 0.06f));
                    break;
                case TalentGlyph.Phoenix:
                    g.Add(Polygon(0f, -0.65f, 0.15f, -0.4f, 0.95f, -0.75f, 0.55f, -0.1f, 0.25f, 0.05f, 0.15f, 0.55f, 0.35f, 0.95f, 0f, 0.7f, -0.35f, 0.95f, -0.15f, 0.55f, -0.25f, 0.05f, -0.55f, -0.1f, -0.95f, -0.75f, -0.15f, -0.4f));
                    g.Add(Circle(0f, -0.78f, 0.13f));
                    break;
                case TalentGlyph.Scorch:
                    g.Add(Lens(-0.35f, -0.85f, -0.35f, 0.25f, 0.28f)).Add(Lens(0.3f, -0.2f, 0.3f, 0.85f, 0.26f));
                    Flame(g, -0.35f, -0.45f, 0.22f);
                    Flame(g, 0.3f, 0.3f, 0.2f);
                    g.Cut(Circle(-0.35f, -0.3f, 0.1f)).Cut(Circle(0.3f, 0.4f, 0.1f));
                    break;
                case TalentGlyph.Spellblade:
                    Blade(g, 0f, 0.05f, 1f, 0f);
                    g.Cut(Star(0f, -0.35f, 4, 0.22f, 0.07f));
                    g.Add(Star(-0.55f, -0.55f, 4, 0.28f, 0.07f)).Add(Star(0.55f, -0.2f, 4, 0.2f, 0.06f));
                    break;
                case TalentGlyph.Puppet:
                    g.Add(Line(-0.7f, -0.82f, 0.7f, -0.82f, 0.1f)).Add(Line(0f, -0.95f, 0f, -0.7f, 0.08f));
                    Figure(g, 0f, 0.35f, 0.55f);
                    g.Add(Line(-0.6f, -0.82f, -0.32f, 0.38f, 0.04f)).Add(Line(0.6f, -0.82f, 0.32f, 0.38f, 0.04f)).Add(Line(0f, -0.82f, 0f, 0.0f, 0.04f));
                    break;
                case TalentGlyph.Crown:
                    g.Add(Polygon(-0.85f, -0.5f, -0.45f, -0.05f, 0f, -0.75f, 0.45f, -0.05f, 0.85f, -0.5f, 0.75f, 0.55f, -0.75f, 0.55f));
                    g.Add(Box(0f, 0.7f, 0.78f, 0.1f, 0.03f));
                    g.Add(Circle(-0.85f, -0.55f, 0.1f)).Add(Circle(0f, -0.8f, 0.1f)).Add(Circle(0.85f, -0.55f, 0.1f));
                    g.Cut(Regular(0f, 0.25f, 4, 0.13f));
                    break;
                case TalentGlyph.Banner:
                    g.Add(Line(-0.6f, -0.9f, -0.6f, 0.95f, 0.09f));
                    g.Add(Polygon(-0.55f, -0.85f, 0.75f, -0.75f, 0.55f, -0.45f, 0.85f, -0.1f, 0.3f, -0.05f, 0.1f, 0.15f, -0.1f, -0.05f, -0.55f, 0f));
                    break;
                case TalentGlyph.Lantern:
                    g.Add(Box(0f, 0.12f, 0.45f, 0.55f, 0.12f)).Cut(Box(0f, 0.12f, 0.28f, 0.4f, 0.08f));
                    g.Add(Polygon(-0.55f, -0.42f, 0.55f, -0.42f, 0.25f, -0.65f, -0.25f, -0.65f)).Add(Ring(0f, -0.78f, 0.15f, 0.07f));
                    Flame(g, 0f, 0.15f, 0.25f);
                    g.Add(Box(0f, 0.72f, 0.55f, 0.07f, 0.03f));
                    break;
                case TalentGlyph.Crystal:
                    g.Add(Polygon(0f, -0.95f, 0.42f, -0.45f, 0.42f, 0.45f, 0f, 0.95f, -0.42f, 0.45f, -0.42f, -0.45f));
                    g.Cut(Line(0f, -0.62f, 0f, 0.62f, 0.06f)).Cut(Line(-0.25f, -0.35f, 0.25f, -0.35f, 0.05f));
                    g.Add(Polygon(0.62f, -0.2f, 0.8f, 0.05f, 0.62f, 0.45f, 0.48f, 0.05f)).Add(Polygon(-0.62f, -0.1f, -0.75f, 0.12f, -0.62f, 0.4f, -0.52f, 0.12f));
                    break;
                case TalentGlyph.Glacier:
                    g.Add(Polygon(-0.95f, 0.5f, -0.55f, -0.35f, -0.25f, -0.05f, 0.1f, -0.75f, 0.5f, -0.15f, 0.7f, -0.35f, 0.95f, 0.5f));
                    g.Cut(Path(0.07f, 0.1f, -0.45f, -0.05f, -0.2f, 0.15f, -0.05f));
                    g.Add(Wave(-0.95f, 0.95f, 0.72f, 0.06f, 2f, 0.1f));
                    break;
                case TalentGlyph.Pool:
                    g.Add(Ring(0f, 0.45f, 0.35f, 0.08f)).Add(Ring(0f, 0.45f, 0.62f, 0.07f)).Add(Ring(0f, 0.45f, 0.88f, 0.06f));
                    g.Cut(Box(0f, 1.2f, 1f, 0.55f));
                    Drop(g, 0f, -0.42f, 0.42f);
                    break;
                case TalentGlyph.Sundial:
                    g.Add(Ring(0f, 0.2f, 0.75f, 0.1f));
                    g.Add(Polygon(-0.05f, 0.2f, 0.05f, 0.2f, 0.05f, -0.75f));
                    g.Add(Polygon(0f, 0.2f, 0.55f, 0.55f, 0.6f, 0.45f));
                    for (int i = 0; i < 6; i++)
                    {
                        float a = OpenTK.Mathematics.MathHelper.DegreesToRadians(-90f + i * 36f);
                        g.Add(Line(MathF.Sin(a) * 0.55f, 0.2f - MathF.Cos(a) * 0.55f, MathF.Sin(a) * 0.68f, 0.2f - MathF.Cos(a) * 0.68f, 0.06f));
                    }
                    break;
                case TalentGlyph.Scales:
                    g.Add(Line(0f, -0.75f, 0f, 0.7f, 0.1f)).Add(Line(-0.8f, -0.55f, 0.8f, -0.55f, 0.09f)).Add(Box(0f, 0.78f, 0.45f, 0.08f, 0.03f));
                    g.Add(Line(-0.8f, -0.55f, -0.95f, 0.05f, 0.04f)).Add(Line(-0.8f, -0.55f, -0.65f, 0.05f, 0.04f)).Add(Arc(-0.8f, 0.0f, 0.2f, 90f, 270f, 0.08f));
                    g.Add(Line(0.8f, -0.55f, 0.65f, 0.05f, 0.04f)).Add(Line(0.8f, -0.55f, 0.95f, 0.05f, 0.04f)).Add(Arc(0.8f, 0.0f, 0.2f, 90f, 270f, 0.08f));
                    g.Add(Circle(0f, -0.82f, 0.1f));
                    break;
                case TalentGlyph.Key:
                    g.Add(Ring(-0.45f, -0.45f, 0.32f, 0.14f));
                    g.Add(Line(-0.22f, -0.22f, 0.75f, 0.75f, 0.14f));
                    g.Add(Line(0.42f, 0.42f, 0.62f, 0.22f, 0.12f)).Add(Line(0.62f, 0.62f, 0.82f, 0.42f, 0.12f));
                    break;
                default:
                    g.Add(Circle(0f, 0f, 0.6f));
                    break;
            }
        }
    }
}
