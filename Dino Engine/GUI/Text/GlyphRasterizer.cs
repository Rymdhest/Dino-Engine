using OpenTK.Mathematics;

namespace Dino_Engine.GUI.Text
{
    /// <summary>A rasterized glyph coverage bitmap (one byte per pixel, row-major, y down).</summary>
    public readonly struct GlyphBitmap
    {
        public readonly int Width;
        public readonly int Height;
        /// <summary>Pixel offset of the bitmap's left edge from the pen position.</summary>
        public readonly int OffsetX;
        /// <summary>Pixel offset of the bitmap's top edge from the baseline (y down, so usually negative).</summary>
        public readonly int OffsetY;
        public readonly byte[] Pixels;

        public GlyphBitmap(int width, int height, int offsetX, int offsetY, byte[] pixels)
        {
            Width = width;
            Height = height;
            OffsetX = offsetX;
            OffsetY = offsetY;
            Pixels = pixels;
        }

        public bool IsEmpty => Width == 0 || Height == 0;
        public static GlyphBitmap Empty => new GlyphBitmap(0, 0, 0, 0, Array.Empty<byte>());
    }

    /// <summary>
    /// Anti-aliased scanline rasterizer for quadratic outlines using exact signed area coverage
    /// accumulation (same approach as font-rs / fontdue). Non-zero style fill via |winding| clamped to 1.
    /// </summary>
    public static class GlyphRasterizer
    {
        private const int Padding = 1;

        public static GlyphBitmap Rasterize(List<List<FontFace.OutlinePoint>> contours, float scale)
        {
            if (contours.Count == 0) return GlyphBitmap.Empty;

            // Control points bound the quadratic curves, so the point bounds are a safe bitmap bound.
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var contour in contours)
            {
                foreach (var point in contour)
                {
                    minX = MathF.Min(minX, point.Position.X);
                    maxX = MathF.Max(maxX, point.Position.X);
                    minY = MathF.Min(minY, point.Position.Y);
                    maxY = MathF.Max(maxY, point.Position.Y);
                }
            }
            if (minX > maxX || minY > maxY) return GlyphBitmap.Empty;

            // Pixel space, y down.
            int x0 = (int)MathF.Floor(minX * scale);
            int x1 = (int)MathF.Ceiling(maxX * scale);
            int y0 = (int)MathF.Floor(-maxY * scale);
            int y1 = (int)MathF.Ceiling(-minY * scale);
            int width = x1 - x0 + Padding * 2;
            int height = y1 - y0 + Padding * 2;
            if (width <= Padding * 2 || height <= Padding * 2) return GlyphBitmap.Empty;
            if (width > 4096 || height > 4096) return GlyphBitmap.Empty;

            var canvas = new Canvas(width, height);
            Vector2 ToPixel(Vector2 p) => new Vector2(p.X * scale - x0 + Padding, -p.Y * scale - y0 + Padding);

            foreach (var contour in contours)
            {
                DrawContour(canvas, contour, ToPixel);
            }

            byte[] pixels = canvas.Accumulate();
            return new GlyphBitmap(width, height, x0 - Padding, y0 - Padding, pixels);
        }

        private static void DrawContour(Canvas canvas, List<FontFace.OutlinePoint> contour, Func<Vector2, Vector2> toPixel)
        {
            int n = contour.Count;
            if (n < 2) return;

            // Insert implied on-curve points between consecutive off-curve points.
            var expanded = new List<FontFace.OutlinePoint>(n * 2);
            for (int i = 0; i < n; i++)
            {
                var current = contour[i];
                var next = contour[(i + 1) % n];
                expanded.Add(current);
                if (!current.OnCurve && !next.OnCurve)
                {
                    expanded.Add(new FontFace.OutlinePoint((current.Position + next.Position) * 0.5f, true));
                }
            }

            int start = expanded.FindIndex(p => p.OnCurve);
            if (start < 0) return;

            int m = expanded.Count;
            Vector2 cursor = toPixel(expanded[start].Position);
            for (int k = 1; k <= m; k++)
            {
                var point = expanded[(start + k) % m];
                if (point.OnCurve)
                {
                    Vector2 target = toPixel(point.Position);
                    canvas.Line(cursor, target);
                    cursor = target;
                }
                else
                {
                    // After expansion an off-curve point is always followed by an on-curve point.
                    var end = expanded[(start + k + 1) % m];
                    Vector2 control = toPixel(point.Position);
                    Vector2 target = toPixel(end.Position);
                    canvas.Quadratic(cursor, control, target);
                    cursor = target;
                    k++;
                }
            }
        }

        private sealed class Canvas
        {
            private readonly int _width;
            private readonly int _height;
            private readonly float[] _accumulation;

            public Canvas(int width, int height)
            {
                _width = width;
                _height = height;
                // Extra cells: contributions at x == width spill into the next row, which is fine because
                // accumulation runs over the whole buffer; the last row needs room past the end.
                _accumulation = new float[width * height + 4];
            }

            public void Quadratic(Vector2 p0, Vector2 p1, Vector2 p2)
            {
                float devX = p0.X - 2f * p1.X + p2.X;
                float devY = p0.Y - 2f * p1.Y + p2.Y;
                float devSquared = devX * devX + devY * devY;
                if (devSquared < 0.333f)
                {
                    Line(p0, p2);
                    return;
                }
                const float tolerance = 3f;
                int segments = 1 + (int)MathF.Floor(MathF.Sqrt(MathF.Sqrt(tolerance * devSquared)));
                Vector2 previous = p0;
                for (int i = 1; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    float mt = 1f - t;
                    Vector2 point = mt * mt * p0 + 2f * mt * t * p1 + t * t * p2;
                    if (i == segments) point = p2;
                    Line(previous, point);
                    previous = point;
                }
            }

            public void Line(Vector2 p0, Vector2 p1)
            {
                if (MathF.Abs(p0.Y - p1.Y) <= float.Epsilon) return;
                float direction;
                if (p0.Y < p1.Y)
                {
                    direction = 1f;
                }
                else
                {
                    direction = -1f;
                    (p0, p1) = (p1, p0);
                }

                float dxdy = (p1.X - p0.X) / (p1.Y - p0.Y);
                float x = p0.X;
                if (p0.Y < 0f) x -= p0.Y * dxdy;
                int yStart = Math.Max(0, (int)p0.Y);
                int yEnd = Math.Min(_height, (int)MathF.Ceiling(p1.Y));

                for (int y = yStart; y < yEnd; y++)
                {
                    int lineStart = y * _width;
                    float dy = MathF.Min(y + 1f, p1.Y) - MathF.Max(y, p0.Y);
                    float xNext = x + dxdy * dy;
                    float d = dy * direction;
                    float xa = x < xNext ? x : xNext;
                    float xb = x < xNext ? xNext : x;
                    float xaFloor = MathF.Floor(xa);
                    int xai = (int)xaFloor;
                    float xbCeil = MathF.Ceiling(xb);
                    int xbi = (int)xbCeil;

                    if (xai < 0 || xbi > _width)
                    {
                        // Outside the canvas (cannot happen with padded bounds, but stay memory safe).
                        x = xNext;
                        continue;
                    }

                    if (xbi <= xai + 1)
                    {
                        float xmf = 0.5f * (x + xNext) - xaFloor;
                        _accumulation[lineStart + xai] += d - d * xmf;
                        _accumulation[lineStart + xai + 1] += d * xmf;
                    }
                    else
                    {
                        float s = 1f / (xb - xa);
                        float xaf = xa - xaFloor;
                        float a0 = 0.5f * s * (1f - xaf) * (1f - xaf);
                        float xbf = xb - xbCeil + 1f;
                        float am = 0.5f * s * xbf * xbf;
                        _accumulation[lineStart + xai] += d * a0;
                        if (xbi == xai + 2)
                        {
                            _accumulation[lineStart + xai + 1] += d * (1f - a0 - am);
                        }
                        else
                        {
                            float a1 = s * (1.5f - xaf);
                            _accumulation[lineStart + xai + 1] += d * (a1 - a0);
                            for (int xi = xai + 2; xi < xbi - 1; xi++)
                            {
                                _accumulation[lineStart + xi] += d * s;
                            }
                            float a2 = a1 + (xbi - xai - 3) * s;
                            _accumulation[lineStart + xbi - 1] += d * (1f - a2 - am);
                        }
                        _accumulation[lineStart + xbi] += d * am;
                    }
                    x = xNext;
                }
            }

            public byte[] Accumulate()
            {
                int count = _width * _height;
                byte[] result = new byte[count];
                float accumulator = 0f;
                for (int i = 0; i < count; i++)
                {
                    accumulator += _accumulation[i];
                    float coverage = MathF.Min(1f, MathF.Abs(accumulator));
                    result[i] = (byte)(coverage * 255f + 0.5f);
                }
                return result;
            }
        }
    }
}
