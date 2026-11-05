using Dino_Engine.Sound.Dsp;

namespace Dino_Engine.Sound.Synthesis
{
    /// <summary>
    /// Water. The sound of water is not the water: it's the bubbles. A bubble trapped below the surface rings like a spring, at
    /// a pitch set by its size alone, which Minnaert worked out in 1933: f = 3.26 / r, r in metres, so a 1 mm bubble sings at 3.3 kHz. It
    /// rises in pitch as it nears the surface and dies away quickly. Van den Doel showed that a stream, a pour, a splash or rain are just
    /// such bubbles in different numbers and sizes, which is what's here.
    /// </summary>
    public static class Liquid
    {
        /// <summary>Lays one bubble in. <paramref name="radius"/> in metres; <paramref name="rise"/> how far the pitch climbs, as a share per second of decay.</summary>
        public static void Bubble(Signal target, float at, float radius, float level, float rise = 0.1f)
        {
            float frequency = 3.26f / radius;
            if (frequency > 18000f) return;

            // Damping by radiation and heat, as fitted by van den Doel.
            float decay = 0.13f / radius + 0.0072f * MathF.Pow(radius, -1.5f);
            float climb = rise * decay;

            int start = (int)(at * AudioFormat.SampleRate);
            int length = Math.Min((int)(6.5f / decay * AudioFormat.SampleRate), target.Length - start);
            if (length <= 1) return;

            float phase = 0f;
            float[] samples = target.Samples;
            const float Step = 1f / AudioFormat.SampleRate;
            float fall = MathF.Exp(-decay * Step);
            float envelope = level;

            // It starts from a little way up its first swing, as the air is already compressed, which is the click at a bubble's birth.
            int attack = Math.Min(24, length);
            for (int i = 0; i < length; i++)
            {
                float t = i * Step;
                phase += MathF.Tau * frequency * (1f + climb * t) * Step;
                if (phase > MathF.Tau) phase -= MathF.Tau;
                float edge = i < attack ? i / (float)attack : 1f;
                samples[start + i] += MathF.Sin(phase) * envelope * edge;
                envelope *= fall;
            }
        }

        /// <summary>
        /// A population of bubbles: <paramref name="rate"/> a second, between two radii, with small ones far more common than large
        /// (a power law, as measured for real entrained air, <paramref name="slope"/> around 2 to 3).
        /// </summary>
        public static Signal Bubbles(Take take, float seconds, Curve rate, Curve level, float smallest, float largest, float slope = 2.5f, float rise = 0.1f)
        {
            Signal output = take.Track(seconds);
            SoundRandom random = take.Random;

            float exponent = 1f - slope;
            float a = MathF.Pow(smallest, exponent), b = MathF.Pow(largest, exponent);
            foreach (float time in Grains.Times(random, seconds, rate))
            {
                float loudness = level.At(time);
                if (loudness <= 0.001f) continue;

                // Inverse transform sampling of r^-slope, between the two. Bigger bubbles move more air.
                float radius = MathF.Pow(a + (b - a) * random.Float(), 1f / exponent);
                float amplitude = loudness * MathF.Pow(radius / largest, 0.6f);
                Bubble(output, time, radius, amplitude * random.Range(0.4f, 1f), rise);
            }
            return output;
        }
    }
}
