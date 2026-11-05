using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Util;

namespace Dino_Engine.ECS.Systems
{
    public class BeamRenderSystem : SystemBase
    {
        private const float DaylightLift = 1.8f;    // What a beam needs on top to hold its colour against a sunlit field, which is a good deal brighter than it is at night.

        public BeamRenderSystem()
            : base(new BitMask(typeof(BeamComponent), typeof(PositionComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var beam = entity.Get<BeamComponent>();
            if (beam.Height <= 0f || !Engine.RenderEngine.Lighting.Emission) return;

            Colour colour = beam.Colour;
            colour.Intensity *= 1f + DaylightLift * Engine.RenderEngine.context.daylight;

            Engine.RenderEngine._beamRenderer.SubmitCommand(new BeamRenderCommand
            {
                Base = entity.Get<PositionComponent>().value,
                Height = beam.Height,
                Width = beam.Width,
                Colour = colour,
                Seed = entity.Entity.Id * 1.618f % 6.28f,
            });
        }
    }
}
