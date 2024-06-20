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
using static Dino_Engine.ECS.Components.CascadingShadowComponent;


namespace Dino_Engine.Rendering
{
    internal class LightRenderer : Renderer
    {
        private ShaderProgram _directionalLightShader = new ShaderProgram("Simple_Vertex", "Directional_Light_Fragment");
        private readonly int cascadesTextureIndexStart = 4;

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
                float ambientFactor = AmbientLightComponent.DEFAULT;

                if (entity.TryGetComponent<AmbientLightComponent>(out AmbientLightComponent? ambientLightComponent))
                {
                    ambientFactor = ambientLightComponent.AmbientFactor;
                }
                if (entity.TryGetComponent<CascadingShadowComponent>(out CascadingShadowComponent shadow))
                {
                    for (int i = 0; i < shadow.Cascades.Count; i++)
                    {

                        ShadowCascade cascade = shadow.Cascades[i];
                        _directionalLightShader.loadUniformInt("shadowMaps[" + i + "]", cascadesTextureIndexStart + i);

                        _directionalLightShader.loadUniformFloat("cascadeProjectionSizes[" + i + "]", cascade.getProjectionSize());
                        GL.ActiveTexture(TextureUnit.Texture4 + i);
                        GL.BindTexture(TextureTarget.Texture2D, cascade.getDepthTexture());
                        _directionalLightShader.loadUniformVector2f("shadowMapResolutions[" + i + "]", cascade.getResolution());


                        Matrix4 shadowMatrix = Matrix4.Invert(viewMatrix) * shadow.LightViewMatrix * cascade.getProjectionMatrix();
                        _directionalLightShader.loadUniformMatrix4f("sunSpaceMatrices[" + i + "]", shadowMatrix);
                        _directionalLightShader.loadUniformInt("numberOfCascades", shadow.Cascades.Count);
                    }
                } else
                {
                    _directionalLightShader.loadUniformInt("numberOfCascades", 0);
                }



                Vector4 sunDirectionViewSpace = new Vector4(lightDirection, 1.0f) * Matrix4.Transpose(Matrix4.Invert(viewMatrix));
                _directionalLightShader.loadUniformVector3f("LightDirectionViewSpace", sunDirectionViewSpace.Xyz);

                _directionalLightShader.loadUniformVector3f("lightColour", lightColour);
                _directionalLightShader.loadUniformFloat("ambientFactor", ambientFactor);
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
