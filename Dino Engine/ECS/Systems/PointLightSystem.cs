using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class PointLightSystem : SystemBase
    {
        public PointLightSystem()
            : base(new BitMask(
                typeof(PointLightTag),
                typeof(LocalToWorldMatrixComponent),
                typeof(LightRangeComponent),
                typeof(ColorComponent)))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            if (!Engine.RenderEngine.Lighting.LocalLights) return;

            var range = entity.Get<LightRangeComponent>();

            PointlightRenderCommand command = new PointlightRenderCommand();
            command.colour = Radiance(entity);
            if (command.colour == Vector3.Zero) return;
            command.positionWorld = entity.Get<LocalToWorldMatrixComponent>().value.ExtractTranslation();
            command.radius = range.Radius;
            command.fill = range.Fill;

            if (entity.Has<PointLightShadowComponent>())
            {
                var shadow = entity.Get<PointLightShadowComponent>();
                command.shadow = shadow.Shadow;
                command.shadowStrength = shadow.Strength;
            }

            Engine.RenderEngine._pointLightRenderer.SubmitCommand(command);
            if (range.LightsFog) Engine.RenderEngine._volumetricFogRenderer.SubmitPointLight(command);
            Engine.RenderEngine._waterRenderer.SubmitPointLight(command);
            Engine.RenderEngine._particleRenderer.SubmitPointLight(command);
        }

        /// <summary>What a local light gives at one metre right now, flickering and dying away included.</summary>
        internal static Vector3 Radiance(EntityView entity)
        {
            Vector3 colour = entity.Get<ColorComponent>().value.ToVector3() * Engine.RenderEngine.Lighting.LocalLightScale;
            if (entity.Has<LightFlickerComponent>()) colour *= entity.Get<LightFlickerComponent>().Brightness(Engine.Time);
            if (entity.Has<LightPulseTag>())
            {
                float left = entity.Get<selfDestroyComponent>().getRemainingRatio();
                colour *= left * left;
            }
            return colour;
        }
    }
}
