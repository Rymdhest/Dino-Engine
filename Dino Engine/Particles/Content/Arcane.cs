using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Particles.Content
{
    /// <summary>Magic with no element: wisps of violet light wandering on the curl field, glints, and a ring going out where it bursts.</summary>
    [ParticleCollection]
    public static class Arcane
    {
        private static FloatRange R(float min, float max) => new FloatRange(min, max);

        /// <summary>Someone hexed: wisps winding round them, and glints.</summary>
        public static readonly ParticleEffect Hexed = new()
        {
            Reach = 4f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Glows.Wisp(ParticlePalette.Arcane), Rate = 3f, Per = RateBasis.Metre, Place = EmitterPlace.Within, Size = R(0.22f, 0.352f), Life = R(0.8f, 1.4f) },
                new ParticleEmitter { Style = Glows.Glint(ParticlePalette.Arcane), Rate = 2f, Per = RateBasis.Metre, Place = EmitterPlace.Surface, Size = R(0.16f, 0.24f), Life = R(0.5f, 0.9f) },
            },
        };

        /// <summary>Ground thick with magic, all over an area: wisps rising off it and glints.</summary>
        public static readonly ParticleEffect Ground = new()
        {
            Reach = 6f,
            Light = new EffectLight { Colour = new Vector3(0.7f, 0.3f, 1f), Intensity = 80f, Radius = 11.2f, Height = 2.2f, Flicker = 0.2f, FlickerSpeed = 3f, Fill = 0.3f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Glows.Wisp(ParticlePalette.Arcane), Rate = 0.117f, Per = RateBasis.SquareMetre, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 0.44f, 0f), Size = R(0.264f, 0.44f), Life = R(1f, 1.6f), Speed = R(0.3f, 0.9f) },
                new ParticleEmitter { Style = Glows.Glint(ParticlePalette.Arcane), Rate = 0.1f, Per = RateBasis.SquareMetre, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 0.66f, 0f), Size = R(0.16f, 0.256f), Life = R(0.5f, 1f) },
            },
        };

        /// <summary>A burst of magic: a ring to the edge of its EffectShape.Ball, wisps flung out and glints, and a flash.</summary>
        public static readonly ParticleEffect Burst = new()
        {
            Priority = ParticlePriority.Vital, Reach = 6f,
            Light = new EffectLight { Colour = new Vector3(0.72f, 0.3f, 1f), Intensity = 500f, Radius = 19.2f, Height = 3.3f, Flicker = 0f, Length = 0.55f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Glows.Ring(ParticlePalette.Arcane), Burst = 1, Fills = true, Place = EmitterPlace.Ground, Size = R(1f, 1f), Life = R(0.45f, 0.45f) },
                new ParticleEmitter { Style = Glows.Core(ParticlePalette.Arcane), Burst = 2, Offset = new Vector3(0f, 1.76f, 0f), Size = R(2.64f, 3.3f), Life = R(0.15f, 0.2f) },
                new ParticleEmitter { Style = Glows.Wisp(ParticlePalette.Arcane), Burst = 22, Spreads = true, Offset = new Vector3(0f, 1.32f, 0f), Size = R(0.264f, 0.44f), Life = R(0.7f, 1.2f), Outward = 4.5f, Speed = R(0.75f, 2.25f), Spread = MathF.PI },
                new ParticleEmitter { Style = Glows.Glint(ParticlePalette.Arcane), Burst = 14, Spreads = true, Offset = new Vector3(0f, 1.32f, 0f), Size = R(0.192f, 0.288f), Life = R(0.5f, 0.9f), Outward = 3f, Spread = MathF.PI },
            },
        };

        /// <summary>A blow of magic landing, or the magic in a caster's hand: wisps, glints and a flash.</summary>
        public static readonly ParticleEffect Hit = new()
        {
            Priority = ParticlePriority.Vital, Reach = 4f,
            Light = new EffectLight { Colour = new Vector3(0.72f, 0.3f, 1f), Intensity = 90f, Radius = 7f, Height = 0.5f, Flicker = 0f, Length = 0.3f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Glows.Core(ParticlePalette.Arcane), Burst = 1, Size = R(1.32f, 1.76f), Life = R(0.12f, 0.16f) },
                new ParticleEmitter { Style = Glows.Wisp(ParticlePalette.Arcane), Burst = 8, Size = R(0.176f, 0.308f), Life = R(0.5f, 0.9f), Speed = R(1.2f, 3f), Spread = MathF.PI },
                new ParticleEmitter { Style = Glows.Glint(ParticlePalette.Arcane), Burst = 4, Size = R(0.16f, 0.24f), Life = R(0.4f, 0.7f), Speed = R(0.75f, 2.25f), Spread = MathF.PI },
            },
        };

        /// <summary>What a bolt of magic trails: a glowing head, wisps wandering off it, and its light on the ground under it.</summary>
        public static readonly ParticleEffect Bolt = new()
        {
            Priority = ParticlePriority.Vital, Reach = 4f,
            Light = new EffectLight { Colour = new Vector3(0.72f, 0.3f, 1f), Intensity = 100f, Radius = 9f, Flicker = 0.15f, FlickerSpeed = 5f, Fill = 0.25f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Glows.Core(ParticlePalette.Arcane), Rate = 25f, Size = R(1.1f, 1.43f), Life = R(0.12f, 0.18f) },
                new ParticleEmitter { Style = Glows.Wisp(ParticlePalette.Arcane), Rate = 14f, Size = R(0.176f, 0.308f), Life = R(0.5f, 0.9f), Speed = R(0.45f, 1.5f), Spread = MathF.PI },
            },
        };

        /// <summary>A holy place waking: green light rising off it, glints, and a flash.</summary>
        public static readonly ParticleEffect Shrine = new()
        {
            Priority = ParticlePriority.Vital, Reach = 12f,
            Light = new EffectLight { Colour = new Vector3(0.5f, 1f, 0.75f), Intensity = 540f, Radius = 41.6f, Height = 5.5f, Flicker = 0f, Length = 1.6f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Glows.Wisp(ParticlePalette.Spirit), Burst = 30, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 0.66f, 0f), Jitter = 5.5f, Size = R(0.308f, 0.528f), Life = R(1.4f, 2.4f), Speed = R(1.5f, 3.75f), Spread = 0.3f },
                new ParticleEmitter { Style = Glows.Glint(ParticlePalette.Spirit), Burst = 20, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 2.2f, 0f), Jitter = 4.4f, Size = R(0.224f, 0.352f), Life = R(0.8f, 1.4f), Speed = R(0.75f, 2.25f), Spread = 0.5f },
                new ParticleEmitter { Style = Glows.Ring(ParticlePalette.Spirit), Burst = 1, Place = EmitterPlace.Ground, Size = R(10f, 10f), Life = R(0.9f, 0.9f) },
            },
        };
    }
}
