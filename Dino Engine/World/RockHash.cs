namespace Dino_Engine.World
{
    /// <summary>
    /// The hashes and the value noise rock is drawn with, the same as Rock.glsl's <c>rockMix</c>, <c>rockHash</c>, <c>rockUnit</c> and <c>rockNoise</c>: the skin of the rock
    /// (<see cref="Modelling.Procedural.Rock.RockSkin"/>) breaks into its blocks by them. Headless, and safe from any thread.
    /// </summary>
    public static class RockHash
    {
        public static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        public static uint Hash(int a, int b, int c)
        {
            unchecked
            {
                return Mix((uint)a * 0x8DA6B343u + Mix((uint)b * 0xD8163841u ^ (uint)c * 0xCB1AB31Fu ^ 0x165667B1u));
            }
        }

        private static uint Corner(int x, int y)
        {
            unchecked
            {
                return Mix((uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ 0x27D4EB2Fu);
            }
        }

        public static float Unit(uint h) => (h & 0xFFFFu) * (1f / 65535f);

        public static float Noise(float x, float y)
        {
            float ix = MathF.Floor(x), iy = MathF.Floor(y);
            float fx = x - ix, fy = y - iy;
            float ux = fx * fx * (3f - 2f * fx);
            float uy = fy * fy * (3f - 2f * fy);
            int cx = (int)ix, cy = (int)iy;
            float a = Unit(Corner(cx, cy));
            float b = Unit(Corner(cx + 1, cy));
            float d = Unit(Corner(cx, cy + 1));
            float e = Unit(Corner(cx + 1, cy + 1));
            return a + (b - a) * ux + (d - a) * uy + (a - b - d + e) * ux * uy;
        }
    }
}
