using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// The shape across something: a closed outline, counter-clockwise, within -1 to 1 each way. A hard corner stays a crease where it's lofted, and anything else shades round.
    /// </summary>
    public sealed class Section
    {
        public readonly Vector2[] Points;
        public readonly bool[] Hard;

        /// <summary>
        /// For something thin and bowed, whose outline isn't convex: its ends are closed straight across, from each point to the one facing it, and not from the middle, which can
        /// lie outside it. The points have to run along one face and back along the other.
        /// </summary>
        public bool ClosesAcross;

        public Section(Vector2[] points, bool[] hard)
        {
            Points = points;
            Hard = hard;
        }

        public int Count => Points.Length;

        public static Section Round(int sides) => Super(sides, 2f);

        /// <summary>A superellipse. At 2 it's a circle, above that a rounded box, and at 1 a diamond. Below that its sides draw in towards a star.</summary>
        public static Section Super(int points, float power)
        {
            var outline = new Vector2[points];
            for (int i = 0; i < points; i++)
            {
                float angle = i * (MathF.Tau / points);
                float x = MathF.Cos(angle);
                float y = MathF.Sin(angle);
                outline[i] = new Vector2(MathF.Sign(x) * MathF.Pow(MathF.Abs(x), 2f / power), MathF.Sign(y) * MathF.Pow(MathF.Abs(y), 2f / power));
            }
            return new Section(outline, new bool[points]);
        }

        /// <summary>A shape with flat sides and sharp corners.</summary>
        public static Section Polygon(int sides, float turn = 0f)
        {
            var outline = new Vector2[sides];
            var hard = new bool[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = turn + i * (MathF.Tau / sides);
                outline[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                hard[i] = true;
            }
            return new Section(outline, hard);
        }

        /// <summary>A rectangle with its corners cut off, by <paramref name="bevel"/> of its half width.</summary>
        public static Section Slab(float bevel)
        {
            float b = 1f - Math.Clamp(bevel, 0.02f, 0.9f);
            var outline = new[]
            {
                new Vector2(1f, -b), new Vector2(1f, b), new Vector2(b, 1f), new Vector2(-b, 1f),
                new Vector2(-1f, b), new Vector2(-1f, -b), new Vector2(-b, -1f), new Vector2(b, -1f),
            };
            var hard = new bool[8];
            Array.Fill(hard, true);
            return new Section(outline, hard);
        }

        /// <summary>
        /// A blade across its width, with an edge either side, a flat that the bevels come in from, and a groove down the middle of each face. All of these can change along a blade, so this
        /// is an outline and not a <see cref="Section"/>, and always has the same points in the same order.
        /// </summary>
        /// <param name="flat">How much of the half width is the flat, from 0 for a diamond up.</param>
        /// <param name="groove">How much of the half width the groove takes.</param>
        /// <param name="depth">How deep the groove goes, as a share of the thickness.</param>
        public static Vector2[] BladeOutline(float flat, float groove, float depth)
        {
            float f = Math.Clamp(flat, 0.02f, 0.92f);
            float g = Math.Clamp(groove, 0.01f, 0.9f) * f;
            float low = 1f - Math.Clamp(depth, 0f, 0.92f);
            return new[]
            {
                new Vector2(1f, 0f), new Vector2(f, 1f), new Vector2(g, 1f), new Vector2(0f, low), new Vector2(-g, 1f), new Vector2(-f, 1f),
                new Vector2(-1f, 0f), new Vector2(-f, -1f), new Vector2(-g, -1f), new Vector2(0f, -low), new Vector2(g, -1f), new Vector2(f, -1f),
            };
        }

        /// <summary>What's hard on a <see cref="BladeOutline"/>: the edges, and where a bevel meets the flat. The groove is left to shade soft.</summary>
        public static Section Blade() => new Section(BladeOutline(0.5f, 0.5f, 0.3f), new[] { true, true, false, false, false, true, true, true, false, false, false, true });

        /// <summary>Which points of a <see cref="BladeOutline"/> are its edges.</summary>
        public static bool IsBladeEdge(int point) => point == 0 || point == 6;

        /// <summary>Which points of a <see cref="BladeOutline"/> are down its groove.</summary>
        public static bool IsBladeGroove(int point) => point == 3 || point == 9;
    }
}
