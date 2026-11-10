using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Plaza;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_WorldLab
{
    /// <summary>
    /// Builds every piece of the Haven's square from its id, as the game does on its first frame there (SQUARE.md), and says what each kind costs: how many there are, their
    /// triangles and lights, and how long they take to build. Fails on a piece that builds nothing, has a point that isn't a number, reaches somewhere it can't, or makes the
    /// square cost more than it should, as the square is drawn whenever anyone's in the Haven.
    /// </summary>
    internal static class SquareCheck
    {
        private const int MostTriangles = 900_000;          // The whole square's, every piece once.
        private const int MostPieceTriangles = 120_000;
        private const long MostBuildMs = 2500;              // The whole square's, on one thread, as it's built when the Haven is first drawn.
        private const float FarthestReach = 40f;            // Metres from its own origin that a piece's model may reach.
        private const int HavenSeed = 1610;                 // Lands.HavenSeed, in the game.

        public static int Run()
        {
            WorldData world = new Dino_Engine.World.Generation.WorldGenerator().Generate(new WorldRequest(HavenSeed, "haven"));
            var ids = world.Objects.AssetIds.Where(PlazaSpec.IsPlaza).ToList();
            int problems = 0, triangles = 0, lights = 0;
            long total = 0;
            var byKind = new SortedDictionary<string, (int Count, int Triangles, int Lights, long Ms)>();
            foreach (string id in ids)
            {
                if (!PlazaSpec.TryParse(id, out PlazaSpec spec)) { Console.WriteLine($"PROBLEM: '{id}' doesn't parse"); problems++; continue; }
                if (spec.Id != id) { Console.WriteLine($"PROBLEM: '{id}' writes back as '{spec.Id}'"); problems++; }
                var lit = new List<AssetLight>();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                Mesh mesh = PlazaForge.Make(spec, lit);
                watch.Stop();
                float[] positions = mesh.getAllPositionsArray();
                int count = mesh.getAllIndicesArray().Length / 3;
                if (count == 0) { Console.WriteLine($"PROBLEM: {id} builds nothing"); problems++; }
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
                string kind = spec.Kind.ToString();
                byKind.TryGetValue(kind, out var sum);
                byKind[kind] = (sum.Count + 1, sum.Triangles + count, sum.Lights + lit.Count, sum.Ms + watch.ElapsedMilliseconds);
                triangles += count;
                lights += lit.Count;
                total += watch.ElapsedMilliseconds;
            }
            foreach ((string kind, var sum) in byKind) Console.WriteLine($"{kind,-8} {sum.Count,3} pieces {sum.Triangles,8} triangles {sum.Lights,3} lights {sum.Ms,5} ms");
            Console.WriteLine($"All      {ids.Count,3} pieces {triangles,8} triangles {lights,3} lights {total,5} ms");
            if (ids.Count == 0) { Console.WriteLine("PROBLEM: the Haven has no square"); problems++; }
            if (triangles > MostTriangles) { Console.WriteLine($"PROBLEM: over {MostTriangles} triangles"); problems++; }
            if (total > MostBuildMs) { Console.WriteLine($"PROBLEM: takes over {MostBuildMs} ms to build"); problems++; }
            Console.WriteLine(problems == 0 ? "The square holds up." : $"{problems} problems.");
            return problems == 0 ? 0 : 1;
        }
    }
}
