using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Churchyard;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_WorldLab
{
    /// <summary>
    /// Grows every piece of the churchyards of a run of lands from its id, as the game does while a land loads (CHURCHYARD.md), and says what each kind costs: how many there are,
    /// how many of them are different models, their triangles and lights, and how long they take to grow. Fails on a piece that builds nothing, has a point that isn't a number,
    /// reaches further than it should, or a churchyard that costs more than a land can afford to draw, as one is often all the camera sees.
    /// </summary>
    internal static class YardCheck
    {
        private const int MostChurchTriangles = 160_000;
        private const int MostPieceTriangles = 12_000;      // Anything but the church: a headstone is drawn a hundred times over.
        private const int MostModels = 90;                  // Different models in one churchyard: each is a draw of its own.
        private const int MostLights = 60;
        private const float FarthestReach = 45f;

        public static int Run(int first, int count, string? archetype)
        {

            int problems = 0, yards = 0;
            var byKind = new SortedDictionary<string, (int Models, int Triangles, int Lights, long Ms, int Most)>();
            for (int seed = first; seed < first + count; seed++)
            {
                WorldData world = new Dino_Engine.World.Generation.WorldGenerator().Generate(new WorldRequest(seed, archetype));
                foreach (WorldSite site in world.Sites.Where(s => s.Kind == SiteKind.Graveyard))
                {
                    yards++;
                    YardPlan? yard = site.Yard;
                    var ids = new HashSet<string>();
                    int placed = 0;
                    foreach (WorldObject item in world.Objects.All())
                    {
                        string id = world.Objects.AssetIds[item.Asset];
                        if (!YardSpec.IsYard(id) || yard == null || !yard.Inside(item.Position.Xz, 6f)) continue;
                        ids.Add(id);
                        placed++;
                    }
                    int triangles = 0, lights = 0;
                    foreach (string id in ids)
                    {
                        if (!YardSpec.TryParse(id, out YardSpec spec)) { Console.WriteLine($"PROBLEM: '{id}' doesn't parse"); problems++; continue; }
                        if (spec.Id != id) { Console.WriteLine($"PROBLEM: '{id}' writes back as '{spec.Id}'"); problems++; }
                        var lit = new List<AssetLight>();
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        Mesh mesh = YardForge.Make(spec, lit);
                        watch.Stop();
                        float[] positions = mesh.getAllPositionsArray();
                        int n = mesh.getAllIndicesArray().Length / 3;
                        if (n == 0) { Console.WriteLine($"PROBLEM: {id} builds nothing"); problems++; }
                        int most = spec.Kind == YardKind.Church ? MostChurchTriangles : MostPieceTriangles;
                        if (n > most) { Console.WriteLine($"PROBLEM: {id} is {n} triangles"); problems++; }
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
                        byKind[kind] = (sum.Models + 1, sum.Triangles + n, sum.Lights + lit.Count, sum.Ms + watch.ElapsedMilliseconds, Math.Max(sum.Most, n));
                        triangles += n;
                        lights += lit.Count(l => l.Intensity > 0f);
                    }
                    // Lights are counted as placed, as each object carries its own.
                    int placedLights = 0;
                    foreach (WorldObject item in world.Objects.All())
                    {
                        string id = world.Objects.AssetIds[item.Asset];
                        if (!YardSpec.TryParse(id, out YardSpec spec) || yard == null || !yard.Inside(item.Position.Xz, 6f)) continue;
                        if (spec.Kind is YardKind.Lamp or YardKind.Candles or YardKind.Gate) placedLights++;
                        if (spec.Kind == YardKind.Church) placedLights += 6;
                    }
                    Console.WriteLine($"[{seed}] {site.Name}: {yard?.HalfX * 2f:0} x {yard?.HalfZ * 2f:0} m, {yard?.Gates.Count} gates, {placed} pieces of {ids.Count} models, {triangles} triangles in its models, about {placedLights} lights");
                    if (ids.Count > MostModels) { Console.WriteLine($"PROBLEM: {site.Name} has {ids.Count} different models"); problems++; }
                    if (placedLights > MostLights) { Console.WriteLine($"PROBLEM: {site.Name} has about {placedLights} lights"); problems++; }
                }
            }
            foreach ((string kind, var sum) in byKind) Console.WriteLine($"{kind,-10} {sum.Models,4} models {sum.Triangles,9} triangles (most {sum.Most,6}) {sum.Lights,4} lights {sum.Ms,6} ms");
            if (yards == 0) { Console.WriteLine("PROBLEM: no churchyards in those lands"); problems++; }
            Console.WriteLine(problems == 0 ? $"{yards} churchyards hold up." : $"{problems} problems.");
            return problems == 0 ? 0 : 1;
        }
    }
}
