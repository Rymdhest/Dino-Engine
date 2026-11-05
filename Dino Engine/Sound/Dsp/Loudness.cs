using System.Buffers;

namespace Dino_Engine.Sound.Dsp
{
    /// <summary>
    /// Measures loudness as ITU-R BS.1770 does: K-weighted power, in LUFS. Peak level says almost nothing about how loud a
    /// thing sounds, and a library of sounds levelled by peak is a mess, so every take is levelled by this. It runs for every take
    /// while a game is loading, so it streams through the sound with one small buffer, as anything the length of a sound would land on the
    /// large object heap and bring on full collections.
    /// </summary>
    public static class Loudness
    {
        private const float BlockSeconds = 0.4f;    // BS.1770's momentary window.
        private const float ShortSeconds = 0.1f;    // For sounds too short for that to mean anything.

        // The standard gives these at 48 kHz, which is the rate everything here runs at.
        private static Biquad HeadFilter() => Biquad.FromCoefficients(1.53512485958697, -2.69169618940638, 1.19839281085285, -1.69065929318241, 0.73248077421585);
        private static Biquad WeightingFilter() => Biquad.FromCoefficients(1.0, -2.0, 1.0, -1.99004745483398, 0.99007225036621);

        private static float ToLufs(double meanSquare) => -0.691f + 10f * MathF.Log10(MathF.Max((float)meanSquare, 1e-12f));

        // Calls back with the mean K-weighted power of each window, one quarter of a window apart.
        private static void ForEachWindow<T>(ReadOnlySpan<float> left, ReadOnlySpan<float> right, int window, ref T state, WindowAction<T> action)
        {
            Biquad headLeft = HeadFilter(), weightLeft = WeightingFilter();
            Biquad headRight = HeadFilter(), weightRight = WeightingFilter();
            int step = Math.Max(window / 4, 1);
            double[] ring = ArrayPool<double>.Shared.Rent(window);
            Array.Clear(ring, 0, window);
            double sum = 0.0;
            bool mono = right.IsEmpty;

            for (int i = 0; i < left.Length; i++)
            {
                float l = weightLeft.Process(headLeft.Process(left[i]));

                // Played in the middle a mono signal reaches both ears, and the standard sums channels.
                double power;
                if (mono) power = 2.0 * l * l;
                else
                {
                    float r = weightRight.Process(headRight.Process(right[i]));
                    power = (double)l * l + (double)r * r;
                }

                int slot = i % window;
                sum += power - ring[slot];
                ring[slot] = power;

                if (i + 1 >= window && (i + 1 - window) % step == 0) action(ref state, sum / window);
            }

            ArrayPool<double>.Shared.Return(ring);
        }

        private delegate void WindowAction<T>(ref T state, double meanSquare);

        /// <summary>How loud its loudest moment is, over a window short enough to mean something for a single footstep.</summary>
        public static float Peak(ReadOnlySpan<float> left, ReadOnlySpan<float> right = default)
        {
            if (left.IsEmpty) return -120f;
            int window = Math.Min((int)(ShortSeconds * AudioFormat.SampleRate), left.Length);
            double loudest = 0.0;
            ForEachWindow(left, right, window, ref loudest, static (ref double best, double meanSquare) => best = Math.Max(best, meanSquare));
            return ToLufs(loudest);
        }

        private struct Gated
        {
            public double Sum;
            public int Count;
            public double Gate;
        }

        /// <summary>
        /// Integrated loudness: momentary blocks overlapping by 75%, gated at -70 LUFS and then 10 LU below their mean, so a
        /// long quiet passage doesn't count against a loud one.
        /// </summary>
        public static float Integrated(ReadOnlySpan<float> left, ReadOnlySpan<float> right = default)
        {
            int window = (int)(BlockSeconds * AudioFormat.SampleRate);
            if (left.Length < window) return Peak(left, right);

            // Absolute gate first, to find the mean the relative gate is measured from.
            var absolute = new Gated { Gate = -70.0 };
            ForEachWindow(left, right, window, ref absolute, Gate);
            if (absolute.Count == 0) return -120f;

            var relative = new Gated { Gate = ToLufs(absolute.Sum / absolute.Count) - 10.0 };
            ForEachWindow(left, right, window, ref relative, Gate);
            return relative.Count > 0 ? ToLufs(relative.Sum / relative.Count) : -120f;
        }

        private static void Gate(ref Gated state, double meanSquare)
        {
            if (ToLufs(meanSquare) <= state.Gate) return;
            state.Sum += meanSquare;
            state.Count++;
        }

        /// <summary>
        /// The highest the waveform really reaches between samples, in dBFS, found by 4x oversampling. A sample peak of 0 dBFS
        /// can reconstruct well above that, and that overshoot is what clips in a converter.
        /// </summary>
        public static float TruePeak(ReadOnlySpan<float> samples)
        {
            const int Taps = 12;
            const int Phases = 4;
            float highest = 0f;

            for (int i = 0; i < samples.Length; i++)
            {
                highest = MathF.Max(highest, MathF.Abs(samples[i]));
                for (int phase = 1; phase < Phases; phase++)
                {
                    float position = phase / (float)Phases;
                    float sum = 0f;
                    for (int tap = -Taps + 1; tap <= Taps; tap++)
                    {
                        int index = i + tap;
                        if ((uint)index >= (uint)samples.Length) continue;
                        float x = tap - position;
                        float window = 0.5f + 0.5f * MathF.Cos(MathF.PI * x / Taps);
                        sum += samples[index] * Sinc(x) * window;
                    }
                    highest = MathF.Max(highest, MathF.Abs(sum));
                }
            }
            return AudioFormat.GainToDecibels(highest);
        }

        private static float Sinc(float x) => MathF.Abs(x) < 1e-5f ? 1f : MathF.Sin(MathF.PI * x) / (MathF.PI * x);
    }
}
