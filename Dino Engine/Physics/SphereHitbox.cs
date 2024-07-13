using Dino_Engine.Util.Data_Structures.Grids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Physics
{
    public class SphereHitbox : HitBox
    {
        private float _radius;
        public SphereHitbox(float radius)
        {
            Radius = radius;
        }

        public float Radius { get => _radius; set => _radius = value; }
    }
}
