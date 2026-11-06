using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_AnimLab
{
    /// <summary>A mesh made ready for skinning on the CPU, again and again, with no GPU.</summary>
    internal sealed class PosableMesh
    {
        public readonly Vector3[] Positions;
        public readonly Vector3[] Normals;
        public readonly Vector3[] Colours;
        public readonly int[] FirstBone;
        public readonly int[] SecondBone;
        public readonly Vector2[] Weights;
        public readonly int[] Triangles;
        public readonly Vector3[] Posed;
        public readonly Vector3[] PosedNormals;

        public PosableMesh(Mesh mesh)
        {
            var vertices = mesh.meshVertices;
            int count = vertices.Count;
            Positions = new Vector3[count];
            Normals = new Vector3[count];
            Colours = new Vector3[count];
            FirstBone = new int[count];
            SecondBone = new int[count];
            Weights = new Vector2[count];
            Posed = new Vector3[count];
            PosedNormals = new Vector3[count];

            var index = new Dictionary<MeshVertex, int>(ReferenceEqualityComparer.Instance as IEqualityComparer<MeshVertex>);
            for (int i = 0; i < count; i++)
            {
                MeshVertex vertex = vertices[i];
                index[vertex] = i;
                Positions[i] = vertex.position;
                Normals[i] = vertex.normal;
                Colours[i] = vertex.colour.ToVector3();
                FirstBone[i] = (int)vertex.boneIndices.X;
                SecondBone[i] = (int)vertex.boneIndices.Y;
                float total = vertex.BoneWeights.X + vertex.BoneWeights.Y;
                Weights[i] = total > 0.001f ? vertex.BoneWeights / total : new Vector2(1f, 0f);
            }

            var triangles = new List<int>(mesh.faces.Count * 3);
            foreach (Face face in mesh.faces)
            {
                triangles.Add(index[face.A]);
                triangles.Add(index[face.B]);
                triangles.Add(index[face.C]);
            }
            Triangles = triangles.ToArray();
        }

        /// <summary>Puts every vertex where these bones say, and then into the world.</summary>
        public void Pose(Matrix4[] bones, Matrix4 world)
        {
            for (int i = 0; i < Positions.Length; i++)
            {
                Vector2 weights = Weights[i];
                Matrix4 a = bones[FirstBone[i]];
                Matrix4 skin = weights.Y > 0f ? a * weights.X + bones[SecondBone[i]] * weights.Y : a;
                Matrix4 whole = skin * world;
                Posed[i] = Vector3.TransformPosition(Positions[i], whole);
                PosedNormals[i] = Vector3.TransformNormal(Normals[i], whole).Normalized();
            }
        }
    }

    /// <summary>Which way a picture looks. Unlike a model's own picture this doesn't fit itself to what's in it: a run of frames has to stay put, or nothing can be seen to move.</summary>
    internal readonly record struct PoseView(Vector3 Right, Vector3 Up, Vector3 Towards)
    {
        public static PoseView Named(string name) => name switch
        {
            "side" => Looking(new Vector3(1f, 0f, 0f)),
            "front" => Looking(new Vector3(0f, 0f, -1f)),
            "back" => Looking(new Vector3(0f, 0f, 1f)),
            "top" => Looking(new Vector3(0f, -1f, 0.001f)),
            "three" => Looking(new Vector3(0.75f, -0.32f, -0.6f)),
            _ => Looking(new Vector3(0f, -MathF.Sin(MathF.PI / 2.7f), MathF.Cos(MathF.PI / 2.7f))),
        };

        private static PoseView Looking(Vector3 forward)
        {
            forward.Normalize();
            Vector3 right = Vector3.Cross(forward, Vector3.UnitY).Normalized();
            return new PoseView(right, Vector3.Cross(right, forward), -forward);
        }
    }

    /// <summary>A fixed camera on a z-buffer, with a floor drawn in, so that a pose can be looked at with no window and no GPU.</summary>
    internal sealed class PoseRender
    {
        private static readonly Vector3 Sun = new Vector3(-0.4f, 0.8f, -0.45f).Normalized();

        public readonly int Width;
        public readonly int Height;
        private readonly float[] _depth;
        private readonly Vector3[] _colour;

        public PoseRender(int width, int height)
        {
            Width = width;
            Height = height;
            _depth = new float[width * height];
            _colour = new Vector3[width * height];
        }

        public void Clear(Vector3 colour)
        {
            Array.Fill(_depth, float.NegativeInfinity);
            Array.Fill(_colour, colour);
        }

        private Vector3 Screen(Vector3 at, PoseView view, Vector3 centre, float pixelsPerMetre)
        {
            Vector3 p = at - centre;
            return new Vector3(Width * 0.5f + Vector3.Dot(p, view.Right) * pixelsPerMetre, Height * 0.5f - Vector3.Dot(p, view.Up) * pixelsPerMetre, Vector3.Dot(p, view.Towards));
        }

        /// <summary>A line through the world, in front of or behind what's already there as it should be.</summary>
        public void Line(Vector3 from, Vector3 to, Vector3 colour, PoseView view, Vector3 centre, float pixelsPerMetre, float thickness = 1f)
        {
            Vector3 a = Screen(from, view, centre, pixelsPerMetre);
            Vector3 b = Screen(to, view, centre, pixelsPerMetre);
            int steps = (int)MathF.Max(MathF.Abs(b.X - a.X), MathF.Abs(b.Y - a.Y)) + 1;
            int half = (int)(thickness * 0.5f);
            for (int i = 0; i <= steps; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, (float)i / steps);
                for (int dy = -half; dy <= half; dy++)
                {
                    for (int dx = -half; dx <= half; dx++)
                    {
                        int x = (int)p.X + dx, y = (int)p.Y + dy;
                        if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                        int at = y * Width + x;
                        if (p.Z < _depth[at]) continue;
                        _depth[at] = p.Z;
                        _colour[at] = colour;
                    }
                }
            }
        }

        /// <summary>A floor of squares, for seeing that feet really do stay where they're put.</summary>
        public void Floor(Func<float, float, float> height, Vector3 middle, float reach, float spacing, PoseView view, float pixelsPerMetre)
        {
            Vector3 colour = new Vector3(0.11f, 0.12f, 0.13f);
            float startX = MathF.Floor((middle.X - reach) / spacing) * spacing;
            float startZ = MathF.Floor((middle.Z - reach) / spacing) * spacing;
            int lines = (int)(reach * 2f / spacing) + 2;
            const int pieces = 24;
            for (int i = 0; i < lines; i++)
            {
                float x = startX + i * spacing;
                float z = startZ + i * spacing;
                for (int j = 0; j < pieces; j++)
                {
                    float t0 = middle.Z - reach + reach * 2f * j / pieces;
                    float t1 = middle.Z - reach + reach * 2f * (j + 1) / pieces;
                    Line(new Vector3(x, height(x, t0), t0), new Vector3(x, height(x, t1), t1), colour, view, middle, pixelsPerMetre);
                    float s0 = middle.X - reach + reach * 2f * j / pieces;
                    float s1 = middle.X - reach + reach * 2f * (j + 1) / pieces;
                    Line(new Vector3(s0, height(s0, z), z), new Vector3(s1, height(s1, z), z), colour, view, middle, pixelsPerMetre);
                }
            }
        }

        public void Draw(PosableMesh mesh, PoseView view, Vector3 centre, float pixelsPerMetre)
        {
            int[] triangles = mesh.Triangles;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
                Triangle(Screen(mesh.Posed[ia], view, centre, pixelsPerMetre), Screen(mesh.Posed[ib], view, centre, pixelsPerMetre), Screen(mesh.Posed[ic], view, centre, pixelsPerMetre),
                    mesh.PosedNormals[ia], mesh.PosedNormals[ib], mesh.PosedNormals[ic], mesh.Colours[ia], mesh.Colours[ib], mesh.Colours[ic], view);
            }
        }

        private void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Vector3 ca, Vector3 cb, Vector3 cc, PoseView view)
        {
            float area = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
            if (MathF.Abs(area) < 1e-6f) return;

            int left = Math.Max((int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))), 0);
            int right = Math.Min((int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))), Width - 1);
            int top = Math.Max((int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))), 0);
            int bottom = Math.Min((int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))), Height - 1);

            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = ((b.X - px) * (c.Y - py) - (c.X - px) * (b.Y - py)) / area;
                    float w1 = ((c.X - px) * (a.Y - py) - (a.X - px) * (c.Y - py)) / area;
                    float w2 = 1f - w0 - w1;
                    if (w0 < 0f || w1 < 0f || w2 < 0f) continue;

                    float z = a.Z * w0 + b.Z * w1 + c.Z * w2;
                    int at = y * Width + x;
                    if (z <= _depth[at]) continue;
                    _depth[at] = z;

                    Vector3 normal = (na * w0 + nb * w1 + nc * w2).Normalized();
                    if (Vector3.Dot(normal, view.Towards) < 0f) normal = -normal;
                    Vector3 colour = ca * w0 + cb * w1 + cc * w2;
                    float light = 0.34f + MathF.Max(Vector3.Dot(normal, Sun), 0f) * 0.95f + (0.5f + 0.5f * normal.Y) * 0.25f;
                    _colour[at] = colour * light * 2.2f;
                }
            }
        }

        /// <summary>Copies the picture into a sheet, with a gamma of 2.2.</summary>
        public void Blit(byte[] sheet, int sheetWidth, int atX, int atY, int shrink)
        {
            int weight = shrink * shrink;
            for (int y = 0; y < Height / shrink; y++)
            {
                for (int x = 0; x < Width / shrink; x++)
                {
                    Vector3 sum = Vector3.Zero;
                    for (int j = 0; j < shrink; j++)
                    {
                        for (int i = 0; i < shrink; i++) sum += _colour[(y * shrink + j) * Width + x * shrink + i];
                    }

                    Vector3 c = sum / weight;
                    int o = ((atY + y) * sheetWidth + atX + x) * 4;
                    sheet[o] = (byte)(Math.Clamp(MathF.Pow(c.X, 1f / 2.2f), 0f, 1f) * 255f);
                    sheet[o + 1] = (byte)(Math.Clamp(MathF.Pow(c.Y, 1f / 2.2f), 0f, 1f) * 255f);
                    sheet[o + 2] = (byte)(Math.Clamp(MathF.Pow(c.Z, 1f / 2.2f), 0f, 1f) * 255f);
                    sheet[o + 3] = 255;
                }
            }
        }
    }
}
