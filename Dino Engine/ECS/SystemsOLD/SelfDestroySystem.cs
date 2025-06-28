using Dino_Engine.Core;
using Dino_Engine.ECS.ComponentsOLD;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.SystemsOLD
{
    public class SelfDestroySystem : ComponentSystem
    {
        public SelfDestroySystem()
        {
            addRequiredComponent<SelfDestroyComponent>();
        }

        internal override void UpdateEntity(EntityOLD entity)
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
