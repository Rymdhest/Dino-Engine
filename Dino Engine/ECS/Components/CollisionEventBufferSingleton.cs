using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct CollisionEvent
    {
        public EntityView EntityA;
        public EntityView EntityB;
        public Vector3 ContactPoint;
        public Vector3 Normal;
        public float ImpactForce;
    }

    // Singleton Component to hold frame events. 
    // Changed to a struct to satisfy the generic IComponent constraint.
    public struct CollisionEventBufferSingleton : IComponent
    {
        public List<CollisionEvent> Events;

        public CollisionEventBufferSingleton()
        {
            Events = new List<CollisionEvent>();
        }
    }
}
