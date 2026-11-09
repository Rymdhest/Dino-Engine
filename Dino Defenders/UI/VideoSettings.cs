using Dino_Defenders.Client;
using Dino_Engine.Core;
using Dino_Engine.Rendering;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

namespace Dino_Defenders.UI
{
    /// <summary>How this player wants the game drawn. It's about their machine, so like their sound it's kept beside the saves.</summary>
    internal sealed class VideoSettings
    {
        private const string File = "video.json";

        /// <summary>What can be chosen as a limit, where nothing is no limit at all.</summary>
        public static readonly int[] FrameLimits = { 30, 60, 120, 144, 165, 240, 0 };

        public DisplayMode Mode { get; set; }

        /// <summary>The size of the window, which nothing means until someone's chosen one.</summary>
        public int Width { get; set; }
        public int Height { get; set; }
        public int FrameLimit { get; set; } = 240;
        public bool VSync { get; set; }
        public GraphicsQuality Quality { get; set; } = GraphicsQuality.High;

        /// <summary>Light that bounces (GI.md). Its own toggle for now, apart from the graphics quality.</summary>
        public bool GlobalIllumination { get; set; } = true;

        private static VideoSettings? _current;

        public static VideoSettings Load() => _current ??= SettingsStore.Read<VideoSettings>(File) ?? new VideoSettings();

        public void Store() => SettingsStore.Write(File, this);

        /// <summary>Whatever has to be known before there's a window.</summary>
        public void Launching(EngineLaunchSettings launch)
        {
            if (Width > 0 && Height > 0) launch._resolution = new Vector2i(Width, Height);
            launch._frameRateLimit = FrameLimit;
        }

        /// <summary>Everything, once there is one.</summary>
        public void Apply()
        {
            ApplyDisplay();
            ApplyFrames();
            ApplyQuality();
        }

        public void ApplyDisplay()
        {
            Vector2i size = Width > 0 && Height > 0 ? new Vector2i(Width, Height) : Engine.WindowHandler.ClientSize;
            Engine.WindowHandler.ApplyDisplay(Mode, size);
        }

        public void ApplyFrames()
        {
            Engine.WindowHandler.UpdateFrequency = FrameLimit;
            Engine.WindowHandler.VSync = VSync ? VSyncMode.On : VSyncMode.Off;
        }

        public void ApplyQuality()
        {
            GraphicsPresets.Apply(Engine.RenderEngine, Quality);
            if (Engine.Instance?.WorldReady == true) GraphicsPresets.Apply(Engine.Instance.world, Quality);
            ApplyGlobalIllumination();
        }

        public void ApplyGlobalIllumination()
        {
            Engine.RenderEngine.GlobalIllumination.Enabled = GlobalIllumination;
        }
    }
}
