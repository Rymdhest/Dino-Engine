using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace Dino_FloraLab
{
    /// <summary>
    /// Grows plants with no window and no GPU, to look at them and to check that they hold up. See FLORA.md.
    ///   sheet   [--plants a,b] [--count N] [--wide N] [--seed N] [--tile px] [--angle degrees] [--top true] [--out file.png]   Real plants with real cards, from the side and from above.
    ///           [--span metres] [--focus height]                                                                      A close look: so many metres across, centred so high up.
    ///   cards   [--cards a,b] [--out file.png]                                                                        Every card there is, on a checked ground.
    ///   list                                                                                                          Every plant, with what it costs.
    ///   check   [--plants a,b] [--count N]                                                                            Is every plant at every seed sound, and within budget? Exit code 1 if not.
    ///   variety [--plants a,b] [--count N]                                                                            Are two plants of a species really different? Exit code 1 if not.
    ///   find                                                                                                          A seed and an archetype for every biome, to look at it with --biome.
    ///   bend    [--plants a,b] [--count N]                                                                            Bends every plant as hard as it can go. Exit code 1 if one comes apart.
    ///   fingerprint [--plants a,b] [--count N] [--out file.txt]                                                       A hash of every plant at N seeds, to tell which species a change moved.
    ///   rocks, rockcheck                                                                                              The same for stones: see RockCheck.
    ///   fences, fencecheck                                                                                            The same for fences and walls: see FenceLab.
    ///   houses                                                                                                        Every building of a way of building: see HouseLab.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string command = args.Length > 0 ? args[0] : "sheet";
            var options = new Options(args);
            FloraMaterials.FirstCard = Preview.FirstCard;

            switch (command)
            {
                case "sheet":
                    return Sheet(options);
                case "cards":
                    return Cards(options);
                case "list":
                    return List(options);
                case "bend":
                    return BendCheck.Run(options);
                case "find":
                    return Find();
                case "fingerprint":
                    return Fingerprint(options);
                case "check":
                    return FloraCheck.Run(options);
                case "variety":
                    return FloraVariety.Run(options);
                case "rocks":
                    return RockCheck.Sheet(options);
                case "rockcheck":
                    return RockCheck.Run(options);
                case "fences":
                    return FenceLab.Sheet(options);
                case "fencecheck":
                    return FenceLab.Check(options);
                case "houses":
                    return HouseLab.Sheet(options);
                default:
                    Console.WriteLine($"No command called {command}.");
                    return 1;
            }
        }

        public static List<PlantSpecies> Chosen(Options options)
        {
            if (!options.Has("plants")) return FloraCatalog.Species.ToList();
            return options.Text("plants", "").Split(',').Select(FloraCatalog.Get).ToList();
        }

        private static int Sheet(Options options)
        {
            var clock = Stopwatch.StartNew();
            BakedCards cards = CardLibrary.Bake();
            Console.WriteLine($"{cards.Layers} layers of cards painted in {clock.ElapsedMilliseconds} ms");

            List<PlantSpecies> species = Chosen(options);
            int count = options.Int("count", 3);
            int seed = options.Int("seed", 1);
            int tile = options.Int("tile", 420);
            int shrink = 2;
            float low = MathHelper.DegreesToRadians(options.Float("angle", 8f));
            float high = MathHelper.DegreesToRadians(67f);
            // A close look: so many metres across the picture, centred so high up the plant.
            float? span = options.Has("span") ? options.Float("span", 8f) : null;
            Vector3? focus = options.Has("focus") ? new Vector3(0f, options.Float("focus", 0f), 0f) : null;
            bool top = options.Text("top", "true") == "true";

            // Every plant gets its views side by side, and they run across a sheet as many to a row as --wide says.
            int views = top ? 2 : 1;
            var jobs = new List<(PlantSpecies plant, int seed)>();
            foreach (PlantSpecies plant in species)
            {
                for (int c = 0; c < count; c++) jobs.Add((plant, seed + c * 7919));
            }
            int wide = options.Int("wide", count);
            int columns = wide * views;
            int rows = (jobs.Count + wide - 1) / wide;
            var sheet = new byte[columns * tile * rows * tile * 4];

            Parallel.For(0, jobs.Count, i =>
            {
                (PlantSpecies plant, int plantSeed) = jobs[i];
                var grown = Stopwatch.StartNew();
                FloraMesh mesh = FloraForge.Grow(plant, (ulong)plantSeed);
                long ms = grown.ElapsedMilliseconds;
                var preview = new Preview(tile * shrink, tile * shrink, cards);
                int atX = i % wide * views * tile, atY = i / wide * tile;
                preview.Draw(mesh, low, 0.6f, span, focus);
                preview.Blit(sheet, columns * tile, atX, atY, shrink);
                if (top)
                {
                    preview.Draw(mesh, high, 0.6f, span, focus);
                    preview.Blit(sheet, columns * tile, atX + tile, atY, shrink);
                }
            });

            string path = options.Text("out", "flora.png");
            PngWriter.Write(path, sheet, columns * tile, rows * tile);
            Console.WriteLine($"Wrote {path} in {clock.ElapsedMilliseconds} ms");
            return 0;
        }

        private static int Cards(Options options)
        {
            BakedCards baked = CardLibrary.Bake();
            IEnumerable<CardForm> chosen = options.Has("cards")
                ? options.Text("cards", "").Split(',').Select(id => CardLibrary.All.First(card => card.Id == id))
                : CardLibrary.All;
            List<CardForm> cards = chosen.ToList();

            int tile = options.Int("tile", 256);
            int columns = CardLibrary.CellsPerLayer;
            int rows = cards.Count;
            var sheet = new byte[columns * tile * rows * tile * 4];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    CardImage image = baked.Image(cards[r].Layer, c);
                    int level = 0;
                    while ((image.Size >> level) > tile) level++;
                    int size = image.Size >> level;
                    CardLevel data = image.Levels[level];
                    for (int y = 0; y < tile; y++)
                    {
                        for (int x = 0; x < tile; x++)
                        {
                            int sx = x * size / tile, sy = (tile - 1 - y) * size / tile;
                            int at = sy * size + sx;
                            float alpha = data.Read(at, 3) >= CardImage.Cutoff ? 1f : 0f;
                            bool check = ((x / 16) + (y / 16)) % 2 == 0;
                            float ground = check ? 0.32f : 0.26f;
                            int to = ((r * tile + y) * columns * tile + c * tile + x) * 4;
                            for (int i = 0; i < 3; i++) sheet[to + i] = (byte)(Math.Clamp(MathHelper.Lerp(ground, data.Read(at, i), alpha), 0f, 1f) * 255f);
                            sheet[to + 3] = 255;
                        }
                    }
                }
            }
            string path = options.Text("out", "cards.png");
            PngWriter.Write(path, sheet, columns * tile, rows * tile);
            Console.WriteLine($"Wrote {path}");
            return 0;
        }

        private static int Find()
        {
            var found = new Dictionary<string, (int seed, string archetype)>();
            foreach (WorldArchetype archetype in ArchetypeCatalog.All)
            {
                for (int seed = 1; seed < 200; seed++)
                {
                    foreach (BiomeDefinition biome in WorldPlan.Create(new WorldRequest(seed, archetype.Id, 1)).Biomes) found.TryAdd(biome.Id, (seed, archetype.Id));
                }
            }
            foreach (BiomeDefinition biome in BiomeCatalog.All) Console.WriteLine(found.TryGetValue(biome.Id, out var where) ? $"{biome.Id} {where.seed} {where.archetype}" : $"{biome.Id} none");
            return 0;
        }

        // Every species at a few seeds, each as one hash of everything its meshes hold, a line each: two runs either side of a change, compared, are the species it moved. Cards
        // are painted once for every plant, so a change to one shows only in its own line, and a plant names its cards by id and not by layer, as a new card moves every
        // layer after it.
        private static int Fingerprint(Options options)
        {
            int count = options.Int("count", 3);
            var lines = new List<string>();
            BakedCards cards = CardLibrary.Bake();
            uint IdHash(string id)
            {
                uint hash = 2166136261u;
                foreach (char c in id) hash = (hash ^ c) * 16777619u;
                return hash;
            }
            foreach (CardForm card in CardLibrary.All)
            {
                ulong hash = 14695981039346656037UL;
                for (int cell = 0; cell < CardLibrary.CellsPerLayer; cell++)
                {
                    CardLevel level = cards.Image(card.Layer, cell).Levels[0];
                    foreach (byte b in level.Albedo) hash = (hash ^ b) * 1099511628211UL;
                    foreach (byte b in level.Normal) hash = (hash ^ b) * 1099511628211UL;
                    foreach (byte b in level.Material) hash = (hash ^ b) * 1099511628211UL;
                }
                lines.Add($"card {card.Id,-24} {hash:x16}");
            }
            foreach (PlantSpecies species in Chosen(options))
            {
                ulong hash = 14695981039346656037UL;
                void MixBits(uint bits) => hash = (hash ^ bits) * 1099511628211UL;
                void Mix(float value) => MixBits(BitConverter.SingleToUInt32Bits(value));
                void MixVector(Vector3 v) { Mix(v.X); Mix(v.Y); Mix(v.Z); }
                for (int i = 0; i < count; i++)
                {
                    FloraMesh mesh = FloraForge.Grow(species, (ulong)(i * 104729 + 3));
                    foreach (FloraVertex vertex in mesh.Vertices)
                    {
                        MixVector(vertex.Position);
                        MixVector(vertex.Normal);
                        MixVector(vertex.Tangent);
                        MixVector(vertex.Colour);
                        Mix(vertex.UV.X);
                        Mix(vertex.UV.Y);
                        int card = vertex.Material - Preview.FirstCard;
                        if (card >= 0 && card < CardLibrary.All.Count) MixBits(IdHash(CardLibrary.All[card].Id));
                        else Mix(vertex.Material);
                        Mix(vertex.Joint);
                        Mix(vertex.Shell);
                    }
                    foreach (int index in mesh.Indices) Mix(index);
                    foreach (Dino_Engine.Modelling.Model.PlantJoint joint in mesh.Joints)
                    {
                        MixVector(joint.Base);
                        MixVector(joint.Tip);
                        Mix(joint.Parent);
                    }
                }
                lines.Add($"{species.Id,-29} {hash:x16}");
            }
            foreach (string line in lines) Console.WriteLine(line);
            if (options.Has("out")) File.WriteAllLines(options.Text("out", "fingerprint.txt"), lines);
            return 0;
        }

        private static int List(Options options)
        {
            Console.WriteLine($"{"",-24}{"verts",8}{"tris",8}{"cards",8}{"joints",8}{"depth",7}{"ms",7}");
            foreach (PlantSpecies species in Chosen(options))
            {
                var clock = Stopwatch.StartNew();
                FloraMesh mesh = FloraForge.Grow(species, 1);
                long ms = clock.ElapsedMilliseconds;
                int depth = 0;
                for (int i = 0; i < mesh.Joints.Count; i++) depth = Math.Max(depth, Dino_Engine.Modelling.Model.PlantRig.Depth(mesh.Joints, i));
                // Of its triangles, how many are cards: the rest is wood.
                int cards = 0;
                for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
                {
                    if (mesh.Vertices[mesh.Indices[i]].Material >= Preview.FirstCard) cards++;
                }
                Console.WriteLine($"{species.Id,-24}{mesh.VertexCount,8}{mesh.TriangleCount,8}{cards,8}{mesh.Joints.Count,8}{depth,7}{ms,7}");
            }
            return 0;
        }
    }

    internal sealed class Options
    {
        private readonly Dictionary<string, string> _values = new();

        public Options(string[] args)
        {
            for (int i = 1; i + 1 < args.Length; i += 2)
            {
                if (args[i].StartsWith("--")) _values[args[i].Substring(2)] = args[i + 1];
            }
        }

        public bool Has(string name) => _values.ContainsKey(name);

        public string Text(string name, string fallback) => _values.GetValueOrDefault(name, fallback);

        public int Int(string name, int fallback) => _values.TryGetValue(name, out string? value) ? int.Parse(value) : fallback;

        public float Float(string name, float fallback) => _values.TryGetValue(name, out string? value) ? float.Parse(value, System.Globalization.CultureInfo.InvariantCulture) : fallback;
    }
}
