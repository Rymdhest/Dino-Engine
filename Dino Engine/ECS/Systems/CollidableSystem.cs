using Dino_Engine.ECS.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Systems
{
    public class CollidableSystem : ComponentSystem
    {

        public CollidableSystem()
        {
            addRequiredComponent<TransformationComponent>();
            addRequiredComponent<CollisionComponent>();
        }
        internal override void UpdateEntity(Entity entity)
        {

        }
    }
}
