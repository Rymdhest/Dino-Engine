namespace Dino_Engine.ECS
{
    public class ComponentSystem
    {
        private List<Entity> _memberEntities = new List<Entity>();
        public List<Entity> MemberEntities { get => _memberEntities;}

        public ComponentSystem()
        {

        }

        public virtual void AddMember(Entity member)
        {
            MemberEntities.Add(member);
            member.AddSubscribedSystem(this);
        }
        public virtual void RemoveMember(Entity member)
        {
            MemberEntities.Remove(member);
            member.RemoveSubscribedSystem(this);
        }
        public virtual void Update() { }
    }

}
