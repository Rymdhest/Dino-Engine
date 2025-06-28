using System;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.ComponentsOLD
{
    public class CollisionEventComponent : ComponentOLD
    {

        public OnCollision onCollision;

        public CollisionEventComponent(OnCollision onCollision)
        {
            this.onCollision = onCollision;
        }
        public delegate void OnCollision(EntityOLD collider, Vector3 collisionPoint, Vector3 collisionNormal);
    }
}
