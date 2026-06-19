using OpenTK.Windowing.Common;
using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.Core;
using Dino_Engine.ECS;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    public class DepthBasedBlurRenderer : RenderPassRenderer
    {
        private ShaderProgram _depthBasedBlurShader = new ShaderProgram("Simple.vert", "Depth_Blur.frag");
        private FrameBuffer _frameBuffer;
        private int _downscalingFactor;

        public DepthBasedBlurRenderer(int downscalingFactor = 2) : base("Depth Based Blur")
        {
            trackPerformance = false;
            _depthBasedBlurShader.bind();
            _depthBasedBlurShader.loadUniformInt("originalTexture", 0);
            _depthBasedBlurShader.loadUniformInt("gDepth", 1);
            _depthBasedBlurShader.unBind();
            _downscalingFactor = downscalingFactor;

            FrameBufferSettings settings = new FrameBufferSettings(Engine.Resolution / _downscalingFactor);
            DrawBufferSettings drawSettings = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            drawSettings.formatInternal = PixelInternalFormat.Rgba16f;
            //drawSettings.formatExternal = PixelFormat.Rgb;
            drawSettings.pixelType = PixelType.Float;
            drawSettings.wrapMode = TextureWrapMode.ClampToEdge;
            drawSettings.minFilterType = TextureMinFilter.Linear;
            drawSettings.magFilterType = TextureMagFilter.Linear;
            settings.drawBuffers.Add(drawSettings);
            _frameBuffer = new FrameBuffer(settings);
        }

        public void Render(FrameBuffer sourceBuffer, int depthBuffer, int blurRadius, ScreenQuadRenderer renderer, int attachment = 0)
        {
            _frameBuffer.bind();
            Vector2 textureSize = sourceBuffer.getResolution();
            _depthBasedBlurShader.bind();
            _depthBasedBlurShader.loadUniformVector2f("resolutionInput", textureSize);
            _depthBasedBlurShader.loadUniformFloat("depthStrength", 60f);
            _depthBasedBlurShader.loadUniformInt("blurRange", 3);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, sourceBuffer.GetAttachment(attachment));
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, depthBuffer);
            renderer.Render();

        }

        public int GetLastResultTexture()
        {
            return _frameBuffer.GetAttachment(0);
        }

        public FrameBuffer GetLastResultFrameBuffer()
        {
            return _frameBuffer;
        }

        public override void CleanUp()
        {
            _depthBasedBlurShader.cleanUp();
            _frameBuffer.cleanUp();
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
            _frameBuffer.resize(eventArgs.Size / _downscalingFactor);
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
            throw new NotImplementedException();
        }

        internal override void Finish(RenderEngine renderEngine)
        {
            throw new NotImplementedException();
        }

        internal override void Render(RenderEngine renderEngine)
        {
            throw new NotImplementedException();
        }
    }
}
