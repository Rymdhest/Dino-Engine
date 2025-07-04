using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct AmbientLightComponent : IComponent
    {
        public float value;

        public AmbientLightComponent(float ambient)
        {
            value = ambient;
        }
    }


}
