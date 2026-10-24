using Dino_Engine.ECS.ECS_Architecture;

namespace Dino_Engine.ECS.Components
{
    public struct ParentComponent : IComponent
    {
        public Entity parent;
        public bool ignoreParentRotation;

        public ParentComponent(Entity parent, bool ignoreParentRotation = false)
        {
            this.parent = parent;
            this.ignoreParentRotation = ignoreParentRotation;
        }
    }
}
