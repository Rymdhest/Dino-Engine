using OpenTK.Windowing.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.ECS_Architecture
{
    public class SystemRegistry
    {
        public static readonly List<SystemBase> Systems = new();
        public static void AutoRegisterAllSystems()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (!type.IsSubclassOf(typeof(SystemBase)) || type.IsAbstract)
                        continue;

                    var ctor = type.GetConstructor(Type.EmptyTypes);
                    if (ctor == null)
                        throw new InvalidOperationException($"System {type} must have a parameterless constructor!");

                    var instance = (SystemBase)ctor.Invoke(null);
                    Systems.Add(instance);
                }
            }
        }
        public static void UpdateAll(ECSWorld world, float deltaTime)
        {
            foreach (var sys in Systems)
                sys.Update(world, deltaTime);
        }

        public static void OnResize(ECSWorld world, ResizeEventArgs args)
        {
            foreach (var sys in Systems)
                sys.OnResize(world, args);
        }
    }


}
