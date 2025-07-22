using Dino_Engine.Core;
using Dino_Engine.ECS;
using OpenTK.Windowing.Common;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    internal class FXAARenderer : RenderPassRenderer
    {
        private ShaderProgram FXAAShader = new ShaderProgram("Simple.vert", "FXAA.frag");

        public FXAARenderer()
        {
            FXAAShader.bind();
            FXAAShader.loadUniformInt("l_tex", 0);
            FXAAShader.unBind();
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
            FXAAShader.bind();
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {

            DualBuffer buffer = renderEngine.lastUsedBuffer;

            FXAAShader.loadUniformFloat("reduceMin", 128.0f);
            FXAAShader.loadUniformFloat("reduceMul", 16.0f);
            FXAAShader.loadUniformFloat("spanMax", 4.0f);


            //renderer.RenderToNextFrameBuffer();
            buffer.RenderTextureToNextFrameBuffer(buffer.GetLastOutputTexture());

            FXAAShader.unBind();
        }
        public override void CleanUp()
        {
            FXAAShader.cleanUp();
        }

    }
}
