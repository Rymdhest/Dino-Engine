using Dino_Engine.Core;
using Dino_Engine.Debug;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Util;

namespace Dino_Engine.GUI.Debug
{
    /// <summary>Lists the engine's GPU passes and CPU tasks (from <see cref="PerformanceMonitor"/>) sorted by cost.</summary>
    internal sealed class PerformanceWindowController
    {
        private const float RefreshInterval = 0.5f;

        private readonly Foldout _gpuSection;
        private readonly Foldout _cpuSection;
        private readonly Label _gpuTotal;
        private readonly Label _cpuTotal;
        private readonly Checkbox _hideIdle;
        private float _timer = RefreshInterval;

        public GuiWindow Window { get; }

        public PerformanceWindowController()
        {
            GuiTheme theme = GuiContext.DefaultTheme;
            Window = new GuiWindow("Performance", 360f, 520f) { Anchor = Anchor.TopRight, X = 12f, Y = 56f };

            Window.Content.Add(DebugUi.Muted("Average time per frame over the last second.", wrap: true));
            _hideIdle = Window.Content.Add(new Checkbox("Hide tasks under 0.01 ms", true));

            var scroll = Window.Content.Add(new ScrollPanel { Width = UIValue.Fill, Height = UIValue.Fill });

            _gpuSection = scroll.Content.Add(new Foldout("GPU passes"));
            _gpuTotal = _gpuSection.Content.Add(new Label("") { Font = theme.MonoFont, FontSize = theme.SmallFontSize, Color = theme.Accent });
            _cpuSection = scroll.Content.Add(new Foldout("CPU tasks"));
            _cpuTotal = _cpuSection.Content.Add(new Label("") { Font = theme.MonoFont, FontSize = theme.SmallFontSize, Color = theme.Accent });
            ((StackLayout)_gpuSection.Content.Layout!).Spacing = 1f;
            ((StackLayout)_cpuSection.Content.Layout!).Spacing = 1f;

            Window.Updated += Update;
        }

        private void Update(float deltaTime)
        {
            _timer += deltaTime;
            if (_timer < RefreshInterval) return;
            _timer = 0f;

            PerformanceMonitor monitor = Engine.PerformanceMonitor;
            Fill(_gpuSection, _gpuTotal, monitor.GPUTaskTrackers, "GPU");
            Fill(_cpuSection, _cpuTotal, monitor.CPUTaskTrackers, "CPU");
        }

        private void Fill(Foldout section, Label totalLabel, IReadOnlyCollection<TaskTracker> trackers, string kind)
        {
            if (!section.Expanded) return;
            GuiTheme theme = GuiContext.DefaultTheme;
            var sorted = trackers.OrderByDescending(t => t.averageTimePerTaskLastSecond).ToList();
            double total = sorted.Sum(t => t.averageTimePerTaskLastSecond) / 1_000_000.0;
            double max = sorted.Count > 0 ? sorted[0].averageTimePerTaskLastSecond / 1_000_000.0 : 0.0;
            if (_hideIdle.Checked) sorted.RemoveAll(t => t.averageTimePerTaskLastSecond < 10_000); // 0.01 ms in ns
            totalLabel.Text = DebugUi.Inv($"{kind} total {total:0.000} ms ({sorted.Count} tasks)");

            // Children after the total label are rows; reuse them.
            var content = section.Content;
            int rowCount = content.Children.Count - 1;
            while (rowCount < sorted.Count)
            {
                content.Add(new BarRow());
                rowCount++;
            }
            while (rowCount > sorted.Count)
            {
                content.Remove(content.Children[^1]);
                rowCount--;
            }

            for (int i = 0; i < sorted.Count; i++)
            {
                var row = (BarRow)content.Children[i + 1];
                double milliseconds = sorted[i].averageTimePerTaskLastSecond / 1_000_000.0;
                row.NameLabel.Text = sorted[i].Name;
                row.ValueLabel.Text = DebugUi.Inv($"{milliseconds:0.000} ms");
                row.Fraction = max > 0.0 ? (float)(milliseconds / max) : 0f;
                row.BarColor = milliseconds > 4.0 ? theme.Warning.WithAlpha(0.3f) : theme.Accent.WithAlpha(0.22f);
                row.Tooltip = DebugUi.Inv($"{sorted[i].Name}\n{milliseconds:0.0000} ms ({(total > 0 ? milliseconds / total * 100.0 : 0):0.0}% of {kind} total)");
            }
        }
    }
}
