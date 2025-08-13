using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using Dino_Engine.Core;
using static OpenTK.Graphics.OpenGL.GL;


namespace Dino_Engine.Rendering.Renderers.Lighting
{
    public struct DirectionallightRenderCommand : IRenderCommand
    {
        public Vector3 colour;
        public Vector3 direction;
        public float ambient;
        public Shadow[] cascades;
    }

    public class DirectionalLightRenderer : CommandDrivenRenderer<DirectionallightRenderCommand>
    {
        private ShaderProgram _directionalLightShader = new ShaderProgram("Simple.vert", "Directional_Light.frag");

        public DirectionalLightRenderer() : base("Directional Light")
        {
            _directionalLightShader.bind();
            _directionalLightShader.loadUniformInt("gAlbedo", 0);
            _directionalLightShader.loadUniformInt("gNormal", 1);
            _directionalLightShader.loadUniformInt("gMaterials", 2);
            _directionalLightShader.loadUniformInt("gDepth", 3);
            _directionalLightShader.unBind();
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
            FrameBuffer gBuffer = renderEngine.GBuffer;

            _directionalLightShader.bind();

            Enable(EnableCap.Blend);
            BlendFunc(BlendingFactor.One, BlendingFactor.One);
            BlendEquation(BlendEquationMode.FuncAdd);

            ActiveTexture(TextureUnit.Texture0);
            BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(0));

            ActiveTexture(TextureUnit.Texture1);
            BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(1));

            ActiveTexture(TextureUnit.Texture2);
            BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(2));

            ActiveTexture(TextureUnit.Texture3);
            BindTexture(TextureTarget.Texture2D, gBuffer.getDepthAttachment());

            _directionalLightShader.loadUniformInt("pcfRadius", 4);
        }
        internal override void Finish(RenderEngine renderEngine)
        {
        }
        public override void CleanUp()
        {
            _directionalLightShader.cleanUp();
        }

        public override void PerformCommand(DirectionallightRenderCommand command, RenderEngine renderEngine)
        {
            int CASCADETEXTURESINDEXSTART = 4;
            _directionalLightShader.loadUniformInt("numberOfCascades", command.cascades.Length);
            for (int i = 0; i < command.cascades.Length; i++)
            {

                Shadow cascade = command.cascades[i];
                _directionalLightShader.loadUniformInt("shadowMaps[" + i + "]", CASCADETEXTURESINDEXSTART + i);

                _directionalLightShader.loadUniformFloat("cascadeProjectionSizes[" + i + "]", cascade.projectionSize);
                ActiveTexture(TextureUnit.Texture4 + i);
                BindTexture(TextureTarget.Texture2D, cascade.shadowFrameBuffer.getDepthAttachment());
                _directionalLightShader.loadUniformVector2f("shadowMapResolutions[" + i + "]", cascade.shadowFrameBuffer.getResolution());


                Matrix4 shadowMatrix = renderEngine.context.invViewMatrix * cascade.lightViewMatrix * cascade.shadowProjectionMatrix;
                _directionalLightShader.loadUniformMatrix4f("sunSpaceMatrices[" + i + "]", shadowMatrix);
                _directionalLightShader.loadUniformInt("numberOfCascades", command.cascades.Length);

            }

            Vector4 lightDirectionViewSpace = new Vector4(-command.direction, 1.0f)* Matrix4.Transpose(renderEngine.context.invViewMatrix);
            _directionalLightShader.loadUniformVector3f("LightDirectionViewSpace", lightDirectionViewSpace.Xyz);

            _directionalLightShader.loadUniformVector3f("lightColour", command.colour);
            _directionalLightShader.loadUniformFloat("ambientFactor", command.ambient);

            //_directionalLightShader.loadUniformVector2f("resolution2", Engine.Resolution);

            renderEngine.ScreenQuadRenderer.Render(clearColor: false, blend: true);
        }

    }
}
