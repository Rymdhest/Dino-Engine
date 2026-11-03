using Dino_Engine.GUI.Widgets;
using Dino_Engine.Util;

namespace Dino_Engine.GUI.Debug
{
    /// <summary>Shows captured <see cref="Console"/> output with filtering, pause and clear.</summary>
    internal sealed class ConsoleWindowController
    {
        private const int MaxStoredLines = 1000;
        private const int MaxShownLines = 300;

        private readonly ConsoleCapture _capture;
        private readonly List<string> _lines = new();
        private readonly ScrollPanel _scroll;
        private readonly TextBox _filter;
        private readonly Checkbox _hideSpam;
        private readonly Checkbox _pause;
        private readonly Label _count;
        private bool _dirty = true;
        private bool _insidePerformanceReport;

        public GuiWindow Window { get; }

        public ConsoleWindowController(ConsoleCapture capture)
        {
            _capture = capture;
            Window = new GuiWindow("Console", 620f, 320f) { Anchor = Anchor.BottomLeft, X = 12f, Y = 12f };

            var toolbar = Window.Content.Add(DebugUi.Row());
            _filter = toolbar.Add(new TextBox("", "Filter...") { Width = UIValue.Fill });
            _filter.TextChanged += _ => _dirty = true;
            _hideSpam = toolbar.Add(new Checkbox("Hide FPS/perf reports", true) { Tooltip = "Hide the per-second FPS line and performance report the engine prints" });
            _hideSpam.CheckedChanged += _ => _dirty = true;
            _pause = toolbar.Add(new Checkbox("Pause"));
            toolbar.Add(new Button("Clear", () =>
            {
                _lines.Clear();
                _dirty = true;
            }));

            _scroll = Window.Content.Add(new ScrollPanel { Width = UIValue.Fill, Height = UIValue.Fill, AutoScrollToBottom = true, BackgroundColor = GuiContext.DefaultTheme.InputBackground.WithAlpha(0.6f), CornerRadius = 3f, Padding = 4f });
            ((StackLayout)_scroll.Content.Layout!).Spacing = 0f;
            _count = Window.Content.Add(DebugUi.Muted(""));

            Window.Updated += _ =>
            {
                if (_dirty) Rebuild();
            };
        }

        /// <summary>Drains captured output. Called every frame, even while the console is hidden.</summary>
        public void Poll()
        {
            bool received = false;
            while (_capture.TryDequeueLine(out string line))
            {
                if (_pause.Checked) continue;
                _lines.Add(line);
                received = true;
            }
            if (_lines.Count > MaxStoredLines) _lines.RemoveRange(0, _lines.Count - MaxStoredLines);
            if (received) _dirty = true;
        }

        private bool IsSpam(string line)
        {
            // The engine prints "FPS; n" every second followed by a multi-line performance report.
            if (line.StartsWith("FPS; ")) return true;
            if (line.StartsWith("PERFORMANCE REPORT")) { _insidePerformanceReport = true; return true; }
            if (_insidePerformanceReport)
            {
                if (line.StartsWith("TOTA")) _insidePerformanceReport = false;
                return true;
            }
            return false;
        }

        private void Rebuild()
        {
            _dirty = false;
            GuiTheme theme = GuiContext.DefaultTheme;
            string filter = _filter.Text;
            var shown = new List<string>();
            _insidePerformanceReport = false;
            foreach (string line in _lines)
            {
                if (_hideSpam.Checked && IsSpam(line)) continue;
                if (line.Length == 0) continue;
                if (filter.Length > 0 && !line.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
                shown.Add(line);
            }
            int skip = Math.Max(0, shown.Count - MaxShownLines);
            int count = shown.Count - skip;

            var content = _scroll.Content;
            while (content.Children.Count < count) content.Add(new Label("") { Width = UIValue.Fill, Wrap = true, Font = theme.MonoFont, FontSize = theme.SmallFontSize });
            while (content.Children.Count > count) content.Remove(content.Children[^1]);
            for (int i = 0; i < count; i++)
            {
                string line = shown[skip + i];
                var label = (Label)content.Children[i];
                label.Text = line;
                label.Color = ColourFor(line, theme);
            }
            _count.Text = skip > 0 ? $"Showing last {count} of {shown.Count} lines" : $"{count} lines";
        }

        private static Colour ColourFor(string line, GuiTheme theme)
        {
            if (line.Contains("error", StringComparison.OrdinalIgnoreCase) || line.Contains("exception", StringComparison.OrdinalIgnoreCase) || line.Contains("could not", StringComparison.OrdinalIgnoreCase)) return theme.Error;
            if (line.Contains("warning", StringComparison.OrdinalIgnoreCase) || line.Contains("went wrong", StringComparison.OrdinalIgnoreCase)) return theme.Warning;
            return theme.Text;
        }
    }
}
