using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Fence
{
    /// <summary>The materials fences are made of. Headless, so that the forge can run with no GPU: <see cref="Textures.TextureGenerator"/> fills it in.</summary>
    public static class FenceMaterials
    {
        public static int Weathered;    // Split and sawn wood gone silver in the weather.
        public static int Bark;         // Logs with the bark still on.
        public static int Cane;         // Bamboo, and the rods of a hurdle.
        public static int Stone;        // Field stone: drawn as whatever rock the world is made of, as a boulder is.
        public static int Masonry;      // Dressed stone in courses.
        public static int Timber;       // Sawn and planed, as a house's frame is.
        public static int Plaster;
        public static int Roof;
        public static int Iron;
        public static int Cord;         // Rope and lashing.
    }

    /// <summary>What a part is made of: a material, its colour as an albedo before the texture's own, and how many metres one repeat of the texture covers.</summary>
    public readonly record struct Surface(int Material, Vector3 Colour, float Tile = 1.6f)
    {
        public Surface Tinted(float by) => this with { Colour = Colour * by };
        public Surface Tinted(Vector3 by) => this with { Colour = Colour * by };
    }

    /// <summary>Which faces of a block to leave out, as those nothing can see cost as much as those it can.</summary>
    [Flags]
    public enum Faces
    {
        None = 0,
        NegativeX = 1, PositiveX = 2,
        NegativeY = 4, PositiveY = 8,
        NegativeZ = 16, PositiveZ = 32,
    }

    /// <summary>
    /// Collects the parts of one piece of fence into a <see cref="FloraMesh"/>. A span is built level, from x = 0 to its length with y up and +z inside, and leans into the
    /// slope it's for in <see cref="Finish"/>: what runs along it (a rail, a band, a wall) is sheared, so that it rises with the ground and its ends stay plumb, and what stands
    /// (a stake, a pale, a stone) is only lifted, by the slope at where it stands, so that it stays upright. Faces go counter-clockwise seen from outside.
    /// </summary>
    internal sealed class FenceBuilder
    {
        private const float DampBelow = 0.75f;      // Metres up that the ground's damp and dirt reach.
        private const float DampDarkest = 0.66f;

        public readonly FloraMesh Mesh = new();
        private readonly List<(int First, int End, float X)> _uprights = new();

        /// <summary>The rise over run of the span: zero for a post.</summary>
        public float Slope;

        public int Mark => Mesh.VertexCount;

        /// <summary>Everything added since <paramref name="mark"/> stands upright, and rides the slope by where it stands, <paramref name="x"/>.</summary>
        public void Upright(int mark, float x) => _uprights.Add((mark, Mesh.VertexCount, x));

        public FloraMesh Finish()
        {
            var lift = new float[Mesh.VertexCount];
            Array.Fill(lift, float.NaN);
            foreach ((int first, int end, float x) in _uprights)
            {
                for (int i = first; i < end; i++) lift[i] = x;
            }

            for (int i = 0; i < Mesh.Vertices.Count; i++)
            {
                FloraVertex v = Mesh.Vertices[i];
                v.Colour *= MathHelper.Lerp(DampDarkest, 1f, Smoothstep(-0.25f, DampBelow, v.Position.Y));
                if (Slope != 0f)
                {
                    if (float.IsNaN(lift[i]))
                    {
                        // A shear: y' = y + slope x. Normals go by its inverse transpose, and tangents by the shear itself.
                        v.Position.Y += Slope * v.Position.X;
                        v.Normal = new Vector3(v.Normal.X - Slope * v.Normal.Y, v.Normal.Y, v.Normal.Z).Normalized();
                        v.Tangent = new Vector3(v.Tangent.X, v.Tangent.Y + Slope * v.Tangent.X, v.Tangent.Z).Normalized();
                    }
                    else
                    {
                        v.Position.Y += Slope * lift[i];
                    }
                }
                Mesh.Vertices[i] = v;
            }
            return Mesh;
        }

        public static float Smoothstep(float edge0, float edge1, float x)
        {
            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private int Vertex(Vector3 position, Vector3 normal, Vector3 tangent, Vector3 colour, Vector2 uv, int material)
        {
            return Mesh.Add(new FloraVertex { Position = position, Normal = normal, Tangent = tangent, Colour = colour, UV = uv, Material = material, Joint = -1 });
        }

        private static void Frame(Vector3 along, out Vector3 side, out Vector3 other)
        {
            Vector3 reference = MathF.Abs(along.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
            side = Vector3.Cross(along, reference).Normalized();
            other = Vector3.Cross(side, along).Normalized();
        }

        // A triangle wound so that it faces away from inside, whichever way its corners came.
        private void Facing(int a, int b, int c, Vector3 inside)
        {
            Vector3 pa = Mesh.Vertices[a].Position, pb = Mesh.Vertices[b].Position, pc = Mesh.Vertices[c].Position;
            Vector3 face = Vector3.Cross(pb - pa, pc - pa);
            if (Vector3.Dot(face, (pa + pb + pc) / 3f - inside) >= 0f) Mesh.Triangle(a, b, c);
            else Mesh.Triangle(a, c, b);
        }

        private void FacingQuad(int a, int b, int c, int d, Vector3 inside)
        {
            Facing(a, b, c, inside);
            Facing(a, c, d, inside);
        }

        /// <summary>
        /// Round wood, or bamboo: a tube from <paramref name="foot"/> to <paramref name="head"/>, bowed by <paramref name="bow"/> at its middle, its radius wandering by
        /// <paramref name="rough"/> around and along. A head can be cut square, sharpened to a point of facets as an axe leaves it, or left open.
        /// </summary>
        public void Log(Vector3 foot, Vector3 head, float footRadius, float headRadius, int sides, Surface surface, uint seed, LogEnd footEnd = LogEnd.Open, LogEnd headEnd = LogEnd.Cut,
            float rough = 0.06f, int rings = 2, Vector3 bow = default, float point = 0f, Vector3? endColour = null, float[]? nodes = null, Vector3? nodeColour = null)
        {
            Vector3 axis = head - foot;
            float length = axis.Length;
            if (length < 1e-4f) return;
            Vector3 along = axis / length;
            Frame(along, out Vector3 side, out Vector3 other);
            float around = MathF.Max(MathF.Round(MathF.Tau * MathF.Max(footRadius, headRadius) / surface.Tile * 2f) * 0.5f, 0.5f);
            Vector3 cut = endColour ?? surface.Colour * 1.25f;

            // Stations along: evenly, and at every node of a cane a ridge of its own.
            var stations = new List<(float t, float swell, bool node)>();
            for (int r = 0; r <= rings; r++) stations.Add((r / (float)rings, 1f, false));
            if (nodes != null)
            {
                foreach (float at in nodes)
                {
                    float t = at / length;
                    if (t <= 0.02f || t >= 0.98f) continue;
                    float half = MathF.Min(0.02f / length, 0.01f);
                    stations.Add((t - half, 1f, false));
                    stations.Add((t, 1.08f, true));
                    stations.Add((t + half, 1f, false));
                }
                stations.Sort((a, b) => a.t.CompareTo(b.t));
            }

            int previous = -1, firstRing = Mesh.VertexCount;
            Vector3 lastCentre = foot;
            float lastRadius = footRadius;
            for (int s = 0; s < stations.Count; s++)
            {
                (float t, float swell, bool node) = stations[s];
                Vector3 centre = foot + axis * t + bow * (4f * t * (1f - t));
                float radius = MathHelper.Lerp(footRadius, headRadius, t) * swell;
                Vector3 colour = node && nodeColour.HasValue ? nodeColour.Value : surface.Colour;
                int start = Mesh.VertexCount;
                for (int i = 0; i <= sides; i++)
                {
                    float angle = i * (MathF.Tau / sides);
                    float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
                    Vector3 direction = side * cos + other * sin;
                    float wander = 1f + rough * (FloraNoise.Value(new Vector3(cos * 1.4f + 3f, sin * 1.4f + 3f, t * length * 0.7f), seed) * 2f - 1f);
                    float shade = 0.92f + 0.16f * FloraNoise.Value(new Vector3(cos * 2f, sin * 2f, t * length * 1.3f), seed + 7u);
                    Vector3 tangent = Vector3.Cross(direction, along);
                    Vertex(centre + direction * radius * wander, direction, tangent, colour * shade, new Vector2(around * i / sides, t * length / surface.Tile), surface.Material);
                }
                if (previous >= 0)
                {
                    for (int i = 0; i < sides; i++)
                    {
                        FacingQuad(previous + i, previous + i + 1, start + i + 1, start + i, (centre + lastCentre) * 0.5f);
                    }
                }
                previous = start;
                lastCentre = centre;
                lastRadius = radius;
            }

            if (footEnd == LogEnd.Cut) Cap(firstRing, sides, foot, -along, cut, surface);
            if (headEnd == LogEnd.Cut) Cap(previous, sides, lastCentre, along, cut, surface);
            else if (headEnd == LogEnd.Point) Sharpen(previous, sides, lastCentre, along, side, other, lastRadius, point, cut, surface, seed);
        }

        // The end grain, across the ring's own corners, so that however the log wanders there's no slit at its edge.
        private void Cap(int ring, int sides, Vector3 centre, Vector3 normal, Vector3 colour, Surface surface)
        {
            Frame(normal, out Vector3 u, out Vector3 v);
            int hub = Vertex(centre, normal, u, colour * 0.9f, new Vector2(0.5f), surface.Material);
            int first = Mesh.VertexCount;
            for (int i = 0; i < sides; i++)
            {
                Vector3 at = Mesh.Vertices[ring + i].Position;
                Vertex(at, normal, u, colour, new Vector2(Vector3.Dot(at - centre, u), Vector3.Dot(at - centre, v)) / surface.Tile + new Vector2(0.5f), surface.Material);
            }
            for (int i = 0; i < sides; i++) Facing(hub, first + i, first + (i + 1) % sides, centre - normal);
        }

        // An axe's point: a facet for each side, each flat, coming to a tip a little off the middle.
        private void Sharpen(int ring, int sides, Vector3 centre, Vector3 along, Vector3 side, Vector3 other, float radius, float length, Vector3 colour, Surface surface, uint seed)
        {
            float offset = radius * 0.25f;
            Vector3 tip = centre + along * length + side * (offset * (FloraNoise.Value(new Vector3(1.7f, 0.3f, 0.5f), seed) * 2f - 1f))
                + other * (offset * (FloraNoise.Value(new Vector3(0.2f, 2.9f, 0.5f), seed) * 2f - 1f));
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = Mesh.Vertices[ring + i].Position, b = Mesh.Vertices[ring + i + 1].Position;
                Vector3 normal = Vector3.Cross(b - a, tip - a).Normalized();
                if (Vector3.Dot(normal, (a + b) * 0.5f - centre) < 0f) normal = -normal;
                Vector3 tangent = (b - a).Normalized();
                float shade = 0.85f + 0.3f * FloraNoise.Value(new Vector3(i * 1.31f, 0.5f, 7.7f), seed);
                int va = Vertex(a, normal, tangent, colour * shade, new Vector2(0f, 0f), surface.Material);
                int vb = Vertex(b, normal, tangent, colour * shade, new Vector2((b - a).Length / surface.Tile, 0f), surface.Material);
                int vt = Vertex(tip, normal, tangent, colour * shade * 0.92f, new Vector2(0.5f * (b - a).Length / surface.Tile, length / surface.Tile), surface.Material);
                Facing(va, vb, vt, centre);
            }
        }

        /// <summary>
        /// A length with flat faces: a split rail, a hewn post, a plank, a strap of iron. Its section is a convex polygon across it, x to the side and y up (towards
        /// <paramref name="up"/>), and at each station along it the section is moved, scaled and turned by <paramref name="shape"/>, so a rail can sag and a tenon taper.
        /// <paramref name="headCut"/> lifts the head's corners by where they are in the section, for a top cut on a slant.
        /// </summary>
        public void Prism(Vector3 foot, Vector3 head, Vector3 up, ReadOnlySpan<Vector2> section, Surface surface, ReadOnlySpan<float> stations,
            Func<float, (Vector3 offset, float scale, float turn)>? shape = null, bool capFoot = true, bool capHead = true, Vector3? endColour = null, Func<Vector2, float>? headCut = null)
        {
            Vector3 axis = head - foot;
            float length = axis.Length;
            if (length < 1e-4f || section.Length < 3) return;
            Vector3 along = axis / length;
            Vector3 upward = (up - along * Vector3.Dot(up, along)).Normalized();
            Vector3 across = Vector3.Cross(along, upward);
            Vector3 cut = endColour ?? surface.Colour * 1.2f;
            int count = section.Length;

            // What's inside is judged from the middle of each section's own corners, as a section needn't be about its line: a pale stands on it, a coping sits on it.
            var rings = new Vector3[stations.Length, count];
            var centres = new Vector3[stations.Length];
            for (int s = 0; s < stations.Length; s++)
            {
                float t = stations[s];
                (Vector3 offset, float scale, float turn) = shape?.Invoke(t) ?? (Vector3.Zero, 1f, 0f);
                float cos = MathF.Cos(turn), sin = MathF.Sin(turn);
                Vector3 centre = foot + axis * t + offset;
                Vector3 sum = Vector3.Zero;
                for (int k = 0; k < count; k++)
                {
                    Vector2 p = section[k] * scale;
                    p = new Vector2(p.X * cos - p.Y * sin, p.X * sin + p.Y * cos);
                    Vector3 at = centre + across * p.X + upward * p.Y;
                    sum += at;
                    if (s == stations.Length - 1 && headCut != null) at += along * headCut(section[k]);
                    rings[s, k] = at;
                }
                centres[s] = sum / count;
            }

            // Faces along it, each flat, with the texture running its length.
            float u = 0f;
            for (int k = 0; k < count; k++)
            {
                int next = (k + 1) % count;
                float width = (section[next] - section[k]).Length;
                for (int s = 0; s + 1 < stations.Length; s++)
                {
                    Vector3 a = rings[s, k], b = rings[s, next], c = rings[s + 1, next], d = rings[s + 1, k];
                    Vector3 normal = Vector3.Cross(b - a, d - a);
                    if (normal.LengthSquared < 1e-12f) normal = Vector3.Cross(c - b, a - b);
                    normal.Normalize();
                    Vector3 middle = (centres[s] + centres[s + 1]) * 0.5f;
                    if (Vector3.Dot(normal, (a + b + c + d) * 0.25f - middle) < 0f) normal = -normal;
                    Vector3 tangent = (b - a).Normalized();
                    float v0 = stations[s] * length / surface.Tile, v1 = stations[s + 1] * length / surface.Tile;
                    float u0 = u / surface.Tile, u1 = (u + width) / surface.Tile;
                    int ia = Vertex(a, normal, tangent, surface.Colour, new Vector2(u0, v0), surface.Material);
                    int ib = Vertex(b, normal, tangent, surface.Colour, new Vector2(u1, v0), surface.Material);
                    int ic = Vertex(c, normal, tangent, surface.Colour, new Vector2(u1, v1), surface.Material);
                    int id = Vertex(d, normal, tangent, surface.Colour, new Vector2(u0, v1), surface.Material);
                    FacingQuad(ia, ib, ic, id, middle);
                }
                u += width;
            }

            if (capFoot) Fan(rings, 0, count, -along, centres[0], cut, surface);
            if (capHead) Fan(rings, stations.Length - 1, count, along, centres[^1], cut, surface);
        }

        private void Fan(Vector3[,] rings, int s, int count, Vector3 outward, Vector3 centre, Vector3 colour, Surface surface)
        {
            Vector3 middle = Vector3.Zero;
            for (int k = 0; k < count; k++) middle += rings[s, k];
            middle /= count;
            Vector3 normal = Vector3.Zero;
            for (int k = 0; k < count; k++) normal += Vector3.Cross(rings[s, k] - middle, rings[s, (k + 1) % count] - middle);
            normal = normal.LengthSquared > 1e-12f ? normal.Normalized() : outward;
            if (Vector3.Dot(normal, outward) < 0f) normal = -normal;
            Frame(normal, out Vector3 u, out Vector3 v);
            int hub = Vertex(middle, normal, u, colour, new Vector2(0.5f), surface.Material);
            int first = Mesh.VertexCount;
            for (int k = 0; k < count; k++)
            {
                Vector3 p = rings[s, k] - middle;
                Vertex(rings[s, k], normal, u, colour, new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)) / surface.Tile + new Vector2(0.5f), surface.Material);
            }
            for (int k = 0; k < count; k++) Facing(hub, first + k, first + (k + 1) % count, middle - normal);
        }

        /// <summary>
        /// A dressed block: a box with its edges taken off by <paramref name="bevel"/>, each face and each bevel flat, so that light catches the edges as it does a cut stone's.
        /// <paramref name="turn"/> turns it about its middle.
        /// </summary>
        public void Block(Vector3 centre, Vector3 half, Matrix3 turn, float bevel, Surface surface, Faces skip = Faces.None, Func<Vector3, Vector3, Vector3>? colourAt = null)
        {
            bevel = MathF.Min(bevel, MathF.Min(half.X, MathF.Min(half.Y, half.Z)) * 0.45f);
            Vector3 inner = half - new Vector3(bevel);

            Vector3 Corner(int axis, float sign, float a, float b)
            {
                // A corner of the face across axis, at signs a and b along the other two.
                Vector3 p = Vector3.Zero;
                int first = (axis + 1) % 3, second = (axis + 2) % 3;
                p[axis] = sign * half[axis];
                p[first] = a * inner[first];
                p[second] = b * inner[second];
                return p;
            }

            Vector3 World(Vector3 local) => centre + local * turn;

            void Face(Vector3 la, Vector3 lb, Vector3 lc, Vector3 ld)
            {
                Vector3 a = World(la), b = World(lb), c = World(lc), d = World(ld);
                Vector3 normal = Vector3.Cross(b - a, d - a);
                if (normal.LengthSquared < 1e-12f) normal = Vector3.Cross(c - b, a - b);
                if (normal.LengthSquared < 1e-12f) return;
                normal.Normalize();
                if (Vector3.Dot(normal, (a + b + c + d) * 0.25f - centre) < 0f) normal = -normal;
                Frame(normal, out Vector3 u, out Vector3 v);
                Vector3 colour = colourAt?.Invoke((a + b + c + d) * 0.25f, normal) ?? surface.Colour;
                int Add(Vector3 p) => Vertex(p, normal, u, colour, new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)) / surface.Tile, surface.Material);
                int ia = Add(a), ib = Add(b), ic = Add(c), id = Add(d);
                FacingQuad(ia, ib, ic, id, centre);
            }

            void Triangle(Vector3 la, Vector3 lb, Vector3 lc)
            {
                Vector3 a = World(la), b = World(lb), c = World(lc);
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.LengthSquared < 1e-12f) return;
                normal.Normalize();
                if (Vector3.Dot(normal, (a + b + c) / 3f - centre) < 0f) normal = -normal;
                Frame(normal, out Vector3 u, out Vector3 v);
                Vector3 colour = colourAt?.Invoke((a + b + c) / 3f, normal) ?? surface.Colour;
                int Add(Vector3 p) => Vertex(p, normal, u, colour, new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)) / surface.Tile, surface.Material);
                Facing(Add(a), Add(b), Add(c), centre);
            }

            Faces[] negative = { Faces.NegativeX, Faces.NegativeY, Faces.NegativeZ };
            Faces[] positive = { Faces.PositiveX, Faces.PositiveY, Faces.PositiveZ };
            for (int axis = 0; axis < 3; axis++)
            {
                foreach (float sign in new[] { -1f, 1f })
                {
                    if ((skip & (sign < 0f ? negative[axis] : positive[axis])) != 0) continue;
                    Face(Corner(axis, sign, -1f, -1f), Corner(axis, sign, 1f, -1f), Corner(axis, sign, 1f, 1f), Corner(axis, sign, -1f, 1f));
                }
            }
            if (bevel <= 1e-4f) return;

            // The bevels along the edges, and a little triangle at each corner.
            for (int axis = 0; axis < 3; axis++)
            {
                int first = (axis + 1) % 3, second = (axis + 2) % 3;
                foreach (float sa in new[] { -1f, 1f })
                {
                    foreach (float sb in new[] { -1f, 1f })
                    {
                        // The edge running along axis, where the faces across first and second meet.
                        bool hiddenA = (skip & (sa < 0f ? negative[first] : positive[first])) != 0;
                        bool hiddenB = (skip & (sb < 0f ? negative[second] : positive[second])) != 0;
                        if (hiddenA && hiddenB) continue;
                        Vector3 a0 = Corner(first, sa, sb, -1f), a1 = Corner(first, sa, sb, 1f);
                        Vector3 b0 = Corner(second, sb, -1f, sa), b1 = Corner(second, sb, 1f, sa);
                        // Corner(first, ...) puts its third argument on second and its fourth on axis; Corner(second, ...) puts its third on axis and its fourth on first.
                        Face(a0, a1, b1, b0);
                    }
                }
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sy in new[] { -1f, 1f })
                {
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        Triangle(Corner(0, sx, sy, sz), Corner(1, sy, sz, sx), Corner(2, sz, sx, sy));
                    }
                }
            }
        }

        /// <summary>
        /// A stone as it comes out of a field: a block worn round, with a little wander at every corner, shaded soft. It's 44 triangles, or fewer with faces left out, as a wall
        /// has hundreds of them. Turned by <paramref name="turn"/> about its middle.
        /// </summary>
        public void Stone(Vector3 centre, Vector3 half, Matrix3 turn, float round, Surface surface, WorldRandom random, Faces skip = Faces.None, Func<Vector3, Vector3, Vector3>? colourAt = null)
        {
            float bevel = MathF.Min(half.X, MathF.Min(half.Y, half.Z)) * MathHelper.Lerp(0.2f, 0.45f, round);
            Vector3 inner = half - new Vector3(bevel);
            int first = Mesh.VertexCount;

            // The 24 corners of a bevelled box, three to each of its eight corners, one on each face. Each is shared by its face, two bevels and its corner.
            int Index(int axis, int sign, int a, int b) => ((axis * 2 + sign) * 2 + a) * 2 + b;
            var positions = new Vector3[24];
            for (int axis = 0; axis < 3; axis++)
            {
                int one = (axis + 1) % 3, two = (axis + 2) % 3;
                for (int sign = 0; sign < 2; sign++)
                {
                    for (int a = 0; a < 2; a++)
                    {
                        for (int b = 0; b < 2; b++)
                        {
                            Vector3 p = Vector3.Zero;
                            p[axis] = (sign * 2 - 1) * half[axis];
                            p[one] = (a * 2 - 1) * inner[one];
                            p[two] = (b * 2 - 1) * inner[two];
                            p += new Vector3(random.Range(-1f, 1f), random.Range(-1f, 1f), random.Range(-1f, 1f)) * (bevel * 0.35f);
                            positions[Index(axis, sign, a, b)] = p;
                        }
                    }
                }
            }

            Vector3 tint = surface.Colour * random.Range(0.88f, 1.12f);
            for (int i = 0; i < 24; i++)
            {
                Vector3 local = positions[i];
                Vector3 round3 = new Vector3(local.X / (half.X * half.X), local.Y / (half.Y * half.Y), local.Z / (half.Z * half.Z)).Normalized();
                int axis = i / 8;
                Vector3 face = Vector3.Zero;
                face[axis] = (i / 4) % 2 == 0 ? -1f : 1f;
                Vector3 normal = Vector3.Lerp(face, round3, MathHelper.Lerp(0.45f, 0.85f, round)).Normalized() * turn;
                Vector3 at = centre + local * turn;
                Vector3 colour = colourAt?.Invoke(at, normal) ?? tint;
                Vector3 tangent = Vector3.Cross(normal, MathF.Abs(normal.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY).Normalized();
                Vertex(at, normal.Normalized(), tangent, colour, new Vector2(at.X + at.Z, at.Y) / surface.Tile, surface.Material);
            }

            Faces[] negative = { Faces.NegativeX, Faces.NegativeY, Faces.NegativeZ };
            Faces[] positive = { Faces.PositiveX, Faces.PositiveY, Faces.PositiveZ };
            bool Hidden(int axis, int sign) => (skip & (sign == 0 ? negative[axis] : positive[axis])) != 0;

            for (int axis = 0; axis < 3; axis++)
            {
                for (int sign = 0; sign < 2; sign++)
                {
                    if (Hidden(axis, sign)) continue;
                    FacingQuad(first + Index(axis, sign, 0, 0), first + Index(axis, sign, 1, 0), first + Index(axis, sign, 1, 1), first + Index(axis, sign, 0, 1), centre);
                }
            }

            // A bevel joins the face across one axis to the face across the next, along the third.
            for (int axis = 0; axis < 3; axis++)
            {
                int one = (axis + 1) % 3, two = (axis + 2) % 3;
                for (int sa = 0; sa < 2; sa++)
                {
                    for (int sb = 0; sb < 2; sb++)
                    {
                        if (Hidden(one, sa) && Hidden(two, sb)) continue;
                        // On the face across one, its "a" runs along two and its "b" along axis; on the face across two, "a" runs along axis and "b" along one.
                        int a0 = first + Index(one, sa, sb, 0), a1 = first + Index(one, sa, sb, 1);
                        int b0 = first + Index(two, sb, 0, sa), b1 = first + Index(two, sb, 1, sa);
                        FacingQuad(a0, a1, b1, b0, centre);
                    }
                }
            }
            for (int sx = 0; sx < 2; sx++)
            {
                for (int sy = 0; sy < 2; sy++)
                {
                    for (int sz = 0; sz < 2; sz++)
                    {
                        if (Hidden(0, sx) && Hidden(1, sy) && Hidden(2, sz)) continue;
                        Facing(first + Index(0, sx, sy, sz), first + Index(1, sy, sz, sx), first + Index(2, sz, sx, sy), centre);
                    }
                }
            }
        }

        /// <summary>
        /// A flat face from <paramref name="origin"/> along <paramref name="u"/> and <paramref name="v"/>, in a grid fine enough to carry colour: a wall's weathering, its lichen
        /// and the stains under its coping. It faces <paramref name="outward"/>, and its texture runs in metres from where it starts.
        /// </summary>
        public void Panel(Vector3 origin, Vector3 u, Vector3 v, int cellsU, int cellsV, Vector3 outward, Surface surface, Func<Vector3, Vector3>? colourAt = null, Vector2 uvStart = default)
        {
            Vector3 normal = Vector3.Cross(u, v).Normalized();
            bool flip = Vector3.Dot(normal, outward) < 0f;
            if (flip) normal = -normal;
            Vector3 tangent = u.Normalized();
            float lengthU = u.Length, lengthV = v.Length;
            int first = Mesh.VertexCount;
            for (int j = 0; j <= cellsV; j++)
            {
                for (int i = 0; i <= cellsU; i++)
                {
                    float fu = i / (float)cellsU, fv = j / (float)cellsV;
                    Vector3 at = origin + u * fu + v * fv;
                    Vector3 colour = colourAt?.Invoke(at) ?? surface.Colour;
                    Vertex(at, normal, tangent, colour, uvStart + new Vector2(fu * lengthU, fv * lengthV) / surface.Tile, surface.Material);
                }
            }
            int row = cellsU + 1;
            for (int j = 0; j < cellsV; j++)
            {
                for (int i = 0; i < cellsU; i++)
                {
                    int a = first + j * row + i;
                    if (flip)
                    {
                        Mesh.Triangle(a, a + row + 1, a + 1);
                        Mesh.Triangle(a, a + row, a + row + 1);
                    }
                    else
                    {
                        Mesh.Triangle(a, a + 1, a + row + 1);
                        Mesh.Triangle(a, a + row + 1, a + row);
                    }
                }
            }
        }

        /// <summary>One flat four-cornered face, facing away from <paramref name="inside"/>.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside, Surface surface, Vector3? colour = null)
        {
            Vector3 normal = Vector3.Cross(b - a, d - a);
            if (normal.LengthSquared < 1e-12f) normal = Vector3.Cross(c - b, a - b);
            if (normal.LengthSquared < 1e-12f) return;
            normal.Normalize();
            if (Vector3.Dot(normal, (a + b + c + d) * 0.25f - inside) < 0f) normal = -normal;
            Frame(normal, out Vector3 u, out Vector3 v);
            Vector3 tint = colour ?? surface.Colour;
            int Add(Vector3 p) => Vertex(p, normal, u, tint, new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)) / surface.Tile, surface.Material);
            FacingQuad(Add(a), Add(b), Add(c), Add(d), inside);
        }

        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 inside, Surface surface, Vector3? colour = null)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.LengthSquared < 1e-12f) return;
            normal.Normalize();
            if (Vector3.Dot(normal, (a + b + c) / 3f - inside) < 0f) normal = -normal;
            Frame(normal, out Vector3 u, out Vector3 v);
            Vector3 tint = colour ?? surface.Colour;
            int Add(Vector3 p) => Vertex(p, normal, u, tint, new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)) / surface.Tile, surface.Material);
            Facing(Add(a), Add(b), Add(c), inside);
        }

        /// <summary>A ball, as on top of a pier: rings of latitude, shaded round.</summary>
        public void Ball(Vector3 centre, float radius, int sides, int rings, Surface surface)
        {
            int first = Mesh.VertexCount;
            for (int j = 0; j <= rings; j++)
            {
                float phi = MathF.PI * j / rings;
                for (int i = 0; i <= sides; i++)
                {
                    float theta = MathF.Tau * i / sides;
                    Vector3 n = new Vector3(MathF.Sin(phi) * MathF.Cos(theta), MathF.Cos(phi), MathF.Sin(phi) * MathF.Sin(theta));
                    Vector3 tangent = new Vector3(-MathF.Sin(theta), 0f, MathF.Cos(theta));
                    Vertex(centre + n * radius, n, tangent, surface.Colour, new Vector2(i / (float)sides, j / (float)rings) * (MathF.PI * radius / surface.Tile), surface.Material);
                }
            }
            int row = sides + 1;
            for (int j = 0; j < rings; j++)
            {
                for (int i = 0; i < sides; i++)
                {
                    int a = first + j * row + i;
                    FacingQuad(a, a + 1, a + row + 1, a + row, centre);
                }
            }
        }
    }

    public enum LogEnd
    {
        Open,
        Cut,
        Point,
    }
}
