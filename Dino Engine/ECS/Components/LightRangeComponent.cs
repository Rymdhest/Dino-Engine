using Dino_Engine.ECS.ECS_Architecture;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// How far a point light or spotlight reaches. Its <see cref="ColorComponent"/> intensity is the light it gives at one
    /// metre; from there it falls off with the square of the distance, to nothing at <see cref="Radius"/> (lightFalloff in
    /// Shaders/Lighting/lightingCalc.glsl).
    /// </summary>
    public struct LightRangeComponent : IComponent
    {
        public float Radius;
        public float Fill;      // Share of the light that reaches a surface however it faces, for what bounces around nearby.
        public bool LightsFog;  // Whether the fog takes it up: it has few slots, so it's for the lights that are meant to be seen.

        public LightRangeComponent(float radius, float fill = 0f, bool lightsFog = true)
        {
            Radius = radius;
            Fill = fill;
            LightsFog = lightsFog;
        }
    }
}
