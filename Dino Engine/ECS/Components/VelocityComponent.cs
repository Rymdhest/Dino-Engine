using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct VelocityComponent : IComponent
    {
        public Vector3 value;

        public VelocityComponent(Vector3 velocity)
        {
            value = velocity;
        }
    }

    
}
