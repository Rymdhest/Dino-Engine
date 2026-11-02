using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Text;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Text;

namespace Dino_Engine.GUI.Widgets
{
    /// <summary>
    /// Single line text input: caret, mouse and keyboard selection, word jumps (Ctrl), clipboard (Ctrl+C/X/V),
    /// select all (Ctrl+A or double click), horizontal scrolling for long text, placeholder text.
    /// While focused it reports <see cref="WantsKeyboardInput"/> so gameplay can ignore the keyboard.
    /// </summary>
    public class TextBox : GuiElement
    {
        private string _text;
        private int _caret;
        private int _anchor;
        private float _scrollX;
        private float _blinkStart;
        private float[]? _caretPositions;
        private string? _caretPositionsText;
        private Font? _caretPositionsFont;

        public string Placeholder { get; set; } = "";
        public int MaxLength { get; set; } = 1024;
        public bool ReadOnly { get; set; }
        public Colour FocusBorderColor { get; set; }

        /// <summary>Raised whenever the text changes through user input or the Text setter.</summary>
        public event Action<string>? TextChanged;
        /// <summary>Raised when Enter is pressed.</summary>
        public event Action<string>? Submitted;

        public TextBox(string text = "", string placeholder = "")
        {
            GuiTheme theme = Theme;
            _text = text ?? "";
            _caret = _anchor = _text.Length;
            Placeholder = placeholder;
            Focusable = true;
            CapturesMouse = true;
            Cursor = MouseCursor.IBeam;
            Width = 160f;
            Height = theme.ControlHeight;
            Padding = new Thickness(6f, 0f);
            BackgroundColor = theme.InputBackground;
            BorderColor = theme.ControlBorder;
            BorderWidth = theme.BorderWidth;
            CornerRadius = theme.CornerRadius;
            FocusBorderColor = theme.Accent;
        }

        public string Text
        {
            get => _text;
            set
            {
                value ??= "";
                if (value.Length > MaxLength) value = value.Substring(0, MaxLength);
                if (value == _text) return;
                _text = value;
                _caret = Math.Min(_caret, _text.Length);
                _anchor = Math.Min(_anchor, _text.Length);
                TextChanged?.Invoke(_text);
            }
        }

        public override bool WantsKeyboardInput => true;
        protected override bool WantsDrag => true;

        public bool HasSelection => _caret != _anchor;
        public int SelectionStart => Math.Min(_caret, _anchor);
        public int SelectionLength => Math.Abs(_caret - _anchor);
        public string SelectedText => _text.Substring(SelectionStart, SelectionLength);
        public int CaretIndex => _caret;

        public void SelectAll()
        {
            _anchor = 0;
            _caret = _text.Length;
            ResetBlink();
        }

        // ---------------------------------------------------------------- text metrics

        private Font? CurrentFont => Context?.GetFont(Theme.Font, Theme.FontSize);

        private float[] CaretPositions(Font font)
        {
            if (_caretPositions == null || !ReferenceEquals(_caretPositionsText, _text) || !ReferenceEquals(_caretPositionsFont, font))
            {
                _caretPositions = TextLayout.GetCaretPositions(font, _text);
                _caretPositionsText = _text;
                _caretPositionsFont = font;
            }
            return _caretPositions;
        }

        private int IndexFromMouse(float mouseX)
        {
            Font? font = CurrentFont;
            if (font == null || Context == null) return _caret;
            float local = (mouseX - ContentBounds.X + _scrollX) * Context.PixelScale;
            return SnapToCharacter(TextLayout.HitTest(CaretPositions(font), local), -1);
        }

        /// <summary>Keeps the caret off the middle of a surrogate pair.</summary>
        private int SnapToCharacter(int index, int direction)
        {
            index = Math.Clamp(index, 0, _text.Length);
            if (index > 0 && index < _text.Length && char.IsLowSurrogate(_text[index]) && char.IsHighSurrogate(_text[index - 1]))
            {
                index += direction < 0 ? -1 : 1;
            }
            return index;
        }

        private void ResetBlink() => _blinkStart = Context?.Time ?? 0f;

        // ---------------------------------------------------------------- editing

        private void ReplaceSelection(string insert)
        {
            if (ReadOnly) return;
            int start = SelectionStart;
            string without = _text.Remove(start, SelectionLength);
            int room = MaxLength - without.Length;
            if (room <= 0 && insert.Length > 0) return;
            if (insert.Length > room) insert = insert.Substring(0, room);
            _text = without.Insert(start, insert);
            _caret = _anchor = start + insert.Length;
            ResetBlink();
            TextChanged?.Invoke(_text);
        }

        private void MoveCaret(int index, bool extendSelection)
        {
            _caret = Math.Clamp(index, 0, _text.Length);
            if (!extendSelection) _anchor = _caret;
            ResetBlink();
        }

        private int PreviousWordBoundary(int index)
        {
            while (index > 0 && char.IsWhiteSpace(_text[index - 1])) index--;
            while (index > 0 && !char.IsWhiteSpace(_text[index - 1])) index--;
            return index;
        }

        private int NextWordBoundary(int index)
        {
            while (index < _text.Length && !char.IsWhiteSpace(_text[index])) index++;
            while (index < _text.Length && char.IsWhiteSpace(_text[index])) index++;
            return index;
        }

        // ---------------------------------------------------------------- input

        protected override void OnMouseDown(GuiMouseEventArgs e)
        {
            if (e.Button != MouseButton.Left) return;
            bool shift = (e.Modifiers & KeyModifiers.Shift) != 0;
            MoveCaret(IndexFromMouse(e.Position.X), shift);
            e.Handled = true;
        }

        protected override void OnDrag(GuiDragEventArgs e)
        {
            if (e.Button == MouseButton.Left) MoveCaret(IndexFromMouse(e.Position.X), true);
        }

        protected override void OnClick(GuiMouseEventArgs e)
        {
            if (e.ClickCount == 2) SelectAll();
        }

        protected override void OnFocusGained() => ResetBlink();

        protected override void OnFocusLost() => _anchor = _caret;

        protected override void OnTextInput(GuiTextEventArgs e)
        {
            if (e.Codepoint < 32 || e.Codepoint == 127) return;
            ReplaceSelection(e.Text);
            e.Handled = true;
        }

        protected override void OnKeyDown(GuiKeyEventArgs e)
        {
            bool shift = e.Shift;
            bool control = e.Control;
            switch (e.Key)
            {
                case Keys.Left:
                    if (HasSelection && !shift) MoveCaret(SelectionStart, false);
                    else MoveCaret(control ? PreviousWordBoundary(_caret) : SnapToCharacter(_caret - 1, -1), shift);
                    break;
                case Keys.Right:
                    if (HasSelection && !shift) MoveCaret(SelectionStart + SelectionLength, false);
                    else MoveCaret(control ? NextWordBoundary(_caret) : SnapToCharacter(_caret + 1, 1), shift);
                    break;
                case Keys.Home:
                    MoveCaret(0, shift);
                    break;
                case Keys.End:
                    MoveCaret(_text.Length, shift);
                    break;
                case Keys.Backspace:
                    if (!HasSelection && _caret > 0) _anchor = control ? PreviousWordBoundary(_caret) : SnapToCharacter(_caret - 1, -1);
                    ReplaceSelection("");
                    break;
                case Keys.Delete:
                    if (!HasSelection && _caret < _text.Length) _anchor = control ? NextWordBoundary(_caret) : SnapToCharacter(_caret + 1, 1);
                    ReplaceSelection("");
                    break;
                case Keys.A when control:
                    SelectAll();
                    break;
                case Keys.C when control:
                    if (HasSelection && Context != null) Context.Clipboard = SelectedText;
                    break;
                case Keys.X when control:
                    if (HasSelection && Context != null && !ReadOnly)
                    {
                        Context.Clipboard = SelectedText;
                        ReplaceSelection("");
                    }
                    break;
                case Keys.V when control:
                    if (Context != null) ReplaceSelection(SanitizeSingleLine(Context.Clipboard));
                    break;
                case Keys.Enter:
                case Keys.KeyPadEnter:
                    Submitted?.Invoke(_text);
                    break;
                case Keys.Escape:
                    Unfocus();
                    break;
                case Keys.Tab:
                    return; // let the context move focus
                default:
                    // Swallow other keys while typing so parents do not react to them.
                    break;
            }
            e.Handled = true;
        }

        private static string SanitizeSingleLine(string text)
        {
            var builder = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '\r') continue;
                builder.Append(c == '\n' || c == '\t' ? ' ' : c);
            }
            return builder.ToString();
        }

        // ---------------------------------------------------------------- drawing

        protected override void OnDraw(GuiDrawList drawList)
        {
            GuiTheme theme = Theme;
            Colour border = IsFocused ? FocusBorderColor : (IsHovered && IsEnabledInHierarchy ? theme.ScrollbarThumbHover : BorderColor);
            if (ShadowSize > 0f) DrawBackground(drawList, BackgroundColor);
            drawList.AddRect(Bounds, IsEnabledInHierarchy ? BackgroundColor : theme.ControlDisabled, CornerRadius, BorderWidth, border);

            Font? font = CurrentFont;
            if (font == null || Context == null) return;
            float scale = Context.PixelScale;
            GuiRect content = ContentBounds;
            float lineHeight = font.LineHeight / scale;
            float textY = content.Y + (content.Height - lineHeight) * 0.5f;

            drawList.PushClip(content.Inflate(1f));
            if (_text.Length == 0)
            {
                if (!IsFocused && Placeholder.Length > 0) drawList.AddText(font, Placeholder, 0, Placeholder.Length, new Vector2(content.X, textY), theme.TextMuted);
            }

            float[] positions = CaretPositions(font);
            float caretX = positions[_caret] / scale;
            float textWidth = positions[^1] / scale;
            // Keep the caret inside the visible area.
            if (IsFocused)
            {
                if (caretX - _scrollX > content.Width - 2f) _scrollX = caretX - content.Width + 2f;
                if (caretX - _scrollX < 0f) _scrollX = caretX;
            }
            _scrollX = Math.Clamp(_scrollX, 0f, MathF.Max(0f, textWidth - content.Width + 2f));
            float originX = content.X - _scrollX;

            if (IsFocused && HasSelection)
            {
                float x0 = originX + positions[SelectionStart] / scale;
                float x1 = originX + positions[SelectionStart + SelectionLength] / scale;
                drawList.AddRect(GuiRect.FromEdges(x0, textY, x1, textY + lineHeight), theme.Selection, 2f);
            }
            if (_text.Length > 0)
            {
                drawList.AddText(font, _text, 0, _text.Length, new Vector2(originX, textY), IsEnabledInHierarchy ? theme.Text : theme.TextDisabled);
            }
            if (IsFocused && !ReadOnly && ((Context.Time - _blinkStart) % 1.06f) < 0.53f)
            {
                float x = MathF.Round((originX + caretX) * scale) / scale;
                drawList.AddRect(new GuiRect(x, textY + 1f, MathF.Max(1f, 1f / scale), lineHeight - 2f), theme.Text);
            }
            drawList.PopClip();
        }
    }
}
