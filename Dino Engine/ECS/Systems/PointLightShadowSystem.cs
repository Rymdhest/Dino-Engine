using System;
using System.Collections.Generic;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class PointLightShadowSystem : SystemBase
    {
        private int minCountForInstanced = 0;

        // Reuse cached lists to prevent garbage collection allocations during rendering
        private readonly List<Entity> _lightCandidates = new();
        private readonly List<Entity> _visibleChunks = new();

        public PointLightShadowSystem()
            : base(new BitMask(
                typeof(PointLightTag),
                typeof(LocalToWorldMatrixComponent),
                typeof(AttunuationComponent),
                typeof(PointLightShadowComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var shadowComp = entity.Get<PointLightShadowComponent>();
            var lightPos = entity.Get<LocalToWorldMatrixComponent>().value.ExtractTranslation();
            var cameraPos = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();
            Shadow shadow = shadowComp.shadow;
            float lightRadius = entity.Get<AttunuationComponent>().AttunuationRadius;

            // -------------------------------------------------------------------
            // STEP 1: BROADPHASE - Query Spatial Grid for Standard Models
            // -------------------------------------------------------------------
            Entity gridEntity = world.GetSingleton<RenderSpatialGridSingleton>();
            SpatialGrid renderGrid = world.GetComponent<RenderSpatialGridSingleton>(gridEntity).Grid;

            renderGrid.QuerySphere(lightPos, lightRadius, _lightCandidates);

            Vector3[] directions = {
                Vector3.UnitX, -Vector3.UnitX, // +X, -X
                Vector3.UnitY, -Vector3.UnitY, // +Y, -Y
                Vector3.UnitZ, -Vector3.UnitZ  // +Z, -Z
            };

            Vector3[] ups = {
                -Vector3.UnitY, -Vector3.UnitY, // +X, -X use -Y as Up
                Vector3.UnitZ, -Vector3.UnitZ,  // +Y uses +Z, -Y uses -Z as Up
                -Vector3.UnitY, -Vector3.UnitY  // +Z, -Z use -Y as Up
            };

            // -------------------------------------------------------------------
            // STEP 2: Render 6 passes into the Cubemap
            // -------------------------------------------------------------------
            for (int i = 0; i < 6; i++)
            {
                Shadow passShadow = shadow;
                GL.DepthMask(true);
                shadow.shadowFrameBuffer.bindFace(TextureTarget.TextureCubeMapPositiveX + i);
                GL.Clear(ClearBufferMask.DepthBufferBit);

                passShadow.cubemapFaceIndex = i;
                passShadow.lightViewMatrix = Matrix4.LookAt(lightPos, lightPos + directions[i], ups[i]);

                // Construct Frustum for this specific Cubemap face
                var viewProjectionMatrix = passShadow.lightViewMatrix * passShadow.shadowProjectionMatrix;
                Frustum faceFrustum = new Frustum(viewProjectionMatrix);

                // ---------------------------------------------------------------
                // STEP 3A: NARROWPHASE - Cull Standard Models
                // ---------------------------------------------------------------
                Dictionary<glModel, List<Matrix4>> standardCommands = new();

                for (int c = 0; c < _lightCandidates.Count; c++)
                {
                    Entity candidate = _lightCandidates[c];

                    if (!world.HasComponent<ModelComponent>(candidate) ||
                        !world.HasComponent<ModelRenderTag>(candidate) ||
                        !world.HasComponent<LocalToWorldMatrixComponent>(candidate))
                    {
                        continue;
                    }

                    var modelLtw = world.GetComponent<LocalToWorldMatrixComponent>(candidate).value;
                    var modelComp = world.GetComponent<ModelComponent>(candidate);

                    if (modelComp.model == null) continue;

                    AABB localBoundsStandard = new AABB(modelComp.model.box.Min, modelComp.model.box.Max);
                    AABB worldBoundsStandard = AABB.Transform(localBoundsStandard, modelLtw);

                    if (faceFrustum.IntersectsAABB(worldBoundsStandard) != IntersectionResult.Outside)
                    {
                        if (!standardCommands.TryGetValue(modelComp.model, out var matrixList))
                        {
                            matrixList = new List<Matrix4>();
                            standardCommands[modelComp.model] = matrixList;
                        }
                        matrixList.Add(modelLtw);
                    }
                }

                // ---------------------------------------------------------------
                // STEP 3B: NARROWPHASE - Cull and Collect Characters Directly from World
                // ---------------------------------------------------------------
                Dictionary<glModel, List<CharacterInstanceData>> characterCommands = new();
                var characterQuery = world.QueryArchetypes(new BitMask(
                    typeof(CharacterModelComponent),
                    typeof(SkeletonComponent),
                    typeof(LocalToWorldMatrixComponent)),
                    BitMask.Empty);

                foreach (var archetype in characterQuery)
                {
                    var modelArray = archetype.GetComponentArray<CharacterModelComponent>();
                    var matrixArray = archetype.GetComponentArray<LocalToWorldMatrixComponent>();
                    var skeletonArray = archetype.GetComponentArray<SkeletonComponent>();

                    int count = archetype.EntityCount;
                    for (int c = 0; c < count; c++)
                    {
                        Matrix4 ltw = matrixArray[c].value;
                        Vector3 charPos = ltw.ExtractTranslation();

                        // Quick coarse distance check against light radius
                        if ((charPos - lightPos).LengthSquared > (lightRadius * lightRadius))
                            continue;

                        glModel model = modelArray[c].model;
                        if (model == null) continue;

                        // Frustum culling against face frustum
                        AABB localBounds = new AABB(model.box.Min, model.box.Max);
                        AABB worldBounds = AABB.Transform(localBounds, ltw);

                        if (faceFrustum.IntersectsAABB(worldBounds) != IntersectionResult.Outside)
                        {
                            var skeleton = skeletonArray[c];
                            var skinningMatrices = skeleton.SkinningMatrices;
                            int boneCount = skinningMatrices != null ? skinningMatrices.Length : (int)BoneType.Count;
                            Matrix4[] skinningMatricesCopy = new Matrix4[boneCount];

                            if (skinningMatrices != null && skinningMatrices.Length > 0)
                            {
                                Array.Copy(skinningMatrices, skinningMatricesCopy, boneCount);
                            }
                            else
                            {
                                for (int b = 0; b < boneCount; b++)
                                {
                                    skinningMatricesCopy[b] = Matrix4.Identity;
                                }
                            }

                            if (!characterCommands.TryGetValue(model, out var charList))
                            {
                                charList = new List<CharacterInstanceData>();
                                characterCommands[model] = charList;
                            }

                            charList.Add(new CharacterInstanceData
                            {
                                ModelMatrix = ltw,
                                SkinningMatrices = skinningMatricesCopy
                            });
                        }
                    }
                }

                // Submit Standard Models
                foreach (var command in standardCommands)
                {
                    var renderCmd = new ModelRenderCommand { model = command.Key, matrices = command.Value };
                    if (renderCmd.matrices.Count > minCountForInstanced)
                        Engine.RenderEngine._instancedModelRenderer.SubmitShadowCommand(renderCmd, passShadow);
                    else
                        Engine.RenderEngine._modelRenderer.SubmitShadowCommand(renderCmd, passShadow);
                }

                // Submit Characters
                foreach (var command in characterCommands)
                {
                    var renderCmd = new CharacterRenderCommand(command.Key, command.Value);
                    Engine.RenderEngine._characterRenderer.SubmitShadowCommand(renderCmd, passShadow);
                }

                // ---------------------------------------------------------------
                // STEP 4: Terrain & Grass Shadow Rendering
                // ---------------------------------------------------------------
                _visibleChunks.Clear();
                var grassChunks = new List<GrassChunkRenderData>();

                var quadtreeComponent = world.GetComponent<TerrainQuadTreeSingleton>(world.GetSingleton<TerrainQuadTreeSingleton>());
                TerrainChunkSystem.CollectVisibleChunks(quadtreeComponent.QuadTree, faceFrustum, _visibleChunks);

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

                Engine.RenderEngine._grassRenderer.SubmitShadowCommand(new GrassRenderCommand(grassChunks.ToArray(), 0), passShadow);
                Engine.RenderEngine._terrainRenderer.SubmitShadowCommand(new TerrainRenderCommand(terrainChunksRenderData.ToArray(), 0.0f), passShadow);
            }
        }
    }
}