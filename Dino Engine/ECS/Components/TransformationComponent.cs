

using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS
{
    internal class TransformationComponent : Component
    {
        private Transformation _transformation;
        public Transformation Transformation { get => _transformation; set => _transformation = value; }

        public TransformationComponent(Vector3 position, Vector3 rotation, Vector3 scale)
        {
            _transformation = new Transformation(position, rotation, scale);
        }
        public TransformationComponent(Transformation transformation)
        {
            _transformation = transformation;
        }
    }
}
