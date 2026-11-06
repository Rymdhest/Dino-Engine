using Dino_Engine.Animation;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_AnimLab
{
    /// <summary>
    /// Moves bodies with no window and no GPU, to look at them and to check that they hold up. See ANIMATION.md.
    ///   sheet  [--body b] [--do script] [--view side|front|back|top|three|game] [--frames N] [--columns N] [--tile N] [--out file.png]
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
                case "check":
                    return AnimCheck.Run(options);
                case "bench":
                    return AnimCheck.Bench();
                default:
                    Console.WriteLine($"No command called {command}.");
                    return 1;
            }
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
            float metres = options.Float("metres", 7.5f);
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

                Vector3 centre = new Vector3(position.X, position.Y + 2.4f, position.Z + options.Float("look", 0f));
                render.Clear(new Vector3(0.05f, 0.052f, 0.06f));
                render.Floor((x, z) => script.Ground.HeightAt(x, z), centre, metres * 1.1f, 1f, view, pixels);
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
