using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Particles.Content
{
    /// <summary>
    /// Cold: mist that sinks and rolls along the ground, as cold air does, glints of ice, shards that fly and bounce, rime grown over the ground, and a ring of it going out. It
    /// glows faintly by the frost palette so it can be read at night, and otherwise takes its look from the light it's in.
    /// </summary>
    [ParticleCollection]
    public static class Frost
    {
        private static FloatRange R(float min, float max) => new FloatRange(min, max);

        /// <summary>Cold mist: pale, sinking, spreading low.</summary>
        public static readonly ParticleStyle Mist = new()
        {
            Sprite = ParticleSprite.Smoke, Lit = true, Palette = ParticlePalette.Frost,
            Colour = new Vector3(0.78f, 0.86f, 0.95f),
            Size = Curve.Line(1f, 2.2f),
            Opacity = new Curve((0f, 0f), (0.12f, 0.3f), (0.6f, 0.18f), (1f, 0f)),
            Erosion = new Curve((0f, 0.3f), (0.4f, 0.4f), (1f, 0.75f)),
            Emission = 0.15f, Heat = 0.4f,
            Drag = 2.5f, Gravity = 0.4f, Turbulence = 0.3f, TurbulenceScale = 4.4f, Wind = 0.8f, SpinDrag = 0.3f,
            Softness = 0.4f,
        };

        /// <summary>A glint of ice, twinkling.</summary>
        public static readonly ParticleStyle Glint = new()
        {
            Sprite = ParticleSprite.Glint, Palette = ParticlePalette.Frost,
            Heat = 0.9f,
            Opacity = Curve.Fade(0.1f, 0.6f),
            Drag = 1.5f, Buoyancy = 0.3f, Turbulence = 0.45f, Softness = 0f,
        };

        /// <summary>A splinter of ice flung out, falling and skittering.</summary>
        public static readonly ParticleStyle Shard = new()
        {
            Sprite = ParticleSprite.Streak, Facing = ParticleFacing.Stretched, Stretch = 0.05f, Palette = ParticlePalette.Frost,
            Heat = Curve.Line(0.85f, 0.5f),
            Opacity = new Curve((0f, 1f), (0.75f, 1f), (1f, 0f)),
            Gravity = 11.2f, Drag = 0.4f, Contact = ParticleContact.Bounce, Bounce = 0.25f, Friction = 0.7f, Softness = 0f,
        };

        /// <summary>Frost grown over the ground.</summary>
        public static readonly ParticleStyle Rime = new()
        {
            Sprite = ParticleSprite.Decal, Pattern = DecalPattern.Rime, Facing = ParticleFacing.Ground, Lit = true, Palette = ParticlePalette.Frost,
            Colour = new Vector3(0.8f, 0.9f, 1f),
            Opacity = Curve.Fade(0.06f, 0.85f),
            Emission = 0.12f, Heat = 0.5f, Softness = 0f,
        };

        /// <summary>A ring of cold going out over the ground.</summary>
        public static readonly ParticleStyle Ring = new()
        {
            Sprite = ParticleSprite.Decal, Pattern = DecalPattern.Ring, Facing = ParticleFacing.Ground, Palette = ParticlePalette.Frost,
            Size = new Curve((0f, 0.25f), (0.5f, 0.9f), (1f, 1f)),
            Opacity = new Curve((0f, 1f), (0.5f, 0.9f), (1f, 0f)),
            Heat = 0.85f, Cover = 0.3f, Softness = 0f,
        };

        /// <summary>Someone chilled: cold mist coming off them, and glints.</summary>
        public static readonly ParticleEffect Chilled = new()
        {
            Reach = 4f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Mist, Rate = 2.2f, Per = RateBasis.Metre, Place = EmitterPlace.Within, Life = R(1.6f, 2.4f), Size = R(0.88f, 1.32f), Outward = 0.45f, Spin = R(0.1f, 0.4f) },
                new ParticleEmitter { Style = Glint, Rate = 2.5f, Per = RateBasis.Metre, Place = EmitterPlace.Surface, Life = R(0.5f, 1f), Size = R(0.16f, 0.256f) },
            },
        };

        /// <summary>Someone frozen solid: more mist, more glints, and frost grown at their feet.</summary>
        public static readonly ParticleEffect Frozen = new()
        {
            Reach = 5f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Mist, Rate = 3.5f, Per = RateBasis.Metre, Place = EmitterPlace.Within, Life = R(1.8f, 2.6f), Size = R(0.99f, 1.54f), Outward = 0.6f, Spin = R(0.1f, 0.4f) },
                new ParticleEmitter { Style = Glint, Rate = 6f, Per = RateBasis.Metre, Place = EmitterPlace.Surface, Life = R(0.6f, 1.2f), Size = R(0.192f, 0.32f) },
                new ParticleEmitter { Style = Rime, Rate = 0.35f, Burst = 1, Place = EmitterPlace.Ground, Life = R(3.5f, 4.5f), Size = R(3.52f, 4.62f) },
            },
        };

        /// <summary>Ground gripped by cold, all over an area: rime, low mist and glints.</summary>
        public static readonly ParticleEffect Ground = new()
        {
            Reach = 6f,
            Light = new EffectLight { Colour = new Vector3(0.45f, 0.75f, 1f), Intensity = 90f, Radius = 12.8f, Height = 2.2f, Flicker = 0.1f, FlickerSpeed = 2f, Fill = 0.3f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Rime, Rate = 0.02f, Per = RateBasis.SquareMetre, Place = EmitterPlace.Ground, Life = R(4f, 6f), Size = R(3.52f, 5.72f) },
                new ParticleEmitter { Style = Mist, Rate = 0.025f, Per = RateBasis.SquareMetre, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 0.66f, 0f), Life = R(2.5f, 3.5f), Size = R(2.2f, 3.52f), Spin = R(0.05f, 0.3f) },
                new ParticleEmitter { Style = Glint, Rate = 0.133f, Per = RateBasis.SquareMetre, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 0.55f, 0f), Life = R(0.5f, 1.1f), Size = R(0.16f, 0.288f) },
            },
        };

        /// <summary>A burst of cold: a ring going out to the edge of its EffectShape.Ball, mist rolling after it, glints and shards flung, and a flash.</summary>
        public static readonly ParticleEffect Burst = new()
        {
            Priority = ParticlePriority.Vital, Reach = 6f,
            Light = new EffectLight { Colour = new Vector3(0.5f, 0.8f, 1f), Intensity = 560f, Radius = 19.2f, Height = 3.3f, Flicker = 0f, Length = 0.6f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Ring, Burst = 1, Fills = true, Place = EmitterPlace.Ground, Size = R(1f, 1f), Life = R(0.5f, 0.5f) },
                new ParticleEmitter { Style = Mist, Burst = 8, Spreads = true, Offset = new Vector3(0f, 0.88f, 0f), Size = R(0.88f, 1.54f), Life = R(1.6f, 2.4f), Outward = 6f, Spread = 0.3f, Spin = R(0.2f, 0.6f) },
                new ParticleEmitter { Style = Glint, Burst = 18, Spreads = true, Offset = new Vector3(0f, 1.32f, 0f), Size = R(0.192f, 0.32f), Life = R(0.5f, 1f), Outward = 4.5f, Speed = R(0.75f, 2.25f), Spread = MathF.PI },
                new ParticleEmitter { Style = Shard, Burst = 16, Offset = new Vector3(0f, 1.1f, 0f), Size = R(0.06f, 0.09f), Life = R(0.5f, 0.8f), Speed = R(6f, 12f), Spread = 1.2f },
                new ParticleEmitter { Style = Rime, Burst = 1, Fills = true, Place = EmitterPlace.Ground, Size = R(1.1f, 1.2f), Life = R(4f, 5f) },
            },
        };

        /// <summary>A blow of cold landing, or the cold in a caster's hand: a puff of mist, glints, a few shards and a flash.</summary>
        public static readonly ParticleEffect Hit = new()
        {
            Priority = ParticlePriority.Vital, Reach = 4f,
            Light = new EffectLight { Colour = new Vector3(0.5f, 0.8f, 1f), Intensity = 80f, Radius = 7f, Height = 0.5f, Flicker = 0f, Length = 0.3f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Mist, Burst = 2, Size = R(0.88f, 1.32f), Life = R(1f, 1.4f), Outward = 1.8f, Spin = R(0.2f, 0.6f) },
                new ParticleEmitter { Style = Glint, Burst = 6, Size = R(0.16f, 0.256f), Life = R(0.4f, 0.8f), Speed = R(0.75f, 3f), Spread = MathF.PI },
                new ParticleEmitter { Style = Shard, Burst = 5, Size = R(0.045f, 0.075f), Life = R(0.4f, 0.6f), Speed = R(3f, 7.5f), Spread = 1.2f },
            },
        };

        /// <summary>What a bolt of cold leaves behind it: a cold glow, mist and glints, and its light on the ground under it.</summary>
        public static readonly ParticleEffect Bolt = new()
        {
            Priority = ParticlePriority.Vital, Reach = 4f,
            Light = new EffectLight { Colour = new Vector3(0.5f, 0.8f, 1f), Intensity = 100f, Radius = 9f, Flicker = 0.1f, FlickerSpeed = 6f, Fill = 0.25f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Glows.Core(ParticlePalette.Frost), Rate = 25f, Size = R(1.21f, 1.54f), Life = R(0.12f, 0.18f) },
                new ParticleEmitter { Style = Mist, Rate = 5f, Size = R(0.66f, 0.99f), Life = R(0.8f, 1.2f), Speed = R(0.15f, 0.75f), Spread = MathF.PI, Strength = 0.6f },
                new ParticleEmitter { Style = Glint, Rate = 14f, Size = R(0.128f, 0.224f), Life = R(0.4f, 0.8f), Speed = R(0.45f, 1.5f), Spread = MathF.PI },
            },
        };
    }
}
