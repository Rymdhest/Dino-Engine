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
                case "bench":
                    return Bench();
                default:
                    Console.WriteLine($"No command called {command}.");
                    return 1;
            }
        }

        /// <summary>A drawn view of something standing up: leaning over, turned a little, and from slightly above, as an icon would be.</summary>
        public static Matrix3 View(GearKind kind, float turn = 0.5f)
        {
            float lean = kind is GearKind.Sword or GearKind.Dagger or GearKind.Spear or GearKind.Staff or GearKind.Wand or GearKind.Axe or GearKind.Mace ? -0.72f : 0f;
            return Matrix3.CreateRotationZ(lean) * Matrix3.CreateRotationY(turn) * Matrix3.CreateRotationX(kind is GearKind.OpenBook or GearKind.Page or GearKind.Sentence ? -0.8f : 0.28f);
        }

        public static GearSpec Spec(Options options, ulong seed, GearKind kind)
        {
            float heavy = options.Float("heavy", 0f), leather = options.Float("leather", 0f), cloth = options.Float("cloth", 0f);
            var form = new GearForm(options.Float("size", 0.5f), options.Float("slender", 0.5f), options.Float("curve", 0.2f), options.Float("cover", 0.5f), options.Float("fancy", 0.4f), options.Float("worn", 0.2f));
            var theme = Enum.Parse<GearTheme>(options.Text("theme", "None"), ignoreCase: true);
            return new GearSpec(seed, kind, heavy, leather, cloth, form, options.Int("grade", 2), theme);
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
        private static int Dress(Options options)
        {
            int count = options.Int("count", 24);
            int seed = options.Int("seed", 1);
            int columns = options.Int("columns", 8);
            int rows = (count + columns - 1) / columns;
            int width = options.Int("tile", 220);
            int height = (int)(width * 1.25f);
            const int shrink = 2;

            var sheet = new byte[columns * width * rows * height * 4];
            var render = new SoftRender(width * shrink, height * shrink);
            Vector3 background = new Vector3(0.05f, 0.045f, 0.04f);
            float heavy = options.Float("heavy", 0f), leather = options.Float("leather", 0f), cloth = options.Float("cloth", 0f);

            for (int i = 0; i < count; i++)
            {
                var worn = new List<(GearSpec, float)>();
                foreach (GearKind kind in new[] { GearKind.Head, GearKind.Body, GearKind.Legs, GearKind.Feet, GearKind.Hands })
                {
                    var form = new GearForm(options.Float("size", 0.55f), options.Float("slender", 0.45f), options.Float("curve", 0.3f), options.Float("cover", 0.5f), options.Float("fancy", 0.45f), options.Float("worn", 0.15f));
                    worn.Add((new GearSpec((ulong)(seed * 100000 + i * 16 + (int)kind + 1), kind, heavy, leather, cloth, form, options.Int("grade", 2), GearTheme.None), -1f));
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
