using OpenTK.Windowing.Common;

namespace Dino_Engine.ECS
{
    public abstract class Component
    {
        private Entity _owner;
        private List<ComponentSystem> _subscribedSystems = new List<ComponentSystem>();

        public Entity Owner { get => _owner; set => _owner = value; }
        public List<ComponentSystem> SubscribedSystems { get => _subscribedSystems; set => _subscribedSystems = value; }

        public virtual void Initialize()   { }
        public virtual void CleanUp()
        {
            for (int i = SubscribedSystems.Count - 1 ; i >= 0; i--)
            {
                SubscribedSystems[i].RemoveMember(this);
            }
            SubscribedSystems.Clear();
        }
        public void AddSubscribedSystem(ComponentSystem system)
        {
            SubscribedSystems.Add(system);
        }
        public void RemoveSubscribedSystem(ComponentSystem system)
        {
            SubscribedSystems.Remove(system);
        }
        public virtual void OnResize(ResizeEventArgs eventArgs)
        {

        }
        public virtual void Update()
        {

        }
    }
}
