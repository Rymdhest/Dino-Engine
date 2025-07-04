using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class PointLightSystem : SystemBase
    {
        public PointLightSystem()
            : base(new BitMask(
                typeof(PointLightTag),
                typeof(PositionComponent),
                typeof(LocalToWorldMatrixComponent),
                typeof(AttunuationComponent),
                typeof(ColorComponent)))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {


            var posToWorldMatrix = entity.Get<LocalToWorldMatrixComponent>().value;

            PointlightRenderCommand command = new PointlightRenderCommand();
            command.colour = entity.Get<ColorComponent>().value.ToVector3();
            command.attenuation = entity.Get<AttunuationComponent>().Attunuation;
            command.positionWorld = posToWorldMatrix.ExtractTranslation();
            command.attenuationRadius = entity.Get<AttunuationComponent>().AttunuationRadius;
            command.ambient = entity.GetOptional(new AmbientLightComponent(0f)).value;


            Engine.RenderEngine._pointLightRenderer.SubmitCommand(command);
        }

    }
}
