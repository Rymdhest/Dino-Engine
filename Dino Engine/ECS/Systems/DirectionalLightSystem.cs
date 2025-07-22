using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class DirectionalLightSystem : SystemBase
    {
        public DirectionalLightSystem()
            : base(new BitMask(
                typeof(DirectionalLightTag),
                typeof(DirectionNormalizedComponent),
                typeof(ColorComponent)))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            DirectionallightRenderCommand command = new DirectionallightRenderCommand();

            command.colour = entity.Get<ColorComponent>().value.ToVector3();
            command.direction = entity.Get<DirectionNormalizedComponent>().value;
            command.ambient = entity.GetOptional(new AmbientLightComponent(0f)).value;
            if (entity.Has<DirectionalCascadingShadowComponent>())
            {
                command.cascades = entity.Get<DirectionalCascadingShadowComponent>().cascades;
            } else
            {
                command.cascades = new Shadow[0];
            }

            Engine.RenderEngine._directionalLightRenderer.SubmitCommand(command);
        }
    }
}
