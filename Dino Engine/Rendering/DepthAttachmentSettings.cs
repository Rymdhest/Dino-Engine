using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Rendering
{
    public class DepthAttachmentSettings
    {
        public bool isTexture = false;
        public bool isShadowDepthTexture = false;
        public PixelInternalFormat precision = PixelInternalFormat.DepthComponent32;
        public TextureTarget target = TextureTarget.Texture2D; // Added target support

        public DepthAttachmentSettings() { }
    }
}