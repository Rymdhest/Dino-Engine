using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Engine.ECS.Systems
{
    public class PositionRotationInputControlSystem : SystemBase
    {
        public const float MaxPitch = MathF.PI / 2f - 0.01f;

        // The camera rotation (world -> view) for a yaw/pitch pair
        public static Quaternion CreateViewRotation(float yaw, float pitch)
        {
            Quaternion yawRotation = Quaternion.FromAxisAngle(Vector3.UnitY, -yaw);
            Quaternion pitchRotation = Quaternion.FromAxisAngle(Vector3.UnitX, -pitch);
            return pitchRotation * yawRotation;
        }

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
            // Only the active camera takes input, so an inactive free camera stays put
            if (entity.Entity.Id != world.Camera.Id) return;

            float moveSpeed = 7.0f;
            float turnSpeed = 0.001f;
            var windowhandler = Engine.WindowHandler;
            // While the cursor is grabbed for mouse look the GUI ignores the mouse, so WantsMouse only
            // blocks starting a look over the GUI; it never interrupts one in progress.
            bool keyboardFree = !Engine.Gui.WantsKeyboard;
            bool mouseFree = !Engine.Gui.WantsMouse;
            if (windowhandler.IsKeyDown(Keys.LeftShift)) moveSpeed *= 10f;
            if (windowhandler.IsKeyDown(Keys.LeftControl)) moveSpeed *= 0.1f;

            if (mouseFree && windowhandler.IsMouseButtonDown(MouseButton.Right))
            {
                windowhandler.setMouseGrabbed(true);

                var pitchYaw = entity.Get<PositionRotationInputControlComponent>();
                var Position = entity.Get<LocalToWorldMatrixComponent>().value.ExtractTranslation();
                //Console.WriteLine(windowhandler.MouseState.Delta.X +" : "+deltaTime);
                pitchYaw.Pitch -= windowhandler.MouseState.Delta.Y * turnSpeed;
                pitchYaw.Yaw -= windowhandler.MouseState.Delta.X * turnSpeed;
                pitchYaw.Pitch = Math.Clamp(pitchYaw.Pitch, -MaxPitch, MaxPitch);

                var rotation = entity.Get<RotationComponent>();
                rotation.quaternion = CreateViewRotation(pitchYaw.Yaw, pitchYaw.Pitch);

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
            if (keyboardFree && windowhandler.IsKeyDown(Keys.W)) movement -= Vector3.UnitZ;   // forward
            if (keyboardFree && windowhandler.IsKeyDown(Keys.S)) movement += Vector3.UnitZ;   // backward
            if (keyboardFree && windowhandler.IsKeyDown(Keys.A)) movement -= Vector3.UnitX;   // strafe left
            if (keyboardFree && windowhandler.IsKeyDown(Keys.D)) movement += Vector3.UnitX;   // strafe right
            if (movement != Vector3.Zero)
            {
                movement = Vector3.Normalize(movement);

                movement = Vector3.Transform(movement, currentFinalRotation) * moveSpeed * deltaTime;
            }

            if (keyboardFree && windowhandler.IsKeyDown(Keys.KeyPadAdd))
            {
                DirectionNormalizedComponent dir = world.GetComponent<DirectionNormalizedComponent>(world.sun);
                dir.value = dir.value + new Vector3(0.0f, -0.02f, 0.0f);
                world.GetEntityView(world.sun).Set(dir); 
            }
            if (keyboardFree && windowhandler.IsKeyDown(Keys.KeyPadSubtract))
            {
                DirectionNormalizedComponent dir = world.GetComponent<DirectionNormalizedComponent>(world.sun);
                dir.value = dir.value + new Vector3(0.0f, 0.02f, 0.0f);
                world.GetEntityView(world.sun).Set(dir);
            }

            if (keyboardFree && windowhandler.IsKeyDown(Keys.Q)) movement.Y -= moveSpeed*deltaTime;   // strafe left
            if (keyboardFree && windowhandler.IsKeyDown(Keys.E)) movement.Y += moveSpeed*deltaTime;   // strafe right
            if (movement != Vector3.Zero)
            {
                var position = entity.Get<PositionComponent>();
                position.value += movement;
                entity.Set(position);
            }

            if (keyboardFree && windowhandler.IsKeyPressed(Keys.V))
            {
                float speed = 6f;
                float mass = 1.0f;
                float duration = 10.0f;
                if (windowhandler.IsKeyDown(Keys.LeftControl))
                {
                    mass = 0.000001f;
                    speed = 0f;
                    duration = 30f;
                }

                //var colVector = MyMath.rng3D();
                var colVector = new Vector3(1f, 1f, 1f);
                if (colVector.Length < 1.0) colVector.Normalize();
                var col = new Colour(colVector);
                col.Intensity = 120.0f;
                Vector3 forward = currentFinalRotation * -Vector3.UnitZ;
                float FoV = MathF.PI / 3.0f;
                world.CreateEntity("Shooting ball",
                    new VelocityComponent(forward * speed),
                    new PositionComponent(entity.Get<LocalToWorldMatrixComponent>().value.ExtractTranslation() + forward * 1f),
                    new LocalToWorldMatrixComponent(),
                    new LightRangeComponent(25f),
                    new ScaleComponent(new Vector3(.1f)),
                    new ModelComponent(ModelGenerator.UNIT_SPHERE),
                    new ModelRenderTag(),
                    new DirectionNormalizedComponent(forward),
                    new SpotLightComponent(0.3f, FoV),
                    new SpotlightShadowComponent(new Vector2i(512), FoV),
                    new MassComponent(mass),
                    new RotationComponent(),
                    new ColorComponent(col),
                    new selfDestroyComponent(duration),
                    new ColliderComponent
                    {
                        Type = ColliderType.Sphere,
                        Data = new ColliderData { Radius = 2.0f }, // Match your ScaleComponent(0.15f)
                        Restitution = 0.2f // Bounciness factor (0 to 1)
                    }
                );
            }

            if (keyboardFree && windowhandler.IsKeyPressed(Keys.B))
            {
                for (int i = 0; i <1; i++) {

                    float speed = 10f;
                    float mass = 1.0f;
                    float duration = 20.0f;
                    if (windowhandler.IsKeyDown(Keys.LeftControl))
                    {
                        mass = 0.000001f;
                        speed = 0f;
                        duration = 30f;
                    }

                    //var colVector = MyMath.rng3D();
                    var colVector = new Vector3(1f, 1f, 1f);
                    if (colVector.Length < 1.0) colVector.Normalize();
                    var col = new Colour(colVector);
                    col.Intensity = 60.0f;
                    Vector3 forward = currentFinalRotation * -Vector3.UnitZ;
                    float FoV = MathF.PI / 3.0f;
                    world.CreateEntity("Shooting ball",
                        new VelocityComponent(forward * speed),
                        new PositionComponent(entity.Get<LocalToWorldMatrixComponent>().value.ExtractTranslation() + forward * 1f),
                        new LocalToWorldMatrixComponent(),
                        new RotationComponent(),
                        new LightRangeComponent(16f, 0.1f),
                        new ScaleComponent(new Vector3(0.05f)),
                        new ModelComponent(ModelGenerator.UNIT_SPHERE),
                        new ModelRenderTag(),
                        new DirectionNormalizedComponent(forward),
                        new PointLightTag(),
                        new PointLightShadowComponent(),
                        new MassComponent(mass),
                        new ColorComponent(col),
                        new ColliderComponent { Type = ColliderType.Sphere, Data = new ColliderData { Radius = 2.5f }, Restitution = 0.2f },
                        new selfDestroyComponent(duration)
                    );
                }

            }
        }

    }
}
