using Dino_Engine.ECS.ComponentsOLD;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.SystemsOLD
{
    public class TerrainSystem : ComponentSystem
    {
        public TerrainSystem()
        {
            addRequiredComponent<TransformationComponent>();
            addRequiredComponent<ModelComponent>();
            addRequiredComponent<TerrainMapsComponent>();
        }

        internal override void UpdateEntity(EntityOLD entity)
        {

        }
    }
}
