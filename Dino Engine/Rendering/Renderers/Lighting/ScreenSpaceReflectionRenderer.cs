using Dino_Engine.Core;
using Dino_Engine.Rendering.Renderers.AntiAliasing;
using Dino_Engine.Rendering.Renderers.Simulation;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    /// <summary>
    /// All the mirror-like light in the scene, after Stachowiak's stochastic screen-space reflections and AMD's FidelityFX SSSR, run after the other lights:
    /// 1. A pyramid of the nearest depths (Reflection_HiZ.comp), which rays walk instead of marching in fixed steps.
    /// 2. A blurred copy of the lit scene, for the width a rough surface's reflection has at any distance.
    /// 3. One ray per pixel of a half-resolution grid (Reflection_Trace.frag), drawn from each surface's own GGX lobe. Mirrors shoot straight, and have no noise at all.
    /// 4. Each pixel using its neighbours' rays as well, weighed by its own lobe (Reflection_Resolve.frag).
    /// 5. A history that follows what's seen in the mirror, and can't leave ghosts (Reflection_Temporal.frag).
    /// 6. Added to the multisampled scene per sample, by what each material makes of it (Reflection_Composite.frag). This pass is the only place surfaces reflect their surroundings, so it's
    ///    also where what the rays couldn't find is made up, by the sky and the rain shadow. The sky, the ambient pass and the sun add nothing mirror-like but the sun's own highlight.
    /// </summary>
    internal class ScreenSpaceReflectionRenderer : RenderPassRenderer
    {
        private const int Downscale = 2;
        private const int SceneBlurLevels = 6;
        private const int MaxPyramidLevels = 8;

        // Texture units of the composite. Units 0..3 and 14 hold the G-buffer (MsaaRenderer.BindGeometrySamples).
        private const int ReflectionUnit = 4;
        private const int DistanceUnit = 5;
        private const int SkyMapUnit = 6;
        private const int RainShadowUnit = 7;

        private readonly ShaderProgram _pyramidShader = new ShaderProgram("Reflections/Reflection_HiZ.comp");
        private readonly ShaderProgram _traceShader = new ShaderProgram("Simple.vert", "Reflections/Reflection_Trace.frag");
        private readonly ShaderProgram _resolveShader = new ShaderProgram("Simple.vert", "Reflections/Reflection_Resolve.frag");
        private readonly ShaderProgram _temporalShader = new ShaderProgram("Simple.vert", "Reflections/Reflection_Temporal.frag");
        // Per pixel, and per sample for the edge pixels (MsaaRenderer.DrawSplit).
        private readonly ShaderProgram _compositeShader = new ShaderProgram("Simple.vert", "Reflections/Reflection_Composite.frag");
        private readonly ShaderProgram _compositeSampleShader = new ShaderProgram("Simple.vert", "Reflections/Reflection_Composite.frag", new[] { "PER_SAMPLE" });

        private readonly FrameBuffer _rays;
        private readonly FrameBuffer _resolved;
        private readonly FrameBuffer[] _history = new FrameBuffer[2];
        private readonly int _sceneBlurFramebuffer;
        private int _pyramid;
        private int _pyramidTop;
        private int _sceneBlur;
        private Vector2i _traceSize;
        private int _historyIndex;
        private bool _historyValid;
        private Matrix4 _previousViewProjection;
        private int _frame;

        public ScreenSpaceReflectionRenderer() : base("Reflections")
        {
            // Timed by its stages instead (GPU timers don't nest).
            trackPerformance = false;

            _pyramidShader.bind();
            _pyramidShader.loadUniformInt("source", 0);

            _traceShader.bind();
            _traceShader.loadUniformInt("depthPyramid", 0);
            _traceShader.loadUniformInt("gNormal", 1);
            _traceShader.loadUniformInt("gMaterials", 2);
            _traceShader.loadUniformInt("gAlbedo", 3);

            _resolveShader.bind();
            _resolveShader.loadUniformInt("rays", 0);
            _resolveShader.loadUniformInt("rayConfidence", 1);
            _resolveShader.loadUniformInt("sceneBlur", 2);
            _resolveShader.loadUniformInt("depthPyramid", 3);
            _resolveShader.loadUniformInt("gNormal", 4);
            _resolveShader.loadUniformInt("gMaterials", 5);
            _resolveShader.loadUniformInt("gAlbedo", 6);

            _temporalShader.bind();
            _temporalShader.loadUniformInt("current", 0);
            _temporalShader.loadUniformInt("previous", 1);
            _temporalShader.loadUniformInt("previousDistance", 2);
            _temporalShader.loadUniformInt("rays", 3);
            _temporalShader.loadUniformInt("depthPyramid", 4);
            _temporalShader.loadUniformInt("gMaterials", 5);

            foreach (ShaderProgram shader in new[] { _compositeShader, _compositeSampleShader })
            {
                shader.bind();
                shader.loadUniformInt("gAlbedo", 0);
                shader.loadUniformInt("gNormal", 1);
                shader.loadUniformInt("gMaterials", 2);
                shader.loadUniformInt("gDepth", 3);
                shader.loadUniformInt("ambientOcclusion", MsaaRenderer.AmbientOcclusionUnit);
                shader.loadUniformInt("reflectionTexture", ReflectionUnit);
                shader.loadUniformInt("reflectionDistance", DistanceUnit);
                shader.loadUniformInt("skyMap", SkyMapUnit);
                shader.loadUniformInt("rainShadowMap", RainShadowUnit);
            }
            _compositeShader.unBind();

            _traceSize = TraceSize(Engine.Resolution);

            FrameBufferSettings raySettings = new FrameBufferSettings(_traceSize);
            raySettings.drawBuffers.Add(Attachment(FramebufferAttachment.ColorAttachment0, PixelInternalFormat.Rgba32f, PixelFormat.Rgba, TextureMinFilter.Nearest));
            raySettings.drawBuffers.Add(Attachment(FramebufferAttachment.ColorAttachment1, PixelInternalFormat.R8, PixelFormat.Red, TextureMinFilter.Nearest));
            _rays = new FrameBuffer(raySettings);

            FrameBufferSettings resolveSettings = new FrameBufferSettings(_traceSize);
            resolveSettings.drawBuffers.Add(Attachment(FramebufferAttachment.ColorAttachment0, PixelInternalFormat.Rgba16f, PixelFormat.Rgba, TextureMinFilter.Nearest));
            _resolved = new FrameBuffer(resolveSettings);

            for (int i = 0; i < _history.Length; i++)
            {
                FrameBufferSettings historySettings = new FrameBufferSettings(_traceSize);
                historySettings.drawBuffers.Add(Attachment(FramebufferAttachment.ColorAttachment0, PixelInternalFormat.Rgba16f, PixelFormat.Rgba, TextureMinFilter.Linear));
                historySettings.drawBuffers.Add(Attachment(FramebufferAttachment.ColorAttachment1, PixelInternalFormat.R32f, PixelFormat.Red, TextureMinFilter.Nearest));
                _history[i] = new FrameBuffer(historySettings);
            }
            _history[0].unbind();

            _sceneBlurFramebuffer = GL.GenFramebuffer();
            CreateTextures(Engine.Resolution);
        }

        private static DrawBufferSettings Attachment(FramebufferAttachment attachment, PixelInternalFormat format, PixelFormat external, TextureMinFilter filter)
        {
            return new DrawBufferSettings(attachment)
            {
                formatInternal = format,
                formatExternal = external,
                pixelType = format == PixelInternalFormat.R8 ? PixelType.UnsignedByte : PixelType.Float,
                wrapMode = TextureWrapMode.ClampToEdge,
                minFilterType = filter,
                magFilterType = filter == TextureMinFilter.Nearest ? TextureMagFilter.Nearest : TextureMagFilter.Linear,
            };
        }

        private static Vector2i TraceSize(Vector2i resolution)
        {
            return new Vector2i(Math.Max((resolution.X + Downscale - 1) / Downscale, 1), Math.Max((resolution.Y + Downscale - 1) / Downscale, 1));
        }

        private void CreateTextures(Vector2i resolution)
        {
            if (_pyramid != 0) GL.DeleteTexture(_pyramid);
            if (_sceneBlur != 0) GL.DeleteTexture(_sceneBlur);

            int largest = Math.Max(resolution.X, resolution.Y);
            int levels = Math.Min(MaxPyramidLevels, 1 + (int)MathF.Floor(MathF.Log2(Math.Max(largest, 1))));
            _pyramid = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _pyramid);
            GL.TexStorage2D(TextureTarget2d.Texture2D, levels, SizedInternalFormat.R32f, resolution.X, resolution.Y);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.NearestMipmapNearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            _pyramidTop = levels - 1;

            _sceneBlur = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _sceneBlur);
            GL.TexStorage2D(TextureTarget2d.Texture2D, SceneBlurLevels, SizedInternalFormat.Rgba16f, _traceSize.X, _traceSize.Y);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _sceneBlurFramebuffer);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _sceneBlur, 0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        /// <summary>The lit scene as a soft mip chain, for what wants to know roughly how bright things are around a point of the screen. Fresh from this frame's lighting on.</summary>
        public int SceneBlurTexture => _sceneBlur;

        internal override void Prepare(RenderEngine renderEngine)
        {
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {
            ReflectionSettings settings = renderEngine.Reflections;
            bool rays = settings.ScreenRays;

            Engine.PerformanceMonitor.startGPUTask("Reflection Inputs");
            CopyScene(renderEngine);
            if (rays) BuildPyramid(renderEngine, Math.Clamp(settings.PyramidLevels - 1, 1, _pyramidTop));
            Engine.PerformanceMonitor.finishGPUTask("Reflection Inputs");

            if (rays)
            {
                Engine.PerformanceMonitor.startGPUTask("Reflection Rays");
                Trace(renderEngine, settings);
                Resolve(renderEngine);
                Accumulate(renderEngine, settings);
                Engine.PerformanceMonitor.finishGPUTask("Reflection Rays");
            }
            else
            {
                _historyValid = false;
            }

            Engine.PerformanceMonitor.startGPUTask("Reflection Composite");
            Composite(renderEngine, rays);
            Engine.PerformanceMonitor.finishGPUTask("Reflection Composite");
            _frame++;
        }

        // The rays look at the lit scene averaged to one colour per pixel, before anything mirror-like has been added to it.
        private void CopyScene(RenderEngine renderEngine)
        {
            FrameBuffer shaded = renderEngine.lastUsedBuffer.GetNextFrameBuffer();
            Vector2i resolution = shaded.getResolution();
            renderEngine.Msaa.CopySceneColour(shaded, new Box2i(Vector2i.Zero, resolution));

            shaded.bindRead();
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _sceneBlurFramebuffer);
            GL.BlitFramebuffer(0, 0, resolution.X, resolution.Y, 0, 0, _traceSize.X, _traceSize.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            GL.BindTexture(TextureTarget.Texture2D, _sceneBlur);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        private void BuildPyramid(RenderEngine renderEngine, int top)
        {
            _pyramidShader.bind();
            GL.ActiveTexture(TextureUnit.Texture0);

            Vector2i size = Engine.Resolution;
            GL.BindTexture(TextureTarget.Texture2D, renderEngine.GBuffer.getDepthAttachment());
            _pyramidShader.loadUniformInt("sourceLevel", -1);
            DispatchLevel(0, size);

            GL.BindTexture(TextureTarget.Texture2D, _pyramid);
            for (int level = 1; level <= top; level++)
            {
                size = new Vector2i(Math.Max(size.X / 2, 1), Math.Max(size.Y / 2, 1));
                _pyramidShader.loadUniformInt("sourceLevel", level - 1);
                DispatchLevel(level, size);
            }
            GL.BindTexture(TextureTarget.Texture2D, 0);
            _pyramidShader.unBind();
        }

        private void DispatchLevel(int level, Vector2i size)
        {
            GL.BindImageTexture(0, _pyramid, level, false, 0, TextureAccess.WriteOnly, SizedInternalFormat.R32f);
            GL.DispatchCompute((size.X + 7) / 8, (size.Y + 7) / 8, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.TextureFetchBarrierBit | MemoryBarrierFlags.ShaderImageAccessBarrierBit);
        }

        private void Trace(RenderEngine renderEngine, ReflectionSettings settings)
        {
            FrameBuffer gBuffer = renderEngine.GBuffer;
            _rays.bind();
            _traceShader.bind();
            Bind(0, _pyramid);
            Bind(1, gBuffer.GetAttachment(1));
            Bind(2, gBuffer.GetAttachment(2));
            Bind(3, gBuffer.GetAttachment(0));
            _traceShader.loadUniformInt("frameIndex", _frame);
            _traceShader.loadUniformInt("maxSteps", settings.MaxSteps);
            _traceShader.loadUniformInt("topLevel", Math.Clamp(settings.PyramidLevels - 1, 1, _pyramidTop));
            _traceShader.loadUniformFloat("thicknessBase", settings.ThicknessBase);
            _traceShader.loadUniformFloat("thicknessPerMetre", settings.ThicknessPerMetre);
            renderEngine.ScreenQuadRenderer.Render(clearColor: false);
        }

        private void Resolve(RenderEngine renderEngine)
        {
            FrameBuffer gBuffer = renderEngine.GBuffer;
            _resolved.bind();
            _resolveShader.bind();
            Bind(0, _rays.GetAttachment(0));
            Bind(1, _rays.GetAttachment(1));
            Bind(2, _sceneBlur);
            Bind(3, _pyramid);
            Bind(4, gBuffer.GetAttachment(1));
            Bind(5, gBuffer.GetAttachment(2));
            Bind(6, gBuffer.GetAttachment(0));
            _resolveShader.loadUniformInt("frameIndex", _frame);
            renderEngine.ScreenQuadRenderer.Render(clearColor: false);
        }

        private void Accumulate(RenderEngine renderEngine, ReflectionSettings settings)
        {
            FrameBuffer target = _history[_historyIndex];
            FrameBuffer previous = _history[1 - _historyIndex];

            target.bind();
            _temporalShader.bind();
            Bind(0, _resolved.GetAttachment(0));
            Bind(1, previous.GetAttachment(0));
            Bind(2, previous.GetAttachment(1));
            Bind(3, _rays.GetAttachment(0));
            Bind(4, _pyramid);
            Bind(5, renderEngine.GBuffer.GetAttachment(2));
            _temporalShader.loadUniformMatrix4f("previousViewProjection", _previousViewProjection);
            _temporalShader.loadUniformFloat("historyWeight", _historyValid ? Math.Clamp(settings.History, 0f, 0.98f) : 0f);
            renderEngine.ScreenQuadRenderer.Render(clearColor: false);

            _previousViewProjection = renderEngine.context.viewMatrix * renderEngine.context.projectionMatrix;
            _historyValid = true;
        }

        private void Composite(RenderEngine renderEngine, bool rays)
        {
            MsaaRenderer msaa = renderEngine.Msaa;
            WeatherRenderer weather = renderEngine._weatherRenderer;
            FrameBuffer history = _history[_historyIndex];

            msaa.SceneBuffer.bind();
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, renderEngine.Reflections.Debug != 0 ? BlendingFactor.Zero : BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            msaa.BindGeometrySamples(renderEngine.AmbientOcclusionTexture);
            Bind(ReflectionUnit, history.GetAttachment(0));
            Bind(DistanceUnit, history.GetAttachment(1));
            Bind(SkyMapUnit, weather.SkyMapTexture);
            Bind(RainShadowUnit, weather.RainShadowTexture);
            GL.ActiveTexture(TextureUnit.Texture0);

            msaa.DrawSplit(_compositeShader, _compositeSampleShader, shader =>
            {
                shader.loadUniformInt("screenReflections", rays ? 1 : 0);
                shader.loadUniformInt("debugView", renderEngine.Reflections.Debug);
                shader.loadUniformVector3f("groundLight", renderEngine._ambientLightRenderer.GroundLight);
                renderEngine.ScreenQuadRenderer.Render(blend: true, clearColor: false);
            });
            GL.Disable(EnableCap.Blend);
            GL.UseProgram(0);

            _historyIndex = 1 - _historyIndex;
        }

        private static void Bind(int unit, int texture)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + unit);
            GL.BindTexture(TextureTarget.Texture2D, texture);
        }

        public override void CleanUp()
        {
            _pyramidShader.cleanUp();
            _traceShader.cleanUp();
            _resolveShader.cleanUp();
            _temporalShader.cleanUp();
            _compositeShader.cleanUp();
            _compositeSampleShader.cleanUp();
            _rays.cleanUp();
            _resolved.cleanUp();
            foreach (FrameBuffer buffer in _history) buffer.cleanUp();
            GL.DeleteFramebuffer(_sceneBlurFramebuffer);
            GL.DeleteTexture(_pyramid);
            GL.DeleteTexture(_sceneBlur);
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
            _traceSize = TraceSize(eventArgs.Size);
            _rays.resize(_traceSize);
            _resolved.resize(_traceSize);
            foreach (FrameBuffer buffer in _history) buffer.resize(_traceSize);
            CreateTextures(eventArgs.Size);
            _historyValid = false;
        }
    }
}
