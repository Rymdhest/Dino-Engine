using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Particles.Content
{
    /// <summary>
    /// What an open waygate breathes out (Way, WAYGATES.md): motes of its light drifting out of both faces of the way, glints crawling round its edge, a rush of light inwards as it opens, and
    /// whatever drifts in the air of the land it goes to coming through after them. Every one fills an <see cref="EffectShape.Across"/> of the way and takes its colour from its run's tint,
    /// which is the way's, so one recipe serves every way to every land. Light first, as everything that glows here is: nothing in it is smoke.
    /// </summary>
    [ParticleCollection]
    public static class Ways
    {
        private static FloatRange R(float min, float max) => new FloatRange(min, max);

        // A metre or so of drift out of the way before anything's carried off, and a little more for what the wind takes.
        private const float Drift = 4f;
        private const float Out = 0.18f;    // Metres in front of the face a particle starts, so that none starts behind the light it's coming out of.

        /// <summary>A mote of a way's light, wandering out of it and fading.</summary>
        public static readonly ParticleStyle Mote = new()
        {
            Sprite = ParticleSprite.Glow, Palette = ParticlePalette.White,
            Heat = Curve.Line(0.72f, 0.42f),
            Opacity = Curve.Fade(0.18f, 0.55f),
            Size = Curve.Line(1f, 0.55f),
            Drag = 1.1f, Buoyancy = 0.25f, Turbulence = 0.6f, TurbulenceScale = 2.4f, Wind = 0.35f, Flicker = 0.35f, Softness = 0f,
        };

        /// <summary>A petal or a leaf from the other side, lit as it would be here, tumbling down on the wind.</summary>
        public static readonly ParticleStyle Petal = new()
        {
            Sprite = ParticleSprite.Glow, Lit = true,
            Colour = Vector3.One,
            Opacity = Curve.Fade(0.08f, 0.78f),
            Drag = 2.2f, Gravity = 0.9f, Turbulence = 0.8f, TurbulenceScale = 2.2f, Softness = 0f,
        };

        /// <summary>The way breathing: motes out of both faces, and glints round its edge, drifting in.</summary>
        public static readonly ParticleEffect Shimmer = new()
        {
            Priority = ParticlePriority.Ambient, Reach = Drift,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Mote, Rate = 0.2f, Per = RateBasis.SquareMetre, Place = EmitterPlace.Within, Offset = new Vector3(0f, 0f, Out), Direction = Vector3.UnitZ, Spread = 0.8f, Speed = R(0.4f, 1.2f), Size = R(0.1f, 0.17f), Life = R(2.2f, 3.6f) },
                new ParticleEmitter { Style = Mote, Rate = 0.2f, Per = RateBasis.SquareMetre, Place = EmitterPlace.Within, Offset = new Vector3(0f, 0f, -Out), Direction = -Vector3.UnitZ, Spread = 0.8f, Speed = R(0.4f, 1.2f), Size = R(0.1f, 0.17f), Life = R(2.2f, 3.6f) },
                new ParticleEmitter { Style = Glows.Glint(ParticlePalette.White), Rate = 10f, Place = EmitterPlace.Surface, Size = R(0.16f, 0.26f), Life = R(0.4f, 0.8f), Speed = R(0.1f, 0.4f), Spread = MathF.PI, Outward = -0.5f },
            },
        };

        /// <summary>A way opening: light rushing in from its edge to fill it, a flash, and a breath of motes thrown out of both faces.</summary>
        public static readonly ParticleEffect Kindle = new()
        {
            Reach = Drift + 2f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Glows.Core(ParticlePalette.White), Burst = 2, Place = EmitterPlace.Centre, Size = R(4.5f, 5.5f), Life = R(0.3f, 0.4f) },
                new ParticleEmitter { Style = Glows.Wisp(ParticlePalette.White), Burst = 56, Place = EmitterPlace.Surface, Size = R(0.16f, 0.28f), Life = R(0.7f, 1.2f), Outward = -2.6f, Speed = R(0.2f, 0.6f), Spread = MathF.PI },
                new ParticleEmitter { Style = Mote, Burst = 36, Place = EmitterPlace.Within, Offset = new Vector3(0f, 0f, Out), Direction = Vector3.UnitZ, Spread = 0.9f, Speed = R(2f, 4.5f), Size = R(0.12f, 0.2f), Life = R(0.9f, 1.6f) },
                new ParticleEmitter { Style = Mote, Burst = 22, Place = EmitterPlace.Within, Offset = new Vector3(0f, 0f, -Out), Direction = -Vector3.UnitZ, Spread = 0.9f, Speed = R(2f, 4.5f), Size = R(0.12f, 0.2f), Life = R(0.9f, 1.6f) },
            },
        };

        // What drifts through from the other side: out of both faces, slowly, and carried off by whatever air is here.
        private static ParticleEffect Through(ParticleStyle style, FloatRange size, float rate = 0.05f) => new()
        {
            Priority = ParticlePriority.Ambient, Reach = Drift + 2f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = style, Rate = rate, Per = RateBasis.SquareMetre, Place = EmitterPlace.Within, Offset = new Vector3(0f, 0f, Out), Direction = Vector3.UnitZ, Spread = 0.7f, Speed = R(0.5f, 1.3f), Size = size, Life = R(4f, 6.5f) },
                new ParticleEmitter { Style = style, Rate = rate, Per = RateBasis.SquareMetre, Place = EmitterPlace.Within, Offset = new Vector3(0f, 0f, -Out), Direction = -Vector3.UnitZ, Spread = 0.7f, Speed = R(0.5f, 1.3f), Size = size, Life = R(4f, 6.5f) },
            },
        };

        public static readonly ParticleEffect Petals = Through(Petal, R(0.09f, 0.14f));
        public static readonly ParticleEffect Leaves = Through(Petal, R(0.13f, 0.2f), 0.035f);
        public static readonly ParticleEffect Snow = Through(Motes.Snow, R(0.07f, 0.12f), 0.07f);
        public static readonly ParticleEffect Embers = Through(Motes.Ember, R(0.06f, 0.1f), 0.06f);
        public static readonly ParticleEffect Fireflies = Through(Motes.Firefly, R(0.08f, 0.12f), 0.03f);
        public static readonly ParticleEffect Dust = Through(Motes.Dust, R(0.05f, 0.09f), 0.07f);
        public static readonly ParticleEffect Pollen = Through(Motes.Pollen, R(0.05f, 0.08f), 0.06f);
        public static readonly ParticleEffect Wisps = Through(Motes.Wisp, R(0.12f, 0.2f), 0.03f);

        /// <summary>What comes through a way from a land whose air has <paramref name="motes"/> in it, and whether it takes the way's tint (what's white until it's told its colour) or keeps its own.</summary>
        public static (ParticleEffect Effect, bool Tinted)? From(AmbientMotes motes) => motes switch
        {
            AmbientMotes.Petals => (Petals, true),
            AmbientMotes.FallingLeaves => (Leaves, true),
            AmbientMotes.Snow => (Snow, false),
            AmbientMotes.Embers => (Embers, false),
            AmbientMotes.Fireflies => (Fireflies, false),
            AmbientMotes.Dust => (Dust, false),
            AmbientMotes.Pollen => (Pollen, false),
            AmbientMotes.Wisps => (Wisps, false),
            _ => null,
        };
    }
}
