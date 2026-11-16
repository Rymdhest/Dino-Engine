using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Particles.Content
{
    /// <summary>
    /// What a smithy gives off, as part of its models (<see cref="World.Content.AssetLight.Emitting"/>): the bed of a forge's fire, a smelting furnace's throat, steam off a quench
    /// trough and a hot vat, metal cooling in its moulds, and the cinders adrift in the heat of it all. Every one is made to stay in a room: it rises a few metres and is gone
    /// before a roof, and it hardly feels the wind, which is outside. None has a light of its own, as what it rises from is a model that carries that. A scale of 1 is said for each.
    /// </summary>
    [ParticleCollection]
    public static class Smithing
    {
        private static FloatRange R(float min, float max) => new FloatRange(min, max);

        // ---------------------------------------------------------------- styles

        /// <summary>Steam indoors: white, billowing up a couple of metres off hot water and gone.</summary>
        public static readonly ParticleStyle Vapour = new()
        {
            Sprite = ParticleSprite.Smoke, Lit = true,
            Colour = new Vector3(0.88f, 0.9f, 0.93f),
            Size = new Curve((0f, 0.5f), (0.35f, 1.25f), (1f, 2.1f)),
            Opacity = new Curve((0f, 0f), (0.12f, 0.55f), (0.5f, 0.34f), (1f, 0f)),
            Erosion = new Curve((0f, 0.2f), (0.5f, 0.45f), (1f, 0.85f)),
            Drag = 2.2f, Buoyancy = 3.4f, BuoyancyFade = 0.9f, Turbulence = 0.6f, TurbulenceScale = 2.6f, Wind = 0.2f, SpinDrag = 0.5f,
            Softness = 0.4f,
        };

        /// <summary>The smoke of a coal fire, drawn up under a hood: dark, quick, and thin within three metres.</summary>
        public static readonly ParticleStyle Reek = new()
        {
            Sprite = ParticleSprite.Smoke, Lit = true,
            Colour = new Gradient((0f, new Vector3(0.2f, 0.19f, 0.18f)), (0.5f, new Vector3(0.34f, 0.33f, 0.32f)), (1f, new Vector3(0.46f, 0.45f, 0.44f))),
            Size = Curve.Line(1f, 2.4f),
            Opacity = new Curve((0f, 0f), (0.1f, 0.5f), (0.5f, 0.34f), (1f, 0f)),
            Erosion = new Curve((0f, 0.2f), (0.45f, 0.4f), (1f, 0.85f)),
            Emission = new Curve((0f, 0.5f), (0.2f, 0f)), Heat = 0.45f,
            Drag = 1.2f, Buoyancy = 2.6f, BuoyancyFade = 1.6f, Turbulence = 0.4f, TurbulenceScale = 4.4f, Wind = 0.15f, SpinDrag = 0.05f,
            Softness = 0.5f,
        };

        /// <summary>A cinder adrift in the heat of a forge: slower than a spark off a fire, wandering, and a long time going out.</summary>
        public static readonly ParticleStyle Cinder = new()
        {
            Sprite = ParticleSprite.Glow, Facing = ParticleFacing.Stretched, Stretch = 0.04f,
            Heat = new Curve((0f, 0.85f), (0.5f, 0.65f), (1f, 0.4f)),
            Opacity = new Curve((0f, 0f), (0.1f, 1f), (0.6f, 0.8f), (1f, 0f)),
            Drag = 1.1f, Buoyancy = 1.5f, BuoyancyFade = 1.6f, Gravity = 0.35f, Turbulence = 1.5f, TurbulenceScale = 2.2f, Wind = 0.2f,
            Flicker = 0.6f, Softness = 0f,
        };

        /// <summary>A drop off a wet hem, the colour it's given, falling straight and gone where it lands.</summary>
        public static readonly ParticleStyle Drop = new()
        {
            Sprite = ParticleSprite.Droplet, Facing = ParticleFacing.Stretched, Stretch = 0.03f, Lit = true,
            Colour = new Vector3(0.9f, 0.9f, 0.9f),
            Opacity = new Curve((0f, 0f), (0.1f, 1f), (0.9f, 1f), (1f, 0f)),
            Gravity = 15.68f, Drag = 0.2f, Wind = 0f, Contact = ParticleContact.Vanish, Softness = 0f,
        };

        // ---------------------------------------------------------------- effects

        /// <summary>
        /// A stretch of a smith's fire: flames low over the coals, tongues off them, embers going up under the hood, now and then a spark thrown out towards the room (its +z)
        /// to bounce on the floor, and its smoke. Scale 1 is a metre and a half of the bed, and a long bed has several side by side.
        /// </summary>
        public static readonly ParticleEffect Coals = new()
        {
            Priority = ParticlePriority.Ambient, Reach = 9f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Fire.HearthFlame, Rate = 1.6f, Burst = 2, Life = R(1.2f, 1.8f), Size = R(1.1f, 1.5f), Jitter = 0.5f, Turned = false },
                new ParticleEmitter { Style = Fire.Tongue, Rate = 2.4f, Life = R(0.45f, 0.7f), Size = R(0.6f, 1f), Jitter = 0.55f, Offset = new Vector3(0f, 0.15f, 0f), Speed = R(0.5f, 1.3f), Spread = 0.25f, Turned = false },
                new ParticleEmitter { Style = Fire.Ember, Rate = 5f, Life = R(0.9f, 1.7f), Size = R(0.04f, 0.065f), Jitter = 0.55f, Offset = new Vector3(0f, 0.4f, 0f), Speed = R(1.2f, 3f), Spread = 0.35f },
                new ParticleEmitter { Style = Fire.Spark, Rate = 0.7f, Life = R(0.6f, 1.1f), Size = R(0.04f, 0.06f), Jitter = 0.4f, Offset = new Vector3(0f, 0.3f, 0f), Direction = new Vector3(0f, 0.8f, 0.6f), Spread = 0.6f, Speed = R(3.5f, 6.5f) },
                new ParticleEmitter { Style = Reek, Rate = 0.8f, Life = R(1.4f, 2f), Size = R(0.7f, 1f), Jitter = 0.4f, Offset = new Vector3(0f, 1.1f, 0f), Speed = R(0.8f, 1.4f), Spin = R(0.05f, 0.25f), Strength = 0.5f },
            },
        };

        /// <summary>
        /// The throat of a smelting furnace under blast: a jet of flame standing out of it, sparks and embers streaming up and spilling over its lip to bounce down its side,
        /// and its smoke. Scale 1 is a throat a metre across.
        /// </summary>
        public static readonly ParticleEffect Furnace = new()
        {
            Priority = ParticlePriority.Ambient, Reach = 10f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Fire.WickFlame, Rate = 1.2f, Burst = 2, Life = R(1.4f, 2f), Size = R(1.2f, 1.5f), Jitter = 0.12f, Turned = false },
                new ParticleEmitter { Style = Fire.Tongue, Rate = 4f, Life = R(0.4f, 0.65f), Size = R(0.7f, 1.1f), Jitter = 0.25f, Offset = new Vector3(0f, 0.3f, 0f), Speed = R(1f, 2.4f), Spread = 0.2f, Turned = false },
                new ParticleEmitter { Style = Fire.Ember, Rate = 9f, Life = R(1f, 1.9f), Size = R(0.04f, 0.07f), Jitter = 0.3f, Offset = new Vector3(0f, 0.5f, 0f), Speed = R(2f, 5f), Spread = 0.35f },
                new ParticleEmitter { Style = Fire.Spark, Rate = 1.6f, Life = R(0.7f, 1.3f), Size = R(0.04f, 0.06f), Jitter = 0.25f, Offset = new Vector3(0f, 0.3f, 0f), Direction = new Vector3(0f, 0.97f, 0.24f), Spread = 0.7f, Speed = R(4f, 8f) },
                new ParticleEmitter { Style = Reek, Rate = 1f, Life = R(1.5f, 2.2f), Size = R(0.8f, 1.2f), Jitter = 0.2f, Offset = new Vector3(0f, 1.7f, 0f), Speed = R(1f, 1.8f), Spin = R(0.05f, 0.25f), Strength = 0.55f },
            },
        };

        /// <summary>Steam off a trough with hot iron in it: thick where the iron went in, rolling up and gone. Scale 1 is a blade's worth.</summary>
        public static readonly ParticleEffect Quench = new()
        {
            Priority = ParticlePriority.Ambient, Reach = 6f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Vapour, Rate = 2.4f, Life = R(1.3f, 2.1f), Size = R(0.55f, 0.9f), Jitter = 0.25f, Offset = new Vector3(0f, 0.2f, 0f), Speed = R(0.5f, 1.1f), Spread = 0.3f, Spin = R(0.1f, 0.4f) },
            },
        };

        /// <summary>Steam idling off a vat that's kept hot: thin, slow, all over its top. Scale 1 is a vat two metres across.</summary>
        public static readonly ParticleEffect Vat = new()
        {
            Priority = ParticlePriority.Ambient, Reach = 6f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Vapour, Rate = 1f, Life = R(1.6f, 2.4f), Size = R(0.6f, 0.9f), Jitter = 0.5f, Offset = new Vector3(0f, 0.15f, 0f), Speed = R(0.2f, 0.5f), Spread = 0.3f, Spin = R(0.05f, 0.3f), Strength = 0.6f },
            },
        };

        /// <summary>Metal just poured, cooling in its moulds: threads of smoke off it, and the odd spark. Scale 1 is a casting bed a couple of metres long.</summary>
        public static readonly ParticleEffect Mould = new()
        {
            Priority = ParticlePriority.Ambient, Reach = 5f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Fire.Wisp, Rate = 1.1f, Life = R(1.6f, 2.4f), Size = R(0.22f, 0.36f), Jitter = 0.7f, Offset = new Vector3(0f, 0.1f, 0f), Speed = R(0.2f, 0.4f) },
                new ParticleEmitter { Style = Fire.Ember, Rate = 1.2f, Life = R(0.8f, 1.4f), Size = R(0.035f, 0.055f), Jitter = 0.6f, Offset = new Vector3(0f, 0.1f, 0f), Speed = R(0.6f, 1.6f), Spread = 0.5f },
            },
        };

        /// <summary>What runs off a length of cloth hung up wet over the vat it was dyed in: a drop now and then from somewhere along its hem, in its dye's colour (the effect's tint), falling as far as the vat. Scale 1 is a hem a metre wide, two and a half metres up.</summary>
        public static readonly ParticleEffect Drip = new()
        {
            Priority = ParticlePriority.Ambient, Reach = 5f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Drop, Rate = 1.5f, Life = R(0.54f, 0.6f), Size = R(0.07f, 0.1f), Jitter = 0.3f, Direction = -Vector3.UnitY, Speed = R(0f, 0.2f), Turned = false },
            },
        };

        /// <summary>Cinders adrift round a forge, wherever its heat carries them. Scale 1 is a hearth's worth of floor, three metres out each way.</summary>
        public static readonly ParticleEffect Cinders = new()
        {
            Priority = ParticlePriority.Ambient, Reach = 9f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Cinder, Rate = 3f, Life = R(2.5f, 4f), Size = R(0.035f, 0.055f), Jitter = 3.2f, Offset = new Vector3(0f, 1.4f, 0f), Speed = R(0.2f, 0.8f), Spread = 1f },
            },
        };
    }
}
