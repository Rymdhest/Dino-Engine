using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Rendering
{
    public class DepthAttachmentSettings
    {
        public bool isTexture = false;
        public bool isShadowDepthTexture = false;
        public PixelInternalFormat precision = PixelInternalFormat.DepthComponent32;
        public TextureTarget target = TextureTarget.Texture2D; // Added target support
        /// <summary>A 32 bit float depth with an 8 bit stencil (DEPTH32F_STENCIL8), attached as depth-stencil. Textures only.</summary>
        public bool stencil = false;

        public DepthAttachmentSettings() { }
    }
}