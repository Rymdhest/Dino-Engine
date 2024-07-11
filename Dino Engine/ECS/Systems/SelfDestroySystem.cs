using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Systems
{
    internal class SelfDestroySystem : ComponentSystem
    {
        public SelfDestroySystem()
        {
            addRequiredComponent<SelfDestroyComponent>();
        }

        internal override void UpdateEntity(Entity entity)
        {
            SelfDestroyComponent component = entity.getComponent<SelfDestroyComponent>();
            component.secondsRemaining -= Engine.Delta;
            if (component.secondsRemaining < 0)
            {
                entity.CleanUp();
            }
        }
    }
}
