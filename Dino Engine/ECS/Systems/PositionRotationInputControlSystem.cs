using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Engine.ECS.Systems
{
    public class PositionRotationInputControlSystem : SystemBase
    {
        public PositionRotationInputControlSystem()
            : base(new BitMask(
                typeof(PositionComponent),
                typeof(RotationComponent),
                typeof(PositionRotationInputControlComponent)
            ))
        {   
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            float moveSpeed = 40.0f;
            float turnSpeed = 0.001f;
            var windowhandler = Engine.WindowHandler;
            if (windowhandler.IsKeyDown(Keys.LeftShift)) moveSpeed *= 7f;
            if (windowhandler.IsKeyDown(Keys.LeftControl)) moveSpeed *= 0.1f;

            if (windowhandler.IsMouseButtonDown(MouseButton.Right))
            {
                windowhandler.setMouseGrabbed(true);

                var pitchYaw = entity.Get<PositionRotationInputControlComponent>();
                pitchYaw.Pitch -= windowhandler.MouseState.Delta.Y * turnSpeed;
                pitchYaw.Yaw -= windowhandler.MouseState.Delta.X * turnSpeed;
                pitchYaw.Pitch = Math.Clamp(pitchYaw.Pitch, -MathF.PI / 2f + 0.01f, MathF.PI / 2f - 0.01f);

                Quaternion yawRotation = Quaternion.FromAxisAngle(Vector3.UnitY, -pitchYaw.Yaw);
                Quaternion pitchRotation = Quaternion.FromAxisAngle(Vector3.UnitX, -pitchYaw.Pitch);
                Quaternion finalRotation = pitchRotation * yawRotation;

                var rotation = entity.Get<RotationComponent>();
                rotation.quaternion = finalRotation; 
                entity.Set(pitchYaw);
                entity.Set(rotation);
            } else
            {
                windowhandler.setMouseGrabbed(false);
            }

            var pitchYawCurrent = entity.Get<PositionRotationInputControlComponent>();

            Quaternion currentYawRotation = Quaternion.FromAxisAngle(Vector3.UnitY, pitchYawCurrent.Yaw);
            Quaternion currentPitchRotation = Quaternion.FromAxisAngle(Vector3.UnitX, pitchYawCurrent.Pitch);
            Quaternion currentFinalRotation =  currentYawRotation * currentPitchRotation;

            Vector3 movement = Vector3.Zero;
            if (windowhandler.IsKeyDown(Keys.W)) movement -= Vector3.UnitZ;   // forward
            if (windowhandler.IsKeyDown(Keys.S)) movement += Vector3.UnitZ;   // backward
            if (windowhandler.IsKeyDown(Keys.A)) movement -= Vector3.UnitX;   // strafe left
            if (windowhandler.IsKeyDown(Keys.D)) movement += Vector3.UnitX;   // strafe right

            if (movement != Vector3.Zero)
            {
                movement = Vector3.Normalize(movement);

                movement = Vector3.Transform(movement, currentFinalRotation) * moveSpeed * deltaTime;
            }

            if (windowhandler.IsKeyDown(Keys.Q)) movement.Y -= moveSpeed*deltaTime;   // strafe left
            if (windowhandler.IsKeyDown(Keys.E)) movement.Y += moveSpeed*deltaTime;   // strafe right
            if (movement != Vector3.Zero)
            {
                var position = entity.Get<PositionComponent>();
                position.value += movement;
                entity.Set(position);
            }

        }

    }
}
