using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.ComponentsOLD
{
    public class VelocityComponent : ComponentOLD
    {
        public Vector3 velocity;

        public VelocityComponent(Vector3 velocity)
        {
            this.velocity = velocity;
        }
    }
}
