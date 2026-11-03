using Dino_Engine.Core;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using static OpenTK.Graphics.OpenGL.GL;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Rendering.Renderers.AntiAliasing;

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
        // Clip planes of the cube shadow map projection (PointLightShadowComponent); shaders reconstruct depth with them.
        public const float ShadowNearPlane = 0.15f;
        public const float ShadowFarPlane = 30f;

        // Per pixel, and per sample for the edge pixels (MsaaRenderer.DrawSplit).
        private ShaderProgram pointLightShader = new ShaderProgram("Point_Light.vert", "Point_Light.frag");
        private ShaderProgram pointLightSampleShader = new ShaderProgram("Point_Light.vert", "Point_Light.frag", new[] { "PER_SAMPLE" });

        public PointLightRenderer() : base("Point Light")
        {
            foreach (ShaderProgram shader in new[] { pointLightShader, pointLightSampleShader })
            {
                shader.bind();
                shader.loadUniformInt("gAlbedo", 0);
                shader.loadUniformInt("gNormal", 1);
                shader.loadUniformInt("gMaterials", 2);
                shader.loadUniformInt("gDepth", 3);
                shader.loadUniformInt("ambientOcclusion", MsaaRenderer.AmbientOcclusionUnit);
                shader.loadUniformInt("shadowMap", 4);
                shader.unBind();
            }
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Front);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            GL.DepthFunc(DepthFunction.Greater);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(false);

            renderEngine.Msaa.BindGeometrySamples(renderEngine.AmbientOcclusionTexture);

            GL.BindVertexArray(ModelGenerator.UNIT_SPHERE.getVAOID());
            EnableVertexAttribArray(0);
        }

        public override void PerformCommand(PointlightRenderCommand command, RenderEngine renderEngine)
        {
            Matrix4 transformationMatrix = MyMath.createTransformationMatrix(command.positionWorld, command.attenuationRadius * 1.1f);
            Vector4 lightPosView = new Vector4(command.positionWorld, 1.0f) * renderEngine.context.viewMatrix;
            if (command.shadow != null)
            {
                ActiveTexture(TextureUnit.Texture4);
                BindTexture(TextureTarget.TextureCubeMap, command.shadow.Value.shadowFrameBuffer.getDepthAttachment());
                ActiveTexture(TextureUnit.Texture0);
            }

            renderEngine.Msaa.DrawSplit(pointLightShader, pointLightSampleShader, shader =>
            {
                // Set Transformation
                shader.loadUniformMatrix4f("TransformationMatrix", transformationMatrix);
                shader.loadUniformVector3f("lightPositionWorld", command.positionWorld);
                // Light Data
                shader.loadUniformVector3f("lightPositionViewSpace", lightPosView.Xyz);
                shader.loadUniformVector3f("lightColor", command.colour);
                shader.loadUniformVector3f("attenuation", command.attenuation);
                shader.loadUniformFloat("lightAmbient", command.ambient);

                // Shadow Handling
                shader.loadUniformBool("isShadow", command.shadow != null);
                if (command.shadow != null)
                {
                    shader.loadUniformFloat("shadowSmoothRadius", 0.015f);

                    // Dynamic clip plane synchronization matches your point light attenuation radius
                    shader.loadUniformFloat("shadowNearPlane", ShadowNearPlane);
                    shader.loadUniformFloat("shadowFarPlane", ShadowFarPlane);
                }

                GL.DrawElements(PrimitiveType.Triangles, ModelGenerator.UNIT_SPHERE.getVertexCount(), DrawElementsType.UnsignedInt, 0);
            });
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
            pointLightSampleShader.cleanUp();
        }
    }
}