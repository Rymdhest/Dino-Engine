using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace Dino_WorldLab
{
    /// <summary>
    /// Generates worlds without a window or a GPU, to look at them and to check that they hold up.
    ///   map     [--seed N] [--archetype id] [--out file.png] [--size pixels]   One world as a map.
    ///   chart   [--seed N] [--archetype id] [--out file.png] [--size pixels] [--walked share]  One world drawn as the game's map, and as it looks with only the ground within
    ///           that share of the walk from the landing seen (written beside it, as file.walked.png).
    ///   sheet   [--seed N] [--count N] [--archetype id] [--out file.png]       A contact sheet of many worlds.
    ///   plan    [--seed N] [--archetype id] [--site kind] [--nth N] [--span m] [--out f] A surveyor's plan of one site, the Nth of its kind.
    ///   check   [--seed N] [--count N] [--archetype id] [--layout linear|open] Generates worlds and validates them. Exit code 1 on any problem.
    ///   packs   [--seed N] [--archetype id] [--level N] [--out file.png]       Who stands where: the mix, and a map of it, each folk in its colour.
    ///   relief  [--seed N] [--count N] [--archetype id]                        What the land is made of, and where the rock stands highest.
    ///   view    --at x,z [--seed N] [--archetype id] [--span m] [--angle d] [--yaw d] [--out f]  A drawing of a place, for shaping land with no game.
    ///   skin    [--seed N] [--count N] [--archetype id] [--at x,z] [--radius m]  Grows the skin of the rock and says what it costs and whether its tiles meet.
    ///   wind                                                                   Puts the wind through what a game does to it. Exit code 1 on any problem.
    ///   halls                                                                  Builds every hall's models and says what they cost. Exit code 1 if one costs too much.
    ///   square                                                                 Builds every piece of the Haven's square and says what it costs. Exit code 1 on any problem.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string command = args.Length > 0 ? args[0] : "sheet";
            var options = new Options(args);

            switch (command)
            {
                case "map":
                    return Map(options);
                case "chart":
                    return ChartOf(options);
                case "where":
                    return Where(options);
                case "sheet":
                    return Sheet(options);
                case "plan":
                    return Plan(options);
                case "check":
                    return Check(options);
                case "packs":
                    return Packs(options);
                case "relief":
                    return ReliefCheck.Survey(options.Int("seed", 1), options.Int("count", ArchetypeCatalog.All.Count), options.Text("archetype"));
                case "view":
                    return View(options);
                case "probe":
                    string[] probe = (options.Text("at") ?? "1000,1000").Split(',');
                    return ReliefCheck.Probe(options.Int("seed", 1), options.Text("archetype"), new Vector2(float.Parse(probe[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(probe[1], System.Globalization.CultureInfo.InvariantCulture)), options.Int("larger", 0));
                case "skin":
                    Vector2? around = null;
                    if (options.Text("at") is string spot)
                    {
                        string[] xz = spot.Split(',');
                        around = new Vector2(float.Parse(xz[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(xz[1], System.Globalization.CultureInfo.InvariantCulture));
                    }
                    if (options.Text("from") is string a && options.Text("to") is string b)
                    {
                        float[] fa = a.Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                        float[] fb = b.Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                        return SkinCheck.Probe(options.Int("seed", 1), options.Text("archetype"), new Vector3(fa[0], fa[1], fa[2]), new Vector3(fb[0], fb[1], fb[2]), options.Int("steps", 40));
                    }
                    return SkinCheck.Run(options.Int("seed", 1), options.Int("count", 1), options.Text("archetype"), around, options.Int("radius", 80));
                case "wind":
                    return WindCheck.Run();
                case "halls":
                    return HallCheck.Run();
                case "square":
                    return SquareCheck.Run();
                case "arenas":
                    if (options.Text("land") is string land) return ArenaCheck.Keep(options.Int("seed", 1), land);
                    return ArenaCheck.Run(options.Int("seed", 1), options.Text("archetype"));
                case "weather":
                    if (options.Text("out") is string path) return WeatherCheck.Map(options.Int("seed", 1), options.Text("archetype"), path, options.Int("clock", 0), options.Int("every", 4), options.Int("maps", 5));
                    return WeatherCheck.Run(options.Int("count", 3));
                default:
                    Console.Error.WriteLine($"Unknown command '{command}'.");
                    return 2;
            }
        }

        private static int View(Options options)
        {
            string[] at = (options.Text("at") ?? "1000,1000").Split(',');
            Vector2 place = new Vector2(float.Parse(at[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(at[1], System.Globalization.CultureInfo.InvariantCulture));
            return ReliefCheck.View(options.Int("seed", 1), options.Text("archetype"), place, options.Int("span", 160), options.Int("angle", 67), options.Int("yaw", 0), options.Int("width", 1100), options.Int("height", 700), options.Text("out") ?? "view.png", options.Text("plain") == "true");
        }

        // Where things are in a world, for pointing the game at them (--at x,z): every site with the way it builds, and every object whose model id starts with --what.
        private static int Where(Options options)
        {
            int seed = SeedFor(options);
            WorldData world = new WorldGenerator().Generate(new WorldRequest(seed, options.Text("archetype")));
            Describe(world, 0);
            foreach (WorldSite site in world.Sites)
            {
                Console.WriteLine($"  {site.Kind,-14} {site.Name,-24} at {site.Position.X:0},{site.Position.Y:0}  {world.Plan.Biomes[site.Biome].Name}, built in {world.StyleOf(site)}");
            }
            foreach (WorldPond pond in world.Ponds) Console.WriteLine($"  Pond           {pond.Area,5:0} m2, level {pond.Level:0.0} m          at {pond.Centre.X:0},{pond.Centre.Y:0}, {pond.HalfExtents.X * 2f:0} by {pond.HalfExtents.Y * 2f:0} m across");
            string? what = options.Text("what");
            Vector2? near = null;
            if (options.Text("near") is string spot)
            {
                string[] parts = spot.Split(',');
                near = new Vector2(float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
                what ??= "";
                Console.WriteLine($"  at {near.Value.X:0},{near.Value.Y:0}: {world.BiomeAt(near.Value).Name}, height {world.Height.Sample(near.Value):0.0} m, slope {world.Elevation.SlopeAt(near.Value):0.00}, {(world.Passable.IsSet(near.Value) ? "passable" : "not passable")}");
            }
            if (what == null) return 0;
            float within = options.Int("within", 20);
            int shown = 0, found = 0;
            foreach (WorldObject item in world.Objects.All())
            {
                string id = world.Objects.AssetIds[item.Asset];
                if (!id.StartsWith(what, StringComparison.Ordinal)) continue;
                if (near is Vector2 centre && (item.Position.Xz - centre).Length > within) continue;
                found++;
                if (shown++ < options.Int("most", 40)) Console.WriteLine($"  {id} at {item.Position.X:0.0},{item.Position.Z:0.0}");
            }
            Console.WriteLine($"{found} objects starting '{what}'.");
            return 0;
        }

        private static int Map(Options options)
        {
            int seed = SeedFor(options);
            var clock = Stopwatch.StartNew();
            WorldData world = new WorldGenerator().Generate(new WorldRequest(seed, options.Text("archetype")));
            clock.Stop();
            Describe(world, clock.Elapsed.TotalSeconds);

            int resolution = options.Int("size", 1024);
            string path = options.Text("out") ?? $"world_{seed}.png";
            PngWriter.Write(path, WorldMapImage.Render(world, resolution), resolution, resolution);
            Console.WriteLine($"Wrote {path}");
            return 0;
        }

        private static int ChartOf(Options options)
        {
            int seed = SeedFor(options);
            WorldData world = new WorldGenerator().Generate(new WorldRequest(seed, options.Text("archetype"), options.Int("level", 1)));
            Describe(world, 0);

            int resolution = options.Int("size", 2048);
            var clock = Stopwatch.StartNew();
            Dino_Engine.World.Chart.Chart chart = Dino_Engine.World.Chart.WorldChart.Draw(world, resolution);
            Console.WriteLine($"Drew the chart in {clock.Elapsed.TotalSeconds:0.00} s, {chart.Labels.Count} regions named: {string.Join(", ", chart.Labels.Select(label => label.Text))}");
            string path = options.Text("out") ?? $"chart_{seed}.png";
            PngWriter.Write(path, chart.Colour, resolution, resolution);
            Console.WriteLine($"Wrote {path}");

            // As a hero sees it who's walked the roads that far from the landing.
            float walked = float.Parse(options.Text("walked") ?? "0.45", System.Globalization.CultureInfo.InvariantCulture);
            var seen = new Dino_Engine.World.Chart.ChartDiscovery(world.Size);
            foreach (WorldRoad road in world.Roads)
            {
                foreach (Vector3 point in road.Points)
                {
                    if (world.Remoteness == null || world.Remoteness.Sample(point.Xz) <= walked) seen.Reveal(point.Xz);
                }
            }
            seen.Reveal(world.Sites[0].Landing);
            clock.Restart();
            byte[] fog = (byte[])chart.Sketch.Clone();
            Dino_Engine.World.Chart.ChartFog.Compose(chart, seen, fog, 0, 0, resolution - 1, resolution - 1);
            Console.WriteLine($"Laid the fog in {clock.Elapsed.TotalMilliseconds:0} ms");
            byte[] shown = new byte[fog.Length];
            for (int i = 0; i < fog.Length; i += 4)
            {
                float a = fog[i + 3] / 255f;
                for (int c = 0; c < 3; c++) shown[i + c] = (byte)(chart.Colour[i + c] * (1f - a) + fog[i + c] * a + 0.5f);
                shown[i + 3] = 255;
            }
            string walkedPath = Path.ChangeExtension(path, ".walked.png");
            PngWriter.Write(walkedPath, shown, resolution, resolution);
            Console.WriteLine($"Wrote {walkedPath}");
            return 0;
        }

        private static int Sheet(Options options)
        {
            int firstSeed = options.Int("seed", 1);
            string? archetype = options.Text("archetype");
            int count = options.Int("count", archetype == null ? ArchetypeCatalog.All.Count : 12);
            int tile = options.Int("tile", 384);
            int columns = (int)MathF.Ceiling(MathF.Sqrt(count));
            int rows = (count + columns - 1) / columns;
            int width = columns * tile;
            int height = rows * tile;
            byte[] sheet = new byte[width * height * 4];

            for (int i = 0; i < count; i++)
            {
                int seed = firstSeed + i;
                // Without a chosen archetype the sheet shows one of each, so a run covers every kind of world.
                string id = archetype ?? ArchetypeCatalog.All[i % ArchetypeCatalog.All.Count].Id;
                var clock = Stopwatch.StartNew();
                WorldData world = new WorldGenerator().Generate(new WorldRequest(seed, id));
                clock.Stop();
                Describe(world, clock.Elapsed.TotalSeconds);

                byte[] image = WorldMapImage.Render(world, tile);
                int originX = (i % columns) * tile;
                int originY = (i / columns) * tile;
                for (int y = 0; y < tile; y++)
                {
                    Buffer.BlockCopy(image, y * tile * 4, sheet, ((originY + y) * width + originX) * 4, tile * 4);
                }
            }

            string path = options.Text("out") ?? "worlds.png";
            PngWriter.Write(path, sheet, width, height);
            Console.WriteLine($"Wrote {path}");
            return 0;
        }

        private static int Plan(Options options)
        {
            int seed = options.Int("seed", 1);
            WorldData world = new WorldGenerator().Generate(new WorldRequest(seed, options.Text("archetype")));
            Describe(world, 0);

            string kind = options.Text("site") ?? "Village";
            WorldSite site = world.Sites.Where(s => s.Kind.ToString() == kind).Skip(options.Int("nth", 0)).FirstOrDefault() ?? world.Sites[0];
            float span = options.Int("span", (int)(site.Radius * 3.4f));
            int resolution = options.Int("size", 1200);
            Console.WriteLine($"{site.Definition.Label} {site.Name}: r {site.Radius:0} at {site.Position}, {site.Spawns.Count} spawns");

            var plan = new SitePlan(world, site.Position, span, resolution);
            plan.Draw();
            string path = options.Text("out") ?? "plan.png";
            PngWriter.Write(path, plan.Pixels, resolution, resolution);
            return 0;
        }

        private static int Packs(Options options)
        {
            int seed = options.Int("seed", 1);
            WorldData world = new WorldGenerator().Generate(new WorldRequest(seed, options.Text("archetype"), options.Int("level", 1)));
            Describe(world, 0);

            foreach (var kind in world.Haunts.GroupBy(h => h.Haunt).OrderBy(g => g.Key))
            {
                string folk = string.Join(" ", kind.GroupBy(h => h.Folk).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}"));
                Console.WriteLine($"{kind.Key,-7} {kind.Count(),4}  average {kind.Average(h => h.Spawns.Count(s => s.Role.IsHostile())):0.0}, most {kind.Max(h => h.Spawns.Count(s => s.Role.IsHostile()))}  {folk}");
            }
            foreach (var folk in world.Haunts.GroupBy(h => h.Folk).OrderBy(g => g.Key))
            {
                WorldSite first = folk.OrderBy(h => (h.Position - world.Sites[0].Position).Length).First();
                Console.WriteLine($"Nearest {folk.Key} to the landing: {first.Haunt} of {first.Spawns.Count} at {first.Position.X:0},{first.Position.Y:0}");
            }

            foreach (WorldSite camp in world.Haunts.Where(h => h.Haunt == HauntKind.Camp).Take(8)) Console.WriteLine($"Camp of {camp.Folk}, {camp.Spawns.Count} there, at {camp.Position.X:0},{camp.Position.Y:0}");

            int resolution = options.Int("size", 1400);
            byte[] image = WorldMapImage.Render(world, resolution);
            float scale = resolution / world.Size;
            void Dot(Vector2 at, float radius, byte r, byte g, byte b)
            {
                int cx = (int)(at.X * scale);
                int cy = resolution - 1 - (int)(at.Y * scale);
                int reach = (int)MathF.Ceiling(radius);
                for (int y = -reach; y <= reach; y++)
                {
                    for (int x = -reach; x <= reach; x++)
                    {
                        if (x * x + y * y > radius * radius || cx + x < 0 || cy + y < 0 || cx + x >= resolution || cy + y >= resolution) continue;
                        int i = ((cy + y) * resolution + cx + x) * 4;
                        image[i] = r;
                        image[i + 1] = g;
                        image[i + 2] = b;
                        image[i + 3] = 255;
                    }
                }
            }

            foreach (WorldSite haunt in world.Haunts)
            {
                (byte r, byte g, byte b) = haunt.Folk switch
                {
                    SpawnRole.Undead => ((byte)190, (byte)120, (byte)255),
                    SpawnRole.Beast => ((byte)255, (byte)170, (byte)40),
                    SpawnRole.Cultist => ((byte)40, (byte)230, (byte)230),
                    _ => ((byte)255, (byte)50, (byte)50),
                };
                if (haunt.Haunt == HauntKind.Camp) Dot(haunt.Position, 6f, 255, 255, 255);
                foreach (SiteSpawn spawn in haunt.Spawns) Dot(spawn.Position, 1.6f, r, g, b);
            }
            foreach (WorldSite site in world.Sites)
            {
                foreach (SiteSpawn spawn in site.Spawns)
                {
                    if (spawn.Role.IsHostile()) Dot(spawn.Position, 1.6f, 255, 255, 120);
                }
            }

            string path = options.Text("out") ?? "packs.png";
            PngWriter.Write(path, image, resolution, resolution);
            Console.WriteLine($"Wrote {path}");
            return 0;
        }

        // Whether a request's land is laid out as --layout asks: "linear" for one way through, "open" for open country, or anything at all.
        private static bool LaidOut(Options options, in WorldRequest request) => options.Text("layout") switch
        {
            "linear" => WorldPlan.Create(request).Linear,
            "open" => !WorldPlan.Create(request).Linear,
            _ => true,
        };

        // The first seed from --seed on whose land is laid out as --layout asks.
        private static int SeedFor(Options options)
        {
            int seed = options.Int("seed", 1);
            while (!LaidOut(options, new WorldRequest(seed, options.Text("archetype")))) seed++;
            return seed;
        }

        private static int Check(Options options)
        {
            int firstSeed = options.Int("seed", 1);
            int count = options.Int("count", 20);
            string? archetype = options.Text("archetype");
            int failures = 0;
            float longestBare = 0f;
            float steepest = 0f;
            var clock = Stopwatch.StartNew();

            for (int i = 0, skipped = 0; i < count; i++)
            {
                int seed = firstSeed + i + skipped;
                string id = archetype ?? ArchetypeCatalog.All[i % ArchetypeCatalog.All.Count].Id;
                var request = new WorldRequest(seed, id, options.Int("level", 1), options.Int("people", 0) / 100f, options.Int("packs", 0) / 100f);
                if (!LaidOut(options, request))
                {
                    skipped++;
                    i--;
                    continue;
                }
                WorldData world;
                try
                {
                    world = new WorldGenerator().Generate(request);
                }
                catch (Exception e)
                {
                    failures++;
                    Console.WriteLine($"[{seed}] {id}: generation threw {e.GetType().Name}: {e.Message}");
                    Console.WriteLine(e.StackTrace);
                    continue;
                }

                var validator = new WorldValidator();
                validator.Check(world);
                longestBare = MathF.Max(longestBare, validator.LongestBareStretch);
                steepest = MathF.Max(steepest, validator.SteepestRoad);

                // Every tenth world is built twice: a seed has to mean the same world every time.
                if (i % 10 == 0 && Fingerprint(world) != Fingerprint(new WorldGenerator().Generate(request)))
                {
                    validator.Problems.Add("Same seed gave a different world");
                }

                if (validator.Problems.Count == 0) continue;
                failures++;
                Console.WriteLine($"[{seed}] {world.Plan.Name} ({id}): {validator.Problems.Count} problems");
                foreach (string problem in validator.Problems.Take(6)) Console.WriteLine($"    {problem}");
            }

            Console.WriteLine($"{count - failures}/{count} worlds passed in {clock.Elapsed.TotalSeconds:0.0} s. " +
                $"Longest bare road {longestBare:0} m, steepest road {steepest:0.00}.");
            return failures == 0 ? 0 : 1;
        }

        // Everything a seed decides, boiled down to one number.
        private static ulong Fingerprint(WorldData world)
        {
            ulong hash = 14695981039346656037UL;
            void Add(float value)
            {
                hash ^= BitConverter.SingleToUInt32Bits(value);
                hash *= 1099511628211UL;
            }

            foreach (WorldSite site in world.Sites)
            {
                Add(site.Position.X);
                Add(site.Position.Y);
                Add(site.Radius);
                Add(site.Spawns.Count);
            }
            foreach (WorldSite haunt in world.Haunts)
            {
                Add((int)haunt.Folk);
                foreach (SiteSpawn spawn in haunt.Spawns)
                {
                    Add(spawn.Position.X);
                    Add(spawn.Position.Y);
                }
            }
            foreach (WorldRoad road in world.Roads)
            {
                foreach (Vector3 point in road.Points) Add(point.Y);
            }
            foreach (WorldObject item in world.Objects.All())
            {
                Add(item.Asset);
                Add(item.Position.X);
                Add(item.Position.Y);
                Add(item.Position.Z);
                Add(item.Scale.X);
            }
            for (int i = 0; i < world.Height.Data.Length; i += 1013) Add(world.Height.Data[i]);
            return hash;
        }

        private static void Describe(WorldData world, double seconds)
        {
            WorldPlan plan = world.Plan;
            var biomes = string.Join(", ", plan.Biomes.Select((b, i) => $"{b.Name} {plan.BiomeShares[i]:P0}"));
            float roadKm = world.Roads.Sum(r => r.Length) / 1000f;
            var sites = string.Join(" ", world.Sites.GroupBy(s => s.Kind).Select(g => $"{g.Key}:{g.Count()}"));
            // Each site builds as its land decides, or as the world's people do: name every way this world's settled places build.
            var settled = world.Sites.Where(s => s.Kind is SiteKind.Village or SiteKind.Hamlet or SiteKind.Farmstead or SiteKind.FishingHamlet or SiteKind.LumberCamp or SiteKind.Watchtower);
            string built = string.Join("/", settled.Select(world.StyleOf).Distinct().DefaultIfEmpty(world.Style));
            Console.WriteLine($"[{plan.Seed}] {plan.Name} ({plan.Archetype.Id}, {plan.Size:0} m, {seconds:0.00} s, {world.ReachableArea / 1e6f:0.00} km2, {roadKm:0.0} km road, built in {built}): {biomes}");
            Console.WriteLine($"      {sites}");
            if (plan.Path is LandPath path)
            {
                Console.WriteLine($"      one way through: {path.Rooms.Count} clearings over {path.Length:0} m, from {path.First.Centre.X:0},{path.First.Centre.Y:0} to {path.Last.Centre.X:0},{path.Last.Centre.Y:0}");
                Console.WriteLine("      " + string.Join("  ", path.Rooms.Select((room, i) => $"{room.Centre.X:0},{room.Centre.Y:0} r{room.Radius:0}{(world.Reachable.Sample(room.Centre) > 0.5f ? "" : " (closed)")}")));
            }
            Console.WriteLine($"      {world.Objects.Count} objects, {world.Placement.Count} footprints, {world.Ponds.Count} ponds | " +
                string.Join(" ", world.Timings.Select(t => $"{t.stage.Replace("Stage", "")}:{t.milliseconds:0}")));
        }

        private sealed class Options
        {
            private readonly Dictionary<string, string> _values = new();

            public Options(string[] args)
            {
                for (int i = 1; i + 1 < args.Length; i += 2)
                {
                    _values[args[i].TrimStart('-')] = args[i + 1];
                }
            }

            public string? Text(string name) => _values.TryGetValue(name, out string? value) ? value : null;
            public int Int(string name, int fallback) => _values.TryGetValue(name, out string? value) ? int.Parse(value) : fallback;
        }
    }
}
