using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Particles.Content
{
    /// <summary>
    /// Fire, built the way real fire is seen: a body of flame that goes on (the hearth or wick flipbook, standing up and leaning with the air), gouts that break off it and rise
    /// and burn out, embers that swirl up as they cool, smoke that's dark over the fire and paler as it thins, coals glowing under it, and its light. Flames glow by their own
    /// temperature through a black body's colours. A candle is the same as a burning field, at a different scale and over a different shape.
    /// </summary>
    [ParticleCollection]
    public static class Fire
    {
        private static FloatRange R(float min, float max) => new FloatRange(min, max);

        // ---------------------------------------------------------------- styles

        /// <summary>The body of a fire that goes on: broad flames off a bed of fuel.</summary>
        public static readonly ParticleStyle HearthFlame = new()
        {
            Sprite = ParticleSprite.Flame, Flame = FlameShape.Hearth, Facing = ParticleFacing.Upright,
            Size = new Curve((0f, 0.85f), (0.2f, 1f), (1f, 0.92f)),
            Opacity = Curve.Fade(0.18f, 0.72f),
            Erosion = new Curve((0f, 0.3f), (0.2f, 0f), (0.72f, 0f), (1f, 0.5f)),
            Wind = 0.35f, Cover = 0.55f, FrameRate = 30f, Flicker = 0.12f, Softness = 0.08f, Toward = 1f,
        };

        /// <summary>One tall tongue of fire that goes on: a candle's, a torch's.</summary>
        public static readonly ParticleStyle WickFlame = new()
        {
            Sprite = ParticleSprite.Flame, Flame = FlameShape.Wick, Facing = ParticleFacing.Upright,
            Size = new Curve((0f, 0.9f), (0.25f, 1f), (1f, 0.95f)),
            Opacity = Curve.Fade(0.15f, 0.75f),
            Erosion = new Curve((0f, 0.25f), (0.15f, 0f), (0.8f, 0f), (1f, 0.4f)),
            Wind = 0.3f, Cover = 0.55f, FrameRate = 30f, Flicker = 0.15f, Softness = 0.08f,
        };

        /// <summary>A lick of flame rising off a fire: a tongue that flickers up, thins and breaks up as it burns out.</summary>
        public static readonly ParticleStyle Tongue = new()
        {
            Sprite = ParticleSprite.Flame, Flame = FlameShape.Hearth, Facing = ParticleFacing.Upright,
            Size = new Curve((0f, 0.7f), (0.3f, 1f), (1f, 0.85f)),
            Opacity = Curve.Fade(0.08f, 0.55f),
            Erosion = new Curve((0f, 0.1f), (0.45f, 0.12f), (1f, 0.7f)),
            Heat = Curve.Line(1f, 0.8f), Emission = 1.25f,
            Drag = 1.6f, Buoyancy = 3.75f, BuoyancyFade = 0.8f, Turbulence = 0.75f, TurbulenceScale = 3.08f, Wind = 0.8f,
            Cover = 0.5f, FrameRate = 30f, Flicker = 0.1f, Softness = 0.08f, Toward = 1f,
        };

        /// <summary>A gout of fire seen whole, as a ball rolling up into itself: what a fireball and its trail are made of.</summary>
        public static readonly ParticleStyle Gout = new()
        {
            Sprite = ParticleSprite.Flame, Flame = FlameShape.Tongue, Facing = ParticleFacing.Camera,
            Size = new Curve((0f, 0.8f), (0.3f, 1f), (1f, 1.2f)),
            Opacity = Curve.Fade(0.04f, 0.6f),
            Erosion = new Curve((0f, 0f), (0.5f, 0f), (1f, 0.5f)),
            Drag = 2.5f, Buoyancy = 2.25f, Turbulence = 0.9f, TurbulenceScale = 2.64f, Wind = 0.5f, SpinDrag = 1f,
            Cover = 0.35f, Flicker = 0.1f, Softness = 0.2f,
        };

        /// <summary>A spark off a fire, swirling up and cooling from yellow to a dull red.</summary>
        public static readonly ParticleStyle Ember = new()
        {
            Sprite = ParticleSprite.Glow, Facing = ParticleFacing.Stretched, Stretch = 0.05f,
            Heat = new Curve((0f, 0.95f), (0.5f, 0.75f), (1f, 0.45f)),
            Opacity = Curve.Fade(0.03f, 0.55f),
            Drag = 0.9f, Buoyancy = 4.5f, BuoyancyFade = 1.1f, Gravity = 0.8f, Turbulence = 2.4f, TurbulenceScale = 2.42f,
            Flicker = 0.55f, Softness = 0f,
        };

        /// <summary>A spark thrown out by a blast or a blow, falling and bouncing as it cools.</summary>
        public static readonly ParticleStyle Spark = new()
        {
            Sprite = ParticleSprite.Streak, Facing = ParticleFacing.Stretched, Stretch = 0.045f,
            Heat = Curve.Line(1f, 0.55f),
            Opacity = new Curve((0f, 1f), (0.7f, 1f), (1f, 0f)),
            Gravity = 15.68f, Drag = 0.35f, Contact = ParticleContact.Bounce, Bounce = 0.35f, Friction = 0.55f, Softness = 0f,
        };

        /// <summary>Wood smoke: dark over the fire, paler as it thins, its underside lit by the flames under it.</summary>
        public static readonly ParticleStyle Smoke = new()
        {
            Sprite = ParticleSprite.Smoke, Lit = true,
            Colour = new Gradient((0f, new Vector3(0.3f, 0.28f, 0.27f)), (0.4f, new Vector3(0.46f, 0.45f, 0.44f)), (1f, new Vector3(0.58f, 0.57f, 0.56f))),
            Size = Curve.Line(1f, 2.8f),
            Opacity = new Curve((0f, 0f), (0.08f, 0.62f), (0.5f, 0.45f), (1f, 0f)),
            Erosion = new Curve((0f, 0.15f), (0.45f, 0.35f), (1f, 0.8f)),
            Emission = new Curve((0f, 0.6f), (0.15f, 0f)), Heat = 0.45f,
            Drag = 1.1f, Buoyancy = 1.95f, BuoyancyFade = 2.5f, Turbulence = 0.525f, TurbulenceScale = 6.6f, SpinDrag = 0.05f,
            Softness = 0.5f,
        };

        /// <summary>A thread of pale smoke, as a candle gives off.</summary>
        public static readonly ParticleStyle Wisp = new()
        {
            Sprite = ParticleSprite.Smoke, Lit = true,
            Colour = new Vector3(0.62f, 0.62f, 0.62f),
            Size = Curve.Line(1f, 4f),
            Opacity = new Curve((0f, 0f), (0.1f, 0.3f), (1f, 0f)),
            Erosion = new Curve((0f, 0.3f), (1f, 0.75f)),
            Drag = 1.5f, Buoyancy = 1.05f, Turbulence = 0.225f, TurbulenceScale = 1.32f,
            Softness = 0.3f,
        };

        /// <summary>A bed of glowing coals and char on the ground under a fire.</summary>
        public static readonly ParticleStyle Coals = new()
        {
            Sprite = ParticleSprite.Decal, Pattern = DecalPattern.Embers, Facing = ParticleFacing.Ground, Lit = true,
            Opacity = Curve.Fade(0.12f, 0.85f),
            Heat = 0.72f, Emission = 1f, Flicker = 0.08f, Softness = 0f,
        };

        /// <summary>Ground that's burning, not burnt: the same coals, hotter, so that a patch of it reads as fire from up here even in full sun.</summary>
        public static readonly ParticleStyle Blaze = new()
        {
            Sprite = ParticleSprite.Decal, Pattern = DecalPattern.Embers, Facing = ParticleFacing.Ground, Lit = true,
            Opacity = Curve.Fade(0.12f, 0.85f),
            Heat = 0.9f, Emission = 1.4f, Flicker = 0.12f, Softness = 0f,
        };

        /// <summary>Ground burnt black, after a fire or a blast.</summary>
        public static readonly ParticleStyle Scorch = new()
        {
            Sprite = ParticleSprite.Decal, Pattern = DecalPattern.Scorch, Facing = ParticleFacing.Ground, Lit = true,
            Colour = new Vector3(0.035f, 0.03f, 0.028f),
            Opacity = new Curve((0f, 0f), (0.04f, 0.9f), (0.8f, 0.9f), (1f, 0f)),
            Softness = 0f,
        };

        /// <summary>A ball of fire turning to smoke: a puff glowing from its core as it swells and cools.</summary>
        public static readonly ParticleStyle Fireball = new()
        {
            Sprite = ParticleSprite.Smoke, Lit = true,
            Colour = new Gradient((0f, new Vector3(0.12f, 0.1f, 0.09f)), (0.5f, new Vector3(0.28f, 0.27f, 0.26f)), (1f, new Vector3(0.42f, 0.41f, 0.4f))),
            Size = new Curve((0f, 0.55f), (0.12f, 1.1f), (1f, 2.1f)),
            Opacity = new Curve((0f, 0.95f), (0.6f, 0.65f), (1f, 0f)),
            Erosion = new Curve((0f, 0f), (0.55f, 0.15f), (1f, 0.75f)),
            Emission = new Curve((0f, 2.4f), (0.18f, 1.3f), (0.4f, 0.15f), (0.55f, 0f)),
            Heat = new Curve((0f, 1f), (0.25f, 0.75f), (0.5f, 0.35f)),
            Drag = 2.2f, Buoyancy = 3.3f, BuoyancyFade = 1.5f, Turbulence = 0.6f, TurbulenceScale = 5.5f, SpinDrag = 0.4f,
            Softness = 0.4f,
        };

        // ---------------------------------------------------------------- effects

        /// <summary>A candle's flame, and now and then a thread of smoke. Scale 1 is a candle.</summary>
        public static readonly ParticleEffect Candle = new()
        {
            Priority = ParticlePriority.Ambient, Reach = 1.6f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = WickFlame, Rate = 0.5f, Burst = 1, Life = R(3f, 4f), Size = R(0.3f, 0.36f), Turned = false },
                new ParticleEmitter { Style = Wisp, Rate = 0.15f, Life = R(2f, 3f), Size = R(0.088f, 0.132f), Offset = new Vector3(0f, 0.198f, 0f), Speed = R(0.15f, 0.3f) },
            },
        };

        /// <summary>A torch or a lantern's flame. Scale 1 is a torch.</summary>
        public static readonly ParticleEffect Torch = new()
        {
            Priority = ParticlePriority.Ambient, Reach = 8f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = WickFlame, Rate = 1.4f, Burst = 2, Life = R(1.6f, 2.2f), Size = R(0.9f, 1.15f), Turned = false },
                new ParticleEmitter { Style = Tongue, Rate = 3f, Life = R(0.45f, 0.65f), Size = R(0.45f, 0.7f), Jitter = 0.088f, Offset = new Vector3(0f, 0.22f, 0f), Speed = R(0.45f, 1.2f), Spread = 0.2f, Turned = false },
                new ParticleEmitter { Style = Ember, Rate = 2.5f, Life = R(1.2f, 2.2f), Size = R(0.038f, 0.06f), Offset = new Vector3(0f, 0.55f, 0f), Speed = R(0.9f, 2.4f), Spread = 0.5f },
                new ParticleEmitter { Style = Smoke, Rate = 1.2f, Life = R(2.5f, 3.5f), Size = R(0.55f, 0.77f), Offset = new Vector3(0f, 0.99f, 0f), Speed = R(0.45f, 0.9f), Spin = R(0.1f, 0.3f), Strength = 0.55f },
            },
        };

        /// <summary>How wide the flames of a candle, a torch and a brazier are at a scale of 1, for what knows only how wide its flame should be (AssetLight.Flame).</summary>
        public const float CandleWidth = 0.08f;
        public const float TorchWidth = 0.25f;
        public const float BrazierWidth = 0.8f;

        /// <summary>A fire in a bowl: flames, gouts, embers and a little smoke, and no coals on the ground, as it's up off it. Scale 1 is a bowl about a metre across.</summary>
        public static readonly ParticleEffect Brazier = new()
        {
            Priority = ParticlePriority.Ambient, Reach = 8f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = HearthFlame, Rate = 2.2f, Burst = 3, Life = R(1.3f, 1.9f), Size = R(1.5f, 1.9f), Jitter = 0.2f, Turned = false },
                new ParticleEmitter { Style = Tongue, Rate = 3f, Life = R(0.5f, 0.8f), Size = R(0.8f, 1.2f), Jitter = 0.3f, Offset = new Vector3(0f, 0.2f, 0f), Speed = R(0.6f, 1.5f), Spread = 0.25f, Turned = false },
                new ParticleEmitter { Style = Ember, Rate = 5f, Life = R(1.2f, 2.4f), Size = R(0.04f, 0.065f), Jitter = 0.3f, Offset = new Vector3(0f, 0.6f, 0f), Speed = R(1.5f, 4f), Spread = 0.45f },
                new ParticleEmitter { Style = Smoke, Rate = 0.8f, Life = R(4f, 6f), Size = R(0.9f, 1.3f), Offset = new Vector3(0f, 1.8f, 0f), Speed = R(0.6f, 1.2f), Spin = R(0.05f, 0.25f), Strength = 0.6f },
            },
        };

        /// <summary>The flames of a fire and nothing else, for something burning that has its own coals and smoke. Scale 1 is a campfire's.</summary>
        public static readonly ParticleEffect Hearth = new()
        {
            Reach = 6f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = HearthFlame, Rate = 2.4f, Burst = 3, Life = R(1.3f, 1.9f), Size = R(1.87f, 2.53f), Jitter = 0.55f, Turned = false },
            },
        };

        /// <summary>A campfire: flames off its bed, gouts, embers, a column of smoke and the coals under it. It has its own light, as part of what it's built into. Scale 1 is a campfire.</summary>
        public static readonly ParticleEffect Campfire = new()
        {
            Reach = 28f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = HearthFlame, Rate = 3f, Burst = 4, Life = R(1.3f, 1.9f), Size = R(2.6f, 3.3f), Jitter = 0.5f, Turned = false },
                new ParticleEmitter { Style = Tongue, Rate = 5f, Life = R(0.55f, 0.85f), Size = R(1.2f, 1.8f), Jitter = 0.88f, Offset = new Vector3(0f, 0.33f, 0f), Speed = R(0.6f, 1.65f), Spread = 0.25f, Turned = false },
                new ParticleEmitter { Style = Ember, Rate = 9f, Life = R(1.4f, 2.6f), Size = R(0.045f, 0.075f), Jitter = 0.77f, Offset = new Vector3(0f, 0.88f, 0f), Speed = R(1.5f, 4.5f), Spread = 0.45f },
                new ParticleEmitter { Style = Smoke, Rate = 1.6f, Life = R(5f, 7.5f), Size = R(1.32f, 1.98f), Jitter = 0.44f, Offset = new Vector3(0f, 2.64f, 0f), Speed = R(0.75f, 1.5f), Spin = R(0.05f, 0.25f) },
                new ParticleEmitter { Style = Coals, Rate = 0.4f, Burst = 2, Life = R(4f, 6f), Size = R(2.5f, 3f), Place = EmitterPlace.Ground },
            },
        };

        /// <summary>Someone on fire, all over their body (an EffectShape.Body), with the light of it.</summary>
        public static readonly ParticleEffect Burning = new()
        {
            Reach = 8f,
            Light = new EffectLight { Colour = new Vector3(1f, 0.5f, 0.18f), Intensity = 99f, Radius = 14.4f, Height = 2.64f, Flicker = 0.35f, FlickerSpeed = 11f, Fill = 0.25f },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Tongue, Rate = 9f, Per = RateBasis.Metre, Place = EmitterPlace.Within, Life = R(0.45f, 0.75f), Size = R(1.6f, 2.6f), Speed = R(0.45f, 1.5f), Spread = 0.3f, Turned = false },
                new ParticleEmitter { Style = Ember, Rate = 5f, Per = RateBasis.Metre, Place = EmitterPlace.Within, Life = R(1f, 2f), Size = R(0.038f, 0.06f), Speed = R(1.5f, 3.75f), Spread = 0.6f },
                new ParticleEmitter { Style = Smoke, Rate = 0.9f, Per = RateBasis.Metre, Place = EmitterPlace.Top, Life = R(3f, 4.5f), Size = R(0.88f, 1.32f), Speed = R(0.75f, 1.5f), Strength = 0.7f },
            },
        };

        /// <summary>Ground on fire, all over an area: coals, flames standing up out of them, gouts, embers and smoke, more of each the bigger it is.</summary>
        public static readonly ParticleEffect Ground = new()
        {
            Reach = 12f,
            Light = new EffectLight { Colour = new Vector3(1f, 0.5f, 0.17f), Intensity = 144f, Radius = 14.4f, Height = 3.3f, Flicker = 0.3f, FlickerSpeed = 8f, Fill = 0.3f, LightsFog = true },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Blaze, Rate = 0.015f, Per = RateBasis.SquareMetre, Place = EmitterPlace.Ground, Life = R(4f, 6f), Size = R(3.52f, 5.28f) },
                new ParticleEmitter { Style = HearthFlame, Rate = 0.11f, Per = RateBasis.SquareMetre, Place = EmitterPlace.Ground, Life = R(1.2f, 1.8f), Size = R(2.2f, 3.2f), Turned = false },
                new ParticleEmitter { Style = Tongue, Rate = 0.16f, Per = RateBasis.SquareMetre, Place = EmitterPlace.Ground, Life = R(0.5f, 0.8f), Size = R(1.6f, 2.4f), Speed = R(0.45f, 1.35f), Spread = 0.3f, Turned = false },
                new ParticleEmitter { Style = Ember, Rate = 0.117f, Per = RateBasis.SquareMetre, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 0.66f, 0f), Life = R(1.2f, 2.4f), Size = R(0.038f, 0.068f), Speed = R(1.2f, 3.75f), Spread = 0.5f },
                new ParticleEmitter { Style = Smoke, Rate = 0.015f, Per = RateBasis.SquareMetre, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 3.08f, 0f), Life = R(4f, 6f), Size = R(1.76f, 2.86f), Speed = R(0.6f, 1.35f), Spin = R(0.05f, 0.2f), Strength = 0.8f },
            },
        };

        /// <summary>A blast of fire: balls of flame swelling into smoke, gouts flung out, sparks, embers, the ground scorched, and a flash. Made for a blast about three metres across; in a wider EffectShape.Ball it spreads over all of it.</summary>
        public static readonly ParticleEffect Burst = new()
        {
            Priority = ParticlePriority.Vital, Reach = 16f,
            Light = new EffectLight { Colour = new Vector3(1f, 0.55f, 0.22f), Intensity = 684f, Radius = 25.6f, Height = 3.3f, Flicker = 0.2f, FlickerSpeed = 20f, Length = 0.7f, Over = new Curve((0f, 1f), (0.15f, 0.7f), (1f, 0f)), LightsFog = true },
            Emitters = new[]
            {
                new ParticleEmitter { Style = Fireball, Burst = 7, Spreads = true, Offset = new Vector3(0f, 1.32f, 0f), Size = R(2.42f, 3.74f), Life = R(1.6f, 2.4f), Outward = 3.75f, Speed = R(0.75f, 2.25f), Spread = 0.6f, Spin = R(0.2f, 0.8f) },
                new ParticleEmitter { Style = Gout, Burst = 10, Spreads = true, Offset = new Vector3(0f, 1.1f, 0f), Size = R(1.32f, 2.2f), Life = R(0.5f, 0.8f), Outward = 6.75f, Speed = R(1.5f, 3.75f), Spread = 0.8f },
                new ParticleEmitter { Style = Spark, Burst = 28, Offset = new Vector3(0f, 1.1f, 0f), Size = R(0.045f, 0.075f), Life = R(0.5f, 1.1f), Speed = R(7.5f, 16.5f), Spread = 1.1f },
                new ParticleEmitter { Style = Ember, Burst = 20, Spreads = true, Offset = new Vector3(0f, 1.76f, 0f), Size = R(0.045f, 0.075f), Life = R(1.5f, 3f), Speed = R(2.25f, 6f), Spread = 1.2f },
                new ParticleEmitter { Style = Smoke, Burst = 4, Spreads = true, Delay = 0.25f, Offset = new Vector3(0f, 2.64f, 0f), Size = R(1.5f, 2.2f), Life = R(3f, 4.5f), Outward = 1.8f, Speed = R(0.75f, 1.8f), Spin = R(0.05f, 0.3f) },
                new ParticleEmitter { Style = Scorch, Burst = 1, Fills = true, Place = EmitterPlace.Ground, Size = R(1.3f, 1.6f), Life = R(8f, 11f) },
            },
        };

        /// <summary>A blow of fire landing on someone, or the fire in a caster's hand: a gout and a few embers. Scale 1 is a hard blow.</summary>
        public static readonly ParticleEffect Hit = new()
        {
            Priority = ParticlePriority.Vital, Reach = 4f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Gout, Burst = 3, Size = R(1.1f, 1.76f), Life = R(0.35f, 0.5f), Speed = R(0.75f, 2.25f), Spread = 0.9f },
                new ParticleEmitter { Style = Ember, Burst = 8, Size = R(0.038f, 0.06f), Life = R(0.6f, 1.2f), Speed = R(2.25f, 6f), Spread = 1.2f },
            },
        };

        /// <summary>What a bolt of fire leaves behind it as it flies: gouts that burn out where they're left, embers and a little smoke.</summary>
        public static readonly ParticleEffect Bolt = new()
        {
            Priority = ParticlePriority.Vital, Reach = 6f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Gout, Rate = 32f, Size = R(1.21f, 1.76f), Life = R(0.22f, 0.32f), Speed = R(0.3f, 0.9f), Spread = MathF.PI },
                new ParticleEmitter { Style = Ember, Rate = 18f, Size = R(0.038f, 0.06f), Life = R(0.6f, 1.2f), Speed = R(0.75f, 2.7f), Spread = MathF.PI },
                new ParticleEmitter { Style = Smoke, Rate = 5f, Size = R(0.66f, 0.99f), Life = R(1.4f, 2f), Speed = R(0.15f, 0.6f), Strength = 0.6f },
            },
        };
    }
}
