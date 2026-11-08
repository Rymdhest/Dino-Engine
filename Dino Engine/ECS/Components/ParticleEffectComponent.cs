using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Particles;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// An effect going on wherever this entity is (its LocalToWorldMatrixComponent), for as long as it has this: a torch, a chimney, someone burning, a patch of burning ground.
    /// <see cref="Systems.ParticleEffectSystem"/> runs it. Taking it away stops new particles, and what's already in the air goes on to the end of its life.
    /// </summary>
    public struct ParticleEffectComponent : IComponent
    {
        public EffectRun Run;

        public ParticleEffectComponent(ParticleEffect effect, EffectShape shape, float scale = 1f, bool prewarm = true)
        {
            Run = new EffectRun(effect) { Shape = shape, Scale = scale, Prewarm = prewarm };
        }
    }
}
