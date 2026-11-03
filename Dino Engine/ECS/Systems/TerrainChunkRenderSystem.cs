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
            var frustum = new Util.Frustum(viewProjectionMatrix);
            TerrainChunkSystem.CollectVisibleChunks(quadtreeComponent, frustum, visibleChunks);

            // All chunks are the same size now, so there is no terrain LOD. A single list for terrain.
            var terrainChunks = new List<TerrainChunkRenderData>();

            foreach (Entity entity in visibleChunks)
            {
                TerrainChunkRenderData chunkCommand = new TerrainChunkRenderData();
                chunkCommand.chunkPos = world.GetComponent<LocalToWorldMatrixComponent>(entity).value.ExtractTranslation();
                chunkCommand.size = world.GetComponent<ScaleComponent>(entity).value;
                chunkCommand.arrayID = world.GetComponent<TerrainChunkComponent>(entity).normalHeightTextureArrayID;

                terrainChunks.Add(chunkCommand);
            }

            // Grass gets every chunk within its draw distance, also those just outside the view whose blades lean into
            // it; its compute pass picks the density and LOD of each blade and culls the rest.
            GrassSingleton grass = world.TryGetSingleton<GrassSingleton>(out Entity grassEntity)
                ? world.GetComponent<GrassSingleton>(grassEntity)
                : new GrassSingleton();
            var grassChunks = new List<GrassChunkRenderData>();
            if (grass.Enabled)
            {
                var grassVisibleChunks = new List<Entity>();
                TerrainChunkSystem.CollectVisibleChunks(quadtreeComponent, frustum, grassVisibleChunks, margin: grass.Height * 1.5f);

                foreach (Entity entity in grassVisibleChunks)
                {
                    Vector3 chunkPosition = world.GetComponent<LocalToWorldMatrixComponent>(entity).value.ExtractTranslation();
                    Vector3 chunkSize = world.GetComponent<ScaleComponent>(entity).value;
                    Vector2 nearestPoint = Vector2.Clamp(cameraPos.Xz, chunkPosition.Xz, chunkPosition.Xz + chunkSize.Xz);
                    if (Vector2.Distance(cameraPos.Xz, nearestPoint) >= grass.DrawDistance) continue;

                    GrassChunkRenderData grassCommand = new GrassChunkRenderData();
                    grassCommand.chunkPos = chunkPosition.Xz;
                    grassCommand.size = chunkSize.X;
                    grassCommand.arrayID = world.GetComponent<TerrainChunkComponent>(entity).normalHeightTextureArrayID;
                    grassChunks.Add(grassCommand);
                }
            }

            // Submitted even when empty: the command carries the grass settings, which the terrain's ground tint reads too.
            Engine.RenderEngine._grassRenderer.SubmitGeometryCommand(new GrassRenderCommand(grassChunks.ToArray(), grass));

            // Single terrain command: every chunk is the same size/quality, so no LOD split needed.
            Engine.RenderEngine._terrainRenderer.SubmitGeometryCommand(new TerrainRenderCommand(terrainChunks.ToArray(), 0.03f));
        }


        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {

        }
    }
}
