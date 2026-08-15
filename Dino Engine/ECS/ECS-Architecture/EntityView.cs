
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;

namespace Dino_Engine.ECS.ECS_Architecture
{

    public static class TransformTraits<T>
    {
        public static readonly bool IsTransformComponent =
            typeof(T) == typeof(PositionComponent) ||
            typeof(T) == typeof(RotationComponent) ||
            typeof(T) == typeof(ScaleComponent);
    }

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
            if (TransformTraits<T>.IsTransformComponent)
            {
                ECSWorld world = Engine.Instance.world;
                world.DirtyEntities.Add(this.Entity);
            }
        }

        public bool Has<T>() where T : struct, IComponent
        {
            return archetype.Has<T>();
        }

             
    }
}
