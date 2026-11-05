using Dino_Engine.Sound.Dsp;
using System.Collections.Concurrent;

namespace Dino_Engine.Sound.Synthesis
{
    /// <summary>
    /// A length of mono sound being worked on, with everything that can be done to one. Each operation changes it in place and
    /// returns it, so a layer of a recipe reads as what's done to it, in order:
    /// <code>take.Pink(0.2f).Bandpass(Curve.Glide(4000f, 900f, 0.1f), 1.2f).Shape(Curve.Settle(1f, 0f, 0.03f)).Drive(2f)</code>
    /// This runs on worker threads, never on the audio thread, so it is free to be thorough.
    /// </summary>
    public sealed class Signal
    {
        private const int ControlStep = 16;     // Samples between filter coefficient updates. Far quicker than anything that sweeps.
        private const float SampleTime = 1f / AudioFormat.SampleRate;

        private static readonly ConcurrentDictionary<int, Fft> Transforms = new();

        public readonly float[] Samples;
        public readonly int Length;
        internal readonly Take Take;

        internal Signal(Take take, float[] samples, int length)
        {
            Take = take;
            Samples = samples;
            Length = length;
        }

        public float Seconds => Length * SampleTime;

        public Span<float> Span => Samples.AsSpan(0, Length);

        // --- Level ---

        public Signal Scale(float gain)
        {
            for (int i = 0; i < Length; i++) Samples[i] *= gain;
            return this;
        }

        public Signal Gain(float decibels) => Scale(AudioFormat.DecibelsToGain(decibels));

        /// <summary>Multiplies by a curve: the amplitude of the sound over time.</summary>
        public Signal Shape(Curve envelope)
        {
            for (int i = 0; i < Length; i++) Samples[i] *= envelope.At(i * SampleTime);
            return this;
        }

        /// <summary>The usual percussive envelope: up in <paramref name="attack"/> seconds, then an exponential fall, losing 63% every <paramref name="decay"/> seconds.</summary>
        public Signal Percussive(float attack, float decay)
        {
            float step = MathF.Exp(-SampleTime / decay);
            float level = 1f;
            int rise = (int)(attack * AudioFormat.SampleRate);
            for (int i = 0; i < Length; i++)
            {
                float gain = level;
                if (i < rise)
                {
                    float x = (i + 0.5f) / rise;
                    gain = x * x * (3f - 2f * x);
                }
                else level *= step;
                Samples[i] *= gain;
            }
            return this;
        }

        /// <summary>Fades in and out, so a layer can't start or end with a step.</summary>
        public Signal Fade(float fadeIn, float fadeOut)
        {
            int rise = Math.Min((int)(fadeIn * AudioFormat.SampleRate), Length);
            int fall = Math.Min((int)(fadeOut * AudioFormat.SampleRate), Length);
            for (int i = 0; i < rise; i++)
            {
                float x = (i + 0.5f) / rise;
                Samples[i] *= x * x * (3f - 2f * x);
            }
            for (int i = 0; i < fall; i++)
            {
                float x = (i + 0.5f) / fall;
                Samples[Length - 1 - i] *= x * x * (3f - 2f * x);
            }
            return this;
        }

        /// <summary>Scales it to this peak, in dBFS.</summary>
        public Signal Normalize(float decibels = 0f)
        {
            float peak = Peak();
            return peak > 1e-9f ? Scale(AudioFormat.DecibelsToGain(decibels) / peak) : this;
        }

        public float Peak()
        {
            float peak = 0f;
            for (int i = 0; i < Length; i++) peak = MathF.Max(peak, MathF.Abs(Samples[i]));
            return peak;
        }

        public float Rms()
        {
            double sum = 0.0;
            for (int i = 0; i < Length; i++) sum += (double)Samples[i] * Samples[i];
            return Length > 0 ? MathF.Sqrt((float)(sum / Length)) : 0f;
        }

        /// <summary>A new signal following the level of this one, smoothed over <paramref name="seconds"/>: to make something else swell as this does.</summary>
        public Signal Follower(float seconds)
        {
            Signal level = Take.Track(Seconds);
            var smoothing = new OnePole();
            smoothing.SetTime(seconds);
            for (int i = 0; i < Length; i++) level.Samples[i] = smoothing.Lowpass(MathF.Abs(Samples[i]));
            return level;
        }

        // --- Filters. A frequency can be a number or a curve; a second order filter resonates by its Q. ---

        public Signal Lowpass(Curve frequency, float q = 0.7071f) => Filter(frequency, q, FilterKind.Low);
        public Signal Highpass(Curve frequency, float q = 0.7071f) => Filter(frequency, q, FilterKind.High);

        /// <summary>Peak of unity, whatever the Q: a narrower band is quieter, as it lets less through.</summary>
        public Signal Bandpass(Curve frequency, float q = 1f) => Filter(frequency, q, FilterKind.Band);
        public Signal Notch(Curve frequency, float q = 1f) => Filter(frequency, q, FilterKind.Notch);

        /// <summary>Two filters in a row, for a steeper slope.</summary>
        public Signal LowpassSteep(Curve frequency) => Lowpass(frequency, 0.54f).Lowpass(frequency, 1.31f);
        public Signal HighpassSteep(Curve frequency) => Highpass(frequency, 0.54f).Highpass(frequency, 1.31f);

        private enum FilterKind { Low, High, Band, Notch }

        private Signal Filter(Curve frequency, float q, FilterKind kind)
        {
            var filter = new Svf();
            filter.Set(frequency.At(0f), q);
            bool moving = !frequency.IsConstant;
            for (int i = 0; i < Length; i++)
            {
                if (moving && i % ControlStep == 0) filter.Set(frequency.At(i * SampleTime), q);
                Samples[i] = kind switch
                {
                    FilterKind.Low => filter.Lowpass(Samples[i]),
                    FilterKind.High => filter.Highpass(Samples[i]),
                    FilterKind.Band => filter.BandpassNormal(Samples[i]),
                    _ => filter.Notch(Samples[i]),
                };
            }
            return this;
        }

        public Signal Bell(float frequency, float decibels, float q = 1f) => Apply(Biquad.Peak(frequency, q, decibels));
        public Signal LowShelf(float frequency, float decibels) => Apply(Biquad.LowShelf(frequency, decibels));
        public Signal HighShelf(float frequency, float decibels) => Apply(Biquad.HighShelf(frequency, decibels));

        private Signal Apply(Biquad filter)
        {
            for (int i = 0; i < Length; i++) Samples[i] = filter.Process(Samples[i]);
            return this;
        }

        /// <summary>
        /// Rings with a delay fed back into itself: a pipe, a hollow, or a metallic sheen. A sweeping frequency gives the flanging
        /// of a jet or a passing spell.
        /// </summary>
        public Signal Comb(Curve frequency, float feedback, float mix = 1f)
        {
            int size = 1 << 14;
            float[] line = new float[size];
            int write = 0;
            for (int i = 0; i < Length; i++)
            {
                float delay = Math.Clamp(AudioFormat.SampleRate / MathF.Max(frequency.At(i * SampleTime), 8f), 1f, size - 2f);
                float position = write - delay;
                int whole = (int)MathF.Floor(position);
                float fraction = position - whole;
                float a = line[whole & (size - 1)];
                float b = line[(whole + 1) & (size - 1)];
                float delayed = a + (b - a) * fraction;
                float input = Samples[i];
                line[write] = input + delayed * feedback;
                write = (write + 1) & (size - 1);
                Samples[i] = input * (1f - mix) + delayed * mix;
            }
            return this;
        }

        // --- Shape of the wave. These make harmonics, so they run at four times the rate and alias far less. ---

        /// <summary>Soft saturation: thickens, and pulls peaks in so the sound holds together. 1 barely touches it, 8 is plainly distorted.</summary>
        public Signal Drive(float amount)
        {
            float normal = 1f / MathF.Tanh(amount);
            return Oversampled(x => MathF.Tanh(x * amount) * normal);
        }

        /// <summary>Folds what goes past the limit back on itself, for a harsh, buzzing edge.</summary>
        public Signal Fold(float amount)
        {
            return Oversampled(x => MathF.Sin(x * amount * MathF.PI * 0.5f));
        }

        /// <summary>Hard clipping, with its bite. Set by how far over the limit the input goes.</summary>
        public Signal Clip(float amount) => Oversampled(x => Math.Clamp(x * amount, -1f, 1f));

        private Signal Oversampled(Func<float, float> shape)
        {
            const int Factor = 4;
            const int Half = 24;
            int taps = Half * 2 + 1;
            Span<float> kernel = stackalloc float[taps];
            float sum = 0f;
            for (int i = 0; i < taps; i++)
            {
                float x = i - Half;
                float sinc = x == 0f ? 1f : MathF.Sin(MathF.PI * x / Factor) / (MathF.PI * x / Factor);
                float window = 0.42f + 0.5f * MathF.Cos(MathF.PI * x / Half) + 0.08f * MathF.Cos(2f * MathF.PI * x / Half);
                kernel[i] = sinc * window;
                sum += kernel[i];
            }
            for (int i = 0; i < taps; i++) kernel[i] *= Factor / sum;

            int upLength = Length * Factor;
            float[] up = Take.Rent(upLength + taps);
            Array.Clear(up, 0, upLength + taps);

            // Up: zeros between samples, smoothed by the filter. Only one sample in four is non-zero, so most of it can be skipped.
            for (int i = 0; i < upLength; i++)
            {
                float value = 0f;
                int first = (i - Half + Factor - 1) / Factor;
                if (i - Half < 0) first = 0;
                for (int s = first; s * Factor <= i + Half && s < Length; s++) value += Samples[s] * kernel[i - s * Factor + Half];
                up[i] = shape(value);
            }

            // Down: the same filter again, keeping one in four.
            for (int i = 0; i < Length; i++)
            {
                float value = 0f;
                int centre = i * Factor;
                for (int t = -Half; t <= Half; t++)
                {
                    int index = centre + t;
                    if ((uint)index < (uint)upLength) value += up[index] * kernel[t + Half];
                }
                Samples[i] = value * (1f / Factor);
            }

            Take.Return(up);
            return this;
        }

        // --- Time ---

        /// <summary>Plays it backwards: swells that lead into something.</summary>
        public Signal Reverse()
        {
            Array.Reverse(Samples, 0, Length);
            return this;
        }

        /// <summary>A copy sped up by <paramref name="ratio"/>, or following a curve of ratios, as a tape would: pitch and time together.</summary>
        public Signal Varispeed(Curve ratio)
        {
            Signal result = Take.Track(Seconds);
            double position = 0.0;
            for (int i = 0; i < result.Length; i++)
            {
                int whole = (int)position;
                if (whole >= Length - 2) break;
                float fraction = (float)(position - whole);
                float a = Samples[Math.Max(whole - 1, 0)], b = Samples[whole], c = Samples[whole + 1], d = Samples[whole + 2];

                // Catmull-Rom. Quality matters less here than in the mixer: what's bent is usually noise.
                result.Samples[i] = b + 0.5f * fraction * (c - a + fraction * (2f * a - 5f * b + 4f * c - d + fraction * (3f * (b - c) + d - a)));
                position += ratio.At(i * SampleTime);
            }
            return result;
        }

        /// <summary>Adds another signal into this one, starting <paramref name="at"/> seconds in. What runs past the end is lost.</summary>
        public Signal Add(Signal other, float at = 0f, float gain = 1f)
        {
            int start = (int)(at * AudioFormat.SampleRate);
            int count = Math.Min(other.Length, Length - start);
            for (int i = 0; i < count; i++) Samples[start + i] += other.Samples[i] * gain;
            return this;
        }

        public Signal Multiply(Signal other)
        {
            int count = Math.Min(other.Length, Length);
            for (int i = 0; i < count; i++) Samples[i] *= other.Samples[i];
            return this;
        }

        /// <summary>
        /// Puts it through an impulse response: makes it happen in that space, or in that body. Done in one go with an FFT, as
        /// there's no hurry here. The result is as long as this signal, so give it room for the tail first.
        /// </summary>
        public Signal Convolve(Signal response, float wet = 1f)
        {
            int size = Fft.NextPowerOfTwo(Length + response.Length);
            Fft fft = Transforms.GetOrAdd(size, s => new Fft(s));

            float[] ar = Take.Rent(size), ai = Take.Rent(size), br = Take.Rent(size), bi = Take.Rent(size);
            Array.Clear(ar, 0, size);
            Array.Clear(ai, 0, size);
            Array.Clear(br, 0, size);
            Array.Clear(bi, 0, size);
            Array.Copy(Samples, ar, Length);
            Array.Copy(response.Samples, br, response.Length);

            fft.Transform(ar, ai);
            fft.Transform(br, bi);
            for (int i = 0; i < size; i++)
            {
                float real = ar[i] * br[i] - ai[i] * bi[i];
                ai[i] = ar[i] * bi[i] + ai[i] * br[i];
                ar[i] = real;
            }
            fft.Transform(ar, ai, inverse: true);

            for (int i = 0; i < Length; i++) Samples[i] = Samples[i] * (1f - wet) + ar[i] * wet;

            Take.Return(ar);
            Take.Return(ai);
            Take.Return(br);
            Take.Return(bi);
            return this;
        }
    }
}
