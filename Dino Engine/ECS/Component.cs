using OpenTK.Windowing.Common;

namespace Dino_Engine.ECS
{
    public abstract class Component
    {
        private Entity _owner;
        public Entity Owner { get => _owner; set => _owner = value; }

        public virtual void Initialize() { }
        public virtual void OnResize(ResizeEventArgs eventArgs)  { }
        public virtual void Update() { }
        public virtual void CleanUp() { }
    }
}
