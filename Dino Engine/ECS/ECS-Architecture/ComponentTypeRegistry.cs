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
        public static List<Type> idToType = new();
        private static readonly List<Func<IComponentArray>> arrayFactories = new();
        public static int Count => idToType.Count;

        public static int Register<T>() where T : struct, IComponent =>
            Register(typeof(T));

        public static int Register(Type type)
        {
            if (!typeof(IComponent).IsAssignableFrom(type) || !type.IsValueType)
                throw new ArgumentException("Only struct types implementing IComponent can be registered.");

            if (!typeToId.TryGetValue(type, out int id))
            {
                id = idToType.Count;
                typeToId[type] = id;
                idToType.Add(type);
            }
            arrayFactories.Add(() =>
            {
                var arrayType = typeof(ComponentArray<>).MakeGenericType(type);
                return (IComponentArray)Activator.CreateInstance(arrayType)!;
            });
            return id;
        }

        public static void AutoRegisterAllComponents()
        {
            var componentTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                    t.IsValueType &&
                    !t.IsAbstract &&
                    typeof(IComponent).IsAssignableFrom(t));

            // Find the correct generic Register<T>() method
            var registerMethod = typeof(ComponentTypeRegistry)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m =>
                    m.Name == nameof(Register) &&
                    m.IsGenericMethodDefinition &&
                    m.GetGenericArguments().Length == 1
                );

            foreach (var type in componentTypes)
            {
                var genericMethod = registerMethod.MakeGenericMethod(type);
                genericMethod.Invoke(null, null);
            }
        }

        public static int GetId<T>() where T : struct, IComponent
        {
            return typeToId[typeof(T)];
        }

        public static IComponentArray CreateArray(int id) => arrayFactories[id]();
        public static int GetId(Type t)
        {
            if (!typeToId.TryGetValue(t, out int id))
                throw new ArgumentException($"Component type not registered: {t.FullName}");
            return id;
        }
    }
}
