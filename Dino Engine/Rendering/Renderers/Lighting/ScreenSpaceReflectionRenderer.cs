﻿using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.Core;
using Dino_Engine.Rendering.Renderers.PostProcessing;
using Dino_Engine.Rendering.Renderers.PosGeometry;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    internal class ScreenSpaceReflectionRenderer : RenderPassRenderer
    {
        private ShaderProgram ScreenSpaceReflectionShader = new ShaderProgram("Simple.vert", "Screen_Reflection.frag");
        private ShaderProgram combineReflectionShader = new ShaderProgram("Simple.vert", "Combine_Reflection.frag");
        private FrameBuffer _reflectionFramebuffer;
        private int _downscalingFactor = 2;

        public ScreenSpaceReflectionRenderer() : base("Screen Space Reflection")
        {
            ScreenSpaceReflectionShader.bind();
            ScreenSpaceReflectionShader.loadUniformInt("shadedColor", 0);
            ScreenSpaceReflectionShader.loadUniformInt("gNormal", 1);
            ScreenSpaceReflectionShader.loadUniformInt("gMaterials", 2);
            ScreenSpaceReflectionShader.loadUniformInt("gDepth", 3);
            ScreenSpaceReflectionShader.unBind();

            combineReflectionShader.bind();
            combineReflectionShader.loadUniformInt("sourceColorTexture", 0);
            combineReflectionShader.loadUniformInt("reflectionTexture", 1);
            combineReflectionShader.loadUniformInt("gMaterials", 2);
            combineReflectionShader.unBind();

            FrameBufferSettings settings = new FrameBufferSettings(Engine.Resolution / _downscalingFactor);
            DrawBufferSettings drawSettings = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            drawSettings.formatInternal = PixelInternalFormat.Rgba16f; // need to be HDR for lighting
            drawSettings.pixelType = PixelType.Float;
            drawSettings.wrapMode = TextureWrapMode.ClampToEdge;
            drawSettings.minFilterType = TextureMinFilter.Linear;
            drawSettings.magFilterType = TextureMagFilter.Linear;
            settings.drawBuffers.Add(drawSettings);
            _reflectionFramebuffer = new FrameBuffer(settings);
        }
        internal override void Prepare(RenderEngine renderEngine)
        {
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {

            DualBuffer buffer = renderEngine.lastUsedBuffer;
            ScreenQuadRenderer renderer = renderEngine.ScreenQuadRenderer;
            FrameBuffer gBuffer = renderEngine.GBuffer;
            GaussianBlurRenderer gaussianBlurRenderer = renderEngine.GaussianBlurRenderer;

            ScreenSpaceReflectionShader.bind();
            _reflectionFramebuffer.bind();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, buffer.GetLastOutputTexture());
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(1));
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(2));
            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.getDepthAttachment());

            ScreenSpaceReflectionShader.loadUniformVector2f("resolutionSSR", _reflectionFramebuffer.getResolution());
            ScreenSpaceReflectionShader.loadUniformFloat("rayStep", 0.10f);
            ScreenSpaceReflectionShader.loadUniformInt("iterationCount", 40);
            ScreenSpaceReflectionShader.loadUniformInt("binaryIterationCount", 50);
            ScreenSpaceReflectionShader.loadUniformFloat("distanceBias", 0.001f);
            ScreenSpaceReflectionShader.loadUniformBool("isBinarySearchEnabled", true);
            ScreenSpaceReflectionShader.loadUniformBool("debugDraw", false);
            ScreenSpaceReflectionShader.loadUniformFloat("stepExponent", 1.2f);

            renderer.Render();
            ScreenSpaceReflectionShader.unBind();


            gaussianBlurRenderer.Render(_reflectionFramebuffer, 6, renderer);

            combineReflectionShader.bind();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, buffer.GetLastOutputTexture());
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, gaussianBlurRenderer.GetLastResultTexture());
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(2));

            buffer.RenderToNextFrameBuffer();
        }

        public override void CleanUp()
        {
            ScreenSpaceReflectionShader.cleanUp();
            combineReflectionShader.cleanUp();
            _reflectionFramebuffer.cleanUp();
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
            _reflectionFramebuffer.resize(eventArgs.Size / _downscalingFactor);
        }
    }
}
