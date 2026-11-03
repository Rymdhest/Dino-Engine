using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Util;

namespace Dino_Engine.GUI.Debug
{
    /// <summary>Small builders shared by the debug windows.</summary>
    internal static class DebugUi
    {
        private static GuiTheme Theme => GuiContext.DefaultTheme;

        /// <summary>Debug output uses invariant formatting so numbers look the same on every machine (no decimal commas).</summary>
        public static readonly System.Globalization.CultureInfo Culture = System.Globalization.CultureInfo.InvariantCulture;

        public static string Inv(FormattableString text) => FormattableString.Invariant(text);

        /// <summary>Two column grid for "name: value" rows.</summary>
        public static GridPanel KeyValueGrid(float keyWeight = 0.42f) => new GridPanel(2, 8f, 1f, keyWeight, 1f - keyWeight);

        public static Label AddRow(GridPanel grid, string key, Func<string> value)
        {
            grid.Add(new Label(key) { Color = Theme.TextMuted, FontSize = Theme.SmallFontSize + 1f });
            return grid.Add(new Label("") { Font = Theme.MonoFont, FontSize = Theme.SmallFontSize + 1f }.Bind(value));
        }

        /// <summary>A labelled control on one line: muted caption on the left, the control filling the rest.</summary>
        public static GuiElement LabeledRow(string caption, GuiElement control, float captionWidth = 92f)
        {
            var row = new StackPanel(Orientation.Horizontal, 6f) { Width = UIValue.Fill };
            row.Stack!.CrossAlignment = Alignment.Center;
            row.Add(new Label(caption) { Width = captionWidth, Color = Theme.TextMuted, FontSize = Theme.SmallFontSize + 1f });
            control.Width = UIValue.Fill;
            row.Add(control);
            return row;
        }

        public static Label Muted(string text, bool wrap = false) => new Label(text) { Color = Theme.TextMuted, FontSize = Theme.SmallFontSize, Wrap = wrap, Width = wrap ? UIValue.Fill : UIValue.Auto };

        public static Label Heading(string text) => new Label(text) { Color = Theme.Accent, FontSize = Theme.SmallFontSize, Margin = new Thickness(0f, 4f, 0f, 0f) };

        public static StackPanel Row(float spacing = 6f)
        {
            var row = new StackPanel(Orientation.Horizontal, spacing) { Width = UIValue.Fill };
            row.Stack!.CrossAlignment = Alignment.Center;
            return row;
        }
    }

    /// <summary>A row with a name, a value and a proportional bar behind them (used for timings).</summary>
    internal sealed class BarRow : GuiElement
    {
        public Label NameLabel { get; }
        public Label ValueLabel { get; }
        public float Fraction { get; set; }
        public Colour BarColor { get; set; }

        public BarRow()
        {
            GuiTheme theme = Theme;
            Width = UIValue.Fill;
            Height = 18f;
            Padding = new Thickness(5f, 0f);
            CornerRadius = 3f;
            BarColor = theme.Accent.WithAlpha(0.22f);
            Layout = new StackLayout(Orientation.Horizontal, 6f) { CrossAlignment = Alignment.Center };
            NameLabel = Add(new Label("") { Width = UIValue.Fill, FontSize = theme.SmallFontSize, MaxHeight = 18f });
            ValueLabel = Add(new Label("") { Width = 70f, Font = theme.MonoFont, FontSize = theme.SmallFontSize, HorizontalTextAlignment = Alignment.End });
            ClipChildren = true;
        }

        protected override void OnDraw(GuiDrawList drawList)
        {
            drawList.AddRect(Bounds, Theme.Highlight.WithAlpha(0.03f), CornerRadius);
            float width = Bounds.Width * Math.Clamp(Fraction, 0f, 1f);
            if (width > 0.5f) drawList.AddRect(new GuiRect(Bounds.X, Bounds.Y, width, Bounds.Height), BarColor, CornerRadius);
        }
    }
}
