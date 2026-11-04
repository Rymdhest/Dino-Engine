using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.AntiAliasing;
using Dino_Engine.Rendering.Renderers.Lighting;
using OpenTK.Mathematics;

namespace Dino_Engine.GUI.Debug
{
    /// <summary>Live render controls: debug view, anti-aliasing, sun and sky, lighting, wind, grass, fog, water, camera FOV and a G-buffer viewer.</summary>
    internal static class RenderWindowBuilder
    {
        private static readonly string[] BufferNames = { "Final image", "Albedo", "Normals", "Materials", "Depth" };
        private static readonly string[] MsaaModes = { "Off", "2x", "4x", "8x" };
        private static readonly int[] MsaaSamples = { 1, 2, 4, 8 };

        public static GuiWindow Build()
        {
            GuiTheme theme = GuiContext.DefaultTheme;
            var window = new GuiWindow("Render", 340f) { X = 12f, Y = 470f };

            window.Content.Add(new Checkbox("Deferred debug view (F8)").Bind(
                () => Engine.RenderEngine.debugView,
                value => Engine.RenderEngine.debugView = value));

            // ---------------------------------------------------------------- anti-aliasing
            var antiAliasing = window.Content.Add(new Foldout("Anti-aliasing"));
            AntiAliasingSettings aa = Engine.RenderEngine.AntiAliasing;
            var msaa = new Dropdown(MsaaModes, Array.IndexOf(MsaaSamples, aa.Samples));
            msaa.SelectionChanged += index => { if (index >= 0) aa.Samples = MsaaSamples[index]; };
            antiAliasing.Content.Add(DebugUi.LabeledRow("MSAA", msaa));
            antiAliasing.Content.Add(new Checkbox("Grass coverage (sub-pixel blades)").Bind(() => aa.GrassCoverage, value => aa.GrassCoverage = value));
            antiAliasing.Content.Add(new Checkbox("Alpha to coverage (cutouts)").Bind(() => aa.AlphaToCoverage, value => aa.AlphaToCoverage = value));
            antiAliasing.Content.Add(new Checkbox("FXAA after tone mapping").Bind(() => aa.Fxaa, value => aa.Fxaa = value));
            antiAliasing.Content.Add(new Checkbox("Show edge pixels (shaded per sample)").Bind(() => aa.ShowEdgePixels, value => aa.ShowEdgePixels = value));
            var antiAliasingGrid = antiAliasing.Content.Add(DebugUi.KeyValueGrid(0.3f));
            DebugUi.AddRow(antiAliasingGrid, "Running", () => DebugUi.Inv(
                $"{Engine.RenderEngine.Msaa.Samples}x MSAA (GPU supports up to {Engine.RenderEngine.Msaa.MaxSupportedSamples}x)"));

            // ---------------------------------------------------------------- sun
            var sun = window.Content.Add(new Foldout("Sun"));
            sun.Content.Add(DebugUi.LabeledRow("Azimuth", new Slider(-180f, 180f) { Format = "0°", Step = 1f, FormatProvider = DebugUi.Culture }.Bind(
                () => SunAngles().azimuth,
                value => SetSunAngles(value, SunAngles().elevation))));
            sun.Content.Add(DebugUi.LabeledRow("Elevation", new Slider(-10f, 90f) { Format = "0°", Step = 0.5f, FormatProvider = DebugUi.Culture }.Bind(
                () => SunAngles().elevation,
                value => SetSunAngles(SunAngles().azimuth, value))));
            sun.Content.Add(DebugUi.LabeledRow("Intensity", new Slider(0f, 40f) { Format = "0.0", FormatProvider = DebugUi.Culture }.Bind(
                () => TryGetSun<ColorComponent>(out var colour) ? colour.value.Intensity : 0f,
                value =>
                {
                    if (!TryGetSun<ColorComponent>(out var colour)) return;
                    var c = colour.value;
                    c.Intensity = value;
                    colour.value = c;
                    SetSun(colour);
                })));
            sun.Content.Add(DebugUi.LabeledRow("Sky", new Slider(0f, 4f) { Format = "0.00", FormatProvider = DebugUi.Culture }.Bind(
                () => TryGetSky(out _, out var colour) ? colour.value.Intensity : 0f,
                value =>
                {
                    if (!TryGetSky(out Entity entity, out var colour)) return;
                    var c = colour.value;
                    c.Intensity = value;
                    colour.value = c;
                    Engine.Instance!.world.GetEntityView(entity).Set(colour);
                })));

            sun.Content.Add(DebugUi.LabeledRow("Night", new Slider(0f, 1f) { Format = "0.00", FormatProvider = DebugUi.Culture }.Bind(
                () => Engine.RenderEngine.Lighting.Night,
                value => Engine.RenderEngine.Lighting.Night = value)));

            // ---------------------------------------------------------------- lighting
            var light = window.Content.Add(new Foldout("Lighting"));
            LightingSettings lighting = Engine.RenderEngine.Lighting;
            light.Content.Add(DebugUi.Muted("Each of these takes one source of light out, to see what the rest do alone.", wrap: true));
            light.Content.Add(new Checkbox("Sun").Bind(() => lighting.Sun, value => lighting.Sun = value));
            light.Content.Add(new Checkbox("Sky").Bind(() => lighting.Sky, value => lighting.Sky = value));
            light.Content.Add(new Checkbox("Point lights and spotlights").Bind(() => lighting.LocalLights, value => lighting.LocalLights = value));
            light.Content.Add(new Checkbox("Emissive surfaces").Bind(() => lighting.Emission, value => lighting.Emission = value));
            light.Content.Add(new Checkbox("Fog and haze").Bind(() => lighting.Fog, value => lighting.Fog = value));
            light.Content.Add(new Checkbox("Bloom").Bind(() => lighting.Bloom, value => lighting.Bloom = value));
            light.Content.Add(DebugUi.Heading("Levels"));
            light.Content.Add(LightingSlider("Exposure", 0.1f, 2f, "0.00", () => lighting.Exposure, v => lighting.Exposure = v));
            light.Content.Add(LightingSlider("Sky fill", 0f, 3f, "0.00", () => lighting.SkyFill, v => lighting.SkyFill = v));
            light.Content.Add(LightingSlider("Sun fill", 0f, 0.2f, "0.000", () => lighting.SunFill, v => lighting.SunFill = v));
            light.Content.Add(LightingSlider("Ground bounce", 0f, 1f, "0.00", () => lighting.GroundAlbedo, v => lighting.GroundAlbedo = v));
            light.Content.Add(LightingSlider("Local lights", 0f, 4f, "0.00", () => lighting.LocalLightScale, v => lighting.LocalLightScale = v));
            light.Content.Add(LightingSlider("Emission", 0f, 4f, "0.00", () => lighting.EmissionScale, v => lighting.EmissionScale = v));
            light.Content.Add(DebugUi.Heading("Bloom"));
            light.Content.Add(LightingSlider("Threshold", 0f, 16f, "0.0", () => lighting.BloomThreshold, v => lighting.BloomThreshold = v));
            light.Content.Add(LightingSlider("Knee", 0.01f, 8f, "0.0", () => lighting.BloomKnee, v => lighting.BloomKnee = v));
            light.Content.Add(LightingSlider("Strength", 0f, 3f, "0.00", () => lighting.BloomStrength, v => lighting.BloomStrength = v));
            light.Content.Add(DebugUi.Heading("Shadows"));
            light.Content.Add(LightingSlider("Point lights", 0f, 8f, "0", () => lighting.ShadowedPointLights, v => lighting.ShadowedPointLights = (int)v, step: 1f));
            light.Content.Add(LightingSlider("Distance", 10f, 120f, "0 m", () => lighting.ShadowDistance, v => lighting.ShadowDistance = v, step: 1f));

            // ---------------------------------------------------------------- wind
            var wind = window.Content.Add(new Foldout("Wind"));
            wind.Content.Add(WindSlider("Speed", 0f, 30f, "0.0 m/s", w => w.Speed, (ref WindSingleton w, float v) => w.Speed = v));
            wind.Content.Add(WindSlider("Direction", -180f, 180f, "0°", w => w.Direction, (ref WindSingleton w, float v) => w.Direction = v, step: 1f));
            wind.Content.Add(WindSlider("Gustiness", 0f, 1.5f, "0.00", w => w.Gustiness, (ref WindSingleton w, float v) => w.Gustiness = v));
            wind.Content.Add(WindSlider("Gust size", 2f, 120f, "0 m", w => w.GustScale, (ref WindSingleton w, float v) => w.GustScale = v));
            wind.Content.Add(WindSlider("Turbulence", 0f, 1.5f, "0.00", w => w.Turbulence, (ref WindSingleton w, float v) => w.Turbulence = v));
            wind.Content.Add(WindSlider("Swirl size", 1f, 60f, "0.0 m", w => w.TurbulenceScale, (ref WindSingleton w, float v) => w.TurbulenceScale = v));
            wind.Content.Add(WindSlider("Response", 0f, 10f, "0.0 s", w => w.ResponseTime, (ref WindSingleton w, float v) => w.ResponseTime = v));
            wind.Content.Add(DebugUi.Heading("Vegetation"));
            wind.Content.Add(WindSlider("Sway freq.", 0.2f, 4f, "0.00 Hz", w => w.VegetationFrequency, (ref WindSingleton w, float v) => w.VegetationFrequency = v));
            wind.Content.Add(WindSlider("Damping", 0.02f, 1.5f, "0.00", w => w.VegetationDamping, (ref WindSingleton w, float v) => w.VegetationDamping = v));
            wind.Content.Add(WindSlider("Max bend", 0f, 1.5f, "0.00 rad", w => w.VegetationMaxBend, (ref WindSingleton w, float v) => w.VegetationMaxBend = v));
            wind.Content.Add(WindSlider("Ref. speed", 1f, 30f, "0.0 m/s", w => w.VegetationReferenceSpeed, (ref WindSingleton w, float v) => w.VegetationReferenceSpeed = v));
            wind.Content.Add(WindSlider("Flutter", 0f, 0.5f, "0.00 rad", w => w.VegetationFlutter, (ref WindSingleton w, float v) => w.VegetationFlutter = v));
            wind.Content.Add(WindSlider("Trample rec.", 0.5f, 60f, "0.0 s", w => w.FlattenRecoveryTime, (ref WindSingleton w, float v) => w.FlattenRecoveryTime = v));
            var windGrid = wind.Content.Add(DebugUi.KeyValueGrid(0.3f));
            DebugUi.AddRow(windGrid, "Live wind", () => Wind.TryGet(Engine.Instance?.world, out var live)
                ? DebugUi.Inv($"{live.Velocity.Length:0.0} m/s towards {MathHelper.RadiansToDegrees(MathF.Atan2(live.Velocity.X, live.Velocity.Y)):0}°")
                : "no wind singleton");
            wind.Content.Add(new Button("Blast at player", () =>
            {
                var world = Engine.Instance?.world;
                if (world == null || !TryGetPlayerPosition(world, out Vector3 position)) return;
                Wind.AddBlast(world, position, radius: 18f, strength: 25f, duration: 1.6f);
            }));

            // ---------------------------------------------------------------- grass
            var grass = window.Content.Add(new Foldout("Grass"));
            grass.Content.Add(new Checkbox("Enabled").Bind(
                () => TryGetGrass(out var settings) && settings.Enabled,
                value => EditGrass((ref GrassSingleton g) => g.Enabled = value)));
            grass.Content.Add(DebugUi.Heading("Field"));
            grass.Content.Add(GrassSlider("Density", 4f, 300f, "0 /m²", g => g.Density, (ref GrassSingleton g, float v) => g.Density = v, step: 1f));
            grass.Content.Add(GrassSlider("Draw distance", 10f, 200f, "0 m", g => g.DrawDistance, (ref GrassSingleton g, float v) => g.DrawDistance = v, step: 1f));
            grass.Content.Add(GrassSlider("Thin from", 1f, 60f, "0.0 m", g => g.FalloffStart, (ref GrassSingleton g, float v) => g.FalloffStart = v));
            grass.Content.Add(GrassSlider("Min density", 0.01f, 1f, "0.00", g => g.MinDensity, (ref GrassSingleton g, float v) => g.MinDensity = v));
            grass.Content.Add(GrassSlider("LOD distance", 0f, 80f, "0.0 m", g => g.LodDistance, (ref GrassSingleton g, float v) => g.LodDistance = v));
            grass.Content.Add(DebugUi.Heading("Blades"));
            grass.Content.Add(GrassSlider("Height", 0.05f, 2.5f, "0.00 m", g => g.Height, (ref GrassSingleton g, float v) => g.Height = v));
            grass.Content.Add(GrassSlider("Height var.", 0f, 1f, "0.00", g => g.HeightVariation, (ref GrassSingleton g, float v) => g.HeightVariation = v));
            grass.Content.Add(GrassSlider("Width", 0.005f, 0.2f, "0.000 m", g => g.Width, (ref GrassSingleton g, float v) => g.Width = v));
            grass.Content.Add(GrassSlider("Tilt", 0f, 1.2f, "0.00 rad", g => g.Tilt, (ref GrassSingleton g, float v) => g.Tilt = v));
            grass.Content.Add(GrassSlider("Tilt var.", 0f, 1f, "0.00 rad", g => g.TiltVariation, (ref GrassSingleton g, float v) => g.TiltVariation = v));
            grass.Content.Add(GrassSlider("Droop", 0f, 1.5f, "0.00 rad", g => g.Droop, (ref GrassSingleton g, float v) => g.Droop = v));
            grass.Content.Add(GrassSlider("Roundness", 0f, 1.5f, "0.00", g => g.Roundness, (ref GrassSingleton g, float v) => g.Roundness = v));
            grass.Content.Add(GrassSlider("Thickening", 0f, 1f, "0.00", g => g.Thickening, (ref GrassSingleton g, float v) => g.Thickening = v));
            grass.Content.Add(DebugUi.Heading("Tufts"));
            grass.Content.Add(GrassSlider("Size", 0.1f, 3f, "0.00 m", g => g.ClumpSize, (ref GrassSingleton g, float v) => g.ClumpSize = v));
            grass.Content.Add(GrassSlider("Pull", 0f, 0.9f, "0.00", g => g.ClumpPull, (ref GrassSingleton g, float v) => g.ClumpPull = v));
            grass.Content.Add(GrassSlider("Height var.", 0f, 1f, "0.00", g => g.ClumpHeightVariation, (ref GrassSingleton g, float v) => g.ClumpHeightVariation = v));
            grass.Content.Add(GrassSlider("Splay", 0f, 1f, "0.00", g => g.ClumpSplay, (ref GrassSingleton g, float v) => g.ClumpSplay = v));
            grass.Content.Add(DebugUi.Heading("Rolling"));
            grass.Content.Add(GrassSlider("Swell", 0f, 1f, "0.00", g => g.Swell, (ref GrassSingleton g, float v) => g.Swell = v));
            grass.Content.Add(GrassSlider("Swell size", 2f, 60f, "0.0 m", g => g.SwellSize, (ref GrassSingleton g, float v) => g.SwellSize = v));
            grass.Content.Add(GrassSlider("Comb", 0f, 1f, "0.00", g => g.Comb, (ref GrassSingleton g, float v) => g.Comb = v));
            grass.Content.Add(GrassSlider("Comb size", 2f, 80f, "0.0 m", g => g.CombSize, (ref GrassSingleton g, float v) => g.CombSize = v));
            grass.Content.Add(DebugUi.Heading("Wind"));
            grass.Content.Add(GrassSlider("Wind gain", 0f, 3f, "0.00", g => g.WindGain, (ref GrassSingleton g, float v) => g.WindGain = v));
            grass.Content.Add(GrassSlider("Waves", 0f, 1.2f, "0.00 rad", g => g.WaveStrength, (ref GrassSingleton g, float v) => g.WaveStrength = v));
            grass.Content.Add(GrassSlider("Wave length", 2f, 40f, "0.0 m", g => g.WaveLength, (ref GrassSingleton g, float v) => g.WaveLength = v));
            grass.Content.Add(GrassSlider("Wind sheen", 0f, 1f, "0.00", g => g.WindSheen, (ref GrassSingleton g, float v) => g.WindSheen = v));
            grass.Content.Add(DebugUi.Heading("Look"));
            grass.Content.Add(GrassSlider("Colour var.", 0f, 1f, "0.00", g => g.ColourVariation, (ref GrassSingleton g, float v) => g.ColourVariation = v));
            grass.Content.Add(GrassSlider("Dry patches", 0f, 1f, "0.00", g => g.DryPatches, (ref GrassSingleton g, float v) => g.DryPatches = v));
            grass.Content.Add(GrassSlider("Translucency", 0f, 1f, "0.00", g => g.Translucency, (ref GrassSingleton g, float v) => g.Translucency = v));
            grass.Content.Add(GrassSlider("Roughness", 0.05f, 1f, "0.00", g => g.Roughness, (ref GrassSingleton g, float v) => g.Roughness = v));
            grass.Content.Add(GrassSlider("Ground tint", 0f, 1f, "0.00", g => g.GroundTint, (ref GrassSingleton g, float v) => g.GroundTint = v));
            grass.Content.Add(GrassSlider("Hue drift", 0f, 1f, "0.00", g => g.HueDrift, (ref GrassSingleton g, float v) => g.HueDrift = v));
            grass.Content.Add(GrassSlider("Canopy shade", 0f, 1f, "0.00", g => g.CanopyShade, (ref GrassSingleton g, float v) => g.CanopyShade = v));
            grass.Content.Add(GrassSlider("Field normal", 0f, 1f, "0.00", g => g.FieldNormal, (ref GrassSingleton g, float v) => g.FieldNormal = v));
            grass.Content.Add(DebugUi.Heading("Flowers"));
            grass.Content.Add(GrassSlider("Blooms", 0f, 3f, "0.00", g => g.BloomAmount, (ref GrassSingleton g, float v) => g.BloomAmount = v));
            grass.Content.Add(GrassSlider("Bloom size", 0.05f, 0.6f, "0.00 m", g => g.BloomSize, (ref GrassSingleton g, float v) => g.BloomSize = v));
            grass.Content.Add(GrassSlider("Plumes", 0f, 3f, "0.00", g => g.PlumeAmount, (ref GrassSingleton g, float v) => g.PlumeAmount = v));
            grass.Content.Add(DebugUi.Heading("Shadows"));
            grass.Content.Add(GrassSlider("Distance", 0f, 150f, "0 m", g => g.ShadowDistance, (ref GrassSingleton g, float v) => g.ShadowDistance = v, step: 1f));
            grass.Content.Add(GrassSlider("Density", 0.05f, 1f, "0.00", g => g.ShadowDensity, (ref GrassSingleton g, float v) => g.ShadowDensity = v));
            var grassGrid = grass.Content.Add(DebugUi.KeyValueGrid(0.3f));
            DebugUi.AddRow(grassGrid, "Blades drawn", () => DebugUi.Inv(
                $"{Engine.RenderEngine._grassRenderer.NearBladesDrawn:N0} near, {Engine.RenderEngine._grassRenderer.FarBladesDrawn:N0} far"));

            // ---------------------------------------------------------------- fog
            var fog = window.Content.Add(new Foldout("Volumetric fog"));
            fog.Content.Add(new Checkbox("Enabled").Bind(
                () => TryGetFog(out var settings) && settings.Enabled,
                value => EditFog((ref VolumetricFogSingleton f) => f.Enabled = value)));
            fog.Content.Add(DebugUi.Heading("Shape"));
            fog.Content.Add(FogSlider("Density", 0f, 3f, "0.00", f => f.Density, (ref VolumetricFogSingleton f, float v) => f.Density = v));
            fog.Content.Add(FogSlider("Height", 0.25f, 20f, "0.0 m", f => f.Height, (ref VolumetricFogSingleton f, float v) => f.Height = v));
            fog.Content.Add(FogSlider("Falloff", 0.25f, 5f, "0.00", f => f.HeightFalloff, (ref VolumetricFogSingleton f, float v) => f.HeightFalloff = v));
            fog.Content.Add(FogSlider("Coverage", 0f, 1f, "0.00", f => f.Coverage, (ref VolumetricFogSingleton f, float v) => f.Coverage = v));
            fog.Content.Add(FogSlider("Softness", 0.02f, 1f, "0.00", f => f.EdgeSoftness, (ref VolumetricFogSingleton f, float v) => f.EdgeSoftness = v));
            fog.Content.Add(FogSlider("Follow terrain", 0f, 1f, "0.00", f => f.TerrainFollow, (ref VolumetricFogSingleton f, float v) => f.TerrainFollow = v));
            fog.Content.Add(FogSlider("Base height", -50f, 150f, "0.0 m", f => f.BaseHeight, (ref VolumetricFogSingleton f, float v) => f.BaseHeight = v));
            fog.Content.Add(DebugUi.Heading("Motion"));
            fog.Content.Add(FogSlider("Noise scale", 4f, 150f, "0 m", f => f.NoiseScale, (ref VolumetricFogSingleton f, float v) => f.NoiseScale = v));
            fog.Content.Add(FogSlider("Turbulence", 0f, 1f, "0.00", f => f.Turbulence, (ref VolumetricFogSingleton f, float v) => f.Turbulence = v));
            fog.Content.Add(FogSlider("Detail", 0f, 0.9f, "0.00", f => f.Detail, (ref VolumetricFogSingleton f, float v) => f.Detail = v));
            fog.Content.Add(FogSlider("Top heave", 0f, 1f, "0.00", f => f.TopVariation, (ref VolumetricFogSingleton f, float v) => f.TopVariation = v));
            fog.Content.Add(FogSlider("Wind drift", 0f, 1.5f, "0.00", f => f.WindInfluence, (ref VolumetricFogSingleton f, float v) => f.WindInfluence = v));
            fog.Content.Add(FogSlider("Gust push", 0f, 3f, "0.00 s", f => f.GustResponse, (ref VolumetricFogSingleton f, float v) => f.GustResponse = v));
            fog.Content.Add(FogSlider("Roll speed", 0f, 3f, "0.00", f => f.RollSpeed, (ref VolumetricFogSingleton f, float v) => f.RollSpeed = v));
            fog.Content.Add(DebugUi.Heading("Lighting"));
            fog.Content.Add(FogSlider("Directional", 0f, 4f, "0.00", f => f.DirectionalScattering, (ref VolumetricFogSingleton f, float v) => f.DirectionalScattering = v));
            fog.Content.Add(FogSlider("Point & spot", 0f, 4f, "0.00", f => f.LocalLightScattering, (ref VolumetricFogSingleton f, float v) => f.LocalLightScattering = v));
            fog.Content.Add(FogSlider("Ambient", 0f, 4f, "0.00", f => f.AmbientScattering, (ref VolumetricFogSingleton f, float v) => f.AmbientScattering = v));
            fog.Content.Add(FogSlider("Anisotropy", -0.5f, 0.9f, "0.00", f => f.Anisotropy, (ref VolumetricFogSingleton f, float v) => f.Anisotropy = v));
            fog.Content.Add(FogSlider("Haze", 0f, 0.02f, "0.0000", f => f.HazeDensity, (ref VolumetricFogSingleton f, float v) => f.HazeDensity = v));
            fog.Content.Add(DebugUi.Heading("Quality"));
            fog.Content.Add(FogSlider("Max steps", 4f, 128f, "0", f => f.MaxSteps, (ref VolumetricFogSingleton f, float v) => f.MaxSteps = (int)v, step: 1f));
            fog.Content.Add(FogSlider("Temporal", 0f, 0.98f, "0.00", f => f.TemporalBlend, (ref VolumetricFogSingleton f, float v) => f.TemporalBlend = v));

            // ---------------------------------------------------------------- water
            var water = window.Content.Add(new Foldout("Water"));
            water.Content.Add(new Checkbox("Enabled").Bind(
                () => Water.TryGet(Engine.Instance?.world, out var w) && w.Enabled,
                value => EditWater((ref WaterSingleton w) => w.Enabled = value)));
            water.Content.Add(new Checkbox("Ocean").Bind(
                () => Water.TryGet(Engine.Instance?.world, out var w) && w.OceanEnabled,
                value => EditWater((ref WaterSingleton w) => w.OceanEnabled = value)));
            water.Content.Add(new Checkbox("Screen space reflections").Bind(
                () => Water.TryGet(Engine.Instance?.world, out var w) && w.ScreenSpaceReflections,
                value => EditWater((ref WaterSingleton w) => w.ScreenSpaceReflections = value)));
            water.Content.Add(WaterSlider("Sea level", -10f, 20f, "0.00 m", w => w.SeaLevel, (ref WaterSingleton w, float v) => w.SeaLevel = v, step: 0.05f));
            water.Content.Add(DebugUi.Heading("Shore waves"));
            water.Content.Add(WaterSlider("Height", 0f, 3f, "0.00 m", w => w.ShoreWaveHeight, (ref WaterSingleton w, float v) => w.ShoreWaveHeight = v));
            water.Content.Add(WaterSlider("Period", 2f, 14f, "0.0 s", w => w.ShoreWavePeriod, (ref WaterSingleton w, float v) => w.ShoreWavePeriod = v));
            water.Content.Add(WaterSlider("Wavelength", 2f, 40f, "0.0 m", w => w.ShoreWaveLength, (ref WaterSingleton w, float v) => w.ShoreWaveLength = v));
            water.Content.Add(WaterSlider("Reach", 5f, 80f, "0 m", w => w.ShoreWaveReach, (ref WaterSingleton w, float v) => w.ShoreWaveReach = v));
            water.Content.Add(WaterSlider("Run-up", 0f, 2f, "0.00", w => w.RunUp, (ref WaterSingleton w, float v) => w.RunUp = v));
            water.Content.Add(WaterSlider("Sets", 0f, 1f, "0.00", w => w.SetVariation, (ref WaterSingleton w, float v) => w.SetVariation = v));
            water.Content.Add(WaterSlider("Wind sea", 0f, 1.5f, "0.00", w => w.WindWaveHeight, (ref WaterSingleton w, float v) => w.WindWaveHeight = v));
            water.Content.Add(WaterSlider("Sea response", 0f, 120f, "0 s", w => w.SeaStateResponseTime, (ref WaterSingleton w, float v) => w.SeaStateResponseTime = v));
            water.Content.Add(DebugUi.Heading("Surface"));
            water.Content.Add(WaterSlider("Ripples", 0f, 3f, "0.00", w => w.RippleScale, (ref WaterSingleton w, float v) => w.RippleScale = v));
            water.Content.Add(WaterSlider("Ripple drift", 0f, 0.5f, "0.00", w => w.RippleDrift, (ref WaterSingleton w, float v) => w.RippleDrift = v));
            water.Content.Add(WaterSlider("Caustics", 0f, 3f, "0.00", w => w.CausticStrength, (ref WaterSingleton w, float v) => w.CausticStrength = v));
            water.Content.Add(WaterSlider("Refraction", 0f, 3f, "0.00", w => w.RefractionStrength, (ref WaterSingleton w, float v) => w.RefractionStrength = v));
            water.Content.Add(WaterSlider("Foam", 0f, 3f, "0.00", w => w.Foam, (ref WaterSingleton w, float v) => w.Foam = v));
            water.Content.Add(WaterSlider("Whitecaps", 0f, 5f, "0.00", w => w.Whitecaps, (ref WaterSingleton w, float v) => w.Whitecaps = v));
            water.Content.Add(WaterSlider("Wet sand", 0f, 0.9f, "0.00", w => w.WetSandDarkening, (ref WaterSingleton w, float v) => w.WetSandDarkening = v));
            water.Content.Add(WaterSlider("Drying", 1f, 60f, "0 s", w => w.DryingTime, (ref WaterSingleton w, float v) => w.DryingTime = v));
            water.Content.Add(DebugUi.Heading("Wading ripples"));
            water.Content.Add(WaterSlider("Speed", 0.2f, 3f, "0.00 m/s", w => w.RippleSpeed, (ref WaterSingleton w, float v) => w.RippleSpeed = v));
            water.Content.Add(WaterSlider("Decay", 0.2f, 6f, "0.0 s", w => w.RippleDecay, (ref WaterSingleton w, float v) => w.RippleDecay = v));
            water.Content.Add(WaterSlider("Strength", 0f, 3f, "0.00", w => w.RippleNormalStrength, (ref WaterSingleton w, float v) => w.RippleNormalStrength = v));
            water.Content.Add(DebugUi.Heading("Sea colour (absorption, scattering per metre)"));
            water.Content.Add(WaterSlider("Absorb R", 0f, 1.5f, "0.000", w => w.OceanOptics.Absorption.X, (ref WaterSingleton w, float v) => w.OceanOptics.Absorption.X = v));
            water.Content.Add(WaterSlider("Absorb G", 0f, 1.5f, "0.000", w => w.OceanOptics.Absorption.Y, (ref WaterSingleton w, float v) => w.OceanOptics.Absorption.Y = v));
            water.Content.Add(WaterSlider("Absorb B", 0f, 1.5f, "0.000", w => w.OceanOptics.Absorption.Z, (ref WaterSingleton w, float v) => w.OceanOptics.Absorption.Z = v));
            water.Content.Add(WaterSlider("Scatter R", 0f, 0.2f, "0.000", w => w.OceanOptics.Scattering.X, (ref WaterSingleton w, float v) => w.OceanOptics.Scattering.X = v));
            water.Content.Add(WaterSlider("Scatter G", 0f, 0.2f, "0.000", w => w.OceanOptics.Scattering.Y, (ref WaterSingleton w, float v) => w.OceanOptics.Scattering.Y = v));
            water.Content.Add(WaterSlider("Scatter B", 0f, 0.2f, "0.000", w => w.OceanOptics.Scattering.Z, (ref WaterSingleton w, float v) => w.OceanOptics.Scattering.Z = v));
            var waterGrid = water.Content.Add(DebugUi.KeyValueGrid(0.3f));
            DebugUi.AddRow(waterGrid, "Sea state", () => Water.TryGet(Engine.Instance?.world, out var w)
                ? DebugUi.Inv($"{w.SeaState:0.0} m/s wind, breakers {WaterWaves.ShoreBreakerHeight(w):0.00} m")
                : "no water singleton");
            water.Content.Add(new Button("Splash at player", () =>
            {
                var world = Engine.Instance?.world;
                if (world == null || !TryGetPlayerPosition(world, out Vector3 position)) return;
                Water.AddSplash(world, position, radius: 1.2f, strength: 0.06f);
            }));

            // ---------------------------------------------------------------- camera
            var camera = window.Content.Add(new Foldout("Camera"));
            camera.Content.Add(DebugUi.LabeledRow("Field of view", new Slider(20f, 120f) { Format = "0°", Step = 1f, FormatProvider = DebugUi.Culture }.Bind(
                () => TryGetCameraProjection(out var projection) ? MathHelper.RadiansToDegrees(projection.FieldOfView) : 60f,
                value =>
                {
                    if (!TryGetCameraProjection(out var projection)) return;
                    projection.FieldOfView = MathHelper.DegreesToRadians(value);
                    projection.RebuildMatrix();
                    var world = Engine.Instance!.world;
                    world.GetEntityView(world.Camera).Set(projection);
                })));
            var cameraGrid = camera.Content.Add(DebugUi.KeyValueGrid(0.3f));
            DebugUi.AddRow(cameraGrid, "Position", () => DebugOverlay.FormatVector(Engine.RenderEngine.context.viewPos));

            // ---------------------------------------------------------------- g-buffer
            var buffers = window.Content.Add(new Foldout("Buffers"));
            var selector = new Dropdown(BufferNames, 0);
            buffers.Content.Add(DebugUi.LabeledRow("Show", selector));
            var image = buffers.Content.Add(new Image
            {
                Width = UIValue.Fill,
                Height = UIValue.Auto,
                FlipY = true,
                Mode = GuiTextureMode.Opaque,
                BorderColor = theme.Separator,
                BorderWidth = 1f,
                BackgroundColor = theme.InputBackground,
            });
            image.Updated += _ =>
            {
                Vector2i resolution = Engine.Resolution;
                if (resolution.Y > 0) image.AspectRatio = (float)resolution.X / resolution.Y;
                image.Mode = selector.SelectedIndex == 4 ? GuiTextureMode.RedAsGray : GuiTextureMode.Opaque;
            };
            image.TextureSource = () => selector.SelectedIndex switch
            {
                0 => Engine.RenderEngine.lastUsedBuffer.GetLastOutputTexture(),
                1 => Engine.RenderEngine.GBuffer.GetAttachment(0),
                2 => Engine.RenderEngine.GBuffer.GetAttachment(1),
                3 => Engine.RenderEngine.GBuffer.GetAttachment(2),
                4 => Engine.RenderEngine.GBuffer.getDepthAttachment(),
                _ => 0,
            };
            buffers.Content.Add(DebugUi.Muted("Depth is non-linear, so most of the scene appears near white.", wrap: true));
            return window;
        }

        // ---------------------------------------------------------------- helpers

        private static GuiElement LightingSlider(string caption, float min, float max, string format, Func<float> get, Action<float> set, float step = 0f)
        {
            return DebugUi.LabeledRow(caption, new Slider(min, max) { Format = format, Step = step, FormatProvider = DebugUi.Culture }.Bind(get, set));
        }

        private static bool TryGetSky(out Entity entity, out ColorComponent colour)
        {
            entity = default;
            colour = default;
            var world = Engine.Instance?.world;
            if (world == null) return false;
            List<Entity> skies = world.QueryEntities(new BitMask(typeof(SkyTag), typeof(ColorComponent)), BitMask.Empty);
            if (skies.Count == 0) return false;
            entity = skies[0];
            colour = world.GetComponent<ColorComponent>(entity);
            return true;
        }

        private delegate void WindSetter(ref WindSingleton wind, float value);

        private static GuiElement WindSlider(string caption, float min, float max, string format, Func<WindSingleton, float> get, WindSetter set, float step = 0f)
        {
            return DebugUi.LabeledRow(caption, new Slider(min, max) { Format = format, Step = step, FormatProvider = DebugUi.Culture }.Bind(
                () => Wind.TryGet(Engine.Instance?.world, out var wind) ? get(wind) : min,
                value =>
                {
                    var world = Engine.Instance?.world;
                    if (world == null || !world.TryGetSingleton<WindSingleton>(out Entity entity)) return;
                    var wind = world.GetComponent<WindSingleton>(entity);
                    set(ref wind, value);
                    world.GetEntityView(entity).Set(wind);
                }));
        }

        private static bool TryGetPlayerPosition(ECSWorld world, out Vector3 position)
        {
            position = default;
            List<Entity> players = world.QueryEntities(new BitMask(typeof(PlayerTag), typeof(LocalToWorldMatrixComponent)), BitMask.Empty);
            if (players.Count == 0) return false;
            position = world.GetComponent<LocalToWorldMatrixComponent>(players[0]).value.ExtractTranslation();
            return true;
        }

        private delegate void GrassSetter(ref GrassSingleton grass, float value);
        private delegate void GrassEdit(ref GrassSingleton grass);

        private static GuiElement GrassSlider(string caption, float min, float max, string format, Func<GrassSingleton, float> get, GrassSetter set, float step = 0f)
        {
            return DebugUi.LabeledRow(caption, new Slider(min, max) { Format = format, Step = step, FormatProvider = DebugUi.Culture }.Bind(
                () => TryGetGrass(out var settings) ? get(settings) : min,
                value => EditGrass((ref GrassSingleton g) => set(ref g, value))));
        }

        private static bool TryGetGrass(out GrassSingleton settings)
        {
            settings = default;
            var world = Engine.Instance?.world;
            if (world == null || !world.TryGetSingleton<GrassSingleton>(out Entity entity)) return false;
            settings = world.GetComponent<GrassSingleton>(entity);
            return true;
        }

        private static void EditGrass(GrassEdit edit)
        {
            var world = Engine.Instance?.world;
            if (world == null || !world.TryGetSingleton<GrassSingleton>(out Entity entity)) return;
            var settings = world.GetComponent<GrassSingleton>(entity);
            edit(ref settings);
            world.GetEntityView(entity).Set(settings);
        }

        private delegate void FogSetter(ref VolumetricFogSingleton fog, float value);
        private delegate void FogEdit(ref VolumetricFogSingleton fog);

        private static GuiElement FogSlider(string caption, float min, float max, string format, Func<VolumetricFogSingleton, float> get, FogSetter set, float step = 0f)
        {
            return DebugUi.LabeledRow(caption, new Slider(min, max) { Format = format, Step = step, FormatProvider = DebugUi.Culture }.Bind(
                () => TryGetFog(out var settings) ? get(settings) : min,
                value => EditFog((ref VolumetricFogSingleton f) => set(ref f, value))));
        }

        private static bool TryGetFog(out VolumetricFogSingleton settings)
        {
            settings = default;
            var world = Engine.Instance?.world;
            if (world == null || !world.TryGetSingleton<VolumetricFogSingleton>(out Entity entity)) return false;
            settings = world.GetComponent<VolumetricFogSingleton>(entity);
            return true;
        }

        private static void EditFog(FogEdit edit)
        {
            var world = Engine.Instance?.world;
            if (world == null || !world.TryGetSingleton<VolumetricFogSingleton>(out Entity entity)) return;
            var settings = world.GetComponent<VolumetricFogSingleton>(entity);
            edit(ref settings);
            world.GetEntityView(entity).Set(settings);
        }

        private delegate void WaterSetter(ref WaterSingleton water, float value);
        private delegate void WaterEdit(ref WaterSingleton water);

        private static GuiElement WaterSlider(string caption, float min, float max, string format, Func<WaterSingleton, float> get, WaterSetter set, float step = 0f)
        {
            return DebugUi.LabeledRow(caption, new Slider(min, max) { Format = format, Step = step, FormatProvider = DebugUi.Culture }.Bind(
                () => Water.TryGet(Engine.Instance?.world, out var water) ? get(water) : min,
                value => EditWater((ref WaterSingleton w) => set(ref w, value))));
        }

        private static void EditWater(WaterEdit edit)
        {
            var world = Engine.Instance?.world;
            if (world == null || !world.TryGetSingleton<WaterSingleton>(out Entity entity)) return;
            var water = world.GetComponent<WaterSingleton>(entity);
            edit(ref water);
            world.GetEntityView(entity).Set(water);
        }

        private static bool TryGetSun<T>(out T component) where T : struct, IComponent
        {
            component = default;
            var world = Engine.Instance?.world;
            if (world == null || !world.HasComponent<T>(world.sun)) return false;
            component = world.GetComponent<T>(world.sun);
            return true;
        }

        private static void SetSun<T>(T component) where T : struct, IComponent
        {
            var world = Engine.Instance?.world;
            if (world == null || !world.HasComponent<T>(world.sun)) return;
            world.GetEntityView(world.sun).Set(component);
        }

        /// <summary>The light direction points from the sun towards the ground.</summary>
        private static (float azimuth, float elevation) SunAngles()
        {
            if (!TryGetSun<DirectionNormalizedComponent>(out var direction)) return (0f, 45f);
            Vector3 d = direction.value;
            float elevation = MathHelper.RadiansToDegrees(MathF.Asin(Math.Clamp(-d.Y, -1f, 1f)));
            float azimuth = MathHelper.RadiansToDegrees(MathF.Atan2(d.X, d.Z));
            return (azimuth, elevation);
        }

        private static void SetSunAngles(float azimuthDegrees, float elevationDegrees)
        {
            if (!TryGetSun<DirectionNormalizedComponent>(out var direction)) return;
            float azimuth = MathHelper.DegreesToRadians(azimuthDegrees);
            float elevation = MathHelper.DegreesToRadians(elevationDegrees);
            float horizontal = MathF.Cos(elevation);
            direction.value = new Vector3(horizontal * MathF.Sin(azimuth), -MathF.Sin(elevation), horizontal * MathF.Cos(azimuth));
            SetSun(direction);
        }

        private static bool TryGetCameraProjection(out PerspectiveProjectionComponent projection)
        {
            projection = default;
            var world = Engine.Instance?.world;
            if (world == null || !world.HasComponent<PerspectiveProjectionComponent>(world.Camera)) return false;
            projection = world.GetComponent<PerspectiveProjectionComponent>(world.Camera);
            return true;
        }
    }
}
