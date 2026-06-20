using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;

namespace Dino_Engine.ECS.Systems
{
    public class GravitySystem : SystemBase
    {
        public static float GRAVITY_CONSTANT = -10f;
        public GravitySystem()
            : base(new BitMask(typeof(VelocityComponent), typeof(MassComponent)))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var velocity = entity.Get<VelocityComponent>();
            var mass = entity.Get<MassComponent>().value;

            velocity.value.Y += GRAVITY_CONSTANT * deltaTime* mass;
            entity.Set(velocity);
        }
    }
}
