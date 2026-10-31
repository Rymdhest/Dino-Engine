using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Geometry;

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

            var buffer = world.GetComponent<CollisionEventBufferSingleton>(world.GetSingleton<CollisionEventBufferSingleton>());


            BlastData blast = new BlastData();
            blast.exponent = grassDisplace.exponent;
            blast.radius = grassDisplace.radius;
            blast.power = grassDisplace.power;
            blast.center = position.value.Xz;

            Engine.RenderEngine._grassRenderer.blasts.Add(blast);

        }
    }
}
