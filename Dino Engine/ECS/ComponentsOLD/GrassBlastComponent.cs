using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.ComponentsOLD
{
    public class GrassBlastComponent : ComponentOLD
    {
        public float radius;
        public float power;
        public float exponent;

        public GrassBlastComponent(float radiusm, float power, float exponent)
        {
            radius = radiusm;
            this.power = power;
            this.exponent = exponent;
        }
    }
}
