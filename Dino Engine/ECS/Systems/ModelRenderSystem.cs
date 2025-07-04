using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;

namespace Dino_Engine.ECS.Systems
{
    public class ModelRenderSystem : SystemBase
    {
        public ModelRenderSystem()
            : base(new BitMask(typeof(ModelRenderTag), typeof(ModelComponent), typeof(LocalToWorldMatrixComponent)))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            ModelRenderCommand command = new ModelRenderCommand();
            command.localToWorldMatrix = entity.Get<LocalToWorldMatrixComponent>().value;
            command.model = entity.Get<ModelComponent>().model;
            Engine.RenderEngine._modelRenderer.SubmitCommand(command);
        }
    }
}
