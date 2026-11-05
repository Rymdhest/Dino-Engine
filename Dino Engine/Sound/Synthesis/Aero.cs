using Dino_Engine.Sound.Dsp;

namespace Dino_Engine.Sound.Synthesis
{
    /// <summary>
    /// Sound from air moving past something. A body in a stream sheds vortices at f = St U / d, with St about 0.2: an Aeolian tone,
    /// from a singing wire to a swung blade. It radiates as a compact dipole, so its pressure goes with the cube of the speed. That
    /// steepness is why a whoosh is so brief, as nearly all of its energy comes at the fastest part of the swing. Selfridge,
    /// Moffat and Reiss build a sword from a row of such sources along the blade, each moving at its own speed, and
    /// found that listeners took a simplified version for the more authentic. This is that.
    /// </summary>
    public static class Aero
    {
        private const float Strouhal = 0.2f;

        /// <summary>What's being swung.</summary>
        public struct Blade
        {
            public float Length;        // Metres from the hand to the tip.
            public float Thickness;     // Metres across, as the air meets it: what sets the pitch.
            public int Sources;         // Compact sources along it.
            public float Turbulence;    // 0 a clean edge, 1 a broad and ragged thing like a club or a cloak.
        }

        /// <summary>
        /// A swing: <paramref name="speed"/> is the angular speed in radians a second over time. Left and right are worked out by
        /// where the tip is in its arc, so it crosses the listener as it swings, which is most of what makes it feel like a stroke.
        /// </summary>
        public static (Signal left, Signal right) Swing(Take take, float seconds, Curve speed, in Blade blade, Curve arc)
        {
            Signal left = take.Track(seconds);
            Signal right = take.Track(seconds);
            SoundRandom random = take.Random;
            int count = Math.Max(blade.Sources, 1);

            for (int s = 0; s < count; s++)
            {
                float along = (s + 0.6f) / count;
                float radius = blade.Length * along;

                // The blade is not the same all the way down, and that's what stops the sources falling into one note.
                float thickness = blade.Thickness * random.Range(0.75f, 1.3f) * (1.15f - 0.4f * along);
                var filter = new Svf();
                var low = new PinkNoise();
                var flutter = new OnePole();
                flutter.SetCutoff(random.Range(30f, 80f));
                float q = 0.9f + 1.5f * (1f - blade.Turbulence);

                for (int i = 0; i < left.Length; i++)
                {
                    float t = i * (1f / AudioFormat.SampleRate);
                    float u = speed.At(t) * radius;
                    if (i % 16 == 0) filter.Set(MathF.Max(Strouhal * u / thickness, 20f), q);

                    float noise = low.Next(random.Bipolar()) * (1f - blade.Turbulence * 0.3f) + random.Bipolar() * 0.3f;

                    // Vortices come off in bursts, not evenly, which is the roughness in a real swish.
                    float rough = 1f + flutter.Lowpass(random.Bipolar()) * 5f;
                    float value = filter.BandpassNormal(noise) * rough;

                    // Pressure as the cube of the speed, normalised to a good fast swing at about 30 m/s.
                    float scale = u * (1f / 30f);
                    value *= scale * scale * scale;

                    float pan = Math.Clamp(arc.At(t), -1f, 1f) * along;
                    float angle = (pan + 1f) * (MathF.PI * 0.25f);
                    left.Samples[i] += value * MathF.Cos(angle);
                    right.Samples[i] += value * MathF.Sin(angle);
                }
            }

            float gain = 1f / MathF.Sqrt(count);
            left.Scale(gain);
            right.Scale(gain);
            return (left, right);
        }

        /// <summary>A thin singing note at the Aeolian frequency for a wind and an obstacle: wires, reeds, a rope. Used for things that whistle.</summary>
        public static float ToneFrequency(float windSpeed, float diameter) => Strouhal * windSpeed / diameter;
    }
}
