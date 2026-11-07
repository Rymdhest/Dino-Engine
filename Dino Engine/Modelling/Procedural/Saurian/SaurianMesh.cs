using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    /// <summary>Which bones a vertex hangs from, and how much of it from the first. Two at most, which is what the shaders read.</summary>
    public readonly record struct Hang(BoneType First, BoneType Second = BoneType.Chest, float Share = 1f)
    {
        public static implicit operator Hang(BoneType bone) => new Hang(bone);
    }

    /// <summary>
    /// Everything a dinosaur's mesh is made of, as plain lists: a skin with its normals worked out from its field, and the small hard parts that go on it. Normals are its own, never worked out again from the
    /// faces, so a skin split for its texture has no seam in its light.
    /// </summary>
    public sealed class SaurianMesh
    {
        public readonly List<Vector3> Positions = new();
        public readonly List<Vector3> Normals = new();
        public readonly List<Vector3> Tangents = new();
        public readonly List<Vector2> Uvs = new();
        public readonly List<Vector3> Colours = new();
        public readonly List<float> Glows = new();
        public readonly List<int> Materials = new();
        public readonly List<Hang> Hangs = new();
        public readonly List<int> Indices = new();

        public int VertexCount => Positions.Count;
        public int TriangleCount => Indices.Count / 3;

        public int Vertex(Vector3 position, Vector3 normal, Vector3 tangent, Vector2 uv, Vector3 colour, int material, Hang hang, float glow = 1f)
        {
            Positions.Add(position);
            Normals.Add(normal);
            Tangents.Add(tangent);
            Uvs.Add(uv);
            Colours.Add(colour);
            Glows.Add(glow);
            Materials.Add(material);
            Hangs.Add(hang);
            return Positions.Count - 1;
        }

        public void Triangle(int a, int b, int c)
        {
            Indices.Add(a);
            Indices.Add(b);
            Indices.Add(c);
        }

        private static Vector3 AnyTangent(Vector3 normal) => Vector3.Cross(normal, MathF.Abs(normal.Y) < 0.95f ? Vector3.UnitY : Vector3.UnitX).Normalized();

        /// <summary>
        /// A tapering tube along a path, closed at both ends, with smooth sides: for toes, fingers, horns and claws, which are a curved cone. A radius of nothing at the end makes a point.
        /// </summary>
        public void Tube(IReadOnlyList<Vector3> path, IReadOnlyList<float> radii, int sides, Vector3 colour, int material, Hang hang, Vector3? flatten = null, float flat = 1f, float glow = 1f)
        {
            int rings = path.Count;
            if (rings < 2) return;
            int first = Positions.Count;
            Vector3 previousAcross = Vector3.Zero;
            float run = 0f;

            for (int r = 0; r < rings; r++)
            {
                Vector3 along = r == 0 ? path[1] - path[0] : r == rings - 1 ? path[r] - path[r - 1] : path[r + 1] - path[r - 1];
                along = along.LengthSquared > 1e-12f ? along.Normalized() : Vector3.UnitZ;
                Vector3 across;
                if (r == 0)
                {
                    Vector3 seed = flatten ?? (MathF.Abs(along.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX);
                    across = Vector3.Cross(along, seed);
                    if (across.LengthSquared < 1e-8f) across = Vector3.Cross(along, Vector3.UnitX);
                    across.Normalize();
                }
                else
                {
                    // Carried along, so the tube doesn't twist where its path turns.
                    across = previousAcross - along * Vector3.Dot(previousAcross, along);
                    across = across.LengthSquared > 1e-8f ? across.Normalized() : AnyTangent(along);
                }
                previousAcross = across;
                Vector3 up = Vector3.Cross(across, along);
                if (r > 0) run += (path[r] - path[r - 1]).Length;

                for (int s = 0; s <= sides; s++)
                {
                    float angle = s * MathF.Tau / sides;
                    float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
                    Vector3 outward = (across * cos + up * sin * flat).Normalized();
                    Vector3 offset = (across * cos + up * sin * flat) * radii[r];
                    Vertex(path[r] + offset, outward, along, new Vector2(run, s / (float)sides), colour, material, hang, glow);
                }
            }

            for (int r = 0; r + 1 < rings; r++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = first + r * (sides + 1) + s;
                    int b = a + sides + 1;
                    Triangle(a, b, a + 1);
                    Triangle(a + 1, b, b + 1);
                }
            }

            Cap(path[0], -(path[1] - path[0]).Normalized(), first, sides, colour, material, hang, glow, true);
            Cap(path[^1], (path[^1] - path[^2]).Normalized(), first + (rings - 1) * (sides + 1), sides, colour, material, hang, glow, false);
        }

        private void Cap(Vector3 centre, Vector3 normal, int ring, int sides, Vector3 colour, int material, Hang hang, float glow, bool start)
        {
            int middle = Vertex(centre, normal, AnyTangent(normal), Vector2.Zero, colour, material, hang, glow);
            for (int s = 0; s < sides; s++)
            {
                int a = Vertex(Positions[ring + s], normal, AnyTangent(normal), Vector2.Zero, colour, material, hang, glow);
                int b = Vertex(Positions[ring + s + 1], normal, AnyTangent(normal), Vector2.Zero, colour, material, hang, glow);
                if (start) Triangle(middle, a, b);
                else Triangle(middle, b, a);
            }
        }

        /// <summary>A curved, pointed thing: a tooth, a claw, a horn or a spike. It bends towards <paramref name="bend"/> as it goes, by <paramref name="curve"/> of its length.</summary>
        public void Spike(Vector3 root, Vector3 way, float length, float radius, Vector3 bend, float curve, int sides, Vector3 colour, int material, Hang hang, float flat = 1f, Vector3? flatten = null, float glow = 1f)
        {
            const int Rings = 5;
            var path = new Vector3[Rings];
            var radii = new float[Rings];
            Vector3 straight = way.Normalized();
            Vector3 toward = bend - straight * Vector3.Dot(bend, straight);
            if (toward.LengthSquared > 1e-8f) toward.Normalize();
            for (int i = 0; i < Rings; i++)
            {
                float t = i / (Rings - 1f);
                path[i] = root + straight * (length * t) + toward * (length * curve * t * t);
                radii[i] = radius * MathF.Pow(1f - t, 0.85f) + (i == Rings - 1 ? 0f : radius * 0.02f);
            }
            Tube(path, radii, sides, colour, material, hang, flatten, flat, glow);
        }

        /// <summary>A ball, for an eye or a knuckle, from a subdivided octahedron.</summary>
        public void Ball(Vector3 centre, Vector3 radii, int detail, Vector3 colour, int material, Hang hang, float glow = 1f)
        {
            Vector3[] corners = { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ };
            int[] faces = { 0, 2, 4, 2, 1, 4, 1, 3, 4, 3, 0, 4, 2, 0, 5, 1, 2, 5, 3, 1, 5, 0, 3, 5 };
            for (int f = 0; f < faces.Length; f += 3)
            {
                Vector3 a = corners[faces[f]], b = corners[faces[f + 1]], c = corners[faces[f + 2]];
                int n = 1 << detail;
                int Point(int i, int j)
                {
                    Vector3 p = (a * (n - i - j) + b * i + c * j) / n;
                    p.Normalize();
                    return Vertex(centre + p * radii, (p / radii).Normalized(), AnyTangent(p), new Vector2(p.X, p.Y), colour, material, hang, glow);
                }
                var row = new int[n + 1][];
                for (int i = 0; i <= n; i++)
                {
                    row[i] = new int[n + 1 - i];
                    for (int j = 0; j <= n - i; j++) row[i][j] = Point(i, j);
                }
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n - i; j++)
                    {
                        Triangle(row[i][j], row[i + 1][j], row[i][j + 1]);
                        if (j + 1 < n - i) Triangle(row[i + 1][j], row[i + 1][j + 1], row[i][j + 1]);
                    }
                }
            }
        }

        /// <summary>A flat, thick sheet bounded by an outline, for a frill, a crest or a plate: its two faces and the edge between them.</summary>
        public void Sheet(IReadOnlyList<Vector3> outline, Vector3 centre, Vector3 normal, float thickness, Vector3 colour, Vector3 rim, int material, Func<Vector3, Hang> hang)
        {
            int count = outline.Count;
            Vector3 up = normal.Normalized();
            Vector3 half = up * (thickness * 0.5f);
            Vector3 tangent = AnyTangent(up);

            // Whichever way round the outline was given, each face is wound to face out.
            float turning = 0f;
            for (int i = 0; i < count; i++) turning += Vector3.Dot(Vector3.Cross(outline[i] - centre, outline[(i + 1) % count] - centre), up);
            bool forwards = turning >= 0f;

            for (int side = 0; side < 2; side++)
            {
                Vector3 offset = side == 0 ? half : -half;
                Vector3 facing = side == 0 ? up : -up;
                int middle = Vertex(centre + offset, facing, tangent, Vector2.Zero, colour, material, hang(centre));
                int start = Positions.Count;
                for (int i = 0; i < count; i++) Vertex(outline[i] + offset, facing, tangent, Vector2.Zero, rim, material, hang(outline[i]));
                for (int i = 0; i < count; i++)
                {
                    int a = start + i, b = start + (i + 1) % count;
                    if ((side == 0) == forwards) Triangle(middle, a, b);
                    else Triangle(middle, b, a);
                }
            }
            for (int i = 0; i < count; i++)
            {
                Vector3 p = outline[i], q = outline[(i + 1) % count];
                Vector3 out_ = Vector3.Cross(q - p, up);
                if (out_.LengthSquared < 1e-12f) continue;
                out_.Normalize();
                if (Vector3.Dot(out_, (p + q) * 0.5f - centre) < 0f) out_ = -out_;
                int a = Vertex(p + half, out_, tangent, Vector2.Zero, rim, material, hang(p));
                int b = Vertex(q + half, out_, tangent, Vector2.Zero, rim, material, hang(q));
                int c = Vertex(q - half, out_, tangent, Vector2.Zero, rim, material, hang(q));
                int d = Vertex(p - half, out_, tangent, Vector2.Zero, rim, material, hang(p));
                bool outward = Vector3.Dot(Vector3.Cross(q - p, -half), out_) > 0f;
                if (outward)
                {
                    Triangle(a, b, c);
                    Triangle(a, c, d);
                }
                else
                {
                    Triangle(a, c, b);
                    Triangle(a, d, c);
                }
            }
        }

        /// <summary>All of it as an engine mesh, with its own normals kept.</summary>
        public Mesh Build()
        {
            var vertices = new List<Vertex>(Positions.Count);
            for (int i = 0; i < Positions.Count; i++)
            {
                Vector3 c = Colours[i];
                Hang hang = Hangs[i];
                float share = Math.Clamp(hang.Share, 0f, 1f);
                vertices.Add(new Vertex(Positions[i], Materials[i], new Colour(c.X, c.Y, c.Z, Glows[i]),
                    new Vector2((int)hang.First, (int)hang.Second), new Vector2(share, 1f - share), Uvs[i]));
            }

            var mesh = new Mesh(vertices, Indices);
            for (int i = 0; i < mesh.meshVertices.Count; i++)
            {
                MeshVertex vertex = mesh.meshVertices[i];
                Vector3 n = Normals[i];
                Vector3 t = Tangents[i] - n * Vector3.Dot(Tangents[i], n);
                t = t.LengthSquared > 1e-10f ? t.Normalized() : AnyTangent(n);
                vertex.normal = n;
                vertex.tangent = t;
                vertex.bitangent = Vector3.Cross(n, t);
            }
            mesh.finishedNormals = true;
            mesh.finishedBaking = true;
            mesh.isUsingBones = true;
            return mesh;
        }
    }
}
