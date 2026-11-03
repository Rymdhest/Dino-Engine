using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
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
            if (!fog.Enabled || !world.Camera.IsValid()) return;

            Advance(fog, deltaTime);

            var command = new VolumetricFogRenderCommand
            {
                settings = fog,
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

        private void Advance(VolumetricFogSingleton fog, float deltaTime)
        {
            float angle = MathHelper.DegreesToRadians(fog.WindAngle);
            Vector3 wind = new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle)) * fog.WindSpeed;
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
