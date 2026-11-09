using Dino_Engine.Core;
using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

namespace Dino_Engine.Rendering.Renderers.Lighting.GlobalIllumination
{
    /// <summary>
    /// Light that bounces: the sky, the sun off the ground, a fire off the walls, as it really falls, in place of the flat sky and ground the
    /// ambient pass had. Read GI.md. The world is kept as voxels around the camera (built from the world's own data, lit once a frame), and
    /// light is gathered by radiance cascades on probes that stand in the world near what the camera sees, so moving the camera never makes
    /// them swim. Everything here runs on the GPU and the CPU never waits on it. The ambient pass reads <see cref="BindGathered"/>.
    /// </summary>
    public partial class GlobalIlluminationRenderer : BaseRenderer
    {
        // ---- The layout, which the shaders in Shaders/GI share: change them together.

        public const int Levels = 4;
        public const int Cascades = 5;
        public static readonly Vector3i Dim = new Vector3i(192, 64, 128);
        public static readonly Vector3i Bricks = Dim / 4;
        public const int PoolCapacity = 262144;
        public const int PoolStride = 8;
        public const float FinestVoxel = 0.25f;
        /// <summary>Voxels a window moves at a time: whole bricks, and few enough moves that each is worth the work.</summary>
        public const int ScrollStep = 16;
        public const float FinestSpacing = 0.5f;
        public const float FirstRayStart = 0.1f;
        public static readonly Vector2i[] ColumnDims = { new(512, 256), new(256, 128), new(128, 64), new(64, 32), new(32, 16) };
        public static readonly int[] ColumnStart = { 0, 131072, 163840, 172032, 174080, 174592 };
        public static readonly int[] ProbeCapacity = { 196608, 49152, 12288, 3072, 768 };
        public static readonly int[] ProbeBase = { 0, 196608, 245760, 258048, 261120, 261888 };
        public static readonly int[] DirectionCount = { 12, 48, 192, 768, 3072 };
        public static readonly int[] DirectionBase = { 0, 12, 60, 252, 1020 };
        public const int MaxLights = 64;
        public const int VoxelizeViewport = 192;
        public const int VoxelizeSamples = 8;

        /// <summary>Metres ahead of the hero (along +z, where the camera looks) that the windows and the probes' region centre on.</summary>
        private const float LevelAhead = 4f;
        private const float RegionAhead = 12f;
        /// <summary>How much of a window's height is at least below the hero (TargetOrigin): most of what stands, stands up.</summary>
        private const float WindowBelow = 0.3f;
        /// <summary>Seconds over which GI fades in once it's ready, so it never appears in a single frame.</summary>
        private const float FadeIn = 0.6f;

        // Counts buffer layout (GI_Voxel_Index.comp, GI_Probe.glsl).
        private const int CountProbeDispatch = 36;
        private const int CountFreshDispatch = 100;
        private const int CountsLength = 128;

        // Texture units the GI's own passes use, out of the way of everyone else's.
        private const int UnitBricks = 20, UnitIndex = 21, UnitHeight = 22, UnitBrickLight = 23, UnitSky = 24, UnitCloud = 25;
        private const int UnitDepth = 26, UnitNormal = 27, UnitColour = 28, UnitChunkLayers = 29;
        private const int UnitHistory = UnitColour;     // Last frame's gather, which only the gather reads, and it reads no colours.

        private readonly GlobalIlluminationSettings _settings;
        private bool _resources;
        private bool _wasEnabled;
        private int _ubo;
        private readonly int[] _uboData = new int[100];
        private int _counts;
        private int _lights;
        private int _skyLight;
        private int _directions;
        private readonly float[] _lightData = new float[MaxLights * 12];
        private int _lightCount;
        private int _frame;
        private float _presence;
        private ShaderProgram _skyShader = null!;
        private ShaderProgram _debugShader = null!;

        // What the sky pass is told by SkySYstem each frame.
        private Vector3 _skyFill;
        private float _skyScale;
        private Vector3 _groundRadiance;

        public GlobalIlluminationRenderer(GlobalIlluminationSettings settings) : base("Global Illumination", trackPerformance: false)
        {
            _settings = settings;
        }

        /// <summary>Whether the ambient pass lights with GI this frame, and how much: it fades in when it's ready.</summary>
        public bool Active { get; private set; }
        public float Presence => _presence;

        /// <summary>Probes each cascade had last time they were counted, for the F3 window. Nothing waits for these.</summary>
        public int[] ProbesSeen { get; } = new int[Cascades];

        // ---- What the frame is told.

        /// <summary>The sky's scale, the scattered sun and the far land's light, as the ambient pass would use them (SkySYstem).</summary>
        public void SetAmbient(float skyScale, Vector3 skyFill, Vector3 groundRadiance)
        {
            _skyScale = skyScale;
            _skyFill = skyFill;
            _groundRadiance = groundRadiance;
        }

        public void SubmitPointLight(PointlightRenderCommand command)
        {
            AddLight(command.positionWorld, command.radius, command.colour, Vector3.UnitY, -2f);
        }

        public void SubmitSpotLight(SpotlightRenderCommand command)
        {
            AddLight(command.positionWorld, command.radius, command.colour, command.direction.Normalized(), command.cutoffCosine);
        }

        private readonly record struct Light(Vector3 Position, float Radius, Vector3 Colour, Vector3 Direction, float Cone);
        private readonly List<Light> _submitted = new();
        private Vector3 _lightFocus;

        private void AddLight(Vector3 position, float radius, Vector3 colour, Vector3 direction, float cone)
        {
            if (radius <= 0f || colour == Vector3.Zero) return;
            _submitted.Add(new Light(position, radius, colour, direction, cone));
        }

        /// <summary>
        /// Whether GI bounces this light this frame, as it does the nearest <see cref="MaxLights"/>: a light renderer leaves out the fill
        /// that stands in for its bounce if so, and keeps it for any light further off.
        /// </summary>
        public bool Bounces(Vector3 position, float radius) => Active && (position - _lightFocus).Length - radius <= _furthestReach;

        /// <summary>How much of a light's fill to keep: none once GI has faded in, if GI bounces it.</summary>
        public float FillKept(Vector3 position, float radius) => Bounces(position, radius) ? 1f - _presence : 1f;

        private float _furthestReach = float.MaxValue;

        // The lights that reach nearest to where the camera looks, as many as GI has room for.
        private void GatherLights(Vector3 focus)
        {
            _lightFocus = focus;
            if (_submitted.Count > MaxLights) _submitted.Sort((a, b) => Reach(a).CompareTo(Reach(b)));
            _lightCount = Math.Min(_submitted.Count, MaxLights);
            _furthestReach = _submitted.Count > MaxLights ? Reach(_submitted[MaxLights - 1]) + 1e-3f : float.MaxValue;
            for (int i = 0; i < _lightCount; i++)
            {
                Light light = _submitted[i];
                int at = i * 12;
                _lightData[at + 0] = light.Position.X;
                _lightData[at + 1] = light.Position.Y;
                _lightData[at + 2] = light.Position.Z;
                _lightData[at + 3] = light.Radius;
                _lightData[at + 4] = light.Colour.X;
                _lightData[at + 5] = light.Colour.Y;
                _lightData[at + 6] = light.Colour.Z;
                _lightData[at + 7] = 0f;
                _lightData[at + 8] = light.Direction.X;
                _lightData[at + 9] = light.Direction.Y;
                _lightData[at + 10] = light.Direction.Z;
                _lightData[at + 11] = light.Cone;
            }
            _submitted.Clear();
        }

        private float Reach(Light light) => (light.Position - _lightFocus).Length - light.Radius;

        public override void Update()
        {
        }

        // ---- The frame.

        /// <summary>Everything GI does in a frame, after the G-buffer and before the ambient pass, which reads what it gathers.</summary>
        public void RenderPass(RenderEngine engine)
        {
            bool enabled = _settings.Enabled && _world != null;
            if (enabled && !_wasEnabled) InvalidateLevels();
            _wasEnabled = enabled;
            if (!enabled)
            {
                Active = false;
                _presence = 0f;
                _submitted.Clear();
                // Turned off in the settings, it gives back its memory, as that's why a player would; between worlds it keeps it.
                if (!_settings.Enabled && _resources) CleanUp();
                return;
            }
            if (!_resources) CreateResources();

            _frame++;
            GatherLights(Focus(engine));
            UploadFrame(engine);
            BindShared();

            Engine.PerformanceMonitor.startGPUTask("GI Voxels");
            UpdateVoxels(engine);
            Engine.PerformanceMonitor.finishGPUTask("GI Voxels");

            bool built = AllLevelsBuilt();
            if (built)
            {
                LightVoxels(engine);

                UpdateProbes(engine);

                Engine.PerformanceMonitor.startGPUTask("GI Gather");
                Gather(engine);
                Engine.PerformanceMonitor.finishGPUTask("GI Gather");
                RememberProbes();
            }
            else ForgetProbes();

            _presence = built ? MathF.Min(_presence + Engine.Delta / FadeIn, 1f) : 0f;
            Active = built && _presence > 0f;
            if (built && _settings.Debug != 0 && _frame % 120 == 0) Report(engine);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
        }

        // What GI is holding, read back from the GPU, which waits on it: only while a debug view is up.
        private void Report(RenderEngine engine)
        {
            var counts = new uint[CountsLength];
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _counts);
            GL.GetBufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, CountsLength * sizeof(uint), counts);
            var sky = new float[8 * 4];
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _skyLight);
            GL.GetBufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, sky.Length * sizeof(float), sky);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            for (int c = 0; c < Cascades; c++) ProbesSeen[c] = (int)counts[28 + c];
            Vector3 up = new Vector3(sky[8], sky[9], sky[10]) / MathF.PI;
            Vector3 side = new Vector3(sky[0], sky[1], sky[2]) / MathF.PI;
            Console.WriteLine($"GI: surfaces {counts[0]} {counts[1]} {counts[2]} {counts[3]}, probes {counts[28]} {counts[29]} {counts[30]} {counts[31]} {counts[32]} ({counts[96]} placed afresh), pending {counts[120]} {counts[121]} {counts[122]} {counts[123]} at {_cacheCursor[0]} {_cacheCursor[1]} {_cacheCursor[2]} {_cacheCursor[3]}, " +
                $"sky up {up.X:0.00} {up.Y:0.00} {up.Z:0.00} side {side.X:0.00} {side.Y:0.00} {side.Z:0.00}, lights {_lightCount}; " +
                $"flat sky {engine._ambientLightRenderer.SkyLight.X:0.00} {engine._ambientLightRenderer.SkyLight.Y:0.00} {engine._ambientLightRenderer.SkyLight.Z:0.00} " +
                $"ground {engine._ambientLightRenderer.GroundLight.X:0.00} {engine._ambientLightRenderer.GroundLight.Y:0.00} {engine._ambientLightRenderer.GroundLight.Z:0.00}");
            ReportProbes();
        }

        private void CreateResources()
        {
            _resources = true;

            GL.GetInteger((GetPName)All.MaxShaderStorageBufferBindings, out int bindings);
            GL.GetInteger((GetPName)All.MaxComputeShaderStorageBlocks, out int blocks);
            Console.WriteLine($"Global illumination: {bindings} storage buffer bindings, {blocks} blocks to a compute shader.");

            _ubo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, _ubo);
            GL.BufferData(BufferTarget.UniformBuffer, _uboData.Length * sizeof(int), IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);

            _counts = CreateBuffer(CountsLength * sizeof(uint));
            _lights = CreateBuffer(_lightData.Length * sizeof(float));
            _skyLight = CreateBuffer(8 * 16);

            int[] nsides = { 1, 2, 4, 8, 16 };
            Vector4[] directions = HealPix.Table(nsides, out float stray);
            if (stray > 1f) Console.WriteLine($"Global illumination: HEALPix children stray {stray:0.00} cells from their parents. The cascades won't merge right.");
            _directions = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _directions);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, directions.Length * 16, directions, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            _skyShader = new ShaderProgram("GI/GI_Sky.comp");
            _debugShader = new ShaderProgram("Simple.vert", "GI/GI_Debug.frag");

            CreateVoxelResources();
            CreateProbeResources();
        }

        private static int CreateBuffer(int bytes)
        {
            int buffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, buffer);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, bytes, new byte[bytes], BufferUsageHint.DynamicCopy);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            return buffer;
        }

        // ---- What every GI shader reads (GiData in GI/GI.glsl).

        private void PutInt(int index, int value) => _uboData[index] = value;
        private void PutFloat(int index, float value) => _uboData[index] = BitConverter.SingleToInt32Bits(value);
        private void PutVector(int index, Vector3 value, float w)
        {
            PutFloat(index, value.X);
            PutFloat(index + 1, value.Y);
            PutFloat(index + 2, value.Z);
            PutFloat(index + 3, w);
        }

        private Vector3 Focus(RenderEngine engine)
        {
            Vector4 focus = engine.context.focusPoint;
            return focus.W > 0f ? focus.Xyz : engine.context.viewPos;
        }

        private void UploadFrame(RenderEngine engine)
        {
            Vector3 focus = Focus(engine);
            for (int level = 0; level < Levels; level++)
            {
                Vector3i origin = _origin[level];
                PutInt(level * 4 + 0, origin.X);
                PutInt(level * 4 + 1, origin.Y);
                PutInt(level * 4 + 2, origin.Z);
                PutInt(level * 4 + 3, 0);
                float size = VoxelSize(level);
                PutFloat(16 + level * 4 + 0, size);
                PutFloat(16 + level * 4 + 1, 1f / size);
                PutFloat(16 + level * 4 + 2, 0f);
                PutFloat(16 + level * 4 + 3, 0f);
            }

            LightingSettings lighting = engine.Lighting;
            Vector3 toSun = engine.context.sunDirection.LengthSquared > 0f ? engine.context.sunDirection.Normalized() : Vector3.UnitY;
            bool sun = engine.context.sunColour != Vector3.Zero;
            PutVector(32, toSun, sun ? 1f : 0f);
            PutVector(36, engine.context.sunColour, 0f);
            PutVector(40, _skyFill, _skyScale);
            PutVector(44, _groundRadiance, 0f);
            PutFloat(48, lighting.Emission ? lighting.EmissionScale : 0f);
            PutFloat(49, Math.Clamp(_settings.Bounce, 0f, 1f));
            PutFloat(50, _frame);
            PutFloat(51, _lightCount);

            for (int c = 0; c < Cascades; c++)
            {
                float spacing = FinestSpacing * (1 << c);
                float start = c == 0 ? FirstRayStart : 0.5f * MathF.Pow(4f, c - 1);
                float end = 0.5f * MathF.Pow(4f, c);
                PutFloat(52 + c * 4 + 0, spacing);
                PutFloat(52 + c * 4 + 1, start);
                PutFloat(52 + c * 4 + 2, end);
                PutFloat(52 + c * 4 + 3, DirectionCount[c]);

                Vector2i dims = ColumnDims[c];
                Vector3 centre = focus + new Vector3(0f, 0f, RegionAhead);
                Vector2i origin = new Vector2i((int)MathF.Floor(centre.X / spacing) - dims.X / 2, (int)MathF.Floor(centre.Z / spacing) - dims.Y / 2);
                _columnOrigin[c] = origin;
                PutInt(72 + c * 4 + 0, origin.X);
                PutInt(72 + c * 4 + 1, origin.Y);
                PutInt(72 + c * 4 + 2, dims.X);
                PutInt(72 + c * 4 + 3, dims.Y);
            }
            PutVector(92, focus, 0f);
            PutFloat(96, _heightMapping.X);
            PutFloat(97, _heightMapping.Y);
            PutFloat(98, _heightMapping.Z);
            PutFloat(99, _heightMapping.W);

            GL.BindBuffer(BufferTarget.UniformBuffer, _ubo);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, _uboData.Length * sizeof(int), _uboData);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _lights);
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, Math.Max(_lightCount, 1) * 12 * sizeof(float), _lightData);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        /// <summary>Binds what every GI pass may read, for the rest of the frame's GI work.</summary>
        private void BindShared()
        {
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 11, _ubo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _counts);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _directions);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, _pool);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 3, _lights);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 4, _skyLight);
            GL.BindBuffer(BufferTarget.DispatchIndirectBuffer, _counts);

            BindTexture(UnitBricks, TextureTarget.Texture3D, _voxBricks);
            BindTexture(UnitIndex, TextureTarget.Texture3D, _voxIndex);
            BindTexture(UnitHeight, TextureTarget.Texture2D, _heightMap);
            BindTexture(UnitBrickLight, TextureTarget.Texture3D, _brickLight);
            BindTexture(UnitColour, TextureTarget.Texture3D, _voxColor);
            BindTexture(UnitChunkLayers, TextureTarget.Texture2D, _chunkLayers);
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        private static void BindTexture(int unit, TextureTarget target, int texture)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + unit);
            GL.BindTexture(target, texture);
        }

        /// <summary>Points a GI shader's samplers at the units <see cref="BindShared"/> fills. Call with it bound.</summary>
        private static void LinkShared(ShaderProgram shader, bool bricks = false, bool index = false, bool height = false, bool brickLight = false, bool colour = false)
        {
            if (bricks) shader.loadUniformInt("giBricks", UnitBricks);
            if (index) shader.loadUniformInt("giIndex", UnitIndex);
            if (height) shader.loadUniformInt("giHeightMap", UnitHeight);
            if (brickLight) shader.loadUniformInt("giBrickLight", UnitBrickLight);
            if (colour) shader.loadUniformInt("giColour", UnitColour);
        }

        private static void Dispatch(int x, int y = 1, int z = 1)
        {
            if (x <= 0 || y <= 0 || z <= 0) return;
            GL.DispatchCompute(x, y, z);
        }

        private static void Barrier()
        {
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit | MemoryBarrierFlags.ShaderImageAccessBarrierBit
                | MemoryBarrierFlags.TextureFetchBarrierBit | MemoryBarrierFlags.CommandBarrierBit | MemoryBarrierFlags.UniformBarrierBit);
        }

        private static int Groups(int count, int size) => (count + size - 1) / size;

        // ---- An exclusive prefix sum over a buffer, in place (GI_Scan.comp). The grand total follows the groups' sums.

        private ShaderProgram _scanShader = null!;
        private int _scanSums;
        private const int ScanGroup = 512;

        private int Scan(int buffer, int count)
        {
            int groups = Groups(count, ScanGroup);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 15, buffer);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 14, _scanSums);
            _scanShader.bind();
            _scanShader.loadUniformInt("count", count);
            _scanShader.loadUniformInt("stage", 0);
            Dispatch(groups);
            Barrier();
            _scanShader.loadUniformInt("count", groups);
            _scanShader.loadUniformInt("stage", 1);
            Dispatch(1);
            Barrier();
            _scanShader.loadUniformInt("count", count);
            _scanShader.loadUniformInt("stage", 2);
            Dispatch(groups);
            Barrier();
            return groups;
        }

        // ---- What the ambient pass reads.

        /// <summary>Binds what's been gathered (a texture array of four layers at half the resolution each way) for the ambient pass.</summary>
        public void BindGathered(int unit)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + unit);
            GL.BindTexture(TextureTarget.Texture2DArray, _resources ? _gathered : 0);
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        /// <summary>The voxels in place of the picture, for --render gidebug=1 or 2. Drawn over the lit scene, before anything else is.</summary>
        public void DrawDebug(RenderEngine engine)
        {
            int mode = _settings.Debug;
            if (!Active || (mode != 1 && mode != 2)) return;
            BindShared();
            engine.Msaa.SceneBuffer.bind();
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.StencilTest);
            GL.Disable(EnableCap.Blend);
            _debugShader.bind();
            LinkShared(_debugShader, bricks: true, index: true, colour: true);
            _debugShader.loadUniformInt("mode", mode);
            engine.ScreenQuadRenderer.Render(clearColor: false, blend: false);
            _debugShader.unBind();
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
            if (_resources) CreateGathered();
        }

        public override void CleanUp()
        {
            if (!_resources) return;
            GL.DeleteBuffer(_ubo);
            GL.DeleteBuffer(_counts);
            GL.DeleteBuffer(_lights);
            GL.DeleteBuffer(_skyLight);
            GL.DeleteBuffer(_directions);
            _skyShader.cleanUp();
            _debugShader.cleanUp();
            CleanUpVoxels();
            CleanUpProbes();
            // Made again from nothing if it's turned back on. A name GL has taken back may already be someone else's, so none is kept.
            _heightMap = 0;
            _gathered = 0;
            _resources = false;
        }
    }
}
