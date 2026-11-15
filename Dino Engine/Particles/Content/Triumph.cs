using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Particles.Content
{
    /// <summary>
    /// A hero going up a level. It's the reward the whole climb is paid in, so it has to be seen from anywhere on screen in the middle of a fight, and over in under a breath so it
    /// never hides one: a flash at the hero's heart and a shockwave going out from their feet (a fast front of light 30 m across, sparks skimming out along the ground, dust thrown
    /// up in a ring, and LevelUpShow's gust flattening the grass with it), rays of light shot straight up that slow as they rise, and a sift of gold coming down after. It keeps time with the fanfare (Treasure.LevelUp): its bells climb for half a second and then its low bell rings, which is when a second, wider ring goes out and the
    /// light swells again. Gold is light here, warm and sunlit, and never the Reich's brass: everything in it glows, nothing in it is smoke. Played on a point at the hero's feet
    /// (LevelUpShow), so the rings are round the hero and not somewhere in a shape.
    /// </summary>
    [ParticleCollection]
    public static class Triumph
    {
        private static FloatRange R(float min, float max) => new FloatRange(min, max);

        // Deep and saturated, as anything pale goes white once mapped: gold with the hot parts going to white, and the faint parts to amber.
        private static readonly Vector3 Gold = new Vector3(1f, 0.62f, 0.22f);
        private static readonly Vector3 PaleGold = new Vector3(1f, 0.8f, 0.5f);
        private static readonly Vector3 Amber = new Vector3(1f, 0.5f, 0.14f);
        private static readonly Vector3 Warm = new Vector3(1f, 0.72f, 0.38f);

        /// <summary>When the fanfare's low bell rings, under its climbing ones.</summary>
        private const float Swell = 0.48f;

        // A streak is drawn along the last Stretch seconds of its way, behind it, so what's shot up from the ground is born as high as its trail is long: born on the ground, the
        // trail went down into it and showed under the hero's feet. The pillar is then whole from the ground up the moment it comes, and lifts off as it slows.
        private const float ShaftSpeed = 33f;
        private const float ShaftStretch = 0.32f;
        private const float RaySpeed = 24f;
        private const float RayStretch = 0.075f;

        /// <summary>A ray of light shot upward, drawn out along its way and shortening as it slows.</summary>
        public static readonly ParticleStyle Ray = new()
        {
            Sprite = ParticleSprite.Streak, Facing = ParticleFacing.Stretched, Stretch = RayStretch, Palette = ParticlePalette.White,
            Heat = Curve.Line(1f, 0.7f),
            Opacity = Curve.Fade(0.03f, 0.5f),
            Size = Curve.Line(1f, 0.55f),
            Drag = 1.5f, Wind = 0.15f, Softness = 0f,
        };

        /// <summary>The pillar: one long shaft of light leaping up from the hero, gone as fast as it came.</summary>
        public static readonly ParticleStyle Shaft = new()
        {
            Sprite = ParticleSprite.Streak, Facing = ParticleFacing.Stretched, Stretch = ShaftStretch, Palette = ParticlePalette.White,
            Heat = Curve.Line(0.85f, 0.55f),
            Opacity = new Curve((0f, 0f), (0.05f, 1f), (0.35f, 0.7f), (1f, 0f)),
            Size = Curve.Line(1f, 0.45f),
            Drag = 2.8f, Wind = 0f, Cover = 0.1f, Softness = 0f,
        };

        /// <summary>The shockwave's front: a ring of light that's out most of the way at once and then eases to its edge, as a blast's front does.</summary>
        public static readonly ParticleStyle Front = new()
        {
            Sprite = ParticleSprite.Decal, Pattern = DecalPattern.Ring, Facing = ParticleFacing.Ground, Palette = ParticlePalette.White,
            Size = new Curve((0f, 0.06f), (0.3f, 0.72f), (0.6f, 0.92f), (1f, 1f)),
            Opacity = new Curve((0f, 1f), (0.3f, 0.75f), (1f, 0f)),
            Heat = Curve.Line(0.85f, 0.6f), Cover = 0.1f, Softness = 0f,
        };

        /// <summary>A spark flung out along the ground by the shockwave, drawn out along its way.</summary>
        public static readonly ParticleStyle Skim = new()
        {
            Sprite = ParticleSprite.Streak, Facing = ParticleFacing.Stretched, Stretch = 0.06f, Palette = ParticlePalette.White,
            Heat = Curve.Line(0.95f, 0.55f),
            Opacity = new Curve((0f, 1f), (0.6f, 0.8f), (1f, 0f)),
            Size = Curve.Line(1f, 0.6f),
            Drag = 2.2f, Wind = 0.1f, Softness = 0f,
        };

        /// <summary>Gold dust settling after it, turning slowly on the air and twinkling.</summary>
        public static readonly ParticleStyle Sift = new()
        {
            Sprite = ParticleSprite.Glow, Palette = ParticlePalette.White,
            Heat = Curve.Line(0.72f, 0.45f),
            Opacity = Curve.Fade(0.12f, 0.7f),
            Size = Curve.Line(1f, 0.6f),
            Drag = 2.4f, Gravity = 1.5f, Turbulence = 0.55f, TurbulenceScale = 2.6f, Wind = 0.4f, Flicker = 0.55f, Softness = 0f,
        };

        /// <summary>A hero going up a level, at their feet on an EffectShape.Point.</summary>
        public static readonly ParticleEffect LevelUp = new()
        {
            Priority = ParticlePriority.Vital, Reach = 20f,
            Light = new EffectLight
            {
                Colour = new Vector3(1f, 0.78f, 0.45f), Intensity = 230f, Radius = 18f, Height = 4.5f, Flicker = 0f, Fill = 0.3f, Length = 1.2f,
                // A flash, settling, and swelling again with the low bell before it goes.
                Over = new Curve((0f, 1f), (0.1f, 0.62f), (0.3f, 0.45f), (0.36f, 0.68f), (0.6f, 0.3f), (1f, 0f)),
            },
            Emitters = new[]
            {
                // The flash at the hero's heart, and again, smaller, with the low bell.
                new ParticleEmitter { Style = Glows.Core(ParticlePalette.White), Burst = 1, Offset = new Vector3(0f, 2.4f, 0f), Size = R(4.6f, 4.6f), Life = R(0.2f, 0.2f), Tint = PaleGold },
                new ParticleEmitter { Style = Glows.Core(ParticlePalette.White), Burst = 1, Delay = Swell, Offset = new Vector3(0f, 2.2f, 0f), Size = R(3.4f, 3.4f), Life = R(0.3f, 0.3f), Tint = Gold, Strength = 0.6f },

                // The shockwave: its front, sparks skimming out under it, and the dust it throws up off the ground.
                new ParticleEmitter { Style = Front, Burst = 1, Place = EmitterPlace.Ground, Size = R(30f, 30f), Life = R(0.7f, 0.7f), Tint = Gold },
                new ParticleEmitter { Style = Skim, Burst = 24, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 0.25f, 0f), Jitter = 1.1f, Direction = Vector3.UnitY, Spread = 0.4f, Speed = R(0.5f, 2f), Outward = 27f, Size = R(0.12f, 0.2f), Life = R(0.5f, 0.85f), Tint = Gold },
                new ParticleEmitter { Style = Smoke.Dust, Burst = 12, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 0.6f, 0f), Jitter = 1.6f, Outward = 19f, Size = R(1.3f, 2.1f), Life = R(1f, 1.6f), Spin = R(0.2f, 0.6f), Tint = new Vector3(1.15f, 1.1f, 1.05f), Strength = 0.8f },

                // A crisp ring out over the ground behind it, and a wider, softer one with the low bell.
                new ParticleEmitter { Style = Glows.Ring(ParticlePalette.White), Burst = 1, Place = EmitterPlace.Ground, Size = R(11f, 11f), Life = R(0.5f, 0.5f), Tint = Gold },
                new ParticleEmitter { Style = Glows.Ring(ParticlePalette.White), Burst = 1, Delay = Swell, Place = EmitterPlace.Ground, Size = R(17f, 17f), Life = R(0.85f, 0.85f), Tint = Amber, Strength = 0.75f },

                // The pillar, and the rays round it, shot up from a ring round the hero's feet.
                new ParticleEmitter { Style = Shaft, Burst = 2, Place = EmitterPlace.Ground, Offset = new Vector3(0f, ShaftSpeed * ShaftStretch, 0f), Jitter = 0.25f, Direction = Vector3.UnitY, Speed = R(ShaftSpeed - 3f, ShaftSpeed + 3f), Size = R(1.1f, 1.4f), Life = R(0.5f, 0.6f), Tint = Warm, Turned = false },
                new ParticleEmitter { Style = Ray, Burst = 32, Place = EmitterPlace.Ground, Offset = new Vector3(0f, RaySpeed * RayStretch, 0f), Jitter = 1.8f, Direction = Vector3.UnitY, Spread = 0.1f, Speed = R(RaySpeed - 6f, RaySpeed + 6f), Size = R(0.17f, 0.27f), Life = R(0.7f, 1.1f), Tint = Gold },

                // Glints climbing the column, and wisps rising slower after them.
                new ParticleEmitter { Style = Glows.Glint(ParticlePalette.White), Burst = 24, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 0.5f, 0f), Jitter = 1.5f, Direction = Vector3.UnitY, Spread = 0.3f, Speed = R(3f, 9f), Size = R(0.26f, 0.42f), Life = R(0.9f, 1.6f), Tint = PaleGold },
                new ParticleEmitter { Style = Glows.Wisp(ParticlePalette.White), Burst = 18, Delay = 0.08f, Place = EmitterPlace.Ground, Offset = new Vector3(0f, 0.6f, 0f), Jitter = 1.3f, Direction = Vector3.UnitY, Spread = 0.45f, Speed = R(1f, 3f), Size = R(0.24f, 0.38f), Life = R(1.2f, 2f), Tint = Gold },

                // Gold sifting down round the hero once it's over.
                new ParticleEmitter { Style = Sift, Burst = 22, Delay = 0.35f, Offset = new Vector3(0f, 8f, 0f), Jitter = 3.4f, Spread = MathF.PI, Speed = R(0.5f, 2f), Size = R(0.1f, 0.17f), Life = R(2f, 3f), Tint = Gold },
            },
        };
    }
}
