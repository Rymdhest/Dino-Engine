using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Chart.ChartInk;

namespace Dino_Engine.World.Chart
{
    /// <summary>
    /// Something drawn standing up on a chart: a tree, a mountain, a house. They're drawn from the top of the sheet down, by where each stands, so that whatever is nearer
    /// the bottom of the picture is in front, as in a drawing seen from the south and a little above, the way the game's camera sees the land.
    /// </summary>
    internal abstract class ChartGlyph
    {
        /// <summary>Where it stands, down the sheet: what it's sorted by.</summary>
        public float Order;
        public Vector2 Min, Max;

        public abstract void Draw(in Pen pen);

        protected static float Ellipse(Vector2 p, Vector2 centre, Vector2 radii)
        {
            Vector2 q = (p - centre) / radii;
            return (q.Length - 1f) * MathF.Min(radii.X, radii.Y);
        }
    }

    /// <summary>A tree in its own species' silhouette and colours, with a shadow cast down to the right.</summary>
    internal sealed class TreeGlyph : ChartGlyph
    {
        private readonly Vector2 _foot;
        private readonly float _w, _h, _ink;
        private readonly TreeLook _look;
        private readonly uint _seed;
        private readonly Vector3[] _lobes = Array.Empty<Vector3>();     // Centre and radius of each mass of a crown.
        private readonly Vector2 _centre;                               // Of the crown, for how it's lit.
        private readonly float _reach;                                  // Half the crown's size.
        private readonly Vector2[]? _outline;                           // For crowns that are one shape.

        public TreeGlyph(Vector2 foot, float width, TreeLook look, uint seed, float ink)
        {
            _foot = foot;
            _w = width;
            _h = width * look.Aspect;
            _look = look;
            _seed = seed;
            _ink = ink;
            Order = foot.Y;
            Min = new Vector2(foot.X - _w * 0.8f - 2f, foot.Y - _h - 2f);
            Max = new Vector2(foot.X + _w * 0.9f + 2f, foot.Y + _w * 0.25f + 2f);

            float r = _w * 0.5f;
            float trunk = _h * look.Trunk;
            float crown = _h - trunk;
            float J(int i) => (ChartNoise.Unit((int)seed, i, 77u) - 0.5f) * 2f;
            switch (look.Shape)
            {
                case TreeShape.Round:
                case TreeShape.Weeping:
                {
                    _centre = new Vector2(foot.X, foot.Y - trunk - crown * 0.5f);
                    float half = crown * 0.5f;
                    _reach = MathF.Max(r, half);
                    _lobes = new[]
                    {
                        new Vector3(_centre.X + J(0) * 0.06f * r, _centre.Y + 0.05f * half, 0.72f * r),
                        new Vector3(_centre.X - 0.42f * r, _centre.Y + 0.3f * half, (0.56f + J(1) * 0.06f) * r),
                        new Vector3(_centre.X + 0.43f * r, _centre.Y + 0.28f * half, (0.54f + J(2) * 0.06f) * r),
                        new Vector3(_centre.X + J(3) * 0.14f * r, _centre.Y - half + 0.6f * r, (0.6f + J(4) * 0.05f) * r),
                        new Vector3(_centre.X - 0.3f * r + J(5) * 0.08f * r, _centre.Y - 0.35f * half, 0.5f * r),
                    };
                    break;
                }
                case TreeShape.Vase:
                {
                    _centre = new Vector2(foot.X, foot.Y - trunk - crown * 0.55f);
                    float half = crown * 0.5f;
                    _reach = MathF.Max(r, half);
                    _lobes = new[]
                    {
                        new Vector3(_centre.X - 0.4f * r, _centre.Y - 0.25f * half, 0.6f * r),
                        new Vector3(_centre.X + 0.42f * r, _centre.Y - 0.3f * half, 0.58f * r),
                        new Vector3(_centre.X + J(0) * 0.1f * r, _centre.Y - 0.55f * half, 0.55f * r),
                        new Vector3(_centre.X, _centre.Y + 0.45f * half, 0.38f * r),
                    };
                    break;
                }
                case TreeShape.Umbrella:
                {
                    float top = foot.Y - _h + 0.42f * r;
                    _centre = new Vector2(foot.X, top + 0.1f * r);
                    _reach = r;
                    _lobes = new Vector3[6];
                    for (int i = 0; i < 5; i++)
                    {
                        float x = (i / 4f - 0.5f) * 1.2f * r;
                        _lobes[i] = new Vector3(foot.X + x, top + MathF.Abs(x) * 0.22f + J(i) * 0.05f * r, (0.42f + (i == 2 ? 0.06f : 0f)) * r);
                    }
                    _lobes[5] = new Vector3(foot.X + J(6) * 0.1f * r, top + 0.32f * r, 0.42f * r);
                    break;
                }
                case TreeShape.Spire:
                    _outline = Spire(foot, r, _h, trunk, look.Aspect > 2f ? 4 : 3, J(0));
                    _centre = new Vector2(foot.X, foot.Y - trunk - crown * 0.5f);
                    _reach = r;
                    break;
                case TreeShape.Column:
                    _outline = Flame(foot, r, _h, trunk);
                    _centre = new Vector2(foot.X, foot.Y - trunk - crown * 0.5f);
                    _reach = r;
                    break;
                case TreeShape.Cactus:
                {
                    _centre = new Vector2(foot.X, foot.Y - _h * 0.5f);
                    _reach = r;
                    bool flip = J(0) > 0f;
                    float s = flip ? -1f : 1f;
                    // Each a segment of the stem and its radius, as x, y, x, y, r.
                    _arms = new[]
                    {
                        new Vector4(foot.X, foot.Y, foot.X, foot.Y - _h * 0.92f),
                        new Vector4(foot.X, foot.Y - _h * 0.42f, foot.X - s * 0.32f * _w, foot.Y - _h * 0.42f),
                        new Vector4(foot.X - s * 0.32f * _w, foot.Y - _h * 0.42f, foot.X - s * 0.32f * _w, foot.Y - _h * 0.7f),
                        new Vector4(foot.X, foot.Y - _h * 0.56f, foot.X + s * 0.28f * _w, foot.Y - _h * 0.56f),
                        new Vector4(foot.X + s * 0.28f * _w, foot.Y - _h * 0.56f, foot.X + s * 0.28f * _w, foot.Y - _h * 0.8f),
                    };
                    _armRadius = new[] { 0.14f * _w, 0.09f * _w, 0.09f * _w, 0.085f * _w, 0.085f * _w };
                    break;
                }
            }
        }

        private readonly Vector4[] _arms = Array.Empty<Vector4>();
        private readonly float[] _armRadius = Array.Empty<float>();

        // A conifer in tiers: each sweeps out wider than the one above and tucks in under it.
        private static Vector2[] Spire(Vector2 foot, float r, float h, float trunk, int tiers, float jitter)
        {
            float crown = h - trunk;
            float apexY = foot.Y - h;
            var right = new List<Vector2>();
            for (int k = 1; k <= tiers; k++)
            {
                float t = k / (float)tiers;
                float y = apexY + crown * t;
                float half = r * MathF.Pow(t, 0.85f) * (1f + (k == tiers ? 0f : jitter * 0.04f));
                right.Add(new Vector2(foot.X + half, y));
                if (k < tiers) right.Add(new Vector2(foot.X + half * 0.45f, y - crown * 0.06f));
            }
            var points = new List<Vector2> { new Vector2(foot.X, apexY) };
            points.AddRange(right);
            points.Add(new Vector2(foot.X, foot.Y - trunk + crown * 0.03f));
            for (int i = right.Count - 1; i >= 0; i--) points.Add(new Vector2(2f * foot.X - right[i].X, right[i].Y));
            return points.ToArray();
        }

        // A cypress: a flame, widest a third of the way up, to a point.
        private static Vector2[] Flame(Vector2 foot, float r, float h, float trunk)
        {
            float crown = h - trunk;
            float apexY = foot.Y - h;
            const int Steps = 9;
            var points = new Vector2[Steps * 2];
            for (int i = 0; i < Steps; i++)
            {
                float t = (i + 1f) / Steps;
                float half = r * MathF.Pow(MathF.Max(MathF.Sin(MathF.PI * MathF.Pow(t, 0.75f)), 0f), 0.8f);
                half = MathF.Max(half, i == Steps - 1 ? r * 0.25f : 0f);
                points[i] = new Vector2(foot.X + half, apexY + crown * t);
                points[Steps * 2 - 1 - i] = new Vector2(foot.X - half, apexY + crown * t);
            }
            var all = new List<Vector2> { new Vector2(foot.X, apexY) };
            all.AddRange(points);
            return all.ToArray();
        }

        public override void Draw(in Pen pen)
        {
            DrawShadow(pen);
            switch (_look.Shape)
            {
                case TreeShape.Palm: DrawPalm(pen); return;
                case TreeShape.Snag: DrawSnag(pen); return;
                case TreeShape.Bamboo: DrawBamboo(pen); return;
            }
            if (_look.Shape != TreeShape.Cactus) DrawTrunk(pen);
            DrawCrown(pen);
        }

        private void DrawShadow(in Pen pen)
        {
            Vector2 centre = _foot + new Vector2(_w * 0.32f, -_w * 0.02f);
            Vector2 radii = new Vector2(_w * 0.55f, _w * 0.17f);
            if (!pen.Clip(centre.X - radii.X - 1f, centre.Y - radii.Y - 1f, centre.X + radii.X + 1f, centre.Y + radii.Y + 1f, out int x0, out int y0, out int x1, out int y1)) return;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float d = Ellipse(new Vector2(x + 0.5f, y + 0.5f), centre, radii);
                    pen.Wash(x, y, Shadow, Math.Clamp(0.5f - d / MathF.Max(radii.Y * 0.6f, 1f), 0f, 1f) * 0.6f);
                }
            }
        }

        private void DrawTrunk(in Pen pen)
        {
            float trunk = _h * _look.Trunk;
            if (trunk < 1f) return;
            float width = MathF.Max(_w * 0.11f, 1.2f);
            Vector2 top = _foot - new Vector2(0f, trunk + _w * 0.15f);
            pen.Stroke(_foot, top, width + _ink * 1.6f, Ink, 0.9f);
            pen.Stroke(_foot, top, width, _look.Wood);
        }

        private float Silhouette(Vector2 p, out float tone)
        {
            tone = 0.5f;
            if (_outline != null)
            {
                float d = Sdf.Polygon(p, _outline);
                // Lit down the left, shaded down the right, and a little lighter towards the top.
                float across = (p.X - _foot.X) / MathF.Max(_reach, 1f);
                float up = (_centre.Y - p.Y) / MathF.Max(_h, 1f);
                tone = 0.56f - across * 0.55f + up * 0.35f;
                return d;
            }
            if (_arms.Length > 0)
            {
                float best = float.MaxValue;
                for (int i = 0; i < _arms.Length; i++)
                {
                    Vector4 a = _arms[i];
                    Vector2 from = new Vector2(a.X, a.Y), to = new Vector2(a.Z, a.W);
                    float d = Sdf.Segment(p, from, to) - _armRadius[i];
                    if (d < best)
                    {
                        best = d;
                        // A stem is lit across itself from the left.
                        Vector2 along = Vector2.Normalize(to - from + new Vector2(1e-4f));
                        Vector2 side = new Vector2(-along.Y, along.X);
                        float offset = Vector2.Dot(p - from, side) / _armRadius[i];
                        tone = 0.55f + MathF.Sign(side.X) * -offset * 0.4f;
                    }
                }
                return best;
            }

            float k = _reach * 0.22f;
            float sdf = float.MaxValue;
            int nearest = 0;
            float nearestD = float.MaxValue;
            for (int i = 0; i < _lobes.Length; i++)
            {
                Vector3 lobe = _lobes[i];
                float d = (p - lobe.Xy).Length - lobe.Z;
                sdf = i == 0 ? d : Sdf.SmoothMin(sdf, d, k);
                if (d < nearestD)
                {
                    nearestD = d;
                    nearest = i;
                }
            }
            if (_look.Shape == TreeShape.Weeping && p.Y > _centre.Y)
            {
                sdf += MathF.Sin((p.X - _foot.X) / MathF.Max(_reach * 0.18f, 0.8f)) * _reach * 0.07f;
            }

            // Each mass is lit as a ball of its own, within the crown lit as one.
            Vector3 mass = _lobes[nearest];
            Vector2 local = (p - mass.Xy) / mass.Z;
            Vector2 whole = (p - _centre) / _reach;
            tone = 0.5f + Vector2.Dot(local, Light) * 0.42f + Vector2.Dot(whole, Light) * 0.3f;
            return sdf;
        }

        private void DrawCrown(in Pen pen)
        {
            if (!pen.Clip(Min.X, Min.Y, Max.X, _foot.Y + 1f, out int x0, out int y0, out int x1, out int y1)) return;
            float edge = MathF.Max(_w * 0.09f, 1.2f);
            bool weeping = _look.Shape == TreeShape.Weeping;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Silhouette(p, out float tone);
                    if (d > _ink + 1f) continue;

                    Vector3 colour = Banded(tone);
                    // Pigment gathers at a wash's edge.
                    colour *= 1f - 0.14f * Smooth(-edge, 0f, d);
                    if (weeping && p.Y > _centre.Y)
                    {
                        float strand = MathF.Abs(((p.X - _foot.X) / MathF.Max(_w * 0.12f, 1.4f)) % 1f);
                        colour *= 1f - 0.18f * Smooth(0.75f, 0.9f, strand);
                    }
                    pen.Blend(x, y, colour, Math.Clamp(0.5f - d, 0f, 1f));
                    pen.Blend(x, y, Ink, Pen.Coverage(MathF.Abs(d) - _ink * 0.5f, _ink) * 0.92f);
                }
            }
        }

        // Three tones with soft steps between them, as a watercolour laid in layers.
        private Vector3 Banded(float tone)
        {
            float toBody = Smooth(0.28f, 0.36f, tone);
            float toSun = Smooth(0.66f, 0.74f, tone);
            return Vector3.Lerp(Vector3.Lerp(_look.Shade, _look.Body, toBody), _look.Sun, toSun);
        }

        private void DrawPalm(in Pen pen)
        {
            float lean = (ChartNoise.Unit((int)_seed, 3, 91u) - 0.5f) * _w * 0.5f;
            Vector2 top = _foot + new Vector2(lean, -_h * 0.82f);
            Vector2 mid = _foot + new Vector2(lean * 0.3f, -_h * 0.42f);
            float trunk = MathF.Max(_w * 0.085f, 1.2f);
            pen.Stroke(_foot, mid, trunk + _ink * 1.6f, Ink, 0.9f);
            pen.Stroke(mid, top, trunk * 0.85f + _ink * 1.6f, Ink, 0.9f);
            pen.Stroke(_foot, mid, trunk, _look.Wood);
            pen.Stroke(mid, top, trunk * 0.85f, _look.Wood);

            const int Fronds = 7;
            Span<Vector2> a = stackalloc Vector2[Fronds];
            Span<Vector2> b = stackalloc Vector2[Fronds];
            Span<Vector2> c = stackalloc Vector2[Fronds];
            float length = _w * 0.58f;
            for (int i = 0; i < Fronds; i++)
            {
                float angle = MathF.PI * (1.08f + 0.84f * i / (Fronds - 1f)) + (ChartNoise.Unit((int)_seed, i, 13u) - 0.5f) * 0.25f;
                Vector2 dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                float drop = 0.25f + 0.25f * MathF.Abs(dir.X);
                a[i] = top;
                b[i] = top + dir * length * 0.55f + new Vector2(0f, -length * 0.08f);
                c[i] = top + dir * length + new Vector2(0f, length * drop);
            }
            float wide = MathF.Max(_w * 0.17f, 1.6f);
            for (int i = 0; i < Fronds; i++)
            {
                pen.Taper(a[i], b[i], wide + _ink * 1.6f, wide * 0.8f + _ink * 1.6f, Ink, 0.9f);
                pen.Taper(b[i], c[i], wide * 0.8f + _ink * 1.6f, _ink, Ink, 0.9f);
            }
            for (int i = 0; i < Fronds; i++)
            {
                Vector3 colour = c[i].X < top.X ? _look.Sun : c[i].Y > top.Y + length * 0.25f ? _look.Shade : _look.Body;
                pen.Taper(a[i], b[i], wide, wide * 0.8f, colour);
                pen.Taper(b[i], c[i], wide * 0.8f, 0.3f, colour);
            }
        }

        private void DrawSnag(in Pen pen)
        {
            float lean = (ChartNoise.Unit((int)_seed, 4, 19u) - 0.5f) * _w * 0.3f;
            Vector2 top = _foot + new Vector2(lean, -_h * 0.92f);
            float trunk = MathF.Max(_w * 0.12f, 1.3f);
            Vector2 forkA = Vector2.Lerp(_foot, top, 0.55f), forkB = Vector2.Lerp(_foot, top, 0.72f);
            Vector2 endA = forkA + new Vector2(-_w * 0.34f, -_h * 0.28f), endB = forkB + new Vector2(_w * 0.3f, -_h * 0.22f);
            Vector2 twig = Vector2.Lerp(forkA, endA, 0.6f) + new Vector2(-_w * 0.04f, -_h * 0.16f);
            Vector3 wood = _look.Wood * 0.85f;
            pen.Taper(_foot, top, trunk + _ink * 1.6f, _ink * 1.4f, Ink, 0.9f);
            pen.Taper(forkA, endA, trunk * 0.6f + _ink * 1.6f, _ink * 1.2f, Ink, 0.9f);
            pen.Taper(forkB, endB, trunk * 0.55f + _ink * 1.6f, _ink * 1.2f, Ink, 0.9f);
            pen.Taper(Vector2.Lerp(forkA, endA, 0.6f), twig, trunk * 0.35f + _ink, _ink, Ink, 0.85f);
            pen.Taper(_foot, top, trunk, 0.4f, wood);
            pen.Taper(forkA, endA, trunk * 0.6f, 0.3f, wood);
            pen.Taper(forkB, endB, trunk * 0.55f, 0.3f, wood);
        }

        private void DrawBamboo(in Pen pen)
        {
            float cane = MathF.Max(_w * 0.075f, 1f);
            for (int i = 0; i < 4; i++)
            {
                float x = (-0.3f + 0.2f * i) * _w;
                float tall = _h * (0.78f + 0.22f * ChartNoise.Unit((int)_seed, i, 5u));
                float lean = (ChartNoise.Unit((int)_seed, i, 6u) - 0.35f) * _w * 0.25f;
                Vector2 from = _foot + new Vector2(x, 0f), to = _foot + new Vector2(x + lean, -tall);
                pen.Taper(from, to, cane + _ink * 1.6f, cane * 0.5f + _ink, Ink, 0.85f);
                pen.Taper(from, to, cane, cane * 0.5f, _look.Wood);
                for (int k = 0; k < 4; k++)
                {
                    float t = 0.5f + 0.13f * k;
                    Vector2 at = Vector2.Lerp(from, to, t);
                    float side = (k + i) % 2 == 0 ? -1f : 1f;
                    Vector2 tip = at + new Vector2(side * _w * 0.24f, _w * 0.08f);
                    Vector3 colour = side < 0f ? _look.Sun : _look.Body;
                    pen.Taper(at, tip, MathF.Max(_w * 0.08f, 1.1f) + _ink, _ink, Ink, 0.7f);
                    pen.Taper(at, tip, MathF.Max(_w * 0.08f, 1.1f), 0.3f, colour);
                }
            }
        }
    }

    /// <summary>
    /// A mountain, in the form its rock breaks into: teeth for jagged peaks, steps for ledged beds, swelling domes, towers, mesas cut by canyons, basalt columns and blades.
    /// Lit down its left face and hatched down its right, its foot fading into the land, and capped with snow where the ground is snow.
    /// </summary>
    internal sealed class MountainGlyph : ChartGlyph
    {
        private sealed class Part
        {
            public Vector2[] Points = Array.Empty<Vector2>();
            public Vector2 Apex, Foot;          // The crest line between its lit face and its shaded one.
            public float BaseY, Height;
            public List<(Vector2 From, Vector2 To)> Ridges = new();
        }

        private readonly List<Part> _parts = new();
        private readonly MountainForm _form;
        private readonly Vector3 _lit, _shade;
        private readonly bool _snow;
        private readonly float _snowLine;
        private readonly float _ink, _scale;
        private readonly Vector2 _hatch;
        private readonly float _columnStart, _columnWidth;

        public static float AspectOf(MountainForm form) => form switch
        {
            MountainForm.Jagged => 0.8f,
            MountainForm.Domes => 0.46f,
            MountainForm.Ledged => 0.52f,
            MountainForm.Canyons => 0.42f,
            MountainForm.Columns => 0.52f,
            MountainForm.Towers => 0.95f,
            _ => 0.66f,
        };

        public MountainGlyph(Vector2 foot, float width, MountainForm form, Vector3 lit, Vector3 shade, bool snow, float snowLine, uint seed, float ink, float scale)
        {
            _form = form;
            _lit = lit;
            _shade = shade;
            _snow = snow;
            _snowLine = snowLine;
            _ink = ink;
            _scale = scale;
            float height = width * AspectOf(form);
            _hatch = Vector2.Normalize(new Vector2(height, -width * 0.55f));
            Order = foot.Y;

            float J(int i) => (ChartNoise.Unit((int)seed, i, 311u) - 0.5f) * 2f;
            float W = width, H = height;
            Vector2 P(float x, float y) => foot + new Vector2(x * W, -y * H);
            Part Make(Vector2[] points, Vector2 apex, Vector2 crestFoot)
            {
                var part = new Part { Points = points, Apex = apex, Foot = crestFoot, BaseY = foot.Y };
                part.Height = MathF.Max(foot.Y - points.Min(p => p.Y), 1f);
                return part;
            }

            switch (form)
            {
                case MountainForm.Jagged:
                {
                    float ax = J(0) * 0.12f;
                    float second = J(1) > 0f ? 1f : -1f;
                    var pts = new[]
                    {
                        P(-0.5f, 0f), P(-0.37f, 0.2f + J(2) * 0.04f), P(-0.27f, 0.36f + J(3) * 0.05f), P(-0.2f, 0.33f), P(-0.13f, 0.56f + J(4) * 0.05f),
                        P(ax - 0.06f, 0.8f), P(ax, 1f), P(ax + 0.05f, 0.84f), P(0.1f, 0.62f + J(5) * 0.04f), P(0.17f, 0.7f + J(6) * 0.05f),
                        P(0.25f, 0.46f), P(0.31f, 0.4f), P(0.4f, 0.2f + J(7) * 0.04f), P(0.5f, 0f),
                    };
                    if (second < 0f)
                    {
                        // The second peak on the other side.
                        for (int i = 0; i < pts.Length; i++) pts[i].X = 2f * foot.X - pts[i].X;
                        Array.Reverse(pts);
                    }
                    Part part = Make(pts, pts.OrderBy(p => p.Y).First(), Vector2.Zero);
                    part.Foot = new Vector2(part.Apex.X + W * 0.12f, foot.Y);
                    part.Ridges.Add((P(-0.13f * second, 0.56f), P(-0.07f * second, 0.22f)));
                    part.Ridges.Add((P(0.17f * second, 0.7f), P(0.21f * second, 0.34f)));
                    _parts.Add(part);
                    break;
                }
                case MountainForm.Domes:
                {
                    const int Steps = 14;
                    var pts = new Vector2[Steps + 1];
                    float bump = J(0) * 0.08f;
                    for (int i = 0; i <= Steps; i++)
                    {
                        float t = i / (float)Steps;
                        float y = MathF.Pow(MathF.Sin(MathF.PI * t), 0.7f) * (1f + bump * MathF.Sin(MathF.PI * 3f * t));
                        pts[i] = P(t - 0.5f, y);
                    }
                    Vector2 apex = P(-0.04f, 1f);
                    _parts.Add(Make(pts, apex, new Vector2(apex.X + W * 0.18f, foot.Y)));
                    if (J(1) > -0.2f)
                    {
                        // A lower dome shouldering the first.
                        var low = new Vector2[Steps + 1];
                        float side = J(2) > 0f ? 1f : -1f;
                        for (int i = 0; i <= Steps; i++)
                        {
                            float t = i / (float)Steps;
                            low[i] = P(side * 0.28f + (t - 0.5f) * 0.6f, MathF.Pow(MathF.Sin(MathF.PI * t), 0.75f) * 0.62f);
                        }
                        Vector2 lowApex = P(side * 0.28f - 0.03f, 0.62f);
                        _parts.Add(Make(low, lowApex, new Vector2(lowApex.X + W * 0.12f, foot.Y)));
                    }
                    break;
                }
                case MountainForm.Ledged:
                {
                    var pts = new[]
                    {
                        P(-0.5f, 0f), P(-0.42f, 0.14f), P(-0.36f, 0.34f + J(0) * 0.04f), P(-0.27f, 0.4f), P(-0.2f, 0.6f + J(1) * 0.04f), P(-0.12f, 0.66f), P(-0.07f, 0.86f),
                        P(0.02f, 1f), P(0.1f, 0.94f), P(0.15f, 0.76f), P(0.24f, 0.7f), P(0.3f, 0.48f + J(2) * 0.04f), P(0.38f, 0.42f), P(0.44f, 0.18f), P(0.5f, 0f),
                    };
                    Part part = Make(pts, P(0.07f, 1f), P(0.2f, 0f));
                    _parts.Add(part);
                    break;
                }
                case MountainForm.Canyons:
                {
                    // A mesa, a butte on a bench, or a lone stack, each its own width.
                    float top = 0.22f + 0.14f * J(0);
                    Vector2[] pts = J(1) > 0.1f
                        ? new[] { P(-0.5f, 0f), P(-0.42f, 0.14f), P(-0.37f, 0.5f), P(-0.25f, 0.56f + J(2) * 0.04f), P(-top, 0.6f), P(-top + 0.04f, 1f), P(top - 0.02f, 0.98f), P(top + 0.03f, 0.62f), P(0.34f, 0.56f), P(0.39f, 0.36f + J(3) * 0.05f), P(0.45f, 0.12f), P(0.5f, 0f) }
                        : J(1) > -0.5f
                            ? new[] { P(-0.5f, 0f), P(-0.43f, 0.12f), P(-0.37f, 0.76f), P(-0.33f, 1f), P(-0.05f, 1f), P(-0.02f, 0.9f + J(2) * 0.03f), P(0.03f, 1f), P(0.29f, 0.98f), P(0.33f, 0.8f), P(0.38f, 0.44f + J(3) * 0.05f), P(0.44f, 0.13f), P(0.5f, 0f) }
                            : new[] { P(-0.3f, 0f), P(-0.24f, 0.2f), P(-0.17f, 0.9f), P(-0.13f, 1.2f), P(0.1f, 1.18f), P(0.14f, 0.86f), P(0.2f, 0.3f), P(0.3f, 0f) };
                    Sdf.Bounds(pts, out Vector2 low, out Vector2 high);
                    Vector2 corner = pts.Where(p => p.Y < low.Y + H * 0.06f).OrderByDescending(p => p.X).First();
                    _parts.Add(Make(pts, corner, new Vector2(corner.X + W * 0.02f, foot.Y)));
                    break;
                }
                case MountainForm.Columns:
                {
                    const int Columns = 6;
                    var pts = new List<Vector2> { P(-0.5f, 0f), P(-0.45f, 0.68f) };
                    for (int i = 0; i < Columns; i++)
                    {
                        float x0 = -0.45f + 0.9f * i / Columns, x1 = -0.45f + 0.9f * (i + 1) / Columns;
                        float top = 0.88f + 0.12f * ChartNoise.Unit((int)seed, i, 17u);
                        pts.Add(P(x0, top));
                        pts.Add(P(x1, top));
                    }
                    pts.Add(P(0.45f, 0.68f));
                    pts.Add(P(0.5f, 0f));
                    _columnStart = foot.X - 0.45f * W;
                    _columnWidth = 0.9f * W / Columns;
                    _parts.Add(Make(pts.ToArray(), P(0.22f, 0.95f), P(0.24f, 0f)));
                    break;
                }
                case MountainForm.Towers:
                {
                    int count = J(0) > 0f ? 3 : 2;
                    var towers = new List<(float x, float w, float h)>();
                    for (int i = 0; i < count; i++)
                    {
                        float x = count == 3 ? (-0.3f + 0.3f * i) : (-0.17f + 0.34f * i);
                        towers.Add((x + J(i + 1) * 0.04f, 0.3f + 0.08f * ChartNoise.Unit((int)seed, i, 23u), 0.7f + 0.35f * ChartNoise.Unit((int)seed, i, 29u)));
                    }
                    // The tallest stands behind.
                    foreach ((float x, float w, float h) in towers.OrderByDescending(t => t.h))
                    {
                        const int Steps = 12;
                        var pts = new Vector2[Steps + 1];
                        for (int k = 0; k <= Steps; k++)
                        {
                            float theta = MathF.PI * k / Steps;
                            float c = -MathF.Cos(theta);
                            float s = MathF.Sin(theta);
                            pts[k] = P(x + c * w * 0.5f * (0.75f + 0.25f * s), h * MathF.Pow(s, 0.3f));
                        }
                        Vector2 apex = P(x - w * 0.06f, h);
                        Part part = Make(pts, apex, P(x + w * 0.12f, 0f));
                        part.Ridges.Add((P(x - w * 0.18f, h * 0.8f), P(x - w * 0.22f, h * 0.35f)));
                        _parts.Add(part);
                    }
                    break;
                }
                default:
                {
                    // Blades: ribs all leaning one way, a gentle back and a sheer front.
                    int count = 3;
                    for (int i = 0; i < count; i++)
                    {
                        float x0 = -0.5f + 0.3f * i + J(i) * 0.03f;
                        float h = 0.72f + 0.28f * ChartNoise.Unit((int)seed, i, 41u);
                        var pts = new[] { P(x0, 0f), P(x0 + 0.12f, h * 0.45f), P(x0 + 0.3f, h), P(x0 + 0.33f, h * 0.7f), P(x0 + 0.4f, 0f) };
                        _parts.Add(Make(pts, P(x0 + 0.3f, h), P(x0 + 0.34f, 0f)));
                    }
                    break;
                }
            }

            Min = new Vector2(float.MaxValue);
            Max = new Vector2(float.MinValue);
            foreach (Part part in _parts)
            {
                Sdf.Bounds(part.Points, out Vector2 min, out Vector2 max);
                Min = Vector2.ComponentMin(Min, min - new Vector2(ink + 2f));
                Max = Vector2.ComponentMax(Max, max + new Vector2(ink + 2f));
            }
        }

        /// <summary>Points over what it covers, for seeing whether it would hide anywhere anyone goes.</summary>
        public IEnumerable<Vector2> Samples(int across, int down)
        {
            foreach (Part part in _parts)
            {
                Sdf.Bounds(part.Points, out Vector2 min, out Vector2 max);
                for (int j = 0; j < down; j++)
                {
                    for (int i = 0; i < across; i++)
                    {
                        Vector2 p = new Vector2(min.X + (max.X - min.X) * (i + 0.5f) / across, min.Y + (max.Y - min.Y) * (j + 0.5f) / down);
                        if (Sdf.Polygon(p, part.Points) < 0f) yield return p;
                    }
                }
            }
        }

        public override void Draw(in Pen pen)
        {
            foreach (Part part in _parts) DrawPart(pen, part);
        }

        private void DrawPart(in Pen pen, Part part)
        {
            Sdf.Bounds(part.Points, out Vector2 min, out Vector2 max);
            if (!pen.Clip(min.X - _ink - 1f, min.Y - _ink - 1f, max.X + _ink + 1f, max.Y + 1f, out int x0, out int y0, out int x1, out int y1)) return;

            Vector2 crest = part.Foot - part.Apex;
            float crestLength = MathF.Max(crest.Length, 1f);
            float spacing = MathF.Max(3.3f * _scale, 2f);
            bool strata = _form is MountainForm.Ledged or MountainForm.Canyons;
            float bands = _form == MountainForm.Canyons ? 5f : 3f;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Sdf.Polygon(p, part.Points);
                    if (d > _ink + 1f) continue;

                    float v = (part.BaseY - p.Y) / part.Height;
                    float fade = Smooth(0f, 0.24f, v);
                    float side = (crest.X * (p.Y - part.Apex.Y) - crest.Y * (p.X - part.Apex.X)) / crestLength;
                    float shaded = Smooth(1.2f, -1.2f, side);

                    Vector3 colour = Vector3.Lerp(_lit * (0.94f + 0.14f * v), _shade * 0.92f, shaded);
                    // Hatching down the shaded face, along the fall of the slope.
                    float u = Vector2.Dot(p, _hatch) / spacing;
                    float f = u - MathF.Floor(u);
                    float hatch = Smooth(0.55f, 0.68f, f) * (1f - Smooth(0.82f, 0.95f, f));
                    colour *= 1f - 0.32f * hatch * shaded;

                    if (strata)
                    {
                        float band = v * bands;
                        float g = band - MathF.Floor(band);
                                                colour = Vector3.Lerp(colour, Ink, (1f - Smooth(0.03f, 0.08f, g)) * 0.16f * fade);
                    }
                    if (_form == MountainForm.Columns && v < 0.88f)
                    {
                        float c = (p.X - _columnStart) / _columnWidth;
                        float g = c - MathF.Floor(c);
                        colour = Vector3.Lerp(colour, Ink, (1f - Smooth(0.04f, 0.12f, g)) * 0.4f * fade);
                    }
                    if (_snow)
                    {
                        float zig = MathF.Abs((p.X / MathF.Max(6f * _scale, 3f)) % 1f - 0.5f) * 0.14f;
                        float snow = Smooth(_snowLine - 0.02f, _snowLine + 0.02f, v + zig);
                        colour = Vector3.Lerp(colour, Vector3.Lerp(Snow, SnowShade, shaded), snow);
                    }

                    pen.Blend(x, y, colour, Math.Clamp(0.5f - d, 0f, 1f) * fade);
                    pen.Blend(x, y, Ink, Pen.Coverage(MathF.Abs(d) - _ink * 0.7f, _ink * 1.4f) * 0.95f * fade);
                }
            }

            // The crest, from the top part way down, and the ribs on its face.
            Vector2 crestEnd = Vector2.Lerp(part.Apex, part.Foot, 0.55f);
            pen.Taper(part.Apex, crestEnd, _ink, _ink * 0.3f, Ink, 0.6f);
            foreach ((Vector2 from, Vector2 to) in part.Ridges) pen.Taper(from, to, _ink * 0.9f, _ink * 0.25f, Ink, 0.5f);
        }
    }

    /// <summary>
    /// A building, drawn from its own footprint stood up as the game's camera sees it from the south: its south walls under its roof, and its shadow. A tower is round.
    /// </summary>
    internal sealed class BuildingGlyph : ChartGlyph
    {
        private readonly Vector2[] _base;
        private readonly float _height, _ink;
        private readonly Vector3 _wall, _roof;
        private readonly bool _flat;
        private readonly Vector2? _round;
        private readonly float _radius;

        public BuildingGlyph(Vector2[] corners, float height, Vector3 wall, Vector3 roof, bool flat, float ink)
        {
            _base = corners;
            _height = height;
            _wall = wall;
            _roof = roof;
            _flat = flat;
            _ink = ink;
            Sdf.Bounds(corners, out Vector2 min, out Vector2 max);
            Order = max.Y;
            Min = min - new Vector2(ink + 2f, height + ink + 2f);
            Max = max + new Vector2(height * 0.7f + ink + 2f, height * 0.3f + ink + 2f);
        }

        public BuildingGlyph(Vector2 centre, float radius, float height, Vector3 wall, Vector3 roof, float ink)
        {
            _base = Array.Empty<Vector2>();
            _round = centre;
            _radius = radius;
            _height = height;
            _wall = wall;
            _roof = roof;
            _ink = ink;
            Order = centre.Y + radius;
            Min = centre - new Vector2(radius + ink + 2f, radius + height * 1.4f + ink + 2f);
            Max = centre + new Vector2(radius + height * 0.7f + ink + 2f, radius + height * 0.3f + ink + 2f);
        }

        public override void Draw(in Pen pen)
        {
            if (_round is Vector2 centre) DrawTower(pen, centre);
            else DrawHouse(pen);
        }

        private void DrawHouse(in Pen pen)
        {
            Vector2 lift = new Vector2(0f, -_height);
            Vector2 cast = new Vector2(_height * 0.6f, _height * 0.22f);

            // Its shadow: the footprint swept down to the right.
            var swept = new Vector2[_base.Length * 2];
            for (int i = 0; i < _base.Length; i++)
            {
                swept[i] = _base[i];
                swept[i + _base.Length] = _base[i] + cast;
            }
            WashPolygon(pen, Hull(swept), Shadow, 0.55f);

            Vector2 middle = Vector2.Zero;
            foreach (Vector2 corner in _base) middle += corner;
            middle /= _base.Length;
            for (int i = 0; i < _base.Length; i++)
            {
                Vector2 a = _base[i], b = _base[(i + 1) % _base.Length];
                Vector2 edge = b - a;
                Vector2 normal = Vector2.Normalize(new Vector2(edge.Y, -edge.X));
                if (Vector2.Dot(normal, (a + b) * 0.5f - middle) < 0f) normal = -normal;
                if (normal.Y <= 0.05f) continue;
                Vector3 wall = _wall * (normal.X < 0f ? 1f : 0.8f);
                pen.Polygon(new[] { a, b, b + lift, a + lift }, wall, 1f, _ink * 0.8f, Ink, 0.85f);
            }

            var roof = new Vector2[_base.Length];
            for (int i = 0; i < _base.Length; i++) roof[i] = _base[i] + lift;
            if (_flat || _base.Length != 4)
            {
                pen.Polygon(roof, _roof, 1f, _ink, Ink, 0.9f);
                return;
            }

            // A pitched roof: its ridge along the long side, the half facing the light lighter.
            bool alongFirst = (roof[1] - roof[0]).LengthSquared >= (roof[2] - roof[1]).LengthSquared;
            Vector2 m1 = alongFirst ? (roof[0] + roof[3]) * 0.5f : (roof[0] + roof[1]) * 0.5f;
            Vector2 m2 = alongFirst ? (roof[1] + roof[2]) * 0.5f : (roof[2] + roof[3]) * 0.5f;
            Vector2 ridge = m2 - m1;
            if (!pen.Clip(Min.X, Min.Y, Max.X, Max.Y, out int x0, out int y0, out int x1, out int y1)) return;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Sdf.Polygon(p, roof);
                    if (d > _ink + 1f) continue;
                    float side = ridge.X * (p.Y - m1.Y) - ridge.Y * (p.X - m1.X);
                    Vector2 away = Vector2.Normalize(new Vector2(-ridge.Y, ridge.X) * MathF.Sign(side) + new Vector2(1e-4f));
                    Vector3 colour = _roof * (Vector2.Dot(away, Light) > 0f ? 1.08f : 0.8f);
                    pen.Blend(x, y, colour, Math.Clamp(0.5f - d, 0f, 1f));
                    pen.Blend(x, y, Ink, Pen.Coverage(MathF.Abs(d) - _ink * 0.5f, _ink) * 0.9f);
                }
            }
            pen.Stroke(m1, m2, _ink * 0.9f, Ink, 0.75f);
        }

        private void DrawTower(in Pen pen, Vector2 centre)
        {
            Vector2 top = centre - new Vector2(0f, _height);
            Vector2 cast = new Vector2(_height * 0.6f, _height * 0.22f);
            WashPolygon(pen, Hull(Circle(centre, _radius).Concat(Circle(centre + cast, _radius)).ToArray()), Shadow, 0.55f);

            // The wall: the base circle's lower half and the rectangle up to the top.
            if (pen.Clip(Min.X, Min.Y, Max.X, Max.Y, out int x0, out int y0, out int x1, out int y1))
            {
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                        float body = MathF.Max(MathF.Abs(p.X - centre.X) - _radius, MathF.Max(top.Y - p.Y, p.Y - centre.Y));
                        float d = MathF.Min(body, (p - centre).Length - _radius);
                        if (d > _ink + 1f) continue;
                        float across = (p.X - centre.X) / _radius;
                        Vector3 colour = _wall * (1.05f - 0.3f * Smooth(-0.4f, 0.8f, across));
                        pen.Blend(x, y, colour, Math.Clamp(0.5f - d, 0f, 1f));
                        pen.Blend(x, y, Ink, Pen.Coverage(MathF.Abs(d) - _ink * 0.5f, _ink) * 0.85f);
                    }
                }
            }
            // A cone of a roof.
            Vector2 apex = top - new Vector2(0f, _radius * 1.3f);
            var cone = new List<Vector2> { apex };
            for (int k = 0; k <= 8; k++)
            {
                float a = MathF.PI * k / 8f;
                cone.Add(top + new Vector2(MathF.Cos(a) * _radius * 1.12f, MathF.Sin(a) * _radius * 0.45f));
            }
            Vector2[] coneShape = cone.ToArray();
            if (pen.Clip(Min.X, Min.Y, Max.X, Max.Y, out x0, out y0, out x1, out y1))
            {
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                        float d = Sdf.Polygon(p, coneShape);
                        if (d > _ink + 1f) continue;
                        Vector3 colour = _roof * (p.X < apex.X ? 1.08f : 0.78f);
                        pen.Blend(x, y, colour, Math.Clamp(0.5f - d, 0f, 1f));
                        pen.Blend(x, y, Ink, Pen.Coverage(MathF.Abs(d) - _ink * 0.5f, _ink) * 0.9f);
                    }
                }
            }
        }

        private static IEnumerable<Vector2> Circle(Vector2 centre, float radius)
        {
            for (int k = 0; k < 12; k++)
            {
                float a = MathF.Tau * k / 12f;
                yield return centre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
            }
        }

        private static void WashPolygon(in Pen pen, Vector2[] shape, Vector3 tint, float alpha)
        {
            Sdf.Bounds(shape, out Vector2 min, out Vector2 max);
            if (!pen.Clip(min.X - 1f, min.Y - 1f, max.X + 1f, max.Y + 1f, out int x0, out int y0, out int x1, out int y1)) return;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float d = Sdf.Polygon(new Vector2(x + 0.5f, y + 0.5f), shape);
                    pen.Wash(x, y, tint, Math.Clamp(0.5f - d, 0f, 1f) * alpha);
                }
            }
        }

        // The convex hull, by Andrew's monotone chain.
        private static Vector2[] Hull(Vector2[] points)
        {
            Vector2[] sorted = points.OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
            var hull = new List<Vector2>();
            static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                IEnumerable<Vector2> order = pass == 0 ? sorted : sorted.Reverse();
                foreach (Vector2 p in order)
                {
                    while (hull.Count >= start + 2 && Cross(hull[^2], hull[^1], p) <= 0f) hull.RemoveAt(hull.Count - 1);
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count - 1);
            }
            return hull.ToArray();
        }
    }
}
