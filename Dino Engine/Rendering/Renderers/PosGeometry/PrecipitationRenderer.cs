using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.Rendering.Renderers.Simulation;
using Dino_Engine.World;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.PosGeometry
{
    /// <summary>
    /// Whatever the sky is dropping: rain, snow and ash, in the multisampled scene after the fog. Nothing is simulated or stored. A streak's place follows from its number and a
    /// few running totals (Weather/Precipitation.vert), so what's in the air costs a draw call and a few thousand vertices. Splashes come up on the rain shadow's own surface
    /// (Weather/Splash.vert), wherever the rain is really landing, and water comes off whatever's holding it (Weather/Drip.vert) until well after the rain stops.
    /// </summary>
    public class PrecipitationRenderer : RenderPassRenderer
    {
        private const int RainKind = 0;
        private const int FlakeKind = 1;

        private const int MapUnit = 0;
        private const int RainShadowUnit = 1;
        private const int CloudShadowUnit = 2;
        private const int SceneBlurUnit = 3;
        private const int DripSourceUnit = 4;

        private static readonly Vector3 AshTint = new Vector3(0.62f, 0.6f, 0.6f);
        private static readonly Vector3 SnowTint = new Vector3(1.8f, 1.85f, 1.95f);

        private readonly ShaderProgram _fallShader = new ShaderProgram("Weather/Precipitation.vert", "Weather/Precipitation.frag");
        private readonly ShaderProgram _splashShader = new ShaderProgram("Weather/Splash.vert", "Weather/Splash.frag");
        private readonly ShaderProgram _dripShader = new ShaderProgram("Weather/Drip.vert", "Weather/Drip.frag");
        private readonly int _emptyVao;

        public PrecipitationRenderer() : base("Precipitation")
        {
            foreach (ShaderProgram shader in new[] { _fallShader, _splashShader })
            {
                shader.bind();
                shader.loadUniformInt("weatherMapA", MapUnit);
                shader.loadUniformInt("rainShadowMap", RainShadowUnit);
                shader.loadUniformInt("cloudShadowMap", CloudShadowUnit);
                shader.loadUniformInt("sceneBlur", SceneBlurUnit);
            }
            _dripShader.bind();
            _dripShader.loadUniformInt("weatherMapA", MapUnit);
            _dripShader.loadUniformInt("cloudShadowMap", CloudShadowUnit);
            _dripShader.loadUniformInt("sceneBlur", SceneBlurUnit);
            _dripShader.loadUniformInt("dripSources", DripSourceUnit);
            _dripShader.unBind();
            _emptyVao = GL.GenVertexArray();
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {
            if (renderEngine._weatherRenderer.Frame is not WeatherRenderCommand frame) return;
            WeatherSingleton weather = frame.weather;
            WeatherSample here = weather.Here;
            WeatherRenderer renderer = renderEngine._weatherRenderer;
            if (here.Fall < 0.004f && !renderer.DripsOn) return;

            renderEngine.Msaa.SceneBuffer.bind();
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.DepthMask(false);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.Blend);
            GL.BlendFuncSeparate(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.Zero, BlendingFactorDest.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.BindVertexArray(_emptyVao);

            Bind(MapUnit, renderer.SurfaceMapTexture);
            Bind(RainShadowUnit, renderer.RainShadowTexture);
            Bind(CloudShadowUnit, renderer.CloudShadowTexture);
            Bind(SceneBlurUnit, renderEngine.SceneBlurTexture);
            Bind(DripSourceUnit, renderer.DripSourceTexture);
            GL.ActiveTexture(TextureUnit.Texture0);

            Vector3 eye = renderEngine.context.viewPos;
            Vector3 centre = (frame.focus + eye) * 0.5f;
            float rain = (1f - here.Snow) * (1f - here.Ash);
            float flakes = 1f - rain;

            if (rain > 0.01f && here.Fall > 0.004f)
            {
                _fallShader.bind();
                LoadFall(_fallShader, frame, centre, RainKind, weather.RainFallen, weather.RainSpeed, weather.RainExposure, 0.016f, weather.RainOpacity, Vector3.One, rain);
                _fallShader.loadUniformFloat("hides", 0.3f);
                GL.DrawArraysInstanced(PrimitiveType.TriangleStrip, 0, 4, weather.RainDrops);

                DrawSplashes(frame, renderEngine, centre);
            }

            if (flakes > 0.01f && here.Fall > 0.004f)
            {
                Vector3 tint = Vector3.Lerp(SnowTint, AshTint, here.Ash / MathF.Max(here.Ash + here.Snow, 1e-3f));
                _fallShader.bind();
                LoadFall(_fallShader, frame, centre, FlakeKind, weather.SnowFallen, weather.SnowSpeed, 0f, 0.075f, 0.6f, tint, flakes);
                _fallShader.loadUniformFloat("hides", 0.85f);
                GL.DrawArraysInstanced(PrimitiveType.TriangleStrip, 0, 4, weather.Flakes);
            }

            if (renderer.DripsOn) DrawDrips(frame, renderer);

            GL.BindVertexArray(0);
            GL.UseProgram(0);
            GL.Disable(EnableCap.Blend);
            GL.DepthFunc(DepthFunction.Less);
            GL.Enable(EnableCap.CullFace);
        }

        private static void LoadFall(ShaderProgram shader, in WeatherRenderCommand frame, Vector3 centre, int kind, float fallen, float speed, float exposure, float width, float opacity, Vector3 tint, float share)
        {
            WeatherSingleton weather = frame.weather;
            shader.loadUniformInt("fallKind", kind);
            shader.loadUniformVector3f("volumeCentre", centre);
            shader.loadUniformFloat("volumeSize", weather.VolumeSize);
            shader.loadUniformFloat("fallen", fallen);
            shader.loadUniformVector2f("drifted", weather.AirDrift);
            shader.loadUniformVector2f("windVelocity", frame.wind);
            shader.loadUniformFloat("fallSpeed", speed);
            shader.loadUniformFloat("exposure", exposure);
            shader.loadUniformFloat("width", width);
            shader.loadUniformFloat("opacity", opacity);
            shader.loadUniformVector3f("tint", tint);
            shader.loadUniformFloat("share", share);
        }

        private void DrawSplashes(in WeatherRenderCommand frame, RenderEngine renderEngine, Vector3 centre)
        {
            WeatherSingleton weather = frame.weather;
            _splashShader.bind();
            _splashShader.loadUniformVector3f("focus", frame.focus);
            _splashShader.loadUniformFloat("reach", weather.VolumeSize);
            _splashShader.loadUniformFloat("splashClock", weather.SplashClock);
            _splashShader.loadUniformFloat("waterLevel", WorldPlan.SeaLevel);
            _splashShader.loadUniformMatrix4f("rainShadowInverse", renderEngine._weatherRenderer.RainShadowInverse);
            _splashShader.loadUniformFloat("opacity", 0.32f);
            GL.DrawArraysInstanced(PrimitiveType.TriangleStrip, 0, 4, weather.Splashes);
        }

        private void DrawDrips(in WeatherRenderCommand frame, WeatherRenderer renderer)
        {
            _dripShader.bind();
            _dripShader.loadUniformVector2f("windVelocity", frame.wind);
            _dripShader.loadUniformFloat("opacity", frame.weather.DripOpacity);
            _dripShader.loadUniformInt("dripStage", 0);
            GL.DrawArraysInstanced(PrimitiveType.TriangleStrip, 0, 4, renderer.DripCount);
            _dripShader.loadUniformFloat("opacity", frame.weather.DripOpacity * 0.5f);
            _dripShader.loadUniformInt("dripStage", 1);
            GL.DrawArraysInstanced(PrimitiveType.TriangleStrip, 0, 4, renderer.DripCount);
        }

        private static void Bind(int unit, int texture)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + unit);
            GL.BindTexture(TextureTarget.Texture2D, texture);
        }

        public override void CleanUp()
        {
            _fallShader.cleanUp();
            _splashShader.cleanUp();
            _dripShader.cleanUp();
            GL.DeleteVertexArray(_emptyVao);
        }
    }
}
