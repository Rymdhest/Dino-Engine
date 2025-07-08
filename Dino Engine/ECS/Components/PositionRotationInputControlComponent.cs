using Dino_Engine.ECS.ECS_Architecture;

namespace Dino_Engine.ECS.Components
{
    public struct PositionRotationInputControlComponent : IComponent
    {
        public float Yaw;
        public float Pitch;

        public PositionRotationInputControlComponent(float yaw, float pitch)
        {
            Yaw = yaw;
            Pitch = pitch;
        }
    }


}
