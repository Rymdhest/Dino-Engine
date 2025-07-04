using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class SpotLightSystem : SystemBase
    {
        public SpotLightSystem()
            : base(new BitMask(
                typeof(SpotLightComponent),
                typeof(PositionComponent),
                typeof(LocalToWorldMatrixComponent),
                typeof(AttunuationComponent),
                typeof(ColorComponent),
                typeof(DirectionNormalizedComponent)))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var posToWorldMatrix = entity.Get<LocalToWorldMatrixComponent>().value;

            SpotlightRenderCommand command = new SpotlightRenderCommand();
            command.rotation = posToWorldMatrix.ExtractRotation();
            command.colour = entity.Get<ColorComponent>().value.ToVector3();
            command.attenuation = entity.Get<AttunuationComponent>().Attunuation;
            command.positionWorld = posToWorldMatrix.ExtractTranslation();
            command.softness = entity.Get<SpotLightComponent>().softness;
            command.attenuationRadius = entity.Get<AttunuationComponent>().AttunuationRadius;
            command.direction = entity.Get<DirectionNormalizedComponent>().value;
            command.halfAngleRad = entity.Get<SpotLightComponent>().HalfAngleRad;
            command.cutoffCosine = entity.Get<SpotLightComponent>().CutoffCosine;
            command.ambient = entity.GetOptional(new AmbientLightComponent(0f)).value;


            Engine.RenderEngine._spotLightRenderer.SubmitCommand(command);
        }

    }
}
