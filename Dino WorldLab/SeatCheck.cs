using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;

namespace Dino_WorldLab
{
    /// <summary>
    /// Grows the commander's seat of a run of lands, every piece of it from its id as the game does while a land loads (SEATS.md), and holds each to what a seat promises: that it's the
    /// kind its land's seed says, that its tyrant waits in the middle of a yard nobody else stands in, that each of its three ways in can be walked through and the road comes to the great
    /// one, that it has what makes its kind what it is, a chest of what its commander took, and a garrison; and that it costs no more than a land can afford to draw, as it's all the
    /// camera sees for the whole of a land's last fight. Says what each costs, and with --list which seeds have which kind, for looking at one in the game.
    /// </summary>
    internal static class SeatCheck
    {
        private const int MostBigTriangles = 110_000;       // The one great thing a seat is built round: a hall and keep, a dome, a colossus.
        private const int MostPieceTriangles = 20_000;      // Anything else, which may be drawn a dozen times over.
        private const int MostDrawn = 1_100_000;            // Triangles of all its pieces as they stand, every copy counted.
        private const int MostModels = 120;                 // Different models: each is a draw of its own.
        private const int MostLights = 60;
        private const float FarthestReach = 48f;
        private const int LeastHostiles = 6;

        /// <summary>What makes each kind of seat what it is, and how many of each it has to have: a citadel with no hall says nothing, however much else stands there.</summary>
        internal static readonly Dictionary<SeatKind, (PlacePiece Piece, int Least)[]> Signatures = new()
        {
            [SeatKind.Citadel] = new[] { (PlacePiece.GreatHall, 1), (PlacePiece.SeatTower, 4), (PlacePiece.SeatGate, 3), (PlacePiece.SeatWall, 12), (PlacePiece.TyrantStall, 1), (PlacePiece.FallenLord, 1), (PlacePiece.GreatBrazier, 6) },
            [SeatKind.Boneyard] = new[] { (PlacePiece.BoneMatrix, 1), (PlacePiece.BonePale, 14), (PlacePiece.RibArch, 3), (PlacePiece.Fossil, 1), (PlacePiece.GreatBrazier, 6) },
            [SeatKind.Barrow] = new[] { (PlacePiece.GreatBarrow, 1), (PlacePiece.Menhir, 14), (PlacePiece.Fossil, 1), (PlacePiece.GreatBrazier, 6) },
            [SeatKind.Bileworks] = new[] { (PlacePiece.BileVats, 1), (PlacePiece.BileCradle, 1), (PlacePiece.GatePier, 6), (PlacePiece.BilePool, 5), (PlacePiece.GreatBrazier, 6) },
            [SeatKind.Hatchery] = new[] { (PlacePiece.BroodDome, 1), (PlacePiece.BroodTanks, 2), (PlacePiece.GreatShell, 1), (PlacePiece.GatePier, 6), (PlacePiece.GreatBrazier, 6) },
            [SeatKind.Foundry] = new[] { (PlacePiece.BlastFurnace, 1), (PlacePiece.Armature, 1), (PlacePiece.ScrapHeap, 3), (PlacePiece.GatePier, 6), (PlacePiece.GreatBrazier, 6) },
            [SeatKind.Triumph] = new[] { (PlacePiece.Colossus, 1), (PlacePiece.TrophyPylon, 8), (PlacePiece.Dais, 1), (PlacePiece.SeatWall, 10), (PlacePiece.GreatBrazier, 6) },
            [SeatKind.Warcamp] = new[] { (PlacePiece.Pavilion, 1), (PlacePiece.WarWagon, 24), (PlacePiece.CageWagon, 1), (PlacePiece.GreatBrazier, 6) },
        };

        public static int Run(int first, int count, string? archetype, string? kind, bool pieces, bool list)
        {
            SeatKind? wanted = kind != null && Enum.TryParse(kind, ignoreCase: true, out SeatKind parsed) ? parsed : null;
            if (kind != null && wanted == null)
            {
                Console.Error.WriteLine($"No kind of seat is called '{kind}'. There are: {string.Join(", ", SeatCatalog.All)}.");
                return 2;
            }
            if (list)
            {
                foreach (SeatKind each in SeatCatalog.All)
                {
                    var seeds = Enumerable.Range(first, 400).Where(seed => SeatCatalog.KindOf(seed) == each).Take(12);
                    Console.WriteLine($"{each,-10} {string.Join(", ", seeds)}");
                }
                return 0;
            }

            int problems = 0, seats = 0;
            var byPiece = new SortedDictionary<string, (int Models, int Triangles, int Lights, long Ms, int Most)>();
            var byKind = new SortedDictionary<string, int>();
            for (int seed = first; seats < count && seed < first + 4000; seed++)
            {
                if (wanted is SeatKind only && SeatCatalog.KindOf(seed) != only) continue;
                seats++;
                WorldData world = new WorldGenerator().Generate(new WorldRequest(seed, archetype));
                void Problem(string text)
                {
                    Console.WriteLine($"PROBLEM [{seed}]: {text}");
                    problems++;
                }
                WorldSite? site = world.Sites.FirstOrDefault(s => s.Kind == SiteKind.Keep);
                if (site?.Seat is not SeatPlan seat || !seat.Framed)
                {
                    Problem("the land has no seat");
                    continue;
                }
                byKind[seat.Kind.ToString()] = byKind.GetValueOrDefault(seat.Kind.ToString()) + 1;
                if (seat.Kind != SeatCatalog.KindOf(world.Plan.Seed)) Problem($"{site.Name} is a {seat.Kind}, and its land's seed says {SeatCatalog.KindOf(world.Plan.Seed)}");

                // What stands there, and what it costs.
                float reach = site.Radius * 1.5f;
                var ids = new Dictionary<string, int>();
                var counted = new SortedDictionary<PlacePiece, int>();
                foreach (WorldObject item in world.Objects.All())
                {
                    string id = world.Objects.AssetIds[item.Asset];
                    if (!PlaceSpec.IsPlace(id) || (item.Position.Xz - site.Position).Length > reach) continue;
                    ids[id] = ids.GetValueOrDefault(id) + 1;
                    if (PlaceSpec.TryParse(id, out PlaceSpec seen)) counted[seen.Kind] = counted.GetValueOrDefault(seen.Kind) + 1;
                }
                int triangles = 0, drawn = 0, lights = 0;
                foreach ((string id, int placed) in ids)
                {
                    if (!PlaceSpec.TryParse(id, out PlaceSpec spec))
                    {
                        Problem($"'{id}' doesn't parse");
                        continue;
                    }
                    if (spec.Id != id) Problem($"'{id}' writes back as '{spec.Id}'");
                    var lit = new List<AssetLight>();
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    Mesh mesh = PlaceForge.Make(spec, lit);
                    watch.Stop();
                    float[] positions = mesh.getAllPositionsArray();
                    int n = mesh.getAllIndicesArray().Length / 3;
                    if (n == 0) Problem($"{id} builds nothing");
                    int most = PlaceForge.Slowness(spec.Kind) >= 3 ? MostBigTriangles : MostPieceTriangles;
                    if (n > most) Problem($"{id} is {n} triangles");
                    float far = 0f;
                    bool broken = false;
                    for (int i = 0; i + 2 < positions.Length; i += 3)
                    {
                        if (!float.IsFinite(positions[i]) || !float.IsFinite(positions[i + 1]) || !float.IsFinite(positions[i + 2])) broken = true;
                        else far = MathF.Max(far, new Vector3(positions[i], positions[i + 1], positions[i + 2]).Length);
                    }
                    if (broken) Problem($"{id} has a point that isn't a number");
                    if (far > FarthestReach) Problem($"{id} reaches {far:0.0} m from its origin");
                    string piece = spec.Kind.ToString();
                    byPiece.TryGetValue(piece, out var sum);
                    byPiece[piece] = (sum.Models + 1, sum.Triangles + n, sum.Lights + lit.Count(l => l.Intensity > 0f), sum.Ms + watch.ElapsedMilliseconds, Math.Max(sum.Most, n));
                    triangles += n;
                    drawn += n * placed;
                    lights += lit.Count(l => l.Intensity > 0f) * placed;
                }
                int hostiles = site.Spawns.Count(s => s.Role.IsHostile() && s.Role != SpawnRole.Boss);
                Console.WriteLine($"[{seed}] {seat.Kind,-10} {site.Name,-24} at {site.Position.X:0},{site.Position.Y:0} r {site.Radius:0}: {ids.Values.Sum()} pieces of {ids.Count} models, {triangles} triangles in its models and {drawn} as they stand, {lights} lights, {hostiles} hostile, gate {seat.GateSide}, room {SeatCatalog.RoomAt(world, site.Position, site.Radius):0.00}{(world.Plan.Linear ? ", one way" : "")}");
                if (pieces) Console.WriteLine("    " + string.Join(", ", counted.Select(c => $"{c.Value} {c.Key}")));
                if (ids.Count > MostModels) Problem($"{site.Name} has {ids.Count} different models");
                if (lights > MostLights) Problem($"{site.Name} has {lights} lights");
                if (drawn > MostDrawn) Problem($"{site.Name} is {drawn} triangles as it stands");
                if (Signatures.TryGetValue(seat.Kind, out var signature))
                {
                    foreach ((PlacePiece piece, int least) in signature)
                    {
                        if (counted.GetValueOrDefault(piece) < least) Problem($"{site.Name} has {counted.GetValueOrDefault(piece)} {piece}, and wants {least}");
                    }
                }

                // Its tyrant, alone in the middle of its yard.
                SiteSpawn[] tyrants = site.Spawns.Where(s => s.Role == SpawnRole.Boss).ToArray();
                if (tyrants.Length != 1) Problem($"{site.Name} has {tyrants.Length} tyrants");
                else if ((tyrants[0].Position - seat.Heart).Length > 6f) Problem($"{site.Name}'s tyrant stands {(tyrants[0].Position - seat.Heart).Length:0.0} m from the middle of its yard");
                SiteSpawn[] inYard = site.Spawns.Where(s => s.Role.IsHostile() && s.Role != SpawnRole.Boss && (s.Position - seat.Heart).Length < seat.Yard - 1f).ToArray();
                if (inYard.Length > 0) Problem($"{inYard.Length} of {site.Name}'s garrison stand in the yard, which is the tyrant's: {string.Join("; ", inYard.Select(s => $"{s.Role} {(s.Position - seat.Heart).Length:0.0} m from its middle at {s.Position.X:0.0},{s.Position.Y:0.0}"))}");
                if (hostiles < LeastHostiles) Problem($"{site.Name} is held by only {hostiles}");
                if (!site.Spawns.Any(s => s.Role == SpawnRole.Chest)) Problem($"{site.Name} has no chest");

                // Its ways in: three, each walked straight through, and a road up to the great one.
                var gates = site.Place?.Gates;
                if (gates == null || gates.Count != 3) Problem($"{site.Name} has {gates?.Count ?? 0} ways in, and a seat has three");
                else
                {
                    for (int g = 0; g < gates.Count; g++)
                    {
                        (Vector2 at, Vector2 outward) = gates[g];
                        for (float t = -7f; t <= 7f; t += 1f)
                        {
                            if (world.Passable.IsSet(at + outward * t)) continue;
                            Problem($"{site.Name}'s way in {g} can't be walked through: it's shut {t:0} m out from its wall, at {(at + outward * t).X:0.0},{(at + outward * t).Y:0.0}");
                            break;
                        }
                    }
                    if (site.OnRoad)
                    {
                        (Vector2 great, Vector2 _) = gates[Math.Clamp(seat.GateSide, 0, 2)];
                        float nearest = world.Roads.SelectMany(road => road.Points).Min(point => (point.Xz - great).Length);
                        if (nearest > 6f) Problem($"{site.Name}'s road ends {nearest:0} m from its great gate");
                    }
                }
            }
            foreach ((string piece, var sum) in byPiece) Console.WriteLine($"{piece,-14} {sum.Models,4} models {sum.Triangles,9} triangles (most {sum.Most,6}) {sum.Lights,4} lights {sum.Ms,6} ms");
            Console.WriteLine(string.Join(", ", byKind.Select(k => $"{k.Value} {k.Key}")));
            if (seats == 0)
            {
                Console.WriteLine("PROBLEM: no seats in those lands");
                problems++;
            }
            Console.WriteLine(problems == 0 ? $"{seats} seats hold up." : $"{problems} problems.");
            return problems == 0 ? 0 : 1;
        }
    }
}
