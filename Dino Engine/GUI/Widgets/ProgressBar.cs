using Dino_Engine.GUI.Rendering;
using Dino_Engine.Util;

namespace Dino_Engine.GUI.Widgets
{
    /// <summary>Horizontal bar filled to <see cref="Value"/> (0..1) with optional centred text.</summary>
    public class ProgressBar : GuiElement
    {
        private float _value;

        public float Value { get => _value; set => _value = Math.Clamp(float.IsFinite(value) ? value : 0f, 0f, 1f); }
        public Colour FillColor { get; set; }
        /// <summary>Text drawn over the bar. Null shows the percentage, empty shows nothing.</summary>
        public string? Text { get; set; }

        public ProgressBar(float value = 0f)
        {
            GuiTheme theme = Theme;
            Value = value;
            Width = UIValue.Fill;
            Height = 16f;
            CornerRadius = theme.CornerRadius;
            BackgroundColor = theme.InputBackground;
            BorderColor = theme.Separator;
            BorderWidth = 1f;
            FillColor = theme.Accent;
        }

        /// <summary>Reads the value every frame.</summary>
        public ProgressBar Bind(Func<float> getter)
        {
            Updated += _ => Value = getter();
            return this;
        }

        protected override void OnDraw(GuiDrawList drawList)
        {
            base.OnDraw(drawList);
            GuiRect inner = Bounds.Deflate(new Thickness(2f));
            if (_value > 0f && inner.Width > 0f)
            {
                GuiRect fill = new GuiRect(inner.X, inner.Y, MathF.Max(inner.Height, inner.Width * _value), inner.Height);
                fill.Width = MathF.Min(fill.Width, inner.Width);
                drawList.AddRect(fill, FillColor, MathF.Max(0f, CornerRadius - 2f));
            }
            string text = Text ?? $"{_value * 100f:0}%";
            if (text.Length > 0 && Context != null)
            {
                GuiTheme theme = Theme;
                GuiText.Draw(Context, drawList, text, theme.Font, theme.SmallFontSize, Bounds, theme.Text, Alignment.Center);
            }
        }
    }
}
