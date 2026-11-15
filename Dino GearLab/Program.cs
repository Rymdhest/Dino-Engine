using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace Dino_GearLab
{
    /// <summary>
    /// Makes gear with no window and no GPU, to look at it and to check that it holds up. See GEAR.md.
    ///   sheet   [--kind k] [--count N] [--seed N] [--grade N] [--heavy s] [--leather s] [--cloth s] [--size s] [--slender s] [--curve s] [--cover s] [--fancy s] [--worn s] [--theme t] [--out file.png]
    ///   check   [--kind k] [--count N]   Is every one valid? Exit code 1 if not.
    ///   variety [--kind k] [--count N]   Are they really different? Exit code 1 if not.
    ///   bench                            What one costs to make.
    ///   fit     [--count N]              Does every piece of armour keep to its bands (GearFit)? Exit code 1 if not.
    ///   looks   [--kind k] [--count N]   No Reich's colours, and every kind's legendary pieces far more splendid than its plain ones? Exit code 1 if not.
    ///   one     [--kind k] [--index N]   One of check's pieces taken apart, for a fault it found.
    ///   describe [--kind k] [--seed N] [--count N] [--stride N]   What a look chose, in words.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string command = args.Length > 0 ? args[0] : "sheet";
            var options = new Options(args);
            switch (command)
            {
                case "sheet":
                    return Sheet(options);
                case "check":
                    return GearCheck.Run(options);
                case "variety":
                    return GearVariety.Run(options);
                case "dress":
                    return Dress(options);
                case "icons":
                    return Icons(options);
                case "bench":
                    return Bench();
                case "describe":
                    return Describe(options);
                case "fit":
                    return GearFitCheck.Run(options);
                case "one":
                    return One(options);
                case "looks":
                    return GearLookCheck.Run(options);
                default:
                    Console.WriteLine($"No command called {command}.");
                    return 1;
            }
        }

        /// <summary>A drawn view of something standing up: leaning over, turned a little, and from slightly above, as an icon would be.</summary>
        public static Matrix3 View(GearKind kind, float turn = 0.5f)
        {
            float lean = GearDisplay.IsHeld(kind) && kind is not (GearKind.Bow or GearKind.OffHand) ? -0.72f : 0f;
            return Matrix3.CreateRotationZ(lean) * Matrix3.CreateRotationY(turn) * Matrix3.CreateRotationX(kind is GearKind.OpenBook or GearKind.Page or GearKind.Sentence ? -0.8f : 0.28f);
        }

        public static GearSpec Spec(Options options, ulong seed, GearKind kind)
        {
            float heavy = options.Float("heavy", 0f), leather = options.Float("leather", 0f), cloth = options.Float("cloth", 0f);
            var form = new GearForm(options.Float("size", 0.5f), options.Float("slender", 0.5f), options.Float("curve", 0.2f), options.Float("cover", 0.5f), options.Float("fancy", 0.4f), options.Float("worn", 0.2f));
            var theme = Enum.Parse<GearTheme>(options.Text("theme", "None"), ignoreCase: true);
            return new GearSpec(seed, kind, heavy, leather, cloth, form, options.Int("grade", 2), theme, Level: options.Float("level", 0.5f), Pages: options.Int("pages", 0));
        }

        private static int Sheet(Options options)
        {
            var kind = Enum.Parse<GearKind>(options.Text("kind", "Sword"), ignoreCase: true);
            int count = options.Int("count", 48);
            int seed = options.Int("seed", 1);
            int columns = options.Int("columns", (int)MathF.Ceiling(MathF.Sqrt(count * 1.6f)));
            int rows = (count + columns - 1) / columns;
            int tile = options.Int("tile", 200);
            const int shrink = 2;

            var sheet = new byte[columns * tile * rows * tile * 4];
            var render = new SoftRender(tile * shrink, tile * shrink);
            Vector3 background = new Vector3(0.05f, 0.045f, 0.04f);
            for (int i = 0; i < count; i++)
            {
                Mesh mesh = GearForge.Make(Spec(options, (ulong)(seed * 100000 + i + 1), kind)).Build(bones: false);
                render.Draw(mesh, View(kind, options.Float("turn", 0.5f)));
                render.Blit(sheet, columns * tile, i % columns * tile, i / columns * tile, shrink, background);
            }

            string path = options.Text("out", $"gear-{kind}.png");
            PngWriter.Write(path, sheet, columns * tile, rows * tile);
            Console.WriteLine($"Wrote {path}");
            return 0;
        }

        // A whole person in a whole family's gear, as heroes are what's really seen.
        /// <summary>Things as the game pictures them: each as it lies on the ground (GearDisplay.Lying), turned as the studio turns it (GearDisplay.PictureOf), seen from in front.</summary>
        private static int Icons(Options options)
        {
            var kind = Enum.Parse<GearKind>(options.Text("kind", "Hands"), ignoreCase: true);
            int count = options.Int("count", 16);
            int seed = options.Int("seed", 1);
            // A ladder of books, as --ladder true: each row one book, bound for one page to the most along it, so that only how many it holds changes along a row.
            bool ladder = options.Text("ladder", "false") == "true";
            int columns = ladder ? BookGenerator.MostPages : options.Int("columns", (int)MathF.Ceiling(MathF.Sqrt(count * 1.6f)));
            int rows = (count + columns - 1) / columns;
            int tile = options.Int("tile", 160);
            const int shrink = 2;
            GearDisplay.Angle angle = GearDisplay.PictureOf(kind);
            angle = new GearDisplay.Angle(options.Float("rise", angle.Rise), options.Float("yaw", angle.Yaw), options.Float("pitch", angle.Pitch), options.Float("roll", angle.Roll));
            Matrix4 turn = Matrix4.CreateRotationX(angle.Rise) * Matrix4.CreateRotationY(angle.Yaw) * Matrix4.CreateRotationX(angle.Pitch) * Matrix4.CreateRotationZ(angle.Roll);

            var sheet = new byte[columns * tile * rows * tile * 4];
            var render = new SoftRender(tile * shrink, tile * shrink);
            Vector3 background = new Vector3(0.05f, 0.045f, 0.04f);
            for (int i = 0; i < count; i++)
            {
                GearSpec spec = ladder ? Spec(options, (ulong)(seed * 100000 + i / columns + 1), kind) with { Pages = i % columns + 1 } : Spec(options, (ulong)(seed * 100000 + i + 1), kind);
                GearMesh lying = GearDisplay.Lying(GearForge.Make(spec), kind);
                lying.Transform(turn);
                render.Draw(lying.Build(bones: false), Matrix3.Identity);
                render.Blit(sheet, columns * tile, i % columns * tile, i / columns * tile, shrink, background);
            }

            string path = options.Text("out", $"icons-{kind}.png");
            PngWriter.Write(path, sheet, columns * tile, rows * tile);
            Console.WriteLine($"Wrote {path}");
            return 0;
        }

        private static int Dress(Options options)
        {
            int count = options.Int("count", 24);
            int seed = options.Int("seed", 1);
            // A ladder, as --levels 0,0.5,1: each body once a level across, and the next below, so that only what the climb does changes along a row.
            float[] levels = options.Text("levels", "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(text => float.Parse(text, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            if (levels.Length > 0) count *= levels.Length;
            int columns = levels.Length > 0 ? levels.Length : options.Int("columns", 8);
            int rows = (count + columns - 1) / columns;
            int width = options.Int("tile", 220);
            int height = (int)(width * 1.25f);
            const int shrink = 2;

            var sheet = new byte[columns * width * rows * height * 4];
            var render = new SoftRender(width * shrink, height * shrink);
            Vector3 background = new Vector3(0.05f, 0.045f, 0.04f);
            float heavy = options.Float("heavy", 0f), leather = options.Float("leather", 0f), cloth = options.Float("cloth", 0f);
            // What's left off, as --without Body,Head, and a necklace put on with --neck true.
            var without = options.Text("without", "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(name => Enum.Parse<GearKind>(name, ignoreCase: true)).ToHashSet();
            var kinds = new List<GearKind> { GearKind.Head, GearKind.Body, GearKind.Legs, GearKind.Feet, GearKind.Hands };
            if (options.Text("neck", "false") == "true") kinds.Add(GearKind.Neck);

            for (int i = 0; i < count; i++)
            {
                int body = levels.Length > 0 ? i / levels.Length : i;
                float level = levels.Length > 0 ? levels[i % levels.Length] : options.Float("level", 0.5f);
                var worn = new List<(GearSpec, float)>();
                foreach (GearKind kind in kinds)
                {
                    if (without.Contains(kind)) continue;
                    var form = new GearForm(options.Float("size", 0.55f), options.Float("slender", 0.45f), options.Float("curve", 0.3f), options.Float("cover", 0.5f), options.Float("fancy", 0.45f), options.Float("worn", 0.15f));
                    worn.Add((new GearSpec((ulong)(seed * 100000 + body * 16 + (int)kind + 1), kind, heavy, leather, cloth, form, options.Int("grade", 2), GearTheme.None, Level: level), -1f));
                }
                Mesh dressed = Dino_Engine.Modelling.Procedural.Character.Outfit.Dress(new Dino_Engine.Modelling.Procedural.Character.HumanoidLook(), worn);
                render.Draw(dressed, Matrix3.CreateRotationY(options.Float("turn", 0.35f)) * Matrix3.CreateRotationX(options.Float("look", 0.18f)));
                render.Blit(sheet, columns * width, i % columns * width, i / columns * height, shrink, background);
            }

            string path = options.Text("out", "gear-dressed.png");
            PngWriter.Write(path, sheet, columns * width, rows * height);
            Console.WriteLine($"Wrote {path}");
            return 0;
        }

        // One of check's pieces, by its place in the run, taken apart: what it is, its parts and where each one's vertices are, and every triangle with no area, for a fault check found.
        private static int One(Options options)
        {
            var kind = Enum.Parse<GearKind>(options.Text("kind", "Body"), ignoreCase: true);
            int index = options.Int("index", 0);
            GearSpec spec = GearCheck.Sample(kind, (ulong)index * 2654435761UL + 1, index);
            var look = new GearLook(spec);
            Console.WriteLine($"{kind} {index}: seed {spec.Seed}, heavy {spec.Heavy:0.00} leather {spec.Leather:0.00} cloth {spec.Cloth:0.00}, grade {spec.Grade}, theme {spec.Theme}, {spec.Form}, make {GearForge.MakeOf(spec)}, craft {look.Craft:0.00}, motif {look.Motif}");
            GearMesh made = GearForge.Make(spec, new Dictionary<string, int>());
            foreach ((string name, int from, int to) in made.Parts ?? new()) Console.WriteLine($"  {name,-12} vertices {from}..{to}");
            Mesh mesh = made.Build(false);
            foreach (Face face in mesh.faces)
            {
                Vector3 cross = Vector3.Cross(face.B.position - face.A.position, face.C.position - face.A.position);
                if (cross.LengthSquared >= 1e-14f) continue;
                int at = -1;
                for (int v = 0; v < made.VertexCount && at < 0; v++) if (made.Vertices[v].position == face.A.position) at = v;
                Console.WriteLine($"  no area at vertex {at} ({GearFitCheck.PartOf(made, at)}): {face.A.position}, {face.B.position}, {face.C.position}");
            }
            return 0;
        }

        // What a look chose, in words: how it's made and what it's coloured with, for working out why a sheet looks as it does.
        private static int Describe(Options options)
        {
            var kind = Enum.Parse<GearKind>(options.Text("kind", "Body"), ignoreCase: true);
            int count = options.Int("count", 12);
            for (int i = 0; i < count; i++)
            {
                ulong seed = (ulong)options.Int("seed", 1) + (ulong)i * (ulong)options.Int("stride", 1);
                GearSpec spec = Spec(options, seed, kind);
                var look = new GearLook(spec);
                GearColours c = look.Colours;
                Console.WriteLine($"{seed,6} {GearForge.MakeOf(spec),-10} craft {look.Craft:0.00} ornament {look.Ornament:0.00} wear {look.Wear:0.00} motif {look.Motif,-6} metal {c.MetalName}, fittings {c.FittingName}, leather {c.LeatherName}, dye {c.DyeName}, trim {c.TrimName}, wood {c.WoodName}, gem {c.GemName}");
            }
            return 0;
        }

        private static int Bench()
        {
            foreach (GearKind kind in Enum.GetValues<GearKind>())
            {
                for (int i = 0; i < 40; i++) GearForge.Make(GearCheck.Sample(kind, (ulong)i + 1, i)).Build(false);

                int count = 300;
                int triangles = 0;
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                {
                    GearMesh mesh = GearForge.Make(GearCheck.Sample(kind, (ulong)i + 1000, i));
                    mesh.Build(false);
                    triangles += mesh.TriangleCount;
                }
                watch.Stop();
                Console.WriteLine($"{kind,-10} {watch.Elapsed.TotalMilliseconds / count,6:0.00} ms to make, {triangles / count,6} triangles on average");
            }
            return 0;
        }
    }

    /// <summary>--name value pairs.</summary>
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
