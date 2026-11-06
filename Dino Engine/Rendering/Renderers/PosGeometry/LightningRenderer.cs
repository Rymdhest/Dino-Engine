using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.Systems;
using Dino_Engine.Rendering.Renderers.Simulation;
using Dino_Engine.World;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.PosGeometry
{
    /// <summary>
    /// Bolts near enough to see. The channel is drawn from the strike's own number by midpoint displacement, a jagged line that forks on its way down, so a bolt looks the same to everyone
    /// who sees it, and each stroke of its flicker (WeatherSystem.BoltLight) lights the whole of it. Forks only carry the first stroke: what comes after runs down the channel that's open.
    /// </summary>
    public class LightningRenderer : RenderPassRenderer
    {
        private const float Brightness = 90f;
        private const int Levels = 8;           // Halvings of the main channel.
        private const int FloatsPerPiece = 8;

        private readonly record struct Piece(Vector3 From, Vector3 To, float Width, float Share, bool Fork);

        private readonly ShaderProgram _shader = new ShaderProgram("Weather/Lightning.vert", "Weather/Lightning.frag");
        private readonly Dictionary<uint, List<Piece>> _shapes = new();
        private readonly List<uint> _stale = new();
        private readonly int _vao;
        private readonly int _buffer;
        private float[] _data = new float[FloatsPerPiece * 2048];

        public LightningRenderer() : base("Lightning")
        {
            _vao = GL.GenVertexArray();
            _buffer = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _buffer);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 4, VertexAttribPointerType.Float, false, FloatsPerPiece * sizeof(float), 0);
            GL.VertexAttribDivisor(0, 1);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, FloatsPerPiece * sizeof(float), 4 * sizeof(float));
            GL.VertexAttribDivisor(1, 1);
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {
            if (renderEngine._weatherRenderer.Frame is not WeatherRenderCommand frame) return;
            WeatherSingleton weather = frame.weather;
            if (weather.Bolts == null || weather.Bolts.Count == 0)
            {
                _shapes.Clear();
                return;
            }

            int pieces = 0;
            _stale.Clear();
            _stale.AddRange(_shapes.Keys);
            foreach (Bolt bolt in weather.Bolts)
            {
                _stale.Remove(bolt.Strike.Seed);
                if (!_shapes.TryGetValue(bolt.Strike.Seed, out List<Piece>? shape))
                {
                    shape = Shape(bolt, weather.CloudAltitude);
                    _shapes[bolt.Strike.Seed] = shape;
                }

                float light = WeatherSystem.BoltLight(bolt.Strike, bolt.Age);
                if (light <= 0.01f) continue;
                float forks = MathF.Exp(-bolt.Age / 0.08f);
                foreach (Piece piece in shape)
                {
                    float strength = light * piece.Share * (piece.Fork ? forks : 1f);
                    if (strength <= 0.01f) continue;
                    if ((pieces + 1) * FloatsPerPiece > _data.Length) Array.Resize(ref _data, _data.Length * 2);
                    int at = pieces * FloatsPerPiece;
                    _data[at] = piece.From.X;
                    _data[at + 1] = piece.From.Y;
                    _data[at + 2] = piece.From.Z;
                    _data[at + 3] = piece.Width;
                    _data[at + 4] = piece.To.X;
                    _data[at + 5] = piece.To.Y;
                    _data[at + 6] = piece.To.Z;
                    _data[at + 7] = strength;
                    pieces++;
                }
            }
            foreach (uint gone in _stale) _shapes.Remove(gone);
            if (pieces == 0) return;

            renderEngine.Msaa.SceneBuffer.bind();
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.DepthMask(false);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            _shader.bind();
            _shader.loadUniformFloat("brightness", Brightness);
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _buffer);
            GL.BufferData(BufferTarget.ArrayBuffer, pieces * FloatsPerPiece * sizeof(float), _data, BufferUsageHint.StreamDraw);
            GL.DrawArraysInstanced(PrimitiveType.TriangleStrip, 0, 4, pieces);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
            GL.UseProgram(0);

            GL.Disable(EnableCap.Blend);
            GL.DepthFunc(DepthFunction.Less);
            GL.Enable(EnableCap.CullFace);
        }

        private static List<Piece> Shape(in Bolt bolt, float cloudAltitude)
        {
            var random = new WorldRandom(bolt.Strike.Seed);
            var pieces = new List<Piece>();
            Vector3 ground = bolt.Ground;
            Vector3 top = ground + new Vector3(random.Range(-90f, 90f), cloudAltitude, random.Range(-90f, 90f));
            Channel(pieces, top, ground, 0.55f, 1f, Levels, 3, false, random);
            return pieces;
        }

        // Halves the line again and again, pushing each middle aside by a share of its length, and lets branches go off down the way as it goes.
        private static void Channel(List<Piece> pieces, Vector3 from, Vector3 to, float width, float share, int levels, int forks, bool fork, WorldRandom random)
        {
            var points = new List<Vector3> { from, to };
            float scale = (to - from).Length * 0.21f;
            for (int level = 0; level < levels; level++)
            {
                var next = new List<Vector3>(points.Count * 2);
                for (int i = 0; i + 1 < points.Count; i++)
                {
                    next.Add(points[i]);
                    Vector3 sideways = new Vector3(random.Range(-1f, 1f), random.Range(-0.3f, 0.3f), random.Range(-1f, 1f));
                    next.Add((points[i] + points[i + 1]) * 0.5f + sideways * scale);
                }
                next.Add(points[^1]);
                points = next;
                scale *= 0.54f;
            }

            for (int i = 0; i + 1 < points.Count; i++)
            {
                pieces.Add(new Piece(points[i], points[i + 1], width, share, fork));
                if (forks <= 0 || !random.Chance(0.11f)) continue;

                // Forks leave at a slant and keep going down, thinning as they go.
                Vector3 onward = (points[i + 1] - points[i]).Normalized();
                Vector3 away = new Vector3(random.Range(-1f, 1f), random.Range(-0.8f, -0.2f), random.Range(-1f, 1f)).Normalized();
                float reach = (to - from).Length * random.Range(0.12f, 0.3f);
                Channel(pieces, points[i], points[i] + (onward + away).Normalized() * reach, width * 0.5f, share * 0.6f, Math.Max(levels - 2, 2), forks - 1, true, random);
            }
        }

        public override void CleanUp()
        {
            _shader.cleanUp();
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_buffer);
        }
    }
}
