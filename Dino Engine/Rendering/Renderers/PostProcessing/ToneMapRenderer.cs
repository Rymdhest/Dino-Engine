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
        internal override void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            HDRMapShader.bind();
        }

        internal override void Finish(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
        }

        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            ScreenQuadRenderer renderer = renderEngine.ScreenQuadRenderer;

            HDRMapShader.loadUniformVector2f("resolution", Engine.Resolution);

            HDRMapShader.loadUniformFloat("exposure", 0.97f);
            HDRMapShader.loadUniformFloat("gamma", 2.2f);
            HDRMapShader.loadUniformFloat("saturation", 1.0f);
            HDRMapShader.loadUniformFloat("brightness", 1.0f);
            HDRMapShader.loadUniformFloat("contrast", 1.0f);
            HDRMapShader.loadUniformFloat("dithering", 0.5f);

            //renderer.RenderToNextFrameBuffer();
            renderer.RenderTextureToNextFrameBuffer(renderer.GetLastOutputTexture());

            HDRMapShader.unBind();
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        public override void Update()
        {
        }
        public override void CleanUp()
        {
            HDRMapShader.cleanUp();
        }


    }
}
