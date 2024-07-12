using Dino_Engine.Core;
using Dino_Engine.ECS;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    public class DepthOfFieldRenderer : Renderer
    {
        private ShaderProgram _depthOfFieldShader = new ShaderProgram("Simple.vert", "Depth_Of_Field.frag");


        public DepthOfFieldRenderer()
        {
            _depthOfFieldShader.bind();
            _depthOfFieldShader.loadUniformInt("colorTexture", 0);
            _depthOfFieldShader.loadUniformInt("blurTexture", 1);
            _depthOfFieldShader.loadUniformInt("positionTexture", 2);
            _depthOfFieldShader.unBind();
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

            gaussianBlurRenderer.Render(renderer.GetLastFrameBuffer(), 6, renderer);

            _depthOfFieldShader.bind();
            _depthOfFieldShader.loadUniformFloat("range", .002f);
            _depthOfFieldShader.loadUniformFloat("focusDistance", 0.0f);


            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, renderer.GetLastOutputTexture());

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, gaussianBlurRenderer.GetLastResultTexture());

            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(2));


            renderer.RenderToNextFrameBuffer();

            _depthOfFieldShader.unBind();
        }

        public override void CleanUp()
        {
            _depthOfFieldShader.cleanUp();
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        public override void Update()
        {
        }




    }
}
