
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
        /// <summary>The size the scene is drawn at before it's put on the screen: the window's, times <see cref="Rendering.RenderEngine.RenderScale"/>. Everything in the
        /// 3D pipeline is this size, and only the last pass (RenderEngine's upscale) and the GUI are the window's.</summary>
        public static Vector2i RenderResolution { get => _instance._renderEngine?.ScaledSize(Resolution) ?? Resolution; }
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

        /// <summary>The bar on the window while the engine is made, until the first frame.</summary>
        private StartupScreen? _startup;

        /// <summary>How far along a loading bar is when the engine is made and the game's own loading can begin: the game's screen carries on from here.</summary>
        public const float StartupShare = 0.4f;

        /// <summary>For the game: a stage of its own making done, and the share of the start that's over with it (of the engine's share, which is most of it).</summary>
        public static void Loading(string stage, float share) => _instance?._startup?.Reached(stage, share);

        internal static StartupScreen? Startup => _instance?._startup;

        public Engine(EngineLaunchSettings settings)
        {
            _instance = this;
            Console.WriteLine($"Launching {Name} version {Version}");
            _windowHandler = InitWindow(settings);
            // Something on the window from the moment it exists, as it's white until something's drawn on it, and the rest of this takes seconds.
            _startup = new StartupScreen(_windowHandler, !settings._hidden);
            _deltaFrameTimeTracker = new DeltaFrameTimeTracker();
            _performanceMonitor = new PerformanceMonitor();
            _renderEngine = new RenderEngine();
            _renderEngine.InitRenderers(settings._resolution);
            _startup.Reached("renderers", 0.90f);


            //ComponentTypeRegistry.Register<Position>();
            //ComponentTypeRegistry.Register<Velocity>();
            ComponentTypeRegistry.AutoRegisterAllComponents();
            //SystemRegistry.AutoRegisterAllSystems();
            SystemRegistry.ManualRegisterSystems();

            world = new();

            _gui = new GuiContext(_windowHandler);
            _debugOverlay = new DebugOverlay(_gui);
            _startup.Reached("gui", 0.93f);
            _startup.Built();

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
            // To see the driver's messages: GL.Enable(EnableCap.DebugOutput) and GL.DebugMessageCallback(debugProcCallback, IntPtr.Zero). Left on with nothing listening, it only costs.



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
            
            // WindowHandler loads the two sets of bindings the engine calls. OpenTK's own loading finds every set it has by name, OpenGL ES's too,
            // which a trimmed release has taken out as nothing calls them (LAUNCHER.md).
            nws.AutoLoadBindings = false;
            nws.Title = settings._gameTitle;
            nws.ClientSize = settings._resolution;
            nws.Location = new Vector2i(0, 0);
            nws.WindowState = WindowState.Normal;
            nws.WindowBorder = WindowBorder.Resizable;
            nws.StartVisible = !settings._hidden;
            nws.StartFocused = !settings._hidden;
            // The rules keep their own tick whatever the frame rate, and this paces drawing too: it's the frame rate limit, nothing more.
            gws.UpdateFrequency = settings._frameRateLimit;
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
            if (_startup != null)
            {
                _startup.Finish();
                _startup = null;
            }
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
            _performanceMonitor.startCPUTask("Game");
            game.update();
            _performanceMonitor.finishCPUTask("Game");

            if (WorldReady) world.Update(Engine.Delta);

        }

        public void OnResize(ResizeEventArgs eventArgs)
        {
            _windowHandler.onResize(eventArgs);
            world.OnResize(eventArgs);
            _renderEngine.OnResize(eventArgs);
            _startup?.Reached("resize", 0.93f);
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
