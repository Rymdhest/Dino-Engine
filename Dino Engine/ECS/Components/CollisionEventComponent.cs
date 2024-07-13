using System;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public class CollisionEventComponent : Component
    {

        public OnCollision onCollision;

        public CollisionEventComponent(OnCollision onCollision)
        {
            this.onCollision = onCollision;
        }
        public delegate void OnCollision(Entity collider, Vector3 collisionPoint, Vector3 collisionNormal);
    }
}
