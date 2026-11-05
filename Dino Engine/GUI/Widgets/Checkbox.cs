using Dino_Engine.GUI.Rendering;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;

using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;

namespace Dino_Engine.GUI.Widgets
{
    /// <summary>A box with a check mark followed by a label. Clicking anywhere on it toggles <see cref="Checked"/>.</summary>
    public class Checkbox : GuiElement
    {
        private bool _checked;

        public Label Label { get; }
        public string Text
        {
            get => Label.Text;
            set
            {
                Label.Text = value;
                Label.Visible = !string.IsNullOrEmpty(value);
            }
        }
        public Colour BoxColor { get; set; }
        public Colour BoxHoverColor { get; set; }
        public Colour CheckedColor { get; set; }
        public Colour CheckMarkColor { get; set; } = GuiColors.Rgba(18, 22, 20);

        public event Action<bool>? CheckedChanged;

        public Checkbox(string text = "", bool isChecked = false)
        {
            GuiTheme theme = Theme;
            _checked = isChecked;
            CapturesMouse = true;
            Cursor = MouseCursor.Hand;
            MinHeight = theme.ControlHeight - 4f;
            BoxColor = theme.InputBackground;
            BoxHoverColor = theme.ControlHover;
            CheckedColor = theme.Accent;
            Layout = new StackLayout(Orientation.Horizontal, 7f) { CrossAlignment = Alignment.Center };
            Add(new CheckBoxGlyph(this) { Width = 16f, Height = 16f });
            Label = Add(new Label(text) { HitTest = HitTestMode.None });
            Label.Visible = text.Length > 0;
        }

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked == value) return;
                _checked = value;
                CheckedChanged?.Invoke(value);
            }
        }

        /// <summary>Keeps the checkbox in sync with external state: reads <paramref name="getter"/> every frame and calls <paramref name="setter"/> on user toggles.</summary>
        public Checkbox Bind(Func<bool> getter, Action<bool> setter)
        {
            Updated += _ =>
            {
                bool value = getter();
                if (value != _checked) _checked = value; // no event: the change came from outside
            };
            CheckedChanged += setter;
            return this;
        }

        protected override void OnClick(GuiMouseEventArgs e)
        {
            if (e.Button != MouseButton.Left) return;
            Audio.Play(Interface.Toggle);
            Checked = !Checked;
        }

        private sealed class CheckBoxGlyph : GuiElement
        {
            private readonly Checkbox _owner;

            public CheckBoxGlyph(Checkbox owner)
            {
                _owner = owner;
                HitTest = HitTestMode.None;
            }

            protected override void OnDraw(GuiDrawList drawList)
            {
                GuiTheme theme = Theme;
                bool enabled = _owner.IsEnabledInHierarchy;
                GuiRect box = Bounds;
                if (_owner.Checked)
                {
                    Colour fill = enabled ? (_owner.IsHovered ? theme.AccentHover : _owner.CheckedColor) : theme.ControlDisabled;
                    drawList.AddRect(box, fill, theme.CornerRadius - 1f);
                    // Check mark as two anti-aliased strokes.
                    Vector2 a = new Vector2(box.X + box.Width * 0.24f, box.Y + box.Height * 0.52f);
                    Vector2 b = new Vector2(box.X + box.Width * 0.43f, box.Y + box.Height * 0.71f);
                    Vector2 c = new Vector2(box.X + box.Width * 0.77f, box.Y + box.Height * 0.31f);
                    Colour mark = enabled ? _owner.CheckMarkColor : theme.TextDisabled;
                    drawList.AddLine(a, b, mark, 2f, roundCaps: true);
                    drawList.AddLine(b, c, mark, 2f, roundCaps: true);
                }
                else
                {
                    Colour fill = enabled && _owner.IsHovered ? _owner.BoxHoverColor : _owner.BoxColor;
                    drawList.AddRect(box, fill, theme.CornerRadius - 1f, 1f, enabled ? theme.ControlBorder : theme.Separator);
                }
            }
        }
    }
}
