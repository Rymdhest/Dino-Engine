using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using static OpenTK.Graphics.OpenGL.GL;
using Dino_Engine.ECS.Systems;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    internal class SpotLightRenderer : Renderer
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

        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            Matrix4 viewMatrix = MyMath.createViewMatrix(eCSEngine.Camera.getComponent<TransformationComponent>().Transformation);
            Matrix4 projectionMatrix = eCSEngine.Camera.getComponent<ProjectionComponent>().ProjectionMatrix;




            glModel cone = ModelGenerator.UNIT_CONE;
            GL.BindVertexArray(cone.getVAOID());
            EnableVertexAttribArray(0);

            _spotLightShader.loadUniformMatrix4f("viewMatrix", viewMatrix);
            _spotLightShader.loadUniformMatrix4f("projectionMatrix", projectionMatrix);
            _spotLightShader.loadUniformMatrix4f("invProjection", Matrix4.Invert(projectionMatrix));
            _spotLightShader.loadUniformVector2f("resolution", Engine.Resolution);

            foreach (Entity entity in eCSEngine.getSystem<SpotLightSystem>().MemberEntities)
            {

                Vector3 position = entity.getComponent<TransformationComponent>().Transformation.position;
                Vector3 rotation = entity.getComponent<TransformationComponent>().Transformation.rotation;
                // hard coded to match the cone model
                Vector3 direction = (new Vector4(0f, -1.0f, 0.0f, 1.0f) * MyMath.createRotationMatrix(rotation)).Xyz;
                float attunuationRadius = entity.getComponent<AttunuationComponent>().AttunuationRadius;
                Matrix4 transformationMatrix = MyMath.createTransformationMatrix(position, rotation, attunuationRadius);
                _spotLightShader.loadUniformMatrix4f("TransformationMatrix", transformationMatrix);

                Vector3 lightColour = entity.getComponent<ColourComponent>().Colour.ToVector3();
                Vector3 attenuation = entity.getComponent<AttunuationComponent>().Attunuation;

                Vector4 lightPositionViewSpace = new Vector4(position, 1.0f) * viewMatrix;
                Vector4 lightDirectionViewSpace = new Vector4(direction, 1.0f) * Matrix4.Transpose(Matrix4.Invert(viewMatrix));
                _spotLightShader.loadUniformVector3f("lightPositionViewSpace", lightPositionViewSpace.Xyz);
                _spotLightShader.loadUniformVector3f("lightDirectionViewSpace", lightDirectionViewSpace.Xyz);

                _spotLightShader.loadUniformVector3f("lightColor", lightColour);
                _spotLightShader.loadUniformVector3f("attenuation", attenuation);
                _spotLightShader.loadUniformFloat("softness", 0.3f);

                GL.DrawElements(PrimitiveType.Triangles, cone.getVertexCount(), DrawElementsType.UnsignedInt, 0);
            }

        }
        internal override void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            FrameBuffer gBuffer = renderEngine.GBuffer;
            _spotLightShader.bind();

           //GL.Disable(EnableCap.CullFace);
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

            GL.DepthFunc(DepthFunction.Less);
            GL.CullFace(CullFaceMode.Back);
        }

        public override void CleanUp()
        {
            _spotLightShader.cleanUp();
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        public override void Update()
        {
        }
    }
}
