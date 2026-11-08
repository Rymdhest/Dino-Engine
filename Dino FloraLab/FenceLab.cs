using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Util;
using Dino_Engine.World;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace Dino_FloraLab
{
    /// <summary>
    /// Builds fences with no window and no GPU, laid out over uneven ground exactly as a world lays them, to look at them and to check that they hold up.
    ///   fences     [--kinds a,b] [--styles N | --style hex] [--seed N] [--tile px] [--span metres] [--focus x,z] [--out file.png]   Every kind round a corner, over a hill and through a gate, from the game's height and low down.
    ///   fencecheck [--count N]                                                                        Is every piece sound, within budget and no wider than what's solid; does every span meet its posts; are two styles really different? Exit code 1 if not.
    /// </summary>
    internal static class FenceLab
    {
        private const float MostLean = 2f;          // Degrees an upright of a span may lean by, on ground as rough as any a site is built on.
        private const float MostMiss = 0.02f;       // Metres a span's far end may be from the post it's meant to reach.
        private const float BodyHeight = 2.6f;      // Metres up that a fence's width is what stops a body, and has to be what's solid.
        private const float Overhang = 0.22f;       // Metres past what's solid that a coping or a nailed-on pale may reach.
        private const long Slowest = 60;            // Milliseconds for a piece, on one core.
        private const float LeastApart = 0.04f;     // How far, typically, one style's span is from the one most like it.

        // What each material's texture is, roughly, under the colour in its vertices: the lab has no textures, and this is what they'd multiply it by.
        private static readonly Dictionary<int, Vector3> Albedo = new()
        {
            [1] = new Vector3(0.77f, 0.75f, 0.73f),     // Weathered: bare wood, pale.
            [2] = new Vector3(0.74f, 0.73f, 0.71f),     // Bark.
            [3] = new Vector3(0.69f, 0.67f, 0.65f),     // Cane.
            [4] = new Vector3(0.32f, 0.31f, 0.3f),      // Stone: what a granite world's rock comes to.
            [5] = new Vector3(0.44f, 0.42f, 0.38f),     // Masonry.
            [6] = new Vector3(0.44f, 0.33f, 0.23f),     // Timber.
            [7] = new Vector3(0.59f, 0.55f, 0.47f),     // Plaster.
            [8] = new Vector3(0.44f, 0.41f, 0.38f),     // Roof.
            [9] = new Vector3(0.5f, 0.5f, 0.52f),       // Iron.
            [10] = new Vector3(0.42f, 0.34f, 0.2f),     // Cord.
            [11] = Vector3.One,                         // Ground.
        };

        private const int Ground = 11;

        private static void UseLabMaterials()
        {
            FenceMaterials.Weathered = 1;
            FenceMaterials.Bark = 2;
            FenceMaterials.Cane = 3;
            FenceMaterials.Stone = 4;
            FenceMaterials.Masonry = 5;
            FenceMaterials.Timber = 6;
            FenceMaterials.Plaster = 7;
            FenceMaterials.Roof = 8;
            FenceMaterials.Iron = 9;
            FenceMaterials.Cord = 10;
        }

        // A hillside with a fold across it, about as steep as anything a site is built on.
        private static float GroundAt(Vector2 p) => 5f + 1.4f * MathF.Sin(p.X * 0.21f) + 0.18f * p.Y + 0.5f * MathF.Sin(p.Y * 0.33f + p.X * 0.12f);

        // Runs that meet in line, turn a right angle, turn a shallower one, stop at a gate, and start again past it.
        private static List<FenceRun> Scene(FenceKind kind, ulong style)
        {
            return new List<FenceRun>
            {
                new(kind, style, new Vector2(-19f, -5f), new Vector2(-11f, -5f)),
                new(kind, style, new Vector2(-11f, -5f), new Vector2(-1f, -5f)),
                new(kind, style, new Vector2(-1f, -5f), new Vector2(-1f, 4f)),
                new(kind, style, new Vector2(-1f, 4f), new Vector2(6f, 6.5f)),
                new(kind, style, new Vector2(10.5f, 7.6f), new Vector2(15f, 8.6f)),
            };
        }

        private readonly record struct Placed(string Id, Vector3 At, Quaternion Turn, Vector3 Stretch);

        private static List<Placed> Lay(List<FenceRun> runs)
        {
            var placed = new List<Placed>();
            FenceLayout.Lay(runs, GroundAt, (id, at, turn, stretch) => placed.Add(new Placed(id, at, turn, stretch)));
            return placed;
        }

        public static int Sheet(Options options)
        {
            UseLabMaterials();
            FenceKind[] kinds = options.Has("kinds") ? options.Text("kinds", "").Split(',').Select(FenceCatalog.Get).ToArray() : FenceCatalog.All;
            int styles = options.Int("styles", 1);
            int seed = options.Int("seed", 1);
            int tile = options.Int("tile", 900);
            float span = options.Float("span", 34f);
            const int Shrink = 2;
            float high = MathHelper.DegreesToRadians(67f);
            float low = MathHelper.DegreesToRadians(options.Float("angle", 20f));
            Vector3? focus = null;
            if (options.Has("focus"))
            {
                float[] at = options.Text("focus", "0,0").Split(',').Select(t => float.Parse(t, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                focus = new Vector3(at[0], GroundAt(new Vector2(at[0], at[1])) + 1.5f, at[1]);
            }

            var jobs = new List<(int row, FenceKind kind, ulong style)>();
            foreach (FenceKind kind in kinds)
            {
                if (options.Has("style")) jobs.Add((jobs.Count, kind, Convert.ToUInt64(options.Text("style", "0"), 16)));
                else for (int s = 0; s < styles; s++) jobs.Add((jobs.Count, kind, FenceCatalog.StyleSeed(seed, kind, s)));
            }

            int columns = 2;
            var sheet = new byte[columns * tile * jobs.Count * tile * 4];
            Parallel.ForEach(jobs, job =>
            {
                var clock = Stopwatch.StartNew();
                List<Placed> placed = Lay(Scene(job.kind, job.style));
                FloraMesh scene = Assemble(placed, out int pieces, out int triangles);
                AddGround(scene, new Vector2(-2f, 2f), 46f);
                var preview = new Preview(tile * Shrink, tile * Shrink, null!) { GrainFloor = 0.82f, GrainDepth = 0.32f };
                preview.Draw(scene, high, 0f, span, focus);
                preview.Blit(sheet, columns * tile, 0, job.row * tile, Shrink);
                preview.Draw(scene, low, 0.35f, span * 0.8f, focus);
                preview.Blit(sheet, columns * tile, tile, job.row * tile, Shrink);
                Console.WriteLine($"{job.kind.Id,-12} {job.style:x6}  {placed.Count} placed, {pieces} models, {triangles} triangles, {clock.ElapsedMilliseconds} ms");
            });

            string path = options.Text("out", "fences.png");
            PngWriter.Write(path, sheet, columns * tile, jobs.Count * tile);
            Console.WriteLine($"Wrote {path}");
            return 0;
        }

        // Everything a layout placed, as one mesh, each model built once and moved where it stands.
        private static FloraMesh Assemble(List<Placed> placed, out int models, out int triangles)
        {
            var built = new Dictionary<string, FloraMesh>();
            var scene = new FloraMesh();
            triangles = 0;
            foreach (Placed item in placed)
            {
                if (!built.TryGetValue(item.Id, out FloraMesh? mesh))
                {
                    FenceSpec.TryParse(item.Id, out FenceSpec spec);
                    built[item.Id] = mesh = FenceForge.Make(spec);
                }
                triangles += mesh.TriangleCount;
                int first = scene.VertexCount;
                foreach (FloraVertex v in mesh.Vertices)
                {
                    FloraVertex moved = v;
                    moved.Position = item.At + Vector3.Transform(v.Position * item.Stretch, item.Turn);
                    moved.Normal = Vector3.Transform(v.Normal / item.Stretch, item.Turn).Normalized();
                    moved.Tangent = Vector3.Transform(v.Tangent * item.Stretch, item.Turn).Normalized();
                    moved.Colour = v.Colour * (Albedo.TryGetValue(v.Material, out Vector3 albedo) ? albedo : Vector3.One);
                    scene.Add(moved);
                }
                foreach (int index in mesh.Indices) scene.Indices.Add(first + index);
            }
            models = built.Count;
            return scene;
        }

        // The hillside itself, mottled earth and grass, so a fence can be seen standing on it.
        private static void AddGround(FloraMesh scene, Vector2 centre, float size)
        {
            const int Cells = 92;
            int first = scene.VertexCount;
            for (int j = 0; j <= Cells; j++)
            {
                for (int i = 0; i <= Cells; i++)
                {
                    Vector2 p = centre + new Vector2(i / (float)Cells - 0.5f, j / (float)Cells - 0.5f) * size;
                    float e = 0.05f;
                    Vector3 normal = new Vector3(GroundAt(p - new Vector2(e, 0f)) - GroundAt(p + new Vector2(e, 0f)), 2f * e, GroundAt(p - new Vector2(0f, e)) - GroundAt(p + new Vector2(0f, e))).Normalized();
                    float mottle = FloraNoise.Fbm(new Vector3(p.X * 0.35f, p.Y * 0.35f, 0.5f), 5u, 3);
                    Vector3 colour = Vector3.Lerp(new Vector3(0.26f, 0.22f, 0.16f), new Vector3(0.14f, 0.19f, 0.08f), FenceSmooth(0.4f, 0.65f, mottle));
                    scene.Add(new FloraVertex { Position = new Vector3(p.X, GroundAt(p), p.Y), Normal = normal, Tangent = Vector3.UnitX, Colour = colour, Material = Ground, Joint = -1 });
                }
            }
            for (int j = 0; j < Cells; j++)
            {
                for (int i = 0; i < Cells; i++)
                {
                    int a = first + j * (Cells + 1) + i;
                    scene.Triangle(a, a + Cells + 1, a + Cells + 2);
                    scene.Triangle(a, a + Cells + 2, a + 1);
                }
            }
        }

        private static float FenceSmooth(float a, float b, float x)
        {
            float t = Math.Clamp((x - a) / (b - a), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        public static int Check(Options options)
        {
            UseLabMaterials();
            int count = options.Int("count", 6);
            int failures = 0;
            int[] slopes = { -12, -4, 0, 3, 11 };
            Console.WriteLine($"{"kind",-12} {"span",6} {"post",6} {"end",6} {"ms",5} {"wide",6} {"lean",6} {"miss",6} {"apart",6}");
            foreach (FenceKind kind in FenceCatalog.All)
            {
                int spanMost = 0, postMost = 0, endMost = 0;
                long slowest = 0;
                float widest = 0f;
                string problem = "";
                var signatures = new List<float[]>();
                for (int s = 0; s < count; s++)
                {
                    ulong style = FenceCatalog.StyleSeed(9000 + s, kind, s % FenceCatalog.StylesPerWorld);
                    var pieces = new List<FenceSpec>();
                    if (kind.LinePosts) pieces.Add(new FenceSpec(kind, style, FencePiece.Post, 0, 0));
                    pieces.Add(new FenceSpec(kind, style, FencePiece.End, 0, 0));
                    foreach (int slope in slopes) pieces.Add(new FenceSpec(kind, style, FencePiece.Span, s % kind.SpanVariants, slope));

                    foreach (FenceSpec spec in pieces)
                    {
                        var clock = Stopwatch.StartNew();
                        FloraMesh mesh = FenceForge.Make(spec);
                        slowest = Math.Max(slowest, clock.ElapsedMilliseconds);
                        if (mesh.TriangleCount == 0) problem += $" {spec.Id} is empty";
                        if (HasBadNumbers(mesh)) problem += $" {spec.Id} has a bad number";
                        if (FaceSomewhereElse(mesh) > 0.02f) problem += $" {spec.Id} has normals away from its faces";
                        switch (spec.Piece)
                        {
                            case FencePiece.Span:
                                spanMost = Math.Max(spanMost, mesh.TriangleCount);
                                if (spec.Slope != 0) break;
                                float wide = WidthAtBody(mesh);
                                widest = MathF.Max(widest, wide);
                                if (wide > kind.HalfThickness + Overhang) problem += $" {spec.Id} is {wide:0.00} m wide where only {kind.HalfThickness:0.00} is solid";
                                signatures.Add(Signature(mesh));
                                break;
                            case FencePiece.Post: postMost = Math.Max(postMost, mesh.TriangleCount); break;
                            default: endMost = Math.Max(endMost, mesh.TriangleCount); break;
                        }
                    }
                }

                (float lean, float miss, int runs) = CheckLayout(kind, FenceCatalog.StyleSeed(77, kind, 0));
                if (lean > MostLean) problem += $" a span leans {lean:0.0} degrees";
                if (miss > MostMiss) problem += $" a span misses its post by {miss:0.000} m";
                if (runs > 0) problem += $" {runs} run ends with no post";
                float apart = Difference(signatures);
                if (apart < LeastApart) problem += " its styles all look alike";
                if (slowest > Slowest) problem += " too slow";
                if (spanMost > BudgetOf(kind) || postMost > 700 || endMost > 1600) problem += " over budget";
                if (problem.Length > 0) failures++;
                Console.WriteLine($"{kind.Id,-12} {spanMost,6} {postMost,6} {endMost,6} {slowest,5} {widest,6:0.00} {lean,6:0.00} {miss,6:0.000} {apart,6:0.00}{problem}");
            }
            MeasureLoad();
            Console.WriteLine(failures == 0 ? "All fences sound." : $"{failures} kinds of fence failed.");
            return failures == 0 ? 0 : 1;
        }

        // Triangles a span may have. A wall of stones is hundreds of stones; a field's fence is a few rails.
        private static int BudgetOf(FenceKind kind) => kind.Form switch
        {
            FenceForm.DryStone => 4200,
            FenceForm.Wattle => 2800,
            FenceForm.Bamboo or FenceForm.Stockade or FenceForm.Pen or FenceForm.Curtain => 2000,
            _ => 1400,
        };

        // Lays the scene out and asks of every span whether it reaches the post it's for, and how far its uprights lean; and of every run's ends whether a post stands there.
        private static (float lean, float miss, int bareEnds) CheckLayout(FenceKind kind, ulong style)
        {
            List<FenceRun> runs = Scene(kind, style);
            List<Placed> placed = Lay(runs);
            bool IsSpan(Placed p) => FenceSpec.TryParse(p.Id, out FenceSpec spec) && spec.Piece == FencePiece.Span;
            var posts = placed.Where(p => !IsSpan(p)).Select(p => p.At).ToList();
            float bay = FenceForge.BayOf(kind, style);
            float worstLean = 0f, worstMiss = 0f;
            foreach (Placed item in placed)
            {
                if (!FenceSpec.TryParse(item.Id, out FenceSpec spec) || spec.Piece != FencePiece.Span) continue;
                Vector3 up = Vector3.Transform(Vector3.UnitY, item.Turn);
                worstLean = MathF.Max(worstLean, MathHelper.RadiansToDegrees(MathF.Acos(Math.Clamp(up.Y, -1f, 1f))));
                Vector3 end = item.At + Vector3.Transform(new Vector3(bay, spec.Gradient * bay, 0f) * item.Stretch, item.Turn);

                // A wall's spans meet each other between posts; what they meet is the next span's foot.
                float nearest = float.MaxValue;
                foreach (Vector3 post in posts) nearest = MathF.Min(nearest, (post - end).Length);
                foreach (Placed other in placed)
                {
                    if (IsSpan(other)) nearest = MathF.Min(nearest, (other.At - end).Length);
                }
                worstMiss = MathF.Max(worstMiss, nearest);
            }

            // Every end has a post, but where a wall carries straight on into the next run, which needs none.
            int bare = 0;
            foreach (FenceRun run in runs)
            {
                foreach (Vector2 end in new[] { run.From, run.To })
                {
                    int meeting = runs.Count(other => (other.From - end).Length < 0.4f || (other.To - end).Length < 0.4f);
                    bool straightOn = meeting == 2 && !kind.LinePosts && MathF.Abs(end.Y + 5f) < 0.01f;
                    if (!straightOn && !posts.Any(p => (p.Xz - end).Length < 0.4f)) bare++;
                }
            }
            return (worstLean, worstMiss, bare);
        }

        // What a world's fences add to the wait for it: a few kinds, each in a style and a dozen slopes, as WorldAssetLibrary.Grow builds them.
        private static void MeasureLoad()
        {
            var specs = new List<FenceSpec>();
            foreach (FenceKind kind in new[] { FenceCatalog.Rails, FenceCatalog.Paling, FenceCatalog.Pen, FenceCatalog.Churchyard, FenceCatalog.Curtain, FenceCatalog.DryStone })
            {
                ulong style = FenceCatalog.StyleSeed(5, kind, 0);
                specs.Add(new FenceSpec(kind, style, FencePiece.End, 0, 0));
                if (kind.LinePosts) specs.Add(new FenceSpec(kind, style, FencePiece.Post, 0, 0));
                for (int slope = -6; slope <= 6; slope++)
                {
                    for (int variant = 0; variant < kind.SpanVariants; variant++) specs.Add(new FenceSpec(kind, style, FencePiece.Span, variant, slope));
                }
            }
            var clock = Stopwatch.StartNew();
            Parallel.ForEach(specs, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount - 1, 1) }, spec => FenceForge.Make(spec));
            Console.WriteLine($"A world's {specs.Count} fence models take {clock.ElapsedMilliseconds} ms.");
        }

        // How far out from its line a span reaches at the height a body is, which is what has to be solid.
        private static float WidthAtBody(FloraMesh mesh)
        {
            float widest = 0f;
            foreach (FloraVertex v in mesh.Vertices)
            {
                if (v.Position.Y < 0.05f || v.Position.Y > BodyHeight) continue;
                widest = MathF.Max(widest, MathF.Abs(v.Position.Z));
            }
            return widest;
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

        // What tells one style from another: how tall and thick a span is, how much there is to it, how its surface faces and what colour it is.
        private static float[] Signature(FloraMesh mesh)
        {
            mesh.Bounds(out Vector3 min, out Vector3 max);
            Vector3 size = max - min;
            float[] facing = new float[6];
            Vector3 colour = Vector3.Zero;
            foreach (FloraVertex v in mesh.Vertices)
            {
                facing[v.Normal.X > 0f ? 0 : 1] += MathF.Abs(v.Normal.X);
                facing[v.Normal.Y > 0f ? 2 : 3] += MathF.Abs(v.Normal.Y);
                facing[v.Normal.Z > 0f ? 4 : 5] += MathF.Abs(v.Normal.Z);
                colour += v.Colour;
            }
            float total = facing.Sum();
            colour /= Math.Max(mesh.VertexCount, 1);
            return facing.Select(f => f / total).Concat(new[] { size.Y / 8f, size.Z / 2f, mesh.TriangleCount / 3000f, colour.X, colour.Y, colour.Z }).ToArray();
        }

        private static float Difference(List<float[]> signatures)
        {
            if (signatures.Count < 2) return 1f;
            var nearest = new List<float>();
            for (int i = 0; i < signatures.Count; i++)
            {
                float closest = float.MaxValue;
                for (int j = 0; j < signatures.Count; j++)
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
    }
}
