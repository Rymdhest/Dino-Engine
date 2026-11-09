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
        public Shadow[] cascades;
    }

    public class DirectionalLightRenderer : CommandDrivenRenderer<DirectionallightRenderCommand>
    {
        // Per pixel, and per sample for the edge pixels (MsaaRenderer.DrawSplit).
        private ShaderProgram _directionalLightShader = new ShaderProgram("Simple.vert", "Directional_Light.frag");
        private ShaderProgram _directionalLightSampleShader = new ShaderProgram("Simple.vert", "Directional_Light.frag", new[] { "PER_SAMPLE" });

        private const int CloudShadowUnit = 9;
        private const int FirstCascadeUnit = 4;
        private const int MostCascades = 5;

        private int shadowSampler;

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
                LinkShadows(shader);
                shader.unBind();
            }

            // Plain depth, gathered four texels at a time: the filter (Lighting/SunShadow.glsl) weights each texel itself, and needs to know
            // how far off each one's caster is. Past the edge is the far end of the map, which shades nothing. The fog, the water and the
            // particles read the same maps through comparisons of their own, which this leaves as they are.
            shadowSampler = GL.GenSampler();
            GL.SamplerParameter(shadowSampler, SamplerParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.SamplerParameter(shadowSampler, SamplerParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.SamplerParameter(shadowSampler, SamplerParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.SamplerParameter(shadowSampler, SamplerParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.SamplerParameter(shadowSampler, SamplerParameterName.TextureBorderColor, new[] { 1f, 1f, 1f, 1f });
            GL.SamplerParameter(shadowSampler, SamplerParameterName.TextureCompareMode, (int)All.None);
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
            GL.DeleteSampler(shadowSampler);
        }

        /// <summary>
        /// Takes the sun out of this frame's lights, for <see cref="AmbientLightRenderer"/>, which draws it along with the sky. The sun is
        /// the light with cascades: lightning has none, and stays here.
        /// </summary>
        public bool TakeSun(out DirectionallightRenderCommand sun)
        {
            sun = default;
            int index = commands.FindLastIndex(command => command.cascades.Length > 0);
            if (index < 0) return false;

            sun = commands[index];
            Sun = sun;
            commands.RemoveAll(command => command.cascades.Length > 0);
            return true;
        }

        /// <summary>This frame's sun, once <see cref="TakeSun"/> has had it, for what's drawn after the light passes and is lit by it too: particles.</summary>
        public DirectionallightRenderCommand? Sun { get; private set; }

        public override void Update()
        {
            Sun = null;
        }

        /// <summary>Points a shader's shadow samplers (Lighting/SunShadow.glsl) at the units <see cref="BindShadows"/> fills.</summary>
        public static void LinkShadows(ShaderProgram shader)
        {
            for (int i = 0; i < MostCascades; i++) shader.loadUniformInt("shadowMaps[" + i + "]", FirstCascadeUnit + i);
            shader.loadUniformInt("cloudShadowMap", CloudShadowUnit);
        }

        /// <summary>Binds what a light's shadow is read from, until <see cref="ReleaseShadows"/>.</summary>
        public void BindShadows(DirectionallightRenderCommand command, RenderEngine renderEngine)
        {
            for (int i = 0; i < command.cascades.Length; i++)
            {
                ActiveTexture(TextureUnit.Texture0 + FirstCascadeUnit + i);
                BindTexture(TextureTarget.Texture2D, command.cascades[i].shadowFrameBuffer.getDepthAttachment());
                BindSampler(FirstCascadeUnit + i, shadowSampler);
            }
            ActiveTexture(TextureUnit.Texture0 + CloudShadowUnit);
            BindTexture(TextureTarget.Texture2D, renderEngine._weatherRenderer.CloudShadowTexture);
            ActiveTexture(TextureUnit.Texture0);
        }

        /// <summary>Loads where a light's cascades lie, for a shader that's bound.</summary>
        public static void LoadShadows(ShaderProgram shader, DirectionallightRenderCommand command, RenderEngine renderEngine)
        {
            shader.loadUniformInt("numberOfCascades", command.cascades.Length);
            for (int i = 0; i < command.cascades.Length; i++)
            {
                Shadow cascade = command.cascades[i];
                Matrix4 shadowMatrix = renderEngine.context.invViewMatrix * cascade.lightViewMatrix * cascade.shadowProjectionMatrix;
                shader.loadUniformMatrix4f("sunSpaceMatrices[" + i + "]", shadowMatrix);
            }
        }

        public void ReleaseShadows(DirectionallightRenderCommand command)
        {
            for (int i = 0; i < command.cascades.Length; i++) BindSampler(FirstCascadeUnit + i, 0);
        }

        /// <summary>The way to a light from what it shines on, as the shaders want it.</summary>
        public static Vector3 ViewSpaceDirection(DirectionallightRenderCommand command, RenderEngine renderEngine)
        {
            return (new Vector4(-command.direction, 0.0f) * Matrix4.Transpose(renderEngine.context.invViewMatrix)).Xyz;
        }

        public override void PerformCommand(DirectionallightRenderCommand command, RenderEngine renderEngine)
        {
            BindShadows(command, renderEngine);
            Vector3 lightDirectionViewSpace = ViewSpaceDirection(command, renderEngine);

            renderEngine.Msaa.DrawSplit(_directionalLightShader, _directionalLightSampleShader, shader =>
            {
                LoadShadows(shader, command, renderEngine);
                shader.loadUniformVector3f("LightDirectionViewSpace", lightDirectionViewSpace);
                shader.loadUniformVector3f("lightColour", command.colour);

                renderEngine.ScreenQuadRenderer.Render(clearColor: false, blend: true);
            });

            ReleaseShadows(command);
        }

    }
}
