using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct PositionComponent : IComponent
    {
        public Vector3 value;

        public PositionComponent(Vector3 position)
        {
            value = position;
        }

        public PositionComponent(float x, float y, float z)
        {
            value = new Vector3(x, y, z);
        }
    }
}
