using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Particles.Content
{
    /// <summary>What blows and landings throw up that isn't blood or magic: sparks off steel, a shockwave across the ground, and the light of something good coming down.</summary>
    [ParticleCollection]
    public static class Impact
    {
        private static FloatRange R(float min, float max) => new FloatRange(min, max);

        /// <summary>A chip of something hard knocked off, falling and skittering.</summary>
        public static readonly ParticleStyle Chip = new()
        {
            Sprite = ParticleSprite.Streak, Facing = ParticleFacing.Stretched, Stretch = 0.02f, Lit = true,
            Colour = new Vector3(0.78f, 0.75f, 0.68f),
            Opacity = new Curve((0f, 1f), (0.8f, 1f), (1f, 0f)),
            Gravity = 16f, Drag = 0.3f, Contact = ParticleContact.Bounce, Bounce = 0.3f, Friction = 0.5f, Softness = 0f,
        };

        /// <summary>Steel on steel: white-hot sparks along the effect's Forward, falling and bouncing as they cool to orange.</summary>
        public static readonly ParticleEffect Sparks = new()
        {
            Priority = ParticlePriority.Vital, Reach = 4f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Fire.Spark, Burst = 10, Direction = new Vector3(0f, 0.6f, 1f), Spread = 0.9f, Speed = R(4.5f, 10.5f), Size = R(0.03f, 0.045f), Life = R(0.15f, 0.45f) },
            },
        };

        /// <summary>A ring of dust thrown out across the ground by something striking it all round, to the edge of its EffectShape.Ball.</summary>
        public static readonly ParticleEffect Shockwave = new()
        {
            Priority = ParticlePriority.Vital, Reach = 4f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Glows.Ring(ParticlePalette.White), Burst = 1, Fills = true, Place = EmitterPlace.Ground, Size = R(1f, 1f), Life = R(0.4f, 0.4f), Tint = new Vector3(0.55f, 0.6f, 0.65f) },
                new ParticleEmitter { Style = Smoke.Dust, Burst = 16, Spreads = true, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 0.66f, 0f), Size = R(0.77f, 1.32f), Life = R(0.9f, 1.4f), Outward = 10.5f, Spin = R(0.2f, 0.6f) },
            },
        };

        /// <summary>Ground being churned up, all over an area: dust kicked up off it.</summary>
        public static readonly ParticleEffect Churn = new()
        {
            Reach = 4f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Smoke.Dust, Rate = 0.04f, Per = RateBasis.SquareMetre, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 0.44f, 0f), Size = R(1.1f, 1.98f), Life = R(1f, 1.6f), Speed = R(0.45f, 1.5f), Spread = 0.5f, Spin = R(0.2f, 0.6f) },
            },
        };

        /// <summary>Bone struck: a puff of pale dust and chips of it.</summary>
        public static readonly ParticleEffect Bone = new()
        {
            Priority = ParticlePriority.Vital, Reach = 4f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Smoke.Dust, Burst = 3, Direction = new Vector3(0f, 0.4f, 1f), Spread = 0.6f, Speed = R(1.2f, 2.7f), Size = R(0.77f, 1.21f), Life = R(0.8f, 1.2f), Tint = new Vector3(1.4f, 1.4f, 1.45f), Spin = R(0.2f, 0.6f) },
                new ParticleEmitter { Style = Chip, Burst = 6, Direction = new Vector3(0f, 0.6f, 1f), Spread = 0.8f, Speed = R(3f, 6.75f), Size = R(0.06f, 0.09f), Life = R(0.6f, 1f) },
            },
        };

        /// <summary>Something good come to rest, in its own colour (the tint): a ring on the ground and glints rising. Strength says how good.</summary>
        public static readonly ParticleEffect Landing = new()
        {
            Priority = ParticlePriority.Vital, Reach = 6f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Glows.Ring(ParticlePalette.White), Burst = 1, Place = EmitterPlace.Ground, Size = R(5f, 5f), Life = R(0.6f, 0.6f) },
                new ParticleEmitter { Style = Glows.Glint(ParticlePalette.White), Burst = 14, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 0.88f, 0f), Jitter = 1.32f, Size = R(0.192f, 0.32f), Life = R(0.7f, 1.3f), Speed = R(1.5f, 5.25f), Spread = 0.5f },
                new ParticleEmitter { Style = Glows.Wisp(ParticlePalette.White), Burst = 10, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 0.66f, 0f), Jitter = 1.1f, Size = R(0.22f, 0.352f), Life = R(1f, 1.8f), Speed = R(0.75f, 3f), Spread = 0.4f },
            },
        };
    }
}
