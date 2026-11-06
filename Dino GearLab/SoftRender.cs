using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_GearLab
{
    /// <summary>
    /// Draws a mesh on the CPU, so that a look can be judged with no window and no GPU. It's a plain z-buffer with a key, a fill and a rim, and only knows what vertex colours and
    /// the kind of material say: for the shape of a thing and its colours, not for how it comes out in the game.
    /// </summary>
    internal sealed class SoftRender
    {
        private static readonly Vector3 Key = new Vector3(-0.5f, 0.7f, 0.6f).Normalized();
        private static readonly Vector3 Fill = new Vector3(0.7f, 0.1f, 0.5f).Normalized();
        private static readonly Vector3 Rim = new Vector3(0.2f, 0.4f, -0.9f).Normalized();

        public readonly int Width;
        public readonly int Height;
        private readonly float[] _depth;
        private readonly Vector3[] _colour;
        private readonly bool[] _covered;

        public SoftRender(int width, int height)
        {
            Width = width;
            Height = height;
            _depth = new float[width * height];
            _colour = new Vector3[width * height];
            _covered = new bool[width * height];
        }

        public bool Covered(int x, int y) => _covered[y * Width + x];

        /// <summary>The picture, as a few big squares of colour, with nothing where there's nothing.</summary>
        public Vector3[] Grid(int cells)
        {
            var grid = new Vector3[cells * cells];
            var counts = new int[cells * cells];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (!_covered[y * Width + x]) continue;
                    int cell = y * cells / Height * cells + x * cells / Width;
                    // As it's seen and not as it's lit: deep colours are all near nothing in linear light.
                    Vector3 c = _colour[y * Width + x];
                    grid[cell] += new Vector3(MathF.Pow(MathF.Max(c.X, 0f), 1f / 2.2f), MathF.Pow(MathF.Max(c.Y, 0f), 1f / 2.2f), MathF.Pow(MathF.Max(c.Z, 0f), 1f / 2.2f));
                    counts[cell]++;
                }
            }
            for (int i = 0; i < grid.Length; i++)
            {
                if (counts[i] > 0) grid[i] /= counts[i];
            }
            return grid;
        }

        /// <summary>Fits a mesh to the picture as seen through <paramref name="view"/>, and draws it. Supersampling is left to whoever reads this.</summary>
        public void Draw(in Mesh mesh, Matrix3 view)
        {
            Array.Fill(_depth, float.MaxValue);
            Array.Clear(_covered);

            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            foreach (MeshVertex vertex in mesh.meshVertices)
            {
                Vector3 p = view * vertex.position;
                min = Vector3.ComponentMin(min, p);
                max = Vector3.ComponentMax(max, p);
            }

            Vector3 size = max - min;
            float scale = MathF.Min(Width / MathF.Max(size.X, 1e-3f), Height / MathF.Max(size.Y, 1e-3f)) * 0.92f;
            Vector2 middle = new Vector2(min.X + max.X, min.Y + max.Y) * 0.5f;

            Vector3 Screen(Vector3 world)
            {
                Vector3 p = view * world;
                return new Vector3((p.X - middle.X) * scale + Width * 0.5f, Height * 0.5f - (p.Y - middle.Y) * scale, -p.Z);
            }

            foreach (Face face in mesh.faces)
            {
                Triangle(Screen(face.A.position), Screen(face.B.position), Screen(face.C.position), face.A, face.B, face.C, view);
            }
        }

        private void Triangle(Vector3 a, Vector3 b, Vector3 c, MeshVertex va, MeshVertex vb, MeshVertex vc, Matrix3 view)
        {
            float area = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
            if (MathF.Abs(area) < 1e-6f) return;

            int left = Math.Max((int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))), 0);
            int right = Math.Min((int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))), Width - 1);
            int top = Math.Max((int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))), 0);
            int bottom = Math.Min((int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))), Height - 1);

            Vector3 na = view * va.normal, nb = view * vb.normal, nc = view * vc.normal;
            Vector3 ca = va.colour.ToVector3(), cb = vb.colour.ToVector3(), cc = vc.colour.ToVector3();
            int material = va.materialTextureIndex;

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
                    if (z >= _depth[at]) continue;
                    _depth[at] = z;
                    _covered[at] = true;

                    Vector3 normal = (na * w0 + nb * w1 + nc * w2).Normalized();
                    if (normal.Z < 0f) normal = -normal;
                    _colour[at] = Shade(ca * w0 + cb * w1 + cc * w2, normal, material);
                }
            }
        }

        private static Vector3 Shade(Vector3 colour, Vector3 normal, int material)
        {
            if (material == GearMaterials.Glow) return Vector3.ComponentMin(colour * 1.6f + new Vector3(0.15f), Vector3.One);

            float key = MathF.Max(Vector3.Dot(normal, Key), 0f);
            float fill = MathF.Max(Vector3.Dot(normal, Fill), 0f);
            float rim = MathF.Pow(1f - MathF.Max(normal.Z, 0f), 3f);
            Vector3 light = colour * (0.3f + key * 1.05f + fill * 0.28f) + colour * rim * 0.5f;

            bool shiny = material == GearMaterials.Metal || material == GearMaterials.Gem;
            if (!shiny) return light;

            Vector3 half = (Key + Vector3.UnitZ).Normalized();
            float specular = MathF.Pow(MathF.Max(Vector3.Dot(normal, half), 0f), 26f);
            float sky = 0.5f + 0.5f * normal.Y;
            return light * 0.8f + colour * sky * 0.3f + new Vector3(specular * 0.7f) * (colour + new Vector3(0.2f));
        }

        /// <summary>Copies the picture into a sheet, over its background, with a gamma of 2.2.</summary>
        public void Blit(byte[] sheet, int sheetWidth, int atX, int atY, int shrink, Vector3 background)
        {
            int weight = shrink * shrink;
            for (int y = 0; y < Height / shrink; y++)
            {
                for (int x = 0; x < Width / shrink; x++)
                {
                    Vector3 sum = Vector3.Zero;
                    for (int j = 0; j < shrink; j++)
                    {
                        for (int i = 0; i < shrink; i++)
                        {
                            int at = (y * shrink + j) * Width + x * shrink + i;
                            sum += _covered[at] ? _colour[at] : background;
                        }
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
