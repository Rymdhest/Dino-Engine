using Dino_Engine.Core;
using System.Reflection;

namespace Dino_Engine.ECS
{
    public abstract class ComponentSystem
    {
        private List<EntityOLD> _memberEntities = new List<EntityOLD>();
        public List<EntityOLD> MemberEntities { get => _memberEntities;}

        private List<Type> _requiredComponents = new List<Type>();
        private List<Type> _optionalComponents = new List<Type>();

        public ComponentSystem()
        {

        }

        internal void addRequiredComponent<T>() where T : ComponentOLD
        {
            _requiredComponents.Add(typeof(T));
        }
        internal void addOptionalComponent<T>() where T : ComponentOLD
        {
            _optionalComponents.Add(typeof(T));
        }

        internal abstract void UpdateEntity(EntityOLD entity);

        public virtual void AddMember(EntityOLD member)
        {
            bool passRequirements = true;
            foreach(Type type in _requiredComponents)
            {
                if (!member.hasComponent(type))
                {
                    Console.WriteLine($"WARNING - entity: {member} in system: {this.GetType().Name} is missing component: {type.Name}");
                    passRequirements = false;
                }
            }

            if (passRequirements)
            {
                MemberEntities.Add(member);
                member.AddSubscribedSystem(this);
            } else
            {
                throw new Exception("Tried to add an entity to a system with missing required components.");
            }
            
        }
        public virtual void RemoveMember(EntityOLD member)
        {
            MemberEntities.Remove(member);
            member.RemoveSubscribedSystem(this);
        }
        public void Update()
        {
            Engine.PerformanceMonitor.startTask(this.GetType().Name);
            for (int i = MemberEntities.Count - 1; i >= 0; i--)
            {
                UpdateEntity(MemberEntities[i]);
            }
            Engine.PerformanceMonitor.finishTask(this.GetType().Name);
        }
        public override string ToString()
        {
            return $"{GetType().Name}({MemberEntities.Count})";
        }
    }

}
