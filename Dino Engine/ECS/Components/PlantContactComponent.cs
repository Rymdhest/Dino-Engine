using Dino_Engine.ECS.ECS_Architecture;

namespace Dino_Engine.ECS.Components
{
    /// <summary>A body that goes through plants. If it has an animator, its feet leave their marks too (PlantContactSystem).</summary>
    public struct PlantContactComponent : IComponent
    {
        public float radius;    // Metres out from it that plants feel it.
        public float lean;      // Radians it pushes them aside at its middle.
        public float press;     // Radians it presses them flat at its middle.

        public PlantContactComponent(float radius)
        {
            this.radius = radius;
            lean = 1.45f;
            press = 0.7f;
        }
    }
}
