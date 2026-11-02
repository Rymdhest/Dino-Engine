using Dino_Engine.GUI.Rendering;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Engine.GUI.Widgets
{
    /// <summary>Horizontal value slider. Click or drag to set the value; optionally shows the value text on the right.</summary>
    public class Slider : GuiElement
    {
        private float _min;
        private float _max;
        private float _value;

        public float Step { get; set; }
        /// <summary>Shows the formatted value to the right of the track.</summary>
        public bool ShowValue { get; set; } = true;
        /// <summary>.NET numeric format string for the value text.</summary>
        public string Format { get; set; } = "0.00";
        /// <summary>Culture used to format the value text; null uses the current culture.</summary>
        public IFormatProvider? FormatProvider { get; set; }
        public float ValueTextWidth { get; set; } = 48f;
        public Colour TrackColor { get; set; }
        public Colour FillColor { get; set; }
        public Colour ThumbColor { get; set; } = GuiColors.Rgba(235, 238, 242);

        public event Action<float>? ValueChanged;

        public Slider(float min = 0f, float max = 1f, float value = 0f)
        {
            GuiTheme theme = Theme;
            _min = min;
            _max = max;
            _value = Math.Clamp(value, MathF.Min(min, max), MathF.Max(min, max));
            CapturesMouse = true;
            Cursor = MouseCursor.Hand;
            Width = 160f;
            Height = theme.ControlHeight - 4f;
            TrackColor = theme.InputBackground;
            FillColor = theme.Accent;
        }

        public float Min { get => _min; set { _min = value; Value = _value; } }
        public float Max { get => _max; set { _max = value; Value = _value; } }

        public float Value
        {
            get => _value;
            set
            {
                float clamped = Math.Clamp(value, MathF.Min(_min, _max), MathF.Max(_min, _max));
                if (clamped == _value) return;
                _value = clamped;
                ValueChanged?.Invoke(_value);
            }
        }

        /// <summary>Value mapped to 0..1.</summary>
        public float NormalizedValue => _max == _min ? 0f : (_value - _min) / (_max - _min);

        /// <summary>Reads <paramref name="getter"/> every frame and calls <paramref name="setter"/> when the user changes the value.</summary>
        public Slider Bind(Func<float> getter, Action<float> setter)
        {
            Updated += _ =>
            {
                if (IsPressed) return; // do not fight the user while dragging
                float external = Math.Clamp(getter(), MathF.Min(_min, _max), MathF.Max(_min, _max));
                if (external != _value) _value = external;
            };
            ValueChanged += setter;
            return this;
        }

        protected override bool WantsDrag => true;

        private GuiRect TrackRect
        {
            get
            {
                GuiRect content = ContentBounds;
                float right = ShowValue ? content.Right - ValueTextWidth : content.Right;
                const float thumbRadius = 7f;
                return GuiRect.FromEdges(content.X + thumbRadius, content.Y, MathF.Max(content.X + thumbRadius, right - thumbRadius), content.Bottom);
            }
        }

        private void SetFromMouse(float mouseX)
        {
            GuiRect track = TrackRect;
            float t = track.Width > 0f ? Math.Clamp((mouseX - track.X) / track.Width, 0f, 1f) : 0f;
            float value = _min + t * (_max - _min);
            if (Step > 0f) value = _min + MathF.Round((value - _min) / Step) * Step;
            Value = value;
        }

        protected override void OnMouseDown(GuiMouseEventArgs e)
        {
            if (e.Button != MouseButton.Left) return;
            SetFromMouse(e.Position.X);
            e.Handled = true;
        }

        protected override void OnDrag(GuiDragEventArgs e) => SetFromMouse(e.Position.X);

        protected override void OnDraw(GuiDrawList drawList)
        {
            base.OnDraw(drawList);
            GuiTheme theme = Theme;
            bool enabled = IsEnabledInHierarchy;
            GuiRect track = TrackRect;
            float centerY = track.Y + track.Height * 0.5f;
            const float trackHeight = 4f;
            GuiRect bar = new GuiRect(track.X - 3f, centerY - trackHeight * 0.5f, track.Width + 6f, trackHeight);
            drawList.AddRect(bar, TrackColor, trackHeight * 0.5f, 1f, theme.Separator);

            float thumbX = track.X + track.Width * NormalizedValue;
            Colour fill = enabled ? FillColor : theme.ControlDisabled;
            drawList.AddRect(GuiRect.FromEdges(bar.X, bar.Y, thumbX, bar.Bottom), fill, trackHeight * 0.5f);

            bool active = enabled && (IsHovered || IsPressed);
            float radius = active ? 7f : 6f;
            drawList.AddCircle(new Vector2(thumbX, centerY), radius + 1f, GuiColors.Rgba(0, 0, 0, 90));
            drawList.AddCircle(new Vector2(thumbX, centerY), radius, enabled ? ThumbColor : theme.TextDisabled);

            if (ShowValue && Context != null)
            {
                GuiRect textRect = GuiRect.FromEdges(ContentBounds.Right - ValueTextWidth + 4f, ContentBounds.Y, ContentBounds.Right, ContentBounds.Bottom);
                GuiText.Draw(Context, drawList, _value.ToString(Format, FormatProvider), theme.MonoFont, theme.SmallFontSize, textRect, enabled ? theme.Text : theme.TextDisabled, Alignment.End);
            }
        }
    }
}
