using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// Walks a character over the generated world. Whatever sets its <see cref="VelocityComponent"/> says where it wants to go;
    /// <see cref="Systems.CharacterMotorSystem"/> turns that into where it can go, around solid things and over walkable ground.
    /// </summary>
    public struct CharacterMotorComponent : IComponent
    {
        public float Radius;        // Metres from the character's centre to its side.
        public float WaterDepth;    // Metres of water it is standing in, set by the motor.

        public CharacterMotorComponent(float radius)
        {
            Radius = radius;
            WaterDepth = 0f;
        }
    }

    /// <summary>
    /// A camera that follows an entity from above and behind. It looks down at a fixed angle, eases after its target and
    /// smooths out the bumps of the ground, so the view doesn't jolt with every step.
    /// </summary>
    public struct FollowCameraComponent : IComponent
    {
        public Entity Target;
        public Vector3 Offset;      // From the target to the camera, at Zoom 1.
        public float Zoom;
        public float MinZoom;
        public float MaxZoom;
        public float FollowSpeed;   // Higher catches up sooner.
        public Vector3 Focus;       // The smoothed point the camera is placed from.
        public bool Initialized;

        /// <summary>How far a blow has knocked the view, and how fast. It rings and dies away by itself.</summary>
        public Vector3 Jolt;
        public Vector3 JoltSpeed;

        public FollowCameraComponent(Entity target, Vector3 offset)
        {
            Target = target;
            Offset = offset;
            Zoom = 1f;
            MinZoom = 0.55f;
            MaxZoom = 1.5f;
            FollowSpeed = 9f;
            Focus = Vector3.Zero;
            Initialized = false;
        }
    }
}
