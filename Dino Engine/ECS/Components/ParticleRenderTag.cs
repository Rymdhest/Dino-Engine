using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct ParticleRenderTag : IComponent
    {
        public bool Lit;    // Seen by the light of day, as smoke or snow are, and not glowing by itself as a flame does.
    }
}
