using OpenTK.Mathematics;

namespace Dino_Engine.Textures.Synthesis
{
    /// <summary>How a rising edge rises, from a border inwards (<see cref="Field.Profile"/>).</summary>
    public enum Rise
    {
        Linear = 0,     // Straight, as a chamfer.
        Smooth = 1,     // Eased at both ends.
        Round = 2,      // Steep at the edge and flat on top, as a pillow, a cobble or a worn stone.
        Cove = 3,       // Flat at the edge and steep at the top, as a hollow.
        Sine = 4,       // Between round and straight.
    }

    public enum Smear
    {
        Average = 0,
        Least = 1,
        Most = 2,
    }

    /// <summary>
    /// A number at every point of the tile: a height, a mask, a share, an id. Arithmetic and every method make a new field and change nothing, and a field made only
    /// of constants is a constant, worked out here rather than drawn. Values are whatever they come to; nothing is clamped unless asked. See TEXTURES.md.
    /// </summary>
    public readonly struct Field
    {
        internal readonly Node? Node;
        internal readonly int Output;
        internal readonly int Channel;
        internal readonly float Value;

        internal Field(Node node, int output = 0, int channel = 0)
        {
            Node = node;
            Output = output;
            Channel = channel;
            Value = 0f;
        }

        public Field(float value)
        {
            Node = null;
            Output = 0;
            Channel = -1;
            Value = value;
        }

        public bool IsConstant => Node == null;

        internal Operand Operand => Node == null ? new Operand(new Vector4(Value)) : new Operand(Node, Output, Channel);

        internal Canvas? Canvas => Node?.Canvas;

        public static implicit operator Field(float value) => new Field(value);

        public static Field operator +(Field a, Field b) => Apply(Op.Add, a, b);
        public static Field operator -(Field a, Field b) => Apply(Op.Subtract, a, b);
        public static Field operator *(Field a, Field b) => Apply(Op.Multiply, a, b);
        public static Field operator /(Field a, Field b) => Apply(Op.Divide, a, b);
        public static Field operator -(Field a) => Apply(Op.Subtract, 0f, a);

        /// <summary>A colour scaled by this, everywhere: a paint.</summary>
        public static Paint operator *(Vector3 colour, Field f) => new Paint(colour) * f;
        public static Paint operator *(Field f, Vector3 colour) => new Paint(colour) * f;

        /// <summary>From one range to another, linearly: what was <paramref name="fromLow"/> becomes <paramref name="toLow"/>. Nothing is clamped.</summary>
        public Field Remap(float fromLow, float fromHigh, float toLow = 0f, float toHigh = 1f) => Apply(Op.Remap, this, 0f, 0f, new Vector4(fromLow, fromHigh, toLow, toHigh));

        public Field Clamp(float low = 0f, float high = 1f) => Apply(Op.Clamp, this, 0f, 0f, new Vector4(low, high, 0f, 0f));

        /// <summary>0 at or below <paramref name="low"/>, 1 at or above <paramref name="high"/>, eased between.</summary>
        public Field Smooth(float low, float high) => Apply(Op.Smoothstep, this, 0f, 0f, new Vector4(low, high, 0f, 0f));

        /// <summary>0 below <paramref name="level"/> and 1 above, eased over <paramref name="softness"/> either side.</summary>
        public Field Threshold(float level, float softness = 0f) => Apply(Op.Threshold, this, 0f, 0f, new Vector4(level, softness, 0f, 0f));

        public Field Pow(float exponent) => Apply(Op.Power, this, 0f, 0f, new Vector4(exponent, 0f, 0f, 0f));
        public Field Abs() => Apply(Op.Abs, this, 0f);
        public Field Invert() => Apply(Op.Invert, this, 0f);
        public Field Sqrt() => Apply(Op.Sqrt, this, 0f);
        public Field Fract() => Apply(Op.Fract, this, 0f);
        public Field Quantize(float steps) => Apply(Op.Quantize, this, 0f, 0f, new Vector4(steps, 0f, 0f, 0f));

        /// <summary>A wave of this, 0 to 1: 0.5 + 0.5 sin(2 pi (x * frequency + phase)).</summary>
        public Field Sine(float frequency = 1f, float phase = 0f) => Apply(Op.Sine, this, 0f, 0f, new Vector4(frequency, phase, 0f, 0f));

        /// <summary>
        /// A distance from a border (positive inside, as <see cref="Cells.Border"/> and <see cref="Bricks.Edge"/> are) as a rising edge: 0 at the border and 1 from
        /// <paramref name="width"/> in, by <paramref name="shape"/>. Width is in whatever the distance is in.
        /// </summary>
        public Field Profile(float width, Rise shape = Rise.Round) => Apply(Op.Profile, this, 0f, 0f, new Vector4(width, (int)shape, 0f, 0f));

        /// <summary>A random number in [0, 1) for every value of this, the same for the same value: a random of each cell from its id.</summary>
        public Field Random(int seed = 0) => Apply(Op.Hash, this, 0f, 0f, new Vector4(seed * 977 + 13, 0f, 0f, 0f));

        public Field Min(Field other) => Apply(Op.Min, this, other);
        public Field Max(Field other) => Apply(Op.Max, this, other);

        /// <summary>The lesser of the two, rounded into each other where they're within <paramref name="k"/>.</summary>
        public Field SmoothMin(Field other, float k) => Apply(Op.SmoothMin, this, other, 0f, new Vector4(k, 0f, 0f, 0f));
        public Field SmoothMax(Field other, float k) => Apply(Op.SmoothMax, this, other, 0f, new Vector4(k, 0f, 0f, 0f));

        public Field Screen(Field other) => Apply(Op.Screen, this, other);
        public Field Difference(Field other) => Apply(Op.Difference, this, other);

        public static Field Lerp(Field a, Field b, Field t) => Apply(Op.Lerp, a, b, t);

        /// <summary>This field moved by <paramref name="offsets"/>, by up to about <paramref name="amount"/> of the tile.</summary>
        public Field Warp(Offsets offsets, float amount)
        {
            if (IsConstant || amount == 0f) return this;
            Canvas canvas = Canvas!;
            Operand moved = Operand;
            Operand by = offsets.Operand;
            return new Field(new PassNode(canvas, gpu => gpu.Warp, new[] { moved, by }, shader => shader.loadUniformFloat("amount", amount), ImageFormat.Scalar));
        }

        /// <summary>Blurred by a Gaussian of <paramref name="radius"/> (its deviation) as a share of the tile, wrapping.</summary>
        public Field Blur(float radius)
        {
            if (IsConstant || radius <= 0f) return this;
            Canvas canvas = Canvas!;
            float sigma = MathF.Min(radius * canvas.Size, 96f);
            return new Field(new BlurNode(canvas, Operand, sigma, ImageFormat.Scalar));
        }

        /// <summary>Smeared down the slopes of <paramref name="slopes"/> for <paramref name="distance"/> of the tile (negative goes up them), as water and wear smear.</summary>
        public Field SlopeBlur(Field slopes, float distance, int samples = 16, Smear mode = Smear.Average)
        {
            if (IsConstant || slopes.IsConstant || distance == 0f) return this;
            Canvas canvas = Canvas ?? slopes.Canvas!;
            return new Field(new PassNode(canvas, gpu => gpu.SlopeBlur, new[] { Operand, slopes.Operand }, shader =>
            {
                shader.loadUniformFloat("distance", distance);
                shader.loadUniformInt("samples", samples);
                shader.loadUniformInt("mode", (int)mode);
            }, ImageFormat.Scalar));
        }

        /// <summary>The least of this within <paramref name="radius"/> of the tile: crests eaten back, edges chipped.</summary>
        public Field Erode(float radius) => Morph(radius, 0);

        /// <summary>The most of this within <paramref name="radius"/> of the tile.</summary>
        public Field Dilate(float radius) => Morph(radius, 1);

        private Field Morph(float radius, int mode)
        {
            if (IsConstant || radius <= 0f) return this;
            Canvas canvas = Canvas!;
            float texels = MathF.Min(radius * canvas.Size, 24f);
            return new Field(new PassNode(canvas, gpu => gpu.Morph, new[] { Operand }, shader =>
            {
                shader.loadUniformFloat("radius", texels);
                shader.loadUniformInt("mode", mode);
            }, ImageFormat.Scalar));
        }

        /// <summary>Stretched to span exactly 0 to 1, from its own least to its own most.</summary>
        public Field Normalize() => IsConstant ? new Field(0.5f) : new Field(new MeasuredNode(Canvas!, Operand, MeasuredNode.Kind.Normalize));

        /// <summary>Spread evenly over 0 to 1, keeping its order: afterwards a threshold of 0.3 leaves just 30% of the tile above it.</summary>
        public Field Equalize() => IsConstant ? new Field(0.5f) : new Field(new MeasuredNode(Canvas!, Operand, MeasuredNode.Kind.Equalize));

        /// <summary>
        /// How much of the sky each point of this height sees (1 open, towards 0 deep in a crack), at the canvas's relief, looking <paramref name="reach"/> of the tile
        /// for what's in the way. Cosine-weighted about each point's own normal.
        /// </summary>
        public Field Occlusion(float reach)
        {
            if (IsConstant) return 1f;
            Canvas canvas = Canvas!;
            return new Field(new OcclusionNode(canvas, Operand, canvas.Spec.ReliefRatio, reach, 12, 10));
        }

        /// <summary>How far this stands above (positive) or sinks below (negative) its own surroundings within <paramref name="radius"/> of the tile: edges and crests, hollows and seams.</summary>
        public Field Curvature(float radius) => this - Blur(radius);

        internal static Field Apply(Op op, Field a, Field b, Field c = default, Vector4 parameters = default)
        {
            if (a.IsConstant && b.IsConstant && c.IsConstant)
            {
                return new Field(Fold(op, a.Value, b.Value, c.Value, parameters));
            }
            Canvas canvas = a.Canvas ?? b.Canvas ?? c.Canvas!;
            if ((a.Canvas != null && a.Canvas != canvas) || (b.Canvas != null && b.Canvas != canvas) || (c.Canvas != null && c.Canvas != canvas))
            {
                throw new InvalidOperationException("Fields of two canvases can't be combined.");
            }
            Operand[] operands = { a.Operand, b.Operand, c.Operand };
            return new Field(new PassNode(canvas, gpu => gpu.Op, operands, shader =>
            {
                shader.loadUniformInt("op", (int)op);
                shader.loadUniformVector4f("parameters", parameters);
            }, ImageFormat.Scalar));
        }

        /// <summary>What Synth_Op.frag would make of constants.</summary>
        internal static float Fold(Op op, float a, float b, float c, Vector4 p)
        {
            switch (op)
            {
                case Op.Add: return a + b;
                case Op.Subtract: return a - b;
                case Op.Multiply: return a * b;
                case Op.Divide: return MathF.Sign(b) * a / MathF.Max(MathF.Abs(b), 1e-6f);
                case Op.Min: return MathF.Min(a, b);
                case Op.Max: return MathF.Max(a, b);
                case Op.Lerp: return a + (b - a) * c;
                case Op.SmoothMin: return SmoothMin(a, b, p.X);
                case Op.SmoothMax: return -SmoothMin(-a, -b, p.X);
                case Op.Remap: return (a - p.X) / (p.Y - p.X) * (p.W - p.Z) + p.Z;
                case Op.Clamp: return Math.Clamp(a, p.X, p.Y);
                case Op.Smoothstep: return SmoothStep(p.X, p.Y, a);
                case Op.Power: return MathF.Pow(MathF.Max(a, 0f), p.X);
                case Op.Abs: return MathF.Abs(a);
                case Op.Invert: return 1f - a;
                case Op.Profile: return ProfileOf(a, p.X, (Rise)(int)p.Y);
                case Op.Quantize: return MathF.Floor(a * p.X) / p.X;
                case Op.Sine: return 0.5f + 0.5f * MathF.Sin(MathF.Tau * (a * p.X + p.Y));
                case Op.Threshold: return SmoothStep(p.X - p.Y, p.X + p.Y + 1e-6f, a);
                case Op.Screen: return 1f - (1f - a) * (1f - b);
                case Op.Difference: return MathF.Abs(a - b);
                case Op.MultiplyAdd: return a * b + c;
                case Op.Sqrt: return MathF.Sqrt(MathF.Max(a, 0f));
                case Op.Fract: return a - MathF.Floor(a);
                default: throw new InvalidOperationException($"{op} has no constant form.");
            }
        }

        private static float SmoothMin(float a, float b, float k)
        {
            float h = MathF.Max(k - MathF.Abs(a - b), 0f) / MathF.Max(k, 1e-6f);
            return MathF.Min(a, b) - h * h * k * 0.25f;
        }

        private static float SmoothStep(float low, float high, float x)
        {
            float t = Math.Clamp((x - low) / (high - low), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private static float ProfileOf(float distance, float width, Rise shape)
        {
            float x = Math.Clamp(distance / MathF.Max(width, 1e-6f), 0f, 1f);
            return shape switch
            {
                Rise.Linear => x,
                Rise.Smooth => x * x * (3f - 2f * x),
                Rise.Round => MathF.Sqrt(1f - (1f - x) * (1f - x)),
                Rise.Cove => 1f - MathF.Sqrt(1f - x * x),
                _ => MathF.Sin(x * MathF.PI * 0.5f),
            };
        }
    }

    /// <summary>
    /// A colour at every point of the tile, linear, in what the engine calls albedo: the share of light a surface throws back, by channel. The same arithmetic as a
    /// field, by channel, and a field in arithmetic with a paint is the same in all three.
    /// </summary>
    public readonly struct Paint
    {
        internal readonly Node? Node;
        internal readonly int Output;
        internal readonly Vector3 Value;

        internal Paint(Node node, int output = 0)
        {
            Node = node;
            Output = output;
            Value = Vector3.Zero;
        }

        public Paint(Vector3 colour)
        {
            Node = null;
            Output = 0;
            Value = colour;
        }

        public bool IsConstant => Node == null;

        internal Operand Operand => Node == null ? new Operand(new Vector4(Value, 1f)) : new Operand(Node, Output, 4);

        internal Canvas? Canvas => Node?.Canvas;

        public static implicit operator Paint(Vector3 colour) => new Paint(colour);

        public static Paint operator +(Paint a, Paint b) => Apply(Op.Add, a.Operand, b.Operand, Operand.None, default, a.Canvas ?? b.Canvas, () => a.Value + b.Value);
        public static Paint operator *(Paint a, Paint b) => Apply(Op.Multiply, a.Operand, b.Operand, Operand.None, default, a.Canvas ?? b.Canvas, () => a.Value * b.Value);
        public static Paint operator *(Paint a, Field f) => Apply(Op.Multiply, a.Operand, f.Operand, Operand.None, default, a.Canvas ?? f.Canvas, () => a.Value * f.Value);
        public static Paint operator *(Field f, Paint a) => a * f;
        public static Paint operator *(Paint a, float f) => a * new Field(f);

        /// <summary>From <paramref name="a"/> where <paramref name="t"/> is 0 to <paramref name="b"/> where it's 1.</summary>
        public static Paint Lerp(Paint a, Paint b, Field t)
        {
            return Apply(Op.Lerp, a.Operand, b.Operand, t.Operand, default, a.Canvas ?? b.Canvas ?? t.Canvas, () => a.Value + (b.Value - a.Value) * t.Value);
        }

        /// <summary>
        /// Every piece a little different, as bricks of one batch are: moved round the colour wheel by up to <paramref name="hue"/> of a turn, made more or less vivid
        /// by up to <paramref name="saturation"/> and lighter or darker by up to <paramref name="value"/>, each by a random of <paramref name="id"/> (a cell's id).
        /// </summary>
        public Paint Jitter(Field id, float hue, float saturation, float value)
        {
            Paint self = this;
            return Apply(Op.Jitter, Operand, id.Operand, Operand.None, new Vector4(hue, saturation, value, 0f), Canvas ?? id.Canvas, () => self.Value);
        }

        /// <summary>Greyer towards 0, as it is at 1, more vivid past it.</summary>
        public Paint Saturation(float amount)
        {
            Paint self = this;
            return Apply(Op.Saturation, Operand, Operand.None, Operand.None, new Vector4(amount, 0f, 0f, 0f), Canvas, () =>
            {
                float grey = Vector3.Dot(self.Value, new Vector3(0.2126f, 0.7152f, 0.0722f));
                return new Vector3(grey) + (self.Value - new Vector3(grey)) * amount;
            });
        }

        /// <summary>A soft-light-like overlay of a pattern on this colour: darker where the pattern's below half, lighter where above.</summary>
        public Paint Overlay(Paint pattern)
        {
            Paint self = this;
            return Apply(Op.Overlay, Operand, pattern.Operand, Operand.None, default, Canvas ?? pattern.Canvas, () => self.Value);
        }

        public Field Luminance()
        {
            if (IsConstant) return Vector3.Dot(Value, new Vector3(0.2126f, 0.7152f, 0.0722f));
            Operand[] operands = { Operand, new Operand(Vector4.Zero), new Operand(Vector4.Zero) };
            return new Field(new PassNode(Canvas!, gpu => gpu.Op, operands, shader =>
            {
                shader.loadUniformInt("op", (int)Op.Luminance);
                shader.loadUniformVector4f("parameters", Vector4.Zero);
            }, ImageFormat.Scalar));
        }

        private static Paint Apply(Op op, Operand a, Operand b, Operand c, Vector4 parameters, Canvas? canvas, Func<Vector3> fold)
        {
            if (a.IsConstant && b.IsConstant && c.IsConstant) return new Paint(fold());
            Operand[] operands = { a, b, c };
            return new Paint(new PassNode(canvas!, gpu => gpu.Op, operands, shader =>
            {
                shader.loadUniformInt("op", (int)op);
                shader.loadUniformVector4f("parameters", parameters);
            }, ImageFormat.Colour));
        }
    }

    /// <summary>Two channels of offsets for a warp, centred on 0.5 (<see cref="Canvas.Offsets"/>).</summary>
    public readonly struct Offsets
    {
        internal readonly Node Node;

        internal Offsets(Node node)
        {
            Node = node;
        }

        internal Operand Operand => new Operand(Node, 0, 4);
    }
}
