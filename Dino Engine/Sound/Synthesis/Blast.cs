using Dino_Engine.Sound.Dsp;

namespace Dino_Engine.Sound.Synthesis
{
    /// <summary>Shock waves: explosions, thunder and anything else that moves the air faster than sound.</summary>
    public static class Blast
    {
        private const float SpeedOfSound = 343f;

        /// <summary>
        /// The pressure of a blast at a distance, the Friedlander waveform: an instant rise, then a fall through zero into a longer
        /// suction. The rise is what no filtered noise can imitate, and the suction is the body of the boom.
        /// </summary>
        public static Signal Friedlander(Take take, float positive, float seconds, float decay = 1.6f)
        {
            Signal output = take.Track(seconds);
            for (int i = 0; i < output.Length; i++)
            {
                float t = i * (1f / AudioFormat.SampleRate);
                float x = t / positive;
                output.Samples[i] = (1f - x) * MathF.Exp(-decay * x);
            }
            return output;
        }

        /// <summary>The N-wave a supersonic thing leaves: straight down from a sudden peak to a sudden trough.</summary>
        public static void NWave(float[] target, int length, float at, float seconds, float level)
        {
            int start = (int)(at * AudioFormat.SampleRate);
            int span = Math.Max((int)(seconds * AudioFormat.SampleRate), 2);
            for (int i = 0; i < span && start + i < length; i++)
            {
                if (start + i < 0) continue;
                target[start + i] += level * (1f - 2f * i / (span - 1f));
            }
        }

        /// <summary>
        /// Thunder, from the physics rather than from a filter sweep (after Few, and Ribner and Roy). A lightning channel is a crooked
        /// path a few kilometres long, and every short straight piece of it fires an N-wave the moment it's struck, nearly all
        /// sideways, at right angles to itself. The rumble is simply these arriving one after another from all along the path, each a
        /// different distance away, and the great claps are stretches of it that happen to lie square to the listener.
        /// </summary>
        public static Signal Thunder(Take take, float seconds, float distance, float channelLength = 3200f, int segments = 7000)
        {
            Signal output = take.Track(seconds);
            SoundRandom random = take.Random;
            float[] samples = output.Samples;

            // Up from the ground beside the listener, wandering as it goes, with branches here and there.
            float x = distance * random.Range(0.8f, 1f), y = distance * random.Range(-0.5f, 0.5f), z = 0f;
            float heading = random.Range(0f, MathF.Tau);
            float pitch = 1.1f;
            float piece = channelLength / segments;

            float nearest = float.MaxValue;
            for (int s = 0; s < segments; s++)
            {
                // A long-tailed random walk: mostly small turns, with an occasional hard kink, as the channel really has.
                heading += random.Gaussian() * 0.5f + (random.Chance(0.03f) ? random.Gaussian() * 1.6f : 0f);
                pitch = Math.Clamp(pitch + random.Gaussian() * 0.22f, 0.2f, 1.45f);
                float dx = MathF.Cos(heading) * MathF.Cos(pitch);
                float dy = MathF.Sin(heading) * MathF.Cos(pitch);
                float dz = MathF.Sin(pitch);
                x += dx * piece;
                y += dy * piece;
                z += dz * piece;

                float range = MathF.Sqrt(x * x + y * y + z * z);
                nearest = MathF.Min(nearest, range);
                float toX = -x / range, toY = -y / range, toZ = -z / range;

                // Facing the listener side on, a piece is heard in full; end on, hardly at all.
                float along = dx * toX + dy * toY + dz * toZ;
                float side = MathF.Pow(1f - along * along, 2.5f);
                float level = side / MathF.Max(range, 100f) * random.Range(0.4f, 1f);

                // Older waves have stretched: a pulse leaves as milliseconds and arrives far longer, which is why thunder's peak is around 100 Hz.
                float width = (0.004f + range * 0.000012f) * random.LogRange(0.5f, 3.4f);
                float arrival = range / SpeedOfSound;
                NWave(samples, output.Length, arrival, width, level * 400f);
            }

            // Everything is measured from the first sound to arrive, not from the flash.
            int lead = (int)(nearest / SpeedOfSound * AudioFormat.SampleRate);
            Array.Copy(samples, lead, samples, 0, output.Length - lead);
            Array.Clear(samples, output.Length - lead, lead);
            return output;
        }

        /// <summary>
        /// Air takes the top off with distance, far more than the bottom: about f squared. So a far blast is a thud and a near
        /// one a crack.
        /// </summary>
        public static Signal ThroughAir(this Signal signal, float distance)
        {
            float cutoff = Math.Clamp(26000f / (1f + distance * 0.05f), 90f, 20000f);
            return signal.Lowpass(cutoff, 0.7071f).Lowpass(cutoff * 1.6f, 0.7071f);
        }
    }
}
