using OpenTK.Mathematics;

namespace Dino_Engine.GUI.Rendering
{
    /// <summary>
    /// A small picture made of shapes, for drawing crisply at any size: what's stored is how far each texel is from the edge (a signed distance field), and
    /// the interface's shader turns that back into a sharp edge wherever it's drawn, or a soft glow around it (<see cref="GuiDrawList.AddField"/>).
    /// Shapes are laid down in order, each either added or cut away, in a square from -1 to 1 with y running down, so a picture reads like a drawing.
    /// It touches no GPU and nothing shared, so pictures can be made on any thread.
    /// </summary>
    public sealed class SdfPicture
    {
        private readonly List<(Func<Vector2, float> distance, bool cut)> _shapes = new();

        public SdfPicture Add(Func<Vector2, float> distance)
        {
            _shapes.Add((distance, false));
            return this;
        }

        public SdfPicture Cut(Func<Vector2, float> distance)
        {
            _shapes.Add((distance, true));
            return this;
        }

        /// <summary>How far a point is outside the picture, negative inside, in its own units.</summary>
        public float Distance(Vector2 p)
        {
            float d = float.PositiveInfinity;
            foreach ((Func<Vector2, float> distance, bool cut) in _shapes) d = cut ? MathF.Max(d, -distance(p)) : MathF.Min(d, distance(p));
            return d;
        }

        /// <summary>
        /// Writes the picture into one square cell of a single channel atlas: 128 at the edge, more inside, less outside, reaching nothing and everything
        /// <paramref name="spread"/> texels either side. The picture's square fills <paramref name="fill"/> of the cell, leaving room for the spread.
        /// </summary>
        public void Write(byte[] atlas, int atlasWidth, int x0, int y0, int size, float spread, float fill = 0.86f)
        {
            float half = size * 0.5f;
            float unitsPerTexel = 1f / (half * fill);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f - half) * unitsPerTexel, (y + 0.5f - half) * unitsPerTexel);
                    float texels = Distance(p) / unitsPerTexel;
                    float value = 0.5f - texels / (2f * spread);
                    atlas[(y0 + y) * atlasWidth + x0 + x] = (byte)(Math.Clamp(value, 0f, 1f) * 255f + 0.5f);
                }
            }
        }

        // ------------------------------------------------------------ shapes, as distances

        public static Func<Vector2, float> Circle(float x, float y, float r) => p => (p - new Vector2(x, y)).Length - r;

        public static Func<Vector2, float> Ring(float x, float y, float r, float width) => p => MathF.Abs((p - new Vector2(x, y)).Length - r) - width * 0.5f;

        /// <summary>A stroke from one point to another, round at both ends.</summary>
        public static Func<Vector2, float> Line(float x1, float y1, float x2, float y2, float width)
        {
            var a = new Vector2(x1, y1);
            var b = new Vector2(x2, y2);
            return p => Segment(p, a, b) - width * 0.5f;
        }

        /// <summary>A stroke along a circle, from one angle to another clockwise, in degrees with 0 straight up, round at both ends.</summary>
        public static Func<Vector2, float> Arc(float x, float y, float r, float from, float to, float width)
        {
            var centre = new Vector2(x, y);
            float a0 = MathHelper.DegreesToRadians(from);
            float sweep = MathHelper.DegreesToRadians(((to - from) % 360f + 360f) % 360f);
            if (sweep <= 0f) sweep = MathF.Tau;
            Vector2 At(float a) => centre + new Vector2(MathF.Sin(a), -MathF.Cos(a)) * r;
            Vector2 start = At(a0);
            Vector2 end = At(a0 + sweep);
            return p =>
            {
                Vector2 d = p - centre;
                float angle = MathF.Atan2(d.X, -d.Y);
                float along = ((angle - a0) % MathF.Tau + MathF.Tau) % MathF.Tau;
                float distance = along <= sweep ? MathF.Abs(d.Length - r) : MathF.Min((p - start).Length, (p - end).Length);
                return distance - width * 0.5f;
            };
        }

        /// <summary>A box, turned by <paramref name="degrees"/>, with rounded corners.</summary>
        public static Func<Vector2, float> Box(float x, float y, float halfWidth, float halfHeight, float corner = 0f, float degrees = 0f)
        {
            var centre = new Vector2(x, y);
            float a = MathHelper.DegreesToRadians(degrees);
            float c = MathF.Cos(a), s = MathF.Sin(a);
            return p =>
            {
                Vector2 d = p - centre;
                var local = new Vector2(d.X * c + d.Y * s, -d.X * s + d.Y * c);
                var q = new Vector2(MathF.Abs(local.X) - halfWidth + corner, MathF.Abs(local.Y) - halfHeight + corner);
                return MathF.Min(MathF.Max(q.X, q.Y), 0f) + new Vector2(MathF.Max(q.X, 0f), MathF.Max(q.Y, 0f)).Length - corner;
            };
        }

        /// <summary>A filled polygon through the given x, y pairs, in either winding, with the exact distance to its edge.</summary>
        public static Func<Vector2, float> Polygon(params float[] xy)
        {
            var points = new Vector2[xy.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return p =>
            {
                float d = float.PositiveInfinity;
                bool inside = false;
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                {
                    Vector2 a = points[i], b = points[j];
                    d = MathF.Min(d, Segment(p, a, b));
                    if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
                }
                return inside ? -d : d;
            };
        }

        /// <summary>A star of <paramref name="points"/> points, the first straight up.</summary>
        public static Func<Vector2, float> Star(float x, float y, int points, float outer, float inner, float turn = 0f)
        {
            var xy = new float[points * 4];
            for (int i = 0; i < points * 2; i++)
            {
                float a = MathHelper.DegreesToRadians(turn + 180f * i / points);
                float r = i % 2 == 0 ? outer : inner;
                xy[i * 2] = x + MathF.Sin(a) * r;
                xy[i * 2 + 1] = y - MathF.Cos(a) * r;
            }
            return Polygon(xy);
        }

        /// <summary>A regular polygon of <paramref name="sides"/> sides, a corner straight up.</summary>
        public static Func<Vector2, float> Regular(float x, float y, int sides, float r, float turn = 0f)
        {
            var xy = new float[sides * 2];
            for (int i = 0; i < sides; i++)
            {
                float a = MathHelper.DegreesToRadians(turn + 360f * i / sides);
                xy[i * 2] = x + MathF.Sin(a) * r;
                xy[i * 2 + 1] = y - MathF.Cos(a) * r;
            }
            return Polygon(xy);
        }

        /// <summary>A pointed oval between two points, as a leaf or an eye is: the overlap of two circles.</summary>
        public static Func<Vector2, float> Lens(float x1, float y1, float x2, float y2, float bulge)
        {
            var a = new Vector2(x1, y1);
            var b = new Vector2(x2, y2);
            Vector2 middle = (a + b) * 0.5f;
            float half = (b - a).Length * 0.5f;
            Vector2 across = new Vector2(-(b - a).Y, (b - a).X).Normalized();
            // Two circles through both ends, each centred off the line on the far side, so their overlap is as wide as asked.
            float radius = (half * half + bulge * bulge) / (2f * bulge);
            float offset = radius - bulge;
            Vector2 c1 = middle + across * offset;
            Vector2 c2 = middle - across * offset;
            return p => MathF.Max((p - c1).Length - radius, (p - c2).Length - radius);
        }

        /// <summary>A stroke through a run of points, as a polyline with round joins.</summary>
        public static Func<Vector2, float> Path(float width, params float[] xy)
        {
            var points = new Vector2[xy.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return p =>
            {
                float d = float.PositiveInfinity;
                for (int i = 1; i < points.Length; i++) d = MathF.Min(d, Segment(p, points[i - 1], points[i]));
                return d - width * 0.5f;
            };
        }

        /// <summary>A stroke along a wave, from one x to another.</summary>
        public static Func<Vector2, float> Wave(float x1, float x2, float y, float amplitude, float waves, float width)
        {
            const int Steps = 40;
            var xy = new float[(Steps + 1) * 2];
            for (int i = 0; i <= Steps; i++)
            {
                float t = i / (float)Steps;
                xy[i * 2] = x1 + (x2 - x1) * t;
                xy[i * 2 + 1] = y + MathF.Sin(t * waves * MathF.Tau) * amplitude;
            }
            return Path(width, xy);
        }

        public static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.LengthSquared > 1e-9f ? Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared, 0f, 1f) : 0f;
            return (p - (a + ab * t)).Length;
        }
    }
}
