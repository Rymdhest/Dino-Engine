using Dino_Engine.Core;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using static OpenTK.Graphics.OpenGL.GL;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.AntiAliasing;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    public struct SpotlightRenderCommand : IRenderCommand
    {
        public Quaternion rotation;
        public Vector3 colour;
        public float radius;
        public float fill;
        public Vector3 direction;
        public Vector3 positionWorld;
        public float softness;
        public float halfAngleRad;
        public float cutoffCosine;
        public Shadow? shadow;
    }
    public class SpotLightRenderer : CommandDrivenRenderer<SpotlightRenderCommand>
    {

        // Per pixel, and per sample for the edge pixels (MsaaRenderer.DrawSplit).
        private ShaderProgram _spotLightShader = new ShaderProgram("Point_Light.vert", "Spot_Light.frag");
        private ShaderProgram _spotLightSampleShader = new ShaderProgram("Point_Light.vert", "Spot_Light.frag", new[] { "PER_SAMPLE" });
        private Frustum _view;

        public SpotLightRenderer() : base("Spotlight")
        {
            foreach (ShaderProgram shader in new[] { _spotLightShader, _spotLightSampleShader })
            {
                shader.bind();
                shader.loadUniformInt("gAlbedo", 0);
                shader.loadUniformInt("gNormal", 1);
                shader.loadUniformInt("gMaterials", 2);
                shader.loadUniformInt("gDepth", 3);
                shader.loadUniformInt("ambientOcclusion", MsaaRenderer.AmbientOcclusionUnit);
                shader.loadUniformInt("shadowMap", 4);
                shader.loadUniformInt("pcfRadius", 1);
                shader.unBind();
            }
        }
        internal override void Prepare(RenderEngine renderEngine)
        {
            _view = new Frustum(renderEngine.context.viewMatrix * renderEngine.context.projectionMatrix);

            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Front);
            //GL.CullFace(CullFaceMode.Back);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            GL.DepthFunc(DepthFunction.Greater);
            //GL.DepthFunc(DepthFunction.Less);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(false);

            renderEngine.Msaa.BindGeometrySamples(renderEngine.AmbientOcclusionTexture);

            GL.BindVertexArray(ModelGenerator.UNIT_CONE.getVAOID());
            EnableVertexAttribArray(0);
        }

        internal override void Finish(RenderEngine renderEngine)
        {
            BindVertexArray(0);

            GL.DepthFunc(DepthFunction.Less);
            GL.CullFace(CullFaceMode.Back);
        }

        public override void CleanUp()
        {
            _spotLightShader.cleanUp();
            _spotLightSampleShader.cleanUp();
        }

        /// <summary>The direction the light shines in, as used for shading (also used by the volumetric fog).</summary>
        public static Vector3 WorldDirection(SpotlightRenderCommand command)
        {
            return (new Vector4(command.direction, 1.0f) * Matrix4.CreateFromQuaternion(command.rotation)).Xyz;
        }

        public override void PerformCommand(SpotlightRenderCommand command, RenderEngine renderEngine)
        {
            Vector3 reach = new Vector3(command.radius);
            if (_view.IntersectsAABB(new AABB(command.positionWorld - reach, command.positionWorld + reach)) == IntersectionResult.Outside) return;

            Matrix4 shadowMatrix = Matrix4.Identity;
            if (command.shadow != null)
            {
                Shadow shadow = command.shadow.Value;
                ActiveTexture(TextureUnit.Texture4);
                BindTexture(TextureTarget.Texture2D, shadow.shadowFrameBuffer.getDepthAttachment());
                ActiveTexture(TextureUnit.Texture0);
                shadowMatrix = renderEngine.context.invViewMatrix * shadow.lightViewMatrix * shadow.shadowProjectionMatrix;
            }

            Vector3 worldPos = command.positionWorld;

            Vector3 from = new Vector3(0, -1, 0);
            Vector3 to = command.direction;
            Quaternion rotationToDesired = MyMath.FromToRotation(from, to);


            Vector3 direction = WorldDirection(command);

            float halfAngleModel = MathF.PI / 8f; // model's built-in 22.5°
            float radiusScale = MathF.Tan(command.halfAngleRad) / MathF.Tan(halfAngleModel);
            Vector3 scale = new Vector3(radiusScale * command.radius, command.radius, radiusScale * command.radius);
            Matrix4 transformationMatrix = MyMath.createTransformationMatrix(worldPos, rotationToDesired, scale * 1.05f);

            Vector4 lightPositionViewSpace = ((new Vector4(worldPos, 1.0f))) * renderEngine.context.viewMatrix;
            Vector4 lightDirectionViewSpace = new Vector4(direction, 1.0f) * Matrix4.Transpose(renderEngine.context.invViewMatrix);

            renderEngine.Msaa.DrawSplit(_spotLightShader, _spotLightSampleShader, shader =>
            {
                shader.loadUniformBool("isShadow", command.shadow != null);
                if (command.shadow != null)
                {
                    shader.loadUniformVector2f("shadowResolution", command.shadow.Value.shadowFrameBuffer.getResolution());
                    shader.loadUniformMatrix4f("lightSpaceMatrix", shadowMatrix);
                }

                shader.loadUniformMatrix4f("TransformationMatrix", transformationMatrix);
                shader.loadUniformVector3f("lightPositionViewSpace", lightPositionViewSpace.Xyz);
                shader.loadUniformVector3f("lightDirectionViewSpace", lightDirectionViewSpace.Xyz);

                shader.loadUniformVector3f("lightColor", command.colour);
                shader.loadUniformFloat("lightRadius", command.radius);
                shader.loadUniformFloat("softness", command.softness);
                shader.loadUniformFloat("cutoffCosine", command.cutoffCosine);
                // Global illumination bounces the light for real while it's in, so the stand-in fades out.
                shader.loadUniformFloat("lightFill", command.fill * renderEngine._globalIllumination.FillKept(command.positionWorld, command.radius));

                GL.DrawElements(PrimitiveType.Triangles, ModelGenerator.UNIT_CONE.getVertexCount(), DrawElementsType.UnsignedInt, 0);
            });
        }
    }
}
