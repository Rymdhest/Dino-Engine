using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.Rendering.Renderers.Lighting;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

namespace Dino_Engine.Rendering.Renderers.Geometry
{

    public struct GrassChunkRenderData
    {
        public Vector2 chunkPos;
        public float size;
        public float arrayID;
    }

    public struct GrassRenderCommand : IRenderCommand
    {
        public GrassChunkRenderData[] chunks;
        // Read from geometry commands only; shadow passes use the settings of the frame's geometry command.
        public GrassSingleton settings;

        public GrassRenderCommand(GrassChunkRenderData[] chunks) : this(chunks, new GrassSingleton())
        {
        }

        public GrassRenderCommand(GrassChunkRenderData[] chunks, GrassSingleton settings)
        {
            this.chunks = chunks;
            this.settings = settings;
        }
    }

    /// <summary>
    /// GPU grass after Ghost of Tsushima. For every view (the camera, and each light that casts grass shadows)
    /// Grass/Grass_Generate.comp scatters blades over the submitted chunks, gathers them into tufts, thins them with
    /// distance, culls them and appends the survivors, as finished Bézier curves, to a near list (15 vertex blades) and a
    /// far list (7 vertices). Both are drawn with indirect draws, the vertices built from gl_VertexID alone. Some blades are
    /// plumes or blooms, which the same strips draw differently; where they grow is in the terrain's bloom and colour maps.
    /// Settings come from <see cref="GrassSingleton"/> and reach the shaders as the GrassData uniform block (binding
    /// <see cref="UniformBinding"/>, Grass/GrassField.glsl), which Terrain.frag also reads to tint the ground under the grass.
    /// With MSAA, blades thinner than <see cref="MinPixelWidth"/> on screen are drawn that wide over a matching share of
    /// the samples (<see cref="AntiAliasing.AntiAliasingSettings.GrassCoverage"/>), so they don't pop between samples.
    /// </summary>
    public class GrassRenderer : GeometryCommandDrivenRenderer<GrassRenderCommand>
    {
        public const int UniformBinding = 7;
        public static readonly int MAX_GRASS_CHUNKS = 1024;

        // Bindings and sizes shared with the shaders in Shaders/Grass.
        private const int ChunkUniformBinding = 5;
        private const int CommandBinding = 0;
        private const int NearBinding = 1;
        private const int FarBinding = 2;
        private const int WorkGroupSize = 64;
        private const int BladeStride = 64;
        private const int NearSegments = 7;
        private const int FarSegments = 3;
        private const int NearCapacity = 1 << 20;
        private const int FarCapacity = 1 << 20;
        private const int SettingsVec4s = 14;
        private const int StatsFrames = 4;
        // Least width of a blade on screen, in pixels, when thin blades are anti-aliased by coverage (Grass.vert). At one
        // pixel a blade crossing the standard MSAA patterns always spans one of every sample position, so the share of
        // the samples it covers stays put as it moves.
        private const float MinPixelWidth = 1f;

        private const int HeightMapUnit = 0;
        private const int GrassMapUnit = 1;
        private const int GrassColourMapUnit = 2;
        private const int GrassBloomMapUnit = 3;
        private const int BendMapUnit = 8;
        private const int WindMapUnit = 9;

        // The two DrawArraysIndirect commands (count, instanceCount, first, baseInstance) as each generate pass starts.
        private static readonly uint[] EmptyCommands = { NearSegments * 2 + 1, 0, 0, 0, FarSegments * 2 + 1, 0, 0, 0 };

        private readonly ShaderProgram _generateShader = new ShaderProgram("Grass/Grass_Generate.comp");
        private readonly ShaderProgram _grassShader = new ShaderProgram("Grass/Grass.vert", "Grass/Grass.frag");
        private readonly ShaderProgram _grassShadowShader = new ShaderProgram("Grass/Grass_Shadow.vert", "Grass/Grass_Shadow.frag");

        private readonly int _chunkBuffer;
        private readonly int _settingsBuffer;
        private readonly int _commandBuffer;
        private readonly int _nearBuffer;
        private readonly int _farBuffer;
        private readonly int _emptyVao;
        private readonly int[] _statsBuffers = new int[StatsFrames];
        private int _statsFrame;

        private readonly Vector4[] _chunkData = new Vector4[MAX_GRASS_CHUNKS];
        private readonly float[] _settingsData = new float[SettingsVec4s * 4];
        private readonly Vector4[] _frustumPlanes = new Vector4[6];
        private readonly List<GrassChunkRenderData> _shadowChunks = new();
        private readonly uint[] _stats = new uint[2];
        private GrassSingleton _settings = new GrassSingleton();

        /// <summary>Blades the camera drew a few frames ago (the counts are read back late so the GPU never stalls).</summary>
        public int NearBladesDrawn { get; private set; }
        public int FarBladesDrawn { get; private set; }

        public GrassRenderer() : base("Grass")
        {
            _generateShader.bind();
            _generateShader.loadUniformInt("heightmaps", HeightMapUnit);
            _generateShader.loadUniformInt("grassMaps", GrassMapUnit);
            _generateShader.loadUniformInt("grassColourMaps", GrassColourMapUnit);
            _generateShader.loadUniformInt("grassBloomMaps", GrassBloomMapUnit);
            _generateShader.loadUniformInt("bendMap", BendMapUnit);
            _generateShader.loadUniformInt("windMap", WindMapUnit);
            _generateShader.loadUniformInt("nearCapacity", NearCapacity);
            _generateShader.loadUniformInt("farCapacity", FarCapacity);
            _generateShader.loadUniformFloat("textureMapOffset", 1f / TerrainRenderer.CHUNK_RESOLUTION);
            _generateShader.unBind();

            _chunkBuffer = CreateBuffer(BufferTarget.UniformBuffer, MAX_GRASS_CHUNKS * Vector4.SizeInBytes, BufferUsageHint.DynamicDraw);
            _settingsBuffer = CreateBuffer(BufferTarget.UniformBuffer, SettingsVec4s * Vector4.SizeInBytes, BufferUsageHint.DynamicDraw);
            _commandBuffer = CreateBuffer(BufferTarget.ShaderStorageBuffer, EmptyCommands.Length * sizeof(uint), BufferUsageHint.DynamicCopy);
            _nearBuffer = CreateBuffer(BufferTarget.ShaderStorageBuffer, NearCapacity * BladeStride, BufferUsageHint.DynamicCopy);
            _farBuffer = CreateBuffer(BufferTarget.ShaderStorageBuffer, FarCapacity * BladeStride, BufferUsageHint.DynamicCopy);
            for (int i = 0; i < StatsFrames; i++)
            {
                _statsBuffers[i] = CreateBuffer(BufferTarget.CopyWriteBuffer, 2 * sizeof(uint), BufferUsageHint.StreamRead);
            }
            _emptyVao = GL.GenVertexArray();

            UploadSettings(_settings);
        }

        private static int CreateBuffer(BufferTarget target, int size, BufferUsageHint usage)
        {
            int buffer = GL.GenBuffer();
            GL.BindBuffer(target, buffer);
            GL.BufferData(target, size, IntPtr.Zero, usage);
            GL.BindBuffer(target, 0);
            return buffer;
        }

        /// <summary>Blades per side of a chunk for the requested density.</summary>
        private static int BladesPerAxis(float chunkSize, float density)
        {
            return Math.Clamp((int)MathF.Round(chunkSize * MathF.Sqrt(MathF.Max(density, 0f))), 1, 256);
        }

        public override void Update()
        {
        }

        public override void CleanUp()
        {
            _generateShader.cleanUp();
            _grassShader.cleanUp();
            _grassShadowShader.cleanUp();
            GL.DeleteBuffer(_chunkBuffer);
            GL.DeleteBuffer(_settingsBuffer);
            GL.DeleteBuffer(_commandBuffer);
            GL.DeleteBuffer(_nearBuffer);
            GL.DeleteBuffer(_farBuffer);
            foreach (int buffer in _statsBuffers) GL.DeleteBuffer(buffer);
            GL.DeleteVertexArray(_emptyVao);
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        private void UploadSettings(GrassSingleton s)
        {
            WriteSettings(0, s.Density, s.DrawDistance, s.FalloffStart, s.MinDensity);
            WriteSettings(1, s.LodDistance, s.ShadowDistance, s.ShadowDensity, s.Thickening);
            WriteSettings(2, s.Height, s.HeightVariation, s.Width, s.Roundness);
            WriteSettings(3, s.Tilt, s.TiltVariation, s.Droop, 0f);
            WriteSettings(4, s.ClumpSize, s.ClumpPull, s.ClumpHeightVariation, s.ClumpSplay);
            WriteSettings(5, s.RootColour.X, s.RootColour.Y, s.RootColour.Z, s.ColourVariation);
            WriteSettings(6, s.BodyColour.X, s.BodyColour.Y, s.BodyColour.Z, s.DryPatches);
            WriteSettings(7, s.TipColour.X, s.TipColour.Y, s.TipColour.Z, s.Translucency);
            WriteSettings(8, s.DryColour.X, s.DryColour.Y, s.DryColour.Z, s.Roughness);
            WriteSettings(9, s.Enabled ? s.GroundTint : 0f, s.BloomAmount, 0f, 0f);
            WriteSettings(10, s.Swell, 1f / MathF.Max(s.SwellSize, 0.1f), s.Comb, 1f / MathF.Max(s.CombSize, 0.1f));
            WriteSettings(11, s.WindGain, s.WaveStrength, 1f / MathF.Max(s.WaveLength, 0.1f), s.WindSheen);
            WriteSettings(12, s.HueDrift, s.CanopyShade, s.FieldNormal, s.BloomSize);
            WriteSettings(13, s.PlumeColour.X, s.PlumeColour.Y, s.PlumeColour.Z, s.PlumeAmount);

            GL.BindBuffer(BufferTarget.UniformBuffer, _settingsBuffer);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, _settingsData.Length * sizeof(float), _settingsData);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, UniformBinding, _settingsBuffer);
        }

        private void WriteSettings(int vec4Index, float x, float y, float z, float w)
        {
            int offset = vec4Index * 4;
            _settingsData[offset] = x;
            _settingsData[offset + 1] = y;
            _settingsData[offset + 2] = z;
            _settingsData[offset + 3] = w;
        }

        /// <summary>
        /// Planes (xyz normal, w distance; inside where dot(n, p) + w >= 0) of the volume a view-projection matrix sees,
        /// with OpenGL's -w..w clip depth.
        /// </summary>
        private void ExtractFrustumPlanes(Matrix4 viewProjection)
        {
            Vector4 c0 = viewProjection.Column0;
            Vector4 c1 = viewProjection.Column1;
            Vector4 c2 = viewProjection.Column2;
            Vector4 c3 = viewProjection.Column3;
            _frustumPlanes[0] = c3 + c0;
            _frustumPlanes[1] = c3 - c0;
            _frustumPlanes[2] = c3 + c1;
            _frustumPlanes[3] = c3 - c1;
            _frustumPlanes[4] = c3 + c2;
            _frustumPlanes[5] = c3 - c2;
            for (int i = 0; i < 6; i++)
            {
                _frustumPlanes[i] /= _frustumPlanes[i].Xyz.Length;
            }
        }

        /// <summary>Grows the grass of <paramref name="chunks"/> seen through <paramref name="viewProjection"/> into the blade lists.</summary>
        private bool Generate(IReadOnlyList<GrassChunkRenderData> chunks, Matrix4 viewProjection, bool shadowPass)
        {
            int chunkCount = Math.Min(chunks.Count, MAX_GRASS_CHUNKS);
            if (chunkCount == 0 || !_settings.Enabled) return false;
            if (chunks.Count > MAX_GRASS_CHUNKS) Console.WriteLine("Warning: More chunks in command than MAX_GRASS_CHUNKS. Some chunks will not be rendered.");

            for (int i = 0; i < chunkCount; i++)
            {
                var chunk = chunks[i];
                _chunkData[i] = new Vector4(chunk.chunkPos.X, chunk.chunkPos.Y, chunk.size, chunk.arrayID);
            }
            GL.BindBuffer(BufferTarget.UniformBuffer, _chunkBuffer);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, chunkCount * Vector4.SizeInBytes, _chunkData);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, ChunkUniformBinding, _chunkBuffer);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _commandBuffer);
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, EmptyCommands.Length * sizeof(uint), EmptyCommands);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, CommandBinding, _commandBuffer);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, NearBinding, _nearBuffer);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, FarBinding, _farBuffer);

            RenderEngine renderEngine = Engine.RenderEngine;
            GL.ActiveTexture(TextureUnit.Texture0 + HeightMapUnit);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine._terrainRenderer.GetNormalRoadHeightTextureArray());
            GL.ActiveTexture(TextureUnit.Texture0 + GrassMapUnit);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine._terrainRenderer.GetGrassTextureArray());
            GL.ActiveTexture(TextureUnit.Texture0 + GrassColourMapUnit);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine._terrainRenderer.GetGrassColourTextureArray());
            GL.ActiveTexture(TextureUnit.Texture0 + GrassBloomMapUnit);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine._terrainRenderer.GetGrassBloomTextureArray());
            GL.ActiveTexture(TextureUnit.Texture0 + BendMapUnit);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine._windRenderer.VegetationBendTexture);
            GL.ActiveTexture(TextureUnit.Texture0 + WindMapUnit);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine._windRenderer.WindMapTexture);
            GL.ActiveTexture(TextureUnit.Texture0);

            ExtractFrustumPlanes(viewProjection);
            int bladesPerAxis = BladesPerAxis(chunks[0].size, _settings.Density);

            _generateShader.bind();
            _generateShader.loadUniformVector4fArray("frustumPlanes", _frustumPlanes);
            _generateShader.loadUniformInt("bladesPerAxis", bladesPerAxis);
            _generateShader.loadUniformInt("shadowPass", shadowPass ? 1 : 0);
            GL.DispatchCompute((bladesPerAxis * bladesPerAxis + WorkGroupSize - 1) / WorkGroupSize, chunkCount, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit | MemoryBarrierFlags.CommandBarrierBit);
            return true;
        }

        private void DrawList(ShaderProgram shader, int blades, int segments, int commandIndex)
        {
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, NearBinding, blades);
            shader.loadUniformInt("segments", segments);
            GL.DrawArraysIndirect(PrimitiveType.TriangleStrip, (IntPtr)(commandIndex * 4 * sizeof(uint)));
        }

        /// <summary>Copies this frame's blade counts aside and reads back the ones copied <see cref="StatsFrames"/> - 1 frames ago.</summary>
        private void UpdateStats()
        {
            int current = _statsBuffers[_statsFrame % StatsFrames];
            GL.BindBuffer(BufferTarget.CopyReadBuffer, _commandBuffer);
            GL.BindBuffer(BufferTarget.CopyWriteBuffer, current);
            GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, (IntPtr)sizeof(uint), IntPtr.Zero, sizeof(uint));
            GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, (IntPtr)(5 * sizeof(uint)), (IntPtr)sizeof(uint), sizeof(uint));
            _statsFrame++;

            if (_statsFrame >= StatsFrames)
            {
                GL.BindBuffer(BufferTarget.CopyReadBuffer, _statsBuffers[_statsFrame % StatsFrames]);
                GL.GetBufferSubData(BufferTarget.CopyReadBuffer, IntPtr.Zero, 2 * sizeof(uint), _stats);
                NearBladesDrawn = (int)_stats[0];
                FarBladesDrawn = (int)_stats[1];
            }
            GL.BindBuffer(BufferTarget.CopyReadBuffer, 0);
            GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0);
        }

        internal override void PrepareGeometry(RenderEngine renderEngine)
        {
        }

        internal override void PerformGeometryCommand(GrassRenderCommand command, RenderEngine renderEngine)
        {
            _settings = command.settings;
            UploadSettings(_settings);

            Matrix4 viewProjection = renderEngine.context.viewMatrix * renderEngine.context.projectionMatrix;
            if (!Generate(command.chunks, viewProjection, shadowPass: false))
            {
                NearBladesDrawn = 0;
                FarBladesDrawn = 0;
                return;
            }
            UpdateStats();

            renderEngine.Msaa.GeometryBuffer.bind();
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            // Blades are seen from both sides.
            GL.Disable(EnableCap.CullFace);

            _grassShader.bind();
            // Blades thinner than a pixel cover part of the samples instead; with one sample there is nothing to share.
            bool coverage = renderEngine.AntiAliasing.GrassCoverage && renderEngine.Msaa.Samples > 1;
            _grassShader.loadUniformFloat("minPixelWidth", coverage ? MinPixelWidth : 0f);
            GL.BindVertexArray(_emptyVao);
            GL.BindBuffer(BufferTarget.DrawIndirectBuffer, _commandBuffer);
            DrawList(_grassShader, _nearBuffer, NearSegments, 0);
            DrawList(_grassShader, _farBuffer, FarSegments, 1);
            GL.BindBuffer(BufferTarget.DrawIndirectBuffer, 0);
            GL.BindVertexArray(0);
        }

        internal override void FinishGeometry(RenderEngine renderEngine)
        {
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
        }

        internal override void PrepareShadow(RenderEngine renderEngine)
        {
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);
        }

        internal override void FinishShadow(RenderEngine renderEngine)
        {
            GL.Disable(EnableCap.PolygonOffsetFill);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
        }

        internal override void PerformShadowCommand(GrassRenderCommand command, Shadow shadow, RenderEngine renderEngine)
        {
            // Only chunks within the shadow distance of the camera have blades that cast shadows.
            Vector2 camera = renderEngine.context.viewPos.Xz;
            _shadowChunks.Clear();
            foreach (var chunk in command.chunks)
            {
                Vector2 nearest = Vector2.Clamp(camera, chunk.chunkPos, chunk.chunkPos + new Vector2(chunk.size));
                if ((nearest - camera).Length < _settings.ShadowDistance) _shadowChunks.Add(chunk);
            }

            Matrix4 viewProjection = shadow.lightViewMatrix * shadow.shadowProjectionMatrix;
            if (!Generate(_shadowChunks, viewProjection, shadowPass: true)) return;

            if (shadow.isCubeMap && shadow.cubemapFaceIndex >= 0)
            {
                shadow.shadowFrameBuffer.bindFace(TextureTarget.TextureCubeMapPositiveX + shadow.cubemapFaceIndex);
            }
            else
            {
                shadow.shadowFrameBuffer.bind();
            }
            GL.PolygonOffset(0.01f, 0.01f);

            // Blades widen where the light sees them edge-on: a directional light (orthographic shadow) from its
            // direction, other lights from their position.
            Matrix4 lightToWorld = shadow.lightViewMatrix.Inverted();
            bool directional = MathF.Abs(shadow.shadowProjectionMatrix.M34) < 1e-6f;
            Vector4 eye = directional
                ? new Vector4((new Vector4(0f, 0f, 1f, 0f) * lightToWorld).Xyz.Normalized(), 0f)
                : new Vector4(lightToWorld.ExtractTranslation(), 1f);

            _grassShadowShader.bind();
            _grassShadowShader.loadUniformMatrix4f("shadowViewProjectionMatrix", viewProjection);
            _grassShadowShader.loadUniformVector4f("shadowEye", eye);
            GL.BindVertexArray(_emptyVao);
            GL.BindBuffer(BufferTarget.DrawIndirectBuffer, _commandBuffer);
            DrawList(_grassShadowShader, _farBuffer, FarSegments, 1);
            GL.BindBuffer(BufferTarget.DrawIndirectBuffer, 0);
            GL.BindVertexArray(0);
        }
    }
}
