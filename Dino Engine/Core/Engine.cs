using Dino_Engine.Animation;
using Dino_Engine.Debug;
using Dino_Engine.ECS;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.ECS.Systems;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Debug;
using Dino_Engine.Rendering;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ErrorCode = OpenTK.Graphics.OpenGL.ErrorCode;

namespace Dino_Engine.Core
{
    public class Engine
    {
        public const string Version = "0.0.2";
        public const string Name = "Dino Engine";
        private WindowHandler _windowHandler;
        private DeltaFrameTimeTracker _deltaFrameTimeTracker;
        private RenderEngine _renderEngine;
        private PerformanceMonitor _performanceMonitor;
        private GuiContext? _gui;
        private DebugOverlay? _debugOverlay;
        private static Engine? _instance;
        private Game game;
        private string? _screenshotPath;
        private Action? _screenshotSaved;

        public static float Delta { get => _instance._deltaFrameTimeTracker.Delta; }
        public static float Time { get => _instance._deltaFrameTimeTracker.TotalTime; }
        /// <summary>For a scripted film: every frame then lasts this long, so saving frames doesn't slow what they show.</summary>
        public static void UseFixedStep(float seconds) => _instance._deltaFrameTimeTracker.FixedStep = seconds;
        /// <summary>For a scripted run: starts as if the engine had already been running this long, since some faults only show late.</summary>
        public static void SkipClock(float seconds) => _instance._deltaFrameTimeTracker.Skip(seconds);
        public static int FramesLastSecond { get => _instance._deltaFrameTimeTracker.FramesLastSecond; }
        public static Engine? Instance { get => _instance; }
        public static WindowHandler WindowHandler { get => _instance._windowHandler; }
        public static Vector2i Resolution { get => _instance._windowHandler.ClientSize; }
        public static RenderEngine RenderEngine { get => _instance._renderEngine; }
        public static PerformanceMonitor PerformanceMonitor { get => _instance._performanceMonitor; }
        /// <summary>The engine's GUI. Add elements to <c>Engine.Gui.Root</c>; check <c>WantsMouse</c>/<c>WantsKeyboard</c> before reacting to input.</summary>
        public static GuiContext Gui { get => _instance!._gui!; }
        /// <summary>Built-in debug tools (toggle with F3).</summary>
        public static DebugOverlay DebugOverlay { get => _instance!._debugOverlay!; }
        internal GuiContext? GuiOrNull => _gui;

        private static DebugProc debugProcCallback = DebugCallback;

        public ECSWorld world;

        /// <summary>Clear this while there is no world yet: the systems expect one, but the interface can still draw over nothing.</summary>
        public bool WorldReady = true;

        public Engine(EngineLaunchSettings settings)
        {
            _instance = this;
            Console.WriteLine($"Launching {Name} version {Version}");
            _windowHandler = InitWindow(settings);
            _deltaFrameTimeTracker = new DeltaFrameTimeTracker();
            _performanceMonitor = new PerformanceMonitor();
            _renderEngine = new RenderEngine();
            _renderEngine.InitRenderers(settings._resolution);

            AnimationLoader.Initialize();

            //ComponentTypeRegistry.Register<Position>();
            //ComponentTypeRegistry.Register<Velocity>();
            ComponentTypeRegistry.AutoRegisterAllComponents();
            //SystemRegistry.AutoRegisterAllSystems();
            SystemRegistry.ManualRegisterSystems();

            world = new();

            _gui = new GuiContext(_windowHandler);
            _debugOverlay = new DebugOverlay(_gui);

            _windowHandler.UpdateFrame += delegate (FrameEventArgs eventArgs)
            {
                Update();
            };
            _windowHandler.RenderFrame += delegate (FrameEventArgs eventArgs)
            {
                Render();
            };
            _windowHandler.Resize += delegate (ResizeEventArgs eventArgs)
            {
                OnResize(eventArgs);
            };
            GL.Enable(EnableCap.DebugOutput);
            //GL.DebugMessageCallback(debugProcCallback, IntPtr.Zero); // Assign the delegate



        }
        private static void DebugCallback(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            string messageString = Marshal.PtrToStringAnsi(message, length);
            Console.WriteLine($"GL DEBUG: {messageString}");
        }

        private WindowHandler InitWindow(EngineLaunchSettings settings)
        {
            GameWindowSettings gws = GameWindowSettings.Default;
            NativeWindowSettings nws = NativeWindowSettings.Default;
            nws.API = ContextAPI.OpenGL;
            // 4.3 for compute shaders, shader storage buffers and indirect draws (GPU grass).
            nws.APIVersion = new Version(4,3);
            
            nws.AutoLoadBindings = true;
            nws.Title = settings._gameTitle;
            nws.ClientSize = settings._resolution;
            nws.Location = new Vector2i(0, 0);
            nws.WindowState = WindowState.Normal;
            nws.WindowBorder = WindowBorder.Resizable;
            nws.StartVisible = !settings._hidden;
            nws.StartFocused = !settings._hidden;
            gws.UpdateFrequency = 60;
            return new WindowHandler(gws, nws);
        }

        public void Run()
        {
            _windowHandler.Run();
        }

        private void Render()
        {
            _renderEngine.Render();
            PerformanceMonitor.FinishFrame();
        }
        private void Update()
        {
            _deltaFrameTimeTracker.update();

            // GUI first, so WantsMouse/WantsKeyboard are current when game code and systems read input.
            _performanceMonitor.startCPUTask("GUI UPDATE");
            _debugOverlay?.Update(Delta);
            _gui?.Update(Delta);
            _performanceMonitor.finishCPUTask("GUI UPDATE");

            /*
            TEST_totalElapsed += sw.Elapsed.TotalMilliseconds;
            TEST_count++;
            if (TEST_count >= 100)
            {
                Console.WriteLine($"Total: "+TEST_totalElapsed/TEST_count+" ms");
                Console.WriteLine(ECSEngine.Entities.Count);
                TEST_count = 0;
                TEST_totalElapsed = 0;
            }
            */
            _renderEngine.Update();
            game.update();

            if (WorldReady) world.Update(Engine.Delta);

        }

        public void OnResize(ResizeEventArgs eventArgs)
        {
            _windowHandler.onResize(eventArgs);
            world.OnResize(eventArgs);
            _renderEngine.OnResize(eventArgs);
        }

        /// <summary>Saves the next finished frame as a PNG. <paramref name="saved"/> runs once it is written.</summary>
        public void RequestScreenshot(string path, Action? saved = null)
        {
            _screenshotPath = path;
            _screenshotSaved = saved;
        }

        internal void CaptureFrame()
        {
            if (_screenshotPath == null) return;

            Vector2i size = _windowHandler.ClientSize;
            byte[] pixels = new byte[size.X * size.Y * 4];
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
            GL.ReadBuffer(ReadBufferMode.Back);
            GL.ReadPixels(0, 0, size.X, size.Y, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

            // GL reads bottom row first; images start with the top one.
            int stride = size.X * 4;
            byte[] flipped = new byte[pixels.Length];
            for (int y = 0; y < size.Y; y++)
            {
                System.Buffer.BlockCopy(pixels, y * stride, flipped, (size.Y - 1 - y) * stride, stride);
            }
            for (int i = 3; i < flipped.Length; i += 4) flipped[i] = 255;

            PngWriter.Write(_screenshotPath, flipped, size.X, size.Y);
            _screenshotPath = null;
            Action? saved = _screenshotSaved;
            _screenshotSaved = null;
            saved?.Invoke();
        }

        public void SetGame(Game game)
        {
            this.game = game;
        }

        public static void CheckGLError(string stage)
        {
            ErrorCode error = GL.GetError();
            if (error != ErrorCode.NoError)
            {
                Console.WriteLine($"{stage}: {error}");
            }
        }
    }
}
