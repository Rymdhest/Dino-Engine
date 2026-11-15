using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Particles.Content
{
    /// <summary>
    /// Blood, and only where something really bleeds: the game shows it on a blow only when its target has the Bleeding ailment, so blood on the ground is a promise that the
    /// wound is open (ParticleShow.Wound). Blood in flight is dark streaks, drawn out along the way they're going, never beads: from 33 m up a drop is a line or nothing, and a
    /// shaded sphere with the sun in it read as a red ball. Where a drop lands it leaves a splat drawn out along the slant it came in at (DecalPattern.Blood), thick and near
    /// black in the middle, crimson and thin at its lobed edge, with spatter flung ahead of it, glossy while it's fresh and drying brown as it lies. Nothing here is a circle.
    /// Everything is lit, so it's dark in the shade and dark at night. Tint it for what isn't red: ichor, sap.
    /// </summary>
    [ParticleCollection]
    public static class Blood
    {
        private static FloatRange R(float min, float max) => new FloatRange(min, max);

        /// <summary>Crimson, as a thin film of it lies: the decal darkens it where it's thick.</summary>
        private static readonly Vector3 Red = new Vector3(0.26f, 0.012f, 0.008f);

        /// <summary>A drop in flight, a dark streak along its path, that lands as a splat drawn out the way it was going.</summary>
        public static readonly ParticleStyle Drop = new()
        {
            Sprite = ParticleSprite.Streak, Facing = ParticleFacing.Stretched, Stretch = 0.06f, Lit = true,
            Pattern = DecalPattern.Blood,
            Colour = new Vector3(0.2f, 0.008f, 0.005f),
            Opacity = new Curve((0f, 1f), (0.85f, 1f), (1f, 0f)),
            Gravity = 17.6f, Drag = 0.5f, Contact = ParticleContact.Stick, Spread = 3.4f, Softness = 0f,
        };

        /// <summary>Fine spray: many tiny threads of it, gone in a moment, that fleck the ground where they land.</summary>
        public static readonly ParticleStyle Spray = new()
        {
            Sprite = ParticleSprite.Streak, Facing = ParticleFacing.Stretched, Stretch = 0.045f, Lit = true,
            Pattern = DecalPattern.Blood,
            Colour = new Vector3(0.26f, 0.012f, 0.008f),
            Opacity = new Curve((0f, 0.8f), (0.7f, 0.7f), (1f, 0f)),
            Gravity = 15f, Drag = 2.2f, Contact = ParticleContact.Stick, Spread = 2.2f, Softness = 0f,
        };

        /// <summary>A pool spreading under something that's bled a lot, drawn out the way the blow went (the effect's Forward, with Along).</summary>
        public static readonly ParticleStyle Pool = new()
        {
            Sprite = ParticleSprite.Decal, Pattern = DecalPattern.Blood, Facing = ParticleFacing.Ground, Lit = true, Stretch = 0.5f,
            Colour = Red,
            Size = new Curve((0f, 0.5f), (0.35f, 1f), (1f, 1f)),
            Opacity = new Curve((0f, 0f), (0.06f, 1f), (0.9f, 1f), (1f, 0f)),
            Softness = 0f,
        };

        /// <summary>A blow opening a wound: drops thrown along the effect's Forward and up a little, with a fine spray.</summary>
        public static readonly ParticleEffect Hit = new()
        {
            Priority = ParticlePriority.Vital, Reach = 6f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Drop, Burst = 9, Direction = new Vector3(0f, 0.45f, 1f), Spread = 0.55f, Speed = R(4f, 9.5f), Size = R(0.05f, 0.1f), Life = R(5f, 7f) },
                new ParticleEmitter { Style = Spray, Burst = 16, Direction = new Vector3(0f, 0.35f, 1f), Spread = 0.75f, Speed = R(3f, 8f), Size = R(0.025f, 0.045f), Life = R(2.5f, 4f) },
            },
        };

        /// <summary>A heavy blow on something bleeding: more and further.</summary>
        public static readonly ParticleEffect Gush = new()
        {
            Priority = ParticlePriority.Vital, Reach = 8f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Drop, Burst = 22, Direction = new Vector3(0f, 0.55f, 1f), Spread = 0.9f, Speed = R(3.5f, 11f), Size = R(0.05f, 0.115f), Life = R(6f, 8f) },
                new ParticleEmitter { Style = Spray, Burst = 28, Direction = new Vector3(0f, 0.45f, 1f), Spread = 1f, Speed = R(3f, 9f), Size = R(0.025f, 0.05f), Life = R(3f, 4.5f) },
            },
        };

        /// <summary>Something bleeding has fallen: a pool spreads under it, drawn out the way it was thrown, and the last of it runs out.</summary>
        public static readonly ParticleEffect Fallen = new()
        {
            Priority = ParticlePriority.Vital, Reach = 6f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Pool, Burst = 1, Delay = 0.45f, Place = EmitterPlace.Ground, Along = true, Size = R(1.7f, 2.4f), Life = R(14f, 18f) },
                new ParticleEmitter { Style = Drop, Rate = 6f, Duration = 1.5f, Offset = new Vector3(0f, 0.5f, 0f), Jitter = 0.5f, Direction = Vector3.UnitY, Spread = MathF.PI, Speed = R(0f, 0.6f), Size = R(0.05f, 0.09f), Life = R(5f, 7f) },
            },
        };

        /// <summary>Someone bleeding: drips falling off them, which spot the ground where they land, and so a trail wherever they go. It's the one sign a wound is open.</summary>
        public static readonly ParticleEffect Bleeding = new()
        {
            Reach = 4f,
            Emitters = new[]
            {
                new ParticleEmitter { Style = Drop, Rate = 1.6f, Per = RateBasis.Metre, Place = EmitterPlace.Within, Speed = R(0f, 0.6f), Spread = MathF.PI, Size = R(0.045f, 0.08f), Life = R(4f, 6f) },
            },
        };
    }
}
