using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.World;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.Simulation
{
    public struct WeatherRenderCommand : IRenderCommand
    {
        public WeatherSingleton weather;
        public float[]? map;        // The whole world's weather, as WeatherField.Bake lays it out. Null with no weather on.
        public float mapSize;       // Metres across.
        public Vector3 focus;       // What's being looked at.
        public Vector2 wind;        // Mean wind in m/s.
        public float deltaTime;
    }

    /// <summary>
    /// The GPU side of the engine's weather, run at the start of every frame before anything is drawn:
    /// 1. Uploads the frame's weather (WeatherData uniform block, binding <see cref="UniformBinding"/>) and the world's weather map for Shaders/Weather/Weather.glsl.
    /// 2. Draws the rain rings (Rain_Ripples.frag), while anything is falling.
    /// 3. Now and then, the sky and its clouds (Sky_Map.frag) and the shadows they cast on the land (Cloud_Shadow.frag). They drift slowly, and everything reading them is soft.
    /// 4. What drips do to the ground around the view (Drip_Surface.frag), while anything is wet.
    /// And later in the frame the rain shadow: the world seen from the way the rain falls, as depth, drawn by the same shadow passes the sun uses. It's only redrawn when the view has moved
    /// far enough to need it. While anything is wet the same passes draw the world from straight overhead, which is where the drips come from: the rain shadow leans with the wind, and a roof's
    /// windward edge doesn't show as an edge in it at all.
    /// </summary>
    public class WeatherRenderer : CommandDrivenRenderer<WeatherRenderCommand>
    {
        public const int UniformBinding = 9;

        // Texture units the geometry shaders read weather from (Weather/Wetness.glsl). No geometry renderer uses them for anything else.
        public const int SurfaceMapUnit = 12;
        public const int RainShadowUnit = 13;
        public const int RippleUnit = 14;
        public const int DripSurfaceUnit = 15;

        public const int MaxMaterials = 128;

        private const int SkyMapResolution = 512;
        private const int SkyMapLevels = 9;
        private const int SkyInterval = 4;              // Frames between redraws of the sky.
        private const int CloudShadowResolution = 256;
        private const float CloudShadowSize = 1024f;
        private const int RippleResolution = 256;
        private const int RippleLevels = 5;
        private const int RainShadowResolution = 1024;
        private const float RainShadowSize = 192f;
        private const float RainShadowRange = 512f;
        private const float RainShadowStep = 12f;       // Metres the view moves before the rain shadow is redrawn around it.
        private const float RainShadowLife = 3f;        // Seconds at most between redraws, for whatever has streamed in.
        private const int DripDepthResolution = 1024;
        private const float DripDepthSize = 128f;       // Metres. Texels of an eighth of a metre, so the rain shadow's step moves it by whole tiles and a place keeps its drips.
        private const float DripDepthRange = 512f;
        private const int DripTile = 8;                 // Texels along a side of a tile, which holds at most one place that drips. Must match Drip_Sources.frag.
        private const int DripTiles = DripDepthResolution / DripTile;
        private const int DripSurfaceResolution = 1024;
        private const float DripSurfaceSize = 80f;      // Metres: the whole view at the furthest zoom.

        // WeatherData in Weather/Weather.glsl: 14 vec4s, a matrix, a vec4, then the materials.
        private const int MatrixSlot = 14;
        private const int RainInfoSlot = MatrixSlot + 4;
        private const int MaterialSlot = RainInfoSlot + 1;
        private const int UniformVec4s = MaterialSlot + MaxMaterials / 4;

        private readonly ShaderProgram _skyShader = new ShaderProgram("Simple.vert", "Weather/Sky_Map.frag");
        private readonly ShaderProgram _cloudShadowShader = new ShaderProgram("Simple.vert", "Weather/Cloud_Shadow.frag");
        private readonly ShaderProgram _rippleShader = new ShaderProgram("Simple.vert", "Weather/Rain_Ripples.frag");
        private readonly ShaderProgram _dripSourceShader = new ShaderProgram("Simple.vert", "Weather/Drip_Sources.frag");
        private readonly ShaderProgram _dripSurfaceShader = new ShaderProgram("Weather/Drip_Surface.vert", "Weather/Drip_Surface.frag");

        private readonly int _uniformBuffer;
        private readonly float[] _uniformData = new float[UniformVec4s * 4];

        private readonly int _mapA;
        private readonly int _mapB;
        private readonly float[] _mapDataA = new float[WeatherField.MapResolution * WeatherField.MapResolution * 4];
        private readonly float[] _mapDataB = new float[WeatherField.MapResolution * WeatherField.MapResolution * 4];

        private readonly int _skyMap;
        private readonly int _skyFramebuffer;
        private readonly int _ripples;
        private readonly int _rippleFramebuffer;
        private readonly int _cloudShadow;
        private readonly int _cloudShadowFramebuffer;
        private readonly int _dripSources;
        private readonly int _dripSourceFramebuffer;
        private readonly int _dripSurface;
        private readonly int _dripSurfaceFramebuffer;
        private readonly int _emptyVao;

        private Shadow _rainShadow;
        private Matrix4 _rainMatrix = Matrix4.Identity;
        private Matrix4 _rainMatrixInverse = Matrix4.Identity;
        private Matrix4 _rainPending = Matrix4.Identity;
        private bool _rainCaptured;
        private bool _rainDue;
        private TerrainChunkRenderData[] _rainChunks = Array.Empty<TerrainChunkRenderData>();
        private Vector2 _rainCentre = new Vector2(float.MaxValue);
        private Vector3 _rainDirection = -Vector3.UnitY;
        private float _rainAge = float.MaxValue;

        private Shadow _dripDepth;
        private Matrix4 _dripPending = Matrix4.Identity;
        private bool _dripDue;
        private bool _dripCaptured;
        private Vector2 _dripCorner;
        private bool _dripSurfaceCleared;
        private bool _wetNearby;

        private int _frame;
        private bool _rippleCleared;
        private bool _weatherOn;

        /// <summary>This frame's weather, for what draws it; null with none on.</summary>
        public WeatherRenderCommand? Frame { get; private set; }

        /// <summary>Everything that reflects or looks at the sky reads this (Weather/Sky.glsl).</summary>
        public int SkyMapTexture => _skyMap;
        /// <summary>Depth from the way the rain falls (Weather/RainShadow.glsl).</summary>
        public int RainShadowTexture => _rainShadow.shadowFrameBuffer.getDepthAttachment();
        public int CloudShadowTexture => _cloudShadow;
        public int RippleTexture => _ripples;
        /// <summary>x wet, y puddles, z settled snow, w falling.</summary>
        public int SurfaceMapTexture => _mapA;
        /// <summary>From the rain shadow's clip space back to the world.</summary>
        public Matrix4 RainShadowInverse => _rainMatrixInverse;
        /// <summary>Where water comes off things (Weather/Drip.glsl).</summary>
        public int DripSourceTexture => _dripSources;
        /// <summary>How many places can drip: instances for whatever draws them.</summary>
        public int DripCount => DripTiles * DripTiles;
        /// <summary>Whether there's anything that could be dripping.</summary>
        public bool DripsOn => _dripCaptured && _wetNearby;

        public WeatherRenderer() : base("Weather")
        {
            foreach (ShaderProgram shader in new[] { _skyShader, _cloudShadowShader })
            {
                shader.bind();
                shader.loadUniformInt("weatherMapB", 1);
                shader.loadUniformInt("cloudNoise", 2);
            }
            _skyShader.unBind();

            _uniformBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, _uniformBuffer);
            GL.BufferData(BufferTarget.UniformBuffer, _uniformData.Length * sizeof(float), _uniformData, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, UniformBinding, _uniformBuffer);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);

            _mapA = CreateMap();
            _mapB = CreateMap();

            _skyMap = CreateTarget(SkyMapResolution, SkyMapLevels, SizedInternalFormat.Rgba16f, TextureWrapMode.ClampToEdge, out _skyFramebuffer);
            _ripples = CreateTarget(RippleResolution, RippleLevels, SizedInternalFormat.Rg16f, TextureWrapMode.Repeat, out _rippleFramebuffer);
            _cloudShadow = CreateTarget(CloudShadowResolution, 1, SizedInternalFormat.R16f, TextureWrapMode.ClampToEdge, out _cloudShadowFramebuffer);
            Clear(_cloudShadowFramebuffer, 1f);
            Clear(_rippleFramebuffer, 0f);

            _dripSources = CreateTarget(DripTiles, 1, SizedInternalFormat.Rgba32f, TextureWrapMode.ClampToEdge, out _dripSourceFramebuffer);
            // Nothing past its edge: what's out there is too far away to see.
            _dripSurface = CreateTarget(DripSurfaceResolution, 1, SizedInternalFormat.Rgba16f, TextureWrapMode.ClampToBorder, out _dripSurfaceFramebuffer);
            Clear(_dripSourceFramebuffer, 0f);
            Clear(_dripSurfaceFramebuffer, 0f);
            _dripSourceShader.bind();
            _dripSourceShader.loadUniformInt("dripDepth", 0);
            _dripSourceShader.loadUniformFloat("depthRange", DripDepthRange);
            _dripSurfaceShader.bind();
            _dripSurfaceShader.loadUniformInt("weatherMapA", 0);
            _dripSurfaceShader.loadUniformInt("dripSources", 1);
            _dripSurfaceShader.unBind();
            _emptyVao = GL.GenVertexArray();

            // A shadow map to the passes that draw into one, and plain depth to everything that reads it.
            Matrix4 projection = Matrix4.CreateOrthographic(RainShadowSize, RainShadowSize, 0f, RainShadowRange);
            _rainShadow = new Shadow(new Vector2i(RainShadowResolution), projection, RainShadowSize, 0.2f, 0f);
            GL.BindTexture(TextureTarget.Texture2D, RainShadowTexture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.None);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            _rainShadow.shadowFrameBuffer.ClearDepth();
            _rainShadow.shadowFrameBuffer.unbind();

            _dripDepth = new Shadow(new Vector2i(DripDepthResolution), Matrix4.CreateOrthographic(DripDepthSize, DripDepthSize, 0f, DripDepthRange), DripDepthSize, 0.2f, 0f);
            GL.BindTexture(TextureTarget.Texture2D, _dripDepth.shadowFrameBuffer.getDepthAttachment());
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.None);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            _dripDepth.shadowFrameBuffer.ClearDepth();
            _dripDepth.shadowFrameBuffer.unbind();
        }

        private static int CreateMap()
        {
            int texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexStorage2D(TextureTarget2d.Texture2D, 1, SizedInternalFormat.Rgba16f, WeatherField.MapResolution, WeatherField.MapResolution);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return texture;
        }

        private static int CreateTarget(int resolution, int levels, SizedInternalFormat format, TextureWrapMode wrap, out int framebuffer)
        {
            int texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexStorage2D(TextureTarget2d.Texture2D, levels, format, resolution, resolution);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)(levels > 1 ? TextureMinFilter.LinearMipmapLinear : TextureMinFilter.Linear));
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrap);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrap);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            framebuffer = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texture, 0);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            return texture;
        }

        private static void Clear(int framebuffer, float value)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
            GL.ClearColor(value, value, value, value);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        /// <summary>How thirsty each material of the texture arrays is, in their order. Set once they're all made.</summary>
        public void SetMaterialPorosity(float[] porosity)
        {
            int count = Math.Min(porosity.Length, MaxMaterials);
            Array.Copy(porosity, 0, _uniformData, MaterialSlot * 4, count);
        }

        /// <summary>Points a geometry shader at the weather (Weather/Wetness.glsl). Call once, with the shader bound.</summary>
        public static void LinkSurfaceSamplers(ShaderProgram shader)
        {
            shader.loadUniformInt("weatherMapA", SurfaceMapUnit);
            shader.loadUniformInt("rainShadowMap", RainShadowUnit);
            shader.loadUniformInt("rainRipples", RippleUnit);
            shader.loadUniformInt("dripSurface", DripSurfaceUnit);
        }

        /// <summary>Binds what geometry shaders read, for the whole of the geometry pass.</summary>
        public void BindSurfaceMaps()
        {
            GL.ActiveTexture(TextureUnit.Texture0 + SurfaceMapUnit);
            GL.BindTexture(TextureTarget.Texture2D, _mapA);
            GL.ActiveTexture(TextureUnit.Texture0 + RainShadowUnit);
            GL.BindTexture(TextureTarget.Texture2D, RainShadowTexture);
            GL.ActiveTexture(TextureUnit.Texture0 + RippleUnit);
            GL.BindTexture(TextureTarget.Texture2D, _ripples);
            GL.ActiveTexture(TextureUnit.Texture0 + DripSurfaceUnit);
            GL.BindTexture(TextureTarget.Texture2D, _dripSurface);
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {
            Frame = null;
            if (commands.Count == 0)
            {
                _weatherOn = false;
                UploadWeatherData(default, renderEngine);
                return;
            }

            // Only the newest is current; older ones pile up when frames skip the render (the F8 debug view).
            PerformCommand(commands[commands.Count - 1], renderEngine);
            commands.Clear();
        }

        public override void PerformCommand(WeatherRenderCommand command, RenderEngine renderEngine)
        {
            _weatherOn = command.map != null;
            if (_weatherOn) Frame = command;
            if (_weatherOn) UploadMaps(command.map!);
            UploadWeatherData(command, renderEngine);

            int fresh = _frame++;
            WeatherSingleton weather = command.weather;

            if (_weatherOn && weather.Here.Fall > 0.002f)
            {
                DrawRipples(weather, renderEngine);
                _rippleCleared = false;
            }
            else if (!_rippleCleared)
            {
                Clear(_rippleFramebuffer, 0f);
                _rippleCleared = true;
            }

            if (DripsOn)
            {
                DrawDripSurface();
                _dripSurfaceCleared = false;
            }
            else if (!_dripSurfaceCleared)
            {
                Clear(_dripSurfaceFramebuffer, 0f);
                _dripSurfaceCleared = true;
            }

            // Fair weather only needs the sky: there's nothing for the sun to get past.
            if (fresh % SkyInterval == 0) DrawSky(renderEngine);
            if (fresh % 2 == 1)
            {
                if (_weatherOn) DrawCloudShadow(renderEngine);
                else Clear(_cloudShadowFramebuffer, 1f);
            }
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            Engine.WindowHandler.refreshViewport();
        }

        private void UploadMaps(float[] map)
        {
            int texels = WeatherField.MapResolution * WeatherField.MapResolution;
            for (int i = 0; i < texels; i++)
            {
                int from = i * WeatherField.MapChannels;
                int to = i * 4;
                _mapDataA[to] = map[from];
                _mapDataA[to + 1] = map[from + 1];
                _mapDataA[to + 2] = map[from + 2];
                _mapDataA[to + 3] = map[from + 3];
                _mapDataB[to] = map[from + 4];
                _mapDataB[to + 1] = map[from + 5];
                _mapDataB[to + 2] = map[from + 6];
                _mapDataB[to + 3] = map[from + 7];
            }
            GL.BindTexture(TextureTarget.Texture2D, _mapA);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, WeatherField.MapResolution, WeatherField.MapResolution, PixelFormat.Rgba, PixelType.Float, _mapDataA);
            GL.BindTexture(TextureTarget.Texture2D, _mapB);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, WeatherField.MapResolution, WeatherField.MapResolution, PixelFormat.Rgba, PixelType.Float, _mapDataB);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        private void UploadWeatherData(in WeatherRenderCommand command, RenderEngine renderEngine)
        {
            WeatherSingleton weather = command.weather;
            WeatherSample here = weather.Here;

            // Whether anything near enough to be seen is wet: dry land skips everything in Wetness.glsl.
            _wetNearby = _weatherOn && WetNearby(command);

            Vector3 fall = Fall(weather, command.wind);
            _rainDirection = fall;
            Vector3 sun = renderEngine.context.sunDirection;
            Vector3 sunColour = renderEngine.context.sunColour;

            // Whole texels, so the shadows don't shimmer as the view moves.
            float cloudTexel = CloudShadowSize / CloudShadowResolution;
            Vector2 cloudCorner = new Vector2(
                MathF.Floor((command.focus.X - CloudShadowSize * 0.5f) / cloudTexel) * cloudTexel,
                MathF.Floor((command.focus.Z - CloudShadowSize * 0.5f) / cloudTexel) * cloudTexel);

            Vector3 clearSky = renderEngine.context.clearSkyColour;
            Write(0, here.Cloud, here.Storm, here.Mist, here.Dust);
            Write(1, here.Snow, here.Ash, _weatherOn ? 1f : 0f, _wetNearby ? 1f : 0f);
            Write(2, 0f, 0f, MathF.Max(command.mapSize, 1f), MathF.Max(command.mapSize, 1f));
            Write(3, weather.Flash.X, weather.Flash.Y, weather.Flash.Z, 0f);
            Write(4, fall.X, fall.Y, fall.Z, weather.RainSpeed);
            Write(5, sun.X, sun.Y, sun.Z, Math.Clamp(weather.CloudShadow, 0f, 1f));
            Write(6, sunColour.X, sunColour.Y, sunColour.Z, PosGeometry.SkyRenderer.SkyStrength);
            Write(7, weather.CloudAltitude, MathF.Max(weather.CloudThickness, 1f), 1f / MathF.Max(weather.CloudSize, 1f), weather.CloudPhase);
            Write(8, weather.CloudOffset.X, weather.CloudOffset.Y, 0f, 0f);
            Write(9, cloudCorner.X, cloudCorner.Y, CloudShadowSize, CloudShadowSize);
            Write(10, Math.Clamp(weather.WetDarkening, 0f, 1f), Math.Clamp(weather.WetGloss, 0f, 1f), MathF.Max(weather.PuddleLevel, 0f), 1f / MathF.Max(weather.RippleSize, 0.1f));
            Write(11, MathF.Max(weather.RippleStrength, 0f), clearSky.X, clearSky.Y, clearSky.Z);

            // Whole texels, and redrawn every frame, so nothing crawls as the view moves.
            float dripTexel = DripSurfaceSize / DripSurfaceResolution;
            _dripCorner = new Vector2(
                MathF.Floor((command.focus.X - DripSurfaceSize * 0.5f) / dripTexel) * dripTexel,
                MathF.Floor((command.focus.Z - DripSurfaceSize * 0.5f) / dripTexel) * dripTexel);
            Write(12, weather.DripClock, Math.Clamp(weather.DripRate, 0f, 1f), 0f, 0f);
            Write(13, _dripCorner.X, _dripCorner.Y, DripSurfaceSize, DripSurfaceSize);
            WriteRainShadow();

            GL.BindBuffer(BufferTarget.UniformBuffer, _uniformBuffer);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, _uniformData.Length * sizeof(float), _uniformData);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, UniformBinding, _uniformBuffer);
        }

        // The way things fall, leaning with the wind. A strong wind drives rain well over.
        private static Vector3 Fall(in WeatherSingleton weather, Vector2 wind)
        {
            float speed = MathF.Max(weather.RainSpeed, 0.1f);
            return new Vector3(wind.X, -speed, wind.Y).Normalized();
        }

        private bool WetNearby(in WeatherRenderCommand command)
        {
            int resolution = WeatherField.MapResolution;
            float cell = MathF.Max(command.mapSize, 1f) / resolution;
            int reach = (int)MathF.Ceiling(RainShadowSize / cell) + 1;
            int cx = (int)(command.focus.X / cell), cz = (int)(command.focus.Z / cell);
            for (int z = Math.Max(cz - reach, 0); z <= Math.Min(cz + reach, resolution - 1); z++)
            {
                for (int x = Math.Max(cx - reach, 0); x <= Math.Min(cx + reach, resolution - 1); x++)
                {
                    int texel = (z * resolution + x) * 4;
                    if (_mapDataA[texel] + _mapDataA[texel + 1] + _mapDataA[texel + 2] + _mapDataA[texel + 3] > 0.004f) return true;
                }
            }
            return false;
        }

        private void DrawRipples(in WeatherSingleton weather, RenderEngine renderEngine)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _rippleFramebuffer);
            GL.Viewport(0, 0, RippleResolution, RippleResolution);
            _rippleShader.bind();
            _rippleShader.loadUniformFloat("rippleClock", weather.RippleClock);
            _rippleShader.loadUniformFloat("intensity", 0.25f + 0.75f * Math.Clamp(weather.Here.Fall, 0f, 1f));
            renderEngine.ScreenQuadRenderer.Render(clearColor: false);
            _rippleShader.unBind();

            GL.BindTexture(TextureTarget.Texture2D, _ripples);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        private void DrawDripSurface()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _dripSurfaceFramebuffer);
            GL.Viewport(0, 0, DripSurfaceResolution, DripSurfaceResolution);
            GL.ClearColor(0f, 0f, 0f, 0f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            _dripSurfaceShader.bind();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _mapA);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _dripSources);
            GL.ActiveTexture(TextureUnit.Texture0);

            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.BindVertexArray(_emptyVao);
            GL.DrawArraysInstanced(PrimitiveType.TriangleStrip, 0, 4, DripCount);
            GL.BindVertexArray(0);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.DepthTest);
            _dripSurfaceShader.unBind();
        }

        private void BindCloudInputs(RenderEngine renderEngine)
        {
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _mapA);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _mapB);
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture3D, renderEngine._volumetricFogRenderer.NoiseTexture);
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        private void DrawSky(RenderEngine renderEngine)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _skyFramebuffer);
            GL.Viewport(0, 0, SkyMapResolution, SkyMapResolution);
            _skyShader.bind();
            BindCloudInputs(renderEngine);
            renderEngine.ScreenQuadRenderer.Render(clearColor: false);
            _skyShader.unBind();

            GL.BindTexture(TextureTarget.Texture2D, _skyMap);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        private void DrawCloudShadow(RenderEngine renderEngine)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _cloudShadowFramebuffer);
            GL.Viewport(0, 0, CloudShadowResolution, CloudShadowResolution);
            _cloudShadowShader.bind();
            BindCloudInputs(renderEngine);
            renderEngine.ScreenQuadRenderer.Render(clearColor: false);
            _cloudShadowShader.unBind();
        }

        // ---------------------------------------------------------------- rain shadow

        /// <summary>
        /// Whether the rain shadow wants redrawing this frame, and the shadow to submit what's in it to if so: the view has moved on, the wind has turned the rain, or it's simply been a while.
        /// </summary>
        public bool WantsRainShadow(Vector3 focus, float deltaTime, out Shadow shadow)
        {
            shadow = _rainShadow;
            _rainAge += deltaTime;

            float step = RainShadowStep;
            Vector2 snapped = new Vector2(MathF.Floor(focus.X / step + 0.5f), MathF.Floor(focus.Z / step + 0.5f)) * step;
            bool moved = snapped != _rainCentre;
            if (!moved && _rainAge < RainShadowLife) return false;

            _rainCentre = snapped;
            _rainAge = 0f;

            // Looks along the rain from high above whatever's being looked at.
            Vector3 direction = _rainDirection;
            Vector3 centre = new Vector3(snapped.X, focus.Y, snapped.Y);
            Vector3 eye = centre - direction * (RainShadowRange * 0.5f);
            Vector3 up = MathF.Abs(direction.Y) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;
            _rainShadow.lightViewMatrix = Matrix4.LookAt(eye, centre, up);
            shadow = _rainShadow;
            _rainPending = _rainShadow.lightViewMatrix * _rainShadow.shadowProjectionMatrix;
            _rainDue = true;
            return true;
        }

        /// <summary>
        /// Whether the view from overhead wants redrawing, which it does whenever the rain shadow does, and the shadow to submit what's in it to if so. Dry land has no use for it.
        /// </summary>
        public bool WantsDripDepth(Vector3 focus, out Shadow shadow)
        {
            shadow = _dripDepth;
            if (!_wetNearby) return false;

            Vector3 eye = new Vector3(_rainCentre.X, focus.Y + DripDepthRange * 0.5f, _rainCentre.Y);
            _dripDepth.lightViewMatrix = Matrix4.LookAt(eye, eye - Vector3.UnitY, Vector3.UnitZ);
            shadow = _dripDepth;
            _dripPending = _dripDepth.lightViewMatrix * _dripDepth.shadowProjectionMatrix;
            _dripDue = true;
            return true;
        }

        /// <summary>The ground under the rain shadow, as it's being redrawn.</summary>
        public void SubmitRainShadowTerrain(TerrainChunkRenderData[] chunks) => _rainChunks = chunks;

        /// <summary>At the start of the shadow pass, before anything is drawn into it. Until then the last one is still in use.</summary>
        public void BeginRainShadow()
        {
            if (_rainDue) _rainShadow.shadowFrameBuffer.ClearDepth();
            if (_dripDue) _dripDepth.shadowFrameBuffer.ClearDepth();
        }

        /// <summary>At the end of the shadow pass: the ground goes in last, and from here on the new map is the one to read.</summary>
        public void FinishRainShadow(RenderEngine renderEngine)
        {
            if (!_rainDue) return;
            _rainDue = false;

            _rainShadow.shadowFrameBuffer.bind();
            renderEngine._terrainRenderer.RenderHeightCapture(_rainChunks, _rainPending);
            _rainShadow.shadowFrameBuffer.unbind();

            _rainMatrix = _rainPending;
            _rainMatrixInverse = Matrix4.Invert(_rainMatrix);
            _rainCaptured = true;

            WriteRainShadow();
            GL.BindBuffer(BufferTarget.UniformBuffer, _uniformBuffer);
            GL.BufferSubData(BufferTarget.UniformBuffer, (IntPtr)(MatrixSlot * 4 * sizeof(float)), 20 * sizeof(float), ref _uniformData[MatrixSlot * 4]);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);

            if (_dripDue) FinishDripDepth(renderEngine);
        }

        // The ground goes in last, as it does for the rain shadow, and water over it: a drop lands on a pond's surface and not on its bed. Then the edges are found once, for every frame till the next.
        private void FinishDripDepth(RenderEngine renderEngine)
        {
            _dripDue = false;

            _dripDepth.shadowFrameBuffer.bind();
            renderEngine._terrainRenderer.RenderHeightCapture(_rainChunks, _dripPending);
            renderEngine._waterRenderer.RenderHeightCapture(_dripPending);
            _dripDepth.shadowFrameBuffer.unbind();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _dripSourceFramebuffer);
            GL.Viewport(0, 0, DripTiles, DripTiles);
            _dripSourceShader.bind();
            _dripSourceShader.loadUniformMatrix4f("dripInverse", Matrix4.Invert(_dripPending));
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _dripDepth.shadowFrameBuffer.getDepthAttachment());
            renderEngine.ScreenQuadRenderer.Render(clearColor: false);
            _dripSourceShader.unBind();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            Engine.WindowHandler.refreshViewport();
            _dripCaptured = true;
        }

        private void WriteRainShadow()
        {
            Matrix4 matrix = _rainMatrix;
            int offset = MatrixSlot * 4;
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++) _uniformData[offset + row * 4 + column] = matrix[row, column];
            }
            Write(RainInfoSlot, _rainCaptured ? 1f : 0f, RainShadowRange, 0f, 0f);
        }

        private void Write(int vec4Index, float x, float y, float z, float w)
        {
            int offset = vec4Index * 4;
            _uniformData[offset] = x;
            _uniformData[offset + 1] = y;
            _uniformData[offset + 2] = z;
            _uniformData[offset + 3] = w;
        }

        public override void CleanUp()
        {
            _skyShader.cleanUp();
            _cloudShadowShader.cleanUp();
            _rippleShader.cleanUp();
            _dripSourceShader.cleanUp();
            _dripSurfaceShader.cleanUp();
            GL.DeleteBuffer(_uniformBuffer);
            GL.DeleteVertexArray(_emptyVao);
            GL.DeleteTexture(_dripSources);
            GL.DeleteTexture(_dripSurface);
            GL.DeleteFramebuffer(_dripSourceFramebuffer);
            GL.DeleteFramebuffer(_dripSurfaceFramebuffer);
            _dripDepth.shadowFrameBuffer.cleanUp();
            GL.DeleteTexture(_mapA);
            GL.DeleteTexture(_mapB);
            GL.DeleteTexture(_skyMap);
            GL.DeleteTexture(_ripples);
            GL.DeleteTexture(_cloudShadow);
            GL.DeleteFramebuffer(_skyFramebuffer);
            GL.DeleteFramebuffer(_rippleFramebuffer);
            GL.DeleteFramebuffer(_cloudShadowFramebuffer);
            _rainShadow.shadowFrameBuffer.cleanUp();
        }
    }
}
