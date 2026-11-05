using Dino_Engine.Sound.Dsp;
using System.Buffers;
using System.Collections.Concurrent;

namespace Dino_Engine.Sound.Synthesis
{
    /// <summary>A finished piece of sound, ready to be played as often as it's wanted. Never changed once made, so any thread may read it.</summary>
    public sealed class SoundBuffer
    {
        public readonly float[] Left;
        public readonly float[]? Right;     // Null for mono.
        public readonly int Length;

        public SoundBuffer(float[] left, float[]? right, int length)
        {
            Left = left;
            Right = right;
            Length = length;
        }

        public bool IsStereo => Right != null;
        public float Seconds => Length / (float)AudioFormat.SampleRate;
    }

    /// <summary>
    /// One performance of a sound, as in a recording session: a recipe is run on one of these to make one take of it, and no two
    /// takes are alike because each has its own dice. A recipe builds layers from the toolkit (see <see cref="Signal"/> and the
    /// models beside it) and mixes them in. Working memory is rented, so a session of fresh footsteps leaves nothing for the
    /// garbage collector to stop the game for.
    /// </summary>
    public sealed class Take : IDisposable
    {
        private const float StartingSeconds = 3f;

        // A pool of its own, as the shared one keeps a stack per core: on a sixteen thread machine that was over a gigabyte held on to for ever.
        private static readonly ArrayPool<float> Memory = ArrayPool<float>.Create(1 << 22, 3);

        private readonly List<float[]> _rented = new();
        private readonly bool _stereo;
        private float[] _left;
        private float[]? _right;
        private int _length;

        /// <summary>The dice of this take.</summary>
        public readonly SoundRandom Random;

        /// <summary>The value it was asked for, when a definition is made in several strengths: 0 is the softest and 1 the hardest.</summary>
        public readonly float Strength;

        public Take(ulong seed, bool stereo, float strength = 0.5f)
        {
            Random = new SoundRandom(seed);
            Strength = strength;
            _stereo = stereo;
            int capacity = (int)(StartingSeconds * AudioFormat.SampleRate);
            _left = RentCleared(capacity);
            if (stereo) _right = RentCleared(capacity);
        }

        public bool IsStereo => _stereo;

        internal float[] Rent(int length)
        {
            float[] array = Memory.Rent(length);
            _rented.Add(array);
            return array;
        }

        // All of it, not just what was asked for: the pool's arrays are longer than that, and hold whatever was last in them.
        private float[] RentCleared(int length)
        {
            float[] array = Rent(length);
            Array.Clear(array);
            return array;
        }

        internal void Return(float[] array)
        {
            _rented.Remove(array);
            Memory.Return(array);
        }

        // --- Starting points ---

        /// <summary>Silence, to build on.</summary>
        public Signal Track(float seconds)
        {
            int length = Math.Max((int)(seconds * AudioFormat.SampleRate), 1);
            float[] samples = Rent(length);
            Array.Clear(samples, 0, length);
            return new Signal(this, samples, length);
        }

        public Signal White(float seconds)
        {
            Signal signal = Track(seconds);
            float[] samples = signal.Samples;
            for (int i = 0; i < signal.Length; i++) samples[i] = Random.Bipolar();
            return signal;
        }

        public Signal Pink(float seconds)
        {
            Signal signal = Track(seconds);
            var pink = new PinkNoise();
            float[] samples = signal.Samples;
            for (int i = 0; i < signal.Length; i++) samples[i] = pink.Next(Random.Bipolar());
            return signal;
        }

        public Signal Brown(float seconds)
        {
            Signal signal = Track(seconds);
            var brown = new BrownNoise();
            float[] samples = signal.Samples;
            for (int i = 0; i < signal.Length; i++) samples[i] = brown.Next(Random.Bipolar());
            return signal;
        }

        /// <summary>A pure tone following a pitch curve.</summary>
        public Signal Sine(float seconds, Curve frequency, float phase = 0f)
        {
            Signal signal = Track(seconds);
            float[] samples = signal.Samples;
            double angle = phase;
            const double Step = 2.0 * Math.PI / AudioFormat.SampleRate;
            for (int i = 0; i < signal.Length; i++)
            {
                samples[i] = (float)Math.Sin(angle);
                angle += Step * frequency.At(i * (1f / AudioFormat.SampleRate));
            }
            return signal;
        }

        /// <summary>Slow, smooth, random movement between 0 and 1: for whatever shouldn't sit still. <paramref name="rate"/> is about how many changes a second.</summary>
        public Curve Drift(float seconds, float rate)
        {
            int points = Math.Max((int)(seconds * rate) + 3, 4);
            float[] values = new float[points];
            for (int i = 0; i < points; i++) values[i] = Random.Float();

            return new Curve(t =>
            {
                float position = Math.Clamp(t * rate, 0f, points - 2.001f);
                int index = (int)position;
                float x = position - index;
                x = x * x * (3f - 2f * x);
                return values[index] + (values[index + 1] - values[index]) * x;
            });
        }

        // --- Mixing ---

        /// <summary>Lays a layer into the take, <paramref name="at"/> seconds in. <paramref name="pan"/> runs from -1 (left) to 1 (right), for a take that is stereo.</summary>
        public void Mix(Signal layer, float at = 0f, float decibels = 0f, float pan = 0f)
        {
            float gain = AudioFormat.DecibelsToGain(decibels);
            int start = (int)(at * AudioFormat.SampleRate);
            int end = start + layer.Length;
            Reserve(end);

            if (_right == null)
            {
                for (int i = 0; i < layer.Length; i++) _left[start + i] += layer.Samples[i] * gain;
            }
            else
            {
                float angle = (Math.Clamp(pan, -1f, 1f) + 1f) * (MathF.PI * 0.25f);
                float left = MathF.Cos(angle) * MathF.Sqrt(2f) * gain;
                float right = MathF.Sin(angle) * MathF.Sqrt(2f) * gain;
                for (int i = 0; i < layer.Length; i++)
                {
                    float value = layer.Samples[i];
                    _left[start + i] += value * left;
                    _right[start + i] += value * right;
                }
            }
            _length = Math.Max(_length, end);
        }

        /// <summary>Lays in a layer with its own left and right, as a wide sound is. Into a mono take, they are simply added.</summary>
        public void MixStereo(Signal left, Signal right, float at = 0f, float decibels = 0f)
        {
            float gain = AudioFormat.DecibelsToGain(decibels);
            int start = (int)(at * AudioFormat.SampleRate);
            int length = Math.Min(left.Length, right.Length);
            int end = start + length;
            Reserve(end);

            if (_right == null)
            {
                gain *= 0.7071f;
                for (int i = 0; i < length; i++) _left[start + i] += (left.Samples[i] + right.Samples[i]) * gain;
            }
            else
            {
                for (int i = 0; i < length; i++)
                {
                    _left[start + i] += left.Samples[i] * gain;
                    _right[start + i] += right.Samples[i] * gain;
                }
            }
            _length = Math.Max(_length, end);
        }

        private void Reserve(int length)
        {
            // Rented arrays can come back longer than asked for, so the two sides needn't be the same length.
            if (length > _left.Length) _left = Grown(_left, Math.Max(length, _left.Length * 2));
            if (_right != null && length > _right.Length) _right = Grown(_right, Math.Max(length, _right.Length * 2));
        }

        private float[] Grown(float[] old, int capacity)
        {
            float[] grown = RentCleared(capacity);
            Array.Copy(old, grown, Math.Min(_length, old.Length));
            Return(old);
            return grown;
        }

        // --- Finishing ---

        /// <summary>
        /// Everything a sound is put through on the way out of the studio, which a recipe can then forget about: subsonics
        /// cleared, the tail cut where it goes under the noise floor, loudness levelled, and peaks caught.
        /// </summary>
        public SoundBuffer Finish(in Mastering mastering)
        {
            int length = _length;
            if (length == 0) return new SoundBuffer(new float[1], _stereo ? new float[1] : null, 1);

            if (mastering.Stage > -90f) length = PlaceOnStage(length, mastering.Stage);

            Clean(_left, length, mastering.Highpass);
            if (_right != null) Clean(_right, length, mastering.Highpass);

            length = TrimmedLength(length, mastering.TailFloor);

            // A true take ends at nothing and begins from nothing: no clicks at either end.
            int fadeOut = Math.Min((int)(mastering.FadeOut * AudioFormat.SampleRate), length);
            int fadeIn = Math.Min(8, length);
            Ends(_left, length, fadeIn, fadeOut);
            if (_right != null) Ends(_right, length, fadeIn, fadeOut);

            ReadOnlySpan<float> left = _left.AsSpan(0, length);
            ReadOnlySpan<float> right = _right != null ? _right.AsSpan(0, length) : default;
            if (mastering.Target is float target)
            {
                float loudness = mastering.Sustained ? Dsp.Loudness.Integrated(left, right) : Dsp.Loudness.Peak(left, right);
                float gain = AudioFormat.DecibelsToGain(Math.Clamp(target - loudness, -40f, 40f));
                Scale(_left, length, gain);
                if (_right != null) Scale(_right, length, gain);
            }

            // The limiter's look-ahead has to run off the end, so it gets a little silence to read.
            var limiter = new Limiter(mastering.Ceiling, 0.0008f, 0.05f);
            int padded = length + limiter.Latency;
            Reserve(padded);
            Array.Clear(_left, length, padded - length);
            float[] right2 = _right ?? Rent(padded);
            if (_right == null) Array.Copy(_left, right2, padded);
            else Array.Clear(_right, length, padded - length);
            limiter.Process(_left.AsSpan(0, padded), right2.AsSpan(0, padded));

            float[] finishedLeft = new float[length];
            Array.Copy(_left, limiter.Latency, finishedLeft, 0, length);
            float[]? finishedRight = null;
            if (_right != null)
            {
                finishedRight = new float[length];
                Array.Copy(_right, limiter.Latency, finishedRight, 0, length);
            }
            return new SoundBuffer(finishedLeft, finishedRight, length);
        }

        // A sound with no room around it sits on digital silence, which no microphone ever heard. This is a small space, convolved in
        // quietly: a few early reflections and a short darkening tail. Mostly it isn't heard as a space at all, only as the
        // sound being real. Where the sound really is, the mixer's reverb says.
        private int PlaceOnStage(int length, float decibels)
        {
            float[] responses = Stage.Response(Random.Int(Stage.Variants), out int responseLength);
            int total = length + responseLength;
            Reserve(total);

            float wet = AudioFormat.DecibelsToGain(decibels);
            int size = Fft.NextPowerOfTwo(total);
            Fft fft = Stage.Transform(size);
            float[] real = Rent(size), imaginary = Rent(size), responseReal = Rent(size), responseImaginary = Rent(size);

            int channels = _stereo ? 2 : 1;
            for (int channel = 0; channel < channels; channel++)
            {
                float[] samples = channel == 0 ? _left : _right!;

                // The same response in both ears would sound like a mono one in the middle, so each has its own.
                Array.Clear(real, 0, size);
                Array.Clear(imaginary, 0, size);
                Array.Clear(responseReal, 0, size);
                Array.Clear(responseImaginary, 0, size);
                Array.Copy(samples, real, length);
                Array.Copy(responses, channel * responseLength, responseReal, 0, responseLength);

                fft.Transform(real, imaginary);
                fft.Transform(responseReal, responseImaginary);
                for (int i = 0; i < size; i++)
                {
                    float product = real[i] * responseReal[i] - imaginary[i] * responseImaginary[i];
                    imaginary[i] = real[i] * responseImaginary[i] + imaginary[i] * responseReal[i];
                    real[i] = product;
                }
                fft.Transform(real, imaginary, inverse: true);
                for (int i = 0; i < total; i++) samples[i] += real[i] * wet;
            }

            Return(real);
            Return(imaginary);
            Return(responseReal);
            Return(responseImaginary);
            _length = total;
            return total;
        }

        private static void Clean(float[] samples, int length, float highpass)
        {
            Biquad first = Biquad.Highpass(highpass, 0.54f);
            Biquad second = Biquad.Highpass(highpass, 1.31f);
            for (int i = 0; i < length; i++) samples[i] = second.Process(first.Process(samples[i]));
        }

        private int TrimmedLength(int length, float floorDecibels)
        {
            float peak = 0f;
            for (int i = 0; i < length; i++)
            {
                peak = MathF.Max(peak, MathF.Abs(_left[i]));
                if (_right != null) peak = MathF.Max(peak, MathF.Abs(_right[i]));
            }
            float floor = peak * AudioFormat.DecibelsToGain(floorDecibels);
            for (int i = length - 1; i > 0; i--)
            {
                if (MathF.Abs(_left[i]) > floor || (_right != null && MathF.Abs(_right[i]) > floor)) return Math.Min(i + 1, length);
            }
            return length;
        }

        private static void Ends(float[] samples, int length, int fadeIn, int fadeOut)
        {
            for (int i = 0; i < fadeIn; i++) samples[i] *= i / (float)fadeIn;
            for (int i = 0; i < fadeOut; i++)
            {
                float x = (i + 0.5f) / fadeOut;
                samples[length - 1 - i] *= x * x * (3f - 2f * x);
            }
        }

        private static void Scale(float[] samples, int length, float gain)
        {
            for (int i = 0; i < length; i++) samples[i] *= gain;
        }

        public void Dispose()
        {
            foreach (float[] array in _rented) Memory.Return(array);
            _rented.Clear();
        }
    }

    /// <summary>How a take is finished. See <see cref="Take.Finish"/>.</summary>
    public struct Mastering
    {
        public float? Target;       // LUFS. Left null, the recipe's own level stands.
        public bool Sustained;      // Measured as a whole, as for a long sound, not by its loudest moment.
        public float Ceiling;       // dBFS no peak may pass.
        public float Highpass;      // Hz. Nobody can hear below this, but it takes headroom all the same.
        public float TailFloor;     // dB below the peak at which what's left is cut off.
        public float FadeOut;       // Seconds over which the very end is faded.
        public float Stage;         // Decibels of the small space it's placed in, relative to itself. Below -90 there is none.

        public static Mastering Default => new Mastering
        {
            Target = -16f,
            Ceiling = -1.5f,
            Highpass = 28f,
            TailFloor = -66f,
            FadeOut = 0.012f,
            Stage = -13f,
        };
    }

    /// <summary>The small space every take is placed in. See <see cref="Take.Finish"/>.</summary>
    internal static class Stage
    {
        public const int Variants = 4;
        private const float Seconds = 0.26f;
        private static readonly ConcurrentDictionary<int, Fft> Transforms = new();
        private static readonly Lazy<float[][]> Responses = new(Build);

        public static Fft Transform(int size) => Transforms.GetOrAdd(size, s => new Fft(s));

        /// <summary>A left response followed by a right one. A mono take reads only the first.</summary>
        public static float[] Response(int variant, out int length)
        {
            length = (int)(Seconds * AudioFormat.SampleRate);
            return Responses.Value[variant];
        }

        private static float[][] Build()
        {
            var responses = new float[Variants][];
            int length = (int)(Seconds * AudioFormat.SampleRate);
            for (int v = 0; v < Variants; v++)
            {
                var random = new SoundRandom(0x57A6E + (ulong)v);
                var both = new float[length * 2];
                for (int channel = 0; channel < 2; channel++)
                {
                    int offset = channel * length;

                    // Early reflections: floor, walls and whatever is nearby, a few milliseconds apart.
                    for (int t = 0; t < 9; t++)
                    {
                        int at = (int)(random.Range(0.0025f, 0.034f) * AudioFormat.SampleRate);
                        both[offset + at] += random.Bipolar() * 1.6f;
                    }

                    // Then a dense tail, losing its top as it goes, as air and soft things take that first.
                    var darkening = new OnePole();
                    float level = 0.32f;
                    float fall = MathF.Exp(-1f / (0.045f * AudioFormat.SampleRate));
                    int start = (int)(0.004f * AudioFormat.SampleRate);
                    for (int i = start; i < length; i++)
                    {
                        float t = (i - start) / (float)AudioFormat.SampleRate;
                        if (i % 32 == 0) darkening.SetCutoff(1400f + 16000f * MathF.Exp(-t * 32f));
                        both[offset + i] += darkening.Lowpass(random.Bipolar()) * level;
                        level *= fall;
                    }

                    // Scaled to an energy of one, so the level asked for is the level got.
                    double energy = 0.0;
                    for (int i = 0; i < length; i++) energy += (double)both[offset + i] * both[offset + i];
                    float scale = 1f / MathF.Sqrt((float)Math.Max(energy, 1e-9));
                    for (int i = 0; i < length; i++) both[offset + i] *= scale;
                }
                responses[v] = both;
            }
            return responses;
        }
    }
}
