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
    public class SelfDestroySystem : SystemBase
    {
        public SelfDestroySystem()
            : base(new BitMask(typeof(selfDestroyComponent)))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var comp = entity.Get<selfDestroyComponent>();
            comp.SecondsRemaining -= deltaTime;
            entity.Set(comp);

            if (comp.SecondsRemaining < 0)
            {
                world.deferredCommands.removeEntityCommands.Add(new RemoveEntityCommand(entity.Entity));
            }
        }
    }
}
