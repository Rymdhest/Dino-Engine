using OpenTK.Mathematics;

namespace Dino_Engine.Particles
{
    /// <summary>What a particle is drawn as. Shaders/Particles/Particle.frag has a branch for each, in this order.</summary>
    public enum ParticleSprite
    {
        Glow,       // A soft round point: a spark of light, or a lit mote of dust.
        Streak,     // A soft line along the way it's going.
        Glint,      // A sharp sparkle with four points, as ice and light on water give.
        Smoke,      // A billowing puff from the smoke flipbook, lit from six ways.
        Flame,      // A tongue of fire from the flame flipbooks, by its heat.
        Droplet,    // A bead of liquid, lit, with the sun in it.
        Arc,        // A jagged thread of lightning along its axis.
        Decal,      // Laid on whatever is under it: see DecalPattern.
    }

    /// <summary>Which of the flame flipbooks a <see cref="ParticleSprite.Flame"/> plays. Shaders/Particles/ParticleFlipbooks.glsl lays them out in this order.</summary>
    public enum FlameShape
    {
        Hearth,     // A broad fire that goes on, from a bed of fuel: a campfire, a burning patch.
        Wick,       // A single tall tongue that goes on: a candle, a torch.
        Tongue,     // A gout of fire that rises, breaks up and is gone, over the particle's life.
    }

    /// <summary>What a <see cref="ParticleSprite.Decal"/> paints on the ground. Shaders/Particles/Particle.frag has a branch for each, in this order.</summary>
    public enum DecalPattern
    {
        Splat,      // Spilt liquid, lit and glossy: blood, venom.
        Embers,     // A bed of glowing coals and char, by its palette.
        Rime,       // Frost grown over the ground, sparkling in the sun.
        Pool,       // A bubbling pool, glowing by its palette.
        Ring,       // A ring going out, as a shockwave on the ground.
        Scorch,     // Ground burnt black.
    }

    /// <summary>Which way a particle's picture faces.</summary>
    public enum ParticleFacing
    {
        Camera,     // Square to the eye, turned by its angle.
        Upright,    // Square to the eye, with its top towards the sky, as a flame stands.
        Stretched,  // Drawn out along the way it's going, by Stretch seconds of its path.
        Ground,     // Lying on the ground under it.
        Segment,    // Along its axis, from where it is to where the axis ends.
    }

    /// <summary>What happens when a particle reaches the ground under where it was born.</summary>
    public enum ParticleContact
    {
        None,       // It goes through.
        Bounce,     // It comes off again, slower.
        Stick,      // It stays where it landed, lying on the ground: blood becomes a splat.
        Vanish,     // It's gone.
    }

    /// <summary>
    /// A kind of particle: how it looks and moves over its life. A style is data, shared by every particle of it, and goes to the GPU as a row of a table and two rows of
    /// curves (Shaders/Particles/Particle.glsl): a particle itself only carries where it is, how it's going, how big it is, and its seed. Curves run over the particle's life,
    /// 0 at birth to 1 at death. A style is given its place in the table the first time it's used, so styles need no registering.
    /// </summary>
    public sealed class ParticleStyle
    {
        public string Name = "";
        public ParticleSprite Sprite = ParticleSprite.Glow;
        public ParticleFacing Facing = ParticleFacing.Camera;
        public FlameShape Flame = FlameShape.Tongue;
        public DecalPattern Pattern = DecalPattern.Splat;

        /// <summary>Seen by the light around it, as smoke, dust and blood are. Otherwise it gives off light, from its <see cref="Palette"/>.</summary>
        public bool Lit;
        public ParticlePalette Palette = ParticlePalette.Fire;

        // ---------------------------------------------------------------- over its life
        public Curve Size = 1f;             // Times the size it was born with.
        public Curve Opacity = 1f;
        public Gradient Colour = Vector3.One;   // What it's made of if lit, and a tint on its palette if it glows.
        public Curve? Emission;             // How strongly it glows, times its palette: 1 for what glows and 0 for what's lit, unless it's said. Smoke over a fire glows too.
        public Curve Heat = 1f;             // Where on its palette it is: what's hot cools.
        public Curve Erosion = 0f;          // How far it's worn away, 0 to 1: smoke thins as it spreads (the depth through it, Particle.frag), and a flame breaks up, its thin parts first.

        // ---------------------------------------------------------------- how it moves
        public float Drag;                  // How quickly it takes on the speed of the air around it, per second.
        public float Gravity;               // Metres per second squared, down.
        public float Buoyancy;              // Metres per second squared, up, at birth: what's hot rises.
        public float BuoyancyFade = float.PositiveInfinity;    // Seconds for the lift to fall by e, as it cools.
        public float Turbulence;            // Metres per second of swirling, from a curl field.
        public float TurbulenceScale = 2f;  // Metres across a swirl.
        public float Wind = 1f;             // Share of the wind it's carried in. Only what has some drag feels it.
        public float SpinDrag;              // How quickly its spinning dies, per second.
        public ParticleContact Contact = ParticleContact.None;
        public float Bounce = 0.35f;        // Share of its speed into the ground that it comes off with.
        public float Friction = 0.5f;       // Share of its speed along the ground that it keeps when it touches.
        public float Spread = 1f;           // What a Stick particle grows to when it lands, times its size, as a drop spreads into a splat.

        // ---------------------------------------------------------------- how it's drawn
        public float Softness = 0.4f;       // Metres it fades over where it meets something solid, as a fraction of its size above 1.
        public float Stretch;               // Seconds of its path a Stretched particle is drawn along.
        public float Cover;                 // How much something glowing hides what's behind it, at its densest. Light alone goes to white over a sunlit field.
        public float Stand = 0.5f;          // How far an Upright particle stands up, from square to the eye (0) to straight up (1).
        public float FrameRate;             // Flipbook frames a second, for a flame that goes on. 0 plays the flipbook once over its life.
        public float Flicker;               // How much its brightness wavers, 0 to 1, each particle on a clock of its own.
        public float Toward;                // Metres it's drawn nearer the eye than it is, which leaves it where it is on screen: a flame in long grass shows over the grass.

        internal int Index = -1;
    }

    /// <summary>Every style that has been used, in the order they were first used: their place in the table the GPU reads.</summary>
    public static class ParticleStyles
    {
        public const int Most = 256;

        private static readonly List<ParticleStyle> _all = new();

        /// <summary>Goes up whenever a style is added, so the renderer knows to send the table again.</summary>
        public static int Version { get; private set; }

        public static IReadOnlyList<ParticleStyle> All => _all;

        public static int IndexOf(ParticleStyle style)
        {
            if (style.Index >= 0) return style.Index;
            if (_all.Count >= Most) throw new InvalidOperationException($"More than {Most} particle styles: {style.Name} has no room.");
            style.Index = _all.Count;
            _all.Add(style);
            Version++;
            return style.Index;
        }
    }
}
