using Dino_Engine.Core;
using Dino_Engine.Rendering.Renderers.PostProcessing;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

namespace Dino_Engine.Rendering.Renderers.AntiAliasing
{
    /// <summary>
    /// Multisample anti-aliasing for the deferred pipeline. Geometry renders into a multisampled G-buffer
    /// (<see cref="GeometryBuffer"/>); lighting, sky, particles, water and the fog composite render into a multisampled
    /// HDR scene buffer (<see cref="SceneBuffer"/>) that shares its depth and stencil. Each frame:
    /// 1. <see cref="ResolveGeometry"/> writes the single-sample G-buffer (<see cref="RenderEngine.GBuffer"/>) and marks the
    ///    edge pixels, whose samples are on different things, in the stencil buffer. Everywhere else the samples can be lit
    ///    as one, and it holds their average; on edge pixels their farthest sample, for the screen-space passes.
    /// 2. Passes that shade from the G-buffer are built twice and drawn with <see cref="DrawSplit"/>: per pixel where a
    ///    pixel's samples agree, per sample on the edge pixels, so every surface in an edge pixel is lit on its own.
    ///    Forward passes (sky, particles, water) draw straight into the scene buffer, where the hardware's per-sample depth
    ///    test anti-aliases their edges.
    /// 3. <see cref="ResolveScene"/> averages the samples into the HDR dual buffer for post-processing, weighted by how
    ///    bright they will look once tone mapped.
    /// Shaders read the sample count and positions from the MsaaData block (Shaders/AntiAliasing/Msaa.glsl).
    /// </summary>
    public class MsaaRenderer : BaseRenderer
    {
        /// <summary>Binding of the MsaaData uniform block (Shaders/AntiAliasing/Msaa.glsl).</summary>
        public const int UniformBinding = 8;
        /// <summary>Stencil bit set on the edge pixels, which <see cref="DrawSplit"/> shades per sample.</summary>
        public const int EdgeStencilBit = 1;
        /// <summary>Stencil bit set wherever a character is, for what goes on the ground under them.</summary>
        public const int CharacterStencilBit = 2;
        /// <summary>Colour attachment of the single-sample G-buffer holding the edge flags (1 on edge pixels).</summary>
        public const int EdgeAttachment = 3;
        /// <summary>Texture unit the light passes read the screen-space ambient occlusion from (Lighting/gBufferSample.glsl).</summary>
        public const int AmbientOcclusionUnit = 14;
        private const int MaxSamples = 8;

        private readonly ShaderProgram _resolveGeometryShader = new ShaderProgram("Simple.vert", "AntiAliasing/Msaa_Resolve_GBuffer.frag");
        private readonly ShaderProgram _markEdgesShader = new ShaderProgram("Simple.vert", "AntiAliasing/Msaa_Mark_Edges.frag");
        private readonly ShaderProgram _resolveDepthShader = new ShaderProgram("Simple.vert", "AntiAliasing/Msaa_Resolve_Depth.frag");
        private readonly ShaderProgram _resolveSceneShader = new ShaderProgram("Simple.vert", "AntiAliasing/Msaa_Resolve.frag");
        private readonly ShaderProgram _edgeOverlayShader = new ShaderProgram("Simple.vert", "AntiAliasing/Msaa_Edge_Overlay.frag");

        private readonly FrameBufferSettings _geometrySettings;
        private readonly FrameBufferSettings _sceneSettings;
        private readonly FrameBuffer _geometryBuffer;
        private readonly FrameBuffer _sceneBuffer;
        private readonly int _uniformBuffer;
        private readonly float[] _uniformData = new float[4 + 2 * MaxSamples];
        private readonly Vector2[] _samplePositions = new Vector2[MaxSamples];
        private readonly int _maxSamples;

        /// <summary>Samples per pixel of the multisampled buffers.</summary>
        public int Samples { get; private set; }
        /// <summary>The most samples per pixel this GPU supports here (at most 8).</summary>
        public int MaxSupportedSamples => _maxSamples;
        /// <summary>Multisampled G-buffer: albedo, normal and material attachments as the geometry shaders write them, and
        /// depth with the edge stencil.</summary>
        public FrameBuffer GeometryBuffer => _geometryBuffer;
        /// <summary>Multisampled HDR scene colour, sharing <see cref="GeometryBuffer"/>'s depth and stencil.</summary>
        public FrameBuffer SceneBuffer => _sceneBuffer;

        public MsaaRenderer(AntiAliasingSettings settings) : base("MSAA")
        {
            _resolveGeometryShader.bind();
            _resolveGeometryShader.loadUniformInt("gAlbedoSamples", 0);
            _resolveGeometryShader.loadUniformInt("gNormalSamples", 1);
            _resolveGeometryShader.loadUniformInt("gMaterialsSamples", 2);
            _resolveGeometryShader.loadUniformInt("gDepthSamples", 3);
            _markEdgesShader.bind();
            _markEdgesShader.loadUniformInt("edges", 0);
            _resolveDepthShader.bind();
            _resolveDepthShader.loadUniformInt("depthSamples", 0);
            _resolveSceneShader.bind();
            _resolveSceneShader.loadUniformInt("sceneSamples", 0);
            _edgeOverlayShader.bind();
            _edgeOverlayShader.loadUniformInt("edges", 0);
            _edgeOverlayShader.unBind();

            GL.GetInteger(GetPName.MaxColorTextureSamples, out int colourSamples);
            GL.GetInteger(GetPName.MaxDepthTextureSamples, out int depthSamples);
            GL.GetInteger(GetPName.MaxSamples, out int framebufferSamples);
            _maxSamples = Math.Clamp(Math.Min(colourSamples, Math.Min(depthSamples, framebufferSamples)), 1, MaxSamples);

            // Built with one sample here; Rebuild then makes them what the settings ask for.
            _geometrySettings = new FrameBufferSettings(Engine.Resolution) { samples = 1 };
            _geometrySettings.drawBuffers.Add(new DrawBufferSettings(FramebufferAttachment.ColorAttachment0) { formatInternal = PixelInternalFormat.Rgba8 });
            _geometrySettings.drawBuffers.Add(new DrawBufferSettings(FramebufferAttachment.ColorAttachment1) { formatInternal = PixelInternalFormat.Rgba8 });
            _geometrySettings.drawBuffers.Add(new DrawBufferSettings(FramebufferAttachment.ColorAttachment2) { formatInternal = PixelInternalFormat.Rgba8 });
            _geometrySettings.depthAttachmentSettings = new DepthAttachmentSettings { isTexture = true, stencil = true };
            _geometryBuffer = new FrameBuffer(_geometrySettings);

            _sceneSettings = new FrameBufferSettings(Engine.Resolution) { samples = 1, sharedDepth = _geometryBuffer };
            _sceneSettings.drawBuffers.Add(new DrawBufferSettings(FramebufferAttachment.ColorAttachment0) { formatInternal = PixelInternalFormat.Rgba16f });
            _sceneBuffer = new FrameBuffer(_sceneSettings);

            _uniformBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, _uniformBuffer);
            GL.BufferData(BufferTarget.UniformBuffer, _uniformData.Length * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);

            Rebuild(SupportedSamples(settings.Samples));
            UploadUniforms(settings);
            _geometryBuffer.unbind();
        }

        /// <summary>Start of the frame: rebuilds the buffers if the sample count changed, and uploads what the shaders read.</summary>
        public void BeginFrame(AntiAliasingSettings settings)
        {
            int samples = SupportedSamples(settings.Samples);
            if (samples != Samples) Rebuild(samples);
            UploadUniforms(settings);
        }

        /// <summary>The largest of 1, 2, 4 and 8 samples that is no more than requested and the GPU supports.</summary>
        private int SupportedSamples(int requested)
        {
            int limit = Math.Min(requested, _maxSamples);
            int samples = 1;
            while (samples * 2 <= limit) samples *= 2;
            return samples;
        }

        private void Rebuild(int samples)
        {
            while (true)
            {
                _geometrySettings.samples = samples;
                _sceneSettings.samples = samples;
                // The geometry buffer first: the scene buffer attaches its new depth.
                _geometryBuffer.resize(_geometryBuffer.getResolution());
                _sceneBuffer.resize(_sceneBuffer.getResolution());
                if ((_geometryBuffer.IsComplete && _sceneBuffer.IsComplete) || samples == 1) break;
                Console.WriteLine($"Warning: {samples}x MSAA buffers are not supported here, falling back to {samples / 2}x.");
                samples /= 2;
            }
            Samples = samples;

            // Sample positions belong to the bound draw framebuffer.
            _geometryBuffer.bind();
            float[] position = new float[2];
            for (int i = 0; i < MaxSamples; i++)
            {
                if (i < samples)
                {
                    GL.GetMultisample(GetMultisamplePName.SamplePosition, i, position);
                    _samplePositions[i] = new Vector2(position[0], position[1]);
                }
                else
                {
                    _samplePositions[i] = new Vector2(0.5f);
                }
            }
            _geometryBuffer.unbind();
        }

        /// <summary>Writes the MsaaData block; the layout must match Shaders/AntiAliasing/Msaa.glsl.</summary>
        private void UploadUniforms(AntiAliasingSettings settings)
        {
            _uniformData[0] = Samples;
            _uniformData[1] = settings.AlphaToCoverage ? 1f : 0f;
            _uniformData[2] = 0f;
            _uniformData[3] = 0f;
            for (int i = 0; i < MaxSamples; i++)
            {
                _uniformData[4 + 2 * i] = _samplePositions[i].X;
                _uniformData[5 + 2 * i] = _samplePositions[i].Y;
            }
            GL.BindBuffer(BufferTarget.UniformBuffer, _uniformBuffer);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, _uniformData.Length * sizeof(float), _uniformData);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, UniformBinding, _uniformBuffer);
        }

        /// <summary>Binds the multisampled G-buffer for the geometry pass and clears it, stencil included.</summary>
        public void BeginGeometry()
        {
            _geometryBuffer.bind();
            GL.DepthMask(true);
            GL.StencilMask(0xFF);
            GL.ClearColor(0f, 0f, 0f, 0f);
            GL.ClearStencil(0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        }

        /// <summary>
        /// After the geometry pass: the single-sample G-buffer (colour attachments, edge flags and depth) and the edge pixels'
        /// stencil bit. See Msaa_Resolve_GBuffer.frag.
        /// </summary>
        public void ResolveGeometry(RenderEngine renderEngine)
        {
            Engine.PerformanceMonitor.startGPUTask("MSAA G-buffer resolve");

            FrameBuffer gBuffer = renderEngine.GBuffer;
            gBuffer.bind();
            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.StencilTest);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Always);
            GL.DepthMask(true);
            _resolveGeometryShader.bind();
            BindTexture(0, _geometryBuffer.GetAttachment(0));
            BindTexture(1, _geometryBuffer.GetAttachment(1));
            BindTexture(2, _geometryBuffer.GetAttachment(2));
            BindTexture(3, _geometryBuffer.getDepthAttachment());
            renderEngine.ScreenQuadRenderer.DrawQuad();
            GL.DepthFunc(DepthFunction.Less);

            Engine.PerformanceMonitor.finishGPUTask("MSAA G-buffer resolve");
            Engine.PerformanceMonitor.startGPUTask("MSAA mark edges");
            if (Samples > 1)
            {
                _geometryBuffer.bind();
                GL.ColorMask(false, false, false, false);
                GL.Disable(EnableCap.DepthTest);
                GL.Enable(EnableCap.StencilTest);
                GL.StencilMask(EdgeStencilBit);
                GL.StencilFunc(StencilFunction.Always, EdgeStencilBit, EdgeStencilBit);
                GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);
                _markEdgesShader.bind();
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(EdgeAttachment));
                renderEngine.ScreenQuadRenderer.DrawQuad();
                GL.StencilMask(0xFF);
                GL.Disable(EnableCap.StencilTest);
                GL.ColorMask(true, true, true, true);
            }

            GL.UseProgram(0);
            gBuffer.unbind();
            Engine.PerformanceMonitor.finishGPUTask("MSAA mark edges");
        }

        /// <summary>
        /// Binds the G-buffer to texture units 0..3 (albedo, normal, materials, depth) and the screen-space ambient occlusion
        /// to <see cref="AmbientOcclusionUnit"/>, as Lighting/gBufferSample.glsl reads them: units 0..2 hold both the
        /// multisampled G-buffer and the resolved one, as a pass's two variants each read their own.
        /// </summary>
        public void BindGeometrySamples(int ambientOcclusionTexture)
        {
            FrameBuffer resolved = Engine.RenderEngine.GBuffer;
            for (int unit = 0; unit < 3; unit++)
            {
                BindTexture(unit, _geometryBuffer.GetAttachment(unit));
                GL.ActiveTexture(TextureUnit.Texture0 + unit);
                GL.BindTexture(TextureTarget.Texture2D, resolved.GetAttachment(unit));
            }
            BindTexture(3, _geometryBuffer.getDepthAttachment());
            GL.ActiveTexture(TextureUnit.Texture0 + AmbientOcclusionUnit);
            GL.BindTexture(TextureTarget.Texture2D, ambientOcclusionTexture);
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        /// <summary>Binds one of the multisampled buffers' textures (as a sampler2DMS) to a texture unit.</summary>
        public static void BindTexture(int unit, int texture)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + unit);
            GL.BindTexture(TextureTarget.Texture2DMultisample, texture);
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        /// <summary>
        /// Draws a pass that shades from the multisampled G-buffer into the bound scene buffer: <paramref name="perPixel"/>
        /// where all of a pixel's samples show one surface, then <paramref name="perSample"/> (the same shader built with
        /// PER_SAMPLE, which runs once per sample) on the edge pixels. <paramref name="draw"/> runs once per variant with
        /// that variant bound: it loads the variant's uniforms and draws. Blend, depth and cull state are left as set.
        /// </summary>
        public void DrawSplit(ShaderProgram perPixel, ShaderProgram perSample, Action<ShaderProgram> draw)
        {
            GL.Enable(EnableCap.StencilTest);
            GL.StencilMask(0);
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);

            perPixel.bind();
            GL.StencilFunc(StencilFunction.Equal, 0, EdgeStencilBit);
            draw(perPixel);

            if (Samples > 1)
            {
                perSample.bind();
                GL.StencilFunc(StencilFunction.Equal, EdgeStencilBit, EdgeStencilBit);
                draw(perSample);
            }

            GL.StencilMask(0xFF);
            GL.Disable(EnableCap.StencilTest);
        }

        /// <summary>Averages the scene's samples (a plain box filter) into a single-sample buffer of the same format, within a pixel rectangle.</summary>
        public void CopySceneColour(FrameBuffer destination, Box2i rect)
        {
            _sceneBuffer.bindRead();
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            destination.bindDraw();
            GL.BlitFramebuffer(rect.Min.X, rect.Min.Y, rect.Max.X, rect.Max.Y, rect.Min.X, rect.Min.Y, rect.Max.X, rect.Max.Y,
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        /// <summary>
        /// Brings the single-sample G-buffer's depth up to date with the multisampled depth within a pixel rectangle, after
        /// forward passes (the water) wrote to it: each pixel's farthest sample, as <see cref="ResolveGeometry"/> picks.
        /// </summary>
        public void ResolveDepth(RenderEngine renderEngine, Box2i rect)
        {
            renderEngine.GBuffer.bind();
            GL.Enable(EnableCap.ScissorTest);
            GL.Scissor(rect.Min.X, rect.Min.Y, rect.Size.X, rect.Size.Y);
            GL.ColorMask(false, false, false, false);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Always);
            GL.DepthMask(true);
            _resolveDepthShader.bind();
            BindTexture(0, _geometryBuffer.getDepthAttachment());
            renderEngine.ScreenQuadRenderer.DrawQuad();
            GL.DepthFunc(DepthFunction.Less);
            GL.ColorMask(true, true, true, true);
            GL.Disable(EnableCap.ScissorTest);
        }

        /// <summary>Resolves the scene into the next half of the HDR dual buffer, which post-processing continues from.</summary>
        public void ResolveScene(RenderEngine renderEngine)
        {
            Engine.PerformanceMonitor.startGPUTask("MSAA resolve");
            _resolveSceneShader.bind();
            _resolveSceneShader.loadUniformFloat("exposure", renderEngine.Lighting.Exposure * renderEngine.Adaptation);
            BindTexture(0, _sceneBuffer.GetAttachment(0));
            renderEngine.lastUsedBuffer.RenderToNextFrameBuffer();
            _resolveSceneShader.unBind();
            Engine.PerformanceMonitor.finishGPUTask("MSAA resolve");
        }

        /// <summary>Debug view: tints the edge pixels over the finished image.</summary>
        public void DrawEdgeOverlay(RenderEngine renderEngine)
        {
            renderEngine.lastUsedBuffer.GetLastFrameBuffer().bind();
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
            _edgeOverlayShader.bind();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine.GBuffer.GetAttachment(EdgeAttachment));
            renderEngine.ScreenQuadRenderer.DrawQuad();
            _edgeOverlayShader.unBind();
            GL.Disable(EnableCap.Blend);
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
            _geometryBuffer.resize(eventArgs.Size);
            _sceneBuffer.resize(eventArgs.Size);
        }

        public override void CleanUp()
        {
            _resolveGeometryShader.cleanUp();
            _markEdgesShader.cleanUp();
            _resolveDepthShader.cleanUp();
            _resolveSceneShader.cleanUp();
            _edgeOverlayShader.cleanUp();
            _sceneBuffer.cleanUp();
            _geometryBuffer.cleanUp();
            GL.DeleteBuffer(_uniformBuffer);
        }
    }
}
