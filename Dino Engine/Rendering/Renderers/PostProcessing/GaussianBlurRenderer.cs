using OpenTK.Windowing.Common;
using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.Core;
using Dino_Engine.ECS;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    public class GaussianBlurRenderer : Renderer
    {
        private ShaderProgram _gaussianBlurShader = new ShaderProgram("Gaussian_Blur.vert", "Gaussian_Blur.frag");
        private FrameBuffer _verticalFramebuffer;
        private FrameBuffer _horizontalFramebuffer;
        private int _downscalingFactor = 2;

        public GaussianBlurRenderer()
        {
            _gaussianBlurShader.bind();
            _gaussianBlurShader.loadUniformInt("originalTexture", 0);
            _gaussianBlurShader.unBind();

            FrameBufferSettings settings = new FrameBufferSettings(Engine.Resolution / _downscalingFactor);
            DrawBufferSettings drawSettings = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            drawSettings.formatInternal = PixelInternalFormat.Rgba16f;
            //drawSettings.formatExternal = PixelFormat.Rgb;
            drawSettings.pixelType = PixelType.Float;
            drawSettings.wrapMode = TextureWrapMode.ClampToEdge;
            drawSettings.minFilterType = TextureMinFilter.Linear;
            drawSettings.magFilterType = TextureMagFilter.Linear;
            settings.drawBuffers.Add(drawSettings);
            _verticalFramebuffer = new FrameBuffer(settings);
            _horizontalFramebuffer = new FrameBuffer(settings);
        }

        public void Render(FrameBuffer sourceBuffer, int blurRadius, ScreenQuadRenderer renderer)
        {
            float[] weights = new float[blurRadius*2+1];


            float totalWeight = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                float distance = MathF.Pow( MathF.Abs(i - blurRadius), 0.5f);
                float weight = 1f / (distance + 1);
                totalWeight += weight;
                weights[i] = weight;
            }
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] /= totalWeight;
            }

            for (int i = 0; i < weights.Length; i++)
            {
                //weights[i] = 1f / (weights.Length);
            }
            _verticalFramebuffer.bind();
            Vector2 textureSize = sourceBuffer.getResolution();
            Vector2 pixelSize = new Vector2(1f)/ textureSize;
            _gaussianBlurShader.bind();
            _gaussianBlurShader.loadUniformFloat("pixelSize", pixelSize.X);
            _gaussianBlurShader.loadUniformInt("blurRadius", blurRadius);
            _gaussianBlurShader.loadUniformVector2f("blurAxis", new Vector2(1f, 0f));
            _gaussianBlurShader.loadUniformFloatArray("weights", weights);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, sourceBuffer.GetAttachment(0));
            renderer.Render();

            _horizontalFramebuffer.bind();
            _gaussianBlurShader.loadUniformVector2f("blurAxis", new Vector2(0f, 1f));
            _gaussianBlurShader.loadUniformFloat("pixelSize", pixelSize.Y);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _verticalFramebuffer.GetAttachment(0));
            renderer.Render();

        }

        public int GetLastResultTexture()
        {
            return _horizontalFramebuffer.GetAttachment(0);
        }

        public override void CleanUp()
        {
            _gaussianBlurShader.cleanUp();
            _horizontalFramebuffer.cleanUp();
            _verticalFramebuffer.cleanUp();
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
            _verticalFramebuffer.resize(eventArgs.Size / _downscalingFactor);
            _horizontalFramebuffer.resize(eventArgs.Size / _downscalingFactor);
        }

        public override void Update()
        {
        }

        internal override void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            throw new NotImplementedException();
        }

        internal override void Finish(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            throw new NotImplementedException();
        }

        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            throw new NotImplementedException();
        }
    }
}
