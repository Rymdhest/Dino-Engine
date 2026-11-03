using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Util;

namespace Dino_Engine.GUI.Debug
{
    /// <summary>Demonstrates every built-in widget; doubles as a quick visual test of the GUI framework.</summary>
    internal static class ShowcaseWindowBuilder
    {
        public static GuiWindow Build(GuiContext gui)
        {
            GuiTheme theme = gui.Theme;
            var window = new GuiWindow("Widgets", 340f) { Anchor = Anchor.Center, X = 0f, Y = 0f };
            var content = window.Content;

            content.Add(DebugUi.Muted("Everything here is built from one element type: a rectangle that can be coloured, rounded, anchored, constrained and nested. Text is rasterized at runtime from the fonts installed with the OS.", wrap: true));

            // Buttons
            content.Add(DebugUi.Heading("BUTTONS"));
            var buttons = content.Add(DebugUi.Row());
            int clicks = 0;
            var clickLabel = new Label("Clicked 0 times") { Color = theme.TextMuted, FontSize = theme.SmallFontSize };
            buttons.Add(new Button("Click me", () => clickLabel.Text = $"Clicked {++clicks} time{(clicks == 1 ? "" : "s")}") { Tooltip = "Buttons animate between normal, hover and pressed" });
            buttons.Add(new Button("Disabled") { Enabled = false });
            buttons.Add(clickLabel);

            // Toggles and values
            content.Add(DebugUi.Heading("INPUTS"));
            var check = content.Add(new Checkbox("Enable the slider below", true));
            var slider = content.Add(DebugUi.LabeledRow("Slider", new Slider(0f, 100f, 42f) { Format = "0", Step = 1f }));
            check.CheckedChanged += value => slider.Enabled = value;

            var progress = content.Add(new ProgressBar());
            progress.Bind(() => 0.5f + 0.5f * MathF.Sin(gui.Time * 1.3f));

            var dropdown = new Dropdown(new[] { "Low", "Medium", "High", "Ultra", "Custom" }, 2) { Tooltip = "Opens a popup above everything, flips upward near the screen bottom" };
            content.Add(DebugUi.LabeledRow("Dropdown", dropdown));
            var selection = content.Add(DebugUi.Muted("Selected: High"));
            dropdown.SelectionChanged += index => selection.Text = $"Selected: {dropdown.SelectedItem ?? "none"}";

            var textBox = new TextBox("", "Type and press Enter...") { Tooltip = "Ctrl+A/C/X/V, Ctrl+arrows, Shift selection, double-click selects all" };
            content.Add(DebugUi.LabeledRow("Text box", textBox));
            var echo = content.Add(DebugUi.Muted("While a text box is focused the game ignores the keyboard.", wrap: true));
            textBox.Submitted += text =>
            {
                echo.Text = text.Length > 0 ? $"You submitted: \"{text}\"" : "Nothing to submit.";
                textBox.Text = "";
            };

            // Layout demos
            var layout = content.Add(new Foldout("Layout & drawing", expanded: false));
            var anchors = layout.Content.Add(new GuiElement
            {
                Width = UIValue.Fill,
                Height = 74f,
                BackgroundColor = theme.InputBackground,
                CornerRadius = theme.CornerRadius,
                BorderColor = theme.Separator,
                BorderWidth = 1f,
                Tooltip = "Children placed with the nine anchors, each 6px in from its edge",
            });
            foreach (Anchor anchor in Enum.GetValues<Anchor>())
            {
                anchors.Add(new GuiElement { Anchor = anchor, X = anchor.ToString().Contains("Left") || anchor.ToString().Contains("Right") ? 6f : 0f, Y = anchor.ToString().StartsWith("Top") || anchor.ToString().StartsWith("Bottom") ? 6f : 0f, Width = 14f, Height = 14f, CornerRadius = 7f, BackgroundColor = theme.Accent.WithAlpha(0.85f) });
            }
            var weights = layout.Content.Add(new StackPanel(Orientation.Horizontal, 4f) { Width = UIValue.Fill, Height = 22f, Tooltip = "Fill children share space by weight: 1 : 2 : 1" });
            foreach (float weight in new[] { 1f, 2f, 1f })
            {
                var cell = weights.Add(new GuiElement { Width = UIValue.FillWeight(weight), Height = UIValue.Fill, BackgroundColor = theme.Control, BorderColor = theme.ControlBorder, BorderWidth = 1f, CornerRadius = 3f });
                cell.Add(new Label($"Fill {weight:0}") { Anchor = Anchor.Center, FontSize = theme.SmallFontSize });
            }
            var sine = layout.Content.Add(new Graph(120) { Style = GraphStyle.Line, Title = "sin", Height = 54f, FixedMax = 2.2f, ValueFormat = "0.00" });
            sine.Updated += _ => sine.Push(1.1f + MathF.Sin(gui.Time * 3f) * (0.6f + 0.4f * MathF.Sin(gui.Time * 0.7f)));
            layout.Content.Add(DebugUi.Muted("Glyph atlas (fonts rasterized at runtime):"));
            layout.Content.Add(new Image
            {
                Width = UIValue.Fill,
                AspectRatio = 4f,
                UV1 = new OpenTK.Mathematics.Vector2(1f, 0.25f),
                Mode = GuiTextureMode.RedAsGray,
                TextureSource = () => gui.Atlas.PageTextures.FirstOrDefault(),
                BorderColor = theme.Separator,
                BorderWidth = 1f,
            });
            return window;
        }
    }
}
