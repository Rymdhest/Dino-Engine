using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Waygates
{
    /// <summary>
    /// The script the old roads are cut in, which nobody can read: every letter a ring broken somewhere, with something small inside it or beside it, all in rounded strokes.
    /// It's made of arcs, dots and at most one short stroke out from the ring, so it can't come out as a zigzag, a cross, an arrow or anything turned about on itself, which
    /// is what keeps any letter from looking like a real rune or a mark anyone has used (LORE.md). Each glyph is a few strokes in a square a unit across, centred on 0.
    /// </summary>
    public static class Glyphs
    {
        /// <summary>One stroke: a polyline of points, drawn <see cref="Width"/> wide.</summary>
        public readonly record struct Stroke(Vector2[] Points, float Width);

        /// <summary>How wide a stroke is, as a share of the glyph: a glyph is a few dozen pixels at most from the game's camera, and a stroke has to be two or three of them.</summary>
        public const float StrokeWidth = 0.15f;

        private const float ArcStep = 0.42f;    // Radians at most along an arc between two of its points.

        public static IReadOnlyList<Stroke> Of(int glyph)
        {
            var genes = new Genes((ulong)(uint)glyph * 0x9E3779B97F4A7C15UL + 0x5A17UL);
            var strokes = new List<Stroke>();

            // The ring, broken by a gap facing one of eight ways.
            float radius = genes.Range("ring", 0.3f, 0.4f);
            float gap = genes.Range("gap", 0.6f, 1.9f);
            float facing = genes.Count("facing", 0, 7) * MathF.PI * 0.25f;
            strokes.Add(Arc(Vector2.Zero, radius, facing + gap * 0.5f, facing + MathF.Tau - gap * 0.5f, StrokeWidth));

            // Something in the middle: a dot, a smaller arc, or a short stroke in from the gap.
            switch (genes.Pick("inner", 0.4f, 0.35f, 0.25f))
            {
                case 0:
                    strokes.Add(Dot(Vector2.Zero, StrokeWidth * 0.75f));
                    break;
                case 1:
                {
                    float inner = genes.Range("inner ring", 0.12f, 0.17f);
                    float turn = facing + MathF.PI + genes.Range("inner turn", -0.6f, 0.6f);
                    float span = genes.Range("inner span", 1.6f, 3.2f);
                    strokes.Add(Arc(Vector2.Zero, inner, turn - span * 0.5f, turn + span * 0.5f, StrokeWidth * 0.85f));
                    break;
                }
                default:
                {
                    Vector2 way = new Vector2(MathF.Cos(facing), MathF.Sin(facing));
                    strokes.Add(new Stroke(new[] { way * (radius + 0.04f), way * genes.Range("tick", 0.02f, 0.12f) }, StrokeWidth * 0.9f));
                    break;
                }
            }

            // And sometimes a dot or two outside it, by the gap.
            int dots = genes.Pick("dots", 0.5f, 0.35f, 0.15f);
            for (int i = 0; i < dots; i++)
            {
                float angle = facing + (dots == 1 ? 0f : (i - 0.5f) * 0.55f);
                strokes.Add(Dot(new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (radius + 0.12f), StrokeWidth * 0.55f));
            }
            return strokes;
        }

        private static Stroke Arc(Vector2 centre, float radius, float from, float to, float width)
        {
            int steps = Math.Max(2, (int)MathF.Ceiling(MathF.Abs(to - from) / ArcStep));
            var points = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float angle = from + (to - from) * i / steps;
                points[i] = centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            }
            return new Stroke(points, width);
        }

        // A dot is a stroke as long as it is wide.
        private static Stroke Dot(Vector2 at, float size) => new Stroke(new[] { at - new Vector2(size * 0.5f, 0f), at + new Vector2(size * 0.5f, 0f) }, size);
    }
}
