using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.ComponentsOLD
{
    public class MassComponent : ComponentOLD
    {
        public float mass;

        public MassComponent(float mass)
        {
            this.mass = mass;
        }
    }
}
