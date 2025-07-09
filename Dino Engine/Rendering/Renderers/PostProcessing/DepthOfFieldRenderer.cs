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
            _depthOfFieldShader.loadUniformInt("gDepth", 2);
            _depthOfFieldShader.unBind();
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {
            DualBuffer buffer = renderEngine.lastUsedBuffer;
            FrameBuffer gBuffer = renderEngine.GBuffer;
            GaussianBlurRenderer gaussianBlurRenderer = renderEngine.GaussianBlurRenderer;

            gaussianBlurRenderer.Render(buffer.GetLastFrameBuffer(), 5, renderEngine.ScreenQuadRenderer);

            _depthOfFieldShader.bind();
            _depthOfFieldShader.loadUniformFloat("range", 0.0003f);
            _depthOfFieldShader.loadUniformFloat("focusDistance", 0.0f);

            _depthOfFieldShader.loadUniformVector2f("resolution", Engine.Resolution);
            _depthOfFieldShader.loadUniformMatrix4f("inverseProjection", Engine.RenderEngine.context.invProjectionMatrix);


            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, buffer.GetLastOutputTexture());

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, gaussianBlurRenderer.GetLastResultTexture());

            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.getDepthAttachment());


            buffer.RenderToNextFrameBuffer();

            _depthOfFieldShader.unBind();
        }

        public override void CleanUp()
        {
            _depthOfFieldShader.cleanUp();
        }
    }
}
