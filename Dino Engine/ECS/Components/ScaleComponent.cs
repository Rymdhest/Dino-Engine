using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct ScaleComponent : IComponent
    {
        public Vector3 value;

        public ScaleComponent(Vector3 scale)
        {
            value = scale;
        }

        public ScaleComponent(float x, float y, float z)
        {
            value = new Vector3(x, y, z);
        }
    }
}
