using Dino_Engine.Core;
using Dino_Engine.Debug;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Rendering;
using Dino_Engine.Util;

namespace Dino_Engine.GUI.Debug
{
    /// <summary>Lists the engine's GPU passes and CPU tasks (from <see cref="PerformanceMonitor"/>) sorted by cost, and what video memory the game holds (<see cref="VideoMemory"/>).</summary>
    internal sealed class PerformanceWindowController
    {
        private const float RefreshInterval = 0.5f;

        private const double Megabyte = 1024.0 * 1024.0;

        private readonly Foldout _memorySection;
        private readonly Label _memory;
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

            _memorySection = scroll.Content.Add(new Foldout("Video memory"));
            _memory = _memorySection.Content.Add(new Label("")
            {
                Font = theme.MonoFont,
                FontSize = theme.SmallFontSize,
                Tooltip = "In use: everything the game holds on the card, as the driver counts it, of what it may hold before the system moves some out to RAM.\n"
                    + "In RAM: what the driver keeps for it in system memory instead.\nMeshes: every model's buffers, and how many models, with who holds them.\n"
                    + "Other: the rest of In use, which is textures, render targets and the driver's own.",
            });
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

            if (_memorySection.Expanded) _memory.Text = MemoryText();
            PerformanceMonitor monitor = Engine.PerformanceMonitor;
            Fill(_gpuSection, _gpuTotal, monitor.GPUTaskTrackers, "GPU");
            Fill(_cpuSection, _cpuTotal, monitor.CPUTaskTrackers, "CPU");
        }

        // The driver's figure for the whole process, then what meshes take of it and who holds them, and what's left, which is textures, render targets and the driver's own.
        private static string MemoryText()
        {
            var text = new System.Text.StringBuilder();
            VideoMemory.Usage? usage = VideoMemory.Read();
            (long meshBytes, int models) = VideoMemory.Meshes;
            if (usage is VideoMemory.Usage used)
            {
                text.AppendLine(used.Adapter);
                text.AppendLine(DebugUi.Inv($"In use      {used.Used / Megabyte,5:0} MB of {used.Budget / Megabyte:0}"));
                text.AppendLine(DebugUi.Inv($"In RAM      {used.SharedUsed / Megabyte,5:0} MB"));
            }
            else text.AppendLine("The driver doesn't say.");
            text.AppendLine(DebugUi.Inv($"Meshes      {meshBytes / Megabyte,5:0} MB {models,5} models"));
            foreach (VideoMemory.Holder holder in VideoMemory.ByHolder())
            {
                text.AppendLine(DebugUi.Inv($"  {holder.Name,-10}{holder.Bytes / Megabyte,5:0} MB {holder.Models,5}"));
            }
            if (usage is VideoMemory.Usage rest) text.Append(DebugUi.Inv($"Other       {(rest.Used - meshBytes) / Megabyte,5:0} MB"));
            return text.ToString().TrimEnd();
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
