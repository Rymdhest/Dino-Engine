using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace Dino_Engine.ECS.Systems
{
    public class DirectionalCascadeShadowSystem : SystemBase
    {
        private int minCountForInstanced = 10;

        public DirectionalCascadeShadowSystem()
            : base(new BitMask(
                typeof(DirectionalLightTag),
                typeof(DirectionNormalizedComponent),
                typeof(DirectionalCascadingShadowComponent)))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var direction = entity.Get<DirectionNormalizedComponent>().value;
            var shadowCascade = entity.Get<DirectionalCascadingShadowComponent>();
            var cameraPos = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();
            for (int i = 0; i<shadowCascade.cascades.Length; i++)
            {
                shadowCascade.cascades[i].lightViewMatrix = CreateLightViewMatrix(direction, cameraPos, shadowCascade.cascades[i].projectionSize);
                shadowCascade.cascades[i].shadowFrameBuffer.ClearDepth(); //// POTENTIALLY REALLY UGLY AND RENDER RELATED
            }
            entity.Set(shadowCascade);



            // MODELS
            var shadowCastingModels = world.QueryEntities(new BitMask(
                typeof(ModelComponent),
                typeof(ModelRenderTag),
                typeof(LocalToWorldMatrixComponent)), BitMask.Empty);
            for (int j = 0; j < shadowCascade.cascades.Length; j++)
            {
                Shadow cascade = shadowCascade.cascades[j];
                Dictionary<glModel, List<Matrix4>> commands = new();
                for (int i = 0; i < shadowCastingModels.Count; i++)
                {
                    var LocalToWorldMatrix = world.GetComponent<LocalToWorldMatrixComponent>(shadowCastingModels[i]).value;
                    var glModel = world.GetComponent<ModelComponent>(shadowCastingModels[i]).model;

                    if (!commands.ContainsKey(glModel)) commands[glModel] = new List<Matrix4>();
                    commands[glModel].Add(LocalToWorldMatrix);
                }
                foreach (var command in commands)
                {
                    var ModelCommand = new ModelRenderCommand();
                    ModelCommand.model = command.Key;
                    ModelCommand.matrices = command.Value.ToArray();

                    if (ModelCommand.matrices.Length > minCountForInstanced)
                    {
                        Engine.RenderEngine._instancedModelRenderer.SubmitShadowCommand(ModelCommand, cascade);
                    }
                    else
                    {
                        Engine.RenderEngine._modelRenderer.SubmitShadowCommand(ModelCommand, cascade);
                    }

                    command.Value.Clear();

                }
                commands.Clear();
            }



            // Terrain
            for (int i = 0; i<shadowCascade.cascades.Length; i++)
            {
                Shadow shadow = shadowCascade.cascades[i];
                var visibleChunks = new List<Entity>();

                var grassChunks = new List<GrassChunkRenderData>();

                var quadtreeComponent = world.GetComponent<TerrainQuadTreeComponent>(world.GetSingleton<TerrainQuadTreeComponent>());
                var viewProjectionMatrix = shadow.lightViewMatrix * shadow.shadowProjectionMatrix;
                TerrainChunkSystem.CollectVisibleChunks(quadtreeComponent.QuadTree, new Util.Frustum(viewProjectionMatrix), visibleChunks);
                var terrainChunksRenderData = new List<TerrainChunkRenderData>();
                foreach (Entity chunkEntity in visibleChunks)
                {
                    Vector3 chunkPosition = world.GetComponent<LocalToWorldMatrixComponent>(chunkEntity).value.ExtractTranslation();
                    Vector3 chunkSize = world.GetComponent<ScaleComponent>(chunkEntity).value;
                    float arrayID = world.GetComponent<TerrainChunkComponent>(chunkEntity).normalHeightTextureArrayID;
                    TerrainChunkRenderData chunkCommand = new TerrainChunkRenderData();
                    chunkCommand.chunkPos = chunkPosition;
                    chunkCommand.size = chunkSize;
                    chunkCommand.arrayID = arrayID;
                    terrainChunksRenderData.Add(chunkCommand);

                    GrassChunkRenderData grassCommand = new GrassChunkRenderData();
                    grassCommand.chunkPos = chunkPosition.Xz;
                    grassCommand.size = chunkSize.X;
                    grassCommand.arrayID = arrayID;

                    float distance = Vector2.Distance(cameraPos.Xz, chunkPosition.Xz + chunkSize.Xz * 0.5f);

                    if (distance < 35 && i <2)
                    {
                        grassChunks.Add(grassCommand);
                    }
                }

                Engine.RenderEngine._grassRenderer.SubmitShadowCommand(new GrassRenderCommand(grassChunks.ToArray(), 0), shadow);
  

                Engine.RenderEngine._terrainRenderer.SubmitShadowCommand(new TerrainRenderCommand(terrainChunksRenderData.ToArray(), 0.0f), shadow);
            }

            // GRASS
        }

        private static Matrix4 CreateLightViewMatrix(Vector3 direction, Vector3 center, float size)
        {
            direction = Vector3.Normalize(direction);

            // Pick an "up" vector that's safe; usually Y up, but if light direction is vertical, use another up.
            Vector3 up = MathF.Abs(Vector3.Dot(direction, Vector3.UnitY)) > 0.99f
                ? Vector3.UnitZ  // fallback up if direction is nearly vertical
                : Vector3.UnitY;

            // The eye position: move "back" along the light direction from the center of the cascade
            Vector3 lightPos = center - direction * size/2f; // 100 units back; adjust based on cascade size

            return Matrix4.LookAt(lightPos, center, up);
        }

    }
}
