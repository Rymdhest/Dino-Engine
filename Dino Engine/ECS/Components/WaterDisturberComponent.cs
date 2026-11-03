using Dino_Engine.ECS.ECS_Architecture;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// Makes an entity stir the water it wades through (needs a PositionComponent): it leaves a wake of ripples while
    /// it moves and a ring when it steps in. Only acts while its body crosses a water surface. For one-off impacts use
    /// <see cref="Physics.Water.AddSplash"/>.
    /// </summary>
    public struct WaterDisturberComponent : IComponent
    {
        public float Radius;     // Metres, the footprint that pushes the water.
        public float Strength;   // Push per m/s of movement, per second: the wake is about Strength x 2 Radius metres deep at any speed.
        public float Height;     // Metres from its position up that its body reaches (it only disturbs a surface in that range).

        public WaterDisturberComponent(float radius, float strength = 0.06f, float height = 1.8f)
        {
            Radius = radius;
            Strength = strength;
            Height = height;
        }
    }
}
