using Dino_Engine.Physics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Components
{
    public class CollisionComponent : Component
    {
        public HitBox HitBox;

        public CollisionComponent(HitBox hitBox)
        {
            this.HitBox = hitBox;
        }
    }
}
