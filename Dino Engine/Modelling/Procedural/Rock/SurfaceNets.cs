using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Rock
{
    /// <summary>
    /// Turns a grid of signed distances into triangles, negative inside (Gibson's surface nets, as Mikola Lysenko laid them out): a point in every cell the surface passes through,
    /// and a quad across every grid edge it crosses. Unlike marching cubes this gives even triangles and no slivers, which is what <see cref="Decimator"/> wants to start from.
    /// </summary>
    internal static class SurfaceNets
    {
        private static readonly (int a, int b)[] Edges =
        {
            (0, 1), (2, 3), (4, 5), (6, 7),
            (0, 2), (1, 3), (4, 6), (5, 7),
            (0, 4), (1, 5), (2, 6), (3, 7),
        };

        public static void Extract(float[] field, int nx, int ny, int nz, Vector3 origin, float cell, List<Vector3> points, List<int> triangles)
        {
            int cx = nx - 1, cy = ny - 1, cz = nz - 1;
            int[] vertexOf = new int[cx * cy * cz];
            Array.Fill(vertexOf, -1);
            Span<float> corner = stackalloc float[8];

            for (int z = 0; z < cz; z++)
            {
                for (int y = 0; y < cy; y++)
                {
                    for (int x = 0; x < cx; x++)
                    {
                        int mask = 0;
                        for (int i = 0; i < 8; i++)
                        {
                            corner[i] = field[(x + (i & 1)) + nx * ((y + ((i >> 1) & 1)) + ny * (z + ((i >> 2) & 1)))];
                            if (corner[i] < 0f) mask |= 1 << i;
                        }
                        if (mask == 0 || mask == 255) continue;

                        Vector3 sum = Vector3.Zero;
                        int crossings = 0;
                        foreach ((int a, int b) in Edges)
                        {
                            if (corner[a] < 0f == corner[b] < 0f) continue;
                            float t = corner[a] / (corner[a] - corner[b]);
                            sum += Vector3.Lerp(Corner(a), Corner(b), t);
                            crossings++;
                        }

                        vertexOf[x + cx * (y + cy * z)] = points.Count;
                        points.Add(origin + (new Vector3(x, y, z) + sum / crossings) * cell);
                    }
                }
            }

            for (int z = 1; z < nz - 1; z++)
            {
                for (int y = 1; y < ny - 1; y++)
                {
                    for (int x = 1; x < nx - 1; x++)
                    {
                        float here = field[x + nx * (y + ny * z)];
                        bool inside = here < 0f;
                        if (inside != field[x + 1 + nx * (y + ny * z)] < 0f) Quad(Cell(x, y - 1, z - 1), Cell(x, y, z - 1), Cell(x, y, z), Cell(x, y - 1, z), inside);
                        if (inside != field[x + nx * (y + 1 + ny * z)] < 0f) Quad(Cell(x - 1, y, z - 1), Cell(x - 1, y, z), Cell(x, y, z), Cell(x, y, z - 1), inside);
                        if (inside != field[x + nx * (y + ny * (z + 1))] < 0f) Quad(Cell(x - 1, y - 1, z), Cell(x, y - 1, z), Cell(x, y, z), Cell(x - 1, y, z), inside);
                    }
                }
            }

            int Cell(int x, int y, int z) => vertexOf[x + cx * (y + cy * z)];

            void Quad(int a, int b, int c, int d, bool outwards)
            {
                if (a < 0 || b < 0 || c < 0 || d < 0) return;
                if (!outwards) (b, d) = (d, b);
                // Across the shorter diagonal, which keeps a crease from being cut the wrong way.
                if ((points[a] - points[c]).LengthSquared <= (points[b] - points[d]).LengthSquared)
                {
                    Add(a, b, c);
                    Add(a, c, d);
                }
                else
                {
                    Add(a, b, d);
                    Add(b, c, d);
                }
            }

            void Add(int a, int b, int c)
            {
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }
        }

        private static Vector3 Corner(int i) => new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1);
    }
}
