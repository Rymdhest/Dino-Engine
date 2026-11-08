namespace Dino_Engine.Particles.Content
{
    /// <summary>Styles every kind of magic shares, one for each palette: the hot heart of a bolt, a wisp, a glint.</summary>
    public static class Glows
    {
        private static readonly Dictionary<ParticlePalette, ParticleStyle> _cores = new();
        private static readonly Dictionary<ParticlePalette, ParticleStyle> _wisps = new();
        private static readonly Dictionary<ParticlePalette, ParticleStyle> _glints = new();
        private static readonly Dictionary<ParticlePalette, ParticleStyle> _rings = new();

        /// <summary>A ring of light going out over the ground to the particle's size.</summary>
        public static ParticleStyle Ring(ParticlePalette palette) => Cached(_rings, palette, () => new ParticleStyle
        {
            Name = "Glows.Ring." + palette,
            Sprite = ParticleSprite.Decal, Pattern = DecalPattern.Ring, Facing = ParticleFacing.Ground, Palette = palette,
            Size = new Curve((0f, 0.25f), (0.5f, 0.9f), (1f, 1f)),
            Opacity = new Curve((0f, 1f), (0.5f, 0.9f), (1f, 0f)),
            Heat = 0.8f, Cover = 0.25f, Softness = 0f,
        });

        /// <summary>The hot heart of a bolt or a blast: a soft ball of light, here and gone, so that a stream of them is a glowing head.</summary>
        public static ParticleStyle Core(ParticlePalette palette) => Cached(_cores, palette, () => new ParticleStyle
        {
            Name = "Glows.Core." + palette,
            Sprite = ParticleSprite.Glow, Palette = palette,
            Size = Curve.Line(1f, 0.6f),
            Opacity = new Curve((0f, 1f), (1f, 0f)),
            Heat = Curve.Line(1f, 0.7f),
            Cover = 0.2f, Softness = 0f,
        });

        /// <summary>A mote of light that wanders as it rises, on the curl field.</summary>
        public static ParticleStyle Wisp(ParticlePalette palette) => Cached(_wisps, palette, () => new ParticleStyle
        {
            Name = "Glows.Wisp." + palette,
            Sprite = ParticleSprite.Glow, Palette = palette,
            Heat = Curve.Line(0.85f, 0.55f),
            Opacity = Curve.Fade(0.15f, 0.6f),
            Size = Curve.Line(1f, 0.7f),
            Drag = 1.5f, Buoyancy = 0.6f, Turbulence = 1.8f, TurbulenceScale = 3.3f, Wind = 0.5f,
            Flicker = 0.3f, Softness = 0f,
        });

        /// <summary>A sparkle with four points, twinkling.</summary>
        public static ParticleStyle Glint(ParticlePalette palette) => Cached(_glints, palette, () => new ParticleStyle
        {
            Name = "Glows.Glint." + palette,
            Sprite = ParticleSprite.Glint, Palette = palette,
            Heat = 0.85f,
            Opacity = Curve.Fade(0.1f, 0.6f),
            Drag = 1.5f, Buoyancy = 0.45f, Turbulence = 0.45f, Softness = 0f,
        });

        private static ParticleStyle Cached(Dictionary<ParticlePalette, ParticleStyle> cache, ParticlePalette palette, Func<ParticleStyle> make)
        {
            if (!cache.TryGetValue(palette, out ParticleStyle? style))
            {
                style = make();
                cache.Add(palette, style);
            }
            return style;
        }
    }
}
