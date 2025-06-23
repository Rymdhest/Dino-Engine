using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Dino_Engine.ECS;
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
        internal override void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            DualBuffer buffer = renderEngine.lastUsedBuffer;
            skyShader.bind();
            buffer.GetLastFrameBuffer().bind();
            GL.DepthFunc(DepthFunction.Lequal);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, buffer.GetLastOutputTexture());

        }

        internal override void Finish(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            GL.DepthFunc(DepthFunction.Less);
        }

        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {

            ScreenQuadRenderer renderer = renderEngine.ScreenQuadRenderer;
            Matrix4 viewMatrix = MyMath.createViewMatrix(eCSEngine.Camera.getComponent<TransformationComponent>().Transformation);
            Matrix4 projectionMatrix = eCSEngine.Camera.getComponent<ProjectionComponent>().ProjectionMatrix;
            Vector3 viewPosition = eCSEngine.Camera.getComponent<TransformationComponent>().Transformation.position;


            skyShader.loadUniformVector3f("viewPositionWorld", viewPosition);
            skyShader.loadUniformMatrix4f("viewMatrix", viewMatrix);
            skyShader.loadUniformMatrix4f("projectionMatrix", projectionMatrix);

            skyShader.loadUniformVector2f("screenResolution", Engine.Resolution);
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

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        public override void Update()
        {
        }


    }
}
