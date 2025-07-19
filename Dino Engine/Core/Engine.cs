using Dino_Engine.Debug;
using Dino_Engine.ECS;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.ECS.Systems;
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
        private static Engine? _instance;
        private Game game;

        public static float Delta { get => _instance._deltaFrameTimeTracker.Delta; }
        public static float Time { get => _instance._deltaFrameTimeTracker.TotalTime; }
        public static int FramesLastSecond { get => _instance._deltaFrameTimeTracker.FramesLastSecond; }
        public static Engine? Instance { get => _instance; }
        public static WindowHandler WindowHandler { get => _instance._windowHandler; }
        public static Vector2i Resolution { get => _instance._windowHandler.ClientSize; }
        public static RenderEngine RenderEngine { get => _instance._renderEngine; }
        public static PerformanceMonitor PerformanceMonitor { get => _instance._performanceMonitor; }

        private static DebugProc debugProcCallback = DebugCallback; // Declare the delegate as a static field

        public ECSWorld world;
        private double TEST_totalElapsed;
        private int TEST_count;

        public Engine(EngineLaunchSettings settings)
        {
            _instance = this;
            Console.WriteLine($"Launching {Name} version {Version}");
            _windowHandler = InitWindow(settings);
            _deltaFrameTimeTracker = new DeltaFrameTimeTracker();
            _performanceMonitor = new PerformanceMonitor();
            _renderEngine = new RenderEngine();
            _renderEngine.InitRenderers(settings._resolution);




            //ComponentTypeRegistry.Register<Position>();
            //ComponentTypeRegistry.Register<Velocity>();
            ComponentTypeRegistry.AutoRegisterAllComponents();
            SystemRegistry.AutoRegisterAllSystems();

            world = new();

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
            nws.APIVersion = new Version(4,2);
            
            nws.AutoLoadBindings = true;
            nws.Title = settings._gameTitle;
            nws.ClientSize = settings._resolution;
            nws.Location = new Vector2i(0, 0);
            gws.UpdateFrequency = 300;
            return new WindowHandler(gws, nws);
        }

        public void Run()
        {
            _windowHandler.Run();
        }

        private void Render()
        {
            _renderEngine.Render();
            PerformanceMonitor.finishTask("Total");
        }
        private void Update()
        {
            PerformanceMonitor.startTask("Total");
            _deltaFrameTimeTracker.update();

            Stopwatch sw = Stopwatch.StartNew();
            sw.Stop();
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



            Stopwatch sw2 = Stopwatch.StartNew();
            world.Update(Engine.Delta);
            sw2.Stop();
            TEST_totalElapsed += sw2.Elapsed.TotalMilliseconds;
            TEST_count++;
            if (TEST_count >= 100)
            {
                Console.WriteLine($"Total: " + TEST_totalElapsed / TEST_count + " ms");
                Console.WriteLine(world.Count);
                TEST_count = 0;
                TEST_totalElapsed = 0;
                Console.WriteLine($"number of entities: {world.Count}");
            }

        }

        public void OnResize(ResizeEventArgs eventArgs)
        {
            _windowHandler.onResize(eventArgs);
            world.OnResize(eventArgs);
            _renderEngine.OnResize(eventArgs);
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
