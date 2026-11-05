namespace Dino_Engine.Sound.Dsp
{
    /// <summary>A plain radix-2 FFT, for convolving impulse responses and for looking at sounds. Nothing on the audio thread uses it.</summary>
    public sealed class Fft
    {
        private readonly int _size;
        private readonly int[] _reversed;
        private readonly float[] _cos;
        private readonly float[] _sin;

        public int Size => _size;

        public Fft(int size)
        {
            if (size < 2 || (size & (size - 1)) != 0) throw new ArgumentException("An FFT's size must be a power of two.", nameof(size));
            _size = size;
            _reversed = new int[size];
            _cos = new float[size / 2];
            _sin = new float[size / 2];

            int bits = System.Numerics.BitOperations.Log2((uint)size);
            for (int i = 0; i < size; i++)
            {
                int reversed = 0;
                for (int b = 0; b < bits; b++)
                {
                    if ((i & (1 << b)) != 0) reversed |= 1 << (bits - 1 - b);
                }
                _reversed[i] = reversed;
            }
            for (int i = 0; i < size / 2; i++)
            {
                double angle = -2.0 * Math.PI * i / size;
                _cos[i] = (float)Math.Cos(angle);
                _sin[i] = (float)Math.Sin(angle);
            }
        }

        /// <summary>In place. Inverse transforms are scaled by 1 / size, so a round trip gives back what went in.</summary>
        public void Transform(float[] real, float[] imaginary, bool inverse = false)
        {
            for (int i = 0; i < _size; i++)
            {
                int j = _reversed[i];
                if (j <= i) continue;
                (real[i], real[j]) = (real[j], real[i]);
                (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]);
            }

            float sign = inverse ? -1f : 1f;
            for (int span = 2; span <= _size; span *= 2)
            {
                int half = span / 2;
                int stride = _size / span;
                for (int start = 0; start < _size; start += span)
                {
                    for (int k = 0; k < half; k++)
                    {
                        float wr = _cos[k * stride];
                        float wi = _sin[k * stride] * sign;
                        int a = start + k;
                        int b = a + half;
                        float tr = real[b] * wr - imaginary[b] * wi;
                        float ti = real[b] * wi + imaginary[b] * wr;
                        real[b] = real[a] - tr;
                        imaginary[b] = imaginary[a] - ti;
                        real[a] += tr;
                        imaginary[a] += ti;
                    }
                }
            }

            if (!inverse) return;
            float scale = 1f / _size;
            for (int i = 0; i < _size; i++)
            {
                real[i] *= scale;
                imaginary[i] *= scale;
            }
        }

        public static int NextPowerOfTwo(int value)
        {
            int size = 1;
            while (size < value) size *= 2;
            return size;
        }
    }
}
