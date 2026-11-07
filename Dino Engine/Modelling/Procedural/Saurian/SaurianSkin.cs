using Dino_Engine.Modelling.Procedural.Rock;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    /// <summary>What a skin is told about each point on it: which bones it hangs from, what colour it is, and how its texture lies.</summary>
    public interface ISkinDresser
    {
        /// <summary>Which of its ways of laying a texture a point goes by: one round the body, one round each limb, one from the side of the head.</summary>
        int Mapping(Vector3 p, SaurianPart part);

        /// <summary>Where a point is on a mapping's texture, and how far round it goes before it comes back to the start, or 0 if it doesn't.</summary>
        Vector2 Uv(Vector3 p, int mapping, out float period);

        /// <summary>Which way the texture's first axis runs on the surface there.</summary>
        Vector3 Tangent(Vector3 p, Vector3 normal, int mapping);

        Hang Hang(Vector3 p, SaurianPart part, ReadOnlySpan<float> parts);

        Vector3 Colour(Vector3 p, Vector3 normal, SaurianPart part, out int material, out float glow);
    }

    /// <summary>
    /// Turns a field into a skin. It's sampled far finer than can be drawn, surface nets make even triangles of it, quadric decimation spends the budget where the shape needs it, and normals come from the
    /// field itself, so they're smooth however the triangles fell. Edges are kept short enough to bend where the skin is skinned. Each triangle takes one way of laying its texture, and vertices are
    /// split wherever two triangles disagree, so a texture never smears across where one way meets another. Safe on any thread.
    /// </summary>
    public static class SaurianSkin
    {
        private const float SmallestPiece = 0.002f;    // Of the whole, for a loose piece to be kept.

        public static void Grow(SaurianMesh mesh, SaurianField field, float cell, int budget, float longestEdge, ISkinDresser dress)
        {
            float[] grid = field.Sample(cell, out Vector3 origin, out int nx, out int ny, out int nz);
            var points = new List<Vector3>();
            var triangles = new List<int>();
            SurfaceNets.Extract(grid, nx, ny, nz, origin, cell, points, triangles);
            if (triangles.Count == 0) return;
            SweepUp(points, triangles);
            Decimator.Reduce(points, triangles, budget, longestEdge);

            int count = points.Count;
            var normals = new Vector3[count];
            var parts = new SaurianPart[count];
            var hangs = new Hang[count];
            var colours = new Vector3[count];
            var materials = new int[count];
            var glows = new float[count];
            Span<float> distances = stackalloc float[8];
            for (int i = 0; i < count; i++)
            {
                Vector3 p = points[i];
                field.Distance(p, out parts[i]);
                field.Parts(p, distances);
                normals[i] = field.Normal(p, cell * 0.5f);
                hangs[i] = dress.Hang(p, parts[i], distances);
                colours[i] = dress.Colour(p, normals[i], parts[i], out materials[i], out glows[i]);
            }

            // One mapping to a triangle, the one most of its corners go by, and a corner that wraps round is unwrapped to where the first one is.
            var made = new Dictionary<(int point, int mapping, int wrap), int>();
            var mapped = new Dictionary<(int point, int mapping), (Vector2 uv, float period)>();
            Span<int> corner = stackalloc int[3];
            Span<int> mapping = stackalloc int[3];
            for (int t = 0; t < triangles.Count; t += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    corner[k] = triangles[t + k];
                    mapping[k] = dress.Mapping(points[corner[k]], parts[corner[k]]);
                }
                int chosen = mapping[0] == mapping[1] || mapping[0] == mapping[2] ? mapping[0] : mapping[1] == mapping[2] ? mapping[1] : mapping[0];

                Vector2 first = default;
                float period = 0f;
                for (int k = 0; k < 3; k++)
                {
                    if (!mapped.TryGetValue((corner[k], chosen), out var found))
                    {
                        found.uv = dress.Uv(points[corner[k]], chosen, out found.period);
                        mapped[(corner[k], chosen)] = found;
                    }
                    if (k == 0) first = found.uv;
                    period = MathF.Max(period, found.period);
                }

                for (int k = 0; k < 3; k++)
                {
                    var found = mapped[(corner[k], chosen)];
                    Vector2 uv = found.uv;
                    int wrap = 0;
                    if (period > 0f)
                    {
                        while (uv.Y - first.Y > period * 0.5f) { uv.Y -= period; wrap--; }
                        while (first.Y - uv.Y > period * 0.5f) { uv.Y += period; wrap++; }
                    }
                    if (!made.TryGetValue((corner[k], chosen, wrap), out int index))
                    {
                        int p = corner[k];
                        index = mesh.Vertex(points[p], normals[p], dress.Tangent(points[p], normals[p], chosen), uv, colours[p], materials[p], hangs[p], glows[p]);
                        made[(p, chosen, wrap)] = index;
                    }
                    corner[k] = index;
                }

                // Surface nets wind outward; keep it so.
                mesh.Triangle(corner[0], corner[1], corner[2]);
            }
        }

        // Loose bits a field leaves where two pieces nearly touched.
        private static void SweepUp(List<Vector3> points, List<int> triangles)
        {
            int[] root = new int[points.Count];
            for (int i = 0; i < root.Length; i++) root[i] = i;
            int Find(int i)
            {
                while (root[i] != i)
                {
                    root[i] = root[root[i]];
                    i = root[i];
                }
                return i;
            }

            for (int i = 0; i < triangles.Count; i += 3)
            {
                root[Find(triangles[i + 1])] = Find(triangles[i]);
                root[Find(triangles[i + 2])] = Find(triangles[i]);
            }

            var faces = new Dictionary<int, int>();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int piece = Find(triangles[i]);
                faces[piece] = faces.GetValueOrDefault(piece) + 1;
            }

            int least = Math.Max((int)(triangles.Count / 3 * SmallestPiece), 8);
            var kept = new List<int>(triangles.Count);
            for (int i = 0; i < triangles.Count; i += 3)
            {
                if (faces[Find(triangles[i])] < least) continue;
                kept.Add(triangles[i]);
                kept.Add(triangles[i + 1]);
                kept.Add(triangles[i + 2]);
            }
            triangles.Clear();
            triangles.AddRange(kept);
        }
    }
}
