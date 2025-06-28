using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.ECS_Architecture
{
    public class Archetype
    {
        public BitMask Mask;
        public readonly Dictionary<int, IComponentArray> ComponentArrays;
        public List<Entity> Entities = new();

        public Archetype(BitMask mask)
        {
            Mask = mask;
            ComponentArrays = new Dictionary<int, IComponentArray>();

            // For each component bit set in the mask, create its component array
            for (int id = 0; id < ComponentTypeRegistry.Count; id++)
            {
                if (mask.Has(id))
                {
                    ComponentArrays[id] = ComponentTypeRegistry.CreateArray(id);
                }
            }
        }

        public void AddEntity(Entity e, Dictionary<int, object> components)
        {
            Entities.Add(e);
            foreach (var kv in components)
                ComponentArrays[kv.Key].AddRaw(kv.Value);
        }

        public void RemoveEntityAt(int index)
        {
            int last = Entities.Count - 1;
            if (index != last)
            {
                Entities[index] = Entities[last];
                for (int i = 0; i < ComponentArrays.Count; i++)
                {
                    if (ComponentArrays[i] != null)
                        ComponentArrays[i].RemoveAt(index);
                }
            }
            Entities.RemoveAt(last);
            for (int i = 0; i < ComponentArrays.Count; i++)
            {
                if (ComponentArrays[i] != null)
                    ComponentArrays[i].RemoveAt(last);
            }
        }
        public ComponentArray<T> GetArray<T>() where T : struct, IComponent
        {
            int id = ComponentTypeRegistry.GetId<T>();
            return (ComponentArray<T>)ComponentArrays[id];
        }

        public T GetComponent<T>(int index) where T : struct, IComponent
        {
            return GetArray<T>().Get(index);
        }
    }
}
