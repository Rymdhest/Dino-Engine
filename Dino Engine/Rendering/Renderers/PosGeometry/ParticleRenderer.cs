using Dino_Engine.Core;
using Dino_Engine.Particles;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.PosGeometry
{
    /// <summary>
    /// Every particle there is, on the GPU (read PARTICLES.md). Each frame, over the lit scene after the fog and before the ground marks:
    /// 1. The particles born this frame go into the slots <see cref="ParticlePool"/> chose (Particles/Particle_Spawn.comp).
    /// 2. Every live particle moves on a step, gets a key by its distance and, if it's lit, the light from six ways about it (Particles/Particle_Simulate.comp).
    /// 3. The keys are sorted, farthest first (Particles/Particle_Sort.comp).
    /// 4. All of them are drawn in one call, blended premultiplied (Particles/Particle.vert and .frag).
    /// Nothing is read back: the CPU knows how many are alive from their lives alone. The flipbooks the flames and smoke are drawn from are grown at load by <see cref="ParticleBaker"/>.
    /// </summary>
    public class ParticleRenderer : RenderPassRenderer
    {
        private const int StyleVec4s = 6;
        private const int SortBlock = 512;
        private const int MostLights = 32;

        // Texture units of the compute pass, then of the draw.
        private const int WindUnit = 0;
        private const int CloudUnit = 1;
        private const int CascadeUnit = 2;
        private const int MostCascades = 5;
        private const int DepthUnit = 0;
        private const int PaletteUnit = 1;
        private const int FlameUnit = 2;
        private const int PuffAUnit = 3;
        private const int PuffBUnit = 4;
        private const int CurveUnit = 5;
        private const int NormalUnit = 6;

        // Shader storage bindings, as Particles/*.comp and Particle.vert declare them.
        private const int ParticleBinding = 0;
        private const int StyleBinding = 1;
        private const int KeyBinding = 2;
        private const int LightingBinding = 3;
        private const int SpawnBinding = 4;
        private const int SpawnSlotBinding = 5;

        public static readonly int CurveRows = ParticleStyles.Most * 2;

        private readonly ShaderProgram _spawnShader = new ShaderProgram("Particles/Particle_Spawn.comp");
        private readonly ShaderProgram _simulateShader = new ShaderProgram("Particles/Particle_Simulate.comp");
        private readonly ShaderProgram _sortShader = new ShaderProgram("Particles/Particle_Sort.comp");
        private readonly ShaderProgram _drawShader = new ShaderProgram("Particles/Particle.vert", "Particles/Particle.frag");

        private readonly int _particles;
        private readonly int _keys;
        private readonly int _lighting;
        private readonly int _spawns;
        private readonly int _spawnSlots;
        private readonly int _styles;
        private readonly int _curves;
        private readonly int _palettes;
        private readonly int _emptyVao;
        private readonly int _shadowSampler;
        private readonly ParticleBaker _baker;

        private int _stylesSent = -1;
        private readonly float[] _styleData = new float[ParticleStyles.Most * StyleVec4s * 4];
        private readonly float[] _curveRow = new float[Curve.Samples * 4];
        private float _turbulencePhase;

        private readonly List<PointlightRenderCommand> _lights = new();
        private readonly List<(float Distance, int Index)> _nearest = new();
        private readonly Vector4[] _lightPositions = new Vector4[MostLights];
        private readonly Vector4[] _lightColours = new Vector4[MostLights];

        /// <summary>How many particles were drawn last frame, for the F3 window.</summary>
        public int Drawn { get; private set; }

        public ParticleBaker Flipbooks => _baker;

        public ParticleRenderer() : base("Particles")
        {
            ParticlePool pool = Particles.Particles.Pool;
            _particles = CreateBuffer(pool.Capacity * GpuParticle.Bytes);
            _keys = CreateBuffer(pool.Capacity * 2 * sizeof(uint));
            _lighting = CreateBuffer(pool.Capacity * 3 * 4 * sizeof(uint));
            _spawns = CreateBuffer(pool.SpawnCapacity * GpuParticle.Bytes);
            _spawnSlots = CreateBuffer(pool.SpawnCapacity * sizeof(uint));
            _styles = CreateBuffer(_styleData.Length * sizeof(float));
            Wipe(_particles);
            _emptyVao = GL.GenVertexArray();

            _curves = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _curves);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, Curve.Samples, CurveRows, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            ClampLinear(TextureTarget.Texture2D);

            _palettes = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _palettes);
            float[] palette = new float[Palettes.Width * Palettes.Count * 4];
            for (int row = 0; row < Palettes.Count; row++)
            {
                for (int i = 0; i < Palettes.Width; i++)
                {
                    Vector3 light = Palettes.Sample((ParticlePalette)row, i / (float)(Palettes.Width - 1));
                    int at = (row * Palettes.Width + i) * 4;
                    palette[at] = light.X;
                    palette[at + 1] = light.Y;
                    palette[at + 2] = light.Z;
                    palette[at + 3] = 1f;
                }
            }
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, Palettes.Width, Palettes.Count, 0, PixelFormat.Rgba, PixelType.Float, palette);
            ClampLinear(TextureTarget.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            _shadowSampler = GL.GenSampler();
            GL.SamplerParameter(_shadowSampler, SamplerParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.SamplerParameter(_shadowSampler, SamplerParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.SamplerParameter(_shadowSampler, SamplerParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.SamplerParameter(_shadowSampler, SamplerParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.SamplerParameter(_shadowSampler, SamplerParameterName.TextureCompareMode, (int)All.CompareRefToTexture);
            GL.SamplerParameter(_shadowSampler, SamplerParameterName.TextureCompareFunc, (int)All.Lequal);

            _simulateShader.bind();
            _simulateShader.loadUniformInt("windMap", WindUnit);
            _simulateShader.loadUniformInt("cloudShadowMap", CloudUnit);
            for (int i = 0; i < MostCascades; i++) _simulateShader.loadUniformInt("cascadeMaps[" + i + "]", CascadeUnit + i);

            _drawShader.bind();
            _drawShader.loadUniformInt("gDepth", DepthUnit);
            _drawShader.loadUniformInt("palettes", PaletteUnit);
            _drawShader.loadUniformInt("flameBook", FlameUnit);
            _drawShader.loadUniformInt("puffBookA", PuffAUnit);
            _drawShader.loadUniformInt("puffBookB", PuffBUnit);
            _drawShader.loadUniformInt("particleCurves", CurveUnit);
            _drawShader.loadUniformInt("gNormal", NormalUnit);
            _drawShader.loadUniformFloat("curveRows", CurveRows);
            _drawShader.loadUniformFloat("paletteRows", Palettes.Count);
            GL.UseProgram(0);

            _baker = new ParticleBaker();
        }

        private static int CreateBuffer(int bytes)
        {
            int buffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, buffer);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, bytes, IntPtr.Zero, BufferUsageHint.DynamicCopy);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            return buffer;
        }

        private static void Wipe(int buffer)
        {
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, buffer);
            GL.ClearBufferData(BufferTarget.ShaderStorageBuffer, PixelInternalFormat.R32ui, PixelFormat.RedInteger, PixelType.UnsignedInt, IntPtr.Zero);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        private static void ClampLinear(TextureTarget target)
        {
            GL.TexParameter(target, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(target, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(target, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(target, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        }

        /// <summary>A local light that particles near it are lit by, as the light systems give it to the fog and the water.</summary>
        public void SubmitPointLight(PointlightRenderCommand light) => _lights.Add(light);

        public override void Update()
        {
            // Before the systems give this frame's lights.
            _lights.Clear();
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {
            ParticlePool pool = Particles.Particles.Pool;
            float step = Particles.Particles.Paused ? 0f : Math.Clamp(Engine.Delta, 0f, 0.1f);
            pool.Advance(step);
            if (pool.NeedsWipe)
            {
                Wipe(_particles);
                pool.Wiped();
            }

            SendStyles();
            Spawn(pool);

            Drawn = 0;
            if (pool.HighWater == 0) return;

            int sortCount = SortBlock;
            while (sortCount < pool.HighWater) sortCount <<= 1;

            // A full turn of the noise is a whole number of its periods, so wrapping moves nothing.
            _turbulencePhase = (_turbulencePhase + step * 0.35f) % 256f;
            Engine.PerformanceMonitor.Count("Particles", pool.Live);
            Simulate(renderEngine, pool.HighWater, sortCount, step);
            Sort(sortCount);
            Draw(renderEngine, pool.Live);
            Drawn = pool.Live;
        }

        private void SendStyles()
        {
            if (_stylesSent == ParticleStyles.Version) return;
            _stylesSent = ParticleStyles.Version;

            IReadOnlyList<ParticleStyle> styles = ParticleStyles.All;
            GL.BindTexture(TextureTarget.Texture2D, _curves);
            for (int i = 0; i < styles.Count; i++)
            {
                ParticleStyle style = styles[i];
                int at = i * StyleVec4s * 4;
                Put(at, style.Drag, style.Gravity, style.Buoyancy, float.IsFinite(style.BuoyancyFade) && style.BuoyancyFade > 0f ? 1f / style.BuoyancyFade : 0f);
                Put(at + 4, style.Turbulence, 1f / MathF.Max(style.TurbulenceScale, 0.01f), style.Wind, style.SpinDrag);
                Put(at + 8, (float)style.Contact, style.Bounce, style.Friction, style.Spread);
                Put(at + 12, (float)style.Sprite, (float)style.Facing, (float)style.Palette, style.Lit ? 1f : 0f);
                Put(at + 16, style.Softness, style.Stretch, style.Cover, style.Stand);
                Put(at + 20, style.Sprite == ParticleSprite.Flame ? (float)style.Flame : (float)style.Pattern, style.FrameRate, style.Flicker, style.Toward);

                for (int s = 0; s < Curve.Samples; s++)
                {
                    float t = s / (float)(Curve.Samples - 1);
                    Vector3 colour = style.Colour[t];
                    _curveRow[s * 4] = colour.X;
                    _curveRow[s * 4 + 1] = colour.Y;
                    _curveRow[s * 4 + 2] = colour.Z;
                    _curveRow[s * 4 + 3] = style.Opacity[t];
                }
                GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, i * 2, Curve.Samples, 1, PixelFormat.Rgba, PixelType.Float, _curveRow);
                Curve emission = style.Emission ?? (style.Lit ? 0f : 1f);
                for (int s = 0; s < Curve.Samples; s++)
                {
                    float t = s / (float)(Curve.Samples - 1);
                    _curveRow[s * 4] = style.Size[t];
                    _curveRow[s * 4 + 1] = emission[t];
                    _curveRow[s * 4 + 2] = style.Heat[t];
                    _curveRow[s * 4 + 3] = style.Erosion[t];
                }
                GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, i * 2 + 1, Curve.Samples, 1, PixelFormat.Rgba, PixelType.Float, _curveRow);
            }
            GL.BindTexture(TextureTarget.Texture2D, 0);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _styles);
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, styles.Count * StyleVec4s * 4 * sizeof(float), _styleData);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        private void Put(int at, float x, float y, float z, float w)
        {
            _styleData[at] = x;
            _styleData[at + 1] = y;
            _styleData[at + 2] = z;
            _styleData[at + 3] = w;
        }

        private void Spawn(ParticlePool pool)
        {
            int count = pool.SpawnCount;
            if (count == 0) return;

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _spawns);
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, count * GpuParticle.Bytes, pool.Spawns);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _spawnSlots);
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, count * sizeof(int), pool.SpawnSlots);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            pool.Sent();

            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, ParticleBinding, _particles);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, SpawnBinding, _spawns);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, SpawnSlotBinding, _spawnSlots);
            _spawnShader.bind();
            _spawnShader.loadUniformInt("spawnCount", count);
            GL.DispatchCompute((count + 63) / 64, 1, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
        }

        private void Simulate(RenderEngine renderEngine, int highWater, int sortCount, float step)
        {
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, ParticleBinding, _particles);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, StyleBinding, _styles);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, KeyBinding, _keys);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, LightingBinding, _lighting);

            _simulateShader.bind();
            _simulateShader.loadUniformInt("highWater", highWater);
            _simulateShader.loadUniformInt("sortCount", sortCount);
            _simulateShader.loadUniformFloat("deltaTime", step);
            _simulateShader.loadUniformFloat("turbulencePhase", _turbulencePhase);

            GL.ActiveTexture(TextureUnit.Texture0 + WindUnit);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine._windRenderer.WindMapTexture);
            GL.ActiveTexture(TextureUnit.Texture0 + CloudUnit);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine._weatherRenderer.CloudShadowTexture);

            LightingSettings lighting = renderEngine.Lighting;
            _simulateShader.loadUniformVector3f("skyLight", renderEngine._ambientLightRenderer.SkyLight);
            _simulateShader.loadUniformVector3f("groundLight", renderEngine._ambientLightRenderer.GroundLight);

            // The sun, as the ambient pass drew it, with its cascades to read shadows from.
            int cascades = 0;
            if (renderEngine._directionalLightRenderer.Sun is DirectionallightRenderCommand sun && sun.colour != Vector3.Zero)
            {
                _simulateShader.loadUniformVector3f("sunTowards", -sun.direction.Normalized());
                _simulateShader.loadUniformVector3f("sunIrradiance", sun.colour);
                cascades = Math.Min(sun.cascades.Length, MostCascades);
                for (int i = 0; i < cascades; i++)
                {
                    Shadow cascade = sun.cascades[i];
                    GL.ActiveTexture(TextureUnit.Texture0 + CascadeUnit + i);
                    GL.BindTexture(TextureTarget.Texture2D, cascade.shadowFrameBuffer.getDepthAttachment());
                    GL.BindSampler(CascadeUnit + i, _shadowSampler);
                    _simulateShader.loadUniformMatrix4f("cascadeMatrices[" + i + "]", cascade.lightViewMatrix * cascade.shadowProjectionMatrix);
                }
            }
            else
            {
                _simulateShader.loadUniformVector3f("sunTowards", Vector3.UnitY);
                _simulateShader.loadUniformVector3f("sunIrradiance", Vector3.Zero);
            }
            _simulateShader.loadUniformInt("cascadeCount", cascades);

            int lights = lighting.LocalLights ? GatherLights(renderEngine) : 0;
            _simulateShader.loadUniformInt("lightCount", lights);
            if (lights > 0)
            {
                _simulateShader.loadUniformVector4fArray("lightPositionRadius", _lightPositions);
                _simulateShader.loadUniformVector4fArray("lightColourFill", _lightColours);
            }

            GL.DispatchCompute(sortCount / 256, 1, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

            for (int i = 0; i < cascades; i++) GL.BindSampler(CascadeUnit + i, 0);
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        // The local lights nearest the hero that can light anything in view, as many as there's room for.
        private int GatherLights(RenderEngine renderEngine)
        {
            Frustum view = new Frustum(renderEngine.context.viewMatrix * renderEngine.context.projectionMatrix);
            Vector3 focus = renderEngine.context.focusPoint.W > 0f ? renderEngine.context.focusPoint.Xyz : renderEngine.context.viewPos;
            _nearest.Clear();
            for (int i = 0; i < _lights.Count; i++)
            {
                PointlightRenderCommand light = _lights[i];
                if (!(light.radius > 0f) || light.colour == Vector3.Zero || !view.IntersectsSphere(light.positionWorld, light.radius)) continue;
                _nearest.Add((MathF.Max((light.positionWorld - focus).Length - light.radius, 0f), i));
            }
            _nearest.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            int count = Math.Min(_nearest.Count, MostLights);
            for (int i = 0; i < MostLights; i++)
            {
                if (i >= count)
                {
                    _lightPositions[i] = Vector4.Zero;
                    _lightColours[i] = Vector4.Zero;
                    continue;
                }
                PointlightRenderCommand light = _lights[_nearest[i].Index];
                _lightPositions[i] = new Vector4(light.positionWorld, light.radius);
                _lightColours[i] = new Vector4(light.colour, light.fill);
            }
            return count;
        }

        private void Sort(int count)
        {
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, KeyBinding, _keys);
            _sortShader.bind();
            int groups = count / SortBlock;

            _sortShader.loadUniformInt("sortMode", 0);
            GL.DispatchCompute(groups, 1, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

            for (int k = SortBlock * 2; k <= count; k <<= 1)
            {
                _sortShader.loadUniformInt("blockSize", k);
                _sortShader.loadUniformInt("sortMode", 1);
                for (int j = k >> 1; j >= SortBlock; j >>= 1)
                {
                    _sortShader.loadUniformInt("stride", j);
                    GL.DispatchCompute(groups, 1, 1);
                    GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
                }
                _sortShader.loadUniformInt("sortMode", 2);
                GL.DispatchCompute(groups, 1, 1);
                GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
            }
        }

        private void Draw(RenderEngine renderEngine, int live)
        {
            renderEngine.Msaa.SceneBuffer.bind();
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.DepthMask(false);
            GL.Disable(EnableCap.StencilTest);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.Blend);
            GL.BlendFuncSeparate(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.Zero, BlendingFactorDest.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, ParticleBinding, _particles);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, StyleBinding, _styles);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, KeyBinding, _keys);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, LightingBinding, _lighting);

            Bind(DepthUnit, TextureTarget.Texture2D, renderEngine.GBuffer.getDepthAttachment());
            Bind(PaletteUnit, TextureTarget.Texture2D, _palettes);
            Bind(FlameUnit, TextureTarget.Texture2DArray, _baker.FlameBook);
            Bind(PuffAUnit, TextureTarget.Texture2DArray, _baker.PuffBookA);
            Bind(PuffBUnit, TextureTarget.Texture2DArray, _baker.PuffBookB);
            Bind(CurveUnit, TextureTarget.Texture2D, _curves);
            Bind(NormalUnit, TextureTarget.Texture2D, renderEngine.GBuffer.GetAttachment(1));
            GL.ActiveTexture(TextureUnit.Texture0);

            LightingSettings lighting = renderEngine.Lighting;
            _drawShader.bind();
            _drawShader.loadUniformFloat("emissionScale", lighting.Emission ? lighting.EmissionScale : 0f);
            _drawShader.loadUniformFloat("particleTime", Engine.Time % 3600f);
            _drawShader.loadUniformFloat("hazeDensity", renderEngine._volumetricFogRenderer.HazeDensity);
            _drawShader.loadUniformVector3f("skyLight", renderEngine._ambientLightRenderer.SkyLight);
            _drawShader.loadUniformVector3f("groundLight", renderEngine._ambientLightRenderer.GroundLight);
            if (renderEngine._directionalLightRenderer.Sun is DirectionallightRenderCommand sun)
            {
                _drawShader.loadUniformVector3f("sunTowards", -sun.direction.Normalized());
                _drawShader.loadUniformVector3f("sunIrradiance", sun.colour);
            }
            else
            {
                _drawShader.loadUniformVector3f("sunTowards", Vector3.UnitY);
                _drawShader.loadUniformVector3f("sunIrradiance", Vector3.Zero);
            }

            // What lies on the ground first, sorted before everything else, and with no depth test: it's painted on the scene under it, and leaves alone anything standing
            // there by how high off the ground that is (Particle.frag), so that grass doesn't hide a burning patch. Then everything else, tested against the scene.
            GL.BindVertexArray(_emptyVao);
            GL.Disable(EnableCap.DepthTest);
            _drawShader.loadUniformInt("drawPass", 0);
            GL.DrawArraysInstanced(PrimitiveType.TriangleStrip, 0, 4, live);
            GL.Enable(EnableCap.DepthTest);
            _drawShader.loadUniformInt("drawPass", 1);
            GL.DrawArraysInstanced(PrimitiveType.TriangleStrip, 0, 4, live);
            GL.BindVertexArray(0);
            GL.UseProgram(0);

            GL.Disable(EnableCap.Blend);
            GL.DepthMask(true);
            GL.DepthFunc(DepthFunction.Less);
            GL.Enable(EnableCap.CullFace);
        }

        private static void Bind(int unit, TextureTarget target, int texture)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + unit);
            GL.BindTexture(target, texture);
        }

        public override void CleanUp()
        {
            _spawnShader.cleanUp();
            _simulateShader.cleanUp();
            _sortShader.cleanUp();
            _drawShader.cleanUp();
            foreach (int buffer in new[] { _particles, _keys, _lighting, _spawns, _spawnSlots, _styles }) GL.DeleteBuffer(buffer);
            GL.DeleteTexture(_curves);
            GL.DeleteTexture(_palettes);
            GL.DeleteVertexArray(_emptyVao);
            GL.DeleteSampler(_shadowSampler);
            _baker.CleanUp();
        }
    }
}
