using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct MassComponent : IComponent
    {
        public float value;

        public MassComponent(float mass)
        {
            value = mass;
        }
    }
}
