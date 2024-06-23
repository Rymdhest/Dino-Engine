using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using static OpenTK.Graphics.OpenGL.GL;
using static Dino_Engine.ECS.Components.CascadingShadowComponent;
using Dino_Engine.ECS.Systems;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    internal class PointLightRenderer : Renderer
    {

        private ShaderProgram pointLightShader = new ShaderProgram("Point_Light_Vertex", "Point_Light_Fragment");
        public PointLightRenderer()
        {
            pointLightShader.bind();
            pointLightShader.loadUniformInt("gAlbedo", 0);
            pointLightShader.loadUniformInt("gNormal", 1);
            pointLightShader.loadUniformInt("gPosition", 2);
            pointLightShader.loadUniformInt("gMaterials", 3);
            pointLightShader.unBind();
        }

        public void Render(ECSEngine eCSEngine, ScreenQuadRenderer renderer, FrameBuffer gBuffer)
        {
            prepareFrame();
            Matrix4 viewMatrix = MyMath.createViewMatrix(eCSEngine.Camera.getComponent<TransformationComponent>().Transformation);
            Matrix4 projectionMatrix = eCSEngine.Camera.getComponent<ProjectionComponent>().ProjectionMatrix;
            pointLightShader.bind();
            ActiveTexture(TextureUnit.Texture0);
            BindTexture(TextureTarget.Texture2D, gBuffer.getRenderAttachment(0));
            ActiveTexture(TextureUnit.Texture1);
            BindTexture(TextureTarget.Texture2D, gBuffer.getRenderAttachment(1));
            ActiveTexture(TextureUnit.Texture2);
            BindTexture(TextureTarget.Texture2D, gBuffer.getRenderAttachment(2));
            ActiveTexture(TextureUnit.Texture3);
            BindTexture(TextureTarget.Texture2D, gBuffer.getRenderAttachment(3));

            renderer.GetLastFrameBuffer().bind();

            glModel model = ModelGenerator.UNIT_SPHERE;
            GL.BindVertexArray(model.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);

            pointLightShader.loadUniformMatrix4f("viewMatrix", viewMatrix);
            pointLightShader.loadUniformMatrix4f("projectionMatrix", projectionMatrix);
            pointLightShader.loadUniformVector2f("resolution", Engine.Resolution);

            foreach (Entity entity in eCSEngine.getSystem<PointLightSystem>().MemberEntities)
            {

                Vector3 position = entity.getComponent<TransformationComponent>().Transformation.position;
                float attunuationRadius = entity.getComponent<AttunuationComponent>().AttunuationRadius;
                Matrix4 transformationMatrix = MyMath.createTransformationMatrix(position, attunuationRadius);
                pointLightShader.loadUniformMatrix4f("TransformationMatrix", transformationMatrix);

                Vector3 lightColour = entity.getComponent<ColourComponent>().Colour.ToVector3();
                Vector3 attenuation = entity.getComponent<AttunuationComponent>().Attunuation;

                Vector4 lightPositionViewSpace = new Vector4(position, 1.0f) * viewMatrix;
                pointLightShader.loadUniformVector3f("lightPositionViewSpace", lightPositionViewSpace.Xyz);

                pointLightShader.loadUniformVector3f("lightColor", lightColour);
                pointLightShader.loadUniformVector3f("attenuation", attenuation);

                GL.DrawElements(PrimitiveType.Triangles, model.getVertexCount(), DrawElementsType.UnsignedInt, 0);
            }

            finishFrame();
        }
        private void prepareFrame()
        {
            EnableVertexAttribArray(0);
            EnableVertexAttribArray(1);
            EnableVertexAttribArray(2);
            EnableVertexAttribArray(3);

            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Front);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            GL.DepthFunc(DepthFunction.Greater);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(false);

        }

        private void finishFrame()
        {
            BindVertexArray(0);
            DisableVertexAttribArray(0);
            DisableVertexAttribArray(1);
            DisableVertexAttribArray(2);
            DisableVertexAttribArray(3);

            GL.DepthFunc(DepthFunction.Less);
            pointLightShader.unBind();
            GL.CullFace(CullFaceMode.Back);
        }

        public override void CleanUp()
        {
            pointLightShader.cleanUp();
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        public override void Update()
        {
        }
    }
}
