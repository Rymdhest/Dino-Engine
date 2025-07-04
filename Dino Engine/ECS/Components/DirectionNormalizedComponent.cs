using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct DirectionNormalizedComponent : IComponent
    {
        private Vector3 _value;

        public Vector3 value
        {
            readonly get => _value;
            set => _value = Vector3.Normalize(value);
        }

        public DirectionNormalizedComponent(Vector3 direction)
        {
            _value = Vector3.Normalize(direction);
        }
    }
}
