
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    public struct Shadow
    {
        public Matrix4 lightViewMatrix;
        public Matrix4 shadowProjectionMatrix;
        public FrameBuffer shadowFrameBuffer;
        public float projectionSize;
        public float polygonOffset;
        public Shadow(Vector2i resolution, Matrix4 lightProjectionMatrix, float projectionSize, float polygonOffset)
        {
            this.projectionSize = projectionSize;
            this.polygonOffset = polygonOffset;
            FrameBufferSettings settings = new FrameBufferSettings(resolution);
            DepthAttachmentSettings depthAttachmentSettings = new DepthAttachmentSettings();
            depthAttachmentSettings.isTexture = true;
            depthAttachmentSettings.isShadowDepthTexture = true;
            settings.depthAttachmentSettings = depthAttachmentSettings;
            shadowFrameBuffer = new FrameBuffer(settings);
            lightViewMatrix = Matrix4.Identity;
            this.shadowProjectionMatrix = lightProjectionMatrix;
        }
    }
}
