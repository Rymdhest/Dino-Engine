using Dino_Engine.Core;
using Dino_Engine.Rendering.Renderers.AntiAliasing;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    public struct AmbientLightRenderCommand : IRenderCommand
    {
        public Vector3 sky;     // Radiance from above.
        public Vector3 ground;  // Radiance from below.
    }

    /// <summary>
    /// The first of the light passes: the sky and the ground around every surface, and the light surfaces give off
    /// themselves (Ambient_Light.frag). It stands apart from the lights, so that each of them is only what it looks like.
    /// </summary>
    public class AmbientLightRenderer : CommandDrivenRenderer<AmbientLightRenderCommand>
    {
        // Per pixel, and per sample for the edge pixels (MsaaRenderer.DrawSplit).
        private readonly ShaderProgram _shader = new ShaderProgram("Simple.vert", "Ambient_Light.frag");
        private readonly ShaderProgram _sampleShader = new ShaderProgram("Simple.vert", "Ambient_Light.frag", new[] { "PER_SAMPLE" });

        public AmbientLightRenderer() : base("Ambient Light")
        {
            foreach (ShaderProgram shader in new[] { _shader, _sampleShader })
            {
                shader.bind();
                shader.loadUniformInt("gAlbedo", 0);
                shader.loadUniformInt("gNormal", 1);
                shader.loadUniformInt("gMaterials", 2);
                shader.loadUniformInt("gDepth", 3);
                shader.loadUniformInt("ambientOcclusion", MsaaRenderer.AmbientOcclusionUnit);
                shader.unBind();
            }
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            renderEngine.Msaa.BindGeometrySamples(renderEngine.AmbientOcclusionTexture);
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {
            // More than one means frames were skipped (the F8 debug view bypasses the light passes). Only the newest is current.
            if (commands.Count > 0) PerformCommand(commands[commands.Count - 1], renderEngine);
            commands.Clear();
        }

        public override void PerformCommand(AmbientLightRenderCommand command, RenderEngine renderEngine)
        {
            LightingSettings lighting = renderEngine.Lighting;
            renderEngine.Msaa.DrawSplit(_shader, _sampleShader, shader =>
            {
                shader.loadUniformVector3f("skyLight", command.sky);
                shader.loadUniformVector3f("groundLight", command.ground);
                shader.loadUniformFloat("emissionScale", lighting.Emission ? lighting.EmissionScale : 0f);
                renderEngine.ScreenQuadRenderer.Render(clearColor: false, blend: true);
            });
        }

        public override void CleanUp()
        {
            _shader.cleanUp();
            _sampleShader.cleanUp();
        }
    }
}
