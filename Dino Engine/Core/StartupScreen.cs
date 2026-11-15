using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Desktop;

namespace Dino_Engine.Core
{
    /// <summary>
    /// What's on the window while the engine is made, before anything can draw: ink, and a bar that moves as the materials are made and the shaders
    /// compiled, so the window is never white and someone starting the game sees from the first moment it exists that something's happening. It's drawn with
    /// nothing but clears, as no shader exists yet, and at most twenty times a second, as a swap waits for the screen. The bar stands where the game's own
    /// loading screen puts its own (Dino Defenders' LoadingScreen), which carries on from <see cref="Engine.StartupShare"/> once there's a frame, so the
    /// handover doesn't show. It also keeps the time each stage took, and says so when the first frame is drawn.
    /// </summary>
    public sealed class StartupScreen
    {
        // Where the bar stands at each stage, as measured on a 7900 XTX: the materials take about a second and the shaders two.
        private const float AfterWindow = 0.02f;
        private const float MaterialsFrom = 0.05f;
        private const float MaterialsTo = 0.30f;
        private const float ShadersTo = 0.90f;
        private const int ExpectedShaders = 160;
        private const long DrawEvery = 50;

        private readonly NativeWindow _window;
        private readonly bool _shown;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly List<(string stage, long ms)> _timeline = new();
        private int _shaders;
        private int _tasks;
        private bool _building = true;
        private float _share;
        private long _drawnAt = -1000;

        public StartupScreen(NativeWindow window, bool shown)
        {
            _window = window;
            _shown = shown;
            Reached("window", AfterWindow);
        }

        /// <summary>One more material made, of so many.</summary>
        public void Material(int index, int count) => Show(MaterialsFrom + (MaterialsTo - MaterialsFrom) * (index + 1) / count);

        /// <summary>One more shader compiled: there are about <see cref="ExpectedShaders"/>, and the bar waits at the end of its share for any over that.</summary>
        public void Shader()
        {
            _shaders++;
            Show(MaterialsTo + (ShadersTo - MaterialsTo) * MathF.Min(1f, _shaders / (float)ExpectedShaders));
        }

        /// <summary>A stage done, and the share of the start that's over with it.</summary>
        public void Reached(string stage, float share)
        {
            _timeline.Add((stage, _clock.ElapsedMilliseconds));
            Show(share);
        }

        /// <summary>The engine is made: from here on the window's own loop pumps its events, and this only paints between the first frame's passes.</summary>
        public void Built() => _building = false;

        /// <summary>A stage of the first update or frame begun (the performance monitor's tasks): the bar creeps on, as there's no knowing how many there are.</summary>
        public void Task(string name)
        {
            if (_building) return;
            _tasks++;
            _timeline.Add((name, _clock.ElapsedMilliseconds));
            Show(0.93f + 0.07f * (1f - MathF.Pow(0.93f, _tasks)));
        }

        /// <summary>The first frame's been drawn: what's left is the game's own loading screen. Says how long each stage took, and which of the first frame's passes took over a tenth of a second.</summary>
        public void Finish()
        {
            Reached("first frame", 1f);
            var parts = new List<string>();
            long last = 0;
            foreach ((string stage, long ms) in _timeline)
            {
                if (ms - last >= 100) parts.Add($"{stage} {(ms - last) / 1000f:0.0}");
                last = ms;
            }
            double sinceLaunch = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds;
            Console.WriteLine($"Started in {sinceLaunch:0.0} s from the process starting, {_shaders} shaders: {string.Join(", ", parts)}");
        }

        private void Show(float share)
        {
            _share = MathF.Max(_share, share);
            if (!_shown || _clock.ElapsedMilliseconds - _drawnAt < DrawEvery) return;
            _drawnAt = _clock.ElapsedMilliseconds;
            Draw();
        }

        // Whatever's being made keeps its framebuffer, viewport, scissor and clear colour: this borrows them and gives them back.
        private void Draw()
        {
            int drawFramebuffer = GL.GetInteger(GetPName.DrawFramebufferBinding);
            int readFramebuffer = GL.GetInteger(GetPName.ReadFramebufferBinding);
            var viewport = new int[4];
            GL.GetInteger(GetPName.Viewport, viewport);
            var scissor = new int[4];
            GL.GetInteger(GetPName.ScissorBox, scissor);
            bool scissored = GL.IsEnabled(EnableCap.ScissorTest);
            var clear = new float[4];
            GL.GetFloat(GetPName.ColorClearValue, clear);

            int width = _window.ClientSize.X, height = _window.ClientSize.Y;
            float scale = _window.TryGetCurrentMonitorScale(out float monitorScale, out _) && monitorScale > 0f ? Math.Clamp(monitorScale, 0.5f, 4f) : 1f;
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Viewport(0, 0, width, height);
            GL.Disable(EnableCap.ScissorTest);
            GL.ClearColor(9 / 255f, 7 / 255f, 6 / 255f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            // The bar, as the game's loading screen draws it: a hair of dark with gold over as much of it as is done.
            float barWidth = MathF.Min(width * 0.36f, 460f * scale);
            int x = (int)((width - barWidth) * 0.5f);
            int top = (int)(height * 0.42f + 100f * scale);
            int thickness = Math.Max(1, (int)MathF.Round(3f * scale));
            int y = height - top - thickness;
            GL.Enable(EnableCap.ScissorTest);
            GL.Scissor(x, y, (int)barWidth, thickness);
            GL.ClearColor(60 / 255f, 50 / 255f, 40 / 255f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.Scissor(x, y, (int)(barWidth * Math.Clamp(_share * Engine.StartupShare, 0f, 1f)), thickness);
            GL.ClearColor(214 / 255f, 172 / 255f, 92 / 255f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            _window.Context.SwapBuffers();
            // Only while the engine is made: in the window's own loop a resize mid-pass would remake what the pass is drawing into.
            if (_building) _window.ProcessEvents(0d);

            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, drawFramebuffer);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, readFramebuffer);
            GL.Viewport(viewport[0], viewport[1], viewport[2], viewport[3]);
            GL.Scissor(scissor[0], scissor[1], scissor[2], scissor[3]);
            if (scissored) GL.Enable(EnableCap.ScissorTest);
            else GL.Disable(EnableCap.ScissorTest);
            GL.ClearColor(clear[0], clear[1], clear[2], clear[3]);
        }
    }
}
