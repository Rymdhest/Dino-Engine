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
        public override void Update(ECSWorld world, float deltaTime)
        {
            foreach (var arch in world.Query(WithMask, WithoutMask))
            {
                var posArray = (ComponentArray<PositionComponent>)arch.ComponentArrays[ComponentTypeRegistry.GetId<PositionComponent>()];
                var velArray = (ComponentArray<VelocityComponent>)arch.ComponentArrays[ComponentTypeRegistry.GetId<VelocityComponent>()];

                for (int i = 0; i < arch.Entities.Count; i++)
                {
                    var pos = posArray.Get(i);
                    var vel = velArray.Get(i);

                    pos.position += vel.velocity * deltaTime;

                    posArray.Set(i, pos);
                }
            }
        }
    }
}
