
using Dino_Engine.Core;
using Dino_Engine.ECS.SystemsOLD;
using OpenTK.Windowing.Common;
using System.ComponentModel;
using System.Text;

namespace Dino_Engine.ECS
{
    public class EntityOLD
    {
        Dictionary<Type, ComponentOLD> components;

        private string _name;

        private List<ComponentSystem> _subscribedSystems = new List<ComponentSystem>();

        public List<ComponentSystem> SubscribedSystems { get => _subscribedSystems; set => _subscribedSystems = value; }
        public string Name { get => _name; set => _name = value; }

        public EntityOLD(string name)
        {
            components= new Dictionary<Type, ComponentOLD>();
            _name = name;

            Engine.Instance.ECSEngine.AddEnityToSystem<AllEntitySystem>(this);

        }
        public bool TryGetComponent<T>(out T? outComponent) where T : ComponentOLD
        {
            Type type = typeof(T);
            if (components.TryGetValue(type, out ComponentOLD component))
            {
                outComponent = (T)component;
                return true;
            } else
            {
                outComponent = null;
                return false;
            }

        }
        public T getComponent<T>() where T : ComponentOLD
        {
            Type type = typeof(T);
            if (components.TryGetValue(type, out ComponentOLD component))
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

        public void addComponent<T>(T component) where T : ComponentOLD
        {
            component.Owner = this;
            components[typeof(T)] = component;
            component.Initialize();
        }
        public virtual void CleanUp()
        {
            for (int i = SubscribedSystems.Count - 1; i >= 0; i--)
            {
                SubscribedSystems[i].RemoveMember(this);
            }
            SubscribedSystems.Clear();

            foreach (ComponentOLD component in components.Values)
            {
                component.CleanUp();
            }
            components.Clear();


        }
        public Boolean hasComponent(Type type)
        {
            return components.ContainsKey(type);
        }
        public Boolean hasComponent<T>() where T : ComponentOLD
        {
            return components.ContainsKey(typeof(T));
        }
        public void removeComponent<T>() where T : ComponentOLD
        {
            Type type = typeof(T);
            if (components.TryGetValue(type, out ComponentOLD component))
            {
                component.CleanUp();
                components.Remove(typeof(T));
            }
        }
        public void updateComponents()
        {
            foreach (ComponentOLD component in components.Values)
            {
                component.Update();
            }
        }

        public void OnResize(ResizeEventArgs eventArgs)
        {
            foreach (ComponentOLD component in components.Values)
            {
                component.OnResize(eventArgs);
            }
        }

        public string GetFullInformationString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"Entity____________{Name}____________");

            if (SubscribedSystems.Count > 0)
            {
                stringBuilder.AppendLine("\tSubscribed Systems:");
                foreach (ComponentSystem system in SubscribedSystems)
                {
                    stringBuilder.AppendLine($"\t\t{system}");
                }
                stringBuilder.AppendLine("\t************\n");
            }


            if (components.Count > 0)
            {
                stringBuilder.AppendLine("\tComponents:");
                foreach (ComponentOLD component in components.Values)
                {
                    stringBuilder.AppendLine(component.getInformationString("\t\t"));
                }
                stringBuilder.AppendLine("\t************\n");
            }


            return stringBuilder.ToString();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
