using Dino_Engine.Core;
using Dino_Engine.Rendering.Renderers.Lighting;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.ECS;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    /// <summary>
    /// The glow around what's too bright to look at. Light over <see cref="LightingSettings.BloomThreshold"/> is picked out,
    /// blurred down a chain of half-sized buffers (Jimenez, "Next Generation Post Processing in Call of Duty", 2014) and
    /// added back on the way up. The chain stops well short of the size of the screen: its last steps are almost one colour,
    /// and adding that is a veil over the whole picture. Emission is in the scene by now (Ambient_Light.frag), so this only
    /// ever spreads light that is really there.
    /// </summary>
    internal class BloomRenderer : RenderPassRenderer
    {
        private ShaderProgram downsamplingShader = new ShaderProgram("Simple.vert", "Downsampling.frag");
        private ShaderProgram upsamplingShader = new ShaderProgram("Simple.vert", "Upsampling.frag");
        private ShaderProgram bloomFilterShader = new ShaderProgram("Simple.vert", "bloom_Filter.frag");
        private const int downSamples = 6;
        public FrameBuffer[] sampleFramebuffers = new FrameBuffer[downSamples];
        internal BloomRenderer() : base("Bloom")
        {
            bloomFilterShader.bind();
            bloomFilterShader.loadUniformInt("shadedInput", 0);
            bloomFilterShader.unBind();

            downsamplingShader.bind();
            downsamplingShader.loadUniformInt("srcTexture", 0);
            downsamplingShader.unBind();

            upsamplingShader.bind();
            upsamplingShader.loadUniformInt("srcTexture", 0);
            upsamplingShader.unBind();

            Vector2i resolution = Engine.Resolution;

            for (int i = 0; i < downSamples; i++)
            {
                resolution /= 2;

                FrameBufferSettings settings = new FrameBufferSettings(resolution);
                DrawBufferSettings drawSettings = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
                drawSettings.formatInternal = PixelInternalFormat.Rgba16f;
                drawSettings.formatExternal = PixelFormat.Rgba;
                drawSettings.pixelType = PixelType.Float;
                drawSettings.wrapMode = TextureWrapMode.ClampToEdge;
                drawSettings.minFilterType = TextureMinFilter.Linear;
                drawSettings.magFilterType = TextureMagFilter.Linear;
                settings.drawBuffers.Add(drawSettings);
                sampleFramebuffers[i] = new FrameBuffer(settings);

            }
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {
            LightingSettings lighting = renderEngine.Lighting;
            if (!lighting.Bloom) return;

            DualBuffer buffer = renderEngine.lastUsedBuffer;
            ScreenQuadRenderer renderer = renderEngine.ScreenQuadRenderer;

            // Into the other half of the dual buffer, which leaves the scene in the half the bloom is added to at the end.
            bloomFilterShader.bind();
            bloomFilterShader.loadUniformFloat("threshold", lighting.BloomThreshold);
            bloomFilterShader.loadUniformFloat("knee", lighting.BloomKnee);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, buffer.GetLastOutputTexture());
            buffer.RenderToNextFrameBuffer();

            downsamplingShader.bind();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, buffer.GetLastOutputTexture());

            Vector2i source = buffer.getResolution();
            for (int i = 0; i < downSamples; i++)
            {
                sampleFramebuffers[i].bind();
                downsamplingShader.loadUniformVector2f("srcResolution", source);
                downsamplingShader.loadUniformInt("mipLevel", i);
                renderer.Render(clearColor: true);
                GL.BindTexture(TextureTarget.Texture2D, sampleFramebuffers[i].GetAttachment(0));
                source = sampleFramebuffers[i].getResolution();
            }

            upsamplingShader.bind();
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            upsamplingShader.loadUniformFloat("strength", 1f);
            for (int i = downSamples - 1; i > 0; i--)
            {
                FrameBuffer current = sampleFramebuffers[i];

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, current.GetAttachment(0));

                sampleFramebuffers[i - 1].bind();
                upsamplingShader.loadUniformVector2f("srcResolution", current.getResolution());
                renderer.Render(blend: true, clearColor: false);
            }

            // Every level of the chain has been summed, so what comes out is shared between them to keep the light it started with.
            GL.BindTexture(TextureTarget.Texture2D, sampleFramebuffers[0].GetAttachment(0));
            buffer.GetNextFrameBuffer().bind();
            upsamplingShader.loadUniformVector2f("srcResolution", sampleFramebuffers[0].getResolution());
            upsamplingShader.loadUniformFloat("strength", lighting.BloomStrength / downSamples);
            renderer.Render(blend: true, clearColor: false);
            buffer.StepToggle();

            Engine.WindowHandler.refreshViewport();

        }

        public override void CleanUp()
        {
            downsamplingShader.cleanUp();
            upsamplingShader.cleanUp();
            bloomFilterShader.cleanUp();

            foreach(FrameBuffer framBuffer in sampleFramebuffers)
            {
                framBuffer.cleanUp();
            }
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
            Vector2i resolution = eventArgs.Size;
            for (int i = 0; i < downSamples; i++)
            {
                resolution /= 2;
                sampleFramebuffers[i].resize(resolution);
            }
        }
    }
}
