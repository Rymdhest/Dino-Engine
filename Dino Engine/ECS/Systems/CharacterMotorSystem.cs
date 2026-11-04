using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Walks characters over the generated world: takes the velocity each one asks for and leaves it with what
    /// <see cref="CharacterMover"/> allows.
    /// </summary>
    public class CharacterMotorSystem : SystemBase
    {
        private CharacterMover? _mover;

        public CharacterMotorSystem()
            : base(new BitMask(typeof(CharacterMotorComponent), typeof(PositionComponent), typeof(VelocityComponent)))
        {
        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            if (deltaTime <= 0f || !world.TryGetSingleton<WorldDataSingleton>(out Entity data)) return;
            WorldData current = world.GetComponent<WorldDataSingleton>(data).World;
            if (_mover?.World != current) _mover = new CharacterMover(current);
            base.UpdateInternal(world, deltaTime);
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var motor = entity.Get<CharacterMotorComponent>();
            var velocity = entity.Get<VelocityComponent>();
            Vector2 from = entity.Get<PositionComponent>().value.Xz;

            motor.WaterDepth = _mover!.World.WaterDepthAt(from);
            entity.Set(motor);

            Vector2 wanted = velocity.value.Xz;
            if (wanted.LengthSquared < 1e-6f) return;

            Vector2 moved = (_mover.Move(from, wanted, motor.Radius, deltaTime) - from) / deltaTime;
            velocity.value = new Vector3(moved.X, 0f, moved.Y);
            entity.Set(velocity);
        }
    }
}
