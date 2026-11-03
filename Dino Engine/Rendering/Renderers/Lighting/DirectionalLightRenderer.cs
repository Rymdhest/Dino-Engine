using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using Dino_Engine.Core;
using Dino_Engine.Rendering.Renderers.AntiAliasing;
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
        // Per pixel, and per sample for the edge pixels (MsaaRenderer.DrawSplit).
        private ShaderProgram _directionalLightShader = new ShaderProgram("Simple.vert", "Directional_Light.frag");
        private ShaderProgram _directionalLightSampleShader = new ShaderProgram("Simple.vert", "Directional_Light.frag", new[] { "PER_SAMPLE" });

        private int shadowSampler;
        private int depthSampler;

        public DirectionalLightRenderer() : base("Directional Light")
        {
            foreach (ShaderProgram shader in new[] { _directionalLightShader, _directionalLightSampleShader })
            {
                shader.bind();
                shader.loadUniformInt("gAlbedo", 0);
                shader.loadUniformInt("gNormal", 1);
                shader.loadUniformInt("gMaterials", 2);
                shader.loadUniformInt("gDepth", 3);
                shader.loadUniformInt("ambientOcclusion", MsaaRenderer.AmbientOcclusionUnit);
                shader.loadUniformInt("pcfRadius", 3);
                shader.unBind();
            }

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
            Enable(EnableCap.Blend);
            BlendFunc(BlendingFactor.One, BlendingFactor.One);
            BlendEquation(BlendEquationMode.FuncAdd);

            renderEngine.Msaa.BindGeometrySamples(renderEngine.AmbientOcclusionTexture);
        }
        internal override void Finish(RenderEngine renderEngine)
        {
        }
        public override void CleanUp()
        {
            _directionalLightShader.cleanUp();
            _directionalLightSampleShader.cleanUp();
            GL.DeleteSampler(depthSampler);
            GL.DeleteSampler(shadowSampler);
        }

        public override void PerformCommand(DirectionallightRenderCommand command, RenderEngine renderEngine)
        {
            int CASCADETEXTURESINDEXSTART = 4;
            int numberCascades = command.cascades.Length;

            for (int i = 0; i < numberCascades; i++)
            {
                Shadow cascade = command.cascades[i];

                ActiveTexture(TextureUnit.Texture4 + i);
                BindTexture(TextureTarget.Texture2D, cascade.shadowFrameBuffer.getDepthAttachment());
                BindSampler(CASCADETEXTURESINDEXSTART + i, shadowSampler);

                ActiveTexture(TextureUnit.Texture4 + numberCascades+i);
                BindTexture(TextureTarget.Texture2D, cascade.shadowFrameBuffer.getDepthAttachment());
                BindSampler(CASCADETEXTURESINDEXSTART + numberCascades + i, depthSampler);
            }
            ActiveTexture(TextureUnit.Texture0);

            Vector4 lightDirectionViewSpace = new Vector4(-command.direction, 0.0f)* Matrix4.Transpose(renderEngine.context.invViewMatrix);

            renderEngine.Msaa.DrawSplit(_directionalLightShader, _directionalLightSampleShader, shader =>
            {
                shader.loadUniformInt("numberOfCascades", numberCascades);
                for (int i = 0; i < numberCascades; i++)
                {
                    Shadow cascade = command.cascades[i];
                    shader.loadUniformFloat("cascadeProjectionSizes[" + i + "]", cascade.projectionSize);
                    shader.loadUniformInt("shadowMaps[" + i + "]", CASCADETEXTURESINDEXSTART + i);
                    shader.loadUniformInt("depthMaps[" + i + "]", CASCADETEXTURESINDEXSTART + numberCascades + i);
                    Matrix4 shadowMatrix = renderEngine.context.invViewMatrix * cascade.lightViewMatrix * cascade.shadowProjectionMatrix;
                    shader.loadUniformMatrix4f("sunSpaceMatrices[" + i + "]", shadowMatrix);
                }
                shader.loadUniformVector3f("LightDirectionViewSpace", lightDirectionViewSpace.Xyz);
                shader.loadUniformVector3f("lightColour", command.colour);
                shader.loadUniformFloat("ambientFactor", command.ambient);

                renderEngine.ScreenQuadRenderer.Render(clearColor: false, blend: true);
            });


            //// TODO. BETTER CLEANER WAY PUT THIS SOMEWHERE ELSE....................
            for (int i = 0; i < numberCascades; i++)
            {
                BindSampler(CASCADETEXTURESINDEXSTART + i, 0);
                BindSampler(CASCADETEXTURESINDEXSTART + numberCascades + i, 0);
            }
        }

    }
}
