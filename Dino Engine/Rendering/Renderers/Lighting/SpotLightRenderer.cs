using Dino_Engine.Core;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using static OpenTK.Graphics.OpenGL.GL;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    public struct SpotlightRenderCommand : IRenderCommand
    {
        public Quaternion rotation;
        public Vector3 colour;
        public Vector3 attenuation;
        public float attenuationRadius;
        public Vector3 direction;
        public Vector3 positionWorld;
        public float softness;
        public float ambient;
        public float halfAngleRad;
        public float cutoffCosine;
    }
    public class SpotLightRenderer : CommandDrivenRenderer<SpotlightRenderCommand>
    {

        private ShaderProgram _spotLightShader = new ShaderProgram("Point_Light.vert", "Spot_Light.frag");
        public SpotLightRenderer()
        {
            _spotLightShader.bind();
            _spotLightShader.loadUniformInt("gAlbedo", 0);
            _spotLightShader.loadUniformInt("gNormal", 1);
            _spotLightShader.loadUniformInt("gMaterials", 2);
            _spotLightShader.loadUniformInt("gDepth", 3);
            _spotLightShader.unBind();
        }
        internal override void Prepare(RenderEngine renderEngine)
        {
            FrameBuffer gBuffer = renderEngine.GBuffer;
            _spotLightShader.bind();

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

            ActiveTexture(TextureUnit.Texture0);
            BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(0));
            ActiveTexture(TextureUnit.Texture1);
            BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(1));
            ActiveTexture(TextureUnit.Texture2);
            BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(2));
            ActiveTexture(TextureUnit.Texture3);
            BindTexture(TextureTarget.Texture2D, gBuffer.getDepthAttachment());

            GL.BindVertexArray(ModelGenerator.UNIT_CONE.getVAOID());
            EnableVertexAttribArray(0);

            _spotLightShader.loadUniformMatrix4f("viewMatrix", renderEngine.context.viewMatrix);
            _spotLightShader.loadUniformMatrix4f("projectionMatrix", renderEngine.context.projectionMatrix);
            _spotLightShader.loadUniformMatrix4f("invProjection", renderEngine.context.invProjectionMatrix);
            _spotLightShader.loadUniformVector2f("resolution", Engine.Resolution);
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
        }

        public override void PerformCommand(SpotlightRenderCommand command, RenderEngine renderEngine)
        {
            float attunuationRadius = command.attenuationRadius;
            Vector3 worldPos = command.positionWorld;

            Vector3 from = new Vector3(0, -1, 0);
            Vector3 to = command.direction;
            Quaternion rotationToDesired = MyMath.FromToRotation(from, to);


            Vector3 direction = (new Vector4(command.direction, 1.0f) * Matrix4.CreateFromQuaternion(command.rotation)).Xyz;

            float halfAngleModel = MathF.PI / 8f; // model's built-in 22.5°
            float radiusScale = MathF.Tan(command.halfAngleRad) / MathF.Tan(halfAngleModel);
            Vector3 scale = new Vector3(radiusScale * attunuationRadius, attunuationRadius, radiusScale * attunuationRadius);
            Matrix4 transformationMatrix = MyMath.createTransformationMatrix(worldPos, rotationToDesired, scale * 1.05f);
            _spotLightShader.loadUniformMatrix4f("TransformationMatrix", transformationMatrix);

            Vector4 lightPositionViewSpace = ((new Vector4(worldPos, 1.0f))) * renderEngine.context.viewMatrix;
            Vector4 lightDirectionViewSpace = new Vector4(direction, 1.0f) * Matrix4.Transpose(renderEngine.context.invViewMatrix);
            _spotLightShader.loadUniformVector3f("lightPositionViewSpace", lightPositionViewSpace.Xyz);
            _spotLightShader.loadUniformVector3f("lightDirectionViewSpace", lightDirectionViewSpace.Xyz);

            _spotLightShader.loadUniformVector3f("lightColor", command.colour);
            _spotLightShader.loadUniformVector3f("attenuation", command.attenuation);
            _spotLightShader.loadUniformFloat("softness", command.softness);
            _spotLightShader.loadUniformFloat("cutoffCosine", command.cutoffCosine);
            _spotLightShader.loadUniformFloat("lightAmbient", command.ambient);

            GL.DrawElements(PrimitiveType.Triangles, ModelGenerator.UNIT_CONE.getVertexCount(), DrawElementsType.UnsignedInt, 0);
        }
    }
}
