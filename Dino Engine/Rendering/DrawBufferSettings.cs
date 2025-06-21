using OpenTK.Graphics.OpenGL;
namespace Dino_Engine.Rendering
{
    public class DrawBufferSettings
    {
        public PixelInternalFormat formatInternal = PixelInternalFormat.Rgba8;
        public PixelFormat formatExternal = PixelFormat.Rgba;
        public PixelType pixelType = PixelType.UnsignedByte;
        public FramebufferAttachment colorAttachment;
        public TextureMinFilter minFilterType = TextureMinFilter.Linear;
        public TextureMagFilter magFilterType = TextureMagFilter.Linear;
        public TextureWrapMode wrapMode = TextureWrapMode.ClampToBorder;
        public DrawBufferSettings(FramebufferAttachment colorAttachment) {
            this.colorAttachment = colorAttachment;
        }
    }
}
