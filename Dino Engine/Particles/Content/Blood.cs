using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Particles.Content
{
    /// <summary>
    /// Blood, as Ghost of Tsushima has it: a spray of drops along the way the blow went, each lit and glossy with the sun in it, drawn out by its speed, that land on the ground
    /// and spread into splats that stay a while, with a fine mist that hangs a moment. Everything here is lit, so it's dark red in the shade and dark at night. Tint it for what
    /// isn't red: ichor, sap.
    /// </summary>
    [ParticleCollection]
    public static class Blood
    {
        private static FloatRange R(float min, float max) => new FloatRange(min, max);

        private static readonly Vector3 Red = new Vector3(0.3f, 0.012f, 0.008f);

        /// <summary>A drop, flying and then a splat where it landed.</summary>
        public static readonly ParticleStyle Drop = new()
        {
            Sprite = ParticleSprite.Droplet, Facing = ParticleFacing.Stretched, Stretch = 0.035f, Lit = true,
            Pattern = DecalPattern.Splat,
            Colour = Red,
            Opacity = new Curve((0f, 1f), (0.85f, 1f), (1f, 0f)),
            Gravity = 17.6f, Drag = 0.4f, Contact = ParticleContact.Stick, Spread = 2.6f, Softness = 0f,
        };

        /// <summary>A fine spray, hanging a moment.</summary>
        public static readonly ParticleStyle Mist = new()
        {
            Sprite = ParticleSprite.Glow, Lit = true,
            Colour = new Vector3(0.34f, 0.02f, 0.014f),
            Size = Curve.Line(0.8f, 1.4f),
            Opacity = new Curve((0f, 0.6f), (0.4f, 0.4f), (1f, 0f)),
            Drag = 4f, Gravity = 3.2f, Softness = 0f,
        };

        /// <summary>A pool spreading under something that's bled a lot.</summary>
        public static readonly ParticleStyle Pool = new()
        {
            Sprite = ParticleSprite.Decal, Pattern = DecalPattern.Splat, Facing = ParticleFacing.Ground, Lit = true,
            Colour = new Vector3(0.22f, 0.008f, 0.006f),
            Size = new Curve((0f, 0.35f), (0.25f, 1f), (1f, 1f)),
            Opacity = new Curve((0f, 0f), (0.08f, 0.95f), (0.85f, 0.95f), (1f, 0f)),
            Softness = 0f,
        };

        /// <summary>A blow landing, the drops thrown along the effect's Forward and up a little.</summary>
        public static readonly ParticleEffect Hit = new()
        {
            Priority = ParticlePriority.Vital, Reach = 6f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Drop, Burst = 12, Direction = new Vector3(0f, 0.5f, 1f), Spread = 0.6f, Speed = R(3.75f, 9f), Size = R(0.075f, 0.135f), Life = R(5f, 7f) },
                new ParticleEmitter { Style = Mist, Burst = 7, Direction = new Vector3(0f, 0.4f, 1f), Spread = 0.7f, Speed = R(2.25f, 4.5f), Size = R(0.22f, 0.396f), Life = R(0.3f, 0.5f) },
            },
        };

        /// <summary>A killing blow: more and further, and a pool spreading where they fell.</summary>
        public static readonly ParticleEffect Gush = new()
        {
            Priority = ParticlePriority.Vital, Reach = 8f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Drop, Burst = 34, Direction = new Vector3(0f, 0.6f, 1f), Spread = 1f, Speed = R(3f, 10.5f), Size = R(0.075f, 0.15f), Life = R(6f, 8f) },
                new ParticleEmitter { Style = Mist, Burst = 12, Direction = new Vector3(0f, 0.5f, 1f), Spread = 0.9f, Speed = R(2.25f, 5.25f), Size = R(0.264f, 0.44f), Life = R(0.35f, 0.6f) },
                new ParticleEmitter { Style = Pool, Burst = 1, Delay = 0.3f, Place = EmitterPlace.Ground, Size = R(3.8f, 5.4f), Life = R(10f, 13f) },
            },
        };

        /// <summary>Someone bleeding: drops falling from them, which splash where they land.</summary>
        public static readonly ParticleEffect Bleeding = new()
        {
            Reach = 4f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Drop, Rate = 2f, Per = RateBasis.Metre, Place = EmitterPlace.Within, Speed = R(0f, 0.75f), Spread = MathF.PI, Size = R(0.06f, 0.105f), Life = R(4f, 6f) },
            },
        };
    }
}
