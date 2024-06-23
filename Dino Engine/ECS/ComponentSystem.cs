using System.Reflection;

namespace Dino_Engine.ECS
{
    public abstract class ComponentSystem
    {
        private List<Entity> _memberEntities = new List<Entity>();
        public List<Entity> MemberEntities { get => _memberEntities;}

        private List<Type> _requiredComponents = new List<Type>();
        private List<Type> _optionalComponents = new List<Type>();

        public ComponentSystem()
        {

        }

        internal void addRequiredComponent<T>() where T : Component
        {
            _requiredComponents.Add(typeof(T));
        }
        internal void addOptionalComponent<T>() where T : Component
        {
            _optionalComponents.Add(typeof(T));
        }

        internal abstract void UpdateEntity(Entity entity);

        public virtual void AddMember(Entity member)
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
        public virtual void RemoveMember(Entity member)
        {
            MemberEntities.Remove(member);
            member.RemoveSubscribedSystem(this);
        }
        public void Update()
        {
            foreach(Entity entity in MemberEntities)
            {
                UpdateEntity(entity);
            }
        }
        public override string ToString()
        {
            return $"{GetType().Name}({MemberEntities.Count})";
        }
    }

}
