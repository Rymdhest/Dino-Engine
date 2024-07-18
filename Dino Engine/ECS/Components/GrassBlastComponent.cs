using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Components
{
    public class GrassBlastComponent : Component
    {
        public float radius;
        public float power;
        public float exponent;

        public GrassBlastComponent(float radiusm, float power, float exponent)
        {
            this.radius = radiusm;
            this.power = power;
            this.exponent = exponent;
        }
    }
}
