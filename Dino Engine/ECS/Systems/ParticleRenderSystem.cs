using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.PosGeometry;

namespace Dino_Engine.ECS.Systems
{
    public class ParticleRenderSystem : SystemBase
    {
        public ParticleRenderSystem()
            : base(new BitMask(typeof(ParticleRenderTag), typeof(ColorComponent), typeof(LocalToWorldMatrixComponent)))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {

            ParticleRenderCommand command = new ParticleRenderCommand();
            command.localToWorldMatrix = entity.Get<LocalToWorldMatrixComponent>().value;
            command.color = entity.Get<ColorComponent>().value;
            Engine.RenderEngine._particleRenderer.SubmitCommand(command);
        }
    }
}
