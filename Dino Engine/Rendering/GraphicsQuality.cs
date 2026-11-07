using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering
{
    /// <summary>The one graphics setting a player has. A new level is a value here and a line in <see cref="GraphicsPresets"/>.</summary>
    public enum GraphicsQuality : byte
    {
        Low,
        Medium,
        High,
    }

    /// <param name="Samples">Of multisampling. Grass leans on this for its edges, so it never goes to one.</param>
    /// <param name="GrassDensity">Blades per square metre close to the camera.</param>
    /// <param name="GrassWidth">What blades are widened by, as fewer of them have to cover the same ground.</param>
    /// <param name="GrassDistance">Metres the field reaches.</param>
    /// <param name="GrassShadowDistance">Metres within which blades cast shadows.</param>
    /// <param name="GrassShadowDensity">The share of blades that do.</param>
    /// <param name="ShadowMap">Of the sun's shadow map, on a side.</param>
    /// <param name="ShadowedLights">How many lamps and fires cast shadows.</param>
    /// <param name="FogSteps">Through the volumetric fog.</param>
    /// <param name="ReflectionSteps">Of a reflected ray, and none means reflections are of the sky alone.</param>
    /// <param name="Creatures">The most living things about at once.</param>
    /// <param name="Leaves">The most leaves falling or lying at once.</param>
    public readonly record struct QualityPreset(int Samples, float GrassDensity, float GrassWidth, float GrassDistance, float GrassShadowDistance, float GrassShadowDensity, int ShadowMap, int ShadowedLights, int FogSteps, int ReflectionSteps, int Creatures, int Leaves);

    /// <summary>
    /// What each level of quality comes to. Everything here was chosen by what it buys for what it costs to look at, measured at the most expensive kind of place there is: what the player sees from the game's own camera
    /// stays the same picture, and what goes first is what can't be noticed from up there. See the Graphics section of MENU.md.
    /// </summary>
    public static class GraphicsPresets
    {
        private static readonly QualityPreset[] Presets =
        {
            new(2, 70f, 1.3f, 85f, 30f, 0.15f, 2048, 1, 16, 0, 80, 500),
            new(2, 100f, 1.15f, 100f, 50f, 0.25f, 3072, 2, 24, 32, 140, 900),
            new(4, 140f, 1f, 115f, 70f, 0.35f, 4096, 4, 32, 56, 200, 1400),
        };

        public static QualityPreset Of(GraphicsQuality quality) => Presets[(int)quality];

        /// <summary>Whatever isn't kept in a world.</summary>
        public static void Apply(RenderEngine render, GraphicsQuality quality)
        {
            QualityPreset preset = Of(quality);
            render.Quality = quality;
            render.AntiAliasing.Samples = preset.Samples;
            render.Lighting.ShadowedPointLights = preset.ShadowedLights;
            render.Reflections.ScreenRays = preset.ReflectionSteps > 0;
            if (preset.ReflectionSteps > 0) render.Reflections.MaxSteps = preset.ReflectionSteps;
        }

        /// <summary>What is kept in a world, which goes when that does, so a new one has to be told.</summary>
        public static void Apply(ECSWorld world, GraphicsQuality quality)
        {
            QualityPreset preset = Of(quality);

            if (world.TryGetSingleton<GrassSingleton>(out Entity grassEntity))
            {
                EntityView view = world.GetEntityView(grassEntity);
                var grass = view.Get<GrassSingleton>();
                var plain = new GrassSingleton();
                grass.Density = preset.GrassDensity;
                grass.Width = plain.Width * preset.GrassWidth;
                grass.DrawDistance = preset.GrassDistance;
                grass.ShadowDistance = preset.GrassShadowDistance;
                grass.ShadowDensity = preset.GrassShadowDensity;
                view.Set(grass);
            }

            if (world.TryGetSingleton<VolumetricFogSingleton>(out Entity fogEntity))
            {
                EntityView view = world.GetEntityView(fogEntity);
                var fog = view.Get<VolumetricFogSingleton>();
                fog.MaxSteps = preset.FogSteps;
                view.Set(fog);
            }

            // A map's size is what it was made with, so a different one is a new one.
            if (world.sun.IsValid() && world.TryGetEntityLocation(world.sun, out _, out _))
            {
                EntityView sun = world.GetEntityView(world.sun);
                var shadow = sun.Get<DirectionalCascadingShadowComponent>();
                if (shadow.cascades[0].shadowFrameBuffer.getResolution().X == preset.ShadowMap) return;
                shadow.Cleanup();
                sun.Set(SunShadow(preset));
            }
        }

        /// <summary>The sun's shadow at a level of quality.</summary>
        public static DirectionalCascadingShadowComponent SunShadow(in QualityPreset preset) => new DirectionalCascadingShadowComponent(new Vector2i(preset.ShadowMap), 1, 150);
    }
}
