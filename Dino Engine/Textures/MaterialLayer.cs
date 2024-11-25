using Dino_Engine.Rendering;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Textures
{
    internal class MaterialLayer
    {
        private FrameBuffer _normalBuffer;
        private FrameBuffer _materialBuffer1;
        private FrameBuffer _materialBuffer2;
        private bool _toggle = true;
        private Vector2i _resolution;

        public Vector2i Resolution { get => _resolution; set => _resolution = value; }

        public MaterialLayer(Vector2i resolution)
        {
            Resolution = resolution;

            DrawBufferSettings normalAttachment = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            normalAttachment.formatInternal = PixelInternalFormat.Rgba;
            normalAttachment.pixelType = PixelType.UnsignedByte;
            normalAttachment.wrapMode = TextureWrapMode.Repeat;
            normalAttachment.formatExternal = PixelFormat.Rgba;
            FrameBufferSettings normalBufferSettings = new FrameBufferSettings(Resolution);
            normalBufferSettings.drawBuffers.Add(normalAttachment);
            _normalBuffer = new FrameBuffer(normalBufferSettings);

            DrawBufferSettings albedoAttachment = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            albedoAttachment.formatInternal = PixelInternalFormat.Rgba;
            albedoAttachment.pixelType = PixelType.UnsignedByte;
            albedoAttachment.formatExternal = PixelFormat.Rgba;
            albedoAttachment.wrapMode = TextureWrapMode.Repeat;
            albedoAttachment.minFilterType = TextureMinFilter.Linear;

            DrawBufferSettings materialAttachment = new DrawBufferSettings(FramebufferAttachment.ColorAttachment1);
            materialAttachment.formatInternal = PixelInternalFormat.Rgba;
            materialAttachment.pixelType = PixelType.UnsignedByte;
            materialAttachment.formatExternal = PixelFormat.Rgba;
            materialAttachment.wrapMode = TextureWrapMode.Repeat;
            materialAttachment.minFilterType = TextureMinFilter.Linear;

            FrameBufferSettings materialSettings = new FrameBufferSettings(Resolution);
            materialSettings.drawBuffers.Add(albedoAttachment);
            materialSettings.drawBuffers.Add(materialAttachment);

            _materialBuffer1 = new FrameBuffer(materialSettings);
            _materialBuffer2 = new FrameBuffer(materialSettings);
        }
        public void CleanUp()
        {
            _normalBuffer.cleanUp();
            _materialBuffer1.cleanUp();
            _materialBuffer2.cleanUp();
        }

        public FrameBuffer GetNextFrameBuffer()
        {
            if (_toggle) return _materialBuffer1;
            else return _materialBuffer2;
        }
        public FrameBuffer GetLastFrameBuffer()
        {
            if (_toggle) return _materialBuffer2;
            else return _materialBuffer1;
        }
        public void StepToggle()
        {
            if (_toggle == true) _toggle = false;
            else _toggle = true;
        }


    }
}
