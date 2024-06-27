using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Dino_Engine.ECS;
using Dino_Engine.Util;
using Dino_Engine.Core;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    internal class SkyRenderer : Renderer
    {
        private ShaderProgram skyShader = new ShaderProgram("Simple_Vertex", "sky_Fragment");

        public static Colour SkyColour = new Colour(0.2f, 0.5f, 0.99f, 1.0f);
        public static Colour HorizonColour = new Colour(0.9f, 0.5f, 0.39f, 1.0f);

        public SkyRenderer()
        {

        }

        public void Render(ECSEngine eCSEngine, ScreenQuadRenderer renderer, FrameBuffer gBuffer)
        {
            //renderer.getNextFrameBuffer().blitDepthBufferFrom(gBuffer);
            //renderer.getLastFrameBuffer().blitDepthBufferFrom(gBuffer);
            /*
            skyShader.cleanUp();
            skyShader = new ShaderProgram("Simple_Vertex", "sky_Fragment");
            */

            Matrix4 viewMatrix = MyMath.createViewMatrix(eCSEngine.Camera.getComponent<TransformationComponent>().Transformation);
            Matrix4 projectionMatrix = eCSEngine.Camera.getComponent<ProjectionComponent>().ProjectionMatrix;
            Vector3 viewPosition = eCSEngine.Camera.getComponent<TransformationComponent>().Transformation.position;


            skyShader.bind();
            skyShader.loadUniformVector3f("viewPositionWorld", viewPosition);
            skyShader.loadUniformMatrix4f("viewMatrix", viewMatrix);
            skyShader.loadUniformMatrix4f("projectionMatrix", projectionMatrix);

            skyShader.loadUniformVector2f("screenResolution", Engine.Resolution);
            //Vector4 sunDirectionViewSpace = new Vector4(sunDirection.X, sunDirection.Y, sunDirection.Z, 1.0f) * Matrix4.Transpose(Matrix4.Invert(viewMatrix));

            skyShader.loadUniformVector3f("skyColor", SkyColour.ToVector3());

            skyShader.loadUniformVector3f("horizonColor", HorizonColour.ToVector3());

            renderer.GetLastFrameBuffer().bind();

            GL.DepthFunc(DepthFunction.Lequal);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, renderer.GetLastOutputTexture());

            //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            renderer.Render(depthTest: true, depthMask: false, blend: false, clearColor: false);
            //renderer.stepToggle();
            skyShader.unBind();

            GL.DepthFunc(DepthFunction.Less);
        }

        public override void CleanUp()
        {
            skyShader.cleanUp();
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        public override void Update()
        {
            SkyColour = new Colour(0.2f, 0.5f, 0.99f, 4.0f);
            HorizonColour = new Colour(0.9f, 0.5f, 0.39f, 10.0f);
    }
    }
}
