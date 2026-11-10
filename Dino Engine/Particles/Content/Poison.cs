using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Particles.Content
{
    /// <summary>
    /// Venom: a heavy green gas that hangs low and glows faintly, drops of it that fall and splash, pools that bubble, and spores drifting in it. Gas is lit like smoke, so it's
    /// darker where it's thick, and reads at night by its own faint glow.
    /// </summary>
    [ParticleCollection]
    public static class Poison
    {
        private static FloatRange R(float min, float max) => new FloatRange(min, max);

        /// <summary>A cloud of venom, hanging low.</summary>
        public static readonly ParticleStyle Gas = new()
        {
            Sprite = ParticleSprite.Smoke, Lit = true, Palette = ParticlePalette.Poison,
            Colour = new Gradient((0f, new Vector3(0.36f, 0.44f, 0.16f)), (1f, new Vector3(0.42f, 0.46f, 0.3f))),
            Size = Curve.Line(1f, 2f),
            Opacity = new Curve((0f, 0f), (0.12f, 0.28f), (0.6f, 0.18f), (1f, 0f)),
            Erosion = new Curve((0f, 0.35f), (0.5f, 0.5f), (1f, 0.8f)),
            Emission = 0.12f, Heat = 0.45f,
            Drag = 2f, Gravity = 0.24f, Turbulence = 0.525f, TurbulenceScale = 5.5f, Wind = 0.7f, SpinDrag = 0.3f,
            Softness = 0.4f,
        };

        /// <summary>A drop of venom, falling and splashing where it lands.</summary>
        public static readonly ParticleStyle Drip = new()
        {
            Sprite = ParticleSprite.Droplet, Facing = ParticleFacing.Stretched, Stretch = 0.03f, Lit = true, Palette = ParticlePalette.Poison,
            Pattern = DecalPattern.Splat,
            Colour = new Vector3(0.2f, 0.5f, 0.04f),
            Opacity = new Curve((0f, 1f), (0.8f, 1f), (1f, 0f)),
            Emission = 0.2f, Heat = 0.5f,
            Gravity = 15.68f, Drag = 0.2f, Contact = ParticleContact.Stick, Spread = 3f, Softness = 0f,
        };

        /// <summary>A bubbling pool of venom on the ground.</summary>
        public static readonly ParticleStyle Pool = new()
        {
            Sprite = ParticleSprite.Decal, Pattern = DecalPattern.Pool, Facing = ParticleFacing.Ground, Lit = true, Palette = ParticlePalette.Poison,
            Colour = new Vector3(0.03f, 0.04f, 0.015f),
            Opacity = Curve.Fade(0.08f, 0.85f),
            Heat = 0.38f, Emission = 0.7f, Softness = 0f,
        };

        /// <summary>Someone envenomed: venom dripping off them, a wisp of gas, spores.</summary>
        public static readonly ParticleEffect Envenomed = new()
        {
            Reach = 4f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Drip, Rate = 1.4f, Per = RateBasis.Metre, Place = EmitterPlace.Within, Size = R(0.06f, 0.09f), Life = R(2.5f, 3.5f) },
                new ParticleEmitter { Style = Gas, Rate = 1.2f, Per = RateBasis.Metre, Place = EmitterPlace.Within, Size = R(0.66f, 1.1f), Life = R(1.6f, 2.4f), Spin = R(0.1f, 0.4f), Strength = 0.7f },
                new ParticleEmitter { Style = Glows.Wisp(ParticlePalette.Poison), Rate = 1.6f, Per = RateBasis.Metre, Place = EmitterPlace.Surface, Size = R(0.11f, 0.176f), Life = R(1.2f, 2f) },
            },
        };

        /// <summary>Venom over an area of ground: pools, gas hanging over them, spores.</summary>
        public static readonly ParticleEffect Ground = new()
        {
            Reach = 6f,
            Light = new EffectLight { Colour = new Vector3(0.35f, 0.9f, 0.2f), Intensity = 65f, Radius = 11.2f, Height = 2.2f, Flicker = 0.15f, FlickerSpeed = 2f, Fill = 0.3f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Pool, Rate = 0.017f, Per = RateBasis.SquareMetre, Place = EmitterPlace.Ground, Life = R(4f, 6f), Size = R(3.85f, 6.6f) },
                new ParticleEmitter { Style = Gas, Rate = 0.022f, Per = RateBasis.SquareMetre, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 1.1f, 0f), Size = R(1.76f, 3.08f), Life = R(3f, 4.5f), Speed = R(0.15f, 0.45f), Spin = R(0.05f, 0.3f) },
                new ParticleEmitter { Style = Glows.Wisp(ParticlePalette.Poison), Rate = 0.1f, Per = RateBasis.SquareMetre, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 0.88f, 0f), Size = R(0.11f, 0.198f), Life = R(1.5f, 2.5f) },
            },
        };

        /// <summary>A splash of venom: gas bursting out, drops flung up that come down as splashes, a pool, a green flash.</summary>
        public static readonly ParticleEffect Burst = new()
        {
            Priority = ParticlePriority.Vital, Reach = 6f,
            Light = new EffectLight { Colour = new Vector3(0.4f, 0.95f, 0.2f), Intensity = 340f, Radius = 16f, Height = 2.64f, Flicker = 0f, Length = 0.5f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Gas, Burst = 6, Spreads = true, Offset = new Vector3(0f, 1.1f, 0f), Size = R(1.32f, 2.2f), Life = R(2.5f, 3.5f), Outward = 3.75f, Spread = 0.4f, Spin = R(0.1f, 0.5f) },
                new ParticleEmitter { Style = Drip, Burst = 18, Spreads = true, Offset = new Vector3(0f, 0.88f, 0f), Size = R(0.075f, 0.12f), Life = R(3f, 4f), Speed = R(4.5f, 9f), Spread = 0.9f },
                new ParticleEmitter { Style = Pool, Burst = 3, Spreads = true, Place = EmitterPlace.Ground, Jitter = 1.2f, Size = R(2.75f, 4.5f), Life = R(4f, 5.5f) },
            },
        };

        /// <summary>A blow of venom landing, or the venom in a caster's hand: a puff of gas, a few drops and a sickly flash.</summary>
        public static readonly ParticleEffect Hit = new()
        {
            Priority = ParticlePriority.Vital, Reach = 4f,
            Light = new EffectLight { Colour = new Vector3(0.4f, 0.95f, 0.2f), Intensity = 60f, Radius = 7f, Height = 0.5f, Flicker = 0f, Length = 0.3f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Gas, Burst = 2, Size = R(0.88f, 1.32f), Life = R(1.2f, 1.8f), Outward = 1.5f, Spin = R(0.2f, 0.5f) },
                new ParticleEmitter { Style = Drip, Burst = 6, Size = R(0.06f, 0.09f), Life = R(2f, 3f), Speed = R(2.25f, 5.25f), Spread = 0.9f },
            },
        };

        /// <summary>What a bolt of venom trails: a green glow, gas and falling drops, and its light on the ground under it.</summary>
        public static readonly ParticleEffect Bolt = new()
        {
            Priority = ParticlePriority.Vital, Reach = 4f,
            Light = new EffectLight { Colour = new Vector3(0.4f, 0.95f, 0.2f), Intensity = 75f, Radius = 8f, Flicker = 0.1f, FlickerSpeed = 4f, Fill = 0.25f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Glows.Core(ParticlePalette.Poison), Rate = 25f, Size = R(1.1f, 1.43f), Life = R(0.12f, 0.18f) },
                new ParticleEmitter { Style = Gas, Rate = 4f, Size = R(0.66f, 0.99f), Life = R(1f, 1.5f), Speed = R(0.15f, 0.6f), Spread = MathF.PI, Strength = 0.55f },
                new ParticleEmitter { Style = Drip, Rate = 6f, Size = R(0.06f, 0.09f), Life = R(2f, 3f) },
            },
        };
    }
}
