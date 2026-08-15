using System;
using System.Collections.Generic;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class SpotlightShadowSystem : SystemBase
    {
        private int minCountForInstanced = 0;

        // Reuse cached lists to prevent garbage collection allocations during rendering
        private readonly List<Entity> _lightCandidates = new();
        private readonly List<Entity> _visibleChunks = new();

        public SpotlightShadowSystem()
            : base(new BitMask(
                typeof(SpotLightComponent),
                typeof(LocalToWorldMatrixComponent),
                typeof(AttunuationComponent),
                typeof(DirectionNormalizedComponent),
                typeof(SpotlightShadowComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var direction = entity.Get<DirectionNormalizedComponent>().value;
            var shadowComp = entity.Get<SpotlightShadowComponent>();
            var lightPos = entity.Get<LocalToWorldMatrixComponent>().value.ExtractTranslation();
            var cameraPos = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();
            float lightRadius = entity.Get<AttunuationComponent>().AttunuationRadius;

            // 1. Update component shadow view matrix directly & clear depth buffer
            shadowComp.shadow.lightViewMatrix = CreateLightViewMatrix(direction, lightPos);
            shadowComp.shadow.shadowFrameBuffer.ClearDepth();
            entity.Set(shadowComp);

            Shadow shadow = shadowComp.shadow;

            // -------------------------------------------------------------------
            // STEP 1: BROADPHASE - Query Spatial Grid using Light Radius
            // -------------------------------------------------------------------
            Entity gridEntity = world.GetSingleton<RenderSpatialGridSingleton>();
            SpatialGrid renderGrid = world.GetComponent<RenderSpatialGridSingleton>(gridEntity).Grid;

            _lightCandidates.Clear();
            renderGrid.QuerySphere(lightPos, lightRadius, _lightCandidates);
            // Construct Frustum for this spotlight
            var viewProjectionMatrix = shadow.lightViewMatrix * shadow.shadowProjectionMatrix;
            Frustum spotlightFrustum = new Frustum(viewProjectionMatrix);

            // -------------------------------------------------------------------
            // STEP 2: NARROWPHASE - Cull candidates against Spotlight Frustum
            // -------------------------------------------------------------------
            Dictionary<glModel, List<Matrix4>> commands = new();

            for (int c = 0; c < _lightCandidates.Count; c++)
            {
                Entity candidate = _lightCandidates[c];

                if (!world.HasComponent<ModelComponent>(candidate) ||
                    !world.HasComponent<ModelRenderTag>(candidate) ||
                    !world.HasComponent<LocalToWorldMatrixComponent>(candidate))
                {
                    continue;
                }

                var ltw = world.GetComponent<LocalToWorldMatrixComponent>(candidate).value;
                var modelComp = world.GetComponent<ModelComponent>(candidate);

                if (modelComp.model == null) continue;

                AABB localBounds = new AABB(modelComp.model.box.Min, modelComp.model.box.Max);
                AABB worldBounds = AABB.Transform(localBounds, ltw);

                if (spotlightFrustum.IntersectsAABB(worldBounds) != IntersectionResult.Outside)
                {
                    if (!commands.TryGetValue(modelComp.model, out var matrixList))
                    {
                        matrixList = new List<Matrix4>();
                        commands[modelComp.model] = matrixList;
                    }
                    matrixList.Add(ltw);
                }
            }

            // Submit model shadow commands
            foreach (var command in commands)
            {
                var renderCmd = new ModelRenderCommand { model = command.Key, matrices = command.Value };
                if (renderCmd.matrices.Count > minCountForInstanced)
                    Engine.RenderEngine._instancedModelRenderer.SubmitShadowCommand(renderCmd, shadow);
                else
                    Engine.RenderEngine._modelRenderer.SubmitShadowCommand(renderCmd, shadow);
            }

            // -------------------------------------------------------------------
            // STEP 3: Terrain & Grass Shadow Rendering
            // -------------------------------------------------------------------
            _visibleChunks.Clear();
            var grassChunks = new List<GrassChunkRenderData>();

            var quadtreeComponent = world.GetComponent<TerrainQuadTreeSingleton>(world.GetSingleton<TerrainQuadTreeSingleton>());
            TerrainChunkSystem.CollectVisibleChunks(quadtreeComponent.QuadTree, spotlightFrustum, _visibleChunks);

            var terrainChunksRenderData = new List<TerrainChunkRenderData>();
            foreach (Entity chunkEntity in _visibleChunks)
            {
                Vector3 chunkPosition = world.GetComponent<LocalToWorldMatrixComponent>(chunkEntity).value.ExtractTranslation();
                Vector3 chunkSize = world.GetComponent<ScaleComponent>(chunkEntity).value;
                float arrayID = world.GetComponent<TerrainChunkComponent>(chunkEntity).normalHeightTextureArrayID;

                TerrainChunkRenderData chunkCommand = new TerrainChunkRenderData
                {
                    chunkPos = chunkPosition,
                    size = chunkSize,
                    arrayID = arrayID
                };
                terrainChunksRenderData.Add(chunkCommand);

                GrassChunkRenderData grassCommand = new GrassChunkRenderData
                {
                    chunkPos = chunkPosition.Xz,
                    size = chunkSize.X,
                    arrayID = arrayID
                };

                float distance = Vector2.Distance(cameraPos.Xz, chunkPosition.Xz + chunkSize.Xz * 0.5f);

                if (distance < 30)
                {
                    grassChunks.Add(grassCommand);
                }
            }

            Engine.RenderEngine._grassRenderer.SubmitShadowCommand(new GrassRenderCommand(grassChunks.ToArray(), 0), shadow);
            Engine.RenderEngine._terrainRenderer.SubmitShadowCommand(new TerrainRenderCommand(terrainChunksRenderData.ToArray(), 0.0f), shadow);
        }

        private static Matrix4 CreateLightViewMatrix(Vector3 direction, Vector3 lightPos)
        {
            Vector3 up = MathF.Abs(Vector3.Dot(direction, Vector3.UnitY)) > 0.99f
                ? Vector3.UnitZ
                : Vector3.UnitY;

            return Matrix4.LookAt(lightPos, lightPos + direction, up);
        }
    }
}