using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using Dino_Engine.Modelling;
using Dino_Engine.ECS;
using OpenTK.Mathematics;
using Dino_Engine.Core;

namespace Dino_Engine.Rendering
{
    public class DualBuffer
    {
        private FrameBuffer _buffer1;
        private FrameBuffer _buffer2;
        private bool _toggle;
        public DualBuffer(FrameBufferSettings frameBufferSettings) {
            _buffer1 = new FrameBuffer(frameBufferSettings);
            _buffer2 = new FrameBuffer(frameBufferSettings);
            _toggle = true;
        }
        private void RenderTexture(int texture)
        {
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texture);
            Engine.RenderEngine.ScreenQuadRenderer.Render();
        }
        public void RenderToScreen()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            Engine.RenderEngine.ScreenQuadRenderer.Render();
        }
        public void RenderTextureToScreen(int texture)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            RenderTexture(texture);
        }
        public void clearBothBuffers()
        {
            GetNextFrameBuffer().ClearColorDepth();
            StepToggle();
            GetNextFrameBuffer().ClearColorDepth();
            StepToggle();
        }

        public void RenderToNextFrameBuffer()
        {
            GetNextFrameBuffer().bind();
            Engine.RenderEngine.ScreenQuadRenderer.Render();
            StepToggle();
            Engine.RenderEngine.lastUsedBuffer = this;
        }


        public void blitBothDepthBufferFrom(FrameBuffer other)
        {
            _buffer1.blitDepthBufferFrom(other);
            _buffer2.blitDepthBufferFrom(other);
        }

        public void RenderTextureToNextFrameBuffer(int texture)
        {
            GetNextFrameBuffer().bind();
            //GL.Viewport(0, 0, resolution.X, resolution.Y);
            RenderTexture(texture);
            StepToggle();
        }
        public int GetLastOutputTexture()
        {
            return GetLastFrameBuffer().GetAttachment(0);
        }
        public int GetNextOutputTexture()
        {
            return GetNextFrameBuffer().GetAttachment(0);
        }
        public FrameBuffer GetNextFrameBuffer()
        {
            if (_toggle) return _buffer1;
            else return _buffer2;
        }
        public FrameBuffer GetLastFrameBuffer()
        {
            if (_toggle) return _buffer2;
            else return _buffer1;
        }
        public void StepToggle()
        {
            if (_toggle == true) _toggle = false;
            else _toggle = true;
        }

        public void OnResize(ResizeEventArgs eventArgs)
        {
            _buffer1.resize(eventArgs.Size);
            _buffer2.resize(eventArgs.Size);
        }
        public void CleanUp()
        {
            _buffer1.cleanUp();
            _buffer2.cleanUp();
        }
    }
}
