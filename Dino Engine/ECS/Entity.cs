
using Dino_Engine.Core;
using OpenTK.Windowing.Common;

namespace Dino_Engine.ECS
{
    public class Entity
    {
        Dictionary<Type, Component> components;

        private string _name;

        private List<ComponentSystem> _subscribedSystems = new List<ComponentSystem>();

        public List<ComponentSystem> SubscribedSystems { get => _subscribedSystems; set => _subscribedSystems = value; }
        public string Name { get => _name; set => _name = value; }

        public Entity(string name)
        {
            components= new Dictionary<Type, Component>();
            _name = name;
            Engine.Instance.ECSEngine.Entities.Add(this);

        }
        public bool TryGetComponent<T>(out T? outComponent) where T : Component
        {
            Type type = typeof(T);
            if (components.TryGetValue(type, out Component component))
            {
                outComponent = (T)component;
                return true;
            } else
            {
                outComponent = null;
                return false;
            }

        }
        public T getComponent<T>() where T : Component
        {
            Type type = typeof(T);
            if (components.TryGetValue(type, out Component component))
            {
                return (T)component;
            }
            else return null;
        }

        public void AddSubscribedSystem(ComponentSystem system)
        {
            SubscribedSystems.Add(system);
        }
        public void RemoveSubscribedSystem(ComponentSystem system)
        {
            SubscribedSystems.Remove(system);
        }

        public void addComponent<T>(T component) where T : Component
        {
            component.Owner = this;
            components[typeof(T)] = component;
            component.Initialize();
        }
        public virtual void CleanUp()
        {
            foreach (Component component in components.Values)
            {
                component.CleanUp();
            }
            components.Clear();

            for (int i = SubscribedSystems.Count - 1; i >= 0; i--)
            {
                SubscribedSystems[i].RemoveMember(this);
            }
            SubscribedSystems.Clear();

            Engine.Instance.ECSEngine.Entities.Remove(this);
        }
        public Boolean hasComponent(Type type)
        {
            return components.ContainsKey(type);
        }
        public Boolean hasComponent<T>() where T : Component
        {
            return components.ContainsKey(typeof(T));
        }
        public void removeComponent<T>() where T : Component
        {
            Type type = typeof(T);
            if (components.TryGetValue(type, out Component component))
            {
                component.CleanUp();
                components.Remove(typeof(T));
            }
        }
        public void updateComponents()
        {
            foreach (Component component in components.Values)
            {
                component.Update();
            }
        }

        public void OnResize(ResizeEventArgs eventArgs)
        {
            foreach (Component component in components.Values)
            {
                component.OnResize(eventArgs);
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
