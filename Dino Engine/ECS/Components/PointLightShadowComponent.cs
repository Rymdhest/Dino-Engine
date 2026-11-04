using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Lighting;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// A point light that can cast shadows. There are far fewer shadow maps than such lights, so
    /// <see cref="Systems.PointLightShadowSystem"/> lends them to the ones near the hero and fades them in and out.
    /// </summary>
    public struct PointLightShadowComponent : IComponent
    {
        public Shadow? Shadow;  // The map on loan to this light, if any.
        public float Strength;  // 0 no shadow, 1 full shadow.
    }
}
