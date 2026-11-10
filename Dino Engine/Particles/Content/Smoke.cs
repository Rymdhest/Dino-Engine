using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Particles.Content
{
    /// <summary>Smoke and dust with no fire under them: a chimney's, and what a blow or a fall throws up off the ground.</summary>
    [ParticleCollection]
    public static class Smoke
    {
        private static FloatRange R(float min, float max) => new FloatRange(min, max);

        /// <summary>Pale smoke from a hearth inside, rising slowly and leaning off with the wind.</summary>
        public static readonly ParticleStyle Hearthsmoke = new()
        {
            Sprite = ParticleSprite.Smoke, Lit = true,
            Colour = new Gradient((0f, new Vector3(0.5f, 0.49f, 0.48f)), (1f, new Vector3(0.66f, 0.65f, 0.64f))),
            Size = Curve.Line(1f, 3.6f),
            Opacity = new Curve((0f, 0f), (0.1f, 0.55f), (0.55f, 0.38f), (1f, 0f)),
            Erosion = new Curve((0f, 0.05f), (0.5f, 0.3f), (1f, 0.75f)),
            Drag = 0.8f, Buoyancy = 1.5f, BuoyancyFade = 3f, Turbulence = 0.45f, TurbulenceScale = 8.8f, SpinDrag = 0.05f,
            Softness = 0.6f,
        };

        /// <summary>Dust thrown up off the ground, settling back as it spreads.</summary>
        public static readonly ParticleStyle Dust = new()
        {
            Sprite = ParticleSprite.Smoke, Lit = true,
            Colour = new Vector3(0.55f, 0.48f, 0.38f),
            Size = new Curve((0f, 0.6f), (0.25f, 1.2f), (1f, 2f)),
            Opacity = new Curve((0f, 0.65f), (0.4f, 0.45f), (1f, 0f)),
            Erosion = new Curve((0f, 0.2f), (1f, 0.7f)),
            Drag = 3f, Gravity = 0.48f, Turbulence = 0.6f, TurbulenceScale = 3.3f, SpinDrag = 0.5f,
            Softness = 0.4f,
        };

        /// <summary>Steam: white, billowing up fast and gone quickly.</summary>
        public static readonly ParticleStyle Steam = new()
        {
            Sprite = ParticleSprite.Smoke, Lit = true,
            Colour = new Vector3(0.9f, 0.92f, 0.95f),
            Size = new Curve((0f, 0.6f), (0.3f, 1.4f), (1f, 2.4f)),
            Opacity = new Curve((0f, 0.7f), (0.4f, 0.45f), (1f, 0f)),
            Erosion = new Curve((0f, 0.15f), (0.5f, 0.4f), (1f, 0.8f)),
            Drag = 2f, Buoyancy = 4.5f, BuoyancyFade = 1f, Turbulence = 0.75f, TurbulenceScale = 3.3f, SpinDrag = 0.5f,
            Softness = 0.4f,
        };

        /// <summary>A gout of steam bursting off something: fire meeting cold.</summary>
        public static readonly ParticleEffect Scald = new()
        {
            Priority = ParticlePriority.Vital, Reach = 8f,
            Light = new EffectLight { Colour = new Vector3(1f, 0.82f, 0.62f), Intensity = 270f, Radius = 16f, Height = 2.64f, Flicker = 0f, Length = 0.35f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Steam, Burst = 6, Offset = new Vector3(0f, 1.76f, 0f), Size = R(1.32f, 2.2f), Life = R(1.2f, 1.8f), Outward = 3.75f, Speed = R(1.5f, 3.75f), Spread = 0.6f, Spin = R(0.2f, 0.6f) },
            },
        };

        /// <summary>Smoke from a chimney, for as long as someone keeps the fire in.</summary>
        public static readonly ParticleEffect Chimney = new()
        {
            Priority = ParticlePriority.Ambient, Reach = 32f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Hearthsmoke, Rate = 1.1f, Life = R(8f, 12f), Size = R(1.32f, 1.98f), Speed = R(0.9f, 1.65f), Spread = 0.2f, Spin = R(0.05f, 0.2f) },
            },
        };

        /// <summary>A puff of dust off the ground, as something heavy lands or strikes it. Scale 1 is a hard blow.</summary>
        public static readonly ParticleEffect Puff = new()
        {
            Reach = 6f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Dust, Burst = 6, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 0.66f, 0f), Size = R(1.32f, 2.2f), Life = R(1.2f, 1.8f), Outward = 3.75f, Speed = R(0.45f, 1.2f), Spread = 0.4f, Spin = R(0.2f, 0.6f) },
            },
        };
    }
}
