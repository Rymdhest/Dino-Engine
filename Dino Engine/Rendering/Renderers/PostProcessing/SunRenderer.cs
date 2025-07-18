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

    public class SunRenderer : CommandDrivenRenderer<CelestialBodyRenderCommand>
    {

        private ShaderProgram _sunRayShader = new ShaderProgram("Simple.vert", "SunRay.frag");
        private ShaderProgram _sunFilterShader = new ShaderProgram("Simple.vert", "SunFilter.frag");
        private int downscale = 2;

        public SunRenderer()
        {
            _sunRayShader.bind();
            _sunRayShader.loadUniformInt("sunTexture", 0);
            _sunRayShader.unBind();
        }


        public override void CleanUp()
        {
            _sunRayShader.cleanUp();
            _sunFilterShader.cleanUp();
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
            _sunFilterShader.loadUniformFloat("exponent", 2.0f);

            buffer.GetNextFrameBuffer().bind();
            renderer.Render(depthTest: true, clearColor: true);


            _sunRayShader.bind();
            _sunRayShader.loadUniformFloat("Density", 0.2f);
            _sunRayShader.loadUniformFloat("Weight", 0.35f);
            _sunRayShader.loadUniformFloat("Exposure", 0.3f);
            _sunRayShader.loadUniformFloat("Decay", .9f);
            _sunRayShader.loadUniformFloat("illuminationDecay", 0.9f);
            _sunRayShader.loadUniformInt("samples", 30);
            _sunRayShader.loadUniformVector3f("sunDirection", -command.direction);


            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, buffer.GetNextFrameBuffer().GetAttachment(0));
            buffer.GetLastFrameBuffer().bind();

            renderer.Render(depthTest: false, clearColor: false, blend: true);
        }
    }
}
