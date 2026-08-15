using Dino_Engine.ECS.ECS_Architecture;

namespace Dino_Engine.ECS.Components
{
    public struct DirtyEntitiesBuffers
    {
        public List<Entity> TransformChangesBuffer { get; } = new();
        public List<Entity> SpawnedEntitiesBuffer { get; } = new();
        public List<Entity> DestroyedEntitiesBuffer { get; } = new();

        public DirtyEntitiesBuffers()
        {
        }
        public void ClearAll()
        {
            SpawnedEntitiesBuffer.Clear();
            DestroyedEntitiesBuffer.Clear();
            TransformChangesBuffer.Clear();
        }
    }
}
