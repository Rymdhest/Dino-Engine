using Dino_Engine.Core;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Engine.GUI.Debug
{
    /// <summary>
    /// Built-in developer tools for any game made with the engine: live stats and frame graph, GPU/CPU timings,
    /// render controls and G-buffer viewer, ECS inspector, captured console output and a widget showcase.
    /// Hidden at start; toggle everything with <see cref="ToggleKey"/> (F3). Windows can be opened from the toolbar.
    /// </summary>
    public class DebugOverlay
    {
        private readonly GuiContext _gui;
        private readonly GuiElement _root;
        private readonly Graph _frameGraph;
        private readonly List<(Button button, GuiWindow window)> _toolbarToggles = new();
        private float _pendingScale;
        private bool _scaleDirty;
        private int _framesSeen;

        public Keys ToggleKey { get; set; } = Keys.F3;
        public GuiWindow StatsWindow { get; }
        public GuiWindow PerformanceWindow { get; }
        public GuiWindow RenderWindow { get; }
        public GuiWindow EntitiesWindow { get; }
        public GuiWindow ConsoleWindow { get; }
        public GuiWindow ShowcaseWindow { get; }
        public GuiWindow SoundWindow { get; }
        public GuiElement Toolbar { get; }

        private readonly ConsoleWindowController _console;
        private readonly PerformanceWindowController _performance;
        private readonly EntityInspectorController _entities;
        private readonly AudioWindowController _sound;

        public DebugOverlay(GuiContext gui)
        {
            _gui = gui;
            ConsoleCapture capture = ConsoleCapture.Install();

            _root = gui.Root.Add(new GuiElement { Name = "DebugOverlay", Width = UIValue.Fill, Height = UIValue.Fill, HitTest = HitTestMode.PassThrough });

            _frameGraph = new Graph(240) { Title = "Frame", Unit = "ms", ValueFormat = "0.0", MinimumRange = 20f, WarningThreshold = 20f, Height = 64f, FormatProvider = DebugUi.Culture };
            StatsWindow = _root.Add(BuildStatsWindow());

            _performance = new PerformanceWindowController();
            PerformanceWindow = _root.Add(_performance.Window);

            RenderWindow = _root.Add(RenderWindowBuilder.Build());

            _entities = new EntityInspectorController();
            EntitiesWindow = _root.Add(_entities.Window);

            _console = new ConsoleWindowController(capture);
            ConsoleWindow = _root.Add(_console.Window);

            ShowcaseWindow = _root.Add(ShowcaseWindowBuilder.Build(gui));

            _sound = new AudioWindowController();
            SoundWindow = _root.Add(_sound.Window);

            Toolbar = _root.Add(BuildToolbar());

            RenderWindow.Visible = false;
            EntitiesWindow.Visible = false;
            ConsoleWindow.Visible = false;
            ShowcaseWindow.Visible = false;
            SoundWindow.Visible = false;

            // Hidden until F3 is pressed.
            Visible = false;
        }

        public bool Visible
        {
            get => _root.Visible;
            set => _root.Visible = value;
        }

        /// <summary>Called by the engine every frame before the GUI update.</summary>
        public void Update(float deltaTime)
        {
            if (Engine.WindowHandler.IsKeyPressed(ToggleKey) && !_gui.WantsKeyboard) Visible = !Visible;

            // Recorded even while hidden so the history is there when the overlay is opened.
            // The first frames include engine start-up (texture generation etc.) and would dwarf everything else.
            if (++_framesSeen > 30) _frameGraph.Push(deltaTime * 1000f);
            _console.Poll();
        }

        private GuiElement BuildToolbar()
        {
            GuiTheme theme = _gui.Theme;
            var bar = new StackPanel(Orientation.Horizontal, 4f)
            {
                Name = "DebugToolbar",
                Anchor = Anchor.Top,
                Y = 8f,
                Padding = new Thickness(10f, 4f, 4f, 4f),
                BackgroundColor = theme.WindowBackground,
                BorderColor = theme.WindowBorder,
                BorderWidth = 1f,
                CornerRadius = theme.WindowCornerRadius,
                ShadowSize = 10f,
                HitTest = HitTestMode.Block,
            };
            bar.Stack!.CrossAlignment = Alignment.Center;
            bar.Add(new Label("DINO ENGINE") { Color = theme.Accent, FontSize = theme.SmallFontSize, Margin = new Thickness(0f, 0f, 6f, 0f) });
            AddToggle(bar, "Stats", StatsWindow);
            AddToggle(bar, "Performance", PerformanceWindow);
            AddToggle(bar, "Render", RenderWindow);
            AddToggle(bar, "Sound", SoundWindow);
            AddToggle(bar, "Entities", EntitiesWindow);
            AddToggle(bar, "Console", ConsoleWindow);
            AddToggle(bar, "Widgets", ShowcaseWindow);
            bar.Add(new Label("F3 hides") { Color = theme.TextDisabled, FontSize = theme.SmallFontSize, Margin = new Thickness(6f, 0f, 6f, 0f) });
            return bar;
        }

        private void AddToggle(GuiElement bar, string text, GuiWindow window)
        {
            GuiTheme theme = _gui.Theme;
            var button = bar.Add(new Button(text, () => window.Toggle()) { Height = 24f, Tooltip = $"Show or hide the {text} window" });
            button.Label.FontSize = theme.SmallFontSize + 1f;
            button.Updated += _ =>
            {
                button.NormalColor = window.Visible ? theme.Accent.WithAlpha(0.28f) : theme.Control;
                button.BorderColor = window.Visible ? theme.Accent.WithAlpha(0.6f) : theme.ControlBorder;
            };
            _toolbarToggles.Add((button, window));
        }

        private GuiWindow BuildStatsWindow()
        {
            GuiTheme theme = _gui.Theme;
            var window = new GuiWindow("Stats", 300f) { X = 12f, Y = 56f };
            window.Closable = true;

            var grid = window.Content.Add(DebugUi.KeyValueGrid());
            DebugUi.AddRow(grid, "FPS", () => Engine.FramesLastSecond.ToString());
            DebugUi.AddRow(grid, "Frame (ms)", () => DebugUi.Inv($"{_frameGraph.Average():0.00} avg, {_frameGraph.Maximum():0.0} max"));
            DebugUi.AddRow(grid, "Time", () => FormatTime(Engine.Time));
            DebugUi.AddRow(grid, "Resolution", () => $"{Engine.Resolution.X} x {Engine.Resolution.Y}");
            DebugUi.AddRow(grid, "Entities", () => Engine.Instance?.world.Count.ToString("N0", DebugUi.Culture) ?? "-");
            DebugUi.AddRow(grid, "Archetypes", () => Engine.Instance?.world.Archetypes.Count.ToString() ?? "-");
            DebugUi.AddRow(grid, "Camera", () => FormatVector(Engine.RenderEngine.context.viewPos));
            DebugUi.AddRow(grid, "Managed mem", () => DebugUi.Inv($"{GC.GetTotalMemory(false) / (1024.0 * 1024.0):0.0} MB"));

            window.Content.Add(_frameGraph);
            _frameGraph.AddReferenceLine(1000f / 60f, theme.Accent.WithAlpha(0.6f));
            _frameGraph.AddReferenceLine(1000f / 30f, theme.Warning.WithAlpha(0.6f));

            window.Content.Add(DebugUi.Heading("GUI"));
            var guiGrid = window.Content.Add(DebugUi.KeyValueGrid());
            DebugUi.AddRow(guiGrid, "Elements", () => _gui.ElementCount.ToString());
            DebugUi.AddRow(guiGrid, "Quads / draws", () => $"{_gui.LastQuadCount} / {_gui.LastDrawCalls}");
            DebugUi.AddRow(guiGrid, "Layout", () => DebugUi.Inv($"{_gui.LastLayoutMilliseconds:0.000} ms"));
            DebugUi.AddRow(guiGrid, "Input", () => (_gui.WantsMouse ? "mouse " : "") + (_gui.WantsKeyboard ? "keyboard" : (_gui.WantsMouse ? "" : "game")));

            var scaleSlider = new Slider(0.75f, 2.5f, _gui.Scale) { Step = 0.05f, Format = "0.00x", FormatProvider = DebugUi.Culture, Tooltip = "GUI scale (applied on release)" };
            // Applied when the mouse is released so the slider does not move under the cursor while dragging.
            // Registered before Bind so the bound read-back already sees the new scale.
            scaleSlider.Updated += _ =>
            {
                if (scaleSlider.IsPressed || !_scaleDirty) return;
                _scaleDirty = false;
                _gui.Scale = _pendingScale;
            };
            scaleSlider.Bind(() => _gui.Scale, value =>
            {
                _pendingScale = value;
                _scaleDirty = true;
            });
            window.Content.Add(DebugUi.LabeledRow("UI scale", scaleSlider));
            return window;
        }

        private static string FormatTime(float seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss", DebugUi.Culture) : time.ToString(@"m\:ss\.f", DebugUi.Culture);
        }

        internal static string FormatVector(Vector3 v) => DebugUi.Inv($"{v.X:0.0}, {v.Y:0.0}, {v.Z:0.0}");
    }
}
