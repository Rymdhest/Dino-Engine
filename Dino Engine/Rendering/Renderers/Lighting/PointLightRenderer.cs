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

        private ShaderProgram pointLightShader = new ShaderProgram("Point_Light.vert", "Point_Light.frag");
        public PointLightRenderer()
        {
            pointLightShader.bind();
            pointLightShader.loadUniformInt("gAlbedo", 0);
            pointLightShader.loadUniformInt("gNormal", 1);
            pointLightShader.loadUniformInt("gMaterials", 2);
            pointLightShader.loadUniformInt("gDepth", 3);
            pointLightShader.unBind();
        }

        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            Matrix4 viewMatrix = MyMath.createViewMatrix(eCSEngine.Camera.getComponent<TransformationComponent>().Transformation);
            Matrix4 projectionMatrix = eCSEngine.Camera.getComponent<ProjectionComponent>().ProjectionMatrix;

            glModel model = ModelGenerator.UNIT_SPHERE;
            GL.BindVertexArray(model.getVAOID());
            EnableVertexAttribArray(0);

            pointLightShader.loadUniformMatrix4f("viewMatrix", viewMatrix);
            pointLightShader.loadUniformMatrix4f("projectionMatrix", projectionMatrix);
            pointLightShader.loadUniformMatrix4f("invProjection", Matrix4.Invert( projectionMatrix));
            pointLightShader.loadUniformVector2f("resolution", Engine.Resolution);

            foreach (Entity entity in eCSEngine.getSystem<PointLightSystem>().MemberEntities)
            {

                Vector3 position = entity.getComponent<TransformationComponent>().Transformation.position;
                float attunuationRadius = entity.getComponent<AttunuationComponent>().AttunuationRadius;
                Matrix4 transformationMatrix = MyMath.createTransformationMatrix(position, attunuationRadius*1.1f);
                pointLightShader.loadUniformMatrix4f("TransformationMatrix", transformationMatrix);

                Vector3 lightColour = entity.getComponent<ColourComponent>().Colour.ToVector3();
                Vector3 attenuation = entity.getComponent<AttunuationComponent>().Attunuation;

                Vector4 lightPositionViewSpace = new Vector4(position, 1.0f) * viewMatrix;
                pointLightShader.loadUniformVector3f("lightPositionViewSpace", lightPositionViewSpace.Xyz);

                pointLightShader.loadUniformVector3f("lightColor", lightColour);
                pointLightShader.loadUniformVector3f("attenuation", attenuation);

                GL.DrawElements(PrimitiveType.Triangles, model.getVertexCount(), DrawElementsType.UnsignedInt, 0);
            }

        }
        internal override void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            FrameBuffer gBuffer = renderEngine.GBuffer;
            pointLightShader.bind();

           GL.Enable(EnableCap.CullFace);
           GL.CullFace(CullFaceMode.Front);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            GL.DepthFunc(DepthFunction.Greater);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(false);

            ActiveTexture(TextureUnit.Texture0);
            BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(0));
            ActiveTexture(TextureUnit.Texture1);
            BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(1));
            ActiveTexture(TextureUnit.Texture2);
            BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(2));
            ActiveTexture(TextureUnit.Texture3);
            BindTexture(TextureTarget.Texture2D, gBuffer.getDepthAttachment());
        }

        internal override void Finish(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            BindVertexArray(0);
            DisableVertexAttribArray(0);

            GL.DepthFunc(DepthFunction.Less);
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
