using Dino_AnimLab;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_LifeLab
{
    /// <summary>A body doing something, as a function of time: what it's built of, and how it stands at any moment, to be looked at with no window.</summary>
    internal sealed record Scene(string Name, Func<Mesh> Build, int Bones, float Length, float Metres, Action<float, Matrix4[]> Pose, Func<float, Vector3>? Follow = null);

    /// <summary>Draws a run of frames of a body, on a floor that stays put, so that what moves can be seen to move.</summary>
    internal static class Sheet
    {
        public static int Run(Options options)
        {
            string name = options.Text("do", "rabbit.bound");
            Scene? scene = Scenes.Named(name);
            if (scene == null)
            {
                Console.WriteLine($"No scene called {name}. There's {string.Join(", ", Scenes.All.Select(s => s.Name))}.");
                return 1;
            }

            int frames = options.Int("frames", 12);
            int columns = options.Int("columns", Math.Min(frames, 6));
            int rows = (frames + columns - 1) / columns;
            int tile = options.Int("tile", 280);
            float metres = options.Float("metres", scene.Metres);
            PoseView view = PoseView.Named(options.Text("view", "side"));
            const int Shrink = 2;

            Mesh built = scene.Build();
            var mesh = new PosableMesh(built);
            var render = new PoseRender(tile * Shrink, tile * Shrink);
            var sheet = new byte[columns * tile * rows * tile * 4];
            var skin = new Matrix4[scene.Bones];
            float pixels = tile * Shrink / metres;

            for (int shot = 0; shot < frames; shot++)
            {
                float time = scene.Length * shot / frames;
                scene.Pose(time, skin);
                mesh.Pose(skin, Matrix4.Identity);

                Vector3 centre = (scene.Follow?.Invoke(time) ?? Vector3.Zero) + new Vector3(0f, metres * 0.22f, 0f);
                render.Clear(new Vector3(0.05f, 0.052f, 0.06f));
                render.Floor((_, _) => 0f, centre, metres, metres < 3f ? 0.25f : 1f, view, pixels);
                render.Draw(mesh, view, centre, pixels);
                render.Blit(sheet, columns * tile, shot % columns * tile, shot / columns * tile, Shrink);
            }

            string path = options.Text("out", $"life-{scene.Name}.png");
            PngWriter.Write(path, sheet, columns * tile, rows * tile);
            Console.WriteLine($"Wrote {path}: {built.faces.Count} triangles, {built.meshVertices.Count} vertices.");
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

        public string Text(string name, string otherwise) => _values.TryGetValue(name, out string? value) ? value : otherwise;
        public int Int(string name, int otherwise) => _values.TryGetValue(name, out string? value) ? int.Parse(value) : otherwise;
        public float Float(string name, float otherwise) => _values.TryGetValue(name, out string? value) ? float.Parse(value, System.Globalization.CultureInfo.InvariantCulture) : otherwise;
    }
}
