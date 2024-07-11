using Dino_Engine.Core;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using System.Net.Mail;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    internal class BloomRenderer : Renderer
    {
        private ShaderProgram downsamplingShader = new ShaderProgram("Simple.vert", "Downsampling.frag");
        private ShaderProgram upsamplingShader = new ShaderProgram("Simple.vert", "Upsampling.frag");
        private ShaderProgram bloomFilterShader = new ShaderProgram("Simple.vert", "bloom_Filter.frag");
        private const int downSamples = 9;
        public FrameBuffer[] sampleFramebuffers = new FrameBuffer[downSamples];
        internal BloomRenderer()
        {
            bloomFilterShader.bind();
            bloomFilterShader.loadUniformInt("shadedInput", 0);
            bloomFilterShader.loadUniformInt("gMaterials", 1);
            bloomFilterShader.loadUniformInt("gAlbedo", 2);
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

        public void Render(ScreenQuadRenderer renderer, FrameBuffer gBuffer)
        {
            bloomFilterShader.bind();
            bloomFilterShader.loadUniformFloat("bloomStrength", 0.002f);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, renderer.GetLastOutputTexture());
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(3));
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(0));
            renderer.RenderToNextFrameBuffer();

            downsamplingShader.bind();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, renderer.GetLastOutputTexture());

            for (int i = 0; i < downSamples; i++)
            {
                sampleFramebuffers[i].bind();
                downsamplingShader.loadUniformVector2f("srcResolution", sampleFramebuffers[i].getResolution());
                downsamplingShader.loadUniformInt("mipLevel", i);
                renderer.Render(clearColor: true);
                GL.BindTexture(TextureTarget.Texture2D, sampleFramebuffers[i].GetAttachment(0));
            }
            //
            //renderer.GetLastFrameBuffer().resolveToScreen();

            upsamplingShader.bind();
            upsamplingShader.loadUniformFloat("filterRadius", 0.001f);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            for (int i = downSamples-1 ; i > 0; i--)
            {
                FrameBuffer current = sampleFramebuffers[i];
                FrameBuffer next = sampleFramebuffers[i-1];

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, current.GetAttachment(0));

                next.bind();

                //upsamplingShader.loadUniformFloat("filterRadius", 0.001f);
                upsamplingShader.loadUniformVector2f("srcResolution", current.getResolution());
                if (i == 1)
                {
                    //renderer.GetLastFrameBuffer().bind();
                    renderer.GetNextFrameBuffer().bind();
                    renderer.Render(blend: true, clearColor: false);
                }
                else
                {
                    renderer.Render(blend: true, clearColor: false);
                }

            }
            renderer.StepToggle();

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

        public override void Update()
        {
        }
    }
}
