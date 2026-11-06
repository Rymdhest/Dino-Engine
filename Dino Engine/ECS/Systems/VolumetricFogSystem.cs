using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PostProcessing;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Animates the volumetric fog and submits it, together with the terrain under the camera, to the
    /// <see cref="VolumetricFogRenderer"/>. Lights reach the renderer from the light systems.
    /// </summary>
    public class VolumetricFogSystem : SystemBase
    {
        // Must match VolumetricFog_March.frag.
        private const float WarpFrequency = 0.5f;
        private const float DetailFrequency = 3.0f;
        private static readonly Vector3 NoiseStretch = new Vector3(1f, 2f, 1f);

        // The warp field drifts slower than the billows it bends and sinks while they rise, so the billows
        // churn and roll over instead of sliding along rigidly. The detail wisps outrun both and lift off.
        private const float WarpWindFactor = 0.45f;
        private const float DetailWindFactor = 1.6f;
        private const float BaseRise = 0.35f;
        private const float WarpRise = -0.5f;
        private const float DetailRise = 1.2f;

        // What each kind of weather makes of the fog. What makes it is the clear ground between its banks (Banks below): a layer with no gaps in it is a veil over the game. The billows sit around
        // 0.55, so a coverage much under 0.8 is hardly there at all. NoiseScale isn't here, and mustn't be: changing it scales the pattern about the far-off world origin, and every billow slides.
        // Glow is how much sky light it scatters. Fog no brighter than the ground it lies on has no shape to see, and only takes the contrast out of what's under it.
        private readonly record struct FogLook(float Height, float Falloff, float Coverage, float Softness, float Detail, float Turbulence, float Roll, float Anisotropy, float Glow);
        private static readonly FogLook MistLook = new(3.4f, 1.4f, 0.86f, 0.42f, 0.35f, 0.4f, 0.5f, 0.3f, 3f);
        private static readonly FogLook SteamLook = new(1.8f, 1.2f, 0.78f, 0.3f, 0.6f, 0.6f, 1.6f, 0.45f, 3f);
        private static readonly FogLook AshLook = new(2.6f, 1.4f, 0.84f, 0.45f, 0.45f, 0.4f, 0.3f, 0.2f, 1.5f);
        private static readonly FogLook DriftLook = new(1.3f, 1.2f, 0.84f, 0.3f, 0.6f, 0.55f, 0.4f, 0.4f, 3f);
        private static readonly FogLook DustLook = new(3.6f, 1.4f, 0.86f, 0.42f, 0.5f, 0.5f, 0.8f, 0.55f, 2.5f);

        private const float WeatherBanks = 0.85f;

        private static readonly Vector3 AshColour = new Vector3(0.42f, 0.4f, 0.4f);
        private static readonly Vector3 SnowColour = new Vector3(0.95f, 0.97f, 1f);

        // Offsets are kept in noise space and wrapped to the noise tile, so they never lose precision and
        // changing the wind or noise scale never makes the fog jump.
        private Vector3 _baseOffset;
        private Vector3 _warpOffset;
        private Vector3 _detailOffset;

        private readonly List<TerrainChunkRenderData> _terrainChunks = new();

        public VolumetricFogSystem()
            : base(new BitMask(typeof(VolumetricFogSingleton)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var fog = entity.Get<VolumetricFogSingleton>();
            if (!fog.Enabled || !Engine.RenderEngine.Lighting.Fog || !world.Camera.IsValid()) return;

            // What's in the air makes the fog what it is. Like the glow below, this is only ever this frame's.
            float sunThrough = 1f;
            float weatherShare = 0f;
            if (Weather.TryGet(world, out WeatherSingleton weather) && weather.Enabled)
            {
                weatherShare = Thicken(ref fog, weather);
                sunThrough = Weather.SunThrough(weather, weather.Here.Cloud);
            }

            Advance(fog, DriftVelocity(Wind.MeanVelocity(world) * fog.WindInfluence, weatherShare), deltaTime);

            NightfallSingleton night = SkySYstem.NightOf(world);
            float glow = MathHelper.Lerp(1f, night.FogGlow, night.Amount);
            fog.AmbientScattering *= glow;
            fog.DirectionalScattering *= glow;

            var command = new VolumetricFogRenderCommand
            {
                settings = fog,
                sunThrough = sunThrough,
                baseOffset = _baseOffset,
                warpOffset = _warpOffset,
                detailOffset = _detailOffset,
            };

            // Centre the terrain capture a little ahead of the camera, where most of the view is.
            Vector3 cameraPosition = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();
            Matrix4 invView = Matrix4.Invert(world.GetComponent<ViewMatrixComponent>(world.Camera).value);
            Vector3 forward = (new Vector4(0f, 0f, -1f, 0f) * invView).Xyz.Normalized();
            command.terrainCaptureCenter = cameraPosition + new Vector3(forward.X, 0f, forward.Z) * fog.TerrainCaptureSize * 0.35f;
            command.terrainChunks = CollectTerrainChunks(world, fog, command.terrainCaptureCenter.Xz);

            Engine.RenderEngine._volumetricFogRenderer.SubmitCommand(command);
        }

        // Makes the fog what this weather makes it, and says how much of it is the weather's own. Ground fog needs still air and water to give, so a gale tears it up, and it's thickest as the
        // rain lets up over soaked ground. Where the sun gets through to that ground it steams. Wind is what lifts dust and drives snow along the ground, and they lie as low as fog does.
        private static float Thicken(ref VolumetricFogSingleton fog, in WeatherSingleton weather)
        {
            var here = weather.Here;
            float calm = MathHelper.Lerp(0.4f, 1f, Smoothstep(2.6f, 1f, here.Gale));
            float lifted = here.Wet * (1f - here.Fall);

            float mist = Math.Clamp((here.Mist + lifted * 0.35f) * calm, 0f, 1f);
            float steam = lifted * (1f - here.Cloud) * calm;
            float ash = here.Fall * here.Ash;
            float drift = here.Snow * MathF.Max(here.Fall, here.SnowCover) * Smoothstep(1f, 2.4f, here.Gale);
            float dust = here.Dust;

            float share = 0f;
            Blend(ref fog, MistLook, weather.MistFog, null, mist, ref share);
            Blend(ref fog, SteamLook, weather.SteamFog, null, steam, ref share);
            Blend(ref fog, AshLook, weather.AshFog, AshColour, ash, ref share);
            Blend(ref fog, DriftLook, weather.DriftFog, SnowColour, drift, ref share);
            Blend(ref fog, DustLook, weather.DustFog, weather.DustColour, dust, ref share);

            fog.ClearingRadius = weather.FogClearing;
            fog.ClearingDepth = 0.75f * share;
            fog.Banks = WeatherBanks * share;
            fog.Pooling = weather.FogPooling * MathF.Max(mist, steam) * (1f - dust);
            return share;
        }

        private static void Blend(ref VolumetricFogSingleton fog, in FogLook look, float density, Vector3? colour, float weight, ref float share)
        {
            if (weight <= 0f) return;
            fog.Density = MathHelper.Lerp(fog.Density, density, weight);
            fog.Height = MathHelper.Lerp(fog.Height, look.Height, weight);
            fog.HeightFalloff = MathHelper.Lerp(fog.HeightFalloff, look.Falloff, weight);
            fog.Coverage = MathHelper.Lerp(fog.Coverage, look.Coverage, weight);
            fog.EdgeSoftness = MathHelper.Lerp(fog.EdgeSoftness, look.Softness, weight);
            fog.Detail = MathHelper.Lerp(fog.Detail, look.Detail, weight);
            fog.Turbulence = MathHelper.Lerp(fog.Turbulence, look.Turbulence, weight);
            fog.RollSpeed = MathHelper.Lerp(fog.RollSpeed, look.Roll, weight);
            fog.Anisotropy = MathHelper.Lerp(fog.Anisotropy, look.Anisotropy, weight);
            fog.AmbientScattering = MathHelper.Lerp(fog.AmbientScattering, look.Glow, weight);
            if (colour is Vector3 tint) fog.Albedo = Vector3.Lerp(fog.Albedo, tint, weight);
            share += (1f - share) * weight;
        }

        // Weather's fog travels at the speed the wind's own patterns do, which levels off: in a gale it should lean and tear, not race across the screen. Added to a running total, so it may change.
        private static Vector2 DriftVelocity(Vector2 wind, float weatherShare)
        {
            float speed = wind.Length;
            if (speed < 1e-3f) return wind;
            return wind * MathHelper.Lerp(1f, WindField.PatternSpeed(speed) / speed, weatherShare);
        }

        private static float Smoothstep(float from, float to, float value)
        {
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <param name="windVelocity">The mean wind the fog drifts with, on the xz plane in m/s.</param>
        private void Advance(VolumetricFogSingleton fog, Vector2 windVelocity, float deltaTime)
        {
            Vector3 wind = new Vector3(windVelocity.X, 0f, windVelocity.Y);
            Vector3 up = Vector3.UnitY * fog.RollSpeed;
            Vector3 toNoiseSpace = NoiseStretch * (deltaTime / MathF.Max(fog.NoiseScale, 0.1f));

            // Subtracting the offset moves the pattern along the velocity.
            _baseOffset = Wrap(_baseOffset - (wind + up * BaseRise) * toNoiseSpace);
            _warpOffset = Wrap(_warpOffset - (wind * WarpWindFactor + up * WarpRise) * toNoiseSpace * WarpFrequency);
            _detailOffset = Wrap(_detailOffset - (wind * DetailWindFactor + up * DetailRise) * toNoiseSpace * DetailFrequency);
        }

        private static Vector3 Wrap(Vector3 offset)
        {
            return new Vector3(offset.X - MathF.Floor(offset.X), offset.Y - MathF.Floor(offset.Y), offset.Z - MathF.Floor(offset.Z));
        }

        private TerrainChunkRenderData[] CollectTerrainChunks(ECSWorld world, VolumetricFogSingleton fog, Vector2 center)
        {
            _terrainChunks.Clear();
            if (fog.TerrainFollow <= 0f || !world.TryGetSingleton<TerrainQuadTreeSingleton>(out Entity terrainEntity)) return Array.Empty<TerrainChunkRenderData>();

            var terrain = world.GetComponent<TerrainQuadTreeSingleton>(terrainEntity);
            float halfSize = fog.TerrainCaptureSize * 0.5f + terrain.ChunkSize;
            foreach (var chunk in terrain.Chunks)
            {
                Vector2 chunkCenter = (new Vector2(chunk.Key.X, chunk.Key.Y) + new Vector2(0.5f)) * terrain.ChunkSize;
                if (MathF.Abs(chunkCenter.X - center.X) > halfSize || MathF.Abs(chunkCenter.Y - center.Y) > halfSize) continue;

                Entity chunkEntity = chunk.Value;
                _terrainChunks.Add(new TerrainChunkRenderData
                {
                    chunkPos = world.GetComponent<LocalToWorldMatrixComponent>(chunkEntity).value.ExtractTranslation(),
                    size = world.GetComponent<ScaleComponent>(chunkEntity).value,
                    arrayID = world.GetComponent<TerrainChunkComponent>(chunkEntity).normalHeightTextureArrayID,
                });
            }
            return _terrainChunks.ToArray();
        }
    }
}
