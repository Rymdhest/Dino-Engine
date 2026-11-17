using System.Collections.Concurrent;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>Somewhere on the edge of a roof a bird could sit, in its model's frame: where its feet go, and the way it faces, out over the drop.</summary>
    public readonly struct RoofPerch
    {
        public readonly Vector3 At;
        public readonly Vector2 Out;

        public RoofPerch(Vector3 at, Vector2 outward)
        {
            At = at;
            Out = outward;
        }
    }

    /// <summary>
    /// How high a model stands over each metre of the ground it covers, in its own frame: the highest point of any triangle reaching over that metre. It's what anything flying keeps over (Wildlife's
    /// <c>SkyFloor</c>), so a bird goes over a roof or a crown as it's really built and never through it. Measured once off the mesh as it's made, whatever made it.
    /// </summary>
    public sealed class ModelTop
    {
        /// <summary>Metres a cell spans, in the model's frame.</summary>
        public readonly float Cell;
        public readonly float MinX, MinZ;
        public readonly int Width, Depth;
        /// <summary>The top over each cell, row by row along x, or negative infinity where nothing stands.</summary>
        public readonly float[] Top;
        /// <summary>The highest point of the model.</summary>
        public readonly float Highest;
        /// <summary>Where a bird could sit along the edges of its roofs (<see cref="FindPerches"/>), for a building, and nothing for anything else.</summary>
        public RoofPerch[] Perches { get; private set; } = Array.Empty<RoofPerch>();

        private ModelTop(float cell, float minX, float minZ, int width, int depth, float[] top, float highest)
        {
            Cell = cell;
            MinX = minX;
            MinZ = minZ;
            Width = width;
            Depth = depth;
            Top = top;
            Highest = highest;
        }

        private const float FineCell = 1f;
        private const int MostCells = 160 * 160;   // Past which a model is measured in coarser cells, as the largest pieces (an arena's ring) are hundreds of metres.

        /// <summary>Measures triangles given as three positions each, by a callback that fills them in, as the two kinds of mesh keep them differently.</summary>
        private delegate bool TriangleSource(int index, out Vector3 a, out Vector3 b, out Vector3 c);

        public static ModelTop? Of(FloraMesh mesh)
        {
            List<FloraVertex> v = mesh.Vertices;
            List<int> i = mesh.Indices;
            return Measure(i.Count / 3, (int t, out Vector3 a, out Vector3 b, out Vector3 c) =>
            {
                a = v[i[t * 3]].Position;
                b = v[i[t * 3 + 1]].Position;
                c = v[i[t * 3 + 2]].Position;
                return true;
            });
        }

        public static ModelTop? Of(Mesh mesh, bool perches = false)
        {
            List<Face>? faces = mesh.faces;
            if (faces == null) return null;
            TriangleSource source = (int t, out Vector3 a, out Vector3 b, out Vector3 c) =>
            {
                Face face = faces[t];
                a = face.A.position;
                b = face.B.position;
                c = face.C.position;
                return true;
            };
            ModelTop? top = Measure(faces.Count, source);
            if (top != null && perches) top.Perches = FindPerches(faces.Count, source, top);
            return top;
        }

        private const float PerchCell = 0.25f;      // Metres, for finding an edge to within a bird's foot.
        private const int MostPerchCells = 480 * 480;
        private const float LowestPerch = 2.5f;     // Metres up, in the model's frame, under which an edge is a step or a sill and not a roof's.
        private const float EdgeDrop = 1f;          // Metres the surface falls within half a metre past an edge: steeper than any roof, so an edge is where a roof ends and not its ridge.
        private const float PerchSpacing = 0.9f;    // Metres between two perches, so that two birds on one edge sit apart.
        private const int MostPerches = 64;

        private static readonly (int X, int Z)[] Ways = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        // Where the top surface is over each small cell's middle, exactly as the triangles over it lie, and then every cell from which it falls away by more than any roof
        // does within half a metre: an eave, a gable's verge, a wall's coping, a chimney's top. A bird sits there facing out over the drop.
        private static RoofPerch[] FindPerches(int triangles, TriangleSource source, ModelTop top)
        {
            float cell = PerchCell;
            float spanX = top.Width * top.Cell, spanZ = top.Depth * top.Cell;
            while (spanX / cell * (spanZ / cell) > MostPerchCells) cell *= 2f;
            int width = Math.Max(1, (int)MathF.Ceiling(spanX / cell)), depth = Math.Max(1, (int)MathF.Ceiling(spanZ / cell));
            var surface = new float[width * depth];
            Array.Fill(surface, float.NegativeInfinity);

            for (int t = 0; t < triangles; t++)
            {
                source(t, out Vector3 a, out Vector3 b, out Vector3 c);
                float area = (b.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (b.Z - a.Z);
                if (MathF.Abs(area) < 1e-5f) continue;
                if (MathF.Max(a.Y, MathF.Max(b.Y, c.Y)) < LowestPerch) continue;
                int x0 = Math.Clamp((int)MathF.Floor((MathF.Min(a.X, MathF.Min(b.X, c.X)) - top.MinX) / cell), 0, width - 1);
                int x1 = Math.Clamp((int)MathF.Floor((MathF.Max(a.X, MathF.Max(b.X, c.X)) - top.MinX) / cell), 0, width - 1);
                int z0 = Math.Clamp((int)MathF.Floor((MathF.Min(a.Z, MathF.Min(b.Z, c.Z)) - top.MinZ) / cell), 0, depth - 1);
                int z1 = Math.Clamp((int)MathF.Floor((MathF.Max(a.Z, MathF.Max(b.Z, c.Z)) - top.MinZ) / cell), 0, depth - 1);
                for (int z = z0; z <= z1; z++)
                {
                    float pz = top.MinZ + (z + 0.5f) * cell;
                    for (int x = x0; x <= x1; x++)
                    {
                        float px = top.MinX + (x + 0.5f) * cell;
                        float u = ((b.X - px) * (c.Z - pz) - (c.X - px) * (b.Z - pz)) / area;
                        float v = ((c.X - px) * (a.Z - pz) - (a.X - px) * (c.Z - pz)) / area;
                        float w = 1f - u - v;
                        if (u < -1e-4f || v < -1e-4f || w < -1e-4f) continue;
                        float y = u * a.Y + v * b.Y + w * c.Y;
                        int i = z * width + x;
                        if (y > surface[i]) surface[i] = y;
                    }
                }
            }

            int k = Math.Max(1, (int)MathF.Round(0.5f / cell));
            var found = new List<RoofPerch>();
            for (int z = 0; z < depth && found.Count < MostPerches; z++)
            {
                for (int x = 0; x < width && found.Count < MostPerches; x++)
                {
                    float s = surface[z * width + x];
                    if (s < LowestPerch) continue;

                    float steepest = EdgeDrop;
                    (int X, int Z) outward = (0, 0);
                    foreach ((int dx, int dz) in Ways)
                    {
                        // Something to sit on behind it, and a fall in front.
                        int bx = x - dx, bz = z - dz;
                        if (bx < 0 || bz < 0 || bx >= width || bz >= depth || surface[bz * width + bx] < s - EdgeDrop * 0.5f) continue;
                        int nx = x + dx * k, nz = z + dz * k;
                        float beyond = nx < 0 || nz < 0 || nx >= width || nz >= depth ? float.NegativeInfinity : surface[nz * width + nx];
                        float drop = s - beyond;
                        if (drop <= steepest) continue;
                        steepest = drop;
                        outward = (dx, dz);
                    }
                    if (outward == (0, 0)) continue;

                    var at = new Vector3(top.MinX + (x + 0.5f) * cell, s, top.MinZ + (z + 0.5f) * cell);
                    bool apart = true;
                    for (int i = 0; i < found.Count && apart; i++) apart = (found[i].At - at).LengthSquared >= PerchSpacing * PerchSpacing;
                    if (apart) found.Add(new RoofPerch(at, new Vector2(outward.X, outward.Z)));
                }
            }
            return found.ToArray();
        }

        private static ModelTop? Measure(int triangles, TriangleSource source)
        {
            if (triangles == 0) return null;
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue, highest = float.MinValue;
            for (int t = 0; t < triangles; t++)
            {
                source(t, out Vector3 a, out Vector3 b, out Vector3 c);
                minX = MathF.Min(minX, MathF.Min(a.X, MathF.Min(b.X, c.X)));
                maxX = MathF.Max(maxX, MathF.Max(a.X, MathF.Max(b.X, c.X)));
                minZ = MathF.Min(minZ, MathF.Min(a.Z, MathF.Min(b.Z, c.Z)));
                maxZ = MathF.Max(maxZ, MathF.Max(a.Z, MathF.Max(b.Z, c.Z)));
                highest = MathF.Max(highest, MathF.Max(a.Y, MathF.Max(b.Y, c.Y)));
            }
            if (!float.IsFinite(minX) || !float.IsFinite(maxX) || !float.IsFinite(highest)) return null;

            float cell = FineCell;
            while ((maxX - minX) / cell * ((maxZ - minZ) / cell) > MostCells) cell *= 2f;
            int width = Math.Max(1, (int)MathF.Ceiling((maxX - minX) / cell + 1e-4f));
            int depth = Math.Max(1, (int)MathF.Ceiling((maxZ - minZ) / cell + 1e-4f));
            var top = new float[width * depth];
            Array.Fill(top, float.NegativeInfinity);

            // Every cell a triangle's outline reaches over takes its highest point. That's a little more than the triangle, never less, which is the side to be wrong on.
            for (int t = 0; t < triangles; t++)
            {
                source(t, out Vector3 a, out Vector3 b, out Vector3 c);
                float y = MathF.Max(a.Y, MathF.Max(b.Y, c.Y));
                int x0 = Math.Clamp((int)((MathF.Min(a.X, MathF.Min(b.X, c.X)) - minX) / cell), 0, width - 1);
                int x1 = Math.Clamp((int)((MathF.Max(a.X, MathF.Max(b.X, c.X)) - minX) / cell), 0, width - 1);
                int z0 = Math.Clamp((int)((MathF.Min(a.Z, MathF.Min(b.Z, c.Z)) - minZ) / cell), 0, depth - 1);
                int z1 = Math.Clamp((int)((MathF.Max(a.Z, MathF.Max(b.Z, c.Z)) - minZ) / cell), 0, depth - 1);
                for (int z = z0; z <= z1; z++)
                {
                    int row = z * width;
                    for (int x = x0; x <= x1; x++)
                    {
                        if (y > top[row + x]) top[row + x] = y;
                    }
                }
            }
            return new ModelTop(cell, minX, minZ, width, depth, top, highest);
        }
    }

    /// <summary>
    /// Every model's <see cref="ModelTop"/> by its asset id, filled in wherever a model is made (<see cref="WorldAssetLibrary.Get"/> in the game, or a lab growing them itself), and read by
    /// whatever flies. An id is all it takes to grow a model, so a top stays right for it in any world.
    /// </summary>
    public static class ModelTops
    {
        private static readonly ConcurrentDictionary<string, ModelTop> Known = new();
        private static readonly ConcurrentDictionary<string, byte> Nothing = new();

        /// <summary>Goes up whenever a top is learnt, so that whatever was waiting for one knows to look again.</summary>
        public static int Version => Volatile.Read(ref _version);
        private static int _version;

        public static void Learn(string id, ModelTop? top)
        {
            if (top == null) Nothing[id] = 0;
            else Known[id] = top;
            Interlocked.Increment(ref _version);
        }

        public static void Learn(string id, FloraMesh mesh) => Learn(id, ModelTop.Of(mesh));
        /// <summary>A built model's top, with where a bird could sit on its roofs, but for a hall's, whose roof goes for the camera while the hero's in it (HALLS.md).</summary>
        public static void Learn(string id, Mesh mesh) => Learn(id, ModelTop.Of(mesh, perches: !HallDesigns.TryParseAsset(id, out _, out _)));

        /// <summary>The top of a model, if it's been made. <paramref name="empty"/> says it was made and covers nothing.</summary>
        public static bool TryGet(string id, out ModelTop? top, out bool empty)
        {
            empty = Nothing.ContainsKey(id);
            if (Known.TryGetValue(id, out ModelTop? found))
            {
                top = found;
                return true;
            }
            top = null;
            return empty;
        }

        public static bool Has(string id) => Known.ContainsKey(id) || Nothing.ContainsKey(id);

        public static void Forget(string id)
        {
            Known.TryRemove(id, out _);
            Nothing.TryRemove(id, out _);
        }
    }
}
