using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>Smooth noise for growing things. It's a pure function of its arguments, so any number of threads can read it and a plant never depends on what grew before it.</summary>
    public static class FloraNoise
    {
        public static uint Hash(uint x)
        {
            unchecked
            {
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return x;
            }
        }

        private static float Lattice(int x, int y, int z, uint seed)
        {
            unchecked
            {
                uint h = Hash((uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ (uint)z * 0xCB1AB31Fu ^ seed);
                return (h >> 8) * (1f / 16777216f);
            }
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>From 0 to 1, about 1 unit to a feature.</summary>
        public static float Value(Vector3 at, uint seed)
        {
            int x = (int)MathF.Floor(at.X), y = (int)MathF.Floor(at.Y), z = (int)MathF.Floor(at.Z);
            float fx = Smooth(at.X - x), fy = Smooth(at.Y - y), fz = Smooth(at.Z - z);

            float x00 = MathHelper.Lerp(Lattice(x, y, z, seed), Lattice(x + 1, y, z, seed), fx);
            float x10 = MathHelper.Lerp(Lattice(x, y + 1, z, seed), Lattice(x + 1, y + 1, z, seed), fx);
            float x01 = MathHelper.Lerp(Lattice(x, y, z + 1, seed), Lattice(x + 1, y, z + 1, seed), fx);
            float x11 = MathHelper.Lerp(Lattice(x, y + 1, z + 1, seed), Lattice(x + 1, y + 1, z + 1, seed), fx);
            return MathHelper.Lerp(MathHelper.Lerp(x00, x10, fy), MathHelper.Lerp(x01, x11, fy), fz);
        }

        /// <summary>From 0 to 1, with detail at half the size for each octave.</summary>
        public static float Fbm(Vector3 at, uint seed, int octaves = 3)
        {
            float sum = 0f, weight = 0.5f, total = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(at, seed + (uint)i * 101u) * weight;
                total += weight;
                weight *= 0.5f;
                at *= 2.03f;
            }
            return sum / total;
        }

        /// <summary>From -1 to 1, along a line, as smooth as a bend in a stem should be.</summary>
        public static float Wave(float at, uint seed) => Value(new Vector3(at, 0.5f, 0.5f), seed) * 2f - 1f;
    }
}
