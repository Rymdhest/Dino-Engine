
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering
{
    public class FrameBufferSettings
    {
        public DepthAttachmentSettings? depthAttachmentSettings = null;
        public List<DrawBufferSettings> drawBuffers= new List<DrawBufferSettings>();
        public Vector2i resolution;
        /// <summary>Samples per pixel. 0: ordinary textures. Above 0: multisample textures for every attachment, read with
        /// texelFetch from a sampler2DMS (never filtered).</summary>
        public int samples = 0;
        /// <summary>Renders with this framebuffer's depth (and stencil) attachment instead of creating one. Resize it first.</summary>
        public FrameBuffer? sharedDepth = null;
        public FrameBufferSettings(Vector2i resolution) {
            this.resolution = resolution;
        }
    }
}
