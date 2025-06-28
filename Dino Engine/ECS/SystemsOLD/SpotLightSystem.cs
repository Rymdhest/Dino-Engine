using Dino_Engine.ECS.ComponentsOLD;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.SystemsOLD
{
    public class SpotLightSystem : ComponentSystem
    {
        public SpotLightSystem()
        {
            addRequiredComponent<ColourComponent>();
            addRequiredComponent<AttunuationComponent>();
            addRequiredComponent<TransformationComponent>();
        }

        internal override void UpdateEntity(EntityOLD entity)
        {

        }
    }
}
