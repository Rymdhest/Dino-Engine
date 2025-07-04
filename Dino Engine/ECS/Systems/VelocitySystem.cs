using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Systems
{
    public class VelocitySystem : SystemBase
    {
        public VelocitySystem()
            : base(new BitMask(typeof(PositionComponent), typeof(VelocityComponent)))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var pos = entity.Get<PositionComponent>();
            var vel = entity.Get<VelocityComponent>();

            pos.value += vel.value * deltaTime;
            entity.Set(pos);
        }
    }
}
