using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Rock;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace Dino_FloraLab
{
    /// <summary>
    /// Makes stones with no window and no GPU, to look at them and to check that they hold up.
    ///   rocks     [--kinds a,b] [--forms a,b] [--seed N] [--tile px] [--angle degrees] [--out file.png]   Every form of every kind of rock, side on and from the game's own height.
    ///   rockcheck [--count N]                                                                             Is every stone sound and within budget, and are two of a kind really different? Exit code 1 if not.
    /// </summary>
    internal static class RockCheck
    {
        private const float BudgetSlack = 1.4f;     // A budget's what's aimed for. Now and then a stone's cracks meet where nothing can be taken away.
        private const long Slowest = 1500;          // Milliseconds, with every core at it at once, as they are while a world loads.
        private const float LeastApart = 0.05f;     // How far, typically, a stone is from the one most like it.

        private static IEnumerable<T> Chosen<T>(Options options, string name) where T : struct, Enum
        {
            if (!options.Has(name)) return Enum.GetValues<T>();
            return options.Text(name, "").Split(',').Select(text => Enum.Parse<T>(text, ignoreCase: true));
        }

        private static Vector3 ColourOf(RockKind kind) => kind switch
        {
            RockKind.Granite => new Vector3(0.42f, 0.4f, 0.39f),
            RockKind.Limestone => new Vector3(0.55f, 0.52f, 0.46f),
            RockKind.Sandstone => new Vector3(0.52f, 0.31f, 0.18f),
            RockKind.Schist => new Vector3(0.28f, 0.29f, 0.28f),
            _ => new Vector3(0.17f, 0.17f, 0.2f),
        };

        public static int Sheet(Options options)
        {
            RockKind[] kinds = Chosen<RockKind>(options, "kinds").ToArray();
            RockForm[] forms = Chosen<RockForm>(options, "forms").ToArray();
            int tile = options.Int("tile", 360);
            int seed = options.Int("seed", 1);
            float low = MathHelper.DegreesToRadians(options.Float("angle", 14f));
            float high = MathHelper.DegreesToRadians(67f);
            const int Shrink = 2;

            int columns = forms.Length * 2;
            int rows = kinds.Length;
            var sheet = new byte[columns * tile * rows * tile * 4];
            var jobs = new List<(int row, int column, RockKind kind, RockForm form)>();
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < forms.Length; column++) jobs.Add((row, column, kinds[row], forms[column]));
            }

            Parallel.ForEach(jobs, job =>
            {
                FloraMesh mesh = RockForge.Forge(job.form, job.kind, (ulong)(seed * 7919 + job.column * 31 + job.row));
                for (int i = 0; i < mesh.Vertices.Count; i++)
                {
                    FloraVertex vertex = mesh.Vertices[i];
                    vertex.Colour *= ColourOf(job.kind);
                    mesh.Vertices[i] = vertex;
                }
                var preview = new Preview(tile * Shrink, tile * Shrink, null!);
                preview.Draw(mesh, low, 0.6f);
                preview.Blit(sheet, columns * tile, job.column * 2 * tile, job.row * tile, Shrink);
                preview.Draw(mesh, high, 0.6f);
                preview.Blit(sheet, columns * tile, (job.column * 2 + 1) * tile, job.row * tile, Shrink);
            });

            string path = options.Text("out", "rocks.png");
            PngWriter.Write(path, sheet, columns * tile, rows * tile);
            Console.WriteLine($"Wrote {path}");
            return 0;
        }

        public static int Run(Options options)
        {
            int count = options.Int("count", 8);
            int failures = 0;
            Console.WriteLine($"{"stone",-20} {"tris",6} {"budget",6} {"verts",6} {"ms",6} {"apart",6}");
            foreach (RockKind kind in Enum.GetValues<RockKind>())
            {
                foreach (RockForm form in Enum.GetValues<RockForm>())
                {
                    var meshes = new FloraMesh[count];
                    long[] times = new long[count];
                    Parallel.For(0, count, i =>
                    {
                        var clock = Stopwatch.StartNew();
                        meshes[i] = RockForge.Forge(form, kind, (ulong)(i * 104729 + 17));
                        times[i] = clock.ElapsedMilliseconds;
                    });

                    int budget = RockForge.BudgetOf(form);
                    int most = meshes.Max(m => m.TriangleCount);
                    float apart = Difference(meshes);
                    string problem = "";
                    if (most > budget * BudgetSlack) problem += $" over budget";
                    if (meshes.Any(m => m.TriangleCount < budget * 0.3f)) problem += " far under budget: something's gone missing";
                    if (meshes.Any(HasBadNumbers)) problem += " not a number in it";
                    if (meshes.Any(m => FaceSomewhereElse(m) > 0.02f)) problem += " normals point away from their faces";
                    if (apart < LeastApart) problem += " they all look alike";
                    if (times.Max() > Slowest) problem += " too slow";
                    if (problem.Length > 0) failures++;

                    Console.WriteLine($"{$"{kind} {form}",-20} {most,6} {budget,6} {meshes.Max(m => m.VertexCount),6} {times.Max(),6} {apart,6:0.00}{problem}");
                }
            }
            MeasureLoad();
            Console.WriteLine(failures == 0 ? "All stones sound." : $"{failures} kinds of stone failed.");
            return failures == 0 ? 0 : 1;
        }

        // What a world's stones add to the wait for it, made the way WorldAssetLibrary.Grow makes them.
        private static void MeasureLoad()
        {
            var models = new List<(RockForm form, ulong seed)>();
            foreach (RockForm form in Enum.GetValues<RockForm>())
            {
                WorldAsset asset = NatureCatalog.RockOf(RockKind.Sandstone, form);
                foreach (string id in NatureCatalog.ModelsOf(asset, 11))
                {
                    NatureCatalog.SpeciesOf(id, out ulong seed);
                    models.Add((form, seed));
                }
            }

            var clock = Stopwatch.StartNew();
            Parallel.ForEach(models, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount - 1, 1) }, model => RockForge.Forge(model.form, RockKind.Sandstone, model.seed));
            Console.WriteLine($"A world's {models.Count} stones take {clock.ElapsedMilliseconds} ms.");
        }

        private static bool HasBadNumbers(FloraMesh mesh)
        {
            foreach (FloraVertex v in mesh.Vertices)
            {
                if (!float.IsFinite(v.Position.X + v.Position.Y + v.Position.Z) || !float.IsFinite(v.Normal.X + v.Normal.Y + v.Normal.Z)) return true;
                if (MathF.Abs(v.Normal.Length - 1f) > 0.01f) return true;
            }
            return false;
        }

        // A share of the faces whose corners disagree with the way the face itself looks, which is what an inside-out rock or a fold looks like.
        private static float FaceSomewhereElse(FloraMesh mesh)
        {
            int wrong = 0;
            for (int i = 0; i < mesh.Indices.Count; i += 3)
            {
                FloraVertex a = mesh.Vertices[mesh.Indices[i]], b = mesh.Vertices[mesh.Indices[i + 1]], c = mesh.Vertices[mesh.Indices[i + 2]];
                Vector3 face = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);
                if (Vector3.Dot(face, a.Normal + b.Normal + c.Normal) < 0f) wrong++;
            }
            return wrong / (float)Math.Max(mesh.TriangleCount, 1);
        }

        // How different stones of a kind are: how far each is from the one most like it, by its proportions and how its surface faces, as a median. The single closest pair says nothing, as it gets closer the more stones there are.
        private static float Difference(FloraMesh[] meshes)
        {
            var signatures = meshes.Select(Signature).ToArray();
            var nearest = new List<float>();
            for (int i = 0; i < signatures.Length; i++)
            {
                float closest = float.MaxValue;
                for (int j = 0; j < signatures.Length; j++)
                {
                    if (i == j) continue;
                    float sum = 0f;
                    for (int k = 0; k < signatures[i].Length; k++) sum += MathF.Abs(signatures[i][k] - signatures[j][k]);
                    closest = MathF.Min(closest, sum);
                }
                nearest.Add(closest);
            }
            nearest.Sort();
            return nearest[nearest.Count / 2];
        }

        private static float[] Signature(FloraMesh mesh)
        {
            mesh.Bounds(out Vector3 min, out Vector3 max);
            Vector3 size = max - min;
            float scale = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
            float[] facing = new float[6];
            foreach (FloraVertex v in mesh.Vertices)
            {
                facing[v.Normal.X > 0f ? 0 : 1] += MathF.Abs(v.Normal.X);
                facing[v.Normal.Y > 0f ? 2 : 3] += MathF.Abs(v.Normal.Y);
                facing[v.Normal.Z > 0f ? 4 : 5] += MathF.Abs(v.Normal.Z);
            }
            float total = facing.Sum();
            return facing.Select(f => f / total).Concat(new[] { size.X / scale, size.Y / scale, size.Z / scale }).ToArray();
        }
    }
}
