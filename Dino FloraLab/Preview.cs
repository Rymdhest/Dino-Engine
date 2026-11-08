using Dino_Engine.Modelling.Procedural.Flora;
using OpenTK.Mathematics;

namespace Dino_FloraLab
{
    /// <summary>
    /// Draws a plant on the CPU with its real cards, lit by a sun with a shadow map, so that a look can be judged in seconds with no window and no GPU. It answers light the way
    /// Lighting/lightingCalc.glsl does, roughly, and tone maps the way the game does, but it's for the shape and colour of a plant: the game is what it really looks like.
    /// </summary>
    internal sealed class Preview
    {
        private const int ShadowSize = 1024;
        public const int FirstCard = 1000;

        private static readonly Vector3 Sun = new Vector3(0.45f, 0.72f, 0.42f).Normalized();
        private static readonly Vector3 SunColour = new Vector3(1f, 0.93f, 0.8f) * 9.5f;
        private static readonly Vector3 SkyColour = new Vector3(0.62f, 0.76f, 0.98f) * 1.05f;
        private static readonly Vector3 GroundColour = new Vector3(0.2f, 0.2f, 0.13f) * 0.55f;
        private static readonly Vector3 Grass = new Vector3(0.1f, 0.15f, 0.06f);

        public readonly int Width;
        public readonly int Height;

        /// <summary>How much of a surface's colour the stand-in for its texture keeps at least, and how much it varies above that.</summary>
        public float GrainFloor = 0.3f;
        public float GrainDepth = 0.75f;
        private readonly BakedCards _cards;
        private readonly float[] _depth;
        private readonly Vector3[] _colour;
        private readonly float[] _shadow = new float[ShadowSize * ShadowSize];

        private Vector3 _shadowRight, _shadowUp;
        private Vector3 _shadowCentre;
        private float _shadowScale;

        public Preview(int width, int height, BakedCards cards)
        {
            Width = width;
            Height = height;
            _cards = cards;
            _depth = new float[width * height];
            _colour = new Vector3[width * height];
        }

        /// <param name="pitch">Radians above the horizon that it's seen from.</param>
        /// <param name="focus">What to look at, if not the middle of it all.</param>
        public void Draw(FloraMesh mesh, float pitch, float yaw, float? span = null, Vector3? focus = null)
        {
            mesh.Bounds(out Vector3 min, out Vector3 max);
            Vector3 size = max - min;
            float reach = span ?? MathF.Max(MathF.Max(size.X, size.Z), size.Y) * 1.12f;
            Vector3 target = focus ?? new Vector3((min.X + max.X) * 0.5f, size.Y * 0.5f * MathF.Cos(pitch) + 0.1f * size.Y, (min.Z + max.Z) * 0.5f);

            Vector3 forward = new Vector3(MathF.Cos(pitch) * MathF.Sin(yaw), -MathF.Sin(pitch), MathF.Cos(pitch) * MathF.Cos(yaw));
            Vector3 right = Vector3.Cross(forward, Vector3.UnitY).Normalized();
            Vector3 up = Vector3.Cross(right, forward).Normalized();
            float scale = Math.Min(Width, Height) / reach;

            Vector3 Screen(Vector3 world)
            {
                Vector3 from = world - target;
                return new Vector3(Width * 0.5f + Vector3.Dot(from, right) * scale, Height * 0.5f - Vector3.Dot(from, up) * scale, Vector3.Dot(from, forward));
            }

            CastShadows(mesh, size.Length * 0.65f, new Vector3((min.X + max.X) * 0.5f, size.Y * 0.5f, (min.Z + max.Z) * 0.5f));

            Array.Fill(_depth, float.MaxValue);
            // The ground, as a plane, so that a plant's shadow and how it stands on the earth can be seen.
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    _colour[y * Width + x] = Background(x, y, target, right, up, forward, scale);
                }
            }

            var triangle = new Vector3[3];
            for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
            {
                FloraVertex a = mesh.Vertices[mesh.Indices[i]], b = mesh.Vertices[mesh.Indices[i + 1]], c = mesh.Vertices[mesh.Indices[i + 2]];
                Raster(Screen(a.Position), Screen(b.Position), Screen(c.Position), a, b, c, forward);
            }
        }

        private Vector3 Background(int x, int y, Vector3 target, Vector3 right, Vector3 up, Vector3 forward, float scale)
        {
            Vector3 origin = target + right * ((x + 0.5f - Width * 0.5f) / scale) - up * ((y + 0.5f - Height * 0.5f) / scale) - forward * 200f;
            if (forward.Y >= -1e-3f) return Tone(SkyColour * 0.7f);
            float t = -origin.Y / forward.Y;
            if (t < 0f) return Tone(SkyColour * 0.7f);
            Vector3 hit = origin + forward * t;
            float shadow = ShadowAt(hit + Vector3.UnitY * 0.02f);
            float mottle = FloraNoise.Fbm(hit * 0.4f, 7u, 3) * 0.6f + 0.7f;
            Vector3 albedo = Grass * mottle;
            return Tone(albedo / MathF.PI * (SunColour * (Sun.Y * shadow)) + albedo * SkyColour * 0.55f);
        }

        private void CastShadows(FloraMesh mesh, float radius, Vector3 centre)
        {
            _shadowRight = Vector3.Cross(Sun, Vector3.UnitY).Normalized();
            _shadowUp = Vector3.Cross(_shadowRight, Sun).Normalized();
            _shadowCentre = centre;
            _shadowScale = ShadowSize / (radius * 2f);
            Array.Fill(_shadow, float.MinValue);

            for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
            {
                FloraVertex a = mesh.Vertices[mesh.Indices[i]], b = mesh.Vertices[mesh.Indices[i + 1]], c = mesh.Vertices[mesh.Indices[i + 2]];
                ShadowTriangle(a, b, c);
            }
        }

        private Vector3 ToShadow(Vector3 world)
        {
            Vector3 from = world - _shadowCentre;
            return new Vector3(ShadowSize * 0.5f + Vector3.Dot(from, _shadowRight) * _shadowScale, ShadowSize * 0.5f + Vector3.Dot(from, _shadowUp) * _shadowScale, Vector3.Dot(from, Sun));
        }

        private void ShadowTriangle(in FloraVertex va, in FloraVertex vb, in FloraVertex vc)
        {
            Vector3 a = ToShadow(va.Position), b = ToShadow(vb.Position), c = ToShadow(vc.Position);
            float area = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
            if (MathF.Abs(area) < 1e-6f) return;
            int left = Math.Max((int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))), 0);
            int right = Math.Min((int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))), ShadowSize - 1);
            int top = Math.Max((int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))), 0);
            int bottom = Math.Min((int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))), ShadowSize - 1);
            bool card = va.Material >= FirstCard;

            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = ((b.X - px) * (c.Y - py) - (c.X - px) * (b.Y - py)) / area;
                    float w1 = ((c.X - px) * (a.Y - py) - (a.X - px) * (c.Y - py)) / area;
                    float w2 = 1f - w0 - w1;
                    if (w0 < 0f || w1 < 0f || w2 < 0f) continue;
                    float depth = a.Z * w0 + b.Z * w1 + c.Z * w2;
                    int at = y * ShadowSize + x;
                    if (depth <= _shadow[at]) continue;
                    if (card)
                    {
                        Vector2 uv = va.UV * w0 + vb.UV * w1 + vc.UV * w2;
                        if (Sample(va.Material - FirstCard, uv, 2)[3] < CardImage.Cutoff) continue;
                    }
                    _shadow[at] = depth;
                }
            }
        }

        private float ShadowAt(Vector3 world)
        {
            Vector3 p = ToShadow(world);
            int x = (int)p.X, y = (int)p.Y;
            if (x < 1 || y < 1 || x >= ShadowSize - 1 || y >= ShadowSize - 1) return 1f;
            float light = 0f;
            for (int j = -1; j <= 1; j++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    if (_shadow[(y + j) * ShadowSize + x + i] <= p.Z + 0.25f) light += 1f;
                }
            }
            return light / 9f;
        }

        private readonly float[] _texel = new float[CardCanvas.Channels];

        private float[] Sample(int layer, Vector2 uv, int level)
        {
            int cellX = uv.X >= 0.5f ? 1 : 0, cellY = uv.Y >= 0.5f ? 1 : 0;
            CardImage image = _cards.Image(layer, cellX + cellY * 2);
            level = Math.Clamp(level, 0, image.Levels.Count - 1);
            int size = image.Size >> level;
            float fx = (uv.X - cellX * 0.5f) * 2f * size - 0.5f;
            float fy = (uv.Y - cellY * 0.5f) * 2f * size - 0.5f;
            int x = Math.Clamp((int)MathF.Floor(fx), 0, size - 1), y = Math.Clamp((int)MathF.Floor(fy), 0, size - 1);
            int x1 = Math.Min(x + 1, size - 1), y1 = Math.Min(y + 1, size - 1);
            float tx = Math.Clamp(fx - x, 0f, 1f), ty = Math.Clamp(fy - y, 0f, 1f);
            CardLevel data = image.Levels[level];
            int a = y * size + x, b = y * size + x1, c = y1 * size + x, d = y1 * size + x1;
            for (int i = 0; i < CardCanvas.Channels; i++)
            {
                _texel[i] = MathHelper.Lerp(MathHelper.Lerp(data.Read(a, i), data.Read(b, i), tx), MathHelper.Lerp(data.Read(c, i), data.Read(d, i), tx), ty);
            }
            return _texel;
        }

        private void Raster(Vector3 a, Vector3 b, Vector3 c, in FloraVertex va, in FloraVertex vb, in FloraVertex vc, Vector3 forward)
        {
            float area = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
            if (MathF.Abs(area) < 1e-6f) return;

            int left = Math.Max((int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))), 0);
            int right = Math.Min((int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))), Width - 1);
            int top = Math.Max((int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))), 0);
            int bottom = Math.Min((int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))), Height - 1);
            if (left > right || top > bottom) return;

            bool card = va.Material >= FirstCard;
            int layer = va.Material - FirstCard;
            int level = 0;
            if (card)
            {
                float uvArea = MathF.Abs((vb.UV.X - va.UV.X) * (vc.UV.Y - va.UV.Y) - (vc.UV.X - va.UV.X) * (vb.UV.Y - va.UV.Y)) * CardLibrary.LayerSize * CardLibrary.LayerSize;
                float ratio = uvArea / MathF.Max(MathF.Abs(area), 1e-3f);
                level = (int)MathF.Max(MathF.Round(0.5f * MathF.Log2(MathF.Max(ratio, 1f))), 0f);
            }

            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = ((b.X - px) * (c.Y - py) - (c.X - px) * (b.Y - py)) / area;
                    float w1 = ((c.X - px) * (a.Y - py) - (a.X - px) * (c.Y - py)) / area;
                    float w2 = 1f - w0 - w1;
                    if (w0 < 0f || w1 < 0f || w2 < 0f) continue;

                    float depth = a.Z * w0 + b.Z * w1 + c.Z * w2;
                    int at = y * Width + x;
                    if (depth >= _depth[at]) continue;

                    Vector3 colour = va.Colour * w0 + vb.Colour * w1 + vc.Colour * w2;
                    Vector3 normal = (va.Normal * w0 + vb.Normal * w1 + vc.Normal * w2).Normalized();
                    Vector3 world = va.Position * w0 + vb.Position * w1 + vc.Position * w2;
                    float translucency = 0f, ambient = 1f;

                    if (card)
                    {
                        Vector2 uv = va.UV * w0 + vb.UV * w1 + vc.UV * w2;
                        float[] texel = Sample(layer, uv, level);
                        if (texel[3] < CardImage.Cutoff) continue;
                        colour *= new Vector3(texel[0], texel[1], texel[2]);
                        Vector3 tangent = (va.Tangent * w0 + vb.Tangent * w1 + vc.Tangent * w2).Normalized();
                        Vector3 bitangent = Vector3.Cross(tangent, normal);
                        // Out of a texture this is y the other way up, as CardLevel stored it.
                        float nx = texel[4], ny = -texel[5];
                        float nz = MathF.Sqrt(MathF.Max(1f - nx * nx - ny * ny, 0f));
                        normal = (tangent * nx + bitangent * ny + normal * nz).Normalized();
                        translucency = texel[6];
                        ambient = texel[7];
                    }
                    else
                    {
                        // No bark here: only enough grain to read a bark's direction.
                        float grain = FloraNoise.Fbm(new Vector3(world.X * 6f, world.Y * 1.2f, world.Z * 6f), 3u, 2);
                        colour *= GrainFloor + GrainDepth * grain;
                    }

                    _depth[at] = depth;
                    _colour[at] = Tone(Light(world, colour, normal, forward, translucency, ambient));
                }
            }
        }

        private Vector3 Light(Vector3 world, Vector3 albedo, Vector3 normal, Vector3 forward, float translucency, float ambient)
        {
            Vector3 view = -forward;
            float shadow = ShadowAt(world);
            float facing = MathF.Max(Vector3.Dot(normal, Sun), 0f);
            Vector3 diffuse = albedo / MathF.PI * SunColour * (facing * shadow);

            // As lightSurface: thin things let light through, most of all seen against it.
            float backLit = Vector3.Dot(view, (normal - Sun).Normalized()) * 0.5f + 0.5f;
            Vector3 transmitted = albedo * (translucency * MathF.Pow(backLit, 1.9f) * 1.2f / MathF.PI) * SunColour * shadow;

            float up = normal.Y * 0.5f + 0.5f;
            Vector3 hemisphere = Vector3.Lerp(GroundColour, SkyColour, up);
            return diffuse + transmitted + albedo * hemisphere * ambient * 0.8f;
        }

        // As HDR_Mapper.frag: one ACES fit, and then the display's curve.
        private static Vector3 Tone(Vector3 c)
        {
            c *= 0.62f;
            Vector3 mapped = new Vector3(Aces(c.X), Aces(c.Y), Aces(c.Z));
            return new Vector3(MathF.Pow(mapped.X, 1f / 2.2f), MathF.Pow(mapped.Y, 1f / 2.2f), MathF.Pow(mapped.Z, 1f / 2.2f));
        }

        private static float Aces(float x) => Math.Clamp(x * (2.51f * x + 0.03f) / (x * (2.43f * x + 0.59f) + 0.14f), 0f, 1f);

        /// <summary>Copies the picture into a sheet, shrinking it to get rid of its stairs.</summary>
        public void Blit(byte[] sheet, int sheetWidth, int atX, int atY, int shrink)
        {
            int width = Width / shrink, height = Height / shrink;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 sum = Vector3.Zero;
                    for (int j = 0; j < shrink; j++)
                    {
                        for (int i = 0; i < shrink; i++) sum += _colour[(y * shrink + j) * Width + x * shrink + i];
                    }
                    sum /= shrink * shrink;
                    int to = ((atY + y) * sheetWidth + atX + x) * 4;
                    sheet[to] = (byte)(Math.Clamp(sum.X, 0f, 1f) * 255f);
                    sheet[to + 1] = (byte)(Math.Clamp(sum.Y, 0f, 1f) * 255f);
                    sheet[to + 2] = (byte)(Math.Clamp(sum.Z, 0f, 1f) * 255f);
                    sheet[to + 3] = 255;
                }
            }
        }
    }
}
