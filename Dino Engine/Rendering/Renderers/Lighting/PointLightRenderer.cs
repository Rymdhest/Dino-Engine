using Dino_Engine.Core;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using static OpenTK.Graphics.OpenGL.GL;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural;
using System.Reflection;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    public struct PointlightRenderCommand : IRenderCommand
    {
        public Vector3 colour;
        public Vector3 attenuation;
        public float attenuationRadius;
        public Vector3 positionWorld;
        public float ambient;
    }

    public class PointLightRenderer : CommandDrivenRenderer<PointlightRenderCommand>
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

        internal override void Prepare(RenderEngine renderEngine)
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

            Matrix4 viewMatrix = renderEngine.context.viewMatrix;
            Matrix4 projectionMatrix = renderEngine.context.projectionMatrix;

            glModel model = ModelGenerator.UNIT_SPHERE;
            GL.BindVertexArray(model.getVAOID());
            EnableVertexAttribArray(0);

            pointLightShader.loadUniformMatrix4f("viewMatrix", viewMatrix);
            pointLightShader.loadUniformMatrix4f("projectionMatrix", projectionMatrix);
            pointLightShader.loadUniformMatrix4f("invProjection", renderEngine.context.invProjectionMatrix);
            pointLightShader.loadUniformVector2f("resolution", Engine.Resolution);
        }

        internal override void Finish(RenderEngine renderEngine)
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

        public override void PerformCommand(PointlightRenderCommand command, RenderEngine renderEngine)
        {
            Vector3 position = command.positionWorld;
            float attunuationRadius = command.attenuationRadius;
            Matrix4 transformationMatrix = MyMath.createTransformationMatrix(position, attunuationRadius * 1.1f);
            pointLightShader.loadUniformMatrix4f("TransformationMatrix", transformationMatrix);

            Vector3 lightColour = command.colour;
            Vector3 attenuation = command.attenuation;

            Vector4 lightPositionViewSpace = new Vector4(position, 1.0f) * renderEngine.context.viewMatrix;
            pointLightShader.loadUniformVector3f("lightPositionViewSpace", lightPositionViewSpace.Xyz);

            pointLightShader.loadUniformVector3f("lightColor", lightColour);
            pointLightShader.loadUniformVector3f("attenuation", attenuation);
            pointLightShader.loadUniformFloat("lightAmbient", command.ambient);

            GL.DrawElements(PrimitiveType.Triangles, ModelGenerator.UNIT_SPHERE.getVertexCount(), DrawElementsType.UnsignedInt, 0);
        }
    }
}
