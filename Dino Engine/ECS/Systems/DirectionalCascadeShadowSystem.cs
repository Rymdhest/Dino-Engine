using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using static Dino_Engine.Modelling.Model.glModel;

namespace Dino_Engine.ECS.Systems
{
    public class DirectionalCascadeShadowSystem : SystemBase
    {
        private int minCountForInstanced = 10;

        // Persistent structures to avoid per-frame allocations
        private readonly Dictionary<glModel, List<Matrix4>> _models = new();
        private readonly List<ImposterInstanceData> _imposters = new();

        private readonly List<Entity> _visibleChunks = new();
        private readonly List<TerrainChunkRenderData> _terrainCommands = new();
        private readonly List<GrassChunkRenderData> _grassCommands = new();

        public DirectionalCascadeShadowSystem()
            : base(new BitMask(typeof(DirectionalLightTag), typeof(DirectionNormalizedComponent), typeof(DirectionalCascadingShadowComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var direction = entity.Get<DirectionNormalizedComponent>().value;
            var shadowCascade = entity.Get<DirectionalCascadingShadowComponent>();
            var cameraPos = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();

            // 1. Prepare View Matrices for Cascades
            for (int i = 0; i < shadowCascade.cascades.Length; i++)
            {
                var cascade = shadowCascade.cascades[i];
                int resolution = cascade.shadowFrameBuffer.getResolution().X;

                shadowCascade.cascades[i].lightViewMatrix = CreateLightViewMatrix(direction, cameraPos, cascade.projectionSize, resolution);
                shadowCascade.cascades[i].shadowFrameBuffer.ClearDepth();
            }
            entity.Set(shadowCascade);

            // 2. Clear state
            foreach (var list in _models.Values) list.Clear();
            _imposters.Clear();

            // 3. Classify Archetypes (LOD checking models vs imposters based on camera distance)
            var modelQuery = world.QueryArchetypes(new BitMask(typeof(ModelRenderTag), typeof(ModelComponent), typeof(LocalToWorldMatrixComponent)), BitMask.Empty);

            foreach (var archetype in modelQuery)
            {
                var modelArray = archetype.GetComponentArray<ModelComponent>();
                var matrixArray = archetype.GetComponentArray<LocalToWorldMatrixComponent>();
                int count = archetype.EntityCount;

                for (int i = 0; i < count; i++)
                {
                    Matrix4 mat = matrixArray[i].value;
                    Vector3 pos = mat.ExtractTranslation();
                    float distSq = (pos - cameraPos).LengthSquared;

                    glModel model = modelArray[i].model;
                    bool useImposter = false;

                    if (model.Imposter != null)
                    {
                        if (distSq > model.Imposter.DistanceSquared) useImposter = true;
                    }

                    if (useImposter)
                    {
                        Vector3 entityScale = mat.ExtractScale();
                        ImposterData imposter = model.Imposter;

                        float rotY = MathF.Atan2(mat.M13, mat.M11);
                        Vector3 scaledCenter = imposter.LocalCenter * entityScale;

                        float cos = MathF.Cos(rotY);
                        float sin = MathF.Sin(rotY);

                        Vector3 rotatedOffset = new Vector3(
                            scaledCenter.X * cos - scaledCenter.Z * sin,
                            scaledCenter.Y,
                            scaledCenter.X * sin + scaledCenter.Z * cos
                        );

                        Vector3 quadWorldCenter = pos + rotatedOffset;

                        _imposters.Add(new ImposterInstanceData
                        {
                            modelID = (float)model.Imposter.TextureIndex,
                            Position = quadWorldCenter,
                            Scale = entityScale,
                            RotationY = rotY,
                            BaseLength = imposter.BaseLength
                        });
                    }
                    else
                    {
                        if (!_models.TryGetValue(model, out var list))
                        {
                            list = new List<Matrix4>();
                            _models[model] = list;
                        }
                        list.Add(mat);
                    }
                }
            }

            // 4. Pre-build Commands ONCE per frame (Array allocations)
            var prebuiltModelCommands = new List<ModelRenderCommand>(_models.Count);
            foreach (var kvp in _models)
            {
                if (kvp.Value.Count > 0)
                {
                    prebuiltModelCommands.Add(new ModelRenderCommand(kvp.Key, kvp.Value.ToArray()));
                }
            }

            ImposterRenderCommand? imposterCommand = null;
            if (_imposters.Count > 0)
            {
                imposterCommand = new ImposterRenderCommand { instances = _imposters.ToArray() };
            }

            // 5. Submit Models & Imposters to each Cascade
            for (int j = 0; j < shadowCascade.cascades.Length; j++)
            {
                Shadow cascade = shadowCascade.cascades[j];

                // Standard Models
                foreach (var cmd in prebuiltModelCommands)
                {
                    if (cmd.matrices.Length > minCountForInstanced)
                        Engine.RenderEngine._instancedModelRenderer.SubmitShadowCommand(cmd, cascade);
                    else
                        Engine.RenderEngine._modelRenderer.SubmitShadowCommand(cmd, cascade);
                }

                // Imposters
                if (imposterCommand.HasValue)
                {
                    Engine.RenderEngine._imposterRenderer.SubmitShadowCommand(imposterCommand.Value, cascade);
                }
            }

            // 6. Terrain & Grass
            var quadtreeComp = world.GetComponent<TerrainQuadTreeComponent>(world.GetSingleton<TerrainQuadTreeComponent>());

            for (int i = 0; i < shadowCascade.cascades.Length; i++)
            {
                Shadow shadow = shadowCascade.cascades[i];

                _visibleChunks.Clear();
                _terrainCommands.Clear();
                _grassCommands.Clear();

                var viewProj = shadow.lightViewMatrix * shadow.shadowProjectionMatrix;
                TerrainChunkSystem.CollectVisibleChunks(quadtreeComp.QuadTree, new Util.Frustum(viewProj), _visibleChunks);

                foreach (Entity chunkEntity in _visibleChunks)
                {
                    var ltw = world.GetComponent<LocalToWorldMatrixComponent>(chunkEntity).value;
                    var size = world.GetComponent<ScaleComponent>(chunkEntity).value;
                    var chunkComp = world.GetComponent<TerrainChunkComponent>(chunkEntity);

                    _terrainCommands.Add(new TerrainChunkRenderData { chunkPos = ltw.ExtractTranslation(), size = size, arrayID = chunkComp.normalHeightTextureArrayID });

                    float dist = Vector2.Distance(cameraPos.Xz, ltw.ExtractTranslation().Xz + size.Xz * 0.5f);
                    if (dist < 500 && i < 5)
                    {
                        _grassCommands.Add(new GrassChunkRenderData { chunkPos = ltw.ExtractTranslation().Xz, size = size.X, arrayID = chunkComp.normalHeightTextureArrayID });
                    }
                }

                Engine.RenderEngine._grassRenderer.SubmitShadowCommand(new GrassRenderCommand(_grassCommands.ToArray(), 0), shadow);
                Engine.RenderEngine._terrainRenderer.SubmitShadowCommand(new TerrainRenderCommand(_terrainCommands.ToArray(), 0.0f), shadow);
            }
        }

        private static Matrix4 CreateLightViewMatrix(Vector3 direction, Vector3 center, float size, int resolution)
        {
            direction = Vector3.Normalize(direction);
            Vector3 up = MathF.Abs(Vector3.Dot(direction, Vector3.UnitY)) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;

            // Derive light orientation axes
            Vector3 forward = direction;
            Vector3 right = Vector3.Normalize(Vector3.Cross(up, forward));
            Vector3 lightUp = Vector3.Cross(forward, right);

            // Calculate world-space size of a single shadow texel
            float texelSize = size / resolution;

            // Project center onto the light's local X/Y plane
            float x = Vector3.Dot(center, right);
            float y = Vector3.Dot(center, lightUp);
            float z = Vector3.Dot(center, forward);

            // Snap X and Y to the discrete texel grid
            x = MathF.Floor(x / texelSize) * texelSize;
            y = MathF.Floor(y / texelSize) * texelSize;

            // Reconstruct the stable, snapped center in world space
            Vector3 snappedCenter = right * x + lightUp * y + forward * z;

            Vector3 lightPos = snappedCenter - direction * size / 2f;
            return Matrix4.LookAt(lightPos, snappedCenter, up);
        }
    }
}