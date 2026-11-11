using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Particles.Content
{
    /// <summary>
    /// Snow thrown up by feet (SnowTrailSystem): a low puff of powder that sinks back down, clumps flung ahead the way the foot was going, and the odd crystal catching
    /// the light. All of it is lit, white as the snow it came from, low and gone in a second, so a crowd running through a snowfield can't hide the ground. Read SNOW.md.
    /// </summary>
    [ParticleCollection]
    public static class Powder
    {
        private static FloatRange R(float min, float max) => new FloatRange(min, max);

        private static readonly Vector3 Ahead = Vector3.Normalize(new Vector3(0f, 0.7f, 1f));
        private static readonly Vector3 Up = Vector3.Normalize(new Vector3(0f, 1.6f, 1f));

        /// <summary>Powder: a thin, bright puff that spreads and sinks, as fine snow does.</summary>
        public static readonly ParticleStyle Dust = new()
        {
            Sprite = ParticleSprite.Smoke, Lit = true,
            Colour = new Vector3(0.66f, 0.7f, 0.76f),
            Size = new Curve((0f, 0.45f), (0.3f, 1f), (1f, 1.45f)),
            Opacity = new Curve((0f, 0f), (0.08f, 0.34f), (0.45f, 0.16f), (1f, 0f)),
            Erosion = new Curve((0f, 0.3f), (0.5f, 0.5f), (1f, 0.85f)),
            Drag = 3.2f, Gravity = 0.9f, Turbulence = 0.3f, TurbulenceScale = 1.6f, Wind = 0.8f, SpinDrag = 0.5f,
            Softness = 0.35f,
        };

        /// <summary>A clump of snow thrown out, falling, and gone into the snow where it lands.</summary>
        public static readonly ParticleStyle Clump = new()
        {
            Sprite = ParticleSprite.Glow, Lit = true,
            Colour = new Vector3(0.9f, 0.93f, 0.98f),
            Size = Curve.Line(1f, 0.75f),
            Opacity = new Curve((0f, 1f), (0.8f, 1f), (1f, 0f)),
            Gravity = 11.2f, Drag = 0.5f, Contact = ParticleContact.Vanish, Softness = 0f,
        };

        /// <summary>A crystal catching the light as it's thrown, and gone.</summary>
        public static readonly ParticleStyle Crystal = new()
        {
            Sprite = ParticleSprite.Glint, Palette = ParticlePalette.White,
            Colour = new Vector3(0.85f, 0.93f, 1f),
            Heat = 0.55f, Emission = 0.7f,
            Opacity = Curve.Fade(0.1f, 0.5f),
            Drag = 2.2f, Gravity = 1.5f, Turbulence = 0.3f, Softness = 0f,
        };

        /// <summary>A foot coming down in snow at a walk: a little puff and a few clumps, thrown the way it was going (the run's Forward).</summary>
        public static readonly ParticleEffect Kick = new()
        {
            Priority = ParticlePriority.Ambient, Reach = 2.5f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Dust, Burst = 1, Offset = new Vector3(0f, 0.12f, 0.1f), Size = R(0.5f, 0.75f), Life = R(0.7f, 1.0f), Direction = Ahead, Spread = 0.5f, Speed = R(0.4f, 1.1f), Outward = 0.4f, Spin = R(0.2f, 0.7f) },
                new ParticleEmitter { Style = Clump, Burst = 5, Offset = new Vector3(0f, 0.08f, 0.15f), Jitter = 0.12f, Size = R(0.045f, 0.08f), Life = R(0.3f, 0.5f), Direction = Up, Spread = 0.6f, Speed = R(1.8f, 3.6f) },
                new ParticleEmitter { Style = Crystal, Burst = 2, Offset = new Vector3(0f, 0.2f, 0.1f), Jitter = 0.2f, Size = R(0.08f, 0.13f), Life = R(0.25f, 0.55f), Speed = R(0.4f, 1.4f), Spread = MathF.PI },
            },
        };

        /// <summary>A foot coming down in snow at a run: powder sprayed ahead, more clumps thrown further, more crystals.</summary>
        public static readonly ParticleEffect Spray = new()
        {
            Priority = ParticlePriority.Ambient, Reach = 3.5f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Dust, Burst = 2, Offset = new Vector3(0f, 0.15f, 0.2f), Size = R(0.55f, 0.9f), Life = R(0.8f, 1.2f), Direction = Ahead, Spread = 0.45f, Speed = R(1.2f, 2.6f), Outward = 0.5f, Spin = R(0.2f, 0.8f) },
                new ParticleEmitter { Style = Clump, Burst = 9, Offset = new Vector3(0f, 0.1f, 0.2f), Jitter = 0.15f, Size = R(0.045f, 0.09f), Life = R(0.35f, 0.6f), Direction = Ahead, Spread = 0.55f, Speed = R(3f, 6f) },
                new ParticleEmitter { Style = Crystal, Burst = 4, Offset = new Vector3(0f, 0.25f, 0.2f), Jitter = 0.25f, Size = R(0.08f, 0.14f), Life = R(0.3f, 0.6f), Direction = Ahead, Speed = R(0.8f, 2.2f), Spread = 1.4f },
            },
        };

        /// <summary>Something huge setting a foot down in snow: a ring of powder bursting out low all round, and clumps thrown everywhere. Scaled to the foot.</summary>
        public static readonly ParticleEffect Stomp = new()
        {
            Priority = ParticlePriority.Ambient, Reach = 5f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Dust, Burst = 6, Place = EmitterPlace.Centre, Offset = new Vector3(0f, 0.15f, 0f), Size = R(0.6f, 0.95f), Life = R(1.0f, 1.5f), Direction = Vector3.UnitY, Spread = 0.3f, Speed = R(0.1f, 0.4f), Outward = 2.2f, Spin = R(0.1f, 0.5f) },
                new ParticleEmitter { Style = Clump, Burst = 12, Offset = new Vector3(0f, 0.1f, 0f), Jitter = 0.3f, Size = R(0.05f, 0.09f), Life = R(0.4f, 0.7f), Direction = Vector3.UnitY, Spread = 0.9f, Speed = R(2f, 4.5f) },
                new ParticleEmitter { Style = Crystal, Burst = 5, Offset = new Vector3(0f, 0.3f, 0f), Jitter = 0.4f, Size = R(0.09f, 0.15f), Life = R(0.3f, 0.7f), Speed = R(0.5f, 1.8f), Spread = MathF.PI },
            },
        };
    }
}
