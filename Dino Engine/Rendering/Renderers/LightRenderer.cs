using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using Dino_Engine.Rendering.Renderers;
using Dino_Engine.ECS;
using Dino_Engine.Util;
using Dino_Engine.Modelling;
using Dino_Engine.Core;
using static OpenTK.Graphics.OpenGL.GL;
using Dino_Engine.ECS.Components;


namespace Dino_Engine.Rendering
{
    internal class LightRenderer : Renderer
    {
        private ShaderProgram _directionalLightShader = new ShaderProgram("Simple_Vertex", "Directional_Light_Fragment");

        public LightRenderer()
        {
            _directionalLightShader.bind();
            _directionalLightShader.loadUniformInt("gAlbedo", 0);
            _directionalLightShader.loadUniformInt("gNormal", 1);
            _directionalLightShader.loadUniformInt("gPosition", 2);
            _directionalLightShader.loadUniformInt("gMaterials", 3);
            _directionalLightShader.unBind();
        }
        private void prepareFrame()
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
        }
        public void render(ECSEngine eCSEngine, ScreenQuadRenderer screenQuadRenderer, FrameBuffer gBuffer)
        {
            screenQuadRenderer.GetLastFrameBuffer().bind();
            prepareFrame();

            DirectionalLightPass(eCSEngine, screenQuadRenderer, gBuffer);
            PointLightPass(eCSEngine);


            finishFrame();
        }
        private void PointLightPass(ECSEngine eCSEngine)
        {

        }
        private void DirectionalLightPass(ECSEngine eCSEngine, ScreenQuadRenderer screenQuadRenderer, FrameBuffer gBuffer)
        {
            Matrix4 viewMatrix = MyMath.createViewMatrix(eCSEngine.Camera.getComponent<TransformationComponent>().Transformation);
            _directionalLightShader.bind();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.getRenderAttachment(0));
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.getRenderAttachment(1));
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.getRenderAttachment(2));
            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.getRenderAttachment(3));


            foreach (Entity entity in eCSEngine.getSystem<DirectionalLightSystem>().MemberEntities)
            {
                Vector3 lightDirection = entity.getComponent<DirectionComponent>().Direction;
                Vector3 lightColour = entity.getComponent<ColourComponent>().Colour.ToVector3();
                Vector4 sunDirectionViewSpace = new Vector4(lightDirection, 1.0f) * Matrix4.Transpose(Matrix4.Invert(viewMatrix));
                _directionalLightShader.loadUniformVector3f("LightDirectionViewSpace", sunDirectionViewSpace.Xyz);

                _directionalLightShader.loadUniformVector3f("lightColour", lightColour);
                _directionalLightShader.loadUniformFloat("ambientFactor", 0.3f);
                _directionalLightShader.loadUniformInt("numberOfCascades", 0);
                _directionalLightShader.loadUniformVector2f("resolution", Engine.WindowHandler.Size);

                screenQuadRenderer.Render(clearColor : false, blend : true);
            }
        }
        public override void Update()
        {
        }
        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }
        private void finishFrame()
        {
            GL.BindVertexArray(0);
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);
            GL.DisableVertexAttribArray(3);
        }
    }
}
