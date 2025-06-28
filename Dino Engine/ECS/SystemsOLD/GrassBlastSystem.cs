using Dino_Engine.ECS.ComponentsOLD;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.SystemsOLD
{
    public class GrassBlastSystem : ComponentSystem
    {
        internal override void UpdateEntity(EntityOLD entity)
        {
            addRequiredComponent<TransformationComponent>();
            addRequiredComponent<GrassBlastComponent>();
        }
    }
}
