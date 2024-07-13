using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Physics
{
    public class TerrainHitBox : AABB
    {
        public TerrainHitBox(Vector3 min, Vector3 max) : base(min, max)
        {

        }
    }
}
