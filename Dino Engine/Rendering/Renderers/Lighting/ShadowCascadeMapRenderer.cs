using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    public struct Shadow
    {
        public Matrix4 lightViewMatrix;
        public Matrix4 shadowProjectionMatrix;
        public FrameBuffer shadowFrameBuffer;
        public float projectionSize;
        public float polygonOffsetModel;
        public float polygonOffsetTerrain;
        public bool isCubeMap;
        public int cubemapFaceIndex; // Crucial for mapping to correct cubemap face dynamically during deferred queue execution

        // Constructor for standard 2D Shadow Maps (Spotlights/Directional)
        public Shadow(Vector2i resolution, Matrix4 lightProjectionMatrix, float projectionSize, float polygonOffsetModel, float polygonOffsetTerrain)
        {
            this.projectionSize = projectionSize;
            this.polygonOffsetModel = polygonOffsetModel;
            this.polygonOffsetTerrain = polygonOffsetTerrain;
            this.isCubeMap = false;
            this.cubemapFaceIndex = -1; // Default state for 2D shadows

            FrameBufferSettings settings = new FrameBufferSettings(resolution);
            DepthAttachmentSettings depthAttachmentSettings = new DepthAttachmentSettings
            {
                isTexture = true,
                isShadowDepthTexture = true
            };
            settings.depthAttachmentSettings = depthAttachmentSettings;
            shadowFrameBuffer = new FrameBuffer(settings);
            lightViewMatrix = Matrix4.Identity;
            this.shadowProjectionMatrix = lightProjectionMatrix;
        }

        // Overloaded constructor for Point Light Cubemap Shadows
        public Shadow(int resolution, Matrix4 lightProjectionMatrix, float projectionSize, float polygonOffsetModel, float polygonOffsetTerrain, bool isCubeMap)
        {
            this.projectionSize = projectionSize;
            this.polygonOffsetModel = polygonOffsetModel;
            this.polygonOffsetTerrain = polygonOffsetTerrain;
            this.isCubeMap = isCubeMap;
            this.cubemapFaceIndex = 0; // Default state

            FrameBufferSettings settings = new FrameBufferSettings(new Vector2i(resolution));
            DepthAttachmentSettings depthAttachmentSettings = new DepthAttachmentSettings
            {
                isTexture = true,
                isShadowDepthTexture = true,
                target = isCubeMap ? TextureTarget.TextureCubeMap : TextureTarget.Texture2D
            };
            settings.depthAttachmentSettings = depthAttachmentSettings;
            shadowFrameBuffer = new FrameBuffer(settings);
            lightViewMatrix = Matrix4.Identity;
            this.shadowProjectionMatrix = lightProjectionMatrix;
        }
    }
}