using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_WorldLab
{
    /// <summary>
    /// Grows every piece of every place with a story of its own in a run of lands from its id, as the game does while a land loads (PLACES.md), and says what each kind costs: how many there
    /// are, how many are different models, their triangles and lights, and how long they take to grow. Fails on a piece that builds nothing, has a point that isn't a number, reaches further
    /// than it should or doesn't write back as the id it was read from, and on a place that costs more than a land can afford to draw, as one is often all the camera sees. Also says which lands
    /// have which, for looking at one in the game.
    /// </summary>
    internal static class PlaceCheck
    {
        private const int MostBigTriangles = 60_000;        // A hull, a laboratory, a quarry's face, a fossil: one of each in a place.
        private const int MostPieceTriangles = 14_000;      // Anything else, which may be drawn a dozen times over.
        private const int MostModels = 70;                  // Different models in one place: each is a draw of its own.
        private const int MostLights = 40;
        private const float FarthestReach = 48f;

        /// <summary>
        /// What makes each place what it is, and how many of it there have to be: a wreck with no hull, a prison with no cages or a nesting ground with one nest in it says nothing, however
        /// much else stands there. Any one of the pieces listed counts.
        /// </summary>
        internal static readonly Dictionary<SiteKind, (PlacePiece[] Pieces, int Least)> Signatures = new()
        {
            [SiteKind.Shipwreck] = (new[] { PlacePiece.Hull }, 1),
            [SiteKind.Prison] = (new[] { PlacePiece.Cage }, 3),
            [SiteKind.Laboratory] = (new[] { PlacePiece.LabHall }, 1),
            [SiteKind.Excavation] = (new[] { PlacePiece.Fossil }, 1),
            [SiteKind.Nest] = (new[] { PlacePiece.NestMound, PlacePiece.Web, PlacePiece.Carcass }, 4),
            [SiteKind.LumberCamp] = (new[] { PlacePiece.Stump }, 6),
            [SiteKind.Quarry] = (new[] { PlacePiece.Face }, 1),
            [SiteKind.FishingHamlet] = (new[] { PlacePiece.Jetty, PlacePiece.Boat }, 2),
            [SiteKind.Ruins] = (new[] { PlacePiece.RuinWall, PlacePiece.RuinColumn, PlacePiece.RuinArch }, 4),
            [SiteKind.Tradepost] = (new[] { PlacePiece.Scales, PlacePiece.Bales }, 2),
            [SiteKind.Farm] = (new[] { PlacePiece.FeedRack, PlacePiece.HatchingShed }, 2),
            [SiteKind.SnowCastle] = (new[] { PlacePiece.SnowWall, PlacePiece.SnowTower }, 4),
        };

        public static int Run(int first, int count, string? archetype, string? kind, bool pieces = false)
        {
            int problems = 0, places = 0;
            var byPiece = new SortedDictionary<string, (int Models, int Triangles, int Lights, long Ms, int Most)>();
            var byKind = new SortedDictionary<string, int>();
            for (int seed = first; seed < first + count; seed++)
            {
                WorldData world = new Dino_Engine.World.Generation.WorldGenerator().Generate(new WorldRequest(seed, archetype));
                foreach (WorldSite site in world.Sites.Where(s => s.Definition.IsStory))
                {
                    if (kind != null && !string.Equals(site.Kind.ToString(), kind, StringComparison.OrdinalIgnoreCase)) continue;
                    places++;
                    byKind[site.Kind.ToString()] = byKind.GetValueOrDefault(site.Kind.ToString()) + 1;
                    float reach = (site.Place?.Reach ?? site.Radius) + 12f;
                    Vector2 middle = site.Place?.Centre ?? site.Position;
                    var ids = new HashSet<string>();
                    var counted = new SortedDictionary<PlacePiece, int>();
                    int placed = 0, placedLights = 0;
                    foreach (WorldObject item in world.Objects.All())
                    {
                        string id = world.Objects.AssetIds[item.Asset];
                        if (!PlaceSpec.IsPlace(id) || (item.Position.Xz - middle).Length > reach) continue;
                        ids.Add(id);
                        placed++;
                        if (PlaceSpec.TryParse(id, out PlaceSpec seen)) counted[seen.Kind] = counted.GetValueOrDefault(seen.Kind) + 1;
                    }
                    int triangles = 0;
                    var lightsOf = new Dictionary<string, int>();
                    foreach (string id in ids)
                    {
                        if (!PlaceSpec.TryParse(id, out PlaceSpec spec)) { Console.WriteLine($"PROBLEM: '{id}' doesn't parse"); problems++; continue; }
                        if (spec.Id != id) { Console.WriteLine($"PROBLEM: '{id}' writes back as '{spec.Id}'"); problems++; }
                        var lit = new List<AssetLight>();
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        Mesh mesh = PlaceForge.Make(spec, lit);
                        watch.Stop();
                        float[] positions = mesh.getAllPositionsArray();
                        int n = mesh.getAllIndicesArray().Length / 3;
                        if (n == 0) { Console.WriteLine($"PROBLEM: {id} builds nothing"); problems++; }
                        int most = PlaceForge.Slowness(spec.Kind) >= 3 ? MostBigTriangles : MostPieceTriangles;
                        if (n > most) { Console.WriteLine($"PROBLEM: {id} is {n} triangles"); problems++; }
                        float far = 0f;
                        bool broken = false;
                        for (int i = 0; i + 2 < positions.Length; i += 3)
                        {
                            if (!float.IsFinite(positions[i]) || !float.IsFinite(positions[i + 1]) || !float.IsFinite(positions[i + 2])) broken = true;
                            else far = MathF.Max(far, new Vector3(positions[i], positions[i + 1], positions[i + 2]).Length);
                        }
                        if (broken) { Console.WriteLine($"PROBLEM: {id} has a point that isn't a number"); problems++; }
                        if (far > FarthestReach) { Console.WriteLine($"PROBLEM: {id} reaches {far:0.0} m from its origin"); problems++; }
                        string piece = spec.Kind.ToString();
                        byPiece.TryGetValue(piece, out var sum);
                        byPiece[piece] = (sum.Models + 1, sum.Triangles + n, sum.Lights + lit.Count, sum.Ms + watch.ElapsedMilliseconds, Math.Max(sum.Most, n));
                        triangles += n;
                        lightsOf[id] = lit.Count(l => l.Intensity > 0f);
                    }
                    foreach (WorldObject item in world.Objects.All())
                    {
                        string id = world.Objects.AssetIds[item.Asset];
                        if (lightsOf.TryGetValue(id, out int lights) && (item.Position.Xz - middle).Length <= reach) placedLights += lights;
                    }
                    int hostiles = site.Spawns.Count(s => s.Role.IsHostile()), captives = site.Spawns.Count(s => s.Role == SpawnRole.Captive);
                    Console.WriteLine($"[{seed}] {site.Kind,-12} {site.Name,-30} at {site.Position.X:0},{site.Position.Y:0}: {placed} pieces of {ids.Count} models, {triangles} triangles in its models, {placedLights} lights, {hostiles} hostile, {captives} captive, {(site.Spawns.Any(s => s.Role == SpawnRole.Chest) ? "a chest" : "NO CHEST")}");
                    if (ids.Count > MostModels) { Console.WriteLine($"PROBLEM: {site.Name} has {ids.Count} different models"); problems++; }
                    if (placedLights > MostLights) { Console.WriteLine($"PROBLEM: {site.Name} has {placedLights} lights"); problems++; }
                    if (hostiles > 0 && !site.Spawns.Any(s => s.Role == SpawnRole.Chest)) { Console.WriteLine($"PROBLEM: {site.Name} is held, but has no chest"); problems++; }
                    if (placed == 0) { Console.WriteLine($"PROBLEM: {site.Name} has nothing of its own in it"); problems++; }
                    if (Signatures.TryGetValue(site.Kind, out var signature))
                    {
                        int has = signature.Pieces.Sum(p => counted.GetValueOrDefault(p));
                        if (has < signature.Least) { Console.WriteLine($"PROBLEM: {site.Name} has {has} of {string.Join(" or ", signature.Pieces)}, and wants {signature.Least}"); problems++; }
                    }
                    if (pieces) Console.WriteLine("    " + string.Join(", ", counted.Select(c => $"{c.Value} {c.Key}")));
                }
            }
            foreach ((string piece, var sum) in byPiece) Console.WriteLine($"{piece,-14} {sum.Models,4} models {sum.Triangles,9} triangles (most {sum.Most,6}) {sum.Lights,4} lights {sum.Ms,6} ms");
            Console.WriteLine(string.Join(", ", byKind.Select(k => $"{k.Value} {k.Key}")));
            if (places == 0) { Console.WriteLine("PROBLEM: no places with a story in those lands"); problems++; }
            Console.WriteLine(problems == 0 ? $"{places} places hold up." : $"{problems} problems.");
            return problems == 0 ? 0 : 1;
        }
    }
}
