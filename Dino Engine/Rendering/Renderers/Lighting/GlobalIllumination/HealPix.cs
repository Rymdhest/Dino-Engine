using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.Lighting.GlobalIllumination
{
    /// <summary>
    /// HEALPix (Gorski et al., 2005): the sphere cut into 12 cells of equal area, each cut into four of equal area again, and so on. The
    /// radiance cascades use it for their directions: every cascade has four times the directions of the one before, the four cells a
    /// cell is cut into are numbered one after another (the nested scheme), so a cell's children are 4n to 4n + 3, and every direction of
    /// a cascade counts exactly as much as every other. The north pole is +y. After pix2ang_nest of the HEALPix C library.
    /// </summary>
    public static class HealPix
    {
        private static readonly int[] Jrll = { 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4 };
        private static readonly int[] Jpll = { 1, 3, 5, 7, 0, 2, 4, 6, 1, 3, 5, 7 };

        public static int Count(int nside) => 12 * nside * nside;

        /// <summary>The direction to the centre of a cell, at a resolution of nside cells along each edge of a base cell.</summary>
        public static Vector3 Centre(int nside, int pixel)
        {
            int faceSize = nside * nside;
            int face = pixel / faceSize;
            int within = pixel % faceSize;
            int ix = Compress(within);
            int iy = Compress(within >> 1);

            long nl4 = 4L * nside;
            double fact2 = 4.0 / (12.0 * nside * nside);
            long jr = (long)Jrll[face] * nside - ix - iy - 1;
            long nr;
            long kshift;
            double z;
            if (jr < nside)
            {
                nr = jr;
                z = 1.0 - nr * nr * fact2;
                kshift = 0;
            }
            else if (jr > 3L * nside)
            {
                nr = nl4 - jr;
                z = nr * nr * fact2 - 1.0;
                kshift = 0;
            }
            else
            {
                double fact1 = 2.0 * nside * fact2;
                nr = nside;
                z = (2L * nside - jr) * fact1;
                kshift = (jr - nside) & 1;
            }
            long jp = ((long)Jpll[face] * nr + ix - iy + 1 + kshift) / 2;
            if (jp > nl4) jp -= nl4;
            if (jp < 1) jp += nl4;
            double phi = (jp - (kshift + 1) * 0.5) * (Math.PI * 0.5 / nr);
            double s = Math.Sqrt(Math.Max(0.0, 1.0 - z * z));
            return new Vector3((float)(s * Math.Cos(phi)), (float)z, (float)(s * Math.Sin(phi)));
        }

        // Every other bit, for the cell's place in its base cell, which the nested number interleaves.
        private static int Compress(int value)
        {
            int result = 0;
            for (int bit = 0; bit < 16; bit++) result |= ((value >> (2 * bit)) & 1) << bit;
            return result;
        }

        /// <summary>
        /// The centres of every cell of each resolution, coarsest first, one after another. Checks that the nesting holds, as everything
        /// the cascades merge rests on it: each cell's four children have to lie around it. Returns the largest angle by which one strays,
        /// in units of its parent's size.
        /// </summary>
        public static Vector4[] Table(int[] nsides, out float worstStray)
        {
            int total = 0;
            foreach (int nside in nsides) total += Count(nside);
            var table = new Vector4[total];
            int at = 0;
            worstStray = 0f;
            for (int level = 0; level < nsides.Length; level++)
            {
                int nside = nsides[level];
                for (int pixel = 0; pixel < Count(nside); pixel++) table[at + pixel] = new Vector4(Centre(nside, pixel), 0f);
                if (level > 0 && nsides[level] == nsides[level - 1] * 2)
                {
                    int parentCount = Count(nsides[level - 1]);
                    float parentSize = MathF.Sqrt(4f * MathF.PI / parentCount);
                    int parentAt = at - parentCount;
                    for (int parent = 0; parent < parentCount; parent++)
                    {
                        Vector3 p = table[parentAt + parent].Xyz;
                        for (int child = 0; child < 4; child++)
                        {
                            Vector3 c = table[at + parent * 4 + child].Xyz;
                            float angle = MathF.Acos(Math.Clamp(Vector3.Dot(p, c), -1f, 1f));
                            worstStray = MathF.Max(worstStray, angle / parentSize);
                        }
                    }
                }
                at += Count(nside);
            }
            return table;
        }
    }
}
