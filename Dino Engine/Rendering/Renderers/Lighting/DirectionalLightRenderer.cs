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

        private int shadowSampler;
        private int depthSampler;

        public DirectionalLightRenderer() : base("Directional Light")
        {
            _directionalLightShader.bind();
            _directionalLightShader.loadUniformInt("gAlbedo", 0);
            _directionalLightShader.loadUniformInt("gNormal", 1);
            _directionalLightShader.loadUniformInt("gMaterials", 2);
            _directionalLightShader.loadUniformInt("gDepth", 3);
            _directionalLightShader.unBind();

            shadowSampler = GL.GenSampler();
            GL.SamplerParameter(shadowSampler, SamplerParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.SamplerParameter(shadowSampler, SamplerParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.SamplerParameter(shadowSampler, SamplerParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.SamplerParameter(shadowSampler, SamplerParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.SamplerParameter(shadowSampler, SamplerParameterName.TextureCompareMode, (int)All.CompareRefToTexture);
            GL.SamplerParameter(shadowSampler, SamplerParameterName.TextureCompareFunc, (int)All.Lequal);

            depthSampler = GL.GenSampler();
            GL.SamplerParameter(depthSampler, SamplerParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.SamplerParameter(depthSampler, SamplerParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.SamplerParameter(depthSampler, SamplerParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.SamplerParameter(depthSampler, SamplerParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.SamplerParameter(depthSampler, SamplerParameterName.TextureCompareMode, (int)All.None);

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

            _directionalLightShader.loadUniformInt("pcfRadius", 2);
        }
        internal override void Finish(RenderEngine renderEngine)
        {
        }
        public override void CleanUp()
        {
            _directionalLightShader.cleanUp();
            GL.DeleteSampler(depthSampler);
            GL.DeleteSampler(shadowSampler);
        }

        public override void PerformCommand(DirectionallightRenderCommand command, RenderEngine renderEngine)
        {
            int CASCADETEXTURESINDEXSTART = 4;
            _directionalLightShader.loadUniformInt("numberOfCascades", command.cascades.Length);
            int numberCascades = command.cascades.Length;

            for (int i = 0; i < numberCascades; i++)
            {
                Shadow cascade = command.cascades[i];

                _directionalLightShader.loadUniformFloat("cascadeProjectionSizes[" + i + "]", cascade.projectionSize);

                _directionalLightShader.loadUniformInt("shadowMaps[" + i + "]", CASCADETEXTURESINDEXSTART + i);
                ActiveTexture(TextureUnit.Texture4 + i);
                BindTexture(TextureTarget.Texture2D, cascade.shadowFrameBuffer.getDepthAttachment());
                BindSampler(CASCADETEXTURESINDEXSTART + i, shadowSampler);

                _directionalLightShader.loadUniformInt("depthMaps[" + i + "]", CASCADETEXTURESINDEXSTART + numberCascades + i);
                ActiveTexture(TextureUnit.Texture4 + numberCascades+i);
                BindTexture(TextureTarget.Texture2D, cascade.shadowFrameBuffer.getDepthAttachment());
                BindSampler(CASCADETEXTURESINDEXSTART + numberCascades + i, shadowSampler);

                Matrix4 shadowMatrix = renderEngine.context.invViewMatrix * cascade.lightViewMatrix * cascade.shadowProjectionMatrix;
                _directionalLightShader.loadUniformMatrix4f("sunSpaceMatrices[" + i + "]", shadowMatrix);
                _directionalLightShader.loadUniformInt("numberOfCascades", numberCascades);

            }

            Vector4 lightDirectionViewSpace = new Vector4(-command.direction, 1.0f)* Matrix4.Transpose(renderEngine.context.invViewMatrix);
            _directionalLightShader.loadUniformVector3f("LightDirectionViewSpace", lightDirectionViewSpace.Xyz);

            _directionalLightShader.loadUniformVector3f("lightColour", command.colour);
            _directionalLightShader.loadUniformFloat("ambientFactor", command.ambient);

            //_directionalLightShader.loadUniformVector2f("resolution2", Engine.Resolution);

            renderEngine.ScreenQuadRenderer.Render(clearColor: false, blend: true);


            //// TODO. BETTER CLEANER WAY PUT THIS SOMEWHERE ELSE....................
            for (int i = 0; i < numberCascades; i++)
            {
                BindSampler(CASCADETEXTURESINDEXSTART + i, 0);
                BindSampler(CASCADETEXTURESINDEXSTART + numberCascades + i, 0);
            }
        }

    }
}
