using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Arena;
using Dino_Engine.Modelling.Procedural.Architecture.Waygates;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// What every place with a story is built of, whatever it is (PLACES.md): blocks and planks turned any way, poles, rope and chain that hang as rope and chain do, solids of revolution, cloth
    /// that sags, crates, barrels and sacks, lanterns, fires and braziers that light what's round them, and the Reich's mark. Everything here winds its faces to face outward whatever order its
    /// corners come in, as a face wound the wrong way renders black. Headless.
    /// </summary>
    internal static class Kit
    {
        // ---------------------------------------------------------------- Faces and blocks

        /// <summary>A quad wound to face <paramref name="want"/>, whichever way round its corners were given.</summary>
        public static void Face(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 want, VertexMaterial m)
        {
            if (Vector3.Dot(Vector3.Cross(p2 - p0, p3 - p1), want) < 0f) b.Quad(p0, p3, p2, p1, m);
            else b.Quad(p0, p1, p2, p3, m);
        }

        /// <summary>A triangle wound to face <paramref name="want"/>.</summary>
        public static void Face(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 want, VertexMaterial m)
        {
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), want) < 0f) b.Triangle(p0, p2, p1, m);
            else b.Triangle(p0, p1, p2, m);
        }

        /// <summary>A thin sheet seen from both sides: a sail, a cloth, a web. Its two faces lie together, as only one of them is ever drawn from where it's seen.</summary>
        public static void Sheet(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, VertexMaterial front, VertexMaterial back)
        {
            b.Quad(p0, p1, p2, p3, front);
            b.Quad(p0, p3, p2, p1, back);
        }

        /// <summary>
        /// A block of any eight corners making a convex solid, the front four and then the back four behind them in the same order, whichever way round they were given, its arrises taken off
        /// <paramref name="chamfer"/>: its winding is put right before it's laid, so a mirrored set of corners doesn't come out inside out.
        /// </summary>
        public static void Solid(MeshBuilder b, Vector3[] c, float chamfer, Func<Vector3, VertexMaterial> facing)
        {
            Vector3 middle = Vector3.Zero;
            for (int i = 0; i < 8; i++) middle += c[i];
            middle /= 8f;
            Vector3 front = (c[0] + c[1] + c[2] + c[3]) * 0.25f;
            Vector3 normal = Vector3.Cross(c[2] - c[0], c[3] - c[1]);
            if (Vector3.Dot(normal, front - middle) < 0f) c = new[] { c[1], c[0], c[3], c[2], c[5], c[4], c[7], c[6] };
            if (chamfer <= 0.001f) Plain(b, c, facing);
            else Masonry.Block(b, c, chamfer, facing);
        }

        public static void Solid(MeshBuilder b, Vector3[] c, float chamfer, VertexMaterial m) => Solid(b, c, chamfer, _ => m);

        // A convex block of eight corners with no arrises taken off: six quads.
        private static void Plain(MeshBuilder b, Vector3[] c, Func<Vector3, VertexMaterial> facing)
        {
            Vector3 middle = Vector3.Zero;
            for (int i = 0; i < 8; i++) middle += c[i];
            middle /= 8f;
            int[][] faces = { new[] { 0, 1, 2, 3 }, new[] { 5, 4, 7, 6 }, new[] { 1, 5, 6, 2 }, new[] { 4, 0, 3, 7 }, new[] { 3, 2, 6, 7 }, new[] { 4, 5, 1, 0 } };
            foreach (int[] f in faces)
            {
                Vector3 centre = (c[f[0]] + c[f[1]] + c[f[2]] + c[f[3]]) * 0.25f;
                Vector3 want = centre - middle;
                Face(b, c[f[0]], c[f[1]], c[f[2]], c[f[3]], want, facing(want.LengthSquared > 1e-12f ? want.Normalized() : Vector3.UnitY));
            }
        }

        /// <summary>A block about its middle on three axes (which needn't be square to the world), half its size along each.</summary>
        public static void Box(MeshBuilder b, Vector3 centre, Vector3 ax, Vector3 ay, Vector3 az, Vector3 half, float chamfer, Func<Vector3, VertexMaterial> facing)
        {
            Vector3 x = ax * half.X, y = ay * half.Y, z = az * half.Z;
            Solid(b, new[]
            {
                centre - x - y + z, centre + x - y + z, centre + x + y + z, centre - x + y + z,
                centre - x - y - z, centre + x - y - z, centre + x + y - z, centre - x + y - z,
            }, chamfer, facing);
        }

        public static void Box(MeshBuilder b, Vector3 centre, Vector3 ax, Vector3 ay, Vector3 az, Vector3 half, float chamfer, VertexMaterial m) => Box(b, centre, ax, ay, az, half, chamfer, _ => m);

        /// <summary>A block square to the model, turned by <paramref name="yaw"/> about y.</summary>
        public static void Block(MeshBuilder b, Vector3 centre, Vector3 half, float yaw, float chamfer, Func<Vector3, VertexMaterial> facing)
        {
            float c = MathF.Cos(yaw), s = MathF.Sin(yaw);
            Box(b, centre, new Vector3(c, 0f, -s), Vector3.UnitY, new Vector3(s, 0f, c), half, chamfer, facing);
        }

        public static void Block(MeshBuilder b, Vector3 centre, Vector3 half, float yaw, float chamfer, VertexMaterial m) => Block(b, centre, half, yaw, chamfer, _ => m);

        /// <summary>A block square to the model, from its least corner to its most.</summary>
        public static void Span(MeshBuilder b, Vector3 min, Vector3 max, float chamfer, VertexMaterial m) => Block(b, (min + max) * 0.5f, (max - min) * 0.5f, 0f, chamfer, m);

        public static void Span(MeshBuilder b, Vector3 min, Vector3 max, float chamfer, Func<Vector3, VertexMaterial> facing) => Block(b, (min + max) * 0.5f, (max - min) * 0.5f, 0f, chamfer, facing);

        /// <summary>
        /// A plank or a beam from one end to the other, <paramref name="width"/> across its face, which faces as near <paramref name="face"/> as its length allows, and
        /// <paramref name="thick"/> through.
        /// </summary>
        public static void Plank(MeshBuilder b, Vector3 from, Vector3 to, Vector3 face, float width, float thick, VertexMaterial m, float chamfer = 0.012f)
        {
            Vector3 along = to - from;
            float length = along.Length;
            if (length < 1e-4f) return;
            along /= length;
            Vector3 up = face - along * Vector3.Dot(face, along);
            if (up.LengthSquared < 1e-6f) up = MathF.Abs(along.Y) < 0.9f ? Vector3.UnitY - along * along.Y : Vector3.UnitX - along * along.X;
            up = up.Normalized();
            Vector3 across = Vector3.Cross(up, along).Normalized();
            Box(b, (from + to) * 0.5f, across, up, along, new Vector3(width * 0.5f, thick * 0.5f, length * 0.5f), MathF.Min(chamfer, MathF.Min(width, thick) * 0.3f), m);
        }

        /// <summary>A round pole, post or spar, with as few sides as a pole that thin is worth from up here.</summary>
        public static void Pole(MeshBuilder b, Vector3 from, Vector3 to, float radius, VertexMaterial m, float tip = -1f, bool capped = true)
        {
            int sides = radius < 0.06f ? 4 : radius < 0.16f ? 6 : radius < 0.4f ? 8 : 12;
            b.Tube(from, to, radius, tip < 0f ? radius : tip, sides, m, capped);
        }

        /// <summary>A pole bent through the points, thinning from one radius to the other, its joints closed by spheres' worth of overlap.</summary>
        public static void Bend(MeshBuilder b, IReadOnlyList<Vector3> points, float from, float to, VertexMaterial m)
        {
            for (int i = 1; i < points.Count; i++)
            {
                float r0 = MathHelper.Lerp(from, to, (i - 1f) / (points.Count - 1f));
                float r1 = MathHelper.Lerp(from, to, i / (points.Count - 1f));
                Vector3 a = points[i - 1], c = points[i];
                Vector3 d = (c - a).Normalized();
                // Each length runs a little into the next, so a bend shows no gap at its outside.
                Pole(b, i > 1 ? a - d * r0 * 0.5f : a, c, r0, m, r1, capped: i == points.Count - 1);
            }
        }

        // ---------------------------------------------------------------- What hangs

        /// <summary>Where a rope or chain hung from <paramref name="a"/> to <paramref name="c"/> is, <paramref name="t"/> along it: a parabola dropping <paramref name="sag"/> at its middle.</summary>
        public static Vector3 Hang(Vector3 a, Vector3 c, float sag, float t) => Vector3.Lerp(a, c, t) - Vector3.UnitY * (sag * 4f * t * (1f - t));

        /// <summary>A rope from one point to another, sagging.</summary>
        public static void Rope(MeshBuilder b, Vector3 a, Vector3 c, float sag, float radius, VertexMaterial m, int segments = 6)
        {
            var points = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++) points[i] = Hang(a, c, sag, i / (float)segments);
            for (int i = 1; i <= segments; i++) b.Tube(points[i - 1], points[i], radius, radius, 4, m, capped: false);
        }

        /// <summary>A chain from one point to another, sagging: links turned each a quarter to the last, flat boxes with their middles open, as a link at this size is.</summary>
        public static void Chain(MeshBuilder b, Vector3 a, Vector3 c, float sag, float link, VertexMaterial m)
        {
            float length = (c - a).Length + sag;
            int links = Math.Max(2, (int)MathF.Ceiling(length / (link * 0.78f)));
            for (int i = 0; i < links; i++)
            {
                float t0 = i / (float)links, t1 = (i + 1f) / links;
                Vector3 p0 = Hang(a, c, sag, t0), p1 = Hang(a, c, sag, t1);
                Vector3 along = p1 - p0;
                if (along.LengthSquared < 1e-8f) continue;
                along.Normalize();
                Vector3 side = MathF.Abs(along.Y) < 0.9f ? Vector3.Cross(along, Vector3.UnitY).Normalized() : Vector3.UnitX;
                Vector3 up = Vector3.Cross(side, along);
                Vector3 across = i % 2 == 0 ? side : up;
                Vector3 thin = i % 2 == 0 ? up : side;
                Vector3 mid = (p0 + p1) * 0.5f;
                float half = (p1 - p0).Length * 0.62f, wide = link * 0.36f, bar = link * 0.1f;
                // Two bars and two ends, so the link is open in its middle.
                Box(b, mid + across * (wide - bar), across, thin, along, new Vector3(bar, bar, half), 0f, m);
                Box(b, mid - across * (wide - bar), across, thin, along, new Vector3(bar, bar, half), 0f, m);
            }
        }

        // ---------------------------------------------------------------- Turned and draped

        /// <summary>
        /// A surface of revolution about <paramref name="axis"/> from <paramref name="foot"/>, its radius a function of how far along it is (0 to 1), facing out. A radius of nothing at
        /// either end closes it there.
        /// </summary>
        // Closed at both ends wherever an end has any width, as an open end is a hole from anywhere that can see into it, and the inside of a lathe isn't drawn:
        // only something meant to be seen into, with something of its own inside, leaves its far end open (openEnd).
        public static void Lathe(MeshBuilder b, Vector3 foot, Vector3 axis, float length, Func<float, float> radius, int around, int along, VertexMaterial m, bool openEnd = false)
        {
            Vector3 dir = axis.Normalized();
            Vector3 right = MathF.Abs(dir.Y) < 0.95f ? Vector3.Cross(dir, Vector3.UnitY).Normalized() : Vector3.UnitX;
            Vector3 up = Vector3.Cross(right, dir).Normalized();
            Vector3 Ring(int i, int k)
            {
                float t = i / (float)along;
                float a = k * MathF.Tau / around;
                return foot + dir * (length * t) + (right * MathF.Cos(a) + up * MathF.Sin(a)) * radius(t);
            }
            // Its texture is laid out over the whole lathe, not face by face, which started every face somewhere else in it and faded its relief at every face's
            // sides, so that a texture with any pattern in it broke up into noise: round it by the turn, a whole number of tiles at its middle radius so that it
            // meets itself with no seam (or as it comes, round anything too thin to take one), and along it by the metres up its side, its relief coming up to
            // nothing only at its two ends.
            float tiles = float.IsNaN(b.TilesPerMetre) ? MaterialCatalog.TilesPerMetre(m.materialTextureIndex) : b.TilesPerMetre;
            float middleRadius = 0f;
            for (int i = 0; i <= along; i++) middleRadius += radius(i / (float)along) / (along + 1);
            float turn = MathF.Tau * middleRadius * tiles;
            float perFace = (turn >= 1f ? MathF.Round(turn) : turn) / tiles / around;
            var climb = new float[along + 1];
            for (int i = 1; i <= along; i++) climb[i] = climb[i - 1] + (Ring(i, 0) - Ring(i - 1, 0)).Length;
            float side = climb[along];
            Vector2 At(int i, int k) => new Vector2(k * perFace, climb[i]);
            Vector4 Edges(Vector2 at) => new Vector4(1e6f, 1e6f, at.Y, side - at.Y);
            void Triangle(Vector3 pa, Vector3 pb, Vector3 pc, Vector2 ma, Vector2 mb, Vector2 mc, Vector3 want)
            {
                if (Vector3.Dot(Vector3.Cross(pb - pa, pc - pa), want) < 0f)
                {
                    (pb, pc) = (pc, pb);
                    (mb, mc) = (mc, mb);
                }
                b.MappedTriangle(pa, pb, pc, m, ma, mb, mc, Edges(ma), Edges(mb), Edges(mc));
            }
            for (int i = 0; i < along; i++)
            {
                for (int k = 0; k < around; k++)
                {
                    Vector3 p0 = Ring(i, k), p1 = Ring(i, k + 1), p2 = Ring(i + 1, k + 1), p3 = Ring(i + 1, k);
                    Vector2 m0 = At(i, k), m1 = At(i, k + 1), m2 = At(i + 1, k + 1), m3 = At(i + 1, k);
                    Vector3 middle = foot + dir * (length * (i + 0.5f) / along);
                    Vector3 want = (p0 + p1 + p2 + p3) * 0.25f - middle;
                    // A ring of nothing is a point, which makes the quad a triangle.
                    bool pointBelow = (p0 - p1).LengthSquared < 1e-10f, pointAbove = (p2 - p3).LengthSquared < 1e-10f;
                    if (!pointBelow) Triangle(p0, p1, p2, m0, m1, m2, want);
                    if (!pointAbove) Triangle(p0, p2, p3, m0, m2, m3, want);
                }
            }
            float r0 = radius(0f), r1 = radius(1f);
            if (r0 > 1e-4f) Cap(b, foot, -dir, right, up, r0, around, m);
            if (r1 > 1e-4f && !openEnd) Cap(b, foot + dir * length, dir, right, up, r1, around, m);
        }

        // A flat disc over a lathe's end, its rim on the lathe's own last ring, so that the two meet with no crack.
        private static void Cap(MeshBuilder b, Vector3 centre, Vector3 facing, Vector3 right, Vector3 up, float radius, int around, VertexMaterial m)
        {
            for (int k = 0; k < around; k++)
            {
                float a0 = k * MathF.Tau / around, a1 = (k + 1) * MathF.Tau / around;
                Vector3 p0 = centre + (right * MathF.Cos(a0) + up * MathF.Sin(a0)) * radius;
                Vector3 p1 = centre + (right * MathF.Cos(a1) + up * MathF.Sin(a1)) * radius;
                Face(b, centre, p0, p1, facing, m);
            }
        }

        /// <summary>A smooth ball, closed at its poles, with these radii along x, y and z: a snowball, a snowman's body.</summary>
        // Not a MeshBuilder surface, whose rows each have vertices of their own at both ends and at a pole: normals are averaged over the faces sharing a vertex,
        // so a pole's leaned (a dark dot on top of every snowball) and a seam creased, and a surface measures its texture across a row from end to end, which on a
        // ring is nothing (the snow smeared round every ball in stripes). Here every point is one vertex, a pole included, and the texture is laid out by distance
        // from the top pole along the surface, which is exact on top, a little stretched round the middle and only gives out underneath, where nothing sees it.
        public static void Ball(MeshBuilder b, Vector3 centre, Vector3 radii, int around, int up, VertexMaterial m)
        {
            around = Math.Max(around, 3);
            up = Math.Max(up, 2);
            float perRadian = (radii.X + radii.Y + radii.Z) / 3f * MaterialCatalog.TilesPerMetre(m.materialTextureIndex);
            var vertices = new List<Vertex>(2 + around * (up - 1));
            var indices = new List<int>(around * (up - 1) * 6);
            void Point(float polar, float a)
            {
                Vector3 p = centre + new Vector3(MathF.Sin(polar) * MathF.Cos(a) * radii.X, MathF.Cos(polar) * radii.Y, -MathF.Sin(polar) * MathF.Sin(a) * radii.Z);
                vertices.Add(new Vertex(p, m, new Vector2(MathF.Cos(a), MathF.Sin(a)) * (polar * perRadian)));
            }
            Point(0f, 0f);
            for (int j = 1; j < up; j++)
            {
                for (int k = 0; k < around; k++) Point(MathF.PI * j / up, k * MathF.Tau / around);
            }
            Point(MathF.PI, 0f);
            int bottom = vertices.Count - 1;
            int Ring(int j, int k) => 1 + (j - 1) * around + k % around;
            // Each wound to face out from the middle, as everything here is.
            void Triangle(int i0, int i1, int i2)
            {
                Vector3 p0 = vertices[i0].position, p1 = vertices[i1].position, p2 = vertices[i2].position;
                bool outward = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), (p0 + p1 + p2) / 3f - centre) >= 0f;
                indices.Add(i0);
                indices.Add(outward ? i1 : i2);
                indices.Add(outward ? i2 : i1);
            }
            for (int k = 0; k < around; k++)
            {
                Triangle(0, Ring(1, k), Ring(1, k + 1));
                for (int j = 1; j < up - 1; j++)
                {
                    Triangle(Ring(j, k), Ring(j + 1, k), Ring(j + 1, k + 1));
                    Triangle(Ring(j, k), Ring(j + 1, k + 1), Ring(j, k + 1));
                }
                Triangle(Ring(up - 1, k), bottom, Ring(up - 1, k + 1));
            }
            b.Add(new Mesh(vertices, indices), Vector3.Zero, Vector3.One);
        }

        /// <summary>A radius along a lathe, as straight runs between the given points (how far along, radius).</summary>
        public static Func<float, float> Profile(params (float T, float R)[] points) => t =>
        {
            for (int i = 1; i < points.Length; i++)
            {
                if (t > points[i].T) continue;
                float span = points[i].T - points[i - 1].T;
                float s = span <= 1e-6f ? 1f : (t - points[i - 1].T) / span;
                return MathHelper.Lerp(points[i - 1].R, points[i].R, s);
            }
            return points[^1].R;
        };

        /// <summary>
        /// A cloth between four corners, sagging <paramref name="sag"/> in its middle and rippled by <paramref name="ripple"/> (of u and v, in metres along its normal), seen from both sides.
        /// </summary>
        public static void Cloth(MeshBuilder b, Vector3 p00, Vector3 p10, Vector3 p11, Vector3 p01, float sag, int steps, VertexMaterial front, VertexMaterial back, Func<float, float, float>? ripple = null)
        {
            Vector3 normal = Vector3.Cross(p10 - p00, p01 - p00);
            normal = normal.LengthSquared > 1e-10f ? normal.Normalized() : Vector3.UnitY;
            Vector3 Point(float u, float v)
            {
                Vector3 p = Vector3.Lerp(Vector3.Lerp(p00, p10, u), Vector3.Lerp(p01, p11, u), v);
                float droop = sag * 16f * u * (1f - u) * v * (1f - v);
                return p - Vector3.UnitY * droop + normal * (ripple?.Invoke(u, v) ?? 0f);
            }
            for (int j = 0; j < steps; j++)
            {
                for (int i = 0; i < steps; i++)
                {
                    float u0 = i / (float)steps, u1 = (i + 1f) / steps, v0 = j / (float)steps, v1 = (j + 1f) / steps;
                    Sheet(b, Point(u0, v0), Point(u1, v0), Point(u1, v1), Point(u0, v1), front, back);
                }
            }
        }

        /// <summary>A mound of earth, sand or spoil, its height over the ground falling off from its middle as a heap does, over an ellipse turned by <paramref name="yaw"/>.</summary>
        public static void Heap(MeshBuilder b, Vector3 centre, Vector2 radii, float height, float yaw, VertexMaterial m, WorldRandom random, int rings = 5, int around = 14)
        {
            float c = MathF.Cos(yaw), s = MathF.Sin(yaw);
            float[] bumps = new float[around];
            for (int k = 0; k < around; k++) bumps[k] = random.Range(0.85f, 1.15f);
            Vector3 At(int ring, int k)
            {
                float t = ring / (float)rings;
                float a = k * MathF.Tau / around;
                float bump = bumps[k % around];
                float x = MathF.Cos(a) * radii.X * t * bump, z = MathF.Sin(a) * radii.Y * t * bump;
                float y = height * (1f - t * t) * (0.85f + 0.15f * bump) + (ring == rings ? -0.25f : 0f);
                return centre + new Vector3(x * c + z * s, y, -x * s + z * c);
            }
            Vector3 top = centre + Vector3.UnitY * height;
            for (int k = 0; k < around; k++) Face(b, top, At(1, k), At(1, k + 1), Vector3.UnitY, m);
            for (int ring = 1; ring < rings; ring++)
            {
                for (int k = 0; k < around; k++)
                {
                    Vector3 p0 = At(ring, k), p1 = At(ring, k + 1), p2 = At(ring + 1, k + 1), p3 = At(ring + 1, k);
                    Vector3 out_ = (p0 + p1) * 0.5f - centre;
                    Face(b, p0, p1, p2, p3, new Vector3(out_.X, MathF.Max(out_.Length * 0.6f, 0.05f), out_.Z), m);
                }
            }
        }

        // ---------------------------------------------------------------- Clutter

        /// <summary>A crate of boards with battens round its edges, a lid on top, and on the Reich's its mark, brass on oxblood, on the faces that show.</summary>
        public static void Crate(MeshBuilder b, Vector3 centre, float yaw, Vector3 half, VertexMaterial boards, VertexMaterial battens, WorldRandom random, bool marked = false)
        {
            Block(b, centre, half - new Vector3(0.02f), yaw, 0.01f, boards);
            float c = MathF.Cos(yaw), s = MathF.Sin(yaw);
            Vector3 ax = new Vector3(c, 0f, -s), az = new Vector3(s, 0f, c);
            const float Batten = 0.07f;
            // The battens up the corners and round the top and the foot.
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    Vector3 corner = centre + ax * (sx * (half.X - Batten * 0.5f)) + az * (sz * (half.Z - Batten * 0.5f));
                    Box(b, corner, ax, Vector3.UnitY, az, new Vector3(Batten * 0.55f, half.Y, Batten * 0.55f), 0.008f, battens);
                }
            }
            foreach (float sy in new[] { -1f, 1f })
            {
                float y = sy * (half.Y - Batten * 0.5f);
                foreach (float sz in new[] { -1f, 1f }) Box(b, centre + Vector3.UnitY * y + az * (sz * (half.Z - Batten * 0.4f)), ax, Vector3.UnitY, az, new Vector3(half.X, Batten * 0.5f, Batten * 0.45f), 0.008f, battens);
                foreach (float sx in new[] { -1f, 1f }) Box(b, centre + Vector3.UnitY * y + ax * (sx * (half.X - Batten * 0.4f)), ax, Vector3.UnitY, az, new Vector3(Batten * 0.45f, Batten * 0.5f, half.Z), 0.008f, battens);
            }
            if (!marked) return;
            // The mark, burnt and painted onto the faces the camera sees: the front and the top.
            float size = MathF.Min(half.X, half.Y) * 1.15f;
            Mark(b, centre + az * (half.Z + 0.005f), ax, Vector3.UnitY, size, 0.012f, random);
            Mark(b, centre + Vector3.UnitY * (half.Y + 0.005f), ax, -az, MathF.Min(half.X, half.Z) * 1.15f, 0.012f, random);
        }

        /// <summary>The Reich's mark, the first tyrant's skull, in brass on an oxblood field, flat against a face: right and up in that face, <paramref name="size"/> across.</summary>
        public static void Mark(MeshBuilder b, Vector3 centre, Vector3 right, Vector3 up, float size, float thick, WorldRandom random)
        {
            Vector3 normal = Vector3.Cross(right, up).Normalized();
            VertexMaterial field = Oxblood(), brass = Brass(random);
            Vector3 r = right * (size * 0.62f), u = up * (size * 0.5f);
            Face(b, centre - r - u, centre + r - u, centre + r + u, centre - r + u, normal, field);
            ArenaStone.Mark(b, centre + normal * 0.004f, right, up, size, thick, brass, field);
        }

        /// <summary>A barrel of staves bound in hoops, standing, or lying on its side along <paramref name="yaw"/>'s x.</summary>
        public static void Barrel(MeshBuilder b, Vector3 foot, float yaw, bool lying, float radius, float height, VertexMaterial wood, VertexMaterial hoops, VertexMaterial? head = null)
        {
            float c = MathF.Cos(yaw), s = MathF.Sin(yaw);
            Vector3 axis = lying ? new Vector3(c, 0f, -s) : Vector3.UnitY;
            Vector3 start = lying ? foot + Vector3.UnitY * radius - axis * (height * 0.5f) : foot;
            Func<float, float> belly = t => radius * (0.86f + 0.14f * MathF.Sin(t * MathF.PI));
            Lathe(b, start, axis, height, belly, 10, 4, wood);
            // Its ends, closed, a little in from the rim.
            foreach (float end in new[] { 0.03f, 0.97f })
            {
                Vector3 centre = start + axis * (height * end);
                float r = belly(end) * 0.94f;
                Vector3 right = MathF.Abs(axis.Y) < 0.95f ? Vector3.Cross(axis, Vector3.UnitY).Normalized() : Vector3.UnitX;
                Vector3 up = Vector3.Cross(right, axis).Normalized();
                Vector3 want = end < 0.5f ? -axis : axis;
                for (int k = 0; k < 10; k++)
                {
                    float a0 = k * MathF.Tau / 10f, a1 = (k + 1) * MathF.Tau / 10f;
                    Face(b, centre, centre + (right * MathF.Cos(a0) + up * MathF.Sin(a0)) * r, centre + (right * MathF.Cos(a1) + up * MathF.Sin(a1)) * r, want, head ?? wood);
                }
            }
            foreach (float t in new[] { 0.12f, 0.36f, 0.64f, 0.88f })
            {
                Vector3 at = start + axis * (height * t);
                b.Tube(at - axis * 0.035f, at + axis * 0.035f, belly(t) + 0.012f, belly(t) + 0.012f, 10, hoops, capped: false);
            }
        }

        /// <summary>A sack of grain or meal, tied at the neck, standing or fallen over.</summary>
        public static void Sack(MeshBuilder b, Vector3 foot, float yaw, bool fallen, float size, VertexMaterial cloth)
        {
            float c = MathF.Cos(yaw), s = MathF.Sin(yaw);
            Vector3 axis = fallen ? new Vector3(c, 0.12f, -s).Normalized() : Vector3.UnitY;
            Vector3 start = fallen ? foot + Vector3.UnitY * (size * 0.34f) - axis * (size * 0.5f) : foot;
            Lathe(b, start, axis, size * 1.1f, Profile((0f, 0f), (0.06f, size * 0.36f), (0.4f, size * 0.42f), (0.78f, size * 0.36f), (0.9f, size * 0.12f), (0.95f, size * 0.14f), (1f, 0f)), 9, 6, cloth);
        }

        /// <summary>A coil of rope lying on the ground.</summary>
        public static void Coil(MeshBuilder b, Vector3 centre, float radius, VertexMaterial rope, WorldRandom random)
        {
            int turns = random.Int(3, 5);
            for (int t = 0; t < turns; t++)
            {
                float r = radius * (1f - t * 0.12f);
                float y = 0.05f + t * 0.07f;
                const int Steps = 10;
                for (int k = 0; k < Steps; k++)
                {
                    float a0 = k * MathF.Tau / Steps, a1 = (k + 1) * MathF.Tau / Steps;
                    b.Tube(centre + new Vector3(MathF.Cos(a0) * r, y, MathF.Sin(a0) * r), centre + new Vector3(MathF.Cos(a1) * r, y, MathF.Sin(a1) * r), 0.055f, 0.055f, 4, rope, capped: false);
                }
            }
        }

        // ---------------------------------------------------------------- What gives light

        /// <summary>A lantern: a cap and a frame round panes lit from inside, hung from a ring, its light just under it. <paramref name="at"/> is where it hangs from.</summary>
        public static void Lantern(MeshBuilder b, Vector3 at, VertexMaterial frame, List<AssetLight> lights, float size = 1f)
        {
            float w = 0.22f * size, h = 0.42f * size;
            Vector3 middle = at - Vector3.UnitY * (0.12f * size + h * 0.5f);
            Span(b, middle - new Vector3(w * 0.9f, h * 0.5f, w * 0.9f), middle + new Vector3(w * 0.9f, h * 0.5f, w * 0.9f), 0f, LampGlow());
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f }) Pole(b, middle + new Vector3(sx * w, -h * 0.5f, sz * w), middle + new Vector3(sx * w, h * 0.5f, sz * w), 0.022f * size, frame);
            }
            Span(b, middle + new Vector3(-w * 1.15f, h * 0.5f, -w * 1.15f), middle + new Vector3(w * 1.15f, h * 0.5f + 0.06f * size, w * 1.15f), 0.01f, frame);
            Lathe(b, middle + Vector3.UnitY * (h * 0.5f + 0.06f * size), Vector3.UnitY, 0.12f * size, Profile((0f, w * 1.05f), (1f, 0.02f)), 6, 1, frame);
            Span(b, middle - new Vector3(w * 1.05f, h * 0.5f + 0.05f * size, w * 1.05f), middle + new Vector3(w * 1.05f, -h * 0.5f, w * 1.05f), 0.01f, frame);
            Pole(b, at - Vector3.UnitY * 0.13f * size, at, 0.018f * size, frame);
            lights.Add(PlaceMaterials.Lantern(middle - Vector3.UnitY * 0.05f));
        }

        /// <summary>A lantern on a post of timber with an arm off its head, <paramref name="height"/> tall, hung towards +z.</summary>
        public static void LanternPost(MeshBuilder b, float height, VertexMaterial wood, VertexMaterial frame, List<AssetLight> lights)
        {
            Span(b, new Vector3(-0.16f, -0.4f, -0.16f), new Vector3(0.16f, height, 0.16f), 0.025f, wood);
            Plank(b, new Vector3(0f, height - 0.25f, -0.1f), new Vector3(0f, height - 0.25f, 0.95f), Vector3.UnitY, 0.12f, 0.12f, wood);
            Plank(b, new Vector3(0f, height - 0.95f, 0.12f), new Vector3(0f, height - 0.32f, 0.72f), Vector3.UnitZ, 0.09f, 0.08f, wood);
            Lantern(b, new Vector3(0f, height - 0.31f, 0.86f), frame, lights);
        }

        /// <summary>A fire in a ring of stones, a few logs burning in it: its flame and its light. Its foot is on the ground.</summary>
        public static void Fire(MeshBuilder b, Vector3 at, float radius, WorldRandom random, List<AssetLight> lights, bool lit = true)
        {
            int stones = Math.Max(7, (int)(radius * 9f));
            for (int i = 0; i < stones; i++)
            {
                float a = i * MathF.Tau / stones + random.Range(-0.08f, 0.08f);
                Vector3 p = at + new Vector3(MathF.Cos(a) * radius, Ground, MathF.Sin(a) * radius);
                Block(b, p + Vector3.UnitY * 0.06f, new Vector3(random.Range(0.13f, 0.2f), random.Range(0.1f, 0.15f), random.Range(0.11f, 0.17f)), a, 0.035f, Flat(new Vector3(0.36f, 0.34f, 0.31f) * random.Range(0.85f, 1.1f)));
            }
            VertexMaterial charred = new VertexMaterial(MaterialCatalog.BarkCharred.Index, new Colour(0.16f, 0.13f, 0.11f));
            for (int i = 0; i < 4; i++)
            {
                float a = i * MathF.PI * 0.5f + random.Range(-0.3f, 0.3f);
                Vector3 d = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                Pole(b, at + d * (radius * 0.85f) + Vector3.UnitY * (Ground + 0.06f), at + Vector3.UnitY * (Ground + 0.42f), 0.08f, charred, 0.05f);
            }
            if (!lit) return;
            Heap(b, at + Vector3.UnitY * Ground, new Vector2(radius * 0.55f), 0.1f, 0f, Coals, random, 2, 8);
            lights.Add(AssetLight.Burning(at + Vector3.UnitY * (Ground + 0.1f), radius * 0.9f));
            lights.Add(PlaceMaterials.Fire(at + Vector3.UnitY * 1.2f));
        }

        /// <summary>A brazier of the Reich's iron on three legs, its coals burning. Its foot is on the ground, and it stands <paramref name="height"/> tall.</summary>
        public static void Brazier(MeshBuilder b, Vector3 at, float height, WorldRandom random, List<AssetLight> lights, VertexMaterial? metal = null)
        {
            VertexMaterial iron = metal ?? Iron(random);
            Vector3 bowl = at + Vector3.UnitY * height;
            for (int i = 0; i < 3; i++)
            {
                float a = i * MathF.Tau / 3f;
                Pole(b, at + new Vector3(MathF.Cos(a) * 0.42f, 0f, MathF.Sin(a) * 0.42f), bowl - Vector3.UnitY * 0.12f + new Vector3(MathF.Cos(a) * 0.3f, 0f, MathF.Sin(a) * 0.3f), 0.035f, iron);
            }
            Lathe(b, bowl - Vector3.UnitY * 0.3f, Vector3.UnitY, 0.32f, Profile((0f, 0.12f), (0.4f, 0.38f), (1f, 0.48f)), 10, 2, iron);
            Heap(b, bowl - Vector3.UnitY * 0.02f, new Vector2(0.42f), 0.1f, 0f, Coals, random, 2, 8);
            lights.Add(AssetLight.Burning(bowl, 0.5f));
            lights.Add(PlaceMaterials.Brazier(bowl + Vector3.UnitY * 0.5f));
        }
    }
}
