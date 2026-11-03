using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

namespace Dino_Engine.Rendering.Renderers.PosGeometry
{
    public struct WaterBodyRenderData
    {
        public WaterBodyComponent body;
        public Vector3 centre;      // World centre; y is the surface level.
        public Vector2 axis;        // The body's local x axis on the xz plane.
        public Vector3 boundsMin;   // World bounds of its quad, for culling and the screen area it covers.
        public Vector3 boundsMax;
    }

    public struct WaterRenderCommand : IRenderCommand
    {
        public WaterSingleton settings;
        public WaterBodyRenderData[] bodies;    // Visible still water.
        public Vector4[] oceanQuads;            // Visible sea quads: xz corner, xz size.
        public Vector2 oceanSlab;               // Highest and lowest world y the sea surface reaches.
        public float breakerHeight;             // Metres, height of the waves breaking on an open coast.
        public Vector4 swellAmplitudesA;        // Metres, swell waves 0..3 and 4..7.
        public Vector4 swellAmplitudesB;
        public Vector2 whitecaps;               // Threshold in standard deviations, 1 / standard deviation of the swell.

        public TerrainChunkRenderData[] shoreChunks;  // The terrain the shore field analyses.
        public Vector2 shoreCorner;
        public float shoreSize;
        public long shoreSignature;             // Changes whenever the shore field has to be rebuilt.

        public Vector2 rippleCenter;
        public WaterContact[] rippleRates;      // Continuous contacts, metres per second.
        public WaterContact[] rippleImpulses;   // One-off splashes, metres.
        public float deltaTime;
    }

    /// <summary>
    /// The engine's water, in two parts each frame:
    /// 1. <see cref="SimulationPass"/>, at the start of the frame: rebuilds the shore field when the terrain changed
    ///    (<see cref="WaterShoreField"/>) and steps the ripples things leave in the water (<see cref="WaterRippleSimulation"/>).
    /// 2. The surface pass, after the deferred lighting: copies the part of the lit scene the water covers, then draws still
    ///    water (Water_Still.frag) and the sea (Water_Ocean.frag) over it, and writes the water's depth back to the G-buffer
    ///    so fog, depth of field and the sun see the surface.
    /// The light systems submit their lights here as they do to the volumetric fog, so water is lit by the same lights.
    /// </summary>
    public class WaterRenderer : CommandDrivenRenderer<WaterRenderCommand>
    {
        // World sizes of the texture tiles. WaterSystem moves the patterns in these units.
        public const float RippleTileA = 5.3f;
        public const float RippleTileB = 1.7f;
        public const float CausticTile = 2.7f;
        public const float FoamTile = 5f;
        public const float OpennessRadius = 20f;

        private const int ReflectionSteps = 8;
        private const float OceanEdgeFade = 0.1f;

        // Light limits; array sizes must match WaterCommon.glsl.
        private const int MaxDirectionalLights = 4;
        private const int MaxCascadeSlots = 5;
        private const int MaxPointLights = 8;
        private const int MaxSpotLights = 8;

        // Texture units of the surface shaders.
        private const int SceneColourUnit = 0;
        private const int SceneDepthUnit = 1;
        private const int WindMapUnit = 2;
        private const int RippleUnit = 3;
        private const int PatternUnit = 4;
        private const int RippleSimulationUnit = 5;
        private const int ShoreFieldUnit = 6;
        private const int ShoreOpennessUnit = 7;
        private const int GNormalUnit = 8;
        private const int CascadeUnitStart = 9;
        private const int TerrainHeightUnit = CascadeUnitStart + MaxCascadeSlots;

        private static readonly string[] DirectionalColourNames = UniformNames("directionalColours", MaxDirectionalLights);
        private static readonly string[] DirectionalDirectionNames = UniformNames("directionalDirections", MaxDirectionalLights);
        private static readonly string[] DirectionalCascadeStartNames = UniformNames("directionalCascadeStart", MaxDirectionalLights);
        private static readonly string[] DirectionalCascadeCountNames = UniformNames("directionalCascadeCount", MaxDirectionalLights);
        private static readonly string[] CascadeMatrixNames = UniformNames("cascadeMatrices", MaxCascadeSlots);
        private static readonly string[] PointPositionRadiusNames = UniformNames("pointPositionRadius", MaxPointLights);
        private static readonly string[] PointColourNames = UniformNames("pointColour", MaxPointLights);
        private static readonly string[] PointAttenuationNames = UniformNames("pointAttenuation", MaxPointLights);
        private static readonly string[] SpotPositionRadiusNames = UniformNames("spotPositionRadius", MaxSpotLights);
        private static readonly string[] SpotColourNames = UniformNames("spotColour", MaxSpotLights);
        private static readonly string[] SpotDirectionCutoffNames = UniformNames("spotDirectionCutoff", MaxSpotLights);
        private static readonly string[] SpotAttenuationSoftnessNames = UniformNames("spotAttenuationSoftness", MaxSpotLights);

        private readonly ShaderProgram _stillShader = new ShaderProgram("Water/Water_Still.vert", "Water/Water_Still.frag");
        private readonly ShaderProgram _oceanShader = new ShaderProgram("Water/Water_Ocean.vert", "Water/Water_Ocean.frag");
        private readonly ShaderProgram _captureStillShader = new ShaderProgram("Water/Water_Still.vert", "Water/Water_Capture_Still.frag");
        private readonly ShaderProgram _captureOceanShader = new ShaderProgram("Water/Water_Ocean.vert", "Water/Water_Capture_Ocean.frag");

        private readonly WaterShoreField _shoreField = new WaterShoreField();
        private readonly WaterRippleSimulation _rippleSimulation = new WaterRippleSimulation();

        private readonly int _rippleTexture;
        private readonly int _patternTexture;
        private readonly float _causticMean;
        private readonly FrameBuffer _sceneCopy;

        private readonly int _quadBuffer;
        private readonly int _indexBuffer;
        private readonly int _instanceBuffer;
        private readonly int _stillVao;
        private readonly int _oceanVao;

        // This frame's water, taken from the newest command by SimulationPass and drawn by the surface pass and fog capture.
        private WaterRenderCommand? _frame;
        private readonly List<WaterContact> _impulses = new();

        private readonly List<DirectionallightRenderCommand> _directionalLights = new();
        private readonly List<PointlightRenderCommand> _pointLights = new();
        private readonly List<SpotlightRenderCommand> _spotLights = new();
        private readonly List<(float distance, int index)> _lightCandidates = new();

        // The lights this frame, as uploaded to both surface shaders.
        private int _directionalCount;
        private readonly Vector3[] _directionalColours = new Vector3[MaxDirectionalLights];
        private readonly Vector3[] _directionalDirections = new Vector3[MaxDirectionalLights];
        private readonly int[] _directionalCascadeStart = new int[MaxDirectionalLights];
        private readonly int[] _directionalCascadeCount = new int[MaxDirectionalLights];
        private readonly Matrix4[] _cascadeMatrices = new Matrix4[MaxCascadeSlots];
        private readonly int[] _cascadeTextures = new int[MaxCascadeSlots];
        private int _cascadeCount;
        private int _pointCount;
        private readonly Vector4[] _pointPositionRadius = new Vector4[MaxPointLights];
        private readonly Vector4[] _pointColour = new Vector4[MaxPointLights];
        private readonly Vector4[] _pointAttenuation = new Vector4[MaxPointLights];
        private int _spotCount;
        private readonly Vector4[] _spotPositionRadius = new Vector4[MaxSpotLights];
        private readonly Vector4[] _spotColour = new Vector4[MaxSpotLights];
        private readonly Vector4[] _spotDirectionCutoff = new Vector4[MaxSpotLights];
        private readonly Vector4[] _spotAttenuationSoftness = new Vector4[MaxSpotLights];

        public WaterRenderer() : base("Water")
        {
            foreach (ShaderProgram shader in new[] { _stillShader, _oceanShader })
            {
                shader.bind();
                shader.loadUniformInt("sceneColour", SceneColourUnit);
                shader.loadUniformInt("sceneDepth", SceneDepthUnit);
                shader.loadUniformInt("windMap", WindMapUnit);
                shader.loadUniformInt("rippleTexture", RippleUnit);
                shader.loadUniformInt("patternTexture", PatternUnit);
                shader.loadUniformInt("rippleSimulation", RippleSimulationUnit);
                shader.loadUniformInt("terrainHeights", TerrainHeightUnit);
                for (int i = 0; i < MaxCascadeSlots; i++) shader.loadUniformInt("cascadeShadowMaps[" + i + "]", CascadeUnitStart + i);
            }
            _oceanShader.loadUniformInt("shoreField", ShoreFieldUnit);
            _oceanShader.loadUniformInt("shoreOpenness", ShoreOpennessUnit);
            _oceanShader.loadUniformInt("gNormal", GNormalUnit);
            _oceanShader.unBind();

            _rippleTexture = CreateRippleTexture();
            _patternTexture = CreatePatternTexture(out _causticMean);

            FrameBufferSettings copySettings = new FrameBufferSettings(Engine.Resolution);
            copySettings.drawBuffers.Add(new DrawBufferSettings(FramebufferAttachment.ColorAttachment0)
            {
                formatInternal = PixelInternalFormat.Rgba16f,
                pixelType = PixelType.Float,
                wrapMode = TextureWrapMode.ClampToEdge,
            });
            _sceneCopy = new FrameBuffer(copySettings);
            _sceneCopy.unbind();

            // A unit quad (corners 0..1); the sea draws one instance per quad in the instance buffer.
            float[] corners = { 0f, 0f, 1f, 0f, 1f, 1f, 0f, 1f };
            uint[] indices = { 0, 1, 2, 0, 2, 3 };
            _quadBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _quadBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, corners.Length * sizeof(float), corners, BufferUsageHint.StaticDraw);
            _instanceBuffer = GL.GenBuffer();

            _stillVao = GL.GenVertexArray();
            GL.BindVertexArray(_stillVao);
            _indexBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _quadBuffer);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);

            _oceanVao = GL.GenVertexArray();
            GL.BindVertexArray(_oceanVao);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _quadBuffer);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, Vector4.SizeInBytes, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, Vector4.SizeInBytes, 0);
            GL.VertexAttribDivisor(1, 1);
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        private static string[] UniformNames(string array, int length)
        {
            string[] names = new string[length];
            for (int i = 0; i < length; i++) names[i] = array + "[" + i + "]";
            return names;
        }

        private static int CreateRippleTexture()
        {
            float[] data = WaterTextureBaker.BakeRipples();
            int texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture3D, texture);
            GL.TexImage3D(TextureTarget.Texture3D, 0, PixelInternalFormat.Rgba16f, WaterTextureBaker.RippleResolution, WaterTextureBaker.RippleResolution,
                WaterTextureBaker.RippleFrames, 0, PixelFormat.Rgba, PixelType.Float, data);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapR, (int)TextureWrapMode.Repeat);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture3D);
            GL.BindTexture(TextureTarget.Texture3D, 0);
            return texture;
        }

        private static int CreatePatternTexture(out float causticMean)
        {
            byte[] data = WaterTextureBaker.BakePattern(out causticMean);
            int texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, WaterTextureBaker.PatternResolution, WaterTextureBaker.PatternResolution,
                0, PixelFormat.Rgba, PixelType.UnsignedByte, data);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return texture;
        }

        public void SubmitDirectionalLight(DirectionallightRenderCommand light) => _directionalLights.Add(light);
        public void SubmitPointLight(PointlightRenderCommand light) => _pointLights.Add(light);
        public void SubmitSpotLight(SpotlightRenderCommand light) => _spotLights.Add(light);

        public override void Update()
        {
            // Runs before the systems submit this frame's lights, and also when the previous frame never reached the water.
            _directionalLights.Clear();
            _pointLights.Clear();
            _spotLights.Clear();
        }

        // ---------------------------------------------------------------- simulation pass

        /// <summary>Start of the frame, before any geometry: shore field and ripples.</summary>
        public void SimulationPass(RenderEngine renderEngine)
        {
            _frame = null;
            if (commands.Count == 0) return;

            // Only the newest command is current; older ones pile up when frames skip the render (e.g. the F8 debug view).
            // Their splashes still count.
            for (int i = 0; i < commands.Count - 1; i++)
            {
                if (commands[i].rippleImpulses != null) _impulses.AddRange(commands[i].rippleImpulses);
            }
            WaterRenderCommand command = commands[commands.Count - 1];
            commands.Clear();
            if (command.rippleImpulses != null) _impulses.AddRange(command.rippleImpulses);
            _frame = command;

            Engine.PerformanceMonitor.startGPUTask("Water Simulation");
            Engine.PerformanceMonitor.startCPUTask("Water Simulation RENDER");

            if (command.shoreSignature != _shoreField.Signature)
            {
                if (command.shoreChunks == null || command.shoreChunks.Length == 0) _shoreField.Invalidate();
                else _shoreField.Build(command.shoreChunks, command.shoreCorner, command.shoreSize, command.settings.SeaLevel, command.shoreSignature, renderEngine);
            }

            WaterSingleton settings = command.settings;
            _rippleSimulation.Step(command.rippleCenter, command.rippleRates ?? Array.Empty<WaterContact>(), _impulses, command.deltaTime,
                settings.RippleSpeed, settings.RippleDecay, renderEngine);
            _impulses.Clear();

            Engine.PerformanceMonitor.finishCPUTask("Water Simulation RENDER");
            Engine.PerformanceMonitor.finishGPUTask("Water Simulation");
        }

        // ---------------------------------------------------------------- surface pass

        internal override void Prepare(RenderEngine renderEngine)
        {
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        public override void PerformCommand(WaterRenderCommand command, RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {
            if (_frame == null) return;
            WaterRenderCommand command = _frame.Value;
            WaterBodyRenderData[] bodies = command.bodies ?? Array.Empty<WaterBodyRenderData>();
            Vector4[] oceanQuads = command.oceanQuads ?? Array.Empty<Vector4>();
            if (bodies.Length == 0 && oceanQuads.Length == 0) return;

            Matrix4 viewProjection = renderEngine.context.viewMatrix * renderEngine.context.projectionMatrix;
            bool stillVisible = TryGetScreenRect(bodies, Array.Empty<Vector4>(), command.oceanSlab, viewProjection, out Box2i stillRect);
            bool oceanVisible = TryGetScreenRect(Array.Empty<WaterBodyRenderData>(), oceanQuads, command.oceanSlab, viewProjection, out Box2i oceanRect);
            if (!stillVisible && !oceanVisible) return;
            Box2i waterRect = stillVisible && oceanVisible ? Union(stillRect, oceanRect) : stillVisible ? stillRect : oceanRect;

            // The part of the scene to copy: refraction looks a little beside the water; reflections on still water can
            // reach much further across the screen.
            Box2i rect = Expand(oceanVisible ? oceanRect : stillRect, 0.04f);
            if (stillVisible) rect = Union(rect, Expand(stillRect, command.settings.ScreenSpaceReflections ? 0.35f : 0.04f));

            DualBuffer buffer = renderEngine.lastUsedBuffer;
            FrameBuffer target = buffer.GetLastFrameBuffer();
            CopyScene(target, rect);

            bool cullFace = GL.IsEnabled(EnableCap.CullFace);
            target.bind();
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.CullFace);

            BindSharedTextures(renderEngine);
            PrepareLights(renderEngine);
            Vector2 resolution = Engine.Resolution;
            Vector4 sceneRect = new Vector4(
                (rect.Min.X + 0.5f) / resolution.X, (rect.Min.Y + 0.5f) / resolution.Y,
                (rect.Max.X - 0.5f) / resolution.X, (rect.Max.Y - 0.5f) / resolution.Y);

            if (stillVisible) DrawStill(command, bodies, viewProjection, sceneRect);
            if (oceanVisible) DrawOcean(command, oceanQuads, viewProjection, sceneRect, renderEngine);

            GL.BindVertexArray(0);
            GL.UseProgram(0);
            if (cullFace) GL.Enable(EnableCap.CullFace);

            // The water's depth to the G-buffer (fog, depth of field and the sun read it) and to the other half of the
            // dual buffer, which the post-processing passes render into next. Depth only changed where the water is.
            BlitDepth(target, renderEngine.GBuffer, waterRect);
            BlitDepth(renderEngine.GBuffer, buffer.GetNextFrameBuffer(), waterRect);
            GL.DepthMask(false);
            target.unbind();
        }

        private static void BlitDepth(FrameBuffer source, FrameBuffer destination, Box2i rect)
        {
            source.bindRead();
            destination.bindDraw();
            GL.BlitFramebuffer(rect.Min.X, rect.Min.Y, rect.Max.X, rect.Max.Y, rect.Min.X, rect.Min.Y, rect.Max.X, rect.Max.Y,
                ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
        }

        /// <summary>Grows a pixel rectangle by a fraction of the screen height on every side, within the screen.</summary>
        private static Box2i Expand(Box2i rect, float fraction)
        {
            Vector2i resolution = Engine.Resolution;
            int margin = (int)MathF.Ceiling(fraction * resolution.Y);
            return new Box2i(Math.Max(rect.Min.X - margin, 0), Math.Max(rect.Min.Y - margin, 0),
                Math.Min(rect.Max.X + margin, resolution.X), Math.Min(rect.Max.Y + margin, resolution.Y));
        }

        private static Box2i Union(Box2i a, Box2i b)
        {
            return new Box2i(Math.Min(a.Min.X, b.Min.X), Math.Min(a.Min.Y, b.Min.Y), Math.Max(a.Max.X, b.Max.X), Math.Max(a.Max.Y, b.Max.Y));
        }

        /// <summary>
        /// Pixel rectangle these water surfaces cover on screen: the bounds of their quads (the whole screen if any reaches
        /// behind the camera). False when there are none or they cover no pixels.
        /// </summary>
        private static bool TryGetScreenRect(WaterBodyRenderData[] bodies, Vector4[] oceanQuads, Vector2 oceanSlab, Matrix4 viewProjection, out Box2i rect)
        {
            Vector2i resolution = Engine.Resolution;
            rect = new Box2i(0, 0, resolution.X, resolution.Y);
            if (resolution.X <= 0 || resolution.Y <= 0 || (bodies.Length == 0 && oceanQuads.Length == 0)) return false;

            Vector2 min = new Vector2(float.MaxValue);
            Vector2 max = new Vector2(float.MinValue);
            foreach (WaterBodyRenderData body in bodies)
            {
                if (!ExtendScreenBounds(body.boundsMin, body.boundsMax, viewProjection, ref min, ref max)) return true;
            }
            foreach (Vector4 quad in oceanQuads)
            {
                Vector3 quadMin = new Vector3(quad.X, oceanSlab.Y, quad.Y);
                Vector3 quadMax = new Vector3(quad.X + quad.Z, oceanSlab.X, quad.Y + quad.W);
                if (!ExtendScreenBounds(quadMin, quadMax, viewProjection, ref min, ref max)) return true;
            }

            // One pixel extra for rasterisation rounding.
            int x0 = Math.Clamp((int)MathF.Floor((min.X * 0.5f + 0.5f) * resolution.X) - 1, 0, resolution.X);
            int y0 = Math.Clamp((int)MathF.Floor((min.Y * 0.5f + 0.5f) * resolution.Y) - 1, 0, resolution.Y);
            int x1 = Math.Clamp((int)MathF.Ceiling((max.X * 0.5f + 0.5f) * resolution.X) + 1, 0, resolution.X);
            int y1 = Math.Clamp((int)MathF.Ceiling((max.Y * 0.5f + 0.5f) * resolution.Y) + 1, 0, resolution.Y);
            if (x1 <= x0 || y1 <= y0) return false;
            rect = new Box2i(x0, y0, x1, y1);
            return true;
        }

        /// <summary>Grows the NDC bounds by a box's corners. False if the box reaches behind the camera (use the whole screen).</summary>
        private static bool ExtendScreenBounds(Vector3 boxMin, Vector3 boxMax, Matrix4 viewProjection, ref Vector2 min, ref Vector2 max)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? boxMin.X : boxMax.X, (i & 2) == 0 ? boxMin.Y : boxMax.Y, (i & 4) == 0 ? boxMin.Z : boxMax.Z);
                Vector4 clip = new Vector4(corner, 1f) * viewProjection;
                if (clip.W < 0.05f) return false;
                Vector2 ndc = clip.Xy / clip.W;
                min = Vector2.ComponentMin(min, ndc);
                max = Vector2.ComponentMax(max, ndc);
            }
            return true;
        }

        private void CopyScene(FrameBuffer source, Box2i rect)
        {
            source.bindRead();
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            _sceneCopy.bindDraw();
            GL.BlitFramebuffer(rect.Min.X, rect.Min.Y, rect.Max.X, rect.Max.Y, rect.Min.X, rect.Min.Y, rect.Max.X, rect.Max.Y,
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        }

        private void BindSharedTextures(RenderEngine renderEngine)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + SceneColourUnit);
            GL.BindTexture(TextureTarget.Texture2D, _sceneCopy.GetAttachment(0));
            GL.ActiveTexture(TextureUnit.Texture0 + SceneDepthUnit);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine.GBuffer.getDepthAttachment());
            GL.ActiveTexture(TextureUnit.Texture0 + WindMapUnit);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine._windRenderer.WindMapTexture);
            GL.ActiveTexture(TextureUnit.Texture0 + RippleUnit);
            GL.BindTexture(TextureTarget.Texture3D, _rippleTexture);
            GL.ActiveTexture(TextureUnit.Texture0 + PatternUnit);
            GL.BindTexture(TextureTarget.Texture2D, _patternTexture);
            GL.ActiveTexture(TextureUnit.Texture0 + RippleSimulationUnit);
            GL.BindTexture(TextureTarget.Texture2D, _rippleSimulation.Texture);
            GL.ActiveTexture(TextureUnit.Texture0 + TerrainHeightUnit);
            GL.BindTexture(TextureTarget.Texture2D, _shoreField.HeightTexture);
        }

        private void UploadShared(ShaderProgram shader, WaterRenderCommand command, Matrix4 viewProjection, Vector4 sceneRect)
        {
            WaterSingleton settings = command.settings;
            shader.loadUniformMatrix4f("viewProjection", viewProjection);
            shader.loadUniformVector4f("sceneRect", sceneRect);
            shader.loadUniformFloat("skyStrength", SkyRenderer.SkyStrength);
            shader.loadUniformVector4f("rippleLayerA", new Vector4(settings.RippleOffsetA.X, settings.RippleOffsetA.Y, settings.RippleTimeA, 1f / RippleTileA));
            shader.loadUniformVector4f("rippleLayerB", new Vector4(settings.RippleOffsetB.X, settings.RippleOffsetB.Y, settings.RippleTimeB, 1f / RippleTileB));
            shader.loadUniformFloat("rippleScale", MathF.Max(settings.RippleScale, 0f));
            shader.loadUniformVector4f("causticLayers", new Vector4(settings.CausticOffsetA.X, settings.CausticOffsetA.Y, settings.CausticOffsetB.X, settings.CausticOffsetB.Y));
            shader.loadUniformVector2f("causticParameters", new Vector2(1f / CausticTile, MathF.Max(settings.CausticStrength, 0f)));
            shader.loadUniformFloat("causticMean", _causticMean);
            shader.loadUniformFloat("refractionStrength", MathF.Max(settings.RefractionStrength, 0f));
            shader.loadUniformInt("reflectionSteps", settings.ScreenSpaceReflections ? ReflectionSteps : 0);
            shader.loadUniformVector4f("rippleSimRegion", _rippleSimulation.Region);
            shader.loadUniformFloat("rippleSimStrength", MathF.Max(settings.RippleNormalStrength, 0f));
            shader.loadUniformVector3f("foamPattern", new Vector3(settings.FoamOffset.X, settings.FoamOffset.Y, 1f / FoamTile));
            shader.loadUniformFloat("bedDarkening", Math.Clamp(settings.WetSandDarkening, 0f, 0.95f));
            bool terrain = _shoreField.Valid;
            shader.loadUniformVector4f("terrainRegion", terrain ? new Vector4(_shoreField.Corner.X, _shoreField.Corner.Y, _shoreField.Size, _shoreField.Size) : Vector4.Zero);
            // The capture draws the bare height map; the visible ground's mesh sits SurfaceOffset above it.
            shader.loadUniformVector2f("terrainHeightRange", new Vector2(_shoreField.CaptureTop + TerrainRenderer.SurfaceOffset, _shoreField.CaptureRange));
            UploadLights(shader);
        }

        private static void UploadOptics(ShaderProgram shader, WaterOptics optics, float windExposure, float edgeFade)
        {
            shader.loadUniformVector3f("absorption", Vector3.ComponentMax(optics.Absorption, Vector3.Zero));
            shader.loadUniformVector3f("scattering", Vector3.ComponentMax(optics.Scattering, Vector3.Zero));
            shader.loadUniformFloat("windExposure", Math.Clamp(windExposure, 0f, 1f));
            shader.loadUniformFloat("edgeFade", MathF.Max(edgeFade, 0.001f));
        }

        private void DrawStill(WaterRenderCommand command, WaterBodyRenderData[] bodies, Matrix4 viewProjection, Vector4 sceneRect)
        {
            _stillShader.bind();
            UploadShared(_stillShader, command, viewProjection, sceneRect);
            GL.BindVertexArray(_stillVao);
            foreach (WaterBodyRenderData data in bodies)
            {
                WaterBodyComponent body = data.body;
                LoadBodyShape(_stillShader, data);
                _stillShader.loadUniformFloat("bodyEdgeFoam", Math.Clamp(body.EdgeFoam * command.settings.Foam, 0f, 1f));
                UploadOptics(_stillShader, body.Optics, body.WindExposure, body.EdgeFade);
                GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
            }
        }

        private static void LoadBodyShape(ShaderProgram shader, WaterBodyRenderData data)
        {
            WaterBodyComponent body = data.body;
            shader.loadUniformVector3f("bodyCentre", data.centre);
            shader.loadUniformVector2f("bodyAxis", data.axis);
            shader.loadUniformVector2f("bodyQuadExtents", WaterShapes.QuadHalfExtents(body));
            shader.loadUniformInt("bodyShape", (int)body.Shape);
            shader.loadUniformVector2f("bodyHalfExtents", body.HalfExtents);
            shader.loadUniformFloat("bodyEdgeSoftness", MathF.Max(body.EdgeSoftness, 0f));
            shader.loadUniformFloat("bodyEdgeNoise", MathF.Max(body.EdgeNoise, 0f));
            shader.loadUniformFloat("bodyInvNoiseScale", 1f / MathF.Max(body.EdgeNoiseScale, 0.05f));
        }

        private void DrawOcean(WaterRenderCommand command, Vector4[] quads, Matrix4 viewProjection, Vector4 sceneRect, RenderEngine renderEngine)
        {
            WaterSingleton settings = command.settings;
            _oceanShader.bind();
            UploadShared(_oceanShader, command, viewProjection, sceneRect);
            // A camera looking down sees the sea reflect a few percent of the light at most: its reflections of the shore are
            // lost against the sky, so it reflects only the sky.
            _oceanShader.loadUniformInt("reflectionSteps", 0);
            UploadOptics(_oceanShader, settings.OceanOptics, 1f, OceanEdgeFade);
            _oceanShader.loadUniformFloat("surfaceHeight", command.oceanSlab.X);
            _oceanShader.loadUniformFloat("seaLevel", settings.SeaLevel);
            _oceanShader.loadUniformVector2f("oceanSlab", command.oceanSlab);

            bool shore = _shoreField.Valid;
            _oceanShader.loadUniformVector4f("shoreRegion", shore ? new Vector4(_shoreField.Corner.X, _shoreField.Corner.Y, _shoreField.Size, _shoreField.Size) : Vector4.Zero);
            _oceanShader.loadUniformFloat("opennessLod", _shoreField.OpennessLod(OpennessRadius));
            float period = MathF.Max(settings.ShoreWavePeriod, 0.5f);
            _oceanShader.loadUniformVector4f("shoreWave", new Vector4(command.breakerHeight, 1f / MathF.Max(settings.ShoreWaveLength, 0.5f),
                settings.ShorePhase, MathF.Max(settings.RunUp, 0f)));
            _oceanShader.loadUniformVector4f("shoreShape", new Vector4(MathF.Max(settings.ShoreWaveReach, 1f), Math.Clamp(settings.SetVariation, 0f, 1f),
                WaterWaves.DeepWaterWavelength(period), period));
            _oceanShader.loadUniformVector4f("swellAmplitudes[0]", command.swellAmplitudesA);
            _oceanShader.loadUniformVector4f("swellAmplitudes[1]", command.swellAmplitudesB);
            _oceanShader.loadUniformVector4f("swellPhases[0]", settings.SwellPhasesA);
            _oceanShader.loadUniformVector4f("swellPhases[1]", settings.SwellPhasesB);
            _oceanShader.loadUniformVector2f("whitecaps", command.whitecaps);
            _oceanShader.loadUniformVector3f("shoreFoam", new Vector3(MathF.Max(settings.Foam, 0f), Math.Clamp(settings.WetSandDarkening, 0f, 0.95f),
                MathF.Max(settings.DryingTime, 0.1f)));

            GL.ActiveTexture(TextureUnit.Texture0 + ShoreFieldUnit);
            GL.BindTexture(TextureTarget.Texture2D, _shoreField.FieldTexture);
            GL.ActiveTexture(TextureUnit.Texture0 + ShoreOpennessUnit);
            GL.BindTexture(TextureTarget.Texture2D, _shoreField.OpennessTexture);
            GL.ActiveTexture(TextureUnit.Texture0 + GNormalUnit);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine.GBuffer.GetAttachment(1));

            DrawOceanQuads(quads);
        }

        private void DrawOceanQuads(Vector4[] quads)
        {
            GL.BindVertexArray(_oceanVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, quads.Length * Vector4.SizeInBytes, quads, BufferUsageHint.DynamicDraw);
            GL.DrawElementsInstanced(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero, quads.Length);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        // ---------------------------------------------------------------- lights

        private void PrepareLights(RenderEngine renderEngine)
        {
            // Directional lights, with the cascades they cast.
            _directionalCount = 0;
            _cascadeCount = 0;
            foreach (DirectionallightRenderCommand light in _directionalLights)
            {
                if (_directionalCount == MaxDirectionalLights) break;
                if (light.colour == Vector3.Zero || !(light.direction.LengthSquared > 0f)) continue;
                Shadow[] cascades = light.cascades ?? Array.Empty<Shadow>();
                int cascadeCount = Math.Min(cascades.Length, MaxCascadeSlots - _cascadeCount);
                _directionalColours[_directionalCount] = light.colour;
                _directionalDirections[_directionalCount] = -light.direction.Normalized();
                _directionalCascadeStart[_directionalCount] = _cascadeCount;
                _directionalCascadeCount[_directionalCount] = cascadeCount;
                for (int c = 0; c < cascadeCount; c++)
                {
                    Shadow cascade = cascades[c];
                    _cascadeTextures[_cascadeCount] = cascade.shadowFrameBuffer.getDepthAttachment();
                    _cascadeMatrices[_cascadeCount] = cascade.lightViewMatrix * cascade.shadowProjectionMatrix;
                    _cascadeCount++;
                }
                _directionalCount++;
            }
            for (int c = 0; c < _cascadeCount; c++)
            {
                GL.ActiveTexture(TextureUnit.Texture0 + CascadeUnitStart + c);
                GL.BindTexture(TextureTarget.Texture2D, _cascadeTextures[c]);
            }

            Frustum frustum = new Frustum(renderEngine.context.viewMatrix * renderEngine.context.projectionMatrix);
            Vector3 cameraPosition = renderEngine.context.viewPos;

            _lightCandidates.Clear();
            for (int i = 0; i < _pointLights.Count; i++)
            {
                PointlightRenderCommand light = _pointLights[i];
                if (IsCandidate(light.positionWorld, light.attenuationRadius, light.colour, frustum, cameraPosition, out float distance)) _lightCandidates.Add((distance, i));
            }
            _lightCandidates.Sort((a, b) => a.distance.CompareTo(b.distance));
            _pointCount = Math.Min(_lightCandidates.Count, MaxPointLights);
            for (int i = 0; i < _pointCount; i++)
            {
                PointlightRenderCommand light = _pointLights[_lightCandidates[i].index];
                _pointPositionRadius[i] = new Vector4(light.positionWorld, light.attenuationRadius);
                _pointColour[i] = new Vector4(light.colour, 0f);
                _pointAttenuation[i] = new Vector4(light.attenuation, 0f);
            }

            _lightCandidates.Clear();
            for (int i = 0; i < _spotLights.Count; i++)
            {
                SpotlightRenderCommand light = _spotLights[i];
                if (SpotLightRenderer.WorldDirection(light).LengthSquared > 0f
                    && IsCandidate(light.positionWorld, light.attenuationRadius, light.colour, frustum, cameraPosition, out float distance))
                {
                    _lightCandidates.Add((distance, i));
                }
            }
            _lightCandidates.Sort((a, b) => a.distance.CompareTo(b.distance));
            _spotCount = Math.Min(_lightCandidates.Count, MaxSpotLights);
            for (int i = 0; i < _spotCount; i++)
            {
                SpotlightRenderCommand light = _spotLights[_lightCandidates[i].index];
                _spotPositionRadius[i] = new Vector4(light.positionWorld, light.attenuationRadius);
                _spotColour[i] = new Vector4(light.colour, 0f);
                _spotDirectionCutoff[i] = new Vector4(SpotLightRenderer.WorldDirection(light).Normalized(), light.cutoffCosine);
                _spotAttenuationSoftness[i] = new Vector4(light.attenuation, light.softness);
            }
        }

        private void UploadLights(ShaderProgram shader)
        {
            shader.loadUniformInt("directionalLightCount", _directionalCount);
            for (int i = 0; i < _directionalCount; i++)
            {
                shader.loadUniformVector3f(DirectionalColourNames[i], _directionalColours[i]);
                shader.loadUniformVector3f(DirectionalDirectionNames[i], _directionalDirections[i]);
                shader.loadUniformInt(DirectionalCascadeStartNames[i], _directionalCascadeStart[i]);
                shader.loadUniformInt(DirectionalCascadeCountNames[i], _directionalCascadeCount[i]);
            }
            for (int c = 0; c < _cascadeCount; c++) shader.loadUniformMatrix4f(CascadeMatrixNames[c], _cascadeMatrices[c]);

            shader.loadUniformInt("pointLightCount", _pointCount);
            for (int i = 0; i < _pointCount; i++)
            {
                shader.loadUniformVector4f(PointPositionRadiusNames[i], _pointPositionRadius[i]);
                shader.loadUniformVector4f(PointColourNames[i], _pointColour[i]);
                shader.loadUniformVector4f(PointAttenuationNames[i], _pointAttenuation[i]);
            }

            shader.loadUniformInt("spotLightCount", _spotCount);
            for (int i = 0; i < _spotCount; i++)
            {
                shader.loadUniformVector4f(SpotPositionRadiusNames[i], _spotPositionRadius[i]);
                shader.loadUniformVector4f(SpotColourNames[i], _spotColour[i]);
                shader.loadUniformVector4f(SpotDirectionCutoffNames[i], _spotDirectionCutoff[i]);
                shader.loadUniformVector4f(SpotAttenuationSoftnessNames[i], _spotAttenuationSoftness[i]);
            }
        }

        /// <summary>
        /// Whether a light with this sphere of influence can light visible water. Candidates are ranked by how far the camera
        /// is from the sphere, nearest first, so the limited slots go to the lights that matter most.
        /// </summary>
        private static bool IsCandidate(Vector3 position, float radius, Vector3 colour, Frustum frustum, Vector3 cameraPosition, out float distance)
        {
            distance = 0f;
            // No reach, no colour, or an attenuation without a real radius (NaN): nothing to add.
            if (!(radius > 0f) || colour == Vector3.Zero) return false;
            if (frustum.IntersectsAABB(new AABB(position - new Vector3(radius), position + new Vector3(radius))) == IntersectionResult.Outside) return false;
            distance = MathF.Max((position - cameraPosition).Length - radius, 0f);
            return true;
        }

        // ---------------------------------------------------------------- height capture

        /// <summary>
        /// Draws this frame's calm water surfaces into the currently bound depth target, for top-down height captures
        /// (VolumetricFogRenderer, so fog lies on water rather than on the bottom).
        /// </summary>
        public void RenderHeightCapture(Matrix4 projectionView)
        {
            if (_frame == null) return;
            WaterRenderCommand command = _frame.Value;
            WaterBodyRenderData[] bodies = command.bodies ?? Array.Empty<WaterBodyRenderData>();
            Vector4[] quads = command.oceanQuads ?? Array.Empty<Vector4>();
            if (bodies.Length == 0 && quads.Length == 0) return;

            bool cullFace = GL.IsEnabled(EnableCap.CullFace);
            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);

            if (bodies.Length > 0)
            {
                _captureStillShader.bind();
                _captureStillShader.loadUniformMatrix4f("viewProjection", projectionView);
                GL.BindVertexArray(_stillVao);
                foreach (WaterBodyRenderData data in bodies)
                {
                    LoadBodyShape(_captureStillShader, data);
                    GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
                }
            }
            if (quads.Length > 0)
            {
                _captureOceanShader.bind();
                _captureOceanShader.loadUniformMatrix4f("viewProjection", projectionView);
                _captureOceanShader.loadUniformFloat("surfaceHeight", command.settings.SeaLevel);
                DrawOceanQuads(quads);
            }

            GL.BindVertexArray(0);
            GL.UseProgram(0);
            if (cullFace) GL.Enable(EnableCap.CullFace);
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
            _sceneCopy.resize(eventArgs.Size);
        }

        public override void CleanUp()
        {
            _stillShader.cleanUp();
            _oceanShader.cleanUp();
            _captureStillShader.cleanUp();
            _captureOceanShader.cleanUp();
            _shoreField.CleanUp();
            _rippleSimulation.CleanUp();
            GL.DeleteTexture(_rippleTexture);
            GL.DeleteTexture(_patternTexture);
            _sceneCopy.cleanUp();
            GL.DeleteVertexArray(_stillVao);
            GL.DeleteVertexArray(_oceanVao);
            GL.DeleteBuffer(_quadBuffer);
            GL.DeleteBuffer(_indexBuffer);
            GL.DeleteBuffer(_instanceBuffer);
        }
    }
}
