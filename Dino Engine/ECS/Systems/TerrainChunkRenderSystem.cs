using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;

namespace Dino_Engine.ECS.Systems
{
    public class TerrainChunkRenderSystem : SystemBase
    {
        public TerrainChunkRenderSystem()
            : base(new BitMask(typeof(TerrainChunkComponent), typeof(ScaleComponent), typeof(LocalToWorldMatrixComponent)))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            TerrainChunkRenderCommand command = new TerrainChunkRenderCommand();
            command.chunkPos = entity.Get<LocalToWorldMatrixComponent>().value.ExtractTranslation();
            command.size = entity.Get<ScaleComponent>().value;
            command.arrayID = entity.Get<TerrainChunkComponent>().normalHeightTextureArrayID;
            Engine.RenderEngine._terrainRenderer.SubmitCommand(command);
        }
    }
}
