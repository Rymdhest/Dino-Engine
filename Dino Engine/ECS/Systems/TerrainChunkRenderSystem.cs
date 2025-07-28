using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class TerrainChunkRenderSystem : SystemBase
    {
        public TerrainChunkRenderSystem()
            : base(new BitMask(typeof(TerrainChunkComponent), typeof(ScaleComponent), typeof(LocalToWorldMatrixComponent)))
        {
        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            var visibleChunks = new List<Entity>();

            Vector3 cameraPos = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();
            Matrix4 CameraViewMatrix = world.GetComponent<ViewMatrixComponent>(world.Camera).value;
            Matrix4 CameraProjectionMatrix = world.GetComponent<PerspectiveProjectionComponent>(world.Camera).ProjectionMatrix;
            var quadtreeComponent = world.GetComponent<TerrainQuadTreeComponent>(world.GetSingleton<TerrainQuadTreeComponent>());
            var viewProjectionMatrix = CameraViewMatrix * CameraProjectionMatrix;
            TerrainChunkSystem.CollectVisibleChunks(quadtreeComponent.QuadTree, new Util.Frustum(viewProjectionMatrix), visibleChunks);

            var grassChunksLOD0 = new List<GrassChunkRenderData>();
            var grassChunksLOD1 = new List<GrassChunkRenderData>();


            var terrainChunksLOD0 = new List<TerrainChunkRenderData>();
            var terrainChunksLOD1 = new List<TerrainChunkRenderData>();

            foreach (Entity entity in visibleChunks)
            {
                Vector3 chunkPosition = world.GetComponent<LocalToWorldMatrixComponent>(entity).value.ExtractTranslation();
                Vector3 chunkSize = world.GetComponent<ScaleComponent>(entity).value;
                float arrayID = world.GetComponent<TerrainChunkComponent>(entity).normalHeightTextureArrayID;
                float distance = Vector2.Distance(cameraPos.Xz, chunkPosition.Xz + chunkSize.Xz * 0.5f);

                TerrainChunkRenderData chunkCommand = new TerrainChunkRenderData();
                chunkCommand.chunkPos = chunkPosition;
                chunkCommand.size = chunkSize;
                chunkCommand.arrayID = arrayID;

                if (distance < 10)
                {
                    terrainChunksLOD0.Add(chunkCommand);
                }
                else 
                {
                    terrainChunksLOD1.Add(chunkCommand);
                }



                GrassChunkRenderData grassCommand = new GrassChunkRenderData();
                grassCommand.chunkPos = chunkPosition.Xz;
                grassCommand.size = chunkSize.X;
                grassCommand.arrayID = arrayID;
                if (distance < 7)
                {
                    grassChunksLOD0.Add(grassCommand);
                }
                else if (distance < 70)
                {
                    grassChunksLOD1.Add(grassCommand);
                }
            }

            Engine.RenderEngine._grassRenderer.SubmitGeometryCommand(new GrassRenderCommand(grassChunksLOD0.ToArray(), 0));
            Engine.RenderEngine._grassRenderer.SubmitGeometryCommand(new GrassRenderCommand(grassChunksLOD1.ToArray(), 1));

            Engine.RenderEngine._terrainRenderer.SubmitGeometryCommand(new TerrainRenderCommand(terrainChunksLOD0.ToArray(), 0.08f));
            Engine.RenderEngine._terrainRenderer.SubmitGeometryCommand(new TerrainRenderCommand(terrainChunksLOD1.ToArray(), 0.0f));
        }


        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {

        }
    }
}
