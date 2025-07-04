using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.ECS_Architecture
{
    public static class ComponentTypeRegistry
    {
        private static Dictionary<Type, int> typeToId = new();
        private static Dictionary<int, Type> idToType = new();

        public static int Count => idToType.Count;

        private static int Register<T>() where T : struct, IComponent =>
            Register(typeof(T));

        private static int Register(Type type)
        {
            if (!typeof(IComponent).IsAssignableFrom(type) || !type.IsValueType)
                throw new ArgumentException("Only struct types implementing IComponent can be registered.");

            if (!typeToId.TryGetValue(type, out int id))
            {
                id = idToType.Count;
                typeToId[type] = id;
                idToType[id] = type;
            }
            return id;
        }

        public static void AutoRegisterAllComponents()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (!type.IsValueType || !typeof(IComponent).IsAssignableFrom(type))
                        continue;

                    Register(type); // registers it
                }
            }
        }

        public static int GetId<T>() where T : struct, IComponent
        {
            return typeToId[typeof(T)];
        }

        public static Type GetType(int id) => idToType[id];
        public static int GetId(Type type)
        {
            if (!typeToId.TryGetValue(type, out int id))
                throw new ArgumentException($"Component type not registered: {type.FullName}");
            return id;
        }
    }
}
