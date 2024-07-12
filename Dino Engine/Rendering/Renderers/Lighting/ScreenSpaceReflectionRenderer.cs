using OpenTK.Windowing.Common;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.ECS;
using Dino_Engine.Util;
using Dino_Engine.Core;
using Dino_Engine.Rendering.Renderers.PostProcessing;
using Dino_Engine.Rendering.Renderers.PosGeometry;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    internal class ScreenSpaceReflectionRenderer : Renderer
    {
        private ShaderProgram ScreenSpaceReflectionShader = new ShaderProgram("Simple.vert", "Screen_Reflection.frag");
        private ShaderProgram combineReflectionShader = new ShaderProgram("Simple.vert", "Combine_Reflection.frag");
        private FrameBuffer _reflectionFramebuffer;
        private int _downscalingFactor = 2;

        public ScreenSpaceReflectionRenderer()
        {
            ScreenSpaceReflectionShader.bind();
            ScreenSpaceReflectionShader.loadUniformInt("shadedColor", 0);
            ScreenSpaceReflectionShader.loadUniformInt("gNormal", 1);
            ScreenSpaceReflectionShader.loadUniformInt("gPosition", 2);
            ScreenSpaceReflectionShader.loadUniformInt("gMaterials", 3);
            ScreenSpaceReflectionShader.unBind();

            combineReflectionShader.bind();
            combineReflectionShader.loadUniformInt("sourceColorTexture", 0);
            combineReflectionShader.loadUniformInt("reflectionTexture", 1);
            combineReflectionShader.loadUniformInt("gMaterials", 2);
            combineReflectionShader.unBind();

            FrameBufferSettings settings = new FrameBufferSettings(Engine.Resolution / _downscalingFactor);
            DrawBufferSettings drawSettings = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            drawSettings.formatInternal = PixelInternalFormat.Rgba16f;
            drawSettings.pixelType = PixelType.Float;
            drawSettings.wrapMode = TextureWrapMode.ClampToEdge;
            drawSettings.minFilterType = TextureMinFilter.Linear;
            drawSettings.magFilterType = TextureMagFilter.Linear;
            settings.drawBuffers.Add(drawSettings);
            _reflectionFramebuffer = new FrameBuffer(settings);
        }
        internal override void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
        }

        internal override void Finish(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
        }

        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {

            ScreenQuadRenderer renderer = renderEngine.ScreenQuadRenderer;
            FrameBuffer gBuffer = renderEngine.GBuffer;
            GaussianBlurRenderer gaussianBlurRenderer = renderEngine.GaussianBlurRenderer;

            Matrix4 projectionMatrix = eCSEngine.Camera.getComponent<ProjectionComponent>().ProjectionMatrix;

            ScreenSpaceReflectionShader.bind();
            _reflectionFramebuffer.bind();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, renderer.GetLastOutputTexture());
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(1));
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(2));
            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(3));



            ScreenSpaceReflectionShader.loadUniformMatrix4f("projectionMatrix", projectionMatrix);
            ScreenSpaceReflectionShader.loadUniformVector3f("skyColor", SkyRenderer.SkyColour.ToVector3());
            ScreenSpaceReflectionShader.loadUniformFloat("rayStep", 3.3f);
            ScreenSpaceReflectionShader.loadUniformInt("iterationCount", 25);
            ScreenSpaceReflectionShader.loadUniformInt("binaryIterationCount", 40);
            ScreenSpaceReflectionShader.loadUniformFloat("distanceBias", 0.00002f);
            ScreenSpaceReflectionShader.loadUniformBool("isBinarySearchEnabled", true);
            ScreenSpaceReflectionShader.loadUniformBool("debugDraw", false);
            ScreenSpaceReflectionShader.loadUniformFloat("stepExponent", 1.3f);

            renderer.Render();
            ScreenSpaceReflectionShader.unBind();


            gaussianBlurRenderer.Render(_reflectionFramebuffer, 5, renderer);

            combineReflectionShader.bind();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, renderer.GetLastOutputTexture());
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, gaussianBlurRenderer.GetLastResultTexture());
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(3));

            renderer.RenderToNextFrameBuffer();
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

        public override void Update()
        {

        }


    }
}
