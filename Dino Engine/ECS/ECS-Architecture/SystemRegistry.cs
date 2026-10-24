using Dino_Engine.ECS.Systems;
using OpenTK.Windowing.Common;

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
            Systems.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        public static void ManualRegisterSystems()
        {

            // -2
            Systems.Add(new SkeletalAnimationSystem());

            // -1
            Systems.Add(new CharacterRenderSystem());
            Systems.Add(new TerrainChunkSystem());


            // 0
            Systems.Add(new CelestialBodyRenderSystem());
            Systems.Add(new DirectionalCascadeShadowSystem());
            Systems.Add(new DirectionalLightSystem());
            Systems.Add(new GrassChunkRenderSystem());
            Systems.Add(new GrassDisplaceSystem());
            Systems.Add(new GravitySystem());
            Systems.Add(new PlayerInputSystem());
            Systems.Add(new PositionRotationInputControlSystem());
            Systems.Add(new LocalToWorldTransformChildSystem());
            Systems.Add(new ModelRenderSystem());
            Systems.Add(new ParticleEmitterSystem());
            Systems.Add(new ParticleRenderSystem());
            Systems.Add(new PhysicsSystem());
            Systems.Add(new PointLightShadowSystem());
            Systems.Add(new PointLightSystem());
            Systems.Add(new SelfDestroySystem());
            Systems.Add(new SkySYstem());
            Systems.Add(new SpotlightShadowSystem());
            Systems.Add(new SpotLightSystem());
            Systems.Add(new VelocitySystem());
            Systems.Add(new ViewMatrixSystem());
            Systems.Add(new CameraSystem());

            // 1
            Systems.Add(new LocalToWorldTransformRootSystem());
            Systems.Add(new RenderSpatialGridSystem());
            Systems.Add(new TerrainChunkRenderSystem());

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
