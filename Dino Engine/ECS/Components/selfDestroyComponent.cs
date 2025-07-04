using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct selfDestroyComponent : IComponent
    {
        public float SecondsRemaining;
        public float InitialSecondsRemaining;

        public selfDestroyComponent(float time)
        {
            SecondsRemaining = time;
            InitialSecondsRemaining = time;
        }
        public float getRemainingRatio()
        {
            return SecondsRemaining / InitialSecondsRemaining;
        }
    }
}
