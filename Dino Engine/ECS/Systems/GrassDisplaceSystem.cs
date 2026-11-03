using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Simulation;

namespace Dino_Engine.ECS.Systems
{
    public class GrassDisplaceSystem : SystemBase
    {
        public GrassDisplaceSystem()
            : base(new BitMask(typeof(PositionComponent), typeof(GrassDisplaceComponent)))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var position = entity.Get<PositionComponent>();
            var grassDisplace = entity.Get<GrassDisplaceComponent>();

            Engine.RenderEngine._windRenderer.AddVegetationContact(new VegetationContact
            {
                center = position.value.Xz,
                radius = grassDisplace.radius,
                power = grassDisplace.power,
                exponent = grassDisplace.exponent,
            });

        }
    }
}
