using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.Physics
{
    /// <summary>
    /// Gameplay access to the engine's wind (the world's <see cref="WindSingleton"/>). Samples match what shaders read
    /// from the wind map. Without a wind singleton the air is still and blasts are ignored.
    /// </summary>
    public static class Wind
    {
        public static bool TryGet(ECSWorld world, out WindSingleton wind)
        {
            wind = default;
            if (world == null || !world.TryGetSingleton<WindSingleton>(out Entity entity)) return false;
            wind = world.GetComponent<WindSingleton>(entity);
            return true;
        }

        /// <summary>The wind at <paramref name="position"/>, near the ground, including every emitter.</summary>
        public static WindSample Sample(ECSWorld world, Vector3 position)
        {
            if (!TryGet(world, out WindSingleton wind)) return new WindSample { Velocity = Vector3.Zero, Gust = 0.5f };
            return WindField.Sample(wind, position.Xz);
        }

        /// <summary>The live mean wind on the xz plane in m/s, which gust patterns and fog drift with.</summary>
        public static Vector2 MeanVelocity(ECSWorld world)
        {
            return TryGet(world, out WindSingleton wind) ? wind.Velocity : Vector2.Zero;
        }

        /// <summary>
        /// Sets off a blast wave at <paramref name="centre"/>: a front of wind racing out to <paramref name="radius"/>
        /// metres at up to <paramref name="strength"/> m/s, dying away over <paramref name="duration"/> seconds.
        /// </summary>
        public static void AddBlast(ECSWorld world, Vector3 centre, float radius, float strength, float duration = 1.5f)
        {
            if (radius <= 0f || duration <= 0f || !TryGet(world, out WindSingleton wind) || wind.Blasts == null) return;
            wind.Blasts.Add(new WindEmitter
            {
                Type = WindEmitterType.Blast,
                Position = centre,
                Radius = radius,
                Strength = strength,
                Age = 0f,
                Duration = duration,
            });
        }
    }
}
