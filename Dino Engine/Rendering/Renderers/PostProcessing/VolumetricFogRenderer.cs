using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.AntiAliasing;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    public struct VolumetricFogRenderCommand : IRenderCommand
    {
        public VolumetricFogSingleton settings;
        public float sunThrough;    // How much of the sun gets past the clouds here, for the fog to scatter.

        // Noise-space scroll offsets, accumulated and wrapped by VolumetricFogSystem.
        public Vector3 baseOffset;
        public Vector3 warpOffset;
        public Vector3 detailOffset;

        // Terrain under the camera for the top-down height capture.
        public TerrainChunkRenderData[] terrainChunks;
        public Vector3 terrainCaptureCenter;
    }

    /// <summary>
    /// Volumetric ground fog, built for a camera that looks down on the world:
    /// 1. A top-down orthographic capture of the terrain heights around the camera, so the fog can follow the ground.
    /// 2. A half-resolution ray-march clipped to the thin slab the fog lives in (VolumetricFog_March.frag).
    /// 3. Temporal accumulation of the jittered march (VolumetricFog_Temporal.frag).
    /// 4. A depth-aware upsample and composite onto the lit image, plus analytic distance haze (VolumetricFog_Composite.frag).
    /// The light systems submit their render commands here too (SubmitDirectionalLight/PointLight/SpotLight), so the fog
    /// is lit by exactly the lights that light the surfaces.
    /// </summary>
    public class VolumetricFogRenderer : CommandDrivenRenderer<VolumetricFogRenderCommand>
    {
        private const int NoiseResolution = 128;
        private const int TilesRestFromFrameRate = 75;
        private const int TerrainHeightResolution = 512;
        private const float TerrainDepthRange = 1000f;

        // Light limits; array sizes must match VolumetricFog_March.frag. The shadow maps fill texture units 3..14 and
        // the wind map unit 15, within the 16 units OpenGL guarantees.
        private const int MaxDirectionalLights = 4;
        private const int MaxCascadeSlots = 5;
        private const int MaxPointLights = 16;
        private const int MaxPointShadows = 3;
        private const int MaxSpotLights = 16;
        private const int MaxSpotShadows = 4;
        private const int CascadeUnitStart = 3;
        private const int SpotShadowUnitStart = CascadeUnitStart + MaxCascadeSlots;
        private const int PointShadowUnitStart = SpotShadowUnitStart + MaxSpotShadows;
        private const int WindMapUnit = PointShadowUnitStart + MaxPointShadows;

        private static readonly string[] DirectionalColourNames = UniformNames("directionalColours", MaxDirectionalLights);
        private static readonly string[] DirectionalDirectionNames = UniformNames("directionalDirections", MaxDirectionalLights);
        private static readonly string[] DirectionalCascadeStartNames = UniformNames("directionalCascadeStart", MaxDirectionalLights);
        private static readonly string[] DirectionalCascadeCountNames = UniformNames("directionalCascadeCount", MaxDirectionalLights);
        private static readonly string[] CascadeMatrixNames = UniformNames("cascadeMatrices", MaxCascadeSlots);
        private static readonly string[] PointPositionRadiusNames = UniformNames("pointPositionRadius", MaxPointLights);
        private static readonly string[] PointColourShadowNames = UniformNames("pointColourShadow", MaxPointLights);
        private static readonly string[] SpotPositionRadiusNames = UniformNames("spotPositionRadius", MaxSpotLights);
        private static readonly string[] SpotColourShadowNames = UniformNames("spotColourShadow", MaxSpotLights);
        private static readonly string[] SpotDirectionCutoffNames = UniformNames("spotDirectionCutoff", MaxSpotLights);
        private static readonly string[] SpotSoftnessNames = UniformNames("spotSoftness", MaxSpotLights);
        private static readonly string[] SpotShadowMatrixNames = UniformNames("spotShadowMatrices", MaxSpotShadows);

        private readonly ShaderProgram _noiseShader = new ShaderProgram("Simple.vert", "VolumetricFog_Noise.frag");
        private readonly ShaderProgram _marchShader = new ShaderProgram("Simple.vert", "VolumetricFog_March.frag");
        private readonly ShaderProgram _temporalShader = new ShaderProgram("Simple.vert", "VolumetricFog_Temporal.frag");
        // Per pixel, and per sample for the edge pixels (MsaaRenderer.DrawSplit).
        private readonly ShaderProgram _compositeShader = new ShaderProgram("Simple.vert", "VolumetricFog_Composite.frag");
        private readonly ShaderProgram _compositeSampleShader = new ShaderProgram("Simple.vert", "VolumetricFog_Composite.frag", new[] { "PER_SAMPLE" });

        private int _noiseTexture;
        private FrameBuffer _terrainHeightBuffer;
        private FrameBuffer _marchBuffer;
        private FrameBuffer[] _historyBuffers = new FrameBuffer[2];
        private int _historyIndex;
        private bool _historyValid;
        private Matrix4 _previousViewProjection;
        private int _frameIndex;

        private readonly List<DirectionallightRenderCommand> _directionalLights = new();
        private readonly List<PointlightRenderCommand> _pointLights = new();
        private readonly List<SpotlightRenderCommand> _spotLights = new();
        private readonly List<(float distance, int index)> _lightCandidates = new();

        /// <summary>The tiling noise fog and clouds are made of: r billows, g and b detail, a broad swells.</summary>
        public int NoiseTexture => _noiseTexture;

        /// <summary>The haze laid over the scene this frame, extinction per metre, for what's drawn over it afterwards and has to be hazed as much: particles.</summary>
        public float HazeDensity { get; private set; }

        public VolumetricFogRenderer() : base("Volumetric Fog")
        {
            // Timed in parts, which can't nest inside a timer of the whole.
            trackPerformance = false;

            _marchShader.bind();
            _marchShader.loadUniformInt("gDepth", 0);
            _marchShader.loadUniformInt("noiseTexture", 1);
            _marchShader.loadUniformInt("terrainHeightMap", 2);
            for (int i = 0; i < MaxCascadeSlots; i++) _marchShader.loadUniformInt("cascadeShadowMaps[" + i + "]", CascadeUnitStart + i);
            for (int i = 0; i < MaxSpotShadows; i++) _marchShader.loadUniformInt("spotShadowMaps[" + i + "]", SpotShadowUnitStart + i);
            for (int i = 0; i < MaxPointShadows; i++) _marchShader.loadUniformInt("pointShadowMaps[" + i + "]", PointShadowUnitStart + i);
            _marchShader.loadUniformFloat("pointShadowNear", PointLightRenderer.ShadowNearPlane);
            _marchShader.loadUniformFloat("pointShadowFar", PointLightRenderer.ShadowFarPlane);
            _marchShader.loadUniformInt("windMap", WindMapUnit);
            _marchShader.unBind();

            _temporalShader.bind();
            _temporalShader.loadUniformInt("currentFog", 0);
            _temporalShader.loadUniformInt("currentDistance", 1);
            _temporalShader.loadUniformInt("historyFog", 2);
            _temporalShader.unBind();

            foreach (ShaderProgram shader in new[] { _compositeShader, _compositeSampleShader })
            {
                shader.bind();
                shader.loadUniformInt("gDepth", 1);
                shader.loadUniformInt("fogTexture", 2);
                shader.loadUniformInt("fogDistance", 3);
                shader.unBind();
            }

            FrameBufferSettings terrainSettings = new FrameBufferSettings(new Vector2i(TerrainHeightResolution));
            terrainSettings.depthAttachmentSettings = new DepthAttachmentSettings { isTexture = true };
            _terrainHeightBuffer = new FrameBuffer(terrainSettings);

            Vector2i halfResolution = HalfResolution(Engine.Resolution);

            FrameBufferSettings marchSettings = new FrameBufferSettings(halfResolution);
            marchSettings.drawBuffers.Add(FogDrawBuffer(FramebufferAttachment.ColorAttachment0));
            DrawBufferSettings distance = new DrawBufferSettings(FramebufferAttachment.ColorAttachment1)
            {
                formatInternal = PixelInternalFormat.R32f,
                formatExternal = PixelFormat.Red,
                pixelType = PixelType.Float,
                minFilterType = TextureMinFilter.Nearest,
                magFilterType = TextureMagFilter.Nearest,
                wrapMode = TextureWrapMode.ClampToEdge,
            };
            marchSettings.drawBuffers.Add(distance);
            _marchBuffer = new FrameBuffer(marchSettings);

            for (int i = 0; i < _historyBuffers.Length; i++)
            {
                FrameBufferSettings historySettings = new FrameBufferSettings(halfResolution);
                historySettings.drawBuffers.Add(FogDrawBuffer(FramebufferAttachment.ColorAttachment0));
                _historyBuffers[i] = new FrameBuffer(historySettings);
            }
            _marchBuffer.unbind();

            GenerateNoiseTexture();
        }

        private static DrawBufferSettings FogDrawBuffer(FramebufferAttachment attachment)
        {
            return new DrawBufferSettings(attachment)
            {
                formatInternal = PixelInternalFormat.Rgba16f,
                pixelType = PixelType.Float,
                wrapMode = TextureWrapMode.ClampToEdge,
            };
        }

        private static string[] UniformNames(string array, int length)
        {
            string[] names = new string[length];
            for (int i = 0; i < length; i++) names[i] = array + "[" + i + "]";
            return names;
        }

        public void SubmitDirectionalLight(DirectionallightRenderCommand light) => _directionalLights.Add(light);
        public void SubmitPointLight(PointlightRenderCommand light) => _pointLights.Add(light);
        public void SubmitSpotLight(SpotlightRenderCommand light) => _spotLights.Add(light);

        private static Vector2i HalfResolution(Vector2i resolution)
        {
            return new Vector2i(Math.Max((resolution.X + 1) / 2, 1), Math.Max((resolution.Y + 1) / 2, 1));
        }

        /// <summary>Bakes the tileable 3D noise once, one z-slice per draw.</summary>
        private void GenerateNoiseTexture()
        {
            _noiseTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture3D, _noiseTexture);
            GL.TexImage3D(TextureTarget.Texture3D, 0, PixelInternalFormat.Rgba8, NoiseResolution, NoiseResolution, NoiseResolution, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapR, (int)TextureWrapMode.Repeat);

            int frameBuffer = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBuffer);
            GL.Viewport(0, 0, NoiseResolution, NoiseResolution);
            _noiseShader.bind();
            for (int slice = 0; slice < NoiseResolution; slice++)
            {
                GL.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, _noiseTexture, 0, slice);
                GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
                _noiseShader.loadUniformFloat("slice", (slice + 0.5f) / NoiseResolution);
                Engine.RenderEngine.ScreenQuadRenderer.Render(clearColor: false);
            }
            _noiseShader.unBind();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DeleteFramebuffer(frameBuffer);
            Engine.WindowHandler.refreshViewport();

            GL.BindTexture(TextureTarget.Texture3D, _noiseTexture);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture3D);
            GL.BindTexture(TextureTarget.Texture3D, 0);
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {
            // No command: the fog is disabled this frame, so the history is stale by the time it comes back.
            if (commands.Count == 0)
            {
                _historyValid = false;
                HazeDensity = 0f;
                return;
            }

            // More than one command means frames were skipped (e.g. the F8 debug view bypasses post-processing).
            // Only the newest is current, and compositing every queued one would apply the fog several times.
            if (commands.Count > 1) _historyValid = false;
            PerformCommand(commands[commands.Count - 1], renderEngine);
            commands.Clear();
        }

        public override void Update()
        {
            // Runs before the systems submit this frame's lights, and also when the previous frame never reached the fog pass.
            _directionalLights.Clear();
            _pointLights.Clear();
            _spotLights.Clear();
        }

        public override void PerformCommand(VolumetricFogRenderCommand command, RenderEngine renderEngine)
        {
            VolumetricFogSingleton settings = command.settings;

            Engine.PerformanceMonitor.startGPUTask("Fog Ground Capture");
            bool terrainHeightValid = CaptureTerrainHeights(command, renderEngine, out Matrix4 terrainMatrix, out float terrainEyeY);
            Engine.PerformanceMonitor.finishGPUTask("Fog Ground Capture");
            Engine.PerformanceMonitor.startGPUTask("Fog March");
            FrameBuffer history = MarchAndAccumulate(command, renderEngine, terrainHeightValid, terrainMatrix, terrainEyeY);
            Engine.PerformanceMonitor.finishGPUTask("Fog March");
            Engine.PerformanceMonitor.startGPUTask("Fog Composite");
            Composite(settings, history, renderEngine);
            Engine.PerformanceMonitor.finishGPUTask("Fog Composite");

            _previousViewProjection = renderEngine.context.viewMatrix * renderEngine.context.projectionMatrix;
            _historyIndex = 1 - _historyIndex;
            _historyValid = true;
            _frameIndex++;
        }

        private bool CaptureTerrainHeights(VolumetricFogRenderCommand command, RenderEngine renderEngine, out Matrix4 projectionView, out float eyeY)
        {
            float size = command.settings.TerrainCaptureSize;
            Vector3 center = command.terrainCaptureCenter;

            // Snap to whole texels so the captured heights don't shimmer as the camera moves.
            float texel = size / TerrainHeightResolution;
            center.X = MathF.Floor(center.X / texel) * texel;
            center.Z = MathF.Floor(center.Z / texel) * texel;

            eyeY = center.Y + TerrainDepthRange * 0.5f;
            Vector3 eye = new Vector3(center.X, eyeY, center.Z);
            Matrix4 view = Matrix4.LookAt(eye, eye - Vector3.UnitY, Vector3.UnitZ);
            Matrix4 projection = Matrix4.CreateOrthographic(size, size, 0f, TerrainDepthRange);
            projectionView = view * projection;

            if (command.settings.TerrainFollow <= 0f || command.terrainChunks == null || command.terrainChunks.Length == 0) return false;

            _terrainHeightBuffer.ClearDepth();
            renderEngine._terrainRenderer.RenderHeightCapture(command.terrainChunks, projectionView);
            // Fog lies on water, not on the bottom under it.
            renderEngine._waterRenderer.RenderHeightCapture(projectionView);
            return true;
        }

        private FrameBuffer MarchAndAccumulate(VolumetricFogRenderCommand command, RenderEngine renderEngine, bool terrainHeightValid, Matrix4 terrainMatrix, float terrainEyeY)
        {
            VolumetricFogSingleton settings = command.settings;

            // ---------------------------------------------------------------- march
            _marchBuffer.bind();
            _marchShader.bind();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine.GBuffer.getDepthAttachment());
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture3D, _noiseTexture);
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, _terrainHeightBuffer.getDepthAttachment());
            GL.ActiveTexture(TextureUnit.Texture0 + WindMapUnit);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine._windRenderer.WindMapTexture);

            UploadDirectionalLights(settings, command.sunThrough);
            UploadLocalLights(settings, renderEngine);

            _marchShader.loadUniformVector3f("scatteringAlbedo", settings.Albedo);
            _marchShader.loadUniformFloat("ambientScattering", settings.AmbientScattering);
            _marchShader.loadUniformFloat("anisotropy", Math.Clamp(settings.Anisotropy, -0.95f, 0.95f));

            _marchShader.loadUniformFloat("density", Math.Max(settings.Density, 0f));
            _marchShader.loadUniformFloat("layerHeight", Math.Max(settings.Height, 0.05f));
            _marchShader.loadUniformFloat("heightFalloff", Math.Max(settings.HeightFalloff, 0.01f));
            _marchShader.loadUniformFloat("terrainFollow", Math.Clamp(settings.TerrainFollow, 0f, 1f));
            _marchShader.loadUniformFloat("baseHeight", settings.BaseHeight);
            _marchShader.loadUniformFloat("coverage", Math.Clamp(settings.Coverage, 0f, 1f));
            _marchShader.loadUniformFloat("edgeSoftness", Math.Max(settings.EdgeSoftness, 0.01f));
            _marchShader.loadUniformFloat("clearingRadius", Math.Max(settings.ClearingRadius, 0.1f));
            _marchShader.loadUniformFloat("clearingDepth", Math.Clamp(settings.ClearingDepth, 0f, 1f));
            _marchShader.loadUniformFloat("pooling", Math.Clamp(settings.Pooling, 0f, 1f));
            _marchShader.loadUniformFloat("banks", Math.Clamp(settings.Banks, 0f, 1f));

            _marchShader.loadUniformFloat("invNoiseScale", 1f / Math.Max(settings.NoiseScale, 0.1f));
            _marchShader.loadUniformFloat("noiseTexelSize", 1f / NoiseResolution);
            _marchShader.loadUniformFloat("turbulence", settings.Turbulence);
            _marchShader.loadUniformFloat("detail", Math.Clamp(settings.Detail, 0f, 0.95f));
            _marchShader.loadUniformFloat("topVariation", Math.Clamp(settings.TopVariation, 0f, 1f));
            _marchShader.loadUniformFloat("gustResponse", Math.Max(settings.GustResponse, 0f));
            _marchShader.loadUniformVector3f("baseOffset", command.baseOffset);
            _marchShader.loadUniformVector3f("warpOffset", command.warpOffset);
            _marchShader.loadUniformVector3f("detailOffset", command.detailOffset);

            _marchShader.loadUniformInt("maxSteps", Math.Max(settings.MaxSteps, 4));
            _marchShader.loadUniformFloat("maxDistance", settings.MaxDistance);
            _marchShader.loadUniformInt("frameIndex", _frameIndex);

            // At a rate like this the fog settles about as fast with half the rays a frame as it does at 60 with all of them. Anything slower gets every ray it can. Read from the last whole second, as a rate that's changing every frame would have it flicker between the two.
            bool takeTurns = _historyValid && Engine.FramesLastSecond >= TilesRestFromFrameRate;
            int restingTiles = takeTurns ? _frameIndex & 1 : -1;
            _marchShader.loadUniformInt("restingTiles", restingTiles);

            _marchShader.loadUniformInt("terrainHeightValid", terrainHeightValid ? 1 : 0);
            _marchShader.loadUniformMatrix4f("terrainHeightMatrix", terrainMatrix);
            _marchShader.loadUniformFloat("terrainEyeY", terrainEyeY);
            _marchShader.loadUniformFloat("terrainDepthRange", TerrainDepthRange);

            renderEngine.ScreenQuadRenderer.Render(clearColor: false);

            // ---------------------------------------------------------------- temporal
            FrameBuffer history = _historyBuffers[_historyIndex];
            FrameBuffer previousHistory = _historyBuffers[1 - _historyIndex];

            history.bind();
            _temporalShader.bind();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _marchBuffer.GetAttachment(0));
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _marchBuffer.GetAttachment(1));
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, previousHistory.GetAttachment(0));

            _temporalShader.loadUniformMatrix4f("previousViewProjection", _previousViewProjection);
            _temporalShader.loadUniformFloat("historyWeight", _historyValid ? Math.Clamp(settings.TemporalBlend, 0f, 0.98f) : 0f);
            _temporalShader.loadUniformInt("restingTiles", restingTiles);

            renderEngine.ScreenQuadRenderer.Render(clearColor: false);
            return history;
        }

        private void UploadDirectionalLights(VolumetricFogSingleton settings, float sunThrough)
        {
            int count = 0;
            int cascadeSlot = 0;
            foreach (DirectionallightRenderCommand light in _directionalLights)
            {
                if (count == MaxDirectionalLights) break;
                Shadow[] cascades = light.cascades ?? Array.Empty<Shadow>();
                // A light with cascades is the sun, and has clouds to get past. Lightning is under them.
                Vector3 colour = light.colour * settings.DirectionalScattering * (cascades.Length > 0 ? sunThrough : 1f);
                if (colour == Vector3.Zero || !(light.direction.LengthSquared > 0f)) continue;

                // Cascades are sorted finest first. Lights past the free slots get fewer (or no) shadow cascades.
                int cascadeCount = Math.Min(cascades.Length, MaxCascadeSlots - cascadeSlot);
                _marchShader.loadUniformVector3f(DirectionalColourNames[count], colour);
                _marchShader.loadUniformVector3f(DirectionalDirectionNames[count], -light.direction.Normalized());
                _marchShader.loadUniformInt(DirectionalCascadeStartNames[count], cascadeSlot);
                _marchShader.loadUniformInt(DirectionalCascadeCountNames[count], cascadeCount);
                for (int c = 0; c < cascadeCount; c++)
                {
                    Shadow cascade = cascades[c];
                    GL.ActiveTexture(TextureUnit.Texture0 + CascadeUnitStart + cascadeSlot);
                    GL.BindTexture(TextureTarget.Texture2D, cascade.shadowFrameBuffer.getDepthAttachment());
                    _marchShader.loadUniformMatrix4f(CascadeMatrixNames[cascadeSlot], cascade.lightViewMatrix * cascade.shadowProjectionMatrix);
                    cascadeSlot++;
                }
                count++;
            }
            _marchShader.loadUniformInt("directionalLightCount", count);
        }

        private void UploadLocalLights(VolumetricFogSingleton settings, RenderEngine renderEngine)
        {
            Frustum frustum = new Frustum(renderEngine.context.viewMatrix * renderEngine.context.projectionMatrix);
            Vector3 cameraPosition = renderEngine.context.viewPos;
            float scattering = settings.LocalLightScattering;

            // ---------------------------------------------------------------- point lights
            _lightCandidates.Clear();
            for (int i = 0; i < _pointLights.Count; i++)
            {
                PointlightRenderCommand light = _pointLights[i];
                if (IsCandidate(light.positionWorld, light.radius, light.colour * scattering, frustum, cameraPosition, out float distance))
                {
                    _lightCandidates.Add((distance, i));
                }
            }
            _lightCandidates.Sort((a, b) => a.distance.CompareTo(b.distance));

            int pointCount = Math.Min(_lightCandidates.Count, MaxPointLights);
            int pointShadows = 0;
            for (int i = 0; i < pointCount; i++)
            {
                PointlightRenderCommand light = _pointLights[_lightCandidates[i].index];
                int shadowIndex = -1;
                if (light.shadow != null && pointShadows < MaxPointShadows)
                {
                    shadowIndex = pointShadows++;
                    GL.ActiveTexture(TextureUnit.Texture0 + PointShadowUnitStart + shadowIndex);
                    GL.BindTexture(TextureTarget.TextureCubeMap, light.shadow.Value.shadowFrameBuffer.getDepthAttachment());
                }
                _marchShader.loadUniformVector4f(PointPositionRadiusNames[i], new Vector4(light.positionWorld, light.radius));
                _marchShader.loadUniformVector4f(PointColourShadowNames[i], new Vector4(light.colour * scattering, shadowIndex));
            }
            _marchShader.loadUniformInt("pointLightCount", pointCount);

            // ---------------------------------------------------------------- spotlights
            _lightCandidates.Clear();
            for (int i = 0; i < _spotLights.Count; i++)
            {
                SpotlightRenderCommand light = _spotLights[i];
                if (SpotLightRenderer.WorldDirection(light).LengthSquared > 0f
                    && IsCandidate(light.positionWorld, light.radius, light.colour * scattering, frustum, cameraPosition, out float distance))
                {
                    _lightCandidates.Add((distance, i));
                }
            }
            _lightCandidates.Sort((a, b) => a.distance.CompareTo(b.distance));

            int spotCount = Math.Min(_lightCandidates.Count, MaxSpotLights);
            int spotShadows = 0;
            for (int i = 0; i < spotCount; i++)
            {
                SpotlightRenderCommand light = _spotLights[_lightCandidates[i].index];
                int shadowIndex = -1;
                if (light.shadow != null && spotShadows < MaxSpotShadows)
                {
                    shadowIndex = spotShadows++;
                    Shadow shadow = light.shadow.Value;
                    GL.ActiveTexture(TextureUnit.Texture0 + SpotShadowUnitStart + shadowIndex);
                    GL.BindTexture(TextureTarget.Texture2D, shadow.shadowFrameBuffer.getDepthAttachment());
                    _marchShader.loadUniformMatrix4f(SpotShadowMatrixNames[shadowIndex], shadow.lightViewMatrix * shadow.shadowProjectionMatrix);
                }
                Vector3 direction = SpotLightRenderer.WorldDirection(light).Normalized();
                _marchShader.loadUniformVector4f(SpotPositionRadiusNames[i], new Vector4(light.positionWorld, light.radius));
                _marchShader.loadUniformVector4f(SpotColourShadowNames[i], new Vector4(light.colour * scattering, shadowIndex));
                _marchShader.loadUniformVector4f(SpotDirectionCutoffNames[i], new Vector4(direction, light.cutoffCosine));
                _marchShader.loadUniformFloat(SpotSoftnessNames[i], light.softness);
            }
            _marchShader.loadUniformInt("spotLightCount", spotCount);
        }

        /// <summary>
        /// Whether a light with this sphere of influence can light visible fog. Candidates are ranked by how far the
        /// camera is from the sphere, nearest first, so the limited light slots go to the lights that matter most.
        /// </summary>
        private static bool IsCandidate(Vector3 position, float radius, Vector3 colour, Frustum frustum, Vector3 cameraPosition, out float distance)
        {
            distance = 0f;
            // No reach or no colour: nothing to add.
            if (!(radius > 0f) || colour == Vector3.Zero) return false;
            if (frustum.IntersectsAABB(new AABB(position - new Vector3(radius), position + new Vector3(radius))) == IntersectionResult.Outside) return false;
            distance = MathF.Max((position - cameraPosition).Length - radius, 0f);
            return true;
        }

        /// <summary>Fog and haze over the multisampled scene (with the water's depth), blended in per sample before the scene is resolved.</summary>
        private void Composite(VolumetricFogSingleton settings, FrameBuffer history, RenderEngine renderEngine)
        {
            MsaaRenderer msaa = renderEngine.Msaa;
            msaa.SceneBuffer.bind();
            GL.Enable(EnableCap.Blend);
            // scene * transmittance + inscattered light; the scene's alpha is left alone.
            GL.BlendFuncSeparate(BlendingFactorSrc.One, BlendingFactorDest.SrcAlpha, BlendingFactorSrc.Zero, BlendingFactorDest.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            MsaaRenderer.BindTexture(1, msaa.GeometryBuffer.getDepthAttachment());
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, history.GetAttachment(0));
            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2D, _marchBuffer.GetAttachment(1));
            GL.ActiveTexture(TextureUnit.Texture0);

            float hazeDensity = Math.Max(settings.HazeDensity, 0f);
            HazeDensity = hazeDensity;
            msaa.DrawSplit(_compositeShader, _compositeSampleShader, shader =>
            {
                shader.loadUniformFloat("hazeDensity", hazeDensity);
                renderEngine.ScreenQuadRenderer.Render(blend: true, clearColor: false);
            });
            GL.Disable(EnableCap.Blend);
            GL.UseProgram(0);
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
            Vector2i halfResolution = HalfResolution(eventArgs.Size);
            _marchBuffer.resize(halfResolution);
            foreach (FrameBuffer history in _historyBuffers) history.resize(halfResolution);
            _historyValid = false;
        }

        public override void CleanUp()
        {
            _noiseShader.cleanUp();
            _marchShader.cleanUp();
            _temporalShader.cleanUp();
            _compositeShader.cleanUp();
            _compositeSampleShader.cleanUp();
            GL.DeleteTexture(_noiseTexture);
            _terrainHeightBuffer.cleanUp();
            _marchBuffer.cleanUp();
            foreach (FrameBuffer history in _historyBuffers) history.cleanUp();
        }
    }
}
