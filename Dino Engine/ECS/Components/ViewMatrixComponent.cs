using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct ViewMatrixComponent : IComponent
    {
        public Matrix4 value;

        public ViewMatrixComponent()
        {
            value = Matrix4.Identity;
        }
    }
}
