using OpenTK.Mathematics;

namespace Dino_Engine.World.Chart
{
    /// <summary>
    /// The chart's own randomness: hashes of where something is, so that drawing a map draws on no stream of the world's and the same land always comes out the same picture.
    /// </summary>
    internal static class ChartNoise
    {
        public static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }

        public static uint Hash(int x, int y, uint salt = 0) => Hash((uint)x * 0x9E3779B1u ^ Hash((uint)y ^ salt * 0x85EBCA77u));

        /// <summary>0..1 from a cell and a salt.</summary>
        public static float Unit(int x, int y, uint salt = 0) => (Hash(x, y, salt) & 0xFFFFFFu) / 16777216f;

        /// <summary>0..1 from a point, by the pixel it's in.</summary>
        public static float Unit(Vector2 at, uint salt) => Unit((int)MathF.Floor(at.X * 4f), (int)MathF.Floor(at.Y * 4f), salt);

        /// <summary>Smooth value noise, 0..1, a cell of <paramref name="scale"/> pixels.</summary>
        public static float Value(float x, float y, float scale, uint salt)
        {
            x /= scale;
            y /= scale;
            int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Unit(ix, iy, salt), b = Unit(ix + 1, iy, salt), c = Unit(ix, iy + 1, salt), d = Unit(ix + 1, iy + 1, salt);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        /// <summary>Several octaves of <see cref="Value"/>, 0..1 and piled round a half.</summary>
        public static float Fbm(float x, float y, float scale, int octaves, uint salt)
        {
            float sum = 0f, weight = 0.5f, total = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(x, y, scale, salt + (uint)i * 101u) * weight;
                total += weight;
                weight *= 0.5f;
                scale *= 0.5f;
            }
            return sum / total;
        }
    }
}
