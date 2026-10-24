using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Engine.ECS.Systems
{
    public class PlayerInputSystem : SystemBase
    {

        public PlayerInputSystem()
            : base(new BitMask(typeof(PlayerTag), typeof(VelocityComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            Vector3 moveDirection = Vector3.Zero;
            float _moveSpeed = 10f;
            var windowhandler = Engine.WindowHandler;


            if (windowhandler.IsKeyDown(Keys.W)) moveDirection.Z += 1.0f; // Assuming Z is forward/back
            if (windowhandler.IsKeyDown(Keys.S)) moveDirection.Z -= 1.0f;
            if (windowhandler.IsKeyDown(Keys.A)) moveDirection.X += 1.0f;
            if (windowhandler.IsKeyDown(Keys.D)) moveDirection.X -= 1.0f;

            var velocity = entity.Get<VelocityComponent>();

            if (moveDirection.LengthSquared > 0.0f)
            {
                moveDirection = Vector3.Normalize(moveDirection);

                float targetAngle = MathF.Atan2(velocity.value.X, velocity.value.Z);

                var rotation = entity.Get<RotationComponent>();
                rotation.quaternion = Quaternion.FromAxisAngle(Vector3.UnitY, targetAngle);
                entity.Set(rotation);

            }

            // Update the velocity component
            velocity.value = moveDirection * _moveSpeed;
            entity.Set(velocity);

        }
    }
}