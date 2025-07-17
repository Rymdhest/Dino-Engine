using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.Util;
using Dino_Engine.Core;

namespace Dino_Engine.Rendering.Renderers.PosGeometry
{
    internal class SkyRenderer : Renderer
    {
        private ShaderProgram skyShader = new ShaderProgram("Simple.vert", "sky.frag");

        public static Colour SkyColour = new Colour(0.21f, 0.43f, 0.99f, 1.0f);
        public static Colour HorizonColour = new Colour(0.9f, 0.5f, 0.39f, 1.0f);

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

        }

        internal override void Finish(RenderEngine renderEngine)
        {
            GL.DepthFunc(DepthFunction.Less);
        }

        internal override void Render(RenderEngine renderEngine)
        {

            ScreenQuadRenderer renderer = renderEngine.ScreenQuadRenderer;

            //Vector4 sunDirectionViewSpace = new Vector4(sunDirection.X, sunDirection.Y, sunDirection.Z, 1.0f) * Matrix4.Transpose(Matrix4.Invert(viewMatrix));

            skyShader.loadUniformVector3f("skyColor", SkyColour.ToVector3());

            skyShader.loadUniformVector3f("horizonColor", HorizonColour.ToVector3());


            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
            //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            renderer.Render(depthTest: true, blend: true, clearColor: false);
            //renderer.StepToggle();

        }

        public override void CleanUp()
        {
            skyShader.cleanUp();
        }
    }
}
