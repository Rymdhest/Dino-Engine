using Dino_Engine.ECS.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Systems
{
    internal class TerrainSystem : ComponentSystem
    {
        public TerrainSystem()
        {
            addRequiredComponent<TransformationComponent>();
            addRequiredComponent<ModelComponent>();
            addRequiredComponent<TerrainMapsComponent>();
        }

        internal override void UpdateEntity(Entity entity)
        {

        }
    }
}
