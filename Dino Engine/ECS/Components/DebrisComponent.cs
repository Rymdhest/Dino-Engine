using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    /// <summary>A piece of something broken, flying or lying where it fell (<see cref="Systems.BreakageSystem"/>).</summary>
    public struct DebrisComponent : IComponent
    {
        public Vector3 Velocity;
        public Vector3 Spin;        // Radians a second about this axis, in world space.
        public Vector3 Half;        // Half its size along its own axes, scaled: what touches the ground first.
        public float Age;
    }
}
