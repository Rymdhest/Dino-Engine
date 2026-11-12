using Dino_Engine.GUI;
using OpenTK.Windowing.Common.Input;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// The game's own cursor, a Defender's arrowhead: an ivory blade with a gold spine, inked all round and casting a soft shadow,
    /// so it reads over a field, a fire, a spell or a crowd alike, where the system's arrow got lost. Over anything that can be
    /// clicked it's gilded. It's drawn here from signed distances, never stored, at the interface's scale, and handed to the
    /// system as a real cursor, so it costs no frame and lags nothing. The system's text and resize cursors are left alone.
    /// </summary>
    internal static class GameCursor
    {
        // Sizes at an interface scale of one, in pixels.
        private const float Length = 31f;        // From the tip to the barbs, along the blade.
        private const float HalfWidth = 10.6f;    // Each barb out from the blade's axis.
        private const float Notch = 0.70f;       // How far back the notch between the barbs sits, as a share of the length.
        private const float Lean = 22f;          // Degrees the blade leans off upright, as a pointer does.
        private const float Ink = 2.1f;          // The outline's width.
        private const float Margin = 3.2f;       // Room round the tip for the outline's edge.
        private const float ShadowReach = 3.6f;  // How far the shadow fades out.
        private const float ShadowX = 1.4f, ShadowY = 2.0f;
        private const float ShadowAlpha = 0.55f;
        private const float Spine = 0.85f;       // Half the gold spine's width.
        private const float Canvas = 44f;

        private static readonly Dictionary<(bool, int), MouseCursor> Made = new();

        public static void Install(GuiContext gui) => gui.CursorStyle = Choose;

        private static MouseCursor Choose(MouseCursor asked, float scale)
        {
            bool hand = ReferenceEquals(asked, MouseCursor.Hand);
            if (!hand && !ReferenceEquals(asked, MouseCursor.Default)) return asked;
            int steps = Math.Clamp((int)MathF.Round(scale * 8f), 6, 24);   // An eighth at a time, so a window being resized makes few.
            if (!Made.TryGetValue((hand, steps), out var cursor))
            {
                cursor = Draw(hand, steps / 8f);
                Made[(hand, steps)] = cursor;
            }
            return cursor;
        }

        private static MouseCursor Draw(bool gilded, float k)
        {
            int size = (int)MathF.Ceiling(Canvas * k);
            float lean = Lean * MathF.PI / 180f;
            float ax = MathF.Sin(lean), ay = MathF.Cos(lean);       // Down the blade from its tip.
            float px = ay, py = -ax;                                // Across it, towards the right barb.
            float tipX = Margin * k;
            float tipY = Margin * k;
            // The left barb reaches a little left of the tip; keep it on the canvas.
            float leftX = Length * k * ax - HalfWidth * k * px;
            if (leftX < 0f) tipX -= leftX;

            float L = Length * k, W = HalfWidth * k;
            var shape = new (float X, float Y)[]
            {
                (tipX, tipY),
                (tipX + L * ax - W * px, tipY + L * ay - W * py),
                (tipX + L * Notch * ax, tipY + L * Notch * ay),
                (tipX + L * ax + W * px, tipY + L * ay + W * py),
            };

            // Ivory, or gold over what can be clicked, lit from the upper left: the facet facing it is the brighter.
            (float R, float G, float B) lit = gilded ? (255f, 222f, 146f) : (255f, 250f, 236f);
            (float R, float G, float B) shaded = gilded ? (214f, 160f, 72f) : (234f, 220f, 192f);
            (float R, float G, float B) spine = gilded ? (255f, 248f, 232f) : (196f, 146f, 58f);
            (float R, float G, float B) ink = (14f, 11f, 9f);

            float ink2 = Ink * k, reach = ShadowReach * k, spineHalf = Spine * k;
            var data = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = x + 0.5f, cy = y + 0.5f;

                    // Back to front: shadow, ink, blade, spine, each over the last.
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    float shadowDistance = Distance(shape, cx - ShadowX * k, cy - ShadowY * k);
                    float shadow = ShadowAlpha * (1f - Smooth(Math.Max(shadowDistance, 0f) / reach));
                    Over(ref r, ref g, ref b, ref a, (0f, 0f, 0f), shadow);

                    float d = Distance(shape, cx, cy);
                    Over(ref r, ref g, ref b, ref a, ink, Coverage(d - ink2));

                    float u = (cx - tipX) * ax + (cy - tipY) * ay;     // Along the blade.
                    float v = (cx - tipX) * px + (cy - tipY) * py;     // Across it.
                    float facet = Math.Clamp(v / (0.8f * k) + 0.5f, 0f, 1f);
                    var blade = Mix(lit, shaded, facet);
                    Over(ref r, ref g, ref b, ref a, blade, Coverage(d));

                    // The spine runs from near the tip to the notch, inside the blade's own edge.
                    float along = Math.Clamp((u - 0.16f * L) / (L * Notch - 0.16f * L), 0f, 1f);
                    float onSpine = u > 0.16f * L && u < L * Notch ? Coverage(MathF.Abs(v) - spineHalf * (0.6f + 0.6f * along)) : 0f;
                    Over(ref r, ref g, ref b, ref a, spine, onSpine * Coverage(d + 1.2f * k));

                    int i = (y * size + x) * 4;
                    if (a > 0f) { r /= a; g /= a; b /= a; }
                    data[i] = (byte)Math.Clamp(r + 0.5f, 0f, 255f);
                    data[i + 1] = (byte)Math.Clamp(g + 0.5f, 0f, 255f);
                    data[i + 2] = (byte)Math.Clamp(b + 0.5f, 0f, 255f);
                    data[i + 3] = (byte)Math.Clamp(a * 255f + 0.5f, 0f, 255f);
                }
            }
            return new MouseCursor((int)MathF.Round(tipX), (int)MathF.Round(tipY), size, size, data);
        }

        /// <summary>Paints a colour over what's there by its coverage, keeping the result premultiplied until the end.</summary>
        private static void Over(ref float r, ref float g, ref float b, ref float a, (float R, float G, float B) colour, float alpha)
        {
            if (alpha <= 0f) return;
            r = colour.R * alpha + r * (1f - alpha);
            g = colour.G * alpha + g * (1f - alpha);
            b = colour.B * alpha + b * (1f - alpha);
            a = alpha + a * (1f - alpha);
        }

        private static (float, float, float) Mix((float R, float G, float B) from, (float R, float G, float B) to, float t)
            => (from.R + (to.R - from.R) * t, from.G + (to.G - from.G) * t, from.B + (to.B - from.B) * t);

        /// <summary>How much of a pixel a shape covers, from its signed distance there (negative inside).</summary>
        private static float Coverage(float distance) => Math.Clamp(0.5f - distance, 0f, 1f);

        private static float Smooth(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Signed distance to a polygon, convex or not, negative inside (after Inigo Quilez).</summary>
        private static float Distance((float X, float Y)[] points, float x, float y)
        {
            float dx = x - points[0].X, dy = y - points[0].Y;
            float best = dx * dx + dy * dy;
            float sign = 1f;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i, i++)
            {
                float ex = points[j].X - points[i].X, ey = points[j].Y - points[i].Y;
                float wx = x - points[i].X, wy = y - points[i].Y;
                float t = Math.Clamp((wx * ex + wy * ey) / (ex * ex + ey * ey), 0f, 1f);
                float bx = wx - ex * t, by = wy - ey * t;
                best = Math.Min(best, bx * bx + by * by);
                bool c1 = y >= points[i].Y, c2 = y < points[j].Y, c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) sign = -sign;
            }
            return sign * MathF.Sqrt(best);
        }
    }
}
