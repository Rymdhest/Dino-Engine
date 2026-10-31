using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Geometry;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class TerrainChunkRenderSystem : SystemBase
    {
        public TerrainChunkRenderSystem()
            : base(new BitMask(typeof(TerrainChunkComponent), typeof(ScaleComponent), typeof(LocalToWorldMatrixComponent)))
        {
            Priority = 1;
        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            var visibleChunks = new List<Entity>();

            Vector3 cameraPos = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();
            Matrix4 CameraViewMatrix = world.GetComponent<ViewMatrixComponent>(world.Camera).value;
            Matrix4 CameraProjectionMatrix = world.GetComponent<PerspectiveProjectionComponent>(world.Camera).ProjectionMatrix;
            var quadtreeComponent = world.GetComponent<TerrainQuadTreeSingleton>(world.GetSingleton<TerrainQuadTreeSingleton>());
            var viewProjectionMatrix = CameraViewMatrix * CameraProjectionMatrix;
            TerrainChunkSystem.CollectVisibleChunks(quadtreeComponent, new Util.Frustum(viewProjectionMatrix), visibleChunks);

            // All chunks are the same size now, so there is no terrain LOD. We keep a single list for
            // terrain and a distance-based split for grass (dense near the camera, sparse further out)
            // which still gives a nice density falloff without any popping.
            var grassChunksLOD0 = new List<GrassChunkRenderData>();
            var grassChunksLOD1 = new List<GrassChunkRenderData>();

            var terrainChunks = new List<TerrainChunkRenderData>();

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

                terrainChunks.Add(chunkCommand);


                GrassChunkRenderData grassCommand = new GrassChunkRenderData();
                grassCommand.chunkPos = chunkPosition.Xz;
                grassCommand.size = chunkSize.X;
                grassCommand.arrayID = arrayID;
                if (distance < 40)
                {
                    grassChunksLOD0.Add(grassCommand);
                }
                else if (distance < 250)
                {
                    grassChunksLOD1.Add(grassCommand);
                }
            }

            Engine.RenderEngine._grassRenderer.SubmitGeometryCommand(new GrassRenderCommand(grassChunksLOD0.ToArray(), 0));
            Engine.RenderEngine._grassRenderer.SubmitGeometryCommand(new GrassRenderCommand(grassChunksLOD1.ToArray(), 1));

            // Single terrain command: every chunk is the same size/quality, so no LOD split needed.
            Engine.RenderEngine._terrainRenderer.SubmitGeometryCommand(new TerrainRenderCommand(terrainChunks.ToArray(), 0.03f));
        }


        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {

        }
    }
}
