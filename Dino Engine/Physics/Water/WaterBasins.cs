using OpenTK.Mathematics;

namespace Dino_Engine.Physics
{
    /// <summary>A hollow in the ground that holds still water.</summary>
    public struct WaterBasin
    {
        public float Level;      // World y of the water surface.
        public Vector2 Min;      // xz bounds of the water.
        public Vector2 Max;
        public Vector2 Deepest;  // xz of the lowest ground under it.
        public float MaxDepth;   // Metres.
        public float Area;       // Square metres of water.

        public Vector2 Center => (Min + Max) * 0.5f;
        public Vector2 HalfExtents => (Max - Min) * 0.5f;
    }

    /// <summary>
    /// Finds where water would collect on the ground: every closed hollow, filled to just below the height at which
    /// it would spill over. Use it to place puddles and ponds that fit procedural terrain (see WaterBodyComponent).
    /// </summary>
    public static class WaterBasins
    {
        private const int MaxCells = 4_000_000;

        /// <summary>
        /// Every hollow in [<paramref name="min"/>, <paramref name="max"/>] holding at least <paramref name="minDepth"/> metres
        /// and <paramref name="minArea"/> square metres of water, largest first. The ground is sampled every
        /// <paramref name="cellSize"/> metres; each surface sits <paramref name="freeboard"/> metres below its spill height.
        /// Hollows that reach the edge of the area are skipped, since what lies beyond is unknown.
        /// A body covering a basin's bounds at its level shows water exactly in the hollow: within the bounds, nothing
        /// outside the hollow lies below the level (the level is lowered until that holds, or the basin is dropped).
        /// </summary>
        public static List<WaterBasin> Find(Func<Vector2, float> groundHeight, Vector2 min, Vector2 max, float cellSize,
            float minDepth, float minArea, float freeboard = 0.03f)
        {
            var basins = new List<WaterBasin>();
            if (!(cellSize > 0f) || max.X <= min.X || max.Y <= min.Y) return basins;

            int nx = (int)MathF.Ceiling((max.X - min.X) / cellSize) + 1;
            int nz = (int)MathF.Ceiling((max.Y - min.Y) / cellSize) + 1;
            if ((long)nx * nz > MaxCells) throw new ArgumentException($"Basin search over {nx}x{nz} cells is too large; use larger cells or a smaller area.");
            if (nx < 3 || nz < 3) return basins;

            int count = nx * nz;
            float[] height = new float[count];
            for (int z = 0; z < nz; z++)
            {
                for (int x = 0; x < nx; x++)
                {
                    height[z * nx + x] = groundHeight(min + new Vector2(x, z) * cellSize);
                }
            }

            float[] fill = PriorityFlood(height, nx, nz);

            // Group the flooded cells into lakes: connected cells filled to the same level.
            int[] lake = new int[count];
            Array.Fill(lake, -1);
            var cells = new List<int>();
            var stack = new Stack<int>();
            int lakeCount = 0;
            for (int start = 0; start < count; start++)
            {
                if (lake[start] >= 0 || fill[start] - height[start] <= 1e-4f) continue;

                int id = lakeCount++;
                float spill = fill[start];
                bool touchesEdge = false;
                cells.Clear();
                lake[start] = id;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    cells.Add(c);
                    int cx = c % nx;
                    int cz = c / nx;
                    if (cx <= 1 || cz <= 1 || cx >= nx - 2 || cz >= nz - 2) touchesEdge = true;
                    for (int n = 0; n < 4; n++)
                    {
                        int qx = cx + (n == 0 ? 1 : n == 1 ? -1 : 0);
                        int qz = cz + (n == 2 ? 1 : n == 3 ? -1 : 0);
                        if (qx < 0 || qz < 0 || qx >= nx || qz >= nz) continue;
                        int q = qz * nx + qx;
                        if (lake[q] >= 0 || fill[q] != spill || fill[q] - height[q] <= 1e-4f) continue;
                        lake[q] = id;
                        stack.Push(q);
                    }
                }

                if (touchesEdge) continue;
                if (TryShape(cells, lake, id, height, nx, nz, min, cellSize, spill - freeboard, minDepth, minArea, out WaterBasin basin))
                {
                    basins.Add(basin);
                }
            }

            basins.Sort((a, b) => b.Area.CompareTo(a.Area));
            return basins;
        }

        /// <summary>
        /// Priority-Flood (Barnes, Lehman and Mulla 2014): the lowest level water at each cell could drain away at,
        /// with the edge of the grid as the outlet. Cells whose level is above their ground hold water.
        /// </summary>
        private static float[] PriorityFlood(float[] height, int nx, int nz)
        {
            int count = nx * nz;
            float[] fill = new float[count];
            bool[] closed = new bool[count];
            var open = new PriorityQueue<int, float>();
            for (int z = 0; z < nz; z++)
            {
                for (int x = 0; x < nx; x++)
                {
                    if (x != 0 && z != 0 && x != nx - 1 && z != nz - 1) continue;
                    int c = z * nx + x;
                    fill[c] = height[c];
                    closed[c] = true;
                    open.Enqueue(c, fill[c]);
                }
            }

            while (open.TryDequeue(out int c, out _))
            {
                int cx = c % nx;
                int cz = c / nx;
                for (int n = 0; n < 4; n++)
                {
                    int qx = cx + (n == 0 ? 1 : n == 1 ? -1 : 0);
                    int qz = cz + (n == 2 ? 1 : n == 3 ? -1 : 0);
                    if (qx < 0 || qz < 0 || qx >= nx || qz >= nz) continue;
                    int q = qz * nx + qx;
                    if (closed[q]) continue;
                    closed[q] = true;
                    fill[q] = MathF.Max(height[q], fill[c]);
                    open.Enqueue(q, fill[q]);
                }
            }
            return fill;
        }

        /// <summary>
        /// Fits a rectangle at <paramref name="level"/> around the lake's cells, lowering the level until no ground outside
        /// the lake inside the rectangle lies below it.
        /// </summary>
        private static bool TryShape(List<int> cells, int[] lake, int id, float[] height, int nx, int nz, Vector2 origin, float cellSize,
            float level, float minDepth, float minArea, out WaterBasin basin)
        {
            basin = default;
            float lowest = float.MaxValue;
            int deepest = cells[0];
            foreach (int c in cells)
            {
                if (height[c] < lowest)
                {
                    lowest = height[c];
                    deepest = c;
                }
            }

            for (int attempt = 0; attempt < 12; attempt++)
            {
                if (level - lowest < minDepth) return false;

                int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue, wet = 0;
                foreach (int c in cells)
                {
                    if (height[c] >= level) continue;
                    int x = c % nx;
                    int z = c / nx;
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minZ = Math.Min(minZ, z);
                    maxZ = Math.Max(maxZ, z);
                    wet++;
                }
                if (wet == 0 || wet * cellSize * cellSize < minArea) return false;

                // The rectangle reaches one cell past the water: those cells are either lake cells above the level or
                // cells outside the lake, which must not be below it.
                int x0 = Math.Max(minX - 1, 0), x1 = Math.Min(maxX + 1, nx - 1);
                int z0 = Math.Max(minZ - 1, 0), z1 = Math.Min(maxZ + 1, nz - 1);
                bool foreign = false;
                for (int z = z0; z <= z1 && !foreign; z++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        int c = z * nx + x;
                        if (lake[c] != id && height[c] < level)
                        {
                            foreign = true;
                            break;
                        }
                    }
                }

                if (!foreign)
                {
                    // Out to the ring of cells just checked: the ground there is above the level, so the outline of the
                    // water is drawn by the ground and never by the rectangle's straight edge.
                    basin = new WaterBasin
                    {
                        Level = level,
                        Min = origin + new Vector2(x0, z0) * cellSize,
                        Max = origin + new Vector2(x1, z1) * cellSize,
                        Deepest = origin + new Vector2(deepest % nx, deepest / nx) * cellSize,
                        MaxDepth = level - lowest,
                        Area = wet * cellSize * cellSize,
                    };
                    return true;
                }

                level -= MathF.Max(0.01f, (level - lowest) * 0.15f);
            }
            return false;
        }
    }
}
