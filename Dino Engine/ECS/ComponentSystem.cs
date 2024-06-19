namespace Dino_Engine.ECS
{
    public class ComponentSystem
    {
        private List<Component> memberComponents = new List<Component>();

        public ComponentSystem()
        {

        }
        public virtual void AddMember(Component member)
        {
            memberComponents.Add(member);
            member.AddSubscribedSystem(this);
        }
        public virtual void RemoveMember(Component member)
        {
            memberComponents.Remove(member);
            member.RemoveSubscribedSystem(this);
        }

        public List<Component> getMembers()
        {
            return memberComponents;
        }
        public virtual void Update() { }
    }

}
