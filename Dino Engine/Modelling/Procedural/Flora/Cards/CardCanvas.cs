using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// Where a card is painted, on the CPU, with no GPU anywhere near it: a card is many small things laid over each other from the back to the front, which is a painter's job and
    /// not a rasteriser's. Leaf outlines are soft, so everything comes out antialiased with no extra samples. Coordinates are 0 to 1, with a stem's foot at the bottom.
    /// </summary>
    public sealed class CardCanvas
    {
        // Red, green, blue, alpha, normal x, normal y, translucency, ambient, roughness, height, all multiplied by alpha.
        public const int Channels = 10;

        public readonly int Size;
        public readonly float[] Data;
        internal readonly float[][] Mips;   // Room for every level, the first being the picture: what's finished leaves as bytes, so one canvas does for all a thread paints.
        private readonly List<(float depth, int order, Action paint)> _queue = new();

        public CardCanvas(int size)
        {
            Size = size;
            Mips = new float[System.Numerics.BitOperations.Log2((uint)size) + 1][];
            for (int i = 0; i < Mips.Length; i++) Mips[i] = new float[(size >> i) * (size >> i) * Channels];
            Data = Mips[0];
        }

        /// <summary>Starts again on a clean picture. The other levels are written whole by <see cref="CardImage"/>, so they needn't be.</summary>
        public void Clear() => Array.Clear(Data);

        /// <summary>Saves something to be painted when <see cref="Finish"/> sorts it by depth: further from the viewer is lower.</summary>
        public void Later(float depth, Action paint) => _queue.Add((depth, _queue.Count, paint));

        public void Finish()
        {
            _queue.Sort((a, b) => a.depth != b.depth ? a.depth.CompareTo(b.depth) : a.order.CompareTo(b.order));
            foreach ((float _, int _, Action paint) in _queue) paint();
            _queue.Clear();
        }

        private void Blend(int x, int y, Vector3 colour, float alpha, Vector3 normal, float translucency, float ambient, float roughness, float height)
        {
            int at = (y * Size + x) * Channels;
            float keep = 1f - alpha;
            float[] data = Data;
            data[at] = data[at] * keep + colour.X * alpha;
            data[at + 1] = data[at + 1] * keep + colour.Y * alpha;
            data[at + 2] = data[at + 2] * keep + colour.Z * alpha;
            data[at + 3] = data[at + 3] * keep + alpha;
            data[at + 4] = data[at + 4] * keep + normal.X * alpha;
            data[at + 5] = data[at + 5] * keep + normal.Y * alpha;
            data[at + 6] = data[at + 6] * keep + translucency * alpha;
            data[at + 7] = data[at + 7] * keep + ambient * alpha;
            data[at + 8] = data[at + 8] * keep + roughness * alpha;
            data[at + 9] = data[at + 9] * keep + height * alpha;
        }

        /// <summary>A leaf from its stalk to its tip. <paramref name="width"/> is how wide its image reaches to each side, in the canvas's terms.</summary>
        public void Leaf(LeafImage leaf, Vector2 foot, Vector2 tip, float width, in CardInk ink, float fold = 0.3f, float curl = 0.2f, float bend = 0f)
        {
            Vector2 along = tip - foot;
            float length = along.Length;
            if (length < 1e-4f) return;
            Vector2 axis = along / length;
            Vector2 across = new Vector2(-axis.Y, axis.X);
            float reach = MathF.Max(width, 1e-4f);

            float pad = reach + MathF.Abs(bend) * length;
            Vector2 min = Vector2.ComponentMin(foot, tip) - new Vector2(pad);
            Vector2 max = Vector2.ComponentMax(foot, tip) + new Vector2(pad);
            int left = Math.Max((int)(min.X * Size), 0), right = Math.Min((int)(max.X * Size) + 1, Size - 1);
            int bottom = Math.Max((int)(min.Y * Size), 0), top = Math.Min((int)(max.Y * Size) + 1, Size - 1);

            for (int y = bottom; y <= top; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    Vector2 point = new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size) - foot;
                    float v = Vector2.Dot(point, axis) / length;
                    if (v < 0f || v > 1f) continue;
                    float u = (Vector2.Dot(point, across) - bend * length * v * v) / reach;
                    if (u < -1f || u > 1f) continue;

                    leaf.Sample(u, v, out Vector3 colour, out float alpha, out float height);
                    if (alpha <= 0.002f) continue;

                    // A leaf folds along its midrib and curls along its length, which is what makes it catch the light in two halves.
                    Vector2 slope = across * (u * fold) + axis * ((v - 0.45f) * curl);
                    Vector3 normal = new Vector3(ink.Normal.X + slope.X, ink.Normal.Y + slope.Y, ink.Normal.Z).Normalized();
                    Blend(x, y, colour * ink.Colour, alpha, normal, ink.Translucency * (1.1f - height * 0.4f), ink.Ambient, ink.Roughness, ink.Height + (height - 0.5f) * 0.2f);
                }
            }
        }

        /// <summary>A line that goes from one width to another, round in section. It's for stems and needles.</summary>
        public void Stroke(Vector2 from, Vector2 to, float radiusFrom, float radiusTo, in CardInk ink)
        {
            Vector2 line = to - from;
            float lengthSquared = line.LengthSquared;
            if (lengthSquared < 1e-9f) return;

            float pad = MathF.Max(radiusFrom, radiusTo) + 1.5f / Size;
            Vector2 min = Vector2.ComponentMin(from, to) - new Vector2(pad);
            Vector2 max = Vector2.ComponentMax(from, to) + new Vector2(pad);
            int left = Math.Max((int)(min.X * Size), 0), right = Math.Min((int)(max.X * Size) + 1, Size - 1);
            int bottom = Math.Max((int)(min.Y * Size), 0), top = Math.Min((int)(max.Y * Size) + 1, Size - 1);
            float texel = 1f / Size;
            Vector2 across = new Vector2(-line.Y, line.X).Normalized();

            for (int y = bottom; y <= top; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    Vector2 point = new Vector2((x + 0.5f) * texel, (y + 0.5f) * texel);
                    float t = Math.Clamp(Vector2.Dot(point - from, line) / lengthSquared, 0f, 1f);
                    float radius = MathHelper.Lerp(radiusFrom, radiusTo, t);
                    Vector2 off = point - (from + line * t);
                    float distance = off.Length;

                    // A line thinner than a texel is only there in part, and that's its alpha, so a far needle fades out and doesn't break into dots.
                    float alpha = Math.Clamp((radius - distance) / texel + 0.5f, 0f, 1f);
                    if (alpha <= 0.003f) continue;

                    float round = Vector2.Dot(off, across) / MathF.Max(radius, texel);
                    round = Math.Clamp(round, -1f, 1f);
                    Vector3 normal = new Vector3(ink.Normal.X + across.X * round, ink.Normal.Y + across.Y * round, ink.Normal.Z).Normalized();
                    Blend(x, y, ink.Colour * (0.85f + 0.15f * (1f - round * round)), alpha, normal, ink.Translucency, ink.Ambient, ink.Roughness, ink.Height);
                }
            }
        }
    }

    /// <summary>What something is painted with.</summary>
    public struct CardInk
    {
        public Vector3 Colour;
        public Vector3 Normal;
        public float Translucency;
        public float Ambient;
        public float Roughness;
        public float Height;

        public static CardInk Leaf(Vector3 colour, Vector3 normal, float depth) => new CardInk
        {
            Colour = colour,
            Normal = normal,
            Translucency = 0.7f,
            Ambient = 0.55f + 0.45f * depth,
            Roughness = 0.74f,
            Height = depth,
        };

        public static CardInk Wood(Vector3 colour, float depth) => new CardInk
        {
            Colour = colour,
            Normal = Vector3.UnitZ,
            Translucency = 0f,
            Ambient = 0.5f + 0.4f * depth,
            Roughness = 0.85f,
            Height = depth,
        };
    }

    /// <summary>
    /// A finished cell as what the textures hold, at every mip level. Going down the mips is where cutout foliage usually dies: averaged alpha falls below the cutoff and a crown goes
    /// thin as the camera backs off. So alpha is scaled at each level to keep the same share of texels above the cutoff, after Castaño's "Computing Alpha Mipmaps", and what's under
    /// transparent texels is filled in from around them, or their black would creep in as mips blend.
    /// </summary>
    public sealed class CardImage
    {
        public const float Cutoff = 0.5f;

        public readonly int Size;
        public readonly List<CardLevel> Levels = new();

        /// <summary>Works everything out on the canvas, which is left to be cleared and used again: a whole library of these as floats is gigabytes.</summary>
        public CardImage(CardCanvas canvas)
        {
            Size = canvas.Size;
            float[][] levels = canvas.Mips;
            for (int i = 1; i < levels.Length; i++) Shrink(levels[i - 1], Size >> (i - 1), levels[i], Size >> i);

            float coverage = Coverage(levels[0], Size, 1f);
            // A cutoff can't see a value of a mip level until it's been unmultiplied, which everything below has to do: see Fill.
            for (int i = levels.Length - 1; i >= 0; i--)
            {
                int size = Size >> i;
                Fill(levels[i], size, i + 1 < levels.Length ? levels[i + 1] : null);
                if (i > 0) Keep(levels[i], size, coverage);
            }
            for (int i = 0; i < levels.Length; i++) Levels.Add(new CardLevel(levels[i], Size >> i));
        }

        private static void Shrink(float[] source, int sourceSize, float[] result, int size)
        {
            const int C = CardCanvas.Channels;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int a = ((y * 2) * sourceSize + x * 2) * C;
                    int b = a + C;
                    int c = a + sourceSize * C;
                    int d = c + C;
                    int to = (y * size + x) * C;
                    for (int i = 0; i < C; i++) result[to + i] = (source[a + i] + source[b + i] + source[c + i] + source[d + i]) * 0.25f;
                }
            }
        }

        private static float Coverage(float[] level, int size, float scale)
        {
            int covered = 0;
            for (int i = 3; i < level.Length; i += CardCanvas.Channels)
            {
                if (level[i] * scale >= Cutoff) covered++;
            }
            return covered / (float)(size * size);
        }

        // Turns premultiplied values into plain ones, with what's under transparency taken from the level above.
        private static void Fill(float[] level, int size, float[]? coarser)
        {
            const int C = CardCanvas.Channels;
            int coarserSize = size / 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int at = (y * size + x) * C;
                    float alpha = level[at + 3];
                    int from = coarser != null && coarserSize > 0 ? (Math.Min(y / 2, coarserSize - 1) * coarserSize + Math.Min(x / 2, coarserSize - 1)) * C : -1;
                    for (int i = 0; i < C; i++)
                    {
                        if (i == 3) continue;
                        float own = level[at + i];
                        float behind = from >= 0 ? coarser![from + i] : DefaultOf(i);
                        level[at + i] = own + behind * (1f - alpha);
                    }
                }
            }
        }

        private static float DefaultOf(int channel) => channel switch
        {
            0 or 1 or 2 => 0.3f,
            7 => 0.8f,
            8 => 0.6f,
            _ => 0f,
        };

        // Finds the scale that brings alpha back to the coverage the full picture has. A small level can't hit that exactly, as a 2 by 2 goes from a quarter to a half in one step, and it
        // has to come out under and not over: over, and a card that's mostly air is a solid square in the far shadow cascades, which read these levels.
        private static void Keep(float[] level, int size, float coverage)
        {
            float low = 0.25f, high = 6f;
            for (int i = 0; i < 12; i++)
            {
                float middle = (low + high) * 0.5f;
                if (Coverage(level, size, middle) <= coverage) low = middle;
                else high = middle;
            }
            float scale = low;
            for (int i = 3; i < level.Length; i += CardCanvas.Channels) level[i] = MathF.Min(level[i] * scale, 1f);
        }

        /// <summary>A level, into where it goes among the rest of a layer.</summary>
        public void Pack(int level, byte[] albedo, byte[] normal, byte[] material, int atX, int atY, int stride)
        {
            CardLevel cell = Levels[level];
            int row = cell.Size * 4;
            for (int y = 0; y < cell.Size; y++)
            {
                int from = y * row, to = ((atY + y) * stride + atX) * 4;
                Buffer.BlockCopy(cell.Albedo, from, albedo, to, row);
                Buffer.BlockCopy(cell.Normal, from, normal, to, row);
                Buffer.BlockCopy(cell.Material, from, material, to, row);
            }
        }
    }

    /// <summary>A level as the three textures hold it: albedo and alpha; normal x and y, translucency and ambient; roughness, emission, metal and height.</summary>
    public sealed class CardLevel
    {
        public readonly int Size;
        public readonly byte[] Albedo;
        public readonly byte[] Normal;
        public readonly byte[] Material;

        public CardLevel(float[] data, int size)
        {
            Size = size;
            Albedo = new byte[size * size * 4];
            Normal = new byte[size * size * 4];
            Material = new byte[size * size * 4];

            for (int texel = 0; texel < size * size; texel++)
            {
                int at = texel * CardCanvas.Channels;
                int to = texel * 4;
                Albedo[to] = Byte(data[at]);
                Albedo[to + 1] = Byte(data[at + 1]);
                Albedo[to + 2] = Byte(data[at + 2]);
                Albedo[to + 3] = Byte(data[at + 3]);

                // The frame's bitangent is the tangent crossed with the normal, which points down a card: its y goes the other way to the canvas's.
                Normal[to] = Byte(data[at + 4] * 0.5f + 0.5f);
                Normal[to + 1] = Byte(-data[at + 5] * 0.5f + 0.5f);
                Normal[to + 2] = Byte(data[at + 6]);
                Normal[to + 3] = Byte(data[at + 7]);

                Material[to] = Byte(data[at + 8]);
                Material[to + 3] = Byte(data[at + 9]);
            }
        }

        /// <summary>One of a texel's <see cref="CardCanvas.Channels"/>, for what reads a card with no GPU.</summary>
        public float Read(int texel, int channel)
        {
            int at = texel * 4;
            return channel switch
            {
                < 4 => Albedo[at + channel] / 255f,
                4 => Normal[at] / 127.5f - 1f,
                5 => 1f - Normal[at + 1] / 127.5f,
                6 or 7 => Normal[at + channel - 4] / 255f,
                8 => Material[at] / 255f,
                _ => Material[at + 3] / 255f,
            };
        }

        private static byte Byte(float value) => (byte)(Math.Clamp(value, 0f, 1f) * 255f + 0.5f);
    }
}
