using Dino_Engine.ECS.ECS_Architecture;

namespace Dino_Engine.ECS.Components
{
    public struct DirtyEntitiesSingleton : IComponent
    {
        public List<Entity> TransformChangesBuffer { get; } = new();
        public List<Entity> SpawnedEntitiesBuffer { get; } = new();
        public List<Entity> DestroyedEntitiesBuffer { get; } = new();

        public DirtyEntitiesSingleton()
        {
        }
    }
}
