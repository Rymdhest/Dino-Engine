using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>Moves cameras with a <see cref="FollowCameraComponent"/> after their targets; the mouse wheel zooms.</summary>
    public class FollowCameraSystem : SystemBase
    {
        private const float JoltFrequency = 6f;
        private const float JoltDamping = 0.55f;

        public FollowCameraSystem()
            : base(new BitMask(typeof(FollowCameraComponent), typeof(PositionComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var camera = entity.Get<FollowCameraComponent>();
            if (!camera.Target.IsValid()) return;

            Vector3 target = world.GetComponent<PositionComponent>(camera.Target).value;
            if (!camera.Initialized)
            {
                camera.Focus = target;
                camera.Initialized = true;
            }

            float scroll = Engine.Gui.WantsMouse || world.IsFreeCameraActive ? 0f : Engine.WindowHandler.MouseState.ScrollDelta.Y;
            camera.Zoom = Math.Clamp(camera.Zoom * MathF.Pow(0.92f, scroll), camera.MinZoom, camera.MaxZoom);

            // Sideways the camera stays close, or fast movement would leave the character behind. Height is what bumps in
            // the ground would shake, so it follows much more slowly.
            float side = 1f - MathF.Exp(-camera.FollowSpeed * deltaTime);
            float up = 1f - MathF.Exp(-camera.FollowSpeed * 0.3f * deltaTime);
            camera.Focus = new Vector3(
                camera.Focus.X + (target.X - camera.Focus.X) * side,
                camera.Focus.Y + (target.Y - camera.Focus.Y) * up,
                camera.Focus.Z + (target.Z - camera.Focus.Z) * side);

            // A jolt is gone in a moment, and it moves the whole view, so that nothing's hidden and nothing's blurred by it.
            Dino_Engine.Animation.Spring.Ring(ref camera.Jolt, ref camera.JoltSpeed, JoltFrequency, JoltDamping, deltaTime);

            entity.Set(new PositionComponent(camera.Focus + camera.Offset * camera.Zoom + camera.Jolt));
            entity.Set(camera);
        }
    }
}
