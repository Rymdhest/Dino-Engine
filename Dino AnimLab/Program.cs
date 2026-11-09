using Dino_Engine.Animation;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_AnimLab
{
    /// <summary>
    /// Moves bodies with no window and no GPU, to look at them and to check that they hold up. See ANIMATION.md.
    ///   sheet  [--body b] [--do script] [--view side|front|back|top|three|game] [--frames N] [--columns N] [--tile N] [--metres m] [--look m] [--up m] [--out file.png]   --look and --up move what's framed forward and up.
    ///   zoo    [--species a,b] [--variants N] [--as living,rot,bones,reich] [--view v] [--tile N] [--out file.png]   Dinosaurs standing, one of each.
    ///   grow   [--dino dino:raptor.0,dino:tyrant.bones.0]   Grows each on one thread and says where the time went.
    ///   check  Does everything hold, whatever's thrown at it? Exit code 1 if not.
    ///   bench  What a body costs.
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
                case "zoo":
                    return Zoo(options);
                case "grow":
                    return Grow(options);
                case "check":
                    return AnimCheck.Run(options);
                case "bench":
                    return AnimCheck.Bench();
                default:
                    Console.WriteLine($"No command called {command}.");
                    return 1;
            }
        }

        // How much of the world a picture of this body needs, and where its middle is.
        private static (float metres, Vector3 middle) Framing(Body body)
        {
            if (!body.Rig.Saurian) return (7.5f, new Vector3(0f, 2.4f, 0f));
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            foreach (Vector3 p in body.Rig.Pivots)
            {
                min = Vector3.ComponentMin(min, p);
                max = Vector3.ComponentMax(max, p);
            }
            min = Vector3.ComponentMin(min, body.Rig.Frame!.TailEnd);
            max = Vector3.ComponentMax(max, body.Rig.Frame.HeadTip);
            Vector3 size = max - min;
            return (MathF.Max(size.Z, size.Y) * 1.15f, new Vector3(0f, (min.Y + max.Y) * 0.5f, (min.Z + max.Z) * 0.5f));
        }

        /// <summary>Grows each dinosaur named on one thread and says where the time went, which is what a loading screen waits for.</summary>
        private static int Grow(Options options)
        {
            Dino_Engine.Modelling.Procedural.Saurian.SaurianSkin.Report = Console.WriteLine;
            foreach (string key in options.Text("dino", "dino:raptor.0,dino:tyrant.0,dino:raptor.bones.0").Split(','))
            {
                var spec = Dino_Engine.Modelling.Procedural.Saurian.SaurianSpec.Parse(key);
                if (spec == null)
                {
                    Console.WriteLine($"No dinosaur called {key}.");
                    return 1;
                }
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var mesh = Dino_Engine.Modelling.Procedural.Saurian.SaurianForge.Make(spec.Value);
                Console.WriteLine($"{key}: {watch.Elapsed.TotalMilliseconds:0} ms, {mesh.faces.Count} triangles");
            }
            return 0;
        }

        /// <summary>Every kind asked for, a few of each, standing still after a moment to settle, so that what's different between them can be seen at once.</summary>
        private static int Zoo(Options options)
        {
            string[] species = options.Text("species", string.Join(",", Dino_Engine.Modelling.Procedural.Saurian.SaurianCatalog.All.Select(s => s.Id))).Split(',');
            int variants = options.Int("variants", 3);
            string[] ways = options.Text("as", "living").Split(',');
            int tile = options.Int("tile", 360);
            PoseView view = PoseView.Named(options.Text("view", "three"));
            string scriptName = options.Text("do", "idle");
            const int shrink = 2;

            var bodies = new List<string>();
            foreach (string kind in species)
            {
                foreach (string way in ways)
                {
                    for (int v = 0; v < variants; v++) bodies.Add(way == "living" ? $"dino:{kind}.{v}" : $"dino:{kind}.{way}.{v}");
                }
            }

            int columns = options.Int("columns", variants * ways.Length);
            int rows = (bodies.Count + columns - 1) / columns;
            var sheet = new byte[columns * tile * rows * tile * 4];
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var built = new (Body body, PosableMesh mesh, int triangles)[bodies.Count];
            Parallel.For(0, bodies.Count, i =>
            {
                Body body = Body.Named(bodies[i]);
                var mesh = body.Build();
                built[i] = (body, new PosableMesh(mesh), mesh.faces.Count);
            });
            Console.WriteLine($"Grew {bodies.Count} in {watch.Elapsed.TotalSeconds:0.0} s");

            for (int i = 0; i < built.Length; i++)
            {
                (Body body, PosableMesh mesh, int triangles) = built[i];
                Script script = Script.Named(scriptName) ?? Script.Named("idle")!;
                CharacterAnimator animator = body.NewAnimator(i * 17 + 3);
                const float step = 1f / 60f;
                float time = 0f;
                while (time < script.Warmup + script.Length * 0.5f)
                {
                    time += step;
                    (Vector3 position, float yaw) = script.Path(time);
                    var input = AnimationInput.At(position, yaw);
                    input.Alert = script.Alert;
                    if (script.Action != null) input.Action = script.Action(time);
                    animator.Step(input, step, script.Ground);
                }
                (Vector3 at, float facing) = script.Path(time);
                mesh.Pose(animator.Skinning, Matrix4.CreateFromQuaternion(Frames.Yaw(facing)) * Matrix4.CreateTranslation(at));

                (float metres, Vector3 middle) = Framing(body);
                var render = new PoseRender(tile * shrink, tile * shrink);
                render.Clear(new Vector3(0.05f, 0.052f, 0.06f));
                float pixels = tile * shrink / metres;
                Vector3 centre = at + Vector3.Transform(middle, Frames.Yaw(facing));
                render.Floor((x, z) => 0f, centre, metres * 0.6f, MathF.Max(1f, MathF.Round(metres / 12f)), view, pixels);
                render.Draw(mesh, view, centre, pixels);
                render.Blit(sheet, columns * tile, i % columns * tile, i / columns * tile, shrink);
                Console.WriteLine($"  {bodies[i],-26} {triangles,6} triangles, {mesh.Positions.Length,6} vertices, {metres / 1.25f:0.0} m long");
            }

            string path = options.Text("out", "zoo.png");
            PngWriter.Write(path, sheet, columns * tile, rows * tile);
            Console.WriteLine($"Wrote {path}");
            return 0;
        }

        private static int Sheet(Options options)
        {
            Body body = Body.Named(options.Text("body", "sword"));
            string scriptName = options.Text("do", "run");
            Script? script = Script.Named(scriptName);
            if (script == null)
            {
                Console.WriteLine($"No such thing to do as {scriptName}.");
                return 1;
            }

            script.Length = options.Float("span", script.Length);
            int frames = options.Int("frames", 12);
            int columns = options.Int("columns", Math.Min(frames, 6));
            int rows = (frames + columns - 1) / columns;
            int tile = options.Int("tile", 300);
            (float fitted, Vector3 middle) = Framing(body);
            float metres = options.Float("metres", fitted);
            PoseView view = PoseView.Named(options.Text("view", "side"));
            const int shrink = 2;

            var mesh = new PosableMesh(body.Build());
            var render = new PoseRender(tile * shrink, tile * shrink);
            var sheet = new byte[columns * tile * rows * tile * 4];
            CharacterAnimator animator = body.NewAnimator();

            const float step = 1f / 60f;
            float time = 0f;
            float end = script.Warmup + script.Length;
            int shot = 0;
            var trail = new List<Vector3>();
            float next = script.Warmup;
            float pixels = tile * shrink / metres;

            while (shot < frames && time <= end + step * 2f)
            {
                time += step;
                (Vector3 position, float yaw) = script.Path(time);
                var input = AnimationInput.At(position, yaw);
                input.Alert = script.Alert;
                input.Dazed = script.Dazed;
                if (script.Action != null) input.Action = script.Action(time);
                if (script.DiesAt >= 0f && time >= script.DiesAt && !animator.Dead) animator.Die(script.Push);
                animator.Step(input, step, script.Ground);

                if (animator.WeaponTip != Vector3.Zero) trail.Add(animator.WeaponTip);
                if (trail.Count > 30) trail.RemoveAt(0);
                if (time < next) continue;
                next += script.Length / frames;

                Matrix4 world = Matrix4.CreateFromQuaternion(Frames.Yaw(yaw)) * Matrix4.CreateTranslation(position);
                mesh.Pose(animator.Skinning, world);

                Vector3 centre = position + Vector3.Transform(middle, Frames.Yaw(yaw)) + new Vector3(0f, options.Float("up", 0f), options.Float("look", 0f));
                render.Clear(new Vector3(0.05f, 0.052f, 0.06f));
                render.Floor((x, z) => script.Ground.HeightAt(x, z), centre, metres * 1.1f, MathF.Max(1f, MathF.Round(metres / 10f)), view, pixels);
                render.Draw(mesh, view, centre, pixels);

                for (int i = 1; i < trail.Count; i++) render.Line(trail[i - 1], trail[i], new Vector3(1f, 0.75f, 0.3f) * (i / (float)trail.Count), view, centre, pixels, 2f);
                foreach (Foot foot in animator.Gait.Feet)
                {
                    if (foot.Swinging) continue;
                    render.Line(foot.At + new Vector3(-0.3f, 0.02f, -0.3f), foot.At + new Vector3(0.3f, 0.02f, 0.3f), new Vector3(0.1f, 0.9f, 0.3f), view, centre, pixels, 3f);
                }

                render.Blit(sheet, columns * tile, shot % columns * tile, shot / columns * tile, shrink);
                shot++;
            }

            string path = options.Text("out", $"anim-{body.Name}-{scriptName}.png");
            PngWriter.Write(path, sheet, columns * tile, rows * tile);
            Console.WriteLine($"Wrote {path}");
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
