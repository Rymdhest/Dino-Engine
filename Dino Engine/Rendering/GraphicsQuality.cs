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
    /// <param name="ShadowedLights">How many lamps and fires cast the shadows of whoever walks by them, the nearest to the hero.</param>
    /// <param name="StillLights">How many cast the shadows of what stands round them, from a map drawn once and kept: past them a lamp lights through walls.</param>
    /// <param name="FogSteps">Through the volumetric fog.</param>
    /// <param name="ReflectionSteps">Of a reflected ray, and none means reflections are of the sky alone.</param>
    /// <param name="Creatures">The most living things about at once.</param>
    /// <param name="Leaves">The most leaves falling or lying at once.</param>
    /// <param name="Anisotropy">How far the materials' colour filters along a slant. From the game's camera the ground is nearly square-on and a wall is seen at
    /// 2.6 to 1, so what's past 4 only sharpens a trunk's edges and a low look, and a trunk's edges, many of them behind leaves, are where it costs most.</param>
    /// <param name="Relief">How deep every material's relief is drawn, as a share of its own (Surface/Relief.glsl): 0 is none, and the normals alone.</param>
    /// <param name="ShellLayers">Layers of moss and fur to each pixel their length covers (SHELLS.md). They stay at every level, as they're what moss and fur look like:
    /// fewer of them only show as steps at a silhouette, close up.</param>
    /// <param name="ShellGround">Whether the ground's moss stands up as shells too: in a mossy wood it's most of the screen, and it's the least of them seen from up here.</param>
    /// <param name="OcclusionSlices">Slices through the hemisphere each pixel's ambient occlusion takes (OCCLUSION.md): its 4 x 4 tile takes sixteen times as many between them.</param>
    /// <param name="OcclusionSteps">Samples along each side of a slice.</param>
    /// <param name="ShadowSoftest">The radius in the sun's shadow map's texels that no shadow softens beyond (SHADOWS.md): the same in metres at every level, as a smaller map's
    /// texels are wider, and what a pixel can cost goes with its square.</param>
    /// <param name="ContactShadows">Whether the sun's contact shadows are traced (SHADOWS.md): what the shadow map is too coarse to see, under a stone or a foot. From the game's camera
    /// they're the least of the shadows, and two passes over the screen, 0.7 ms of a GTX 1660 Ti's frame at 1080p.</param>
    /// <param name="Occlusion">Whether there's ambient occlusion at all (OCCLUSION.md): four passes over the screen, 1.1 ms at 1080p on a GTX 1660 Ti.</param>
    /// <param name="Bloom">Whether what's too bright glows past its edges.</param>
    /// <param name="LightReach">What lamps' radii are drawn at, as a share of their own (LightingSettings.LightReach).</param>
    /// <param name="RenderScale">The share of the window's width and height the scene is drawn at, then sharpened onto it (RenderEngine.RenderScale): what the GPU does for
    /// each pixel goes with its square, and nothing else took Low to 120 frames a second on a GTX 1660 Ti at 1080p.</param>
    public readonly record struct QualityPreset(int Samples, float GrassDensity, float GrassWidth, float GrassDistance, float GrassShadowDistance, float GrassShadowDensity, int ShadowMap, int ShadowedLights, int FogSteps, int ReflectionSteps, int Creatures, int Leaves, float Anisotropy, float Relief, float ShellLayers, bool ShellGround, int StillLights,
        int OcclusionSlices, int OcclusionSteps, float ShadowSoftest, bool ContactShadows, bool Occlusion, bool Bloom, float RenderScale, float LightReach);

    /// <summary>
    /// What each level of quality comes to. Everything here was chosen by what it buys for what it costs to look at, measured at the most expensive kind of place there is: what the player sees from the game's own camera
    /// stays the same picture, and what goes first is what can't be noticed from up there. See the Graphics section of MENU.md. The two lower levels were
    /// thinned again for a GTX 1660 Ti at 1080p: fewer, wider blades of grass, which cover the same ground for a fifth less, and fewer steps through the fog;
    /// and Low, to reach 120 frames a second there, has no contact shadows, ambient occlusion or bloom, shadows on fewer lamps, whose light reaches four fifths as far,
    /// grass that ends sooner, and draws the scene at two thirds of the window.
    /// </summary>
    public static class GraphicsPresets
    {
        private static readonly QualityPreset[] Presets =
        {
            new(2, 42f, 1.6f, 65f, 30f, 0.15f, 2048, 1, 10, 0, 80, 500, 1f, 0f, 1f, false, 4, 1, 2, 4f, false, false, false, 0.667f, 0.8f),
            new(2, 80f, 1.25f, 100f, 50f, 0.25f, 3072, 2, 18, 32, 140, 900, 2f, 1f, 1.25f, true, 16, 1, 3, 6f, true, true, true, 1f, 1f),
            new(4, 140f, 1f, 115f, 70f, 0.35f, 4096, 4, 32, 56, 200, 1400, 4f, 1f, 1.5f, true, 32, 1, 3, 8f, true, true, true, 1f, 1f),
        };

        public static QualityPreset Of(GraphicsQuality quality) => Presets[(int)quality];

        /// <summary>Whatever isn't kept in a world.</summary>
        public static void Apply(RenderEngine render, GraphicsQuality quality)
        {
            QualityPreset preset = Of(quality);
            render.Quality = quality;
            render.AntiAliasing.Samples = preset.Samples;
            render.Lighting.ShadowedPointLights = preset.ShadowedLights;
            render.Lighting.StillShadowedPointLights = preset.StillLights;
            render.textureGenerator.SetAnisotropy(preset.Anisotropy);
            render.ReliefScale = preset.Relief;
            render.Shells.Settings.LayersPerPixel = preset.ShellLayers;
            render.Shells.Settings.Ground = preset.ShellGround;
            render.Reflections.ScreenRays = preset.ReflectionSteps > 0;
            if (preset.ReflectionSteps > 0) render.Reflections.MaxSteps = preset.ReflectionSteps;
            render.Occlusion.Slices = preset.OcclusionSlices;
            render.Occlusion.Steps = preset.OcclusionSteps;
            render.SunShadows.Softest = preset.ShadowSoftest;
            render.ContactShadows.Enabled = preset.ContactShadows;
            render.Occlusion.Enabled = preset.Occlusion;
            render.Lighting.Bloom = preset.Bloom;
            render.Lighting.LightReach = preset.LightReach;
            render.SetRenderScale(preset.RenderScale);
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
                // As wide as they were: a view with a map for what's far off keeps it (SunShadow).
                float[] widths = shadow.cascades.Select(cascade => cascade.projectionSize).ToArray();
                shadow.Cleanup();
                sun.Set(new DirectionalCascadingShadowComponent(new Vector2i(preset.ShadowMap), widths));
            }
        }

        /// <summary>Metres across the sun's shadow map, which is all the game's camera sees and more.</summary>
        public const float SunShadowWidth = 150f;

        /// <summary>The sun's shadow at a level of quality.</summary>
        public static DirectionalCascadingShadowComponent SunShadow(in QualityPreset preset) => new DirectionalCascadingShadowComponent(new Vector2i(preset.ShadowMap), 1, SunShadowWidth);

        /// <summary>
        /// The sun's shadow for a view that looks out across a land: the game's map, and one <paramref name="far"/> metres across for what's past it, as nothing past the first casts or
        /// takes a shadow otherwise, and a low sun's shadows are most of what such a view is made of.
        /// </summary>
        public static DirectionalCascadingShadowComponent SunShadow(in QualityPreset preset, float far) => new DirectionalCascadingShadowComponent(new Vector2i(preset.ShadowMap), SunShadowWidth, far);
    }
}
