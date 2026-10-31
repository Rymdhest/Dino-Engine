using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct GrassDisplaceComponent : IComponent
    {
        public float radius;
        public float power;
        public float exponent;

        public GrassDisplaceComponent(float radius)
        {
            this.radius = radius;
            exponent = 1.0f;
            power = 1.0f;
        }
    }
}
