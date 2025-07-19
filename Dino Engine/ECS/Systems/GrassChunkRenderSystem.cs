using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Procedural.Terrain;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Util.Data_Structures;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class GrassChunkRenderSystem : SystemBase
    {
        public GrassChunkRenderSystem()
            : base(new BitMask(typeof(TerrainChunkComponent), typeof(ScaleComponent), typeof(LocalToWorldMatrixComponent)))
        {
        }
        /*
        public override void Update(ECSWorld world, float deltaTime)
        {
            QuadTreeNode quadtree = world.GetComponent<TerrainQuadTreeComponent>(world.GetSingleton<TerrainQuadTreeComponent>()).QuadTree;
            TerrainGenerator generator = world.GetComponent<TerrainGeneratorComponent>(world.GetSingleton<TerrainGeneratorComponent>()).Generator;
            Vector3 cameraPos = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();
        }
        */
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            GrassChunkRenderCommand command = new GrassChunkRenderCommand();
            command.chunkPos = entity.Get<LocalToWorldMatrixComponent>().value.ExtractTranslation().Xz;
            command.size = entity.Get<ScaleComponent>().value.X;

            Vector3 cameraPos = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();
            if (Vector2.Distance(cameraPos.Xz, command.chunkPos+new Vector2(command.size*0.5f)) >= 49) return;

            command.arrayID = entity.Get<TerrainChunkComponent>().normalHeightTextureArrayID;
            Engine.RenderEngine._grassRenderer.SubmitCommand(command);
        }
    }
}
