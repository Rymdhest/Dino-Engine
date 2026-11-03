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
    ///   sheet   [--seed N] [--count N] [--archetype id] [--out file.png]       A contact sheet of many worlds.
    ///   plan    [--seed N] [--archetype id] [--site kind] [--span m] [--out f] A surveyor's plan of one site.
    ///   check   [--seed N] [--count N] [--archetype id]                        Generates worlds and validates them. Exit code 1 on any problem.
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
                case "sheet":
                    return Sheet(options);
                case "plan":
                    return Plan(options);
                case "check":
                    return Check(options);
                default:
                    Console.Error.WriteLine($"Unknown command '{command}'.");
                    return 2;
            }
        }

        private static int Map(Options options)
        {
            int seed = options.Int("seed", 1);
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
            WorldSite site = world.Sites.FirstOrDefault(s => s.Kind.ToString() == kind) ?? world.Sites[0];
            float span = options.Int("span", (int)(site.Radius * 3.4f));
            int resolution = options.Int("size", 1200);
            Console.WriteLine($"{site.Definition.Label} {site.Name}: r {site.Radius:0} at {site.Position}, {site.Spawns.Count} spawns");

            var plan = new SitePlan(world, site.Position, span, resolution);
            plan.Draw();
            string path = options.Text("out") ?? "plan.png";
            PngWriter.Write(path, plan.Pixels, resolution, resolution);
            return 0;
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

            for (int i = 0; i < count; i++)
            {
                int seed = firstSeed + i;
                string id = archetype ?? ArchetypeCatalog.All[i % ArchetypeCatalog.All.Count].Id;
                var request = new WorldRequest(seed, id);
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
            Console.WriteLine($"[{plan.Seed}] {plan.Name} ({plan.Archetype.Id}, {plan.Size:0} m, {seconds:0.00} s, {world.ReachableArea / 1e6f:0.00} km2, {roadKm:0.0} km road): {biomes}");
            Console.WriteLine($"      {sites}");
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
