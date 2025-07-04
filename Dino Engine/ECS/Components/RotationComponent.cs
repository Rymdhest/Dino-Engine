using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct RotationComponent : IComponent
    {
        public Quaternion quaternion;

        public RotationComponent(Quaternion quaternion)
        {
            this.quaternion = quaternion;
        }

        public RotationComponent(float x, float y, float z)
        {
            this.quaternion = new Quaternion(x, y, z);
        }

        public RotationComponent(Vector3 rotation)
        {
            this.quaternion = new Quaternion(rotation);
        }


    }
}
