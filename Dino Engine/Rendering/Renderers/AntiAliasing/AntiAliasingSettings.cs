namespace Dino_Engine.Rendering.Renderers.AntiAliasing
{
    /// <summary>
    /// How the image is anti-aliased (<see cref="RenderEngine.AntiAliasing"/>; live in the F3 Render window). Geometry is
    /// multisampled and shaded per sample where samples differ (<see cref="MsaaRenderer"/>), with coverage-based
    /// anti-aliasing on top for detail thinner than a pixel. Nothing is accumulated over frames, so there is no ghosting.
    /// </summary>
    public class AntiAliasingSettings
    {
        /// <summary>MSAA samples per pixel: 1 (off), 2, 4 or 8. Clamped to what the GPU supports.</summary>
        public int Samples = 4;

        /// <summary>Grass blades thinner than a pixel are drawn a pixel wide over a matching fraction of the samples,
        /// instead of popping in and out between samples as they sway. Needs 2 or more samples.</summary>
        public bool GrassCoverage = true;

        /// <summary>Alpha-tested cutouts (leaves, imposters) find their edge per sample, so they are anti-aliased like
        /// polygon edges.</summary>
        public bool AlphaToCoverage = true;

        /// <summary>FXAA over the tone-mapped image. Catches what multisampling can't see (shading and texture detail) at the
        /// cost of some sharpness; mostly useful with MSAA off.</summary>
        public bool Fxaa = false;

        /// <summary>Debug: tints the edge pixels, the ones shaded once per sample.</summary>
        public bool ShowEdgePixels = false;
    }
}
