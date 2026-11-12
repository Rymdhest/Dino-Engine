using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Rock;
using Dino_Engine.World;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Dino_WorldLab
{
    /// <summary>
    /// Grows the skin of a world's rock (<see cref="RockSkin"/>) and says what it costs and whether it holds: how long a tile takes and how many triangles it comes to, whether
    /// neighbouring tiles meet where they share an edge, and whether any of it stands over ground somebody walks on.
    /// </summary>
    internal static class SkinCheck
    {
        public static int Probe(int seed, string? archetype, Vector3 from, Vector3 to, int steps)
        {
            WorldData world = new WorldGenerator().Generate(new WorldRequest(seed, archetype));
            var skin = new RockSkin(world);
            foreach (string line in skin.Probe(from, to, steps)) Console.WriteLine(line);
            return 0;
        }

        public static int Run(int seed, int count, string? archetype, Vector2? at, float radius)
        {
            int failures = 0;
            for (int s = seed; s < seed + count; s++)
            {
                var clock = Stopwatch.StartNew();
                WorldData world = new WorldGenerator().Generate(new WorldRequest(s, archetype));
                double generated = clock.Elapsed.TotalMilliseconds;
                clock.Restart();
                var skin = new RockSkin(world);
                double prepared = clock.Elapsed.TotalMilliseconds;

                List<Vector2i> tiles = skin.Tiles.Where(t => at is not Vector2 place || (new Vector2(t.X + 0.5f, t.Y + 0.5f) * RockSkin.TileSize - place).Length <= radius).ToList();
                var built = new ConcurrentDictionary<Vector2i, FloraMesh?>();
                var times = new ConcurrentBag<double>();
                clock.Restart();
                Parallel.ForEach(tiles, new ParallelOptions { MaxDegreeOfParallelism = Environment.GetEnvironmentVariable("SKIN_SERIAL") == "1" ? 1 : -1 }, tile =>
                {
                    var one = Stopwatch.StartNew();
                    built[tile] = skin.Build(tile);
                    times.Add(one.Elapsed.TotalMilliseconds);
                });
                double all = clock.Elapsed.TotalMilliseconds;

                int triangles = built.Values.Sum(m => m?.TriangleCount ?? 0);
                int filled = built.Values.Count(m => m != null);
                double[] sorted = times.OrderBy(t => t).ToArray();
                Console.WriteLine($"{s} {world.Plan.Archetype.Id,-12} {world.Geology.Kind,-10} {tiles.Count} tiles ({filled} with skin), {triangles} triangles, {triangles / Math.Max(filled, 1)} a tile at most {built.Values.Max(m => m?.TriangleCount ?? 0)}; " +
                    $"a tile takes {(sorted.Length > 0 ? sorted[sorted.Length / 2] : 0):0} ms, at worst {(sorted.Length > 0 ? sorted[^1] : 0):0}; all {all:0} ms on every core; set up {prepared:0} ms, world {generated:0} ms");

                // Tiles meet: every open edge one has along its neighbour, out of the ground, the neighbour has too, so there's no crack between them.
                int torn = 0, shared = 0;
                var grounds = new Dictionary<Vector2i, Func<Vector3, float>?>();
                foreach ((Vector2i tile, FloraMesh? mesh) in built)
                {
                    if (mesh == null) continue;
                    foreach (Vector2i step in new[] { new Vector2i(1, 0), new Vector2i(0, 1), new Vector2i(-1, 0), new Vector2i(0, -1) })
                    {
                        if (!built.TryGetValue(tile + step, out FloraMesh? next) || next == null) continue;
                        // The cells they share: this tile's last and the next one's first.
                        float edge = step.X == 1 ? (tile.X + 1) * RockSkin.TileSize - RockSkin.Cell : step.X == -1 ? tile.X * RockSkin.TileSize - RockSkin.Cell
                                   : step.Y == 1 ? (tile.Y + 1) * RockSkin.TileSize - RockSkin.Cell : tile.Y * RockSkin.TileSize - RockSkin.Cell;
                        grounds.TryGetValue(tile, out Func<Vector3, float>? ground);
                        if (ground == null) grounds[tile] = ground = skin.GroundOf(tile);
                        // At a corner four tiles meet, and an edge there can be any of the others'.
                        var theirs = new HashSet<(Vector3, Vector3)>();
                        for (int dz = -1; dz <= 1; dz++)
                            for (int dx = -1; dx <= 1; dx++)
                                if ((dx != 0 || dz != 0) && built.TryGetValue(tile + new Vector2i(dx, dz), out FloraMesh? other) && other != null) theirs.UnionWith(EdgesOf(other, out _));
                        EdgesOf(mesh, out List<(Vector3 a, Vector3 b)> open);
                        foreach ((Vector3 p, Vector3 q) in open)
                        {
                            bool inStrip = InStrip(p, step, edge) && InStrip(q, step, edge);
                            if (!inStrip) continue;
                            // Each tile throws away what's under the ground by its own faces, so where an edge goes in, one may have kept a face there that the other didn't: nobody sees it.
                            Vector3 pm = p / 1000f, qm = q / 1000f;
                            if (ground == null || ground(pm) < 0.02f || ground(qm) < 0.02f) continue;
                            shared++;
                            // Within two millimetres: four hundred metres from the origin a float is a few hundredths of a millimetre, and rounding to whole ones splits the difference.
                            if (!theirs.Contains(Ordered(p, q)) && !theirs.Any(e => ((e.Item1 - p).Length < 2f && (e.Item2 - q).Length < 2f) || ((e.Item1 - q).Length < 2f && (e.Item2 - p).Length < 2f)))
                            {
                                torn++;
                                if (torn <= 8) Console.WriteLine($"      torn {p / 1000f} to {q / 1000f} of {tile}, towards {step}, ground {ground(pm):0.00} {ground(qm):0.00}, the neighbour has a point within {next.Vertices.Min(o => (o.Position - pm).Length):0.000} and {next.Vertices.Min(o => (o.Position - qm).Length):0.000}");
                            }
                        }
                    }
                }

                // Nothing stands where anyone would be standing in it: over walkable ground, as high as a body. An overhang well over a path is the cliff's.
                int over = 0;
                foreach (FloraMesh? mesh in built.Values)
                {
                    if (mesh == null) continue;
                    foreach (FloraVertex v in mesh.Vertices)
                    {
                        Vector2 xz = v.Position.Xz;
                        float above = v.Position.Y - world.Height.SampleCubic(xz.X, xz.Y);
                        // Well inside it: a point of a surface net is anywhere in its cell, a quarter of a metre, whatever the surface between the grid's points does.
                        bool inside = world.Walkable.IsSet(xz) && world.Walkable.IsSet(xz + new Vector2(0.3f, 0f)) && world.Walkable.IsSet(xz - new Vector2(0.3f, 0f)) && world.Walkable.IsSet(xz + new Vector2(0f, 0.3f)) && world.Walkable.IsSet(xz - new Vector2(0f, 0.3f));
                        if (inside && above > 0.15f && above < 1f)
                        {
                            over++;
                            if (over <= 4) Console.WriteLine($"      over walkable ground at {v.Position}, {above:0.00} over it, the slope there {world.Height.SlopeAt(xz):0.00}");
                        }
                    }
                }

                bool fails = torn * 200 > shared || over > 0;
                if (fails) failures++;
                Console.WriteLine($"   tiles meet: {shared - torn} of {shared} shared edges found by the neighbour{(torn * 200 > shared ? "  FAILS" : "")}; over walkable ground: {over}{(over > 0 ? "  FAILS" : "")}");
            }
            return failures == 0 ? 0 : 1;
        }

        private static bool InStrip(Vector3 rounded, Vector2i step, float edge)
        {
            float along = (step.X != 0 ? rounded.X : rounded.Z) / 1000f;
            return along >= edge - 1e-3f && along <= edge + RockSkin.Cell + 1e-3f;
        }

        private static (Vector3, Vector3) Ordered(Vector3 a, Vector3 b) => (a.X, a.Y, a.Z).CompareTo((b.X, b.Y, b.Z)) < 0 ? (a, b) : (b, a);

        // Every edge of a mesh by where its ends are, and those with a face on one side only.
        private static HashSet<(Vector3, Vector3)> EdgesOf(FloraMesh mesh, out List<(Vector3, Vector3)> open)
        {
            var uses = new Dictionary<(Vector3, Vector3), int>();
            for (int i = 0; i < mesh.Indices.Count; i += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    var key = Ordered(Round(mesh.Vertices[mesh.Indices[i + k]].Position), Round(mesh.Vertices[mesh.Indices[i + (k + 1) % 3]].Position));
                    uses[key] = uses.GetValueOrDefault(key) + 1;
                }
            }
            open = uses.Where(e => e.Value == 1).Select(e => e.Key).ToList();
            return new HashSet<(Vector3, Vector3)>(uses.Keys);
        }

        private static Vector3 Round(Vector3 p) => new Vector3(MathF.Round(p.X * 1000f), MathF.Round(p.Y * 1000f), MathF.Round(p.Z * 1000f));
    }
}
