using Dino_Engine.Core;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.ECS.Components;
using System;
using System.Collections.Generic;

namespace Dino_Engine.ECS.ECS_Architecture
{
    public class Archetype
    {
        public BitMask Mask;
        public readonly Dictionary<int, object> ComponentArrays = new();
        public List<Entity> entities = new();
        public int EntityCount => entities.Count;

        public Archetype(BitMask mask)
        {
            Mask = mask;

            foreach (int bit in mask.GetSetBits())
            {
                Type t = ComponentTypeRegistry.GetType(bit);
                Type listType = typeof(List<>).MakeGenericType(t);
                ComponentArrays[bit] = (object)Activator.CreateInstance(listType)!;
            }
        }

        public void ClearAllEntitiesExcept(params Entity[] exceptions)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                bool isException = false;
                for (int j = 0; j < exceptions.Length; j++)
                {
                    if (entities[i].Id == exceptions[j].Id)
                    {
                        isException = true;
                        break;
                    }
                }

                if (!isException)
                {
                    Engine.Instance.world.DestroyEntity(entities[i]);
                }
            }
        }

        public void AddEntity(Entity e, Dictionary<int, object> components)
        {
            entities.Add(e);
            foreach (var kv in ComponentArrays)
            {
                int compID = kv.Key;
                dynamic compArray = kv.Value;
                compArray.Add((dynamic)components[compID]);
            }
        }

        public void RemoveEntityAt(int index)
        {
            foreach (var kv in ComponentArrays)
            {
                dynamic compArray = kv.Value;
                IComponent component = compArray[index];
                if (component is ICleanupComponent) component.Cleanup();
            }

            int last = entities.Count - 1;
            if (index != last)
            {
                entities[index] = entities[last];

                foreach (var kvp in ComponentArrays)
                {
                    dynamic list = kvp.Value;
                    list[index] = list[last];
                }
            }

            entities.RemoveAt(last);

            foreach (var kvp in ComponentArrays)
            {
                dynamic list = kvp.Value;
                list.RemoveAt(last);
            }
        }

        // Optimized array access for bulk processing systems
        public List<T>? GetComponentArray<T>() where T : struct, IComponent
        {
            int id = ComponentTypeRegistry.GetId(typeof(T));
            if (ComponentArrays.TryGetValue(id, out object? array))
            {
                return (List<T>)array;
            }
            return null; // Return null instead of crashing
        }

        public T GetComponent<T>(int index) where T : struct, IComponent
        {
            return GetComponentArray<T>()[index];
        }

        public void SetComponent<T>(int index, T component) where T : struct, IComponent
        {
            GetComponentArray<T>()[index] = component;
        }

        public bool Has<T>() where T : struct, IComponent
        {
            return Mask.Has(ComponentTypeRegistry.GetId(typeof(T)));
        }
    }
}