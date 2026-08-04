using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using OpenTK.Mathematics;
using System.Collections.Generic;

namespace Dino_Engine.ECS.Systems
{
    public class DirectionalCascadeShadowSystem : SystemBase
    {
        private int minCountForInstanced = 10;

        // Persistent structures to avoid per-frame allocations
        private readonly Dictionary<glModel, List<Matrix4>> _cachedCommands = new();
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

            // 1. Prepare View Matrices
            for (int i = 0; i < shadowCascade.cascades.Length; i++)
            {
                shadowCascade.cascades[i].lightViewMatrix = CreateLightViewMatrix(direction, cameraPos, shadowCascade.cascades[i].projectionSize);
                shadowCascade.cascades[i].shadowFrameBuffer.ClearDepth();
            }
            entity.Set(shadowCascade);

            // 2. Pre-build Model Commands (Fixing the Dictionary Trap)
            // ONLY clear the underlying lists, keep the dictionary keys and list allocations intact!
            foreach (var list in _cachedCommands.Values)
            {
                list.Clear();
            }

            var shadowCastingModels = world.QueryEntities(new BitMask(typeof(ModelComponent), typeof(ModelRenderTag), typeof(LocalToWorldMatrixComponent)), BitMask.Empty);

            for (int i = 0; i < shadowCastingModels.Count; i++)
            {
                var ltw = world.GetComponent<LocalToWorldMatrixComponent>(shadowCastingModels[i]).value;
                var model = world.GetComponent<ModelComponent>(shadowCastingModels[i]).model;

                if (!_cachedCommands.TryGetValue(model, out var list))
                {
                    list = new List<Matrix4>();
                    _cachedCommands[model] = list;
                }
                list.Add(ltw);
            }

            // NEW: Convert Lists to Arrays ONCE per frame, not per cascade
            // We create a temporary list of pre-built commands to feed to the cascades
            var prebuiltCommands = new List<ModelRenderCommand>(_cachedCommands.Count);
            foreach (var kvp in _cachedCommands)
            {
                if (kvp.Value.Count > 0)
                {
                    prebuiltCommands.Add(new ModelRenderCommand
                    {
                        model = kvp.Key,
                        matrices = kvp.Value.ToArray() // Allocated exactly ONCE per unique model
                    });
                }
            }

            // 3. Submit Models for each cascade
            for (int j = 0; j < shadowCascade.cascades.Length; j++)
            {
                Shadow cascade = shadowCascade.cascades[j];
                foreach (var cmd in prebuiltCommands)
                {
                    
                    if (cmd.matrices.Length > minCountForInstanced)
                        Engine.RenderEngine._instancedModelRenderer.SubmitShadowCommand(cmd, cascade);
                    else
                        Engine.RenderEngine._modelRenderer.SubmitShadowCommand(cmd, cascade);
                    
                }
            }

            // 4. Terrain & Grass
            var quadtreeComp = world.GetComponent<TerrainQuadTreeComponent>(world.GetSingleton<TerrainQuadTreeComponent>());

            for (int i = 0; i < shadowCascade.cascades.Length; i++)
            {
                Shadow shadow = shadowCascade.cascades[i];

                // Reusing the lists
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
                    if (dist < 500 && i < 5) // TEST_CASCADE_GRASS_LIMIT
                    {
                        _grassCommands.Add(new GrassChunkRenderData { chunkPos = ltw.ExtractTranslation().Xz, size = size.X, arrayID = chunkComp.normalHeightTextureArrayID });
                    }
                }

                // Fix: Call ToArray() at the very end of the collection process. 
                // Note: If GrassRenderCommand/TerrainRenderCommand can be modified in your engine 
                // to accept IReadOnlyList<T> or Span<T> instead of arrays, you would eliminate this final allocation entirely.
                Engine.RenderEngine._grassRenderer.SubmitShadowCommand(new GrassRenderCommand(_grassCommands.ToArray(), 0), shadow);
                Engine.RenderEngine._terrainRenderer.SubmitShadowCommand(new TerrainRenderCommand(_terrainCommands.ToArray(), 0.0f), shadow);
            }
        }

        private static Matrix4 CreateLightViewMatrix(Vector3 direction, Vector3 center, float size)
        {
            direction = Vector3.Normalize(direction);
            Vector3 up = MathF.Abs(Vector3.Dot(direction, Vector3.UnitY)) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;
            Vector3 lightPos = center - direction * size / 2f;
            return Matrix4.LookAt(lightPos, center, up);
        }
    }
}