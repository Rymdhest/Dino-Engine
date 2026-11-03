using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Keeps characters on ground they can stand on. It takes the velocity they ask for and cuts it down to what the world allows:
    /// solid things push them out, so they slide around trees and along walls, and where the ground is too steep or the water
    /// too deep they stop or slide along the edge. It reads the same footprints and maps the world was built from, so what
    /// looks solid is exactly what is solid.
    /// </summary>
    public class CharacterMotorSystem : SystemBase
    {
        private const int PushPasses = 3;
        private const float SteepestRun = 0.5f;     // Slope at which climbing starts to slow a character down.
        private const float WadeSlowdown = 0.45f;   // Speed left in water up to the waist.

        private readonly List<Footprint> _solids = new();

        public CharacterMotorSystem()
            : base(new BitMask(typeof(CharacterMotorComponent), typeof(PositionComponent), typeof(VelocityComponent)))
        {
        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            if (deltaTime <= 0f || !world.TryGetSingleton<WorldStreamSingleton>(out Entity stream)) return;
            _world = world.GetComponent<WorldStreamSingleton>(stream).World;
            base.UpdateInternal(world, deltaTime);
        }

        private WorldData _world = null!;

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var motor = entity.Get<CharacterMotorComponent>();
            var velocity = entity.Get<VelocityComponent>();
            Vector3 position = entity.Get<PositionComponent>().value;
            Vector2 from = position.Xz;

            motor.WaterDepth = _world.WaterDepthAt(from);
            Vector2 wanted = velocity.value.Xz;
            if (wanted.LengthSquared < 1e-6f)
            {
                entity.Set(motor);
                return;
            }

            wanted *= SpeedFactor(from, wanted, motor.WaterDepth);
            Vector2 next = Slide(from + wanted * deltaTime, motor.Radius);

            if (!_world.Walkable.IsSet(next))
            {
                // Try each axis on its own, so that walking at an edge slides along it rather than stopping dead.
                Vector2 alongX = Slide(from + new Vector2(wanted.X, 0f) * deltaTime, motor.Radius);
                Vector2 alongZ = Slide(from + new Vector2(0f, wanted.Y) * deltaTime, motor.Radius);
                if (_world.Walkable.IsSet(alongX)) next = alongX;
                else if (_world.Walkable.IsSet(alongZ)) next = alongZ;
                else next = from;
            }

            Vector2 moved = (next - from) / deltaTime;
            velocity.value = new Vector3(moved.X, 0f, moved.Y);
            entity.Set(velocity);
            entity.Set(motor);
        }

        private float SpeedFactor(Vector2 from, Vector2 wanted, float waterDepth)
        {
            float speed = wanted.Length;
            Vector2 direction = wanted / speed;
            float rise = (_world.Height.Sample(from + direction) - _world.Height.Sample(from));

            float factor = 1f;
            if (rise > 0f) factor *= 1f - 0.55f * MathF.Min(rise / SteepestRun, 1f);
            if (waterDepth > 0.15f) factor *= 1f - (1f - WadeSlowdown) * MathF.Min(waterDepth / WorldData.WadingDepth, 1f);
            return factor;
        }

        private Vector2 Slide(Vector2 position, float radius)
        {
            _solids.Clear();
            _world.Placement.CollectSolids(position, radius + 2f, _solids);

            for (int pass = 0; pass < PushPasses; pass++)
            {
                bool touching = false;
                for (int i = 0; i < _solids.Count; i++)
                {
                    if (!_solids[i].TryPushOut(position, radius, out Vector2 push)) continue;
                    position += push;
                    touching = true;
                }
                if (!touching) break;
            }
            return position;
        }
    }
}
