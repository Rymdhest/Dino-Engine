using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Particles.Content
{
    /// <summary>What the Ossuary calls up in light (SaurianSpectral, LORE.md): wisps of a ghost's cold blue coming off it and rising away, glints on it, and the light it gives the ground round it.</summary>
    [ParticleCollection]
    public static class Haunts
    {
        private static FloatRange R(float min, float max) => new FloatRange(min, max);

        /// <summary>A ghost going about: a thin trail of wisps off all of it and the odd glint, and the cold light round it, slow to change, as a ghost's is.</summary>
        public static readonly ParticleEffect Ghost = new()
        {
            Reach = 5f,
            Light = new EffectLight { Colour = new Vector3(0.32f, 0.7f, 1f), Intensity = 55f, Radius = 10f, Height = 2.4f, Flicker = 0.12f, FlickerSpeed = 1.2f, Fill = 0.3f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Glows.Wisp(ParticlePalette.Frost), Rate = 0.9f, Per = RateBasis.Metre, Place = EmitterPlace.Within, Size = R(0.3f, 0.5f), Life = R(1.2f, 2.2f) },
                new ParticleEmitter { Style = Glows.Glint(ParticlePalette.Frost), Rate = 0.7f, Per = RateBasis.Metre, Place = EmitterPlace.Surface, Size = R(0.16f, 0.26f), Life = R(0.6f, 1.1f) },
            },
        };
    }
}
