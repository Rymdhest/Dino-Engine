using OpenTK.Mathematics;

namespace Dino_Engine.Particles
{
    /// <summary>The light something glowing gives off, by how hot it is. Each is a row of a texture that glowing particles look up their heat in.</summary>
    public enum ParticlePalette
    {
        Fire,       // A black body, as soot in a flame is: deep red through orange to a yellow-white core.
        Frost,
        Lightning,
        Poison,
        Arcane,
        Spirit,     // The cold green of old stones and holy places.
        White,      // Nothing but brightness, for a particle's tint to colour.
    }

    /// <summary>
    /// What each <see cref="ParticlePalette"/> comes to: radiance by heat, 0 to 1. Colours are radiance in a scene lit by a sun of about 13 (exposure 0.42 before an ACES fit), so
    /// only the hottest part of anything is bright enough to go white: the middle stays saturated, or a player can't tell fire from frost.
    /// </summary>
    public static class Palettes
    {
        public const int Width = 256;
        public static readonly int Count = Enum.GetValues<ParticlePalette>().Length;

        // Fire's heat is temperature, from the Draper point, where a body starts to glow, to the core of a wood fire.
        private const float ColdestKelvin = 800f;
        private const float HottestKelvin = 2200f;
        private const float FireCore = 36f;         // Radiance of heat 1: well over the bloom threshold, and white once mapped.

        /// <summary>Radiance at <paramref name="heat"/>, 0 to 1.</summary>
        public static Vector3 Sample(ParticlePalette palette, float heat)
        {
            heat = Math.Clamp(heat, 0f, 1f);
            switch (palette)
            {
                case ParticlePalette.Fire:
                    // Glow climbs very steeply with temperature. Planck's law has it climb by thousands across a flame, which no screen can show, so this keeps its shape and not its range.
                    float kelvin = ColdestKelvin + (HottestKelvin - ColdestKelvin) * heat;
                    return Blackbody.Chromaticity(kelvin) * (FireCore * heat * heat * heat);
                case ParticlePalette.Frost:
                    return Stops(heat, (0f, new Vector3(0.02f, 0.06f, 0.3f) * 0.2f), (0.45f, new Vector3(0.12f, 0.5f, 1f) * 2.4f), (0.8f, new Vector3(0.5f, 0.85f, 1f) * 7f), (1f, new Vector3(0.88f, 0.96f, 1f) * 16f));
                case ParticlePalette.Lightning:
                    return Stops(heat, (0f, new Vector3(0.3f, 0.12f, 0.9f) * 0.4f), (0.45f, new Vector3(0.35f, 0.42f, 1f) * 4f), (0.8f, new Vector3(0.7f, 0.78f, 1f) * 14f), (1f, new Vector3(0.94f, 0.96f, 1f) * 44f));
                case ParticlePalette.Poison:
                    return Stops(heat, (0f, new Vector3(0.04f, 0.2f, 0.02f) * 0.2f), (0.45f, new Vector3(0.24f, 0.8f, 0.04f) * 2f), (0.8f, new Vector3(0.56f, 1f, 0.2f) * 5f), (1f, new Vector3(0.86f, 1f, 0.56f) * 12f));
                case ParticlePalette.Arcane:
                    return Stops(heat, (0f, new Vector3(0.2f, 0.02f, 0.45f) * 0.3f), (0.45f, new Vector3(0.55f, 0.1f, 1f) * 2.6f), (0.8f, new Vector3(0.8f, 0.45f, 1f) * 7f), (1f, new Vector3(0.96f, 0.85f, 1f) * 18f));
                case ParticlePalette.Spirit:
                    return Stops(heat, (0f, new Vector3(0.02f, 0.25f, 0.15f) * 0.3f), (0.45f, new Vector3(0.2f, 0.95f, 0.6f) * 2.4f), (0.8f, new Vector3(0.55f, 1f, 0.85f) * 6f), (1f, new Vector3(0.88f, 1f, 0.95f) * 15f));
                default:
                    return new Vector3(12f * heat * heat);
            }
        }

        private static Vector3 Stops(float heat, params (float At, Vector3 Colour)[] stops)
        {
            if (heat <= stops[0].At) return stops[0].Colour * (heat / MathF.Max(stops[0].At, 1e-4f));
            for (int i = 1; i < stops.Length; i++)
            {
                if (heat > stops[i].At) continue;
                float share = (heat - stops[i - 1].At) / (stops[i].At - stops[i - 1].At);
                // Brightness goes through the stops as a power, as it does for anything hot, and colour as a line.
                Vector3 a = stops[i - 1].Colour;
                Vector3 b = stops[i].Colour;
                float la = MathF.Max(MathF.Max(a.X, a.Y), a.Z);
                float lb = MathF.Max(MathF.Max(b.X, b.Y), b.Z);
                Vector3 hue = Vector3.Lerp(a / MathF.Max(la, 1e-6f), b / MathF.Max(lb, 1e-6f), share);
                float brightness = la <= 0f ? lb * share : la * MathF.Pow(lb / la, share);
                return hue * brightness;
            }
            return stops[^1].Colour;
        }
    }

    /// <summary>
    /// The colour of a black body, from Planck's law over the CIE 1931 colour matching functions (the multi-lobe fits of Wyman, Sloan and Shirley, 2013), as linear sRGB with its
    /// largest channel 1. Brightness is left to whoever asks.
    /// </summary>
    public static class Blackbody
    {
        public static Vector3 Chromaticity(float kelvin)
        {
            double x = 0, y = 0, z = 0;
            for (int nm = 380; nm <= 780; nm += 5)
            {
                double lambda = nm;
                double radiance = Planck(lambda * 1e-9, kelvin);
                x += radiance * (1.056 * Lobe(lambda, 599.8, 37.9, 31.0) + 0.362 * Lobe(lambda, 442.0, 16.0, 26.7) - 0.065 * Lobe(lambda, 501.1, 20.4, 26.2));
                y += radiance * (0.821 * Lobe(lambda, 568.8, 46.9, 40.5) + 0.286 * Lobe(lambda, 530.9, 16.3, 31.1));
                z += radiance * (1.217 * Lobe(lambda, 437.0, 11.8, 36.0) + 0.681 * Lobe(lambda, 459.0, 26.0, 13.8));
            }
            var rgb = new Vector3(
                (float)(3.2406 * x - 1.5372 * y - 0.4986 * z),
                (float)(-0.9689 * x + 1.8758 * y + 0.0415 * z),
                (float)(0.0557 * x - 0.2040 * y + 1.0570 * z));
            rgb = Vector3.ComponentMax(rgb, Vector3.Zero);
            float largest = MathF.Max(MathF.Max(rgb.X, rgb.Y), rgb.Z);
            return largest > 0f ? rgb / largest : Vector3.Zero;
        }

        private static double Planck(double metres, double kelvin)
        {
            const double h = 6.62607015e-34, c = 2.99792458e8, k = 1.380649e-23;
            return 2.0 * h * c * c / (Math.Pow(metres, 5) * (Math.Exp(h * c / (metres * k * kelvin)) - 1.0));
        }

        private static double Lobe(double lambda, double mean, double below, double above)
        {
            double t = (lambda - mean) / (lambda < mean ? below : above);
            return Math.Exp(-0.5 * t * t);
        }
    }
}
