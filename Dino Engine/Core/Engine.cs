using Dino_Engine.ECS;
using Dino_Engine.Rendering;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace Dino_Engine.Core
{
    public class Engine
    {
        public const string Version = "0.0.1";
        public const string Name = "Dino Engine";
        private WindowHandler _windowHandler;
        private DeltaFrameTimeTracker _deltaFrameTimeTracker;
        private RenderEngine _renderEngine;
        private static Engine? _instance;
        private ECSEngine _ECSEngine;
        public static float Delta { get => _instance._deltaFrameTimeTracker.Delta; }
        public static int FramesLastSecond { get => _instance._deltaFrameTimeTracker.FramesLastSecond; }
        public static Engine? Instance { get => _instance; }
        public static WindowHandler WindowHandler { get => _instance._windowHandler; }
        public static RenderEngine RenderEngine { get => _instance._renderEngine; }
        public ECSEngine ECSEngine { get => _ECSEngine; }

        public Engine(EngineLaunchSettings settings)
        {
            _instance = this;
            Console.WriteLine($"Launching {Name} version {Version}");
            _windowHandler = InitWindow(settings);
            _deltaFrameTimeTracker = new DeltaFrameTimeTracker();
            _renderEngine = new RenderEngine();
            _renderEngine.InitRenderers();
            _ECSEngine = new ECSEngine();
            _ECSEngine.Init();
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

        }

        private WindowHandler InitWindow(EngineLaunchSettings settings)
        {
            GameWindowSettings gws = GameWindowSettings.Default;
            NativeWindowSettings nws = NativeWindowSettings.Default;
            nws.API = ContextAPI.OpenGL;
            nws.AutoLoadBindings = true;
            nws.Title = settings._gameTitle;
            nws.ClientSize = settings._resolution;
            nws.Location = new Vector2i(0, 0);
            gws.UpdateFrequency = 30;
            return new WindowHandler(gws, nws);
        }

        public void Run()
        {
            _windowHandler.Run();
        }

        private void Render()
        {
            _renderEngine.Render(_ECSEngine);
        }
        private void Update()
        {
            _deltaFrameTimeTracker.update();
            _renderEngine.Update();
        }

        public void OnResize(ResizeEventArgs eventArgs)
        {
            _windowHandler.onResize(eventArgs);
            _renderEngine.OnResize(eventArgs);
        }
    }
}
