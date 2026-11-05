namespace Dino_Engine.Sound.Dsp
{
    /// <summary>
    /// The dice of the sound system. Worlds have their own, and must: a sound drawing from a world's stream would change every
    /// world after it. Each take is made from a seed of its own, so anything heard can be made again exactly (xoshiro128**).
    /// </summary>
    public sealed class SoundRandom
    {
        private uint _a, _b, _c, _d;
        private float _spare;
        private bool _hasSpare;

        public SoundRandom(ulong seed)
        {
            // SplitMix64 spreads even a seed of 1 over all four words.
            _a = (uint)Mix(ref seed);
            _b = (uint)Mix(ref seed);
            _c = (uint)Mix(ref seed);
            _d = (uint)Mix(ref seed);
        }

        private static ulong Mix(ref ulong state)
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public uint Next()
        {
            uint result = uint.RotateLeft(_b * 5u, 7) * 9u;
            uint t = _b << 9;
            _c ^= _a;
            _d ^= _b;
            _b ^= _c;
            _a ^= _d;
            _c ^= t;
            _d = uint.RotateLeft(_d, 11);
            return result;
        }

        /// <summary>0 up to but not including 1.</summary>
        public float Float() => (Next() >> 8) * (1f / 16777216f);

        /// <summary>-1 up to but not including 1: white noise, a sample at a time.</summary>
        public float Bipolar() => (int)Next() * (1f / 2147483648f);

        public float Range(float min, float max) => min + (max - min) * Float();

        /// <summary>Spread evenly over the octaves between the two, which is how pitch and time are heard.</summary>
        public float LogRange(float min, float max) => min * MathF.Pow(max / min, Float());

        public int Int(int exclusiveMax) => (int)(((ulong)Next() * (ulong)exclusiveMax) >> 32);

        public bool Chance(float probability) => Float() < probability;

        public T Pick<T>(IReadOnlyList<T> items) => items[Int(items.Count)];

        /// <summary>Most near zero, tailing off both ways, with a standard deviation of one.</summary>
        public float Gaussian()
        {
            if (_hasSpare)
            {
                _hasSpare = false;
                return _spare;
            }

            float u = MathF.Max(Float(), 1e-7f);
            float radius = MathF.Sqrt(-2f * MathF.Log(u));
            float angle = MathF.Tau * Float();
            _spare = radius * MathF.Sin(angle);
            _hasSpare = true;
            return radius * MathF.Cos(angle);
        }

        /// <summary>The wait until the next of a run of independent events with this mean spacing: the Poisson process behind rain, grit and crackle.</summary>
        public float Exponential(float mean) => -mean * MathF.Log(MathF.Max(1f - Float(), 1e-7f));

        /// <summary>A stream of its own, so that drawing more from one layer of a sound can't change the next.</summary>
        public SoundRandom Fork() => new SoundRandom(((ulong)Next() << 32) | Next());
    }
}
