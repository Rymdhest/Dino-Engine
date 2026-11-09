using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.Core;
using Dino_Engine.Rendering.Renderers.PostProcessing;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    /// <summary>
    /// Screen-space ambient occlusion from the single-sample G-buffer, blurred. The light passes take it off each
    /// sample's own ambient occlusion (Lighting/gBufferSample.glsl), reading <see cref="OcclusionTexture"/>.
    /// </summary>
    internal class SSAORenderer : RenderPassRenderer
    {
        private ShaderProgram ambientOcclusionShader = new ShaderProgram("Simple.vert", "AmbientOcclusion.frag");
        private FrameBuffer _SSAOFramebuffer;
        public int noiseScale = 4;
        private const int kernelSize = 16;
        private Vector3[] kernelSamples;
        private int noiseTexture;
        private int _downscalingFactor = 1;
        /// <summary>This frame's blurred occlusion (1 = open, 0 = occluded), once the pass has run.</summary>
        public int OcclusionTexture { get; private set; }

        internal SSAORenderer() : base("SSAO")
        {
            // Timed in parts, which can't nest inside a timer of the whole.
            trackPerformance = false;

            ambientOcclusionShader.bind();
            ambientOcclusionShader.loadUniformInt("texNoise", 0);
            ambientOcclusionShader.loadUniformInt("gNormal", 1);
            ambientOcclusionShader.loadUniformInt("gDepth", 3);
            ambientOcclusionShader.unBind();

            kernelSamples = new Vector3[kernelSize];
            for (int i = 0; i < kernelSize; i++)
            {
                Vector3 sample = new Vector3(MyMath.rngMinusPlus(), MyMath.rngMinusPlus(), MyMath.rand.NextSingle());
                sample.Normalize();
                sample *= MyMath.rand.NextSingle();
                float scale = (float)i / kernelSize;
                //scale = 0.1f + scale * scale * (1f - 0.1f);
                sample *= scale;
                kernelSamples[i] = sample;
            }

            var noisePixels = new float[3 * noiseScale * noiseScale];
            for (int i = 0; i < noiseScale * noiseScale; i++)
            {
                noisePixels[i * 3] = MyMath.rngMinusPlus();
                noisePixels[i * 3 + 1] = MyMath.rngMinusPlus();
                noisePixels[i * 3 + 2] = 0;
            }
            noiseTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, noiseTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, noiseScale, noiseScale, 0, PixelFormat.Rgb, PixelType.Float, noisePixels);
            // Nearest: blending between the rotations leaves smooth patches where the noise should break up the kernel's pattern.
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (float)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (float)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (float)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (float)TextureWrapMode.Repeat);



            FrameBufferSettings settings = new FrameBufferSettings(Engine.Resolution / _downscalingFactor);
            DrawBufferSettings drawSettings = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            drawSettings.formatInternal = PixelInternalFormat.R8;
            drawSettings.pixelType = PixelType.UnsignedByte;
            drawSettings.wrapMode = TextureWrapMode.ClampToEdge;
            drawSettings.minFilterType = TextureMinFilter.Linear;
            drawSettings.magFilterType = TextureMagFilter.Linear;
            settings.drawBuffers.Add(drawSettings);
            _SSAOFramebuffer = new FrameBuffer(settings);
        }
        internal override void Prepare(RenderEngine renderEngine)
        {
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {
            FrameBuffer gBuffer = renderEngine.GBuffer;
            Vector2i resolution = Engine.Resolution;
            //GaussianBlurRenderer gaussianBlurRenderer = renderEngine.GaussianBlurRenderer;
            DepthBasedBlurRenderer depthBasedBlurRenderer = renderEngine.DepthBasedBlurRenderer;

            Engine.PerformanceMonitor.startGPUTask("SSAO Samples");
            ambientOcclusionShader.bind();
            ambientOcclusionShader.loadUniformVector2f("noiseScale", new Vector2(resolution.X / noiseScale, resolution.Y / noiseScale));
            ambientOcclusionShader.loadUniformVector3fArray("samples", kernelSamples);

            // While global illumination is in, it has every occluder further than a contact: this is only the contact (GI.md).
            float gi = renderEngine._globalIllumination.Active ? renderEngine._globalIllumination.Presence : 0f;
            ambientOcclusionShader.loadUniformFloat("radius", MathHelper.Lerp(0.9f, renderEngine.GlobalIllumination.ContactReach, gi));
            ambientOcclusionShader.loadUniformFloat("strength", 1.0f);
            ambientOcclusionShader.loadUniformFloat("bias", 0.01f);

            ambientOcclusionShader.loadUniformVector2f("resolutionSSAO", _SSAOFramebuffer.getResolution());

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, noiseTexture);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(1));
            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.getDepthAttachment());
            _SSAOFramebuffer.bind();
            Engine.RenderEngine.ScreenQuadRenderer.Render();
            ambientOcclusionShader.unBind();

            Engine.PerformanceMonitor.finishGPUTask("SSAO Samples");
            Engine.PerformanceMonitor.startGPUTask("SSAO Blur");
            //gaussianBlurRenderer.Render(_SSAOFramebuffer, 1, renderEngine.ScreenQuadRenderer, 0);
            depthBasedBlurRenderer.Render(_SSAOFramebuffer, gBuffer.getDepthAttachment(), 1, renderEngine.ScreenQuadRenderer, 0);
            Engine.PerformanceMonitor.finishGPUTask("SSAO Blur");
            //_SSAOFramebuffer.resolveToScreen();
            //gaussianBlurRenderer.GetLastResultFrameBuffer().resolveToScreen();
            //depthBasedBlurRenderer.GetLastResultFrameBuffer().resolveToScreen();
            OcclusionTexture = depthBasedBlurRenderer.GetLastResultTexture();
        }

        public override void CleanUp()
        {
            ambientOcclusionShader.cleanUp();
            GL.DeleteTexture(noiseTexture);
            _SSAOFramebuffer.cleanUp();
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
            _SSAOFramebuffer.resize(eventArgs.Size / _downscalingFactor);
        }

        public override void Update()
        {
        }


    }
}
