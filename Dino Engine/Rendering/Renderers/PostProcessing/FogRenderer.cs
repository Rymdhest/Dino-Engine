using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.Core;
using Dino_Engine.Rendering.Renderers.PosGeometry;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    internal class FogRenderer : Renderer
    {
        private ShaderProgram fogShader = new ShaderProgram("Simple.vert", "Fog.frag");
        private float time = 0;
        public FogRenderer()
        {
            fogShader.bind();
            fogShader.loadUniformInt("shadedColourTexture", 0);
            fogShader.loadUniformInt("gDepth", 1);
            fogShader.unBind();
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
            fogShader.bind();
            GL.DepthFunc(DepthFunction.Greater);
        }

        internal override void Finish(RenderEngine renderEngine)
        {
            GL.DepthFunc(DepthFunction.Less);
        }

        internal override void Render(RenderEngine renderEngine)
        {
            DualBuffer buffer = renderEngine.lastUsedBuffer;
            FrameBuffer gBuffer = renderEngine.GBuffer;


            fogShader.loadUniformFloat("fogDensity", 0.035f);
            fogShader.loadUniformFloat("heightFallOff", 0.0002f);
            fogShader.loadUniformFloat("noiseFactor", 0.9f);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, buffer.GetLastOutputTexture());
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.getDepthAttachment());
            buffer.RenderToNextFrameBuffer();

            buffer.GetNextFrameBuffer().bind();
            renderEngine.ScreenQuadRenderer.Render(depthTest: true, blend: false, clearColor: false);
            buffer.StepToggle();
            Engine.RenderEngine.lastUsedBuffer = buffer;

        }


        public override void CleanUp()
        {
            fogShader.cleanUp();
        }

        public override void Update()
        {
            time += Engine.Delta;
        }


    }
}
