using Dino_Engine.Core;
using Dino_Engine.ECS.ComponentsOLD;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.SystemsOLD
{
    public class GravitySystem : ComponentSystem
    {
        public float gravityConstant = 10f;
        public GravitySystem()
        {
            addRequiredComponent<MassComponent>();
            addRequiredComponent<VelocityComponent>();
        }

        internal override void UpdateEntity(EntityOLD entity)
        {
            float mass = entity.getComponent<MassComponent>().mass;
            entity.getComponent<VelocityComponent>().velocity.Y -= mass * gravityConstant * Engine.Delta;
        }
    }
}
