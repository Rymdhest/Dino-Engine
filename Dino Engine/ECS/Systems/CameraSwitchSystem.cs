using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Engine.ECS.Systems
{
    // Toggles world.Camera between the game camera and the debug free camera.
    // The free camera starts where the game camera is, looking the same way.
    public class CameraSwitchSystem : SystemBase
    {
        public const Keys ToggleKey = Keys.F2;

        public CameraSwitchSystem()
            : base(new BitMask(typeof(MainCameraTag)))
        {
        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            if (Engine.Gui.WantsKeyboard || !Engine.WindowHandler.IsKeyPressed(ToggleKey)) return;
            if (!world.GameCamera.IsValid() || !world.FreeCamera.IsValid()) return;

            if (world.IsFreeCameraActive)
            {
                world.Camera = world.GameCamera;
            }
            else
            {
                MoveFreeCameraToGameCamera(world);
                world.Camera = world.FreeCamera;
            }

            // Free-cam mouse look grabs the cursor and only releases it while it's the active camera
            Engine.WindowHandler.setMouseGrabbed(false);
        }

        private static void MoveFreeCameraToGameCamera(ECSWorld world)
        {
            Matrix4 gameCameraToWorld = world.GetComponent<LocalToWorldMatrixComponent>(world.GameCamera).value;
            Matrix4 invView = Matrix4.Invert(world.GetComponent<ViewMatrixComponent>(world.GameCamera).value);
            Vector3 forward = (new Vector4(0f, 0f, -1f, 0f) * invView).Xyz.Normalized();

            var control = new PositionRotationInputControlComponent(
                yaw: MathF.Atan2(-forward.X, -forward.Z),
                pitch: MathF.Asin(Math.Clamp(forward.Y, -1f, 1f)));
            control.Pitch = Math.Clamp(control.Pitch, -PositionRotationInputControlSystem.MaxPitch, PositionRotationInputControlSystem.MaxPitch);

            var freeCamera = world.GetEntityView(world.FreeCamera);
            freeCamera.Set(control);
            freeCamera.Set(new PositionComponent(gameCameraToWorld.ExtractTranslation()));
            freeCamera.Set(new RotationComponent(PositionRotationInputControlSystem.CreateViewRotation(control.Yaw, control.Pitch)));
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
        }
    }
}
