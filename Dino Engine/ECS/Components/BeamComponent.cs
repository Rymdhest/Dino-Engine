using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Util;

namespace Dino_Engine.ECS.Components
{
    /// <summary>A pillar of light standing on something. With no height there's nothing to draw.</summary>
    public struct BeamComponent : IComponent
    {
        public float Height;
        public float Width;
        public Colour Colour;   // Keep it deep and saturated: it's added to a scene lit by a sun of about 13, and anything pale goes white.
    }
}
