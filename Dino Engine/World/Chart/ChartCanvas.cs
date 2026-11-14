using OpenTK.Mathematics;

namespace Dino_Engine.World.Chart
{
    /// <summary>The sheet a chart is drawn on: colours as they're shown, a row at a time from the top.</summary>
    internal sealed class ChartCanvas
    {
        public readonly int Size;
        public readonly Vector3[] Pixels;

        public ChartCanvas(int size)
        {
            Size = size;
            Pixels = new Vector3[size * size];
        }

        public Pen All => new Pen(this, 0, Size);

        public byte[] ToBytes()
        {
            byte[] bytes = new byte[Size * Size * 4];
            Parallel.For(0, Size, row =>
            {
                for (int x = 0; x < Size; x++)
                {
                    int i = row * Size + x;
                    Vector3 c = Pixels[i];
                    bytes[i * 4] = ToByte(c.X);
                    bytes[i * 4 + 1] = ToByte(c.Y);
                    bytes[i * 4 + 2] = ToByte(c.Z);
                    bytes[i * 4 + 3] = 255;
                }
            });
            return bytes;
        }

        public static byte ToByte(float value) => (byte)Math.Clamp((int)(value * 255f + 0.5f), 0, 255);
    }

    /// <summary>
    /// Draws on a canvas within a band of its rows, so that the sheet can be cut into bands drawn side by side, each with everything that reaches it in the same order, and
    /// every pixel still sees what's in front of what. Everything is antialiased by how far a pixel's centre is from an edge.
    /// </summary>
    internal readonly struct Pen
    {
        public readonly ChartCanvas Canvas;
        public readonly int Top, Bottom;

        public Pen(ChartCanvas canvas, int top, int bottom)
        {
            Canvas = canvas;
            Top = top;
            Bottom = bottom;
        }

        public int Size => Canvas.Size;

        /// <summary>The pixels of a box that this pen may touch: false if none.</summary>
        public bool Clip(float minX, float minY, float maxX, float maxY, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = Math.Max((int)MathF.Floor(minX), 0);
            y0 = Math.Max((int)MathF.Floor(minY), Top);
            x1 = Math.Min((int)MathF.Ceiling(maxX), Canvas.Size - 1);
            y1 = Math.Min((int)MathF.Ceiling(maxY), Bottom - 1);
            return x0 <= x1 && y0 <= y1;
        }

        public void Blend(int x, int y, Vector3 colour, float alpha)
        {
            if (alpha <= 0f) return;
            ref Vector3 p = ref Canvas.Pixels[y * Canvas.Size + x];
            p += (colour - p) * MathF.Min(alpha, 1f);
        }

        /// <summary>Lays a wash over a pixel, as watercolour does: what's there shows through it, darkened by its colour.</summary>
        public void Wash(int x, int y, Vector3 tint, float alpha)
        {
            if (alpha <= 0f) return;
            ref Vector3 p = ref Canvas.Pixels[y * Canvas.Size + x];
            p += (p * tint - p) * MathF.Min(alpha, 1f);
        }

        public Vector3 Read(int x, int y) => Canvas.Pixels[y * Canvas.Size + x];

        /// <summary>A line with round ends.</summary>
        public void Stroke(Vector2 a, Vector2 b, float width, Vector3 colour, float alpha = 1f)
        {
            float half = width * 0.5f;
            float reach = half + 1f;
            if (!Clip(MathF.Min(a.X, b.X) - reach, MathF.Min(a.Y, b.Y) - reach, MathF.Max(a.X, b.X) + reach, MathF.Max(a.Y, b.Y) + reach, out int x0, out int y0, out int x1, out int y1)) return;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float d = Sdf.Segment(new Vector2(x + 0.5f, y + 0.5f), a, b) - half;
                    float cover = Coverage(d, width);
                    if (cover > 0f) Blend(x, y, colour, cover * alpha);
                }
            }
        }

        /// <summary>A line that tapers from one width to another, as a brush lifted at its end.</summary>
        public void Taper(Vector2 a, Vector2 b, float fromWidth, float toWidth, Vector3 colour, float alpha = 1f)
        {
            float reach = MathF.Max(fromWidth, toWidth) * 0.5f + 1f;
            if (!Clip(MathF.Min(a.X, b.X) - reach, MathF.Min(a.Y, b.Y) - reach, MathF.Max(a.X, b.X) + reach, MathF.Max(a.Y, b.Y) + reach, out int x0, out int y0, out int x1, out int y1)) return;
            Vector2 along = b - a;
            float length = MathF.Max(along.LengthSquared, 1e-6f);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Math.Clamp(Vector2.Dot(p - a, along) / length, 0f, 1f);
                    float width = fromWidth + (toWidth - fromWidth) * t;
                    float d = (p - (a + along * t)).Length - width * 0.5f;
                    float cover = Coverage(d, width);
                    if (cover > 0f) Blend(x, y, colour, cover * alpha);
                }
            }
        }

        public void Disc(Vector2 centre, float radius, Vector3 colour, float alpha = 1f)
        {
            if (!Clip(centre.X - radius - 1f, centre.Y - radius - 1f, centre.X + radius + 1f, centre.Y + radius + 1f, out int x0, out int y0, out int x1, out int y1)) return;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float d = (new Vector2(x + 0.5f, y + 0.5f) - centre).Length - radius;
                    float cover = Coverage(d, radius * 2f);
                    if (cover > 0f) Blend(x, y, colour, cover * alpha);
                }
            }
        }

        public void Ring(Vector2 centre, float radius, float width, Vector3 colour, float alpha = 1f)
        {
            float reach = radius + width + 1f;
            if (!Clip(centre.X - reach, centre.Y - reach, centre.X + reach, centre.Y + reach, out int x0, out int y0, out int x1, out int y1)) return;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float d = MathF.Abs((new Vector2(x + 0.5f, y + 0.5f) - centre).Length - radius) - width * 0.5f;
                    float cover = Coverage(d, width);
                    if (cover > 0f) Blend(x, y, colour, cover * alpha);
                }
            }
        }

        /// <summary>A filled polygon, with an ink edge if <paramref name="ink"/> is wider than nothing.</summary>
        public void Polygon(Vector2[] points, Vector3 fill, float fillAlpha, float ink, Vector3 inkColour, float inkAlpha = 1f)
        {
            Sdf.Bounds(points, out Vector2 min, out Vector2 max);
            float reach = ink + 1f;
            if (!Clip(min.X - reach, min.Y - reach, max.X + reach, max.Y + reach, out int x0, out int y0, out int x1, out int y1)) return;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float d = Sdf.Polygon(new Vector2(x + 0.5f, y + 0.5f), points);
                    if (fillAlpha > 0f) Blend(x, y, fill, Math.Clamp(0.5f - d, 0f, 1f) * fillAlpha);
                    if (ink > 0f) Blend(x, y, inkColour, Coverage(MathF.Abs(d) - ink * 0.5f, ink) * inkAlpha);
                }
            }
        }

        /// <summary>
        /// How much of a pixel a shape covers, from how far its centre is outside the shape's edge. A line thinner than a pixel is drawn a pixel wide and fainter, as a pen
        /// line on paper doesn't break up into dots.
        /// </summary>
        public static float Coverage(float distance, float width)
        {
            float cover = Math.Clamp(0.5f - distance, 0f, 1f);
            if (width < 1f) cover = Math.Clamp(0.5f - (distance - (1f - width) * 0.5f), 0f, 1f) * MathF.Max(width, 0.25f);
            return cover;
        }
    }

    /// <summary>Signed distances, in pixels: below nothing inside a shape.</summary>
    internal static class Sdf
    {
        public static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Math.Clamp(Vector2.Dot(pa, ba) / MathF.Max(Vector2.Dot(ba, ba), 1e-6f), 0f, 1f);
            return (pa - ba * h).Length;
        }

        /// <summary>Inigo Quilez's polygon distance: the nearest edge, signed by the winding.</summary>
        public static float Polygon(Vector2 p, Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i];
                Vector2 w = p - v[i];
                Vector2 b = w - e * Math.Clamp(Vector2.Dot(w, e) / MathF.Max(Vector2.Dot(e, e), 1e-6f), 0f, 1f);
                d = MathF.Min(d, Vector2.Dot(b, b));
                bool c1 = p.Y >= v[i].Y, c2 = p.Y < v[j].Y, c3 = e.X * w.Y > e.Y * w.X;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * MathF.Sqrt(d);
        }

        public static void Bounds(Vector2[] points, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue);
            max = new Vector2(float.MinValue);
            foreach (Vector2 p in points)
            {
                min = Vector2.ComponentMin(min, p);
                max = Vector2.ComponentMax(max, p);
            }
        }

        /// <summary>A smooth union, for crowns that swell into one another.</summary>
        public static float SmoothMin(float a, float b, float k)
        {
            float h = Math.Clamp(0.5f + 0.5f * (b - a) / k, 0f, 1f);
            return b + (a - b) * h - k * h * (1f - h);
        }
    }
}
