using Dino_Engine.Util;
using OpenTK.Mathematics;
using System.Globalization;

namespace Dino_Engine.GUI
{
    /// <summary>Helpers for using the engine's <see cref="Colour"/> in the GUI.</summary>
    public static class GuiColors
    {
        public static Colour Rgba(int red, int green, int blue, int alpha = 255) => new Colour(red / 255f, green / 255f, blue / 255f, 1f, alpha / 255f);
        public static Colour Rgba(float red, float green, float blue, float alpha = 1f) => new Colour(red, green, blue, 1f, alpha);

        /// <summary>Parses "#RGB", "#RRGGBB" or "#RRGGBBAA" (the '#' is optional).</summary>
        public static Colour Hex(string hex)
        {
            string value = hex.TrimStart('#');
            if (value.Length == 3) value = string.Concat(value[0], value[0], value[1], value[1], value[2], value[2]);
            if (value.Length != 6 && value.Length != 8) throw new FormatException($"Invalid hex colour '{hex}'.");
            int r = int.Parse(value.Substring(0, 2), NumberStyles.HexNumber);
            int g = int.Parse(value.Substring(2, 2), NumberStyles.HexNumber);
            int b = int.Parse(value.Substring(4, 2), NumberStyles.HexNumber);
            int a = value.Length == 8 ? int.Parse(value.Substring(6, 2), NumberStyles.HexNumber) : 255;
            return Rgba(r, g, b, a);
        }

        public static Colour Transparent => Rgba(0, 0, 0, 0);
        public static Colour White => Rgba(255, 255, 255);
        public static Colour Black => Rgba(0, 0, 0);

        public static Colour WithAlpha(this Colour colour, float alpha)
        {
            colour.Alpha = alpha;
            return colour;
        }

        public static Colour MultiplyAlpha(this Colour colour, float factor)
        {
            colour.Alpha *= factor;
            return colour;
        }

        /// <summary>Scales the brightness (factor &gt; 1 lightens, &lt; 1 darkens) keeping alpha.</summary>
        public static Colour Shade(this Colour colour, float factor)
        {
            Vector4 v = colour.ToVector4();
            return Rgba(MathF.Min(1f, v.X * factor), MathF.Min(1f, v.Y * factor), MathF.Min(1f, v.Z * factor), v.W);
        }

        public static Colour Lerp(Colour from, Colour to, float t)
        {
            Vector4 a = from.ToVector4();
            Vector4 b = to.ToVector4();
            Vector4 v = Vector4.Lerp(a, b, Math.Clamp(t, 0f, 1f));
            return Rgba(v.X, v.Y, v.Z, v.W);
        }

        public static bool IsVisible(this Colour colour) => colour.Alpha > 0.0001f;

        /// <summary>Packs to RGBA8 (red in the lowest byte) for vertex data. Intensity is applied and the result clamped.</summary>
        public static uint Pack(this Colour colour, float alphaMultiplier = 1f)
        {
            Vector4 v = colour.ToVector4();
            uint r = (uint)(Math.Clamp(v.X, 0f, 1f) * 255f + 0.5f);
            uint g = (uint)(Math.Clamp(v.Y, 0f, 1f) * 255f + 0.5f);
            uint b = (uint)(Math.Clamp(v.Z, 0f, 1f) * 255f + 0.5f);
            uint a = (uint)(Math.Clamp(v.W * alphaMultiplier, 0f, 1f) * 255f + 0.5f);
            return r | (g << 8) | (b << 16) | (a << 24);
        }
    }
}
