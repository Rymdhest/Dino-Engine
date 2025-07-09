
namespace Dino_Engine.ECS.ECS_Architecture
{
    public struct EntityView
    {
        private readonly Archetype archetype;
        private readonly int indexInArchive;

        public EntityView(Archetype archetype, int index)
        {
            this.archetype = archetype;
            this.indexInArchive = index;
        }
        public Entity Entity => archetype.entities[indexInArchive];
        public T Get<T>() where T : struct, IComponent
        {
            return archetype.GetComponent<T>(indexInArchive);
        }
        public T GetOptional<T>(T defaultValue) where T : struct, IComponent
        {
            if (Has<T>()) return archetype.GetComponent<T>(indexInArchive);
            else return defaultValue;

        }

        public void Set<T>(T component) where T : struct, IComponent
        {
            archetype.SetComponent(indexInArchive, component);
        }

        public bool Has<T>() where T : struct, IComponent
        {
            return archetype.Has<T>();
        }

             
    }
}
