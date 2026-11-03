namespace Dino_Engine.World
{
    /// <summary>
    /// Seeded random numbers for world generation (SplitMix64). Unlike <see cref="System.Random"/> and
    /// <see cref="Util.MyMath.rand"/> the sequence is fixed for a seed, so a seed always gives the same world.
    /// Every stage takes its own <see cref="Fork(string)"/>: drawing a few more numbers in one stage never changes another.
    /// </summary>
    public sealed class WorldRandom
    {
        private ulong _state;

        public WorldRandom(ulong seed)
        {
            _state = seed;
        }

        public WorldRandom(int seed) : this((ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL)
        {
        }

        /// <summary>An independent stream named <paramref name="stream"/>. Forking does not advance this generator.</summary>
        public WorldRandom Fork(string stream)
        {
            return new WorldRandom(Mix(_state ^ Hash(stream)));
        }

        /// <summary>An independent stream for the <paramref name="index"/>th thing of a kind.</summary>
        public WorldRandom Fork(int index)
        {
            return new WorldRandom(Mix(_state + (ulong)(uint)index * 0xD1342543DE82EF95UL));
        }

        public ulong NextULong()
        {
            _state += 0x9E3779B97F4A7C15UL;
            return Mix(_state);
        }

        /// <summary>0 (inclusive) to 1 (exclusive).</summary>
        public float Float()
        {
            return (NextULong() >> 40) * (1f / 16777216f);
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * Float();
        }

        public float Range(FloatRange range)
        {
            return Range(range.Min, range.Max);
        }

        /// <summary>From <paramref name="min"/> up to and including <paramref name="max"/>.</summary>
        public int Int(int min, int max)
        {
            return min + (int)(NextULong() % (ulong)(max - min + 1));
        }

        public int Int(IntRange range)
        {
            return Int(range.Min, range.Max);
        }

        /// <summary>From 0 up to but not including <paramref name="exclusiveMax"/>.</summary>
        public int Int(int exclusiveMax)
        {
            return (int)(NextULong() % (ulong)exclusiveMax);
        }

        public bool Chance(float probability)
        {
            return Float() < probability;
        }

        /// <summary>Roughly normal, mean 0, standard deviation 1.</summary>
        public float Gaussian()
        {
            return (Float() + Float() + Float() + Float() - 2f) * 1.7320508f;
        }

        public T Pick<T>(IReadOnlyList<T> items)
        {
            return items[Int(items.Count)];
        }

        /// <summary>A random index, each with probability proportional to its weight. -1 if every weight is zero.</summary>
        public int PickWeighted(IReadOnlyList<float> weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += MathF.Max(weights[i], 0f);
            if (total <= 0f) return -1;

            float pick = Float() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                float weight = MathF.Max(weights[i], 0f);
                if (weight <= 0f) continue;
                pick -= weight;
                if (pick < 0f) return i;
            }
            for (int i = weights.Count - 1; i >= 0; i--)
            {
                if (weights[i] > 0f) return i;
            }
            return -1;
        }

        public void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Int(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        public long NoiseSeed()
        {
            return (long)NextULong();
        }

        private static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        // FNV-1a. string.GetHashCode is randomized per process, which would change every world between runs.
        private static ulong Hash(string text)
        {
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }

    public readonly struct FloatRange
    {
        public readonly float Min;
        public readonly float Max;

        public FloatRange(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public float Lerp(float t) => Min + (Max - Min) * t;
    }

    public readonly struct IntRange
    {
        public readonly int Min;
        public readonly int Max;

        public IntRange(int min, int max)
        {
            Min = min;
            Max = max;
        }
    }
}
