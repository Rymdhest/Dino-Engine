using Dino_Engine.Core;
using Dino_Engine.ECS;
using OpenTK.Windowing.Common;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    internal class ToneMapRenderer : Renderer
    {
        private ShaderProgram HDRMapShader = new ShaderProgram("Simple.vert", "HDR_Mapper.frag");

        public ToneMapRenderer()
        {
            HDRMapShader.bind();
            HDRMapShader.loadUniformInt("HDRcolorTexture", 0);
            HDRMapShader.unBind();
        }
        internal override void Prepare(RenderEngine renderEngine)
        {
            HDRMapShader.bind();
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {
            DualBuffer buffer = renderEngine.lastUsedBuffer;

            HDRMapShader.loadUniformVector2f("resolution", Engine.Resolution);

            HDRMapShader.loadUniformFloat("exposure", 1.0515f);
            HDRMapShader.loadUniformFloat("gamma", 2.2f);
            HDRMapShader.loadUniformFloat("saturation", 1.0f);
            HDRMapShader.loadUniformFloat("brightness", 1.0f);
            HDRMapShader.loadUniformFloat("contrast", 1.0f);
            HDRMapShader.loadUniformFloat("dithering", 0.4f);

            //renderer.RenderToNextFrameBuffer();
            buffer.RenderTextureToNextFrameBuffer(buffer.GetLastOutputTexture());

            HDRMapShader.unBind();
        }

        public override void CleanUp()
        {
            HDRMapShader.cleanUp();
        }


    }
}
