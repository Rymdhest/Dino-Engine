using Dino_Engine.Core;
using OpenTK.Windowing.Common;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    internal class ToneMapRenderer : Renderer
    {
        private ShaderProgram HDRMapShader = new ShaderProgram("Simple_Vertex", "HDR_Mapper_Fragment");

        public ToneMapRenderer()
        {
            HDRMapShader.bind();
            HDRMapShader.loadUniformInt("HDRcolorTexture", 0);
            HDRMapShader.unBind();
        }

        public void Render(ScreenQuadRenderer renderer)
        {
            HDRMapShader.bind();

            HDRMapShader.loadUniformVector2f("resolution", Engine.WindowHandler.Size);

            HDRMapShader.loadUniformFloat("exposure", 0.3f);
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
