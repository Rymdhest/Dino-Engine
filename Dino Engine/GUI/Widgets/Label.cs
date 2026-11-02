using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Text;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.GUI.Widgets
{
    /// <summary>
    /// Text element. Sizes itself to its text by default (Width/Height Auto). Supports multiple lines ('\n'),
    /// word wrapping to the available width, and alignment inside its bounds.
    /// Font, size and colour fall back to the theme while unset, so theme changes apply live.
    ///
    /// Performance: line breaks and glyph positions are cached until the text, font or wrap width changes, and a
    /// text change that keeps the same measured size (e.g. a live number in a monospace font) does not trigger a
    /// layout pass.
    /// </summary>
    public class Label : GuiElement
    {
        private string _text;
        private FontFace? _font;
        private float _fontSize;
        private bool _wrap;

        private readonly List<TextLine> _lines = new();
        private Font? _linesFont;
        private string? _linesText;
        private float _linesWrapWidth = float.NaN;

        private readonly List<List<PositionedGlyph>> _shapedLines = new();
        private bool _shapedValid;

        private bool _hasMeasured;
        private Font? _measuredFont;
        private Vector2 _measuredTextPixels;

        public Label(string text = "")
        {
            _text = text ?? "";
            HitTest = HitTestMode.PassThrough;
        }

        public string Text
        {
            get => _text;
            set
            {
                value ??= "";
                if (value == _text) return;
                _text = value;
                if (!SizeUnchangedAfterTextChange()) InvalidateLayout();
            }
        }

        /// <summary>Font face; null uses the theme font.</summary>
        public FontFace? Font { get => _font; set => SetLayoutField(ref _font, value); }
        /// <summary>Font size in GUI units; 0 uses the theme font size.</summary>
        public float FontSize { get => _fontSize; set => SetLayoutField(ref _fontSize, value); }
        /// <summary>Text colour; null uses the theme text colour (or the disabled colour when disabled).</summary>
        public Colour? Color { get; set; }
        /// <summary>Wrap lines at word boundaries to fit the available width.</summary>
        public bool Wrap { get => _wrap; set => SetLayoutField(ref _wrap, value); }
        public Alignment HorizontalTextAlignment { get; set; } = Alignment.Start;
        public Alignment VerticalTextAlignment { get; set; } = Alignment.Center;

        public FontFace ResolvedFont => _font ?? Theme.Font;
        public float ResolvedFontSize => _fontSize > 0f ? _fontSize : Theme.FontSize;

        /// <summary>Re-evaluates the text every frame. Handy for live debug values: <c>label.Bind(() =&gt; $"FPS {fps}")</c>.</summary>
        public Label Bind(Func<string> getter)
        {
            Updated += _ => Text = getter();
            return this;
        }

        private IReadOnlyList<TextLine> GetLines(Font font, float wrapWidthPixels)
        {
            if (!ReferenceEquals(font, _linesFont) || !ReferenceEquals(_text, _linesText) || !SameWidth(wrapWidthPixels, _linesWrapWidth))
            {
                TextLayout.BreakLines(font, _text, wrapWidthPixels, _lines);
                _linesFont = font;
                _linesText = _text;
                _linesWrapWidth = wrapWidthPixels;
                _shapedValid = false;
            }
            return _lines;
        }

        private static bool SameWidth(float a, float b) => (float.IsPositiveInfinity(a) && float.IsPositiveInfinity(b)) || MathF.Abs(a - b) < 0.01f;

        private static Vector2 TextPixels(Font font, IReadOnlyList<TextLine> lines)
        {
            float width = 0f;
            for (int i = 0; i < lines.Count; i++) width = MathF.Max(width, lines[i].Width);
            return new Vector2(MathF.Ceiling(width), lines.Count * font.LineHeight);
        }

        /// <summary>
        /// For unwrapped text: re-breaks the new text (the result is cached for drawing anyway) and reports whether it
        /// measures exactly the same as at the last layout, in which case no layout pass is needed.
        /// </summary>
        private bool SizeUnchangedAfterTextChange()
        {
            if (Context == null || _wrap || !_hasMeasured) return false;
            Font font = Context.GetFont(ResolvedFont, ResolvedFontSize);
            if (!ReferenceEquals(font, _measuredFont)) return false;
            return TextPixels(font, GetLines(font, float.PositiveInfinity)) == _measuredTextPixels;
        }

        protected override Vector2 MeasureContent(Vector2 reference, Vector2 available)
        {
            Vector2 childSize = base.MeasureContent(reference, available);
            if (Context == null) return childSize;
            float scale = Context.PixelScale;
            Font font = Context.GetFont(ResolvedFont, ResolvedFontSize);
            float wrap = _wrap && float.IsFinite(available.X) ? available.X * scale : float.PositiveInfinity;
            Vector2 pixels = TextPixels(font, GetLines(font, wrap));
            _hasMeasured = true;
            _measuredFont = font;
            _measuredTextPixels = pixels;
            return Vector2.ComponentMax(childSize, pixels / scale);
        }

        protected override void OnDraw(GuiDrawList drawList)
        {
            base.OnDraw(drawList);
            if (Context == null || _text.Length == 0) return;

            float scale = Context.PixelScale;
            Font font = Context.GetFont(ResolvedFont, ResolvedFontSize);
            GuiRect content = ContentBounds;
            // Tolerance: pixel snapping of the bounds may shave a fraction off the measured width.
            float wrap = _wrap ? content.Width * scale + 1.5f : float.PositiveInfinity;
            var lines = GetLines(font, wrap);
            if (!_shapedValid) ShapeLines(font);

            float lineHeight = font.LineHeight / scale;
            float totalHeight = lines.Count * lineHeight;
            float y = VerticalTextAlignment switch
            {
                Alignment.Center => content.Y + (content.Height - totalHeight) * 0.5f,
                Alignment.End => content.Bottom - totalHeight,
                _ => content.Y,
            };

            Colour colour = Color ?? (IsEnabledInHierarchy ? Theme.Text : Theme.TextDisabled);
            for (int i = 0; i < lines.Count; i++)
            {
                float lineWidth = lines[i].Width / scale;
                float x = HorizontalTextAlignment switch
                {
                    Alignment.Center => content.X + (content.Width - lineWidth) * 0.5f,
                    Alignment.End => content.Right - lineWidth,
                    _ => content.X,
                };
                drawList.AddGlyphRun(font, _shapedLines[i], new Vector2(x, y), colour);
                y += lineHeight;
            }
        }

        private void ShapeLines(Font font)
        {
            while (_shapedLines.Count < _lines.Count) _shapedLines.Add(new List<PositionedGlyph>());
            for (int i = 0; i < _lines.Count; i++)
            {
                TextLayout.ShapeLine(font, _text, _lines[i].Start, _lines[i].Length, _shapedLines[i]);
            }
            _shapedValid = true;
        }
    }
}
