using Dino_Engine.Core;
using OpenTK.Windowing.Common;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    internal class FXAARenderer : Renderer
    {
        private ShaderProgram FXAAShader = new ShaderProgram("Simple_Vertex", "FXAA_Fragment");

        public FXAARenderer()
        {
            FXAAShader.bind();
            FXAAShader.loadUniformInt("l_tex", 0);
            FXAAShader.unBind();
        }

        public void Render(ScreenQuadRenderer renderer)
        {

            FXAAShader.bind();

            FXAAShader.loadUniformVector2f("win_size", Engine.WindowHandler.Size);
            FXAAShader.loadUniformFloat("reduceMin", 128.0f);
            FXAAShader.loadUniformFloat("reduceMul", 4.0f);
            FXAAShader.loadUniformFloat("spanMax", 16.0f);


            //renderer.RenderToNextFrameBuffer();
            renderer.RenderTextureToNextFrameBuffer(renderer.GetLastOutputTexture());

            FXAAShader.unBind();
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        public override void Update()
        {
        }
        public override void CleanUp()
        {
            FXAAShader.cleanUp();
        }
    }
}
