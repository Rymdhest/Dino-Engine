using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.World;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Turns the day (<see cref="DaySingleton"/>): works out the moment from its clock and points the sun along it, before anything casts a shadow or reads the sky.
    /// It writes the sun's direction and nothing else: its light and the sky's are read through <see cref="SkySYstem"/>.
    /// </summary>
    public class DaySystem : SystemBase
    {
        private const float MaxStep = 0.1f;

        public DaySystem()
            : base(new BitMask(typeof(DaySingleton)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var day = entity.Get<DaySingleton>();
            if (day.Cycle == null) return;

            if (!day.ClockGiven) day.Clock += Math.Clamp(deltaTime, 0f, MaxStep);
            day.Now = day.Cycle.At(day.Clock);
            entity.Set(day);

            if (world.sun.IsValid() && world.HasComponent<DirectionNormalizedComponent>(world.sun))
            {
                world.GetEntityView(world.sun).Set(new DirectionNormalizedComponent(day.Now.Light));
            }
        }

        /// <summary>The moment of the world's day, if it has one.</summary>
        public static bool TryNow(ECSWorld world, out DaySample now)
        {
            now = default;
            if (!world.TryGetSingleton<DaySingleton>(out Entity entity)) return false;
            DaySingleton day = world.GetComponent<DaySingleton>(entity);
            if (!day.Live) return false;
            now = day.Now;
            return true;
        }
    }
}
