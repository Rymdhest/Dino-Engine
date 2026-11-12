using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Arena;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;

namespace Dino_WorldLab
{
    /// <summary>
    /// Builds every arena kind's boss map, and every piece of each from its id, as the game does while it loads (ARENAS.md), and says what each costs: its plan, how many pieces of each shape, their
    /// triangles and lights, and how long they take to build. Fails on a piece that builds nothing, has a point that isn't a number, reaches somewhere it can't, or an arena that costs more than it
    /// should, as the whole of one is in view at once from the camera.
    /// </summary>
    internal static class ArenaCheck
    {
        private const int MostTriangles = 1_400_000;        // A whole arena's, every piece once.
        private const int MostPieceTriangles = 160_000;
        private const int MostLights = 40;
        private const long MostBuildMs = 6000;              // A whole arena's, on one thread; it's built on every core as the world loads.
        private const float FarthestReach = 90f;            // Metres from its own origin that a piece's model may reach: a wall round a large oval is drawn about a centre well past its middle.

        public static int Run(int seed, string? only)
        {
            int problems = 0;
            foreach (WorldArchetype archetype in ArenaCatalog.All)
            {
                if (only != null && archetype.Id != only) continue;
                WorldData world = new WorldGenerator().Generate(new WorldRequest(seed, archetype.Id, 10));
                WorldSite site = world.Sites[0];
                ArenaPlan? plan = site.Arena;
                Console.WriteLine($"{archetype.Name} ({archetype.Id}), seed {seed}: middle {site.Position.X:0},{site.Position.Y:0}, floor {plan?.Floor:0.0} m, {plan?.Area:0} m2 inside the seal, reaching {plan?.Reach:0} m");
                if (plan == null) { Console.WriteLine("PROBLEM: no plan"); problems++; continue; }
                foreach (ArenaEntry entry in plan.Entries) Console.WriteLine($"    {entry.Kind,-10} out at {entry.Out.X:0.0},{entry.Out.Y:0.0}");
                foreach (ArenaPiece piece in plan.Pieces) Console.WriteLine($"    {piece.Kind,-10} at {piece.At.X:0.0},{piece.At.Y:0.0},{piece.At.Z:0.0}");
                Console.WriteLine($"    {site.Spawns.Count(s => s.Role == SpawnRole.Spectator)} watching, waygate at {site.Spawns.FirstOrDefault(s => s.Role == SpawnRole.Waygate).Position}, tyrant at {site.Spawns.FirstOrDefault(s => s.Role == SpawnRole.Boss).Position}");
                problems += Pieces(world.Objects.AssetIds.Where(ArenaSpec.IsArena).Concat(plan.Entries.Select(e => e.Model).Concat(plan.Pieces.Select(p => p.Model)).Where(m => !string.IsNullOrEmpty(m))).Distinct().ToList());
            }
            Console.WriteLine(problems == 0 ? "The arenas hold up." : $"{problems} problems.");
            return problems == 0 ? 0 : 1;
        }

        /// <summary>A land's keep and the open yard its tyrant waits in (<c>--land id</c>, or <c>any</c> for the seed's own): where everyone of the keep stands, and a map of where anyone can walk.</summary>
        public static int Keep(int seed, string archetype)
        {
            // "any" leaves the archetype to the seed, as check does, which grows a different world from naming it.
            WorldData world = new WorldGenerator().Generate(new WorldRequest(seed, archetype == "any" ? null : archetype));
            WorldSite? keep = world.Sites.FirstOrDefault(s => s.Kind == SiteKind.Keep);
            if (keep == null)
            {
                Console.WriteLine("PROBLEM: no keep");
                return 1;
            }
            Vector2 tyrant = keep.Spawns.FirstOrDefault(s => s.Role == SpawnRole.Boss).Position;
            Console.WriteLine($"{keep.Name}, seed {seed} ({archetype}): radius {keep.Radius:0} about {keep.Position.X:0},{keep.Position.Y:0}, its yard {SettlementStage.KeepYard(keep):0} m across from its middle, the tyrant at {tyrant.X:0.0},{tyrant.Y:0.0}");
            // The keep, two metres to a character, north up: # walkable and passable, + walkable only, . neither, T the tyrant.
            for (float z = keep.Radius + 6f; z >= -keep.Radius - 6f; z -= 2f)
            {
                var row = new System.Text.StringBuilder("    ");
                for (float x = -keep.Radius - 6f; x <= keep.Radius + 6f; x += 2f)
                {
                    Vector2 at = keep.Position + new Vector2(x, z);
                    char c = (at - tyrant).Length < 1.2f ? 'T' : world.Passable.IsSet(at) ? '#' : world.Walkable.IsSet(at) ? '+' : '.';
                    row.Append(c);
                }
                Console.WriteLine(row.ToString());
            }
            foreach (SiteSpawn spawn in keep.Spawns)
            {
                Console.WriteLine($"    {spawn.Role,-10} {(spawn.Position - keep.Position).Length,5:0.0} m from the middle, {(world.Passable.IsSet(spawn.Position) ? "passable" : "NOT PASSABLE")}");
            }
            return 0;
        }

        private static int Pieces(List<string> ids)
        {
            int problems = 0, triangles = 0, lights = 0;
            long total = 0;
            var byShape = new SortedDictionary<string, (int Count, int Triangles, int Lights, long Ms)>();
            foreach (string id in ids)
            {
                if (!ArenaSpec.TryParse(id, out ArenaSpec spec)) { Console.WriteLine($"PROBLEM: '{id}' doesn't parse"); problems++; continue; }
                if (spec.Id != id) { Console.WriteLine($"PROBLEM: '{id}' writes back as '{spec.Id}'"); problems++; }
                var lit = new List<AssetLight>();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                Mesh mesh = ArenaForge.Make(spec, lit);
                watch.Stop();
                float[] positions = mesh.getAllPositionsArray();
                int count = mesh.getAllIndicesArray().Length / 3;
                if (count == 0 && spec.Shape != ArenaShape.Lamp) { Console.WriteLine($"PROBLEM: {id} builds nothing"); problems++; }
                if (count > MostPieceTriangles) { Console.WriteLine($"PROBLEM: {id} is {count} triangles"); problems++; }
                float reach = 0f;
                bool broken = false;
                for (int i = 0; i + 2 < positions.Length; i += 3)
                {
                    if (!float.IsFinite(positions[i]) || !float.IsFinite(positions[i + 1]) || !float.IsFinite(positions[i + 2])) broken = true;
                    else reach = MathF.Max(reach, new Vector3(positions[i], positions[i + 1], positions[i + 2]).Length);
                }
                if (broken) { Console.WriteLine($"PROBLEM: {id} has a point that isn't a number"); problems++; }
                if (reach > FarthestReach) { Console.WriteLine($"PROBLEM: {id} reaches {reach:0.0} m from its origin"); problems++; }
                string shape = spec.Shape.ToString();
                byShape.TryGetValue(shape, out var sum);
                byShape[shape] = (sum.Count + 1, sum.Triangles + count, sum.Lights + lit.Count, sum.Ms + watch.ElapsedMilliseconds);
                triangles += count;
                lights += lit.Count(l => l.FlameWidth <= 0f);
                total += watch.ElapsedMilliseconds;
            }
            foreach ((string shape, var sum) in byShape) Console.WriteLine($"    {shape,-11} {sum.Count,4} pieces {sum.Triangles,9} triangles {sum.Lights,3} lights {sum.Ms,5} ms");
            Console.WriteLine($"    All         {ids.Count,4} pieces {triangles,9} triangles {lights,3} lights {total,5} ms");
            if (triangles > MostTriangles) { Console.WriteLine($"PROBLEM: over {MostTriangles} triangles"); problems++; }
            if (lights > MostLights) { Console.WriteLine($"PROBLEM: {lights} lights, over {MostLights}"); problems++; }
            if (total > MostBuildMs) { Console.WriteLine($"PROBLEM: takes over {MostBuildMs} ms to build"); problems++; }
            return problems;
        }
    }
}
