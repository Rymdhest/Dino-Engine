using Dino_Engine.Sound.Dsp;

namespace Dino_Engine.Sound.Synthesis
{
    /// <summary>What one particle of a crowd sounds like when it collides.</summary>
    public struct GrainStyle
    {
        public float LowHz;         // Each grain rings at a pitch drawn from here to HighHz, evenly over the octaves.
        public float HighHz;
        public float MinDecay;      // Seconds the shortest grain takes to fall 63%, and the longest.
        public float MaxDecay;
        public int Partials;        // How many modes each grain has. One is a tick, three is a small hard thing.
        public float Noise;         // 0 pure ringing, 1 pure noise burst: from a pebble to a blade of grass.
        public float Spread;        // How much louder the loudest grain is than the quietest, in dB.
    }

    /// <summary>
    /// A crowd of small collisions: Perry Cook's physically inspired stochastic event model. Simulating thousands of colliding particles
    /// showed that the sound-making events simply arrive at random, as a Poisson process, with an energy that drains away
    /// exponentially, so the simulation can be replaced by exactly that. Gravel, snow, coins, debris, crackling
    /// and rain on a roof are all this one idea with different particles.
    /// </summary>
    public static class Grains
    {
        /// <summary>
        /// A stream of grains. <paramref name="rate"/> is events per second over time, and <paramref name="energy"/> how hard each one is
        /// at that moment: what's pressing on the gravel, say.
        /// </summary>
        public static Signal Stream(Take take, float seconds, Curve rate, Curve energy, in GrainStyle style)
        {
            Signal output = take.Track(seconds);
            SoundRandom random = take.Random;

            foreach (float time in Times(random, seconds, rate))
            {
                float level = energy.At(time);
                if (level <= 0.001f) continue;
                float spread = AudioFormat.DecibelsToGain(-style.Spread * random.Float() * random.Float());
                Grain(output, random, style, time, level * spread);
            }
            return output;
        }

        /// <summary>
        /// When events happen, for a rate that changes over time. Waiting out an exponential time at the present rate won't do: a rate that
        /// starts at nothing would wait for ever. So time is walked in steps short enough that two events in one hardly ever matter.
        /// </summary>
        public static IEnumerable<float> Times(SoundRandom random, float seconds, Curve rate)
        {
            const float Step = 0.00025f;
            for (float time = 0f; time < seconds; time += Step)
            {
                float expected = rate.At(time) * Step;
                while (expected > 0f)
                {
                    if (random.Float() < expected) yield return time + random.Float() * Step;
                    expected -= 1f;
                }
            }
        }

        /// <summary>One collision, added in at <paramref name="at"/> seconds.</summary>
        public static void Grain(Signal target, SoundRandom random, in GrainStyle style, float at, float level)
        {
            int start = (int)(at * AudioFormat.SampleRate);
            float decay = random.LogRange(style.MinDecay, style.MaxDecay);
            int length = Math.Min((int)(decay * 6.5f * AudioFormat.SampleRate), target.Length - start);
            if (length <= 1) return;

            float[] samples = target.Samples;
            float tone = level * (1f - style.Noise);
            float stepDown = MathF.Exp(-1f / (decay * AudioFormat.SampleRate));

            // Each mode is a recursion, which beats calling sin for every sample of every grain.
            float pitch = random.LogRange(style.LowHz, style.HighHz);
            int partials = Math.Max(style.Partials, 1);
            for (int p = 0; p < partials; p++)
            {
                float f = pitch * (1f + p * random.Range(0.6f, 1.7f));
                if (f > 20000f) break;
                float w = MathF.Tau * f / AudioFormat.SampleRate;
                float amplitude = tone / (p + 1f);
                float c = 2f * MathF.Cos(w);
                float y1 = MathF.Sin(w) * amplitude, y2 = 0f;
                float envelope = 1f;
                for (int i = 0; i < length; i++)
                {
                    samples[start + i] += y1 * envelope;
                    float y = c * y1 - y2;
                    y2 = y1;
                    y1 = y;
                    envelope *= stepDown;
                }
            }

            if (style.Noise <= 0f) return;
            float noise = level * style.Noise;
            var band = Svf.Create(pitch, 1.4f);
            float noiseEnvelope = noise;
            for (int i = 0; i < length; i++)
            {
                samples[start + i] += band.BandpassNormal(random.Bipolar()) * noiseEnvelope * 2.2f;
                noiseEnvelope *= stepDown;
            }
        }

        /// <summary>
        /// A shaken handful of things: energy put in, draining away, and collisions happening in proportion to it. <paramref name="count"/> is
        /// how many there are, and <paramref name="drain"/> how quickly they settle, in seconds.
        /// </summary>
        public static Signal Settle(Take take, float seconds, float count, float drain, in GrainStyle style)
        {
            Curve energy = Curve.Settle(1f, 0f, drain);
            return Stream(take, seconds, energy * (count / drain), energy, style);
        }
    }
}
