using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class SpotLightSystem : SystemBase
    {
        public SpotLightSystem()
            : base(new BitMask(
                typeof(SpotLightComponent),
                typeof(LocalToWorldMatrixComponent),
                typeof(LightRangeComponent),
                typeof(ColorComponent),
                typeof(DirectionNormalizedComponent)))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            if (!Engine.RenderEngine.Lighting.LocalLights) return;

            var posToWorldMatrix = entity.Get<LocalToWorldMatrixComponent>().value;
            var range = entity.Get<LightRangeComponent>();

            SpotlightRenderCommand command = new SpotlightRenderCommand();
            command.rotation = posToWorldMatrix.ExtractRotation();
            command.colour = PointLightSystem.Radiance(entity);
            command.positionWorld = posToWorldMatrix.ExtractTranslation();
            command.softness = entity.Get<SpotLightComponent>().softness;
            command.radius = range.Radius;
            command.fill = range.Fill;
            command.direction = entity.Get<DirectionNormalizedComponent>().value;
            command.halfAngleRad = entity.Get<SpotLightComponent>().HalfAngleRad;
            command.cutoffCosine = entity.Get<SpotLightComponent>().CutoffCosine;

            if (entity.Has<SpotlightShadowComponent>())
            {
                command.shadow = entity.Get<SpotlightShadowComponent>().shadow;
            }
            else
            {
                command.shadow = null;
            }

            Engine.RenderEngine._spotLightRenderer.SubmitCommand(command);
            if (range.LightsFog) Engine.RenderEngine._volumetricFogRenderer.SubmitSpotLight(command);
            Engine.RenderEngine._waterRenderer.SubmitSpotLight(command);
            Engine.RenderEngine._globalIllumination.SubmitSpotLight(command);
        }

    }
}
