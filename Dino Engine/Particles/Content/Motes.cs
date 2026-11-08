using OpenTK.Mathematics;

namespace Dino_Engine.Particles.Content
{
    /// <summary>What drifts in the air of a place for its own sake: pollen, snow, embers, fireflies, dust and wisps. They're carried by the wind, and swirl on the curl field.</summary>
    [ParticleCollection]
    public static class Motes
    {
        public static readonly ParticleStyle Pollen = new()
        {
            Sprite = ParticleSprite.Glow, Lit = true,
            Colour = new Vector3(1f, 0.92f, 0.55f),
            Opacity = Curve.Fade(0.15f, 0.75f),
            Drag = 2f, Buoyancy = 0.15f, Turbulence = 0.3f, TurbulenceScale = 3f, Softness = 0f,
        };

        public static readonly ParticleStyle Snow = new()
        {
            Sprite = ParticleSprite.Glow, Lit = true,
            Colour = new Vector3(0.95f, 0.97f, 1f),
            Opacity = Curve.Fade(0.1f, 0.8f),
            Drag = 3f, Gravity = 4.5f, Turbulence = 0.35f, TurbulenceScale = 2f, Softness = 0f,
        };

        public static readonly ParticleStyle Ember = new()
        {
            Sprite = ParticleSprite.Glow, Palette = ParticlePalette.Fire,
            Heat = Curve.Line(0.8f, 0.4f),
            Opacity = Curve.Fade(0.08f, 0.6f),
            Drag = 3f, Buoyancy = 4f, Turbulence = 0.6f, TurbulenceScale = 1.5f, Flicker = 0.5f, Softness = 0f,
        };

        public static readonly ParticleStyle Firefly = new()
        {
            Sprite = ParticleSprite.Glow, Palette = ParticlePalette.White,
            Colour = new Vector3(0.75f, 1f, 0.35f),
            Heat = 0.62f,
            Opacity = Curve.Fade(0.2f, 0.75f),
            Drag = 2f, Turbulence = 0.8f, TurbulenceScale = 2f, Wind = 0.2f, Flicker = 0.85f, Softness = 0f,
        };

        public static readonly ParticleStyle Dust = new()
        {
            Sprite = ParticleSprite.Glow, Lit = true,
            Colour = new Vector3(0.8f, 0.68f, 0.48f),
            Opacity = Curve.Fade(0.15f, 0.7f, 0.7f),
            Drag = 2.5f, Turbulence = 0.3f, TurbulenceScale = 2.5f, Softness = 0f,
        };

        public static readonly ParticleStyle Wisp = new()
        {
            Sprite = ParticleSprite.Glow, Palette = ParticlePalette.Spirit,
            Heat = 0.62f,
            Opacity = Curve.Fade(0.2f, 0.7f),
            Drag = 1.5f, Turbulence = 0.5f, TurbulenceScale = 2f, Wind = 0.2f, Flicker = 0.4f, Softness = 0f,
        };
    }
}
