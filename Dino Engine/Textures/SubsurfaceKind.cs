namespace Dino_Engine.Textures
{
    /// <summary>
    /// What light does once it's got in under a material's surface (SUBSURFACE.md). How much it does is the material's own, texel by texel (<see cref="Synthesis.Surface.Subsurface"/>);
    /// this is the kind, which no amount can say. The numbers are the shaders' SSS_ kinds (Shaders/Lighting/SubsurfaceKinds.glsl), and each has a profile of its own in
    /// <see cref="Rendering.Renderers.Lighting.SubsurfaceSettings"/>.
    /// </summary>
    public enum SubsurfaceKind : byte
    {
        /// <summary>Thin, with a face of its own: cloth, paper, petals, bone worn thin. What falls on its back comes out of its front. Also everything that scatters nothing.</summary>
        Sheet = 0,

        /// <summary>A mass of thin things turned every way, whose normals are the mass's: leaves, blades, moss, fur. Light goes on through it mostly the way it was going.</summary>
        Canopy = 1,

        /// <summary>Skin and hide, thick: light goes on past where the sun stops and out where it's thin, red with blood.</summary>
        Flesh = 2,

        /// <summary>Snow and ice: light goes a long way in them, and what comes out has gone blue.</summary>
        Frost = 3,
    }
}
