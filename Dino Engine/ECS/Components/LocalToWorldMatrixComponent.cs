using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct LocalToWorldMatrixComponent : IComponent
    {
        public Matrix4 ModelToWorldMatrix;
    }
}
