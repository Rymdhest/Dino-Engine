
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering
{
    internal class FrameBufferSettings
    {
        public DepthAttachmentSettings? depthAttachmentSettings = null;
        public List<DrawBufferSettings> drawBuffers= new List<DrawBufferSettings>();
        public Vector2i resolution;
        public FrameBufferSettings(Vector2i resolution) {
            this.resolution = resolution;
        }
    }
}
