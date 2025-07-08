using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using System.Runtime.CompilerServices;

namespace Dino_Engine.ECS.ECS_Architecture
{
    public class ECSWorld
    {
        private IDAllocator<uint> IDManager = new IDAllocator<uint>();
        public readonly CommandBuffer deferredCommands = new();

        private Dictionary<BitMask, Archetype> archetypes = new();
        private Dictionary<uint, (Archetype archetype, int index)> entityLocations = new();
        private Dictionary<Type, Entity> SingletonToEntity = new();

        public Entity Camera;

        public int Count => entityLocations.Count;
        public ECSWorld()
        {
            /*  OLD PARTICLE TEST SETUP
            Camera = CreateEntity("Camera",
                new PositionComponent(0, 40f, 200f),
                new RotationComponent(0.5f, 0, 0),
                new MainCameraComponent(),
                new LocalToWorldMatrixComponent(),
                new PositionRotationInputControlComponent(),
                new ViewMatrixComponent(),
                new PerspectiveProjectionComponent(MathF.PI / 3.5f, Engine.Resolution, 0.1f, 1000f));
            */


            Camera = CreateEntity("Camera",
                new PositionComponent(0, 240f, 0f),
                new RotationComponent(.26f, 2.35f, 0.0f),
                new MainCameraComponent(),
                new LocalToWorldMatrixComponent(),
                new PositionRotationInputControlComponent(),
                new ViewMatrixComponent(),
                new PerspectiveProjectionComponent(MathF.PI / 3.5f, Engine.Resolution, 0.1f, 1000f));;
        }

        public void Update(float deltaTime)
        {
            SystemRegistry.UpdateAll(this, deltaTime);

            ApplyDeferredCommands();
        }

        public void OnResize(ResizeEventArgs args)
        {
            SystemRegistry.OnResize(this, args);

            ApplyDeferredCommands();
        }

        public List<Entity> QueryEntities(BitMask withMask, BitMask withoutMask)
        {
            List<Entity> entities = new List<Entity>();

            foreach(Archetype archetype in QueryArchetypes(withMask, withoutMask))
            {
                entities.AddRange(archetype.entities);
            }

            return entities;
        }

        public void ClearAllEntitiesExcept(params Entity[] exceptions)
        {
            foreach (var (bitmask, archetype) in archetypes)
            {
                archetype.ClearAllEntitiesExcept (exceptions);
            }
            ApplyDeferredCommands();
        }

        public Entity GetSingleton<T>()
        {
            if (!SingletonToEntity.TryGetValue(typeof(T), out Entity entity))
                throw new Exception($"Singleton of type {typeof(T).Name} not registered!");
            return entity;
        }

        public void RegisterSingleton<T>(Entity entity)
        {
            SingletonToEntity[typeof(T)] = entity;
        }

        public void ApplyDeferredCommands()
        {
            foreach (var cmd in deferredCommands.createEntityCommands)
                CreateEntityDirect(cmd.Entity, cmd.Components);

            foreach (var cmd in deferredCommands.addComponentCommands)
                AddComponentToEntityDirect(cmd.Entity, cmd.Component);

            foreach (var cmd in deferredCommands.removeComponentCommands)
                RemoveComponentFromEntityDirect(cmd.Entity, cmd.Type);

            foreach (var cmd in deferredCommands.removeEntityCommands)
                DestroyEntityDirect(cmd.Entity);

            deferredCommands.Clear();
        }

        //TODO should cache this in the system
        public IEnumerable<Archetype> QueryArchetypes(BitMask withMask, BitMask withoutMask)
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
            var newEntity = new Entity(IDManager.Allocate());
            deferredCommands.createEntityCommands.Add(new CreateEntityCommand(newEntity, components));
            return newEntity;
        }

        public Entity CreateEntity(string name, params IComponent[] components)
        {
            var newEntity = new Entity(IDManager.Allocate());
            deferredCommands.createEntityCommands.Add(new CreateEntityCommand(newEntity, components));
            //deferredCommands.addComponentCommands.Add(new AddComponentCommand(newEntity, new NameComponent(name)));
            return newEntity;
        }

        private Entity CreateEntityDirect(Entity newEntity, params IComponent[] components)
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

            archetype.AddEntity(newEntity, CompIDtoDataMap);
            entityLocations[newEntity.Id] = (archetype, archetype.Count - 1);
            return newEntity;
        }
        public void DestroyEntity(Entity entity)
        {
            deferredCommands.removeEntityCommands.Add(new RemoveEntityCommand(entity));
        }

        private void DestroyEntityDirect(Entity entity)
        {
            if (!entityLocations.TryGetValue(entity.Id, out var location))
            {
                throw new InvalidOperationException("trying to destroy an entity that is not in enityLocations");
                //continue;
            }

            var (archetype, index) = location;
            int last = archetype.Count - 1;
            var lastEntity = archetype.entities[last];

            archetype.RemoveEntityAt(index);

            if (index != last)
                entityLocations[lastEntity.Id] = (archetype, index);

            entityLocations.Remove(entity.Id);
            IDManager.Release(entity.Id);
        }

        public T GetComponent<T>(Entity entity) where T : struct, IComponent
        {
            var (arch, index) = entityLocations[entity.Id];
            return arch.GetComponent<T>(index);
        }
        public void AddComponentToEntity(Entity entity, IComponent newComponent)
        {
            deferredCommands.addComponentCommands.Add(new AddComponentCommand(entity, newComponent));
        }

        private void AddComponentToEntityDirect(Entity entity, IComponent newComponent)
        {
            if (!entityLocations.TryGetValue(entity.Id, out var currentLocation))
                throw new Exception("Entity does not exist.");

            Archetype oldArchetype = currentLocation.archetype;
            int oldIndex = currentLocation.index;

            // Compute the new archetype's bitmask
            int newComponentId = ComponentTypeRegistry.GetId(newComponent.GetType());
            BitMask newMask = oldArchetype.Mask.WithBit(newComponentId);

            // Get or create the target archetype
            if (!archetypes.TryGetValue(newMask, out var newArchetype))
            {
                newArchetype = new Archetype(newMask);
                archetypes[newMask] = newArchetype;
            }

            // Prepare component values to transfer
            var components = new Dictionary<int, object>();

            foreach (int compId in oldArchetype.Mask.GetSetBits())
            {
                var array = oldArchetype.ComponentArrays[compId];
                var componentValue = array.GetType().GetMethod("Get")!.Invoke(array, new object[] { oldIndex });
                components[compId] = componentValue;
            }

            // Add the new component
            components[newComponentId] = newComponent;

            // Remove from old archetype (swap-remove)
            oldArchetype.RemoveEntityAt(oldIndex);

            // Add to new archetype
            newArchetype.AddEntity(entity, components);

            // Update entity location
            entityLocations[entity.Id] = (newArchetype, newArchetype.Count - 1);
        }
        public void RemoveComponentFromEntity(Entity entity, Type type)
        {
            deferredCommands.removeComponentCommands.Add(new RemoveComponentCommand(entity, type));
        }
        private void RemoveComponentFromEntityDirect(Entity entity, Type type) 
        {
            if (!entityLocations.TryGetValue(entity.Id, out var currentLocation))
                throw new Exception("Entity does not exist.");

            int compID = ComponentTypeRegistry.GetId(type);
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
            entityLocations[entity.Id] = (newArchetype, newArchetype.Count - 1);
        }
    }
}
