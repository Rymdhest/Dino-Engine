using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Rendering.Renderers.PostProcessing;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class CelestialBodyRenderSystem : SystemBase
    {
        public CelestialBodyRenderSystem()
            : base(new BitMask(
                typeof(CelestialBodyComponent),
                typeof(DirectionNormalizedComponent),
                typeof(ColorComponent)))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            CelestialBodyRenderCommand command = new CelestialBodyRenderCommand();

            command.colour = entity.Get<ColorComponent>().value.ToVector3();
            command.direction = entity.Get<DirectionNormalizedComponent>().value;

            Engine.RenderEngine._sunRenderer.SubmitCommand(command);

        }
    }
}
