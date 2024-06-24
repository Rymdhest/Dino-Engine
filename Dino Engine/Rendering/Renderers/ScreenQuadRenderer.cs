using Dino_Engine.Rendering.Renderers;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using Dino_Engine.Modelling;
using Dino_Engine.Core;
using System;
using Dino_Engine.Modelling.Model;

namespace Dino_Engine.Rendering.Renderers
{
    internal class ScreenQuadRenderer : Renderer
    {
        private FrameBuffer _buffer1;
        private FrameBuffer _buffer2;
        private bool _toggle;
        glModel _quadModel;
        public ScreenQuadRenderer() {
            float[] positions = { -1, 1, -1, -1, 1, -1, 1, 1 };
            int[] indices = { 0, 1, 2, 3, 0, 2 };
            _quadModel = glLoader.loadToVAO(positions, indices, 2);

            FrameBufferSettings frameBufferSettings= new FrameBufferSettings(Engine.Resolution);
            frameBufferSettings.drawBuffers.Add(new DrawBufferSettings(FramebufferAttachment.ColorAttachment0));
            DepthAttachmentSettings depthSettings = new DepthAttachmentSettings();
            depthSettings.isTexture = true;
            frameBufferSettings.depthAttachmentSettings = depthSettings;
            _buffer1 = new FrameBuffer(frameBufferSettings);
            _buffer2 = new FrameBuffer(frameBufferSettings);
            _toggle = true;
        }
        private void RenderTexture(int texture)
        {
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texture);
            Render();
        }
        public void RenderToScreen()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            Render();
        }
        public void RenderTextureToScreen(int texture)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            RenderTexture(texture);
        }
        public void Render(bool depthTest = false, bool depthMask = false, bool blend = false, bool clearColor = true)
        {
            GL.BindVertexArray(_quadModel.getVAOID());
            GL.EnableVertexAttribArray(0);

            GL.ClearColor(0f, 0f, 0f, 1f);
            if (clearColor)GL.Clear(ClearBufferMask.ColorBufferBit);
            

            if (depthTest) GL.Enable(EnableCap.DepthTest); 
            else GL.Disable(EnableCap.DepthTest);

            GL.DepthMask(depthMask);

            if (blend) GL.Enable(EnableCap.Blend);
            else GL.Disable(EnableCap.Blend);

            GL.DrawElements(PrimitiveType.Triangles, _quadModel.getVertexCount(), DrawElementsType.UnsignedInt, 0);

            GL.BindVertexArray(0);
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
            Render();
            StepToggle();
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

        public override void OnResize(ResizeEventArgs eventArgs)
        {
            _buffer1.resize(eventArgs.Size);
            _buffer2.resize(eventArgs.Size);
        }

        public override void Update()
        {

        }
        public override void CleanUp()
        {
            _buffer1.cleanUp();
            _buffer2.cleanUp();
            _quadModel.cleanUp();
        }
    }
}
