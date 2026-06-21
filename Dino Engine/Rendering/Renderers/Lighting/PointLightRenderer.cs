using Dino_Engine.Core;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using static OpenTK.Graphics.OpenGL.GL;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    public struct PointlightRenderCommand : IRenderCommand
    {
        public Vector3 colour;
        public Vector3 attenuation;
        public float attenuationRadius;
        public Vector3 positionWorld;
        public float ambient;
        public Shadow? shadow; // Support for point light shadows
    }

    public class PointLightRenderer : CommandDrivenRenderer<PointlightRenderCommand>
    {
        private ShaderProgram pointLightShader = new ShaderProgram("Point_Light.vert", "Point_Light.frag");

        public PointLightRenderer() : base("Point Light")
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

            GL.BindVertexArray(ModelGenerator.UNIT_SPHERE.getVAOID());
            EnableVertexAttribArray(0);
        }

        public override void PerformCommand(PointlightRenderCommand command, RenderEngine renderEngine)
        {
            // Set Transformation
            Matrix4 transformationMatrix = MyMath.createTransformationMatrix(command.positionWorld, command.attenuationRadius * 1.1f);
            pointLightShader.loadUniformMatrix4f("TransformationMatrix", transformationMatrix);
            pointLightShader.loadUniformVector3f("lightPositionWorld", command.positionWorld);
            // Light Data
            Vector4 lightPosView = new Vector4(command.positionWorld, 1.0f) * renderEngine.context.viewMatrix;
            pointLightShader.loadUniformVector3f("lightPositionViewSpace", lightPosView.Xyz);
            pointLightShader.loadUniformVector3f("lightColor", command.colour);
            pointLightShader.loadUniformVector3f("attenuation", command.attenuation);
            pointLightShader.loadUniformFloat("lightAmbient", command.ambient);

            // Shadow Handling
            if (command.shadow != null)
            {
                pointLightShader.loadUniformBool("isShadow", true);
                pointLightShader.loadUniformInt("shadowMap", 4);

                // Dynamic clip plane synchronization matches your point light attenuation radius
                pointLightShader.loadUniformFloat("shadowNearPlane", 0.15f);
                pointLightShader.loadUniformFloat("shadowFarPlane", 30f);

                ActiveTexture(TextureUnit.Texture4);
                BindTexture(TextureTarget.TextureCubeMap, command.shadow.Value.shadowFrameBuffer.getDepthAttachment());
            }
            else
            {
                pointLightShader.loadUniformBool("isShadow", false);
            }

            GL.DrawElements(PrimitiveType.Triangles, ModelGenerator.UNIT_SPHERE.getVertexCount(), DrawElementsType.UnsignedInt, 0);
        }

        internal override void Finish(RenderEngine renderEngine)
        {
            BindVertexArray(0);
            DisableVertexAttribArray(0);
            GL.DepthFunc(DepthFunction.Less);
            GL.CullFace(CullFaceMode.Back);
        }

        public override void CleanUp() => pointLightShader.cleanUp();
    }
}