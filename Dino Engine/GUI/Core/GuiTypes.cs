using OpenTK.Mathematics;

namespace Dino_Engine.GUI
{
    /// <summary>Axis aligned rectangle in GUI units, origin top-left, y down.</summary>
    public struct GuiRect : IEquatable<GuiRect>
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public GuiRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public GuiRect(Vector2 position, Vector2 size) : this(position.X, position.Y, size.X, size.Y) { }

        public static GuiRect FromEdges(float left, float top, float right, float bottom) => new GuiRect(left, top, right - left, bottom - top);

        public float Left => X;
        public float Top => Y;
        public float Right => X + Width;
        public float Bottom => Y + Height;
        public Vector2 Position => new Vector2(X, Y);
        public Vector2 Size => new Vector2(Width, Height);
        public Vector2 Center => new Vector2(X + Width * 0.5f, Y + Height * 0.5f);
        public bool IsEmpty => Width <= 0f || Height <= 0f;

        public bool Contains(Vector2 point) => point.X >= X && point.Y >= Y && point.X < Right && point.Y < Bottom;

        public bool Intersects(GuiRect other) => other.X < Right && other.Right > X && other.Y < Bottom && other.Bottom > Y;

        public GuiRect Intersect(GuiRect other)
        {
            float left = MathF.Max(X, other.X);
            float top = MathF.Max(Y, other.Y);
            float right = MathF.Min(Right, other.Right);
            float bottom = MathF.Min(Bottom, other.Bottom);
            return FromEdges(left, top, MathF.Max(left, right), MathF.Max(top, bottom));
        }

        public GuiRect Deflate(Thickness thickness)
        {
            float width = MathF.Max(0f, Width - thickness.Horizontal);
            float height = MathF.Max(0f, Height - thickness.Vertical);
            return new GuiRect(X + thickness.Left, Y + thickness.Top, width, height);
        }

        public GuiRect Inflate(float amount) => new GuiRect(X - amount, Y - amount, Width + amount * 2f, Height + amount * 2f);

        public GuiRect Offset(Vector2 offset) => new GuiRect(X + offset.X, Y + offset.Y, Width, Height);

        public bool Equals(GuiRect other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
        public override bool Equals(object? obj) => obj is GuiRect other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
        public static bool operator ==(GuiRect a, GuiRect b) => a.Equals(b);
        public static bool operator !=(GuiRect a, GuiRect b) => !a.Equals(b);
        public override string ToString() => $"({X:0.#}, {Y:0.#}, {Width:0.#} x {Height:0.#})";
    }

    /// <summary>Per-edge sizes, used for margin and padding.</summary>
    public struct Thickness : IEquatable<Thickness>
    {
        public float Left;
        public float Top;
        public float Right;
        public float Bottom;

        public Thickness(float all) : this(all, all, all, all) { }
        public Thickness(float horizontal, float vertical) : this(horizontal, vertical, horizontal, vertical) { }
        public Thickness(float left, float top, float right, float bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public float Horizontal => Left + Right;
        public float Vertical => Top + Bottom;
        public static Thickness Zero => new Thickness(0f);

        public static implicit operator Thickness(float all) => new Thickness(all);

        public bool Equals(Thickness other) => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
        public override bool Equals(object? obj) => obj is Thickness other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);
        public static bool operator ==(Thickness a, Thickness b) => a.Equals(b);
        public static bool operator !=(Thickness a, Thickness b) => !a.Equals(b);
    }

    public enum SizeMode
    {
        /// <summary>Fixed size in GUI units.</summary>
        Pixels,
        /// <summary>Fraction of the parent's content size, plus a pixel offset.</summary>
        Relative,
        /// <summary>Takes all available space. In stack layouts the remaining space is shared by weight.</summary>
        Fill,
        /// <summary>Sizes to fit the content (text, children).</summary>
        Auto,
    }

    /// <summary>
    /// A layout length. Converts implicitly from numbers (pixels).
    /// Examples: <c>120</c>, <c>UIValue.Relative(0.5f, -8)</c> (50% minus 8px), <c>UIValue.Fill</c>, <c>UIValue.Auto</c>.
    /// </summary>
    public readonly struct UIValue : IEquatable<UIValue>
    {
        public readonly SizeMode Mode;
        /// <summary>Pixels for <see cref="SizeMode.Pixels"/>, fraction for Relative, weight for Fill.</summary>
        public readonly float Value;
        /// <summary>Extra pixels added to a Relative value.</summary>
        public readonly float Offset;

        public UIValue(SizeMode mode, float value, float offset = 0f)
        {
            Mode = mode;
            Value = value;
            Offset = offset;
        }

        public static UIValue Px(float pixels) => new UIValue(SizeMode.Pixels, pixels);
        public static UIValue Relative(float fraction, float offsetPixels = 0f) => new UIValue(SizeMode.Relative, fraction, offsetPixels);
        public static UIValue Percent(float percent, float offsetPixels = 0f) => new UIValue(SizeMode.Relative, percent / 100f, offsetPixels);
        public static UIValue Fill => new UIValue(SizeMode.Fill, 1f);
        public static UIValue FillWeight(float weight) => new UIValue(SizeMode.Fill, MathF.Max(0f, weight));
        public static UIValue Auto => new UIValue(SizeMode.Auto, 0f);

        public static implicit operator UIValue(float pixels) => Px(pixels);
        public static implicit operator UIValue(int pixels) => Px(pixels);

        /// <summary>Resolves a position offset: pixels, or a fraction of the reference length. Other modes give 0.</summary>
        public float ResolveOffset(float reference)
        {
            return Mode switch
            {
                SizeMode.Pixels => Value,
                SizeMode.Relative => float.IsFinite(reference) ? reference * Value + Offset : Offset,
                _ => 0f,
            };
        }

        public bool Equals(UIValue other) => Mode == other.Mode && Value == other.Value && Offset == other.Offset;
        public override bool Equals(object? obj) => obj is UIValue other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Mode, Value, Offset);
        public static bool operator ==(UIValue a, UIValue b) => a.Equals(b);
        public static bool operator !=(UIValue a, UIValue b) => !a.Equals(b);

        public override string ToString() => Mode switch
        {
            SizeMode.Pixels => $"{Value}px",
            SizeMode.Relative => Offset == 0 ? $"{Value * 100}%" : $"{Value * 100}% {Offset:+0;-0}px",
            SizeMode.Fill => Value == 1f ? "Fill" : $"Fill({Value})",
            _ => "Auto",
        };
    }

    /// <summary>
    /// Which point of the parent an element is attached to. The element's matching point is placed there,
    /// and X/Y offsets push the element inward from that edge (for Center axes positive means right/down).
    /// </summary>
    public enum Anchor
    {
        TopLeft, Top, TopRight,
        Left, Center, Right,
        BottomLeft, Bottom, BottomRight,
    }

    public enum Alignment
    {
        Start,
        Center,
        End,
    }

    public enum Orientation
    {
        Vertical,
        Horizontal,
    }

    public enum HitTestMode
    {
        /// <summary>Blocks the mouse if the element is visible (background or border) or interactive; otherwise passes through to what is behind.</summary>
        Auto,
        /// <summary>Always blocks the mouse inside its bounds.</summary>
        Block,
        /// <summary>The element itself is transparent to the mouse, but its children can still be hit.</summary>
        PassThrough,
        /// <summary>Neither the element nor its children can be hit.</summary>
        None,
    }

    internal static class AnchorExtensions
    {
        public static Alignment Horizontal(this Anchor anchor) => anchor switch
        {
            Anchor.TopLeft or Anchor.Left or Anchor.BottomLeft => Alignment.Start,
            Anchor.Top or Anchor.Center or Anchor.Bottom => Alignment.Center,
            _ => Alignment.End,
        };

        public static Alignment Vertical(this Anchor anchor) => anchor switch
        {
            Anchor.TopLeft or Anchor.Top or Anchor.TopRight => Alignment.Start,
            Anchor.Left or Anchor.Center or Anchor.Right => Alignment.Center,
            _ => Alignment.End,
        };
    }
}
