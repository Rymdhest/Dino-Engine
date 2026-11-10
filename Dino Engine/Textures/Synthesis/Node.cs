using Dino_Engine.Rendering;
using OpenTK.Mathematics;

namespace Dino_Engine.Textures.Synthesis
{
    /// <summary>The operations of Synth_Op.frag, by their numbers there.</summary>
    internal enum Op
    {
        Add = 0,
        Subtract = 1,
        Multiply = 2,
        Divide = 3,
        Min = 4,
        Max = 5,
        Lerp = 6,
        SmoothMin = 7,
        SmoothMax = 8,
        Remap = 9,
        Clamp = 10,
        Smoothstep = 11,
        Power = 12,
        Abs = 13,
        Invert = 14,
        Profile = 15,
        Quantize = 16,
        Sine = 17,
        Threshold = 18,
        Screen = 19,
        Difference = 20,
        MultiplyAdd = 21,
        Sqrt = 22,
        Fract = 23,
        Luminance = 24,
        Hash = 25,
        Jitter = 26,
        Saturation = 27,
        Compose = 28,
        Channel = 29,
        Overlay = 30,
        WithAlpha = 31,
    }

    /// <summary>One input of a pass: a channel of what a node made (0 to 3), all of it (4), or a constant.</summary>
    internal readonly struct Operand
    {
        public readonly Node? Node;
        public readonly int Output;
        public readonly int Channel;
        public readonly Vector4 Constant;

        public Operand(Node node, int output, int channel)
        {
            Node = node;
            Output = output;
            Channel = channel;
            Constant = Vector4.Zero;
        }

        public Operand(Vector4 constant)
        {
            Node = null;
            Output = 0;
            Channel = -1;
            Constant = constant;
        }

        public bool IsConstant => Node == null;

        /// <summary>An input that isn't read: the constant 0.</summary>
        public static Operand None => new Operand(Vector4.Zero);
    }

    /// <summary>
    /// One step of a material's graph: something it makes from what it reads. Nothing is drawn when a node is made: a recipe builds the whole graph, and only what the
    /// finished material needs is drawn, in order, each image given back as soon as nothing's left to read it (<see cref="Evaluation"/>).
    /// </summary>
    internal abstract class Node
    {
        public readonly Canvas Canvas;
        public readonly ImageFormat[] Outputs;

        /// <summary>Every node this one reads, once for every time it reads it.</summary>
        public readonly List<Node> Inputs = new();

        protected Node(Canvas canvas, params ImageFormat[] outputs)
        {
            Canvas = canvas;
            Outputs = outputs;
        }

        protected void Reads(Operand operand)
        {
            if (operand.Node != null) Inputs.Add(operand.Node);
        }

        /// <summary>Draws into <paramref name="results"/>, one image for each output, with <paramref name="images"/> giving the images of what this reads.</summary>
        public abstract void Run(Synthesizer gpu, Image[] results, Func<Node, Image[]> images);

        /// <summary>Binds an operand as input A, B or C of a pass (inputA, channelA, constantA), on texture unit <paramref name="unit"/>.</summary>
        protected static void Bind(ShaderProgram shader, string name, int unit, Operand operand, Func<Node, Image[]> images)
        {
            Synthesizer.BindTexture(unit, operand.Node != null ? images(operand.Node)[operand.Output].Texture : 0);
            if (shader.HasUniform("input" + name)) shader.loadUniformInt("input" + name, unit);
            if (shader.HasUniform("channel" + name)) shader.loadUniformInt("channel" + name, operand.IsConstant ? -1 : operand.Channel);
            if (shader.HasUniform("constant" + name)) shader.loadUniformVector4f("constant" + name, operand.Constant);
        }

        protected static List<(int, int)> Targets(Image[] results)
        {
            var targets = new List<(int, int)>(results.Length);
            foreach (Image image in results) targets.Add((image.Texture, 0));
            return targets;
        }
    }

    /// <summary>A node drawn by one pass of one shader, from up to three operands and whatever uniforms it's told.</summary>
    internal sealed class PassNode : Node
    {
        private static readonly string[] Names = { "A", "B", "C" };

        private readonly Func<Synthesizer, ShaderProgram> _shader;
        private readonly Operand[] _operands;
        private readonly Action<ShaderProgram>? _uniforms;

        public PassNode(Canvas canvas, Func<Synthesizer, ShaderProgram> shader, Operand[] operands, Action<ShaderProgram>? uniforms, params ImageFormat[] outputs) : base(canvas, outputs)
        {
            _shader = shader;
            _operands = operands;
            _uniforms = uniforms;
            foreach (Operand operand in operands) Reads(operand);
        }

        public override void Run(Synthesizer gpu, Image[] results, Func<Node, Image[]> images)
        {
            ShaderProgram shader = _shader(gpu);
            shader.bind();
            for (int i = 0; i < _operands.Length; i++) Bind(shader, Names[i], i, _operands[i], images);
            _uniforms?.Invoke(shader);
            gpu.Draw(shader, Canvas.Size, Targets(results));
        }
    }

    /// <summary>A Gaussian blur: across, then down, through an image of its own.</summary>
    internal sealed class BlurNode : Node
    {
        private readonly Operand _input;
        private readonly float _sigma;

        public BlurNode(Canvas canvas, Operand input, float sigma, ImageFormat format) : base(canvas, format)
        {
            _input = input;
            _sigma = sigma;
            Reads(input);
        }

        public override void Run(Synthesizer gpu, Image[] results, Func<Node, Image[]> images)
        {
            ShaderProgram shader = gpu.Blur;
            Image across = gpu.Pool.Take(Outputs[0], Canvas.Size);
            shader.bind();
            Bind(shader, "A", 0, _input, images);
            shader.loadUniformVector2i("direction", new Vector2i(1, 0));
            shader.loadUniformFloat("sigma", _sigma);
            gpu.Draw(shader, Canvas.Size, new[] { (across.Texture, 0) });

            shader.bind();
            Bind(shader, "A", 0, new Operand(this, 0, 0), _ => new[] { across });
            shader.loadUniformInt("channelA", Outputs[0] == ImageFormat.Scalar ? 0 : 4);
            shader.loadUniformVector2i("direction", new Vector2i(0, 1));
            shader.loadUniformFloat("sigma", _sigma);
            gpu.Draw(shader, Canvas.Size, Targets(results));
            gpu.Pool.Give(across);
        }
    }

    /// <summary>How much of the sky each point of a height sees (Synth_Occlusion.frag), through a copy of the height with mips of its own.</summary>
    internal sealed class OcclusionNode : Node
    {
        private readonly Operand _height;
        private readonly float _slope;
        private readonly float _reach;
        private readonly int _directions;
        private readonly int _steps;

        public OcclusionNode(Canvas canvas, Operand height, float slope, float reach, int directions, int steps) : base(canvas, ImageFormat.Scalar)
        {
            _height = height;
            _slope = slope;
            _reach = reach;
            _directions = directions;
            _steps = steps;
            Reads(height);
        }

        public override void Run(Synthesizer gpu, Image[] results, Func<Node, Image[]> images)
        {
            int levels = MaterialBaker.LevelsFor(Canvas.Size);
            Image source = images(_height.Node!)[_height.Output];
            Image mipped = gpu.Pool.Take(ImageFormat.Scalar, Canvas.Size, levels);

            // The height's channel into a scalar image with mips, which the far steps read coarse.
            ShaderProgram copy = gpu.Op;
            copy.bind();
            Bind(copy, "A", 0, _height, images);
            Bind(copy, "B", 1, new Operand(Vector4.Zero), images);
            Bind(copy, "C", 2, new Operand(Vector4.Zero), images);
            copy.loadUniformInt("op", (int)Op.Add);
            copy.loadUniformVector4f("parameters", Vector4.Zero);
            gpu.Draw(copy, Canvas.Size, new[] { (mipped.Texture, 0) });
            Synthesizer.Edit(mipped.Texture);
            OpenTK.Graphics.OpenGL.GL.GenerateMipmap(OpenTK.Graphics.OpenGL.GenerateMipmapTarget.Texture2D);

            ShaderProgram shader = gpu.Occlusion;
            shader.bind();
            Bind(shader, "A", 0, new Operand(this, 0, 0), _ => new[] { mipped });
            shader.loadUniformFloat("slope", _slope);
            shader.loadUniformFloat("reach", _reach);
            shader.loadUniformInt("directions", _directions);
            shader.loadUniformInt("steps", _steps);
            gpu.Draw(shader, Canvas.Size, Targets(results));
            gpu.Pool.Give(mipped);
        }
    }

    /// <summary>A field measured and then remapped by what it holds: Normalize to span 0 to 1, Equalize to spread evenly over it.</summary>
    internal sealed class MeasuredNode : Node
    {
        public enum Kind { Normalize, Equalize }

        private readonly Operand _input;
        private readonly Kind _kind;

        public MeasuredNode(Canvas canvas, Operand input, Kind kind) : base(canvas, ImageFormat.Scalar)
        {
            _input = input;
            _kind = kind;
            Reads(input);
        }

        public override void Run(Synthesizer gpu, Image[] results, Func<Node, Image[]> images)
        {
            Image source = images(_input.Node!)[_input.Output];
            int channel = _input.Channel >= 4 ? 0 : _input.Channel;
            FieldStatistics statistics = gpu.Measure(source, channel);
            float low = statistics.Least;
            float high = MathF.Max(statistics.Most, low + 1e-6f);

            if (_kind == Kind.Normalize)
            {
                ShaderProgram op = gpu.Op;
                op.bind();
                Bind(op, "A", 0, _input, images);
                Bind(op, "B", 1, new Operand(Vector4.Zero), images);
                Bind(op, "C", 2, new Operand(Vector4.Zero), images);
                op.loadUniformInt("op", (int)Op.Remap);
                op.loadUniformVector4f("parameters", new Vector4(low, high, 0f, 1f));
                gpu.Draw(op, Canvas.Size, Targets(results));
                return;
            }

            // The cumulative share of texels at or below each bin, which maps the field onto an even spread.
            uint[] counts = gpu.Histogram(source, channel, low, high);
            float[] curve = new float[counts.Length];
            double total = 0;
            foreach (uint count in counts) total += count;
            double below = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                // The middle of a bin's share, so that the curve runs from just above 0 to just below 1 and stays strictly rising.
                curve[i] = (float)((below + 0.5 * counts[i]) / total);
                below += counts[i];
            }
            ShaderProgram shader = gpu.Curve;
            shader.bind();
            Bind(shader, "A", 0, _input, images);
            gpu.BindCurve(1, curve);
            shader.loadUniformInt("curve", 1);
            shader.loadUniformVector2f("range", new Vector2(low, high));
            gpu.Draw(shader, Canvas.Size, Targets(results));
        }
    }

    /// <summary>
    /// Draws a graph's nodes in an order that has everything a node reads made first, holding each image only as long as something is left to read it, and holding
    /// what was asked for until it's given back.
    /// </summary>
    internal sealed class Evaluation
    {
        private readonly Synthesizer _gpu;
        private readonly Dictionary<Node, int> _readersLeft = new();
        private readonly Dictionary<Node, Image[]> _made = new();
        private readonly HashSet<Node> _kept = new();
        private readonly HashSet<Node> _counted = new();

        public int Drawn { get; private set; }

        public Evaluation(Synthesizer gpu, IEnumerable<Node> wanted)
        {
            _gpu = gpu;
            foreach (Node node in wanted)
            {
                _kept.Add(node);
                Count(node);
            }
        }

        private void Count(Node node)
        {
            if (!_counted.Add(node)) return;
            foreach (Node input in node.Inputs)
            {
                _readersLeft[input] = _readersLeft.GetValueOrDefault(input) + 1;
                Count(input);
            }
        }

        public Image[] Get(Node node)
        {
            if (_made.TryGetValue(node, out Image[]? images)) return images;
            foreach (Node input in node.Inputs) Get(input);

            images = new Image[node.Outputs.Length];
            for (int i = 0; i < images.Length; i++) images[i] = _gpu.Pool.Take(node.Outputs[i], node.Canvas.Size);
            node.Run(_gpu, images, n => _made[n]);
            Drawn++;
            _made[node] = images;
            foreach (Node input in node.Inputs) Release(input);
            return images;
        }

        private void Release(Node node)
        {
            int left = --_readersLeft[node];
            if (left > 0 || _kept.Contains(node)) return;
            foreach (Image image in _made[node]) _gpu.Pool.Give(image);
            _made.Remove(node);
        }

        /// <summary>Gives back everything still held, what was asked for included.</summary>
        public void Finish()
        {
            foreach (Image[] images in _made.Values)
            {
                foreach (Image image in images) _gpu.Pool.Give(image);
            }
            _made.Clear();
        }
    }
}
