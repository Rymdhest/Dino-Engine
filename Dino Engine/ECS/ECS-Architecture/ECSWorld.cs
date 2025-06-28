using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.ECS_Architecture
{
    public class ECSWorld
    {
        private IDAllocator<uint> IDManager = new IDAllocator<uint>();
        private Dictionary<BitMask, Archetype> archetypes = new();
        private Dictionary<int, (Archetype archetype, int index)> entityLocations = new();
        private List<SystemBase> systems = new();
        private HashSet<int> pendingDestruction = new();
        public void RegisterSystem(SystemBase system) => systems.Add(system);

        public ECSWorld()
        {

        }

        public void Update(float deltaTime)
        {
            foreach (var system in systems) system.Update(this, deltaTime);

            FlushDestroyedEntities();
        }

        //TODO should cache this in the system
        public IEnumerable<Archetype> Query(BitMask withMask, BitMask withoutMask)
        {
            foreach (var arch in archetypes.Values)
            {
                if (!arch.Mask.ContainsAll(withMask))
                    continue;
                if (arch.Mask.IntersectsAny(withoutMask))
                    continue;
                yield return arch;
            }
        }

        public Entity CreateEntity(params IComponent[] components)
        {
            var mask = new BitMask();
            var CompIDtoDataMap = new Dictionary<int, object>();

            foreach (IComponent compoent in components)
            {
                int componentID = ComponentTypeRegistry.GetId(compoent.GetType());
                mask = mask.WithBit(componentID);
                CompIDtoDataMap[componentID] = compoent;
            }

            if (!archetypes.TryGetValue(mask, out Archetype? archetype))
            {
                archetype = new Archetype(mask);
                archetypes[mask] = archetype;
            }

            var entity = new Entity(IDManager.Allocate());
            archetype.AddEntity(entity, CompIDtoDataMap);
            entityLocations[entity.Id] = (archetype, archetype.Entities.Count - 1);
            return entity;
        }

        public void DestroyEntity(Entity entity)
        {
            pendingDestruction.Add(entity.Id);
        }
        public void FlushDestroyedEntities()
        {
            foreach (var entityId in pendingDestruction)
            {
                if (!entityLocations.TryGetValue(entityId, out var location))
                    continue;

                var (archetype, index) = location;
                int last = archetype.Entities.Count - 1;
                var lastEntity = archetype.Entities[last];

                archetype.RemoveEntityAt(index);

                if (index != last)
                    entityLocations[lastEntity.Id] = (archetype, index);

                entityLocations.Remove(entityId);
            }
            pendingDestruction.Clear();
        }

        public T GetComponent<T>(Entity entity) where T : struct, IComponent
        {
            var (arch, index) = entityLocations[entity.Id];
            return arch.GetComponent<T>(index);
        }

        public void AddComponentToEntity<T>(Entity entity, T component) where T : struct, IComponent
        {
            if (!entityLocations.TryGetValue(entity.Id, out (Archetype archetype, int index) currentLocation))
                throw new Exception("Entity does not exist.");

            int newCompID = ComponentTypeRegistry.GetId<T>();
            BitMask newMask = currentLocation.archetype.Mask.WithBit(newCompID);

            if (!archetypes.TryGetValue(newMask, out Archetype newArchetype))
            {
                newArchetype = new Archetype(newMask);
                archetypes[newMask] = newArchetype;
            }

            var componentMap = new Dictionary<int, object>();
            for (int i = 0; i < ComponentTypeRegistry.Count; i++)
            {
                if (currentLocation.archetype.Mask.Has(i))
                {
                    var array = currentLocation.archetype.ComponentArrays[i];
                    var method = array.GetType().GetMethod("Get");
                    componentMap[i] = method.Invoke(array, new object[] { currentLocation.index });
                }
            }
            componentMap[newCompID] = component;

            currentLocation.archetype.RemoveEntityAt(currentLocation.index);
            newArchetype.AddEntity(entity, componentMap);
            entityLocations[entity.Id] = (newArchetype, newArchetype.Entities.Count - 1);
        }

        public void RemoveComponentFromEntity<T>(Entity entity) where T : struct, IComponent
        {
            if (!entityLocations.TryGetValue(entity.Id, out var currentLocation))
                throw new Exception("Entity does not exist.");

            int compID = ComponentTypeRegistry.GetId<T>();
            if (!currentLocation.archetype.Mask.Has(compID))
            {
                throw new Exception("Entitys archetypes mask does not have component.");
                return; // Component not present
            }


            var newMask = currentLocation.archetype.Mask.WithoutBit(compID);

            if (!archetypes.TryGetValue(newMask, out Archetype newArchetype))
            {
                newArchetype = new Archetype(newMask);
                archetypes[newMask] = newArchetype;
            }

            var componentMap = new Dictionary<int, object>();
            for (int i = 0; i < ComponentTypeRegistry.Count; i++)
            {
                if (i == compID || !currentLocation.archetype.Mask.Has(i)) continue;
                var array = currentLocation.archetype.ComponentArrays[i];
                var method = array.GetType().GetMethod("Get");
                componentMap[i] = method.Invoke(array, new object[] { currentLocation.index });
            }

            currentLocation.archetype.RemoveEntityAt(currentLocation.index);
            newArchetype.AddEntity(entity, componentMap);
            entityLocations[entity.Id] = (newArchetype, newArchetype.Entities.Count - 1);
        }
    }
}
