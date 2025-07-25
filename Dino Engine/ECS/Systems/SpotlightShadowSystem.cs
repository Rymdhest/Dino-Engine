using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class SpotlightShadowSystem : SystemBase
    {
        private int minCountForInstanced = 10;
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
            var shadowComponent = entity.Get<SpotlightShadowComponent>();
            var lightPos = entity.Get<LocalToWorldMatrixComponent>().value.ExtractTranslation();
            var cameraPos = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();

            shadowComponent.shadow.lightViewMatrix = CreateLightViewMatrix(direction, lightPos);

            shadowComponent.shadow.shadowFrameBuffer.ClearDepth();

            entity.Set(shadowComponent);


            // MODELS
            var shadowCastingModels = world.QueryEntities(new BitMask(
                typeof(ModelComponent),
                typeof(ModelRenderTag),
                typeof(LocalToWorldMatrixComponent)), BitMask.Empty);


            Shadow shadow = shadowComponent.shadow;
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
                    Engine.RenderEngine._instancedModelRenderer.SubmitShadowCommand(ModelCommand, shadow);
                }
                else
                {
                    Engine.RenderEngine._modelRenderer.SubmitShadowCommand(ModelCommand, shadow);
                }

                command.Value.Clear();

            }
            commands.Clear();




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

                if (distance < 25)
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
                ? Vector3.UnitZ  // fallback up if direction is nearly vertical
                : Vector3.UnitY;

            return Matrix4.LookAt(lightPos, lightPos+ direction, Vector3.UnitY);

        }

    }
}
