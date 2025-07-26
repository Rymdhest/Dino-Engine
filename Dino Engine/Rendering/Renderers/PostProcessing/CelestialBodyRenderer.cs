using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.Core;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    public struct CelestialBodyRenderCommand : IRenderCommand
    {
        public Vector3 colour;
        public Vector3 direction;
    }

    public class CelestialBodyRenderer : CommandDrivenRenderer<CelestialBodyRenderCommand>
    {

        private ShaderProgram _sunRayShader = new ShaderProgram("Simple.vert", "SunRay.frag");
        private ShaderProgram _sunFilterShader = new ShaderProgram("Simple.vert", "SunFilter.frag");
        private ShaderProgram _CelestialPassthroughShader = new ShaderProgram("Simple.vert", "CelestialBody_Passthrough.frag");
        private FrameBuffer _CelestialBodyFramebuffer;
        private int _downscalingFactor = 2;

        public CelestialBodyRenderer() : base("Celestial Body")
        {
            _sunRayShader.bind();
            _sunRayShader.loadUniformInt("sunTexture", 0);
            _sunRayShader.unBind();

            FrameBufferSettings settings = new FrameBufferSettings(Engine.Resolution / _downscalingFactor);
            DrawBufferSettings drawSettings = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            drawSettings.formatInternal = PixelInternalFormat.Rgba16f;
            drawSettings.pixelType = PixelType.Float;
            drawSettings.wrapMode = TextureWrapMode.ClampToEdge;
            drawSettings.minFilterType = TextureMinFilter.Linear;
            drawSettings.magFilterType = TextureMagFilter.Linear;
            settings.drawBuffers.Add(drawSettings);
            _CelestialBodyFramebuffer = new FrameBuffer(settings);

            _CelestialPassthroughShader.bind();
            _CelestialPassthroughShader.loadUniformInt("inputTexture", 0);
            _CelestialPassthroughShader.unBind();
        }


        public override void CleanUp()
        {
            _sunRayShader.cleanUp();
            _CelestialPassthroughShader.cleanUp();
            _sunFilterShader.cleanUp();
            _CelestialBodyFramebuffer.cleanUp();
        }

        internal override void Finish(RenderEngine renderEngine)
        {
            GL.DepthFunc(DepthFunction.Less);
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
            GL.DepthFunc(DepthFunction.Lequal);
        }

        public override void PerformCommand(CelestialBodyRenderCommand command, RenderEngine renderEngine)
        {
            ScreenQuadRenderer renderer = renderEngine.ScreenQuadRenderer;
            DualBuffer buffer = renderEngine.lastUsedBuffer;
            _sunFilterShader.bind();
            _sunFilterShader.loadUniformVector3f("sunColour", command.colour);
            _sunFilterShader.loadUniformVector3f("sunDirection", -command.direction);
            _sunFilterShader.loadUniformFloat("exponent", 2.5f);

            buffer.GetNextFrameBuffer().bind();
            renderer.Render(depthTest: true, clearColor: true);


            _CelestialBodyFramebuffer.bind();
            _sunRayShader.bind();
            _sunRayShader.loadUniformFloat("Density", 0.2f);
            _sunRayShader.loadUniformFloat("Weight", 0.3f);
            _sunRayShader.loadUniformFloat("Exposure", 0.25f);
            _sunRayShader.loadUniformFloat("Decay", .92f);
            _sunRayShader.loadUniformFloat("illuminationDecay", 0.9f);
            _sunRayShader.loadUniformVector2f("celestialRayResolution", _CelestialBodyFramebuffer.getResolution());
            _sunRayShader.loadUniformInt("samples", 25);
            _sunRayShader.loadUniformVector3f("sunDirection", -command.direction);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, buffer.GetNextOutputTexture());
            renderer.Render(depthTest: false, clearColor: true, blend: false);

            _CelestialPassthroughShader.bind();
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _CelestialBodyFramebuffer.GetAttachment(0));
            buffer.GetLastFrameBuffer().bind();

            renderer.Render(depthTest: false, clearColor: false, blend: true);
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
            _CelestialBodyFramebuffer.resize(eventArgs.Size / _downscalingFactor);
        }
    }
}
