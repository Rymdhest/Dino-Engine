using Dino_Engine.Core;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using static OpenTK.Graphics.OpenGL.GL;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.AntiAliasing;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    public struct PointlightRenderCommand : IRenderCommand
    {
        public Vector3 colour;
        public float radius;
        public float fill;
        public Vector3 positionWorld;
        public Shadow? shadow;
        public float shadowStrength;
    }

    public class PointLightRenderer : CommandDrivenRenderer<PointlightRenderCommand>
    {
        // Clip planes of the cube shadow map projection (PointLightShadowSystem); shaders reconstruct depth with them.
        public const float ShadowNearPlane = 0.15f;
        public const float ShadowFarPlane = 30f;
        // Width of the shadow filter per metre from the light.
        private const float ShadowSoftness = 0.012f;

        // Per pixel, and per sample for the edge pixels (MsaaRenderer.DrawSplit).
        private ShaderProgram pointLightShader = new ShaderProgram("Point_Light.vert", "Point_Light.frag");
        private ShaderProgram pointLightSampleShader = new ShaderProgram("Point_Light.vert", "Point_Light.frag", new[] { "PER_SAMPLE" });
        private Frustum _view;

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
            _view = new Frustum(renderEngine.context.viewMatrix * renderEngine.context.projectionMatrix);

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
            Vector3 reach = new Vector3(command.radius);
            if (_view.IntersectsAABB(new AABB(command.positionWorld - reach, command.positionWorld + reach)) == IntersectionResult.Outside) return;

            Engine.PerformanceMonitor.Count("Point lights drawn", 1);
            if (command.shadow != null) Engine.PerformanceMonitor.Count("Shadowed point lights drawn", 1);
            Matrix4 transformationMatrix = MyMath.createTransformationMatrix(command.positionWorld, command.radius * 1.1f);
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
                shader.loadUniformFloat("lightRadius", command.radius);
                shader.loadUniformFloat("lightFill", command.fill);

                // Shadow Handling
                shader.loadUniformBool("isShadow", command.shadow != null);
                if (command.shadow != null)
                {
                    shader.loadUniformFloat("shadowSoftness", ShadowSoftness);
                    shader.loadUniformFloat("shadowStrength", command.shadowStrength);
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