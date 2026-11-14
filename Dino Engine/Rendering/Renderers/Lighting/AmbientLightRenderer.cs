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
    /// The first of the light passes: the sky and the ground around every surface, the light surfaces give off themselves,
    /// and the sun (Ambient_Light.frag). Whatever else lights the scene is added on top of this.
    /// </summary>
    public class AmbientLightRenderer : CommandDrivenRenderer<AmbientLightRenderCommand>
    {
        // Per pixel, and per sample for the edge pixels (MsaaRenderer.DrawSplit).
        private readonly ShaderProgram _shader = new ShaderProgram("Simple.vert", "Ambient_Light.frag");
        private readonly ShaderProgram _sampleShader = new ShaderProgram("Simple.vert", "Ambient_Light.frag", new[] { "PER_SAMPLE" });
        // The sun through flesh, added on characters alone (Flesh_Light.frag, SUBSURFACE.md), likewise.
        private readonly ShaderProgram _fleshShader = new ShaderProgram("Simple.vert", "Flesh_Light.frag");
        private readonly ShaderProgram _fleshSampleShader = new ShaderProgram("Simple.vert", "Flesh_Light.frag", new[] { "PER_SAMPLE" });

        private const int SkyMapUnit = 10;
        private const int GatheredUnit = 11;    // What global illumination gathered (GlobalIlluminationRenderer.BindGathered).

        /// <summary>What the ground throws back up, this frame. It's what a reflection pointing down sees.</summary>
        public Vector3 GroundLight { get; private set; }

        /// <summary>What the sky gives a surface facing up, this frame, for what's lit after the light passes: particles.</summary>
        public Vector3 SkyLight { get; private set; }

        public AmbientLightRenderer() : base("Ambient and Sun")
        {
            foreach (ShaderProgram shader in new[] { _shader, _sampleShader })
            {
                shader.bind();
                shader.loadUniformInt("gAlbedo", 0);
                shader.loadUniformInt("gNormal", 1);
                shader.loadUniformInt("gMaterials", 2);
                shader.loadUniformInt("gDepth", 3);
                MsaaRenderer.LinkScreenTerms(shader);
                shader.loadUniformInt("skyMap", SkyMapUnit);
                shader.loadUniformInt("giGathered", GatheredUnit);
                DirectionalLightRenderer.LinkShadows(shader);
                shader.unBind();
            }
            foreach (ShaderProgram shader in new[] { _fleshShader, _fleshSampleShader })
            {
                shader.bind();
                shader.loadUniformInt("gAlbedo", 0);
                shader.loadUniformInt("gNormal", 1);
                shader.loadUniformInt("gMaterials", 2);
                shader.loadUniformInt("gDepth", 3);
                MsaaRenderer.LinkScreenTerms(shader);
                shader.loadUniformInt("fleshDepthMap", FleshDepth.Unit);
                DirectionalLightRenderer.LinkShadows(shader);
                shader.unBind();
            }
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
            renderEngine.Msaa.BindGeometrySamples(renderEngine.AmbientOcclusionTexture);
            GL.ActiveTexture(TextureUnit.Texture0 + SkyMapUnit);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine._weatherRenderer.SkyMapTexture);
            GL.ActiveTexture(TextureUnit.Texture0);
            renderEngine._globalIllumination.BindGathered(GatheredUnit);
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
            GroundLight = command.ground;
            SkyLight = command.sky;

            DirectionalLightRenderer directional = renderEngine._directionalLightRenderer;
            bool sunUp = directional.TakeSun(out DirectionallightRenderCommand sun);
            // Its block maps were made before this pass began (RenderEngine.LightPass), as GPU timers don't nest.
            if (sunUp) directional.BindShadows(sun, renderEngine);
            var gi = renderEngine._globalIllumination;
            GlobalIllumination.GlobalIlluminationSettings giSettings = renderEngine.GlobalIllumination;
            float giMix = gi.Active ? gi.Presence : 0f;

            // The first thing to touch the scene, so that there's nothing to add to: it only has to be written.
            renderEngine.Msaa.DrawSplit(_shader, _sampleShader, shader =>
            {
                shader.loadUniformVector3f("skyLight", command.sky);
                shader.loadUniformVector3f("groundLight", command.ground);
                shader.loadUniformFloat("emissionScale", lighting.Emission ? lighting.EmissionScale : 0f);
                shader.loadUniformFloat("giMix", giMix);
                shader.loadUniformFloat("giStrength", giSettings.Strength);
                shader.loadUniformInt("giDebug", giMix > 0f ? giSettings.Debug : 0);
                shader.loadUniformInt("occlusionDebug", renderEngine.Occlusion.Debug);
                shader.loadUniformInt("contactDebug", renderEngine.ContactShadows.Debug);
                shader.loadUniformInt("shadowDebug", sunUp ? renderEngine.SunShadows.Debug : 0);
                shader.loadUniformVector3f("sunColour", sunUp ? sun.colour : Vector3.Zero);
                if (sunUp)
                {
                    shader.loadUniformVector3f("sunDirectionViewSpace", DirectionalLightRenderer.ViewSpaceDirection(sun, renderEngine).Normalized());
                    DirectionalLightRenderer.LoadShadows(shader, sun, renderEngine);
                }
                renderEngine.ScreenQuadRenderer.Render(clearColor: false, blend: false);
            });

            // The sun through thin flesh, measured in a map of flesh alone from it (FleshDepth), while its own shadows are still bound to say what stood in the way.
            if (sunUp && FleshDepth.Drawn) DrawFlesh(sun, renderEngine);

            if (sunUp) directional.ReleaseShadows(sun);
        }

        /// <summary>
        /// Adds what of the sun gets through flesh, on characters alone: where the characters' stencil bit is set, per pixel where a pixel's samples agree and per sample on the
        /// edge pixels, as <see cref="MsaaRenderer.DrawSplit"/> would. Kept out of the ambient pass, so that nothing but a character carries what it reads and works out.
        /// </summary>
        private void DrawFlesh(DirectionallightRenderCommand sun, RenderEngine renderEngine)
        {
            FleshDepth.Bind();
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.Enable(EnableCap.StencilTest);
            GL.StencilMask(0);
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
            const int Both = MsaaRenderer.EdgeStencilBit | MsaaRenderer.CharacterStencilBit;
            Vector3 towardsSun = DirectionalLightRenderer.ViewSpaceDirection(sun, renderEngine).Normalized();
            Matrix4 fleshSpace = FleshDepth.ViewSpaceMatrix(renderEngine);

            void Draw(ShaderProgram shader, int reference)
            {
                shader.bind();
                GL.StencilFunc(StencilFunction.Equal, reference, Both);
                shader.loadUniformVector3f("sunDirectionViewSpace", towardsSun);
                shader.loadUniformVector3f("sunColour", sun.colour);
                shader.loadUniformMatrix4f("fleshSpaceMatrix", fleshSpace);
                DirectionalLightRenderer.LoadShadows(shader, sun, renderEngine);
                renderEngine.ScreenQuadRenderer.Render(clearColor: false, blend: true);
            }
            Draw(_fleshShader, MsaaRenderer.CharacterStencilBit);
            if (renderEngine.Msaa.Samples > 1) Draw(_fleshSampleShader, Both);

            GL.StencilMask(0xFF);
            GL.Disable(EnableCap.StencilTest);
            GL.Disable(EnableCap.Blend);
            FleshDepth.Release();
            // Used: next frame's flesh has to be drawn into it again before it's read, so a world with no sun to draw it never reads an old one.
            FleshDepth.Skip();
        }

        public override void CleanUp()
        {
            _shader.cleanUp();
            _sampleShader.cleanUp();
            _fleshShader.cleanUp();
            _fleshSampleShader.cleanUp();
        }
    }
}
