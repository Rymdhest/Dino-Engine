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

        /// <summary>A round with ridges running along it, as fluted work has: <paramref name="flutes"/> of them, cut in by <paramref name="depth"/> of the radius between.</summary>
        public static Section Fluted(int points, int flutes, float depth)
        {
            var outline = new Vector2[points];
            for (int i = 0; i < points; i++)
            {
                float angle = i * (MathF.Tau / points);
                float r = 1f - depth * (0.5f - 0.5f * MathF.Cos(angle * flutes));
                outline[i] = new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);
            }
            return new Section(outline, new bool[points]);
        }

        /// <summary>
        /// A bow's limb across: flat on its back (+y), towards whatever it's shot at, and round on its belly, towards the archer, as a longbow's D is. Its two corners are creases, so that a
        /// composite's back of sinew and belly of horn meet in a line (<see cref="IsLimbBack"/>).
        /// </summary>
        public static Section LimbD()
        {
            var outline = new List<Vector2> { new Vector2(1f, 0.55f), new Vector2(0.6f, 1f), new Vector2(0f, 1f), new Vector2(-0.6f, 1f), new Vector2(-1f, 0.55f) };
            for (int k = 1; k <= 7; k++)
            {
                float angle = MathF.PI + k * MathF.PI / 8f;
                outline.Add(new Vector2(MathF.Cos(angle), 0.55f + 1.55f * MathF.Sin(angle)));
            }
            var hard = new bool[outline.Count];
            hard[0] = hard[4] = true;
            return new Section(outline.ToArray(), hard);
        }

        /// <summary>Whether a point of a <see cref="LimbD"/>, on the face before or after its crease, is on the limb's flat back.</summary>
        public static bool IsLimbBack(int point, bool after) => point is 1 or 2 or 3 || point == 0 && after || point == 4 && !after;

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
        /// A strap across its width: flat underneath, where it lies on something, with its top corners cut off by <paramref name="bevel"/> of its half width. What lies on a surface never shows
        /// its underside, so it has no corners there to pay for.
        /// </summary>
        public static Section Strap(float bevel)
        {
            float b = 1f - Math.Clamp(bevel, 0.02f, 0.9f);
            var outline = new[] { new Vector2(1f, -1f), new Vector2(1f, b), new Vector2(b, 1f), new Vector2(-b, 1f), new Vector2(-1f, b), new Vector2(-1f, -1f) };
            var hard = new bool[6];
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

        /// <summary>
        /// A blade across its width, as the finest are ground: a narrow flat at each edge, a polished bevel up from it (<paramref name="band"/> of the half width), the main bevel up to a flat or a
        /// ridge, and a fuller sunk into the flat. A single edge's back is a spine, as thick as <paramref name="spine"/> of the blade, and a blunt one's edge is as thick as <paramref name="edge"/>.
        /// Always the same twenty points in the same order, counter-clockwise from the front edge, so that a blade can change its section all the way along: see <see cref="BladeHard"/> for which are creases.
        /// </summary>
        public static Vector2[] BladeSection(float flat, float groove, float depth, float band, float spine, float edge)
        {
            float e = Math.Clamp(edge, 0.03f, 0.95f);
            float s = Math.Clamp(spine, 0.03f, 0.95f);
            float b = Math.Clamp(band, 0f, 0.6f);
            // Even a diamond keeps a hair of a flat, and the fuller's points a little apart, or a blade's point folds its faces flat onto each other.
            float f = Math.Clamp(MathF.Min(flat, 0.95f - b), 0.08f, 0.9f);
            float g = Math.Clamp(Math.Clamp(groove, 0.12f, 0.9f) * f, 0.05f, f - 0.02f);
            float low = 1f - Math.Clamp(depth, 0f, 0.9f);
            float bevel = MathF.Max(1f - b, f + 0.03f);
            float rise = e + (1f - e) * 0.42f;
            bool spined = s > 0.3f;
            float backFlat = spined ? MathF.Max(f, 0.9f) : f;
            float backBevel = spined ? MathF.Max(0.96f, backFlat + 0.02f) : bevel;
            float backRise = spined ? s + (1f - s) * 0.5f : rise;
            return new[]
            {
                new Vector2(1f, -e), new Vector2(1f, e), new Vector2(bevel, rise), new Vector2(f, 1f), new Vector2(g, 1f), new Vector2(g * 0.3f, low), new Vector2(-g * 0.3f, low),
                new Vector2(-g, 1f), new Vector2(-backFlat, 1f), new Vector2(-backBevel, backRise), new Vector2(-1f, s), new Vector2(-1f, -s), new Vector2(-backBevel, -backRise),
                new Vector2(-backFlat, -1f), new Vector2(-g, -1f), new Vector2(-g * 0.3f, -low), new Vector2(g * 0.3f, -low), new Vector2(g, -1f), new Vector2(f, -1f), new Vector2(bevel, -rise),
            };
        }

        /// <summary>The creases of a <see cref="BladeSection"/>: its edges and every line where one bevel meets another. The fuller is left to shade round.</summary>
        public static Section BladeHard() => new Section(BladeSection(0.5f, 0.5f, 0.3f, 0.2f, 0.04f, 0.04f),
            new[] { true, true, true, true, false, false, false, false, true, true, true, true, true, true, false, false, false, false, true, true });

        /// <summary>Whether a point of a <see cref="BladeSection"/>, on the face before or after its crease, is on the front edge's polished bevel.</summary>
        public static bool IsFrontBand(int point, bool after) => point is 0 or 1 || point == 2 && !after || point == 19 && after;

        /// <summary>The same for the back edge, which only a blade with two edges has.</summary>
        public static bool IsBackBand(int point, bool after) => point is 10 or 11 || point == 9 && after || point == 12 && !after;

        /// <summary>Whether a point of a <see cref="BladeSection"/> is down in its fuller.</summary>
        public static bool IsFuller(int point) => point is 5 or 6 or 15 or 16;

        /// <summary>What's hard on a <see cref="BladeOutline"/>: the edges, and where a bevel meets the flat. The groove is left to shade soft.</summary>
        public static Section Blade() => new Section(BladeOutline(0.5f, 0.5f, 0.3f), new[] { true, true, false, false, false, true, true, true, false, false, false, true });

        /// <summary>Which points of a <see cref="BladeOutline"/> are its edges.</summary>
        public static bool IsBladeEdge(int point) => point == 0 || point == 6;

        /// <summary>Which points of a <see cref="BladeOutline"/> are down its groove.</summary>
        public static bool IsBladeGroove(int point) => point == 3 || point == 9;
    }
}
