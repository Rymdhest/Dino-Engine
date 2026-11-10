using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Particles.Content
{
    /// <summary>
    /// Lightning: arcs, each a jagged line built by midpoint displacement (<see cref="Arcs"/>) whose finest jags the shader draws and redraws many times a second, a white core in
    /// a glow of its colour; sparks that spit off and bounce; and a hot white flash where it lands. Arcs crawl over someone shocked and jump between points of charged ground.
    /// </summary>
    [ParticleCollection]
    public static class Lightning
    {
        private static FloatRange R(float min, float max) => new FloatRange(min, max);

        /// <summary>One piece of an arc.</summary>
        public static readonly ParticleStyle Arc = new()
        {
            Sprite = ParticleSprite.Arc, Facing = ParticleFacing.Segment, Palette = ParticlePalette.Lightning,
            Opacity = new Curve((0f, 1f), (0.7f, 1f), (1f, 0f)),
            Heat = 0.8f, Cover = 0.25f, Flicker = 0.35f, Softness = 0f,
        };

        /// <summary>A spark spat off an arc.</summary>
        public static readonly ParticleStyle Spark = new()
        {
            Sprite = ParticleSprite.Streak, Facing = ParticleFacing.Stretched, Stretch = 0.03f, Palette = ParticlePalette.Lightning,
            Heat = Curve.Line(0.95f, 0.6f),
            Opacity = new Curve((0f, 1f), (0.6f, 1f), (1f, 0f)),
            Gravity = 6.4f, Drag = 1f, Contact = ParticleContact.Bounce, Bounce = 0.4f, Friction = 0.6f, Softness = 0f,
        };

        /// <summary>Someone shocked: arcs crawling over them, and sparks.</summary>
        public static readonly ParticleEffect Shocked = new()
        {
            Reach = 4f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Arc, Rate = 5f, Per = RateBasis.Metre, Place = EmitterPlace.Surface, ArcReach = R(0.8f, 2f), Size = R(0.11f, 0.176f), Life = R(0.08f, 0.16f) },
                new ParticleEmitter { Style = Spark, Rate = 4f, Per = RateBasis.Metre, Place = EmitterPlace.Surface, Size = R(0.038f, 0.053f), Life = R(0.2f, 0.4f), Outward = 1.8f, Speed = R(0.75f, 2.25f), Spread = 0.8f },
            },
        };

        /// <summary>Charged ground, all over an area: arcs jumping between points of it, and sparks.</summary>
        public static readonly ParticleEffect Ground = new()
        {
            Reach = 4f,
            Light = new EffectLight { Colour = new Vector3(0.6f, 0.68f, 1f), Intensity = 120f, Radius = 14.4f, Height = 2.2f, Flicker = 0.7f, FlickerSpeed = 25f, Fill = 0.25f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Arc, Rate = 0.167f, Per = RateBasis.SquareMetre, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 0.33f, 0f), ArcReach = R(2.4f, 5.2f), Size = R(0.132f, 0.22f), Life = R(0.08f, 0.18f) },
                new ParticleEmitter { Style = Spark, Rate = 0.167f, Per = RateBasis.SquareMetre, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 0.44f, 0f), Size = R(0.038f, 0.053f), Life = R(0.25f, 0.45f), Speed = R(2.25f, 6f), Spread = 0.9f },
            },
        };

        /// <summary>Lightning landing: arcs thrown out from where it struck, a white-hot flash, sparks and a scorch, all over its EffectShape.Ball.</summary>
        public static readonly ParticleEffect Burst = new()
        {
            Priority = ParticlePriority.Vital, Reach = 6f,
            Light = new EffectLight { Colour = new Vector3(0.7f, 0.76f, 1f), Intensity = 820f, Radius = 22.4f, Height = 3.3f, Flicker = 0.5f, FlickerSpeed = 40f, Length = 0.4f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Arc, Burst = 6, Spreads = true, Offset = new Vector3(0f, 1.32f, 0f), ArcReach = R(1.8f, 3.6f), Size = R(0.154f, 0.242f), Life = R(0.15f, 0.3f) },
                new ParticleEmitter { Style = Glows.Core(ParticlePalette.Lightning), Burst = 2, Offset = new Vector3(0f, 1.76f, 0f), Size = R(3.08f, 3.96f), Life = R(0.12f, 0.18f) },
                new ParticleEmitter { Style = Spark, Burst = 24, Spreads = true, Offset = new Vector3(0f, 1.1f, 0f), Size = R(0.038f, 0.06f), Life = R(0.3f, 0.6f), Speed = R(6f, 13.5f), Spread = 1.2f },
                new ParticleEmitter { Style = Fire.Scorch, Burst = 1, Fills = true, Place = EmitterPlace.Ground, Size = R(0.7f, 0.9f), Life = R(5f, 7f), Strength = 0.7f },
            },
        };

        /// <summary>A blow of lightning landing, or the charge in a caster's hand: a few short arcs, a spit of sparks and a flash.</summary>
        public static readonly ParticleEffect Hit = new()
        {
            Priority = ParticlePriority.Vital, Reach = 4f,
            Light = new EffectLight { Colour = new Vector3(0.7f, 0.76f, 1f), Intensity = 120f, Radius = 8f, Height = 0.5f, Flicker = 0.5f, FlickerSpeed = 40f, Length = 0.2f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Arc, Burst = 3, ArcReach = R(1f, 2.2f), Size = R(0.088f, 0.154f), Life = R(0.08f, 0.16f) },
                new ParticleEmitter { Style = Spark, Burst = 9, Size = R(0.03f, 0.045f), Life = R(0.2f, 0.4f), Speed = R(3f, 7.5f), Spread = 1.3f },
            },
        };

        /// <summary>What a bolt of lightning trails as it flies: a bright head, arcs crackling round it, sparks, and its light flickering on the ground under it.</summary>
        public static readonly ParticleEffect Bolt = new()
        {
            Priority = ParticlePriority.Vital, Reach = 4f,
            Light = new EffectLight { Colour = new Vector3(0.65f, 0.72f, 1f), Intensity = 130f, Radius = 10f, Flicker = 0.6f, FlickerSpeed = 30f, Fill = 0.25f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Glows.Core(ParticlePalette.Lightning), Rate = 30f, Size = R(0.88f, 1.21f), Life = R(0.06f, 0.1f) },
                new ParticleEmitter { Style = Arc, Rate = 12f, ArcReach = R(0.8f, 1.8f), Size = R(0.088f, 0.132f), Life = R(0.06f, 0.12f) },
                new ParticleEmitter { Style = Spark, Rate = 10f, Size = R(0.03f, 0.045f), Life = R(0.2f, 0.4f), Speed = R(1.5f, 4.5f), Spread = MathF.PI },
            },
        };
    }
}
