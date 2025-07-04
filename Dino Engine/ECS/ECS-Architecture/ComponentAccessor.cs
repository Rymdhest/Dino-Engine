using System.Collections;
namespace Dino_Engine.ECS.ECS_Architecture
{
    public class ComponentAccessor : IEnumerable<EntityView>
    {
        private readonly Archetype archetype;

        public ComponentAccessor(Archetype archetype)
        {
            this.archetype = archetype;
        }

        public IEnumerator<EntityView> GetEnumerator()
        {
            for (int i = 0; i < archetype.Count; i++)
            {
                yield return new EntityView(archetype, i);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
