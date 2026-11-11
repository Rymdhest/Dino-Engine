using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.ECS.Systems;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>What a prop the server moves gives off, light and flame, kept where it is on the prop wherever that goes: an arena's chandeliers burn as they hang, as they fall and as they lie.</summary>
    public struct PropLightsComponent : IComponent
    {
        public PropLights Lights;

        public PropLightsComponent(IReadOnlyList<AssetLight> lights)
        {
            Lights = new PropLights(lights);
        }
    }

    /// <summary>On each light and flame made for a prop, saying whose it is, so that one whose id has gone to something else is never taken for it.</summary>
    public struct PropLightOfComponent : IComponent
    {
        public PropLights Owner;
    }

    /// <summary>One prop's lights, and the entities made for them. It's what goes with the prop, rather than the prop's id, as a client gives an id to the next thing made the moment it's free.</summary>
    public sealed class PropLights
    {
        public readonly IReadOnlyList<AssetLight> Lights;
        public readonly List<Entity> Made = new();
        public bool Spawned;
        public int Seen;

        public PropLights(IReadOnlyList<AssetLight> lights)
        {
            Lights = lights;
        }
    }

    /// <summary>
    /// Makes a moving prop's lights and flames the first time it's seen (<see cref="AssetLightEntities"/>), carries them with it every frame after, and takes them away the first frame it isn't there.
    /// </summary>
    public sealed class PropLightSystem : SystemBase
    {
        private readonly List<PropLights> _live = new();
        private int _frame;

        public PropLightSystem()
            : base(new BitMask(typeof(PropLightsComponent), typeof(PositionComponent), typeof(RotationComponent), typeof(ScaleComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            PropLights lights = entity.Get<PropLightsComponent>().Lights;
            Vector3 position = entity.Get<PositionComponent>().value;
            Quaternion rotation = entity.Get<RotationComponent>().quaternion;
            Vector3 scale = entity.Get<ScaleComponent>().value;
            if (!lights.Spawned)
            {
                lights.Spawned = true;
                _live.Add(lights);
            }
            else if (lights.Made.TrueForAll(made => Owned(world, made, lights)))
            {
                AssetLightEntities.Move(world, lights.Lights, position, rotation, scale, lights.Made);
                lights.Seen = _frame;
                return;
            }
            // New, or something took them away with everything else: made again where the prop is.
            Destroy(world, lights);
            AssetLightEntities.Spawn(world, lights.Lights, position, rotation, scale, lights.Made, new PropLightOfComponent { Owner = lights });
            lights.Seen = _frame;
        }

        private static bool Owned(ECSWorld world, Entity made, PropLights lights)
        {
            if (!made.IsValid() || !world.TryGetEntityLocation(made, out _, out _)) return false;
            EntityView view = world.GetEntityView(made);
            return view.Has<PropLightOfComponent>() && ReferenceEquals(view.Get<PropLightOfComponent>().Owner, lights);
        }

        // Whatever of a prop's lights is still its own goes, and the list is emptied.
        private static void Destroy(ECSWorld world, PropLights lights)
        {
            foreach (Entity made in lights.Made)
            {
                if (Owned(world, made, lights)) world.DestroyEntity(made);
            }
            lights.Made.Clear();
        }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            _frame++;
            base.UpdateFrame(world, deltaTime);

            // A prop that's gone takes what it gave off with it.
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                PropLights lights = _live[i];
                if (lights.Seen == _frame) continue;
                Destroy(world, lights);
                _live.RemoveAt(i);
            }
        }
    }
}
