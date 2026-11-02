using Dino_Engine.GUI.Text;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using System.Runtime.InteropServices;

namespace Dino_Engine.GUI.Rendering
{
    public enum GuiTextureMode
    {
        /// <summary>RGBA texture with straight alpha, multiplied by the tint.</summary>
        Rgba = 2,
        /// <summary>RGB only, alpha ignored (useful for render targets whose alpha holds other data).</summary>
        Opaque = 3,
        /// <summary>Red channel shown as grayscale (depth buffers, single channel maps).</summary>
        RedAsGray = 4,
    }

    /// <summary>
    /// One GUI quad. The vertex shader expands it into 4 corners (instanced triangle strip), so the CPU writes
    /// a single small struct per rectangle, glyph or line instead of four vertices.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct GuiInstance
    {
        public Vector2 Center;     // GUI units
        public Vector2 HalfSize;   // shape half size (the SDF box), GUI units
        public Vector2 Axis;       // unit x axis of the quad; (1, 0) unless rotated (lines)
        public Vector4 UV;         // u0, v0 (top-left), u1, v1 (bottom-right)
        public uint Color;         // RGBA8, straight alpha
        public uint BorderColor;   // RGBA8, straight alpha (bottom colour for gradients)
        public Vector4 Params;     // corner radius, border width, mode, edge softness
        public float Expand;       // GUI units the quad extends past HalfSize (room for anti-aliasing / softness)

        public static readonly int SizeInBytes = Marshal.SizeOf<GuiInstance>();
    }

    public struct GuiDrawCommand
    {
        public int Texture;
        public GuiRect Clip;
        public int FirstInstance;
        public int InstanceCount;
    }

    /// <summary>
    /// Collects GUI geometry for one frame. Everything is a quad: rounded rectangles and borders are evaluated with a
    /// signed distance function in the shader, so they are anti-aliased at any size. Quads are batched into as few draw
    /// commands as possible; a new command only starts when the clip rect or the sampled texture changes.
    /// </summary>
    public class GuiDrawList
    {
        private const int ModeShape = 0;
        private const int ModeText = 1;
        private const int ModeGradient = 5;
        private static readonly Vector2 AxisX = new Vector2(1f, 0f);
        private static readonly Vector4 NoUV = Vector4.Zero;

        private GuiInstance[] _instances = new GuiInstance[4096];
        private int _count;
        private readonly List<GuiDrawCommand> _commands = new();
        private readonly Stack<GuiRect> _clipStack = new();
        private readonly Stack<float> _opacityStack = new();
        private GuiRect _clip;
        private float _opacity = 1f;

        // The open command is kept in fields and only written to the list when it closes.
        private bool _hasCommand;
        private int _commandTexture;
        private GuiRect _commandClip;
        private int _commandFirst;

        /// <summary>Physical pixels per GUI unit. Used to snap text to the pixel grid.</summary>
        public float PixelScale { get; private set; } = 1f;
        public GuiRect ScreenRect { get; private set; }
        public GuiRect CurrentClip => _clip;
        public float CurrentOpacity => _opacity;
        public int InstanceCount => _count;
        public GuiInstance[] Instances => _instances;
        public IReadOnlyList<GuiDrawCommand> Commands => _commands;

        public void Begin(GuiRect screen, float pixelScale)
        {
            _count = 0;
            _commands.Clear();
            _clipStack.Clear();
            _opacityStack.Clear();
            _hasCommand = false;
            ScreenRect = screen;
            PixelScale = pixelScale;
            _clip = screen;
            _opacity = 1f;
        }

        /// <summary>Closes the open draw command. Called by the renderer before drawing.</summary>
        public void End() => CloseCommand();

        // ------------------------------------------------------------ state stacks

        /// <summary>Restricts drawing to the intersection of the current clip and the given rect.</summary>
        public void PushClip(GuiRect rect)
        {
            _clipStack.Push(_clip);
            _clip = _clip.Intersect(rect);
        }

        public void PopClip()
        {
            if (_clipStack.Count > 0) _clip = _clipStack.Pop();
        }

        public void PushOpacity(float opacity)
        {
            _opacityStack.Push(_opacity);
            _opacity *= Math.Clamp(opacity, 0f, 1f);
        }

        public void PopOpacity()
        {
            if (_opacityStack.Count > 0) _opacity = _opacityStack.Pop();
        }

        public bool IsVisible(GuiRect rect) => !_clip.IsEmpty && rect.Intersects(_clip);

        // ------------------------------------------------------------ primitives

        /// <summary>Filled (optionally rounded and bordered) rectangle. Softness &gt; 0 blurs the edge (shadows, glows).</summary>
        public void AddRect(GuiRect rect, Colour fill, float cornerRadius = 0f, float borderWidth = 0f, Colour border = default, float softness = 0f)
        {
            bool hasFill = fill.IsVisible();
            bool hasBorder = borderWidth > 0f && border.IsVisible();
            if (!hasFill && !hasBorder) return;
            if (rect.Width <= 0f || rect.Height <= 0f) return;

            float expand = 1f + softness;
            if (!IsVisible(rect.Inflate(expand))) return;

            uint fillColor = fill.Pack(_opacity);
            ref GuiInstance instance = ref Allocate(0);
            instance.Center = rect.Center;
            instance.HalfSize = rect.Size * 0.5f;
            instance.Axis = AxisX;
            instance.UV = NoUV;
            instance.Color = fillColor;
            instance.BorderColor = hasBorder ? border.Pack(_opacity) : fillColor;
            instance.Params = new Vector4(MathF.Max(0f, cornerRadius), hasBorder ? borderWidth : 0f, ModeShape, MathF.Max(0f, softness));
            instance.Expand = expand;
        }

        /// <summary>Vertical gradient rectangle (top colour to bottom colour).</summary>
        public void AddRectGradient(GuiRect rect, Colour top, Colour bottom, float cornerRadius = 0f)
        {
            if (rect.Width <= 0f || rect.Height <= 0f) return;
            if (!IsVisible(rect.Inflate(1f))) return;
            ref GuiInstance instance = ref Allocate(0);
            instance.Center = rect.Center;
            instance.HalfSize = rect.Size * 0.5f;
            instance.Axis = AxisX;
            instance.UV = NoUV;
            instance.Color = top.Pack(_opacity);
            instance.BorderColor = bottom.Pack(_opacity);
            instance.Params = new Vector4(MathF.Max(0f, cornerRadius), 0f, ModeGradient, 0f);
            instance.Expand = 1f;
        }

        /// <summary>Anti-aliased line segment of any angle. Round caps extend the line by half its thickness.</summary>
        public void AddLine(Vector2 from, Vector2 to, Colour colour, float thickness = 1f, bool roundCaps = false)
        {
            if (!colour.IsVisible() || thickness <= 0f) return;
            Vector2 delta = to - from;
            float length = delta.Length;
            if (length < 0.0001f) return;

            float halfLength = length * 0.5f + (roundCaps ? thickness * 0.5f : 0f);
            float halfThickness = thickness * 0.5f;
            Vector2 center = (from + to) * 0.5f;
            // Conservative bounds: the quad fits in a square of its half diagonal.
            float reach = halfLength + halfThickness + 1f;
            if (!IsVisible(new GuiRect(center.X - reach, center.Y - reach, reach * 2f, reach * 2f))) return;

            uint packed = colour.Pack(_opacity);
            ref GuiInstance instance = ref Allocate(0);
            instance.Center = center;
            instance.HalfSize = new Vector2(halfLength, halfThickness);
            instance.Axis = delta / length;
            instance.UV = NoUV;
            instance.Color = packed;
            instance.BorderColor = packed;
            instance.Params = new Vector4(roundCaps ? halfThickness : 0f, 0f, ModeShape, 0f);
            instance.Expand = 1f;
        }

        public void AddPolyline(IReadOnlyList<Vector2> points, Colour colour, float thickness = 1f)
        {
            for (int i = 1; i < points.Count; i++) AddLine(points[i - 1], points[i], colour, thickness, roundCaps: true);
        }

        /// <summary>Circle (filled, or ring when a border is given and fill is transparent).</summary>
        public void AddCircle(Vector2 center, float radius, Colour fill, float borderWidth = 0f, Colour border = default)
        {
            AddRect(new GuiRect(center.X - radius, center.Y - radius, radius * 2f, radius * 2f), fill, radius, borderWidth, border);
        }

        /// <summary>Draws a texture (any GL texture id, e.g. a render target) into a rect.</summary>
        public void AddImage(int texture, GuiRect rect, Vector2 uv0, Vector2 uv1, Colour tint, GuiTextureMode mode = GuiTextureMode.Rgba, float cornerRadius = 0f)
        {
            if (texture <= 0 || rect.Width <= 0f || rect.Height <= 0f) return;
            if (!IsVisible(rect)) return;
            uint packed = tint.Pack(_opacity);
            ref GuiInstance instance = ref Allocate(texture);
            instance.Center = rect.Center;
            instance.HalfSize = rect.Size * 0.5f;
            instance.Axis = AxisX;
            instance.UV = new Vector4(uv0.X, uv0.Y, uv1.X, uv1.Y);
            instance.Color = packed;
            instance.BorderColor = packed;
            instance.Params = new Vector4(MathF.Max(0f, cornerRadius), 0f, (int)mode, 0f);
            instance.Expand = 0f;
        }

        /// <summary>
        /// Draws one line of text. <paramref name="lineTopLeft"/> is the top-left of the line box in GUI units.
        /// The font must be sized in physical pixels (GUI size * <see cref="PixelScale"/>); glyphs are snapped to the pixel grid.
        /// </summary>
        public void AddText(Font font, string text, int start, int length, Vector2 lineTopLeft, Colour colour)
        {
            if (!colour.IsVisible() || !IsLineVisible(font, lineTopLeft)) return;
            float scale = PixelScale;
            float penX = MathF.Round(lineTopLeft.X * scale);
            float baseline = MathF.Round(lineTopLeft.Y * scale) + font.Ascent;
            uint packed = colour.Pack(_opacity);
            Glyph? previous = null;
            int end = start + length;
            for (int i = start; i < end; i++)
            {
                int codepoint = TextLayout.ReadCodepoint(text, ref i);
                if (codepoint < 32)
                {
                    if (codepoint == '\t') penX += font.GetAdvance('\t');
                    previous = null;
                    continue;
                }
                Glyph glyph = font.GetGlyph(codepoint);
                if (previous != null) penX += font.GetKerning(previous, glyph);
                AddGlyph(glyph, MathF.Round(penX), baseline, packed);
                penX += glyph.Advance;
                previous = glyph;
            }
        }

        /// <summary>Draws a pre-shaped line of glyphs (cached by labels so text is not re-shaped every frame).</summary>
        public void AddGlyphRun(Font font, IReadOnlyList<PositionedGlyph> glyphs, Vector2 lineTopLeft, Colour colour)
        {
            if (glyphs.Count == 0 || !colour.IsVisible() || !IsLineVisible(font, lineTopLeft)) return;
            float scale = PixelScale;
            float originX = MathF.Round(lineTopLeft.X * scale);
            float baseline = MathF.Round(lineTopLeft.Y * scale) + font.Ascent;
            uint packed = colour.Pack(_opacity);
            for (int i = 0; i < glyphs.Count; i++)
            {
                PositionedGlyph positioned = glyphs[i];
                AddGlyph(positioned.Glyph, MathF.Round(originX + positioned.X), baseline, packed);
            }
        }

        private bool IsLineVisible(Font font, Vector2 lineTopLeft)
        {
            float lineBottom = lineTopLeft.Y + font.LineHeight / PixelScale;
            return !_clip.IsEmpty && lineBottom >= _clip.Top && lineTopLeft.Y <= _clip.Bottom && lineTopLeft.X <= _clip.Right;
        }

        /// <summary>Emits one glyph quad. Positions are in physical pixels and already pixel aligned.</summary>
        private void AddGlyph(Glyph glyph, float penX, float baseline, uint colour)
        {
            if (glyph.IsEmpty) return;
            float inverseScale = 1f / PixelScale;
            float x0 = (penX + glyph.OffsetX) * inverseScale;
            float y0 = (baseline + glyph.OffsetY) * inverseScale;
            float halfWidth = glyph.Width * 0.5f * inverseScale;
            float halfHeight = glyph.Height * 0.5f * inverseScale;
            float x1 = x0 + halfWidth * 2f;
            float y1 = y0 + halfHeight * 2f;
            if (x1 < _clip.Left || x0 > _clip.Right || y1 < _clip.Top || y0 > _clip.Bottom) return;

            ref GuiInstance instance = ref Allocate(glyph.Region.Texture);
            instance.Center = new Vector2(x0 + halfWidth, y0 + halfHeight);
            instance.HalfSize = new Vector2(halfWidth, halfHeight);
            instance.Axis = AxisX;
            instance.UV = new Vector4(glyph.Region.UV0.X, glyph.Region.UV0.Y, glyph.Region.UV1.X, glyph.Region.UV1.Y);
            instance.Color = colour;
            instance.BorderColor = colour;
            instance.Params = new Vector4(0f, 0f, ModeText, 0f);
            instance.Expand = 0f;
        }

        // ------------------------------------------------------------ internals

        /// <summary>
        /// Reserves an instance slot in the right draw command. A quad that samples no texture (texture 0) can join any
        /// command; a textured quad can join a command whose texture is unset or identical.
        /// </summary>
        private ref GuiInstance Allocate(int texture)
        {
            if (!_hasCommand || _commandClip != _clip)
            {
                CloseCommand();
                OpenCommand(texture);
            }
            else if (texture != 0)
            {
                if (_commandTexture == 0) _commandTexture = texture;
                else if (_commandTexture != texture)
                {
                    CloseCommand();
                    OpenCommand(texture);
                }
            }

            if (_count == _instances.Length) Array.Resize(ref _instances, _instances.Length * 2);
            return ref _instances[_count++];
        }

        private void OpenCommand(int texture)
        {
            _hasCommand = true;
            _commandTexture = texture;
            _commandClip = _clip;
            _commandFirst = _count;
        }

        private void CloseCommand()
        {
            if (!_hasCommand) return;
            _hasCommand = false;
            int count = _count - _commandFirst;
            if (count <= 0) return;
            _commands.Add(new GuiDrawCommand { Texture = _commandTexture, Clip = _commandClip, FirstInstance = _commandFirst, InstanceCount = count });
        }
    }
}
