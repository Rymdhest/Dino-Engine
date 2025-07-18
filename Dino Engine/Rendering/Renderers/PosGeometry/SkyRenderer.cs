using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.Util;
using Dino_Engine.Core;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.PosGeometry
{

    public class SkyRenderer : Renderer
    {
        private ShaderProgram skyShader = new ShaderProgram("Simple.vert", "sky.frag");

        public SkyRenderer()
        {

        }
        internal override void Prepare(RenderEngine renderEngine)
        {
            DualBuffer buffer = renderEngine.lastUsedBuffer;
            skyShader.bind();
            buffer.GetLastFrameBuffer().bind();
            GL.DepthFunc(DepthFunction.Lequal);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, buffer.GetLastOutputTexture());


            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

        }

        internal override void Finish(RenderEngine renderEngine)
        {
            GL.DepthFunc(DepthFunction.Less);
        }

        public override void CleanUp()
        {
            skyShader.cleanUp();
        }

        internal override void Render(RenderEngine renderEngine)
        {
            renderEngine.ScreenQuadRenderer.Render(depthTest: true, blend: true, clearColor: false);
        }
    }
}
