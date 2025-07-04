using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.ECS_Architecture
{
    public struct AddEntityCommand
    {
        public IComponent[] Components;
        public AddEntityCommand(params IComponent[] components) {Components = components;}
    }
    public struct AddComponentCommand
    {
        public Entity Entity;
        public IComponent Component;

        public AddComponentCommand(Entity entity, IComponent component)
        {
            Entity = entity;
            Component = component;
        }
    }

    public struct RemoveComponentCommand
    {
        public Entity Entity;
        public Type Type;
        public RemoveComponentCommand(Entity entity, Type type)
        {
            Entity = entity;
            Type = type;
        }
    }

    public struct RemoveEntityCommand
    {
        public Entity Entity;
        public RemoveEntityCommand(Entity entity) {Entity = entity;}
    }

    public struct CreateEntityCommand
    {
        public IComponent[] Components;
        public Entity Entity;
        public CreateEntityCommand(Entity entity, params IComponent[] components)
        {
            Components = components;
            Entity = entity;
        }
    }
    public class CommandBuffer
    {
        public List<CreateEntityCommand> createEntityCommands = new();
        public List<AddComponentCommand> addComponentCommands = new();
        public List<RemoveComponentCommand> removeComponentCommands = new();
        public List<RemoveEntityCommand> removeEntityCommands = new();

        public void Clear()
        {
            createEntityCommands.Clear();
            addComponentCommands.Clear();
            removeComponentCommands.Clear();
            removeEntityCommands.Clear();
        }
    }
}
