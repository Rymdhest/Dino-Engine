using Dino_Engine.Core;
using Dino_Engine.ECS;
using OpenTK.Windowing.Common;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    /// <summary>FXAA 3.11 over the tone-mapped image, when <see cref="AntiAliasing.AntiAliasingSettings.Fxaa"/> is on.</summary>
    internal class FXAARenderer : RenderPassRenderer
    {
        private ShaderProgram FXAAShader = new ShaderProgram("Simple.vert", "FXAA.frag");

        public FXAARenderer() : base("FXAA")
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
            buffer.RenderTextureToNextFrameBuffer(buffer.GetLastOutputTexture());

            FXAAShader.unBind();
        }
        public override void CleanUp()
        {
            FXAAShader.cleanUp();
        }

    }
}
