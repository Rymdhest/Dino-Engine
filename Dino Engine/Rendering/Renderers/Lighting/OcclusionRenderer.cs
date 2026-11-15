using Dino_Engine.Core;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    /// <summary>
    /// Ambient occlusion, as ground truth ambient occlusion with visibility bitmasks (OCCLUSION.md), from the resolved G-buffer, in compute passes that
    /// RenderEngine.ScreenTerms runs in stages, between the others it doesn't wait for:
    /// 1. The depth as metres along the view, with canopies marked and three coarser levels (Occlusion_Depth.comp). Contact shadows read it too.
    /// 2. The occlusion itself, a few slices a pixel, each pixel of a 4 x 4 tile at its own offsets (Occlusion_Trace.comp).
    /// 3. A [1 2 2 2 1] filter across, then up, that stops at edges, which takes the tile's pattern out exactly (Occlusion_Denoise.comp).
    /// The light passes take <see cref="OcclusionTexture"/> off each sample's own ambient occlusion (Lighting/gBufferSample.glsl); nothing is random and
    /// nothing is kept from frame to frame.
    /// </summary>
    internal class OcclusionRenderer : RenderPassRenderer
    {
        /// <summary>Levels of <see cref="DepthTexture"/>: its own and three coarser.</summary>
        public const int DepthLevels = 4;
        // What the occlusion holds while it's off: nothing occluded.
        private static readonly float[] Open = { 1f };

        private readonly ShaderProgram _depthShader = new ShaderProgram("Occlusion/Occlusion_Depth.comp");
        private readonly ShaderProgram _denoiseShader = new ShaderProgram("Occlusion/Occlusion_Denoise.comp");
        // The trace, one program for each count of slices and steps it's been asked for, as they're fixed when it's compiled so that its loops unroll.
        private readonly Dictionary<(int slices, int steps), ShaderProgram> _traceShaders = new();

        private int _depth;
        private int _raw;
        private int _edges;
        private int _between;
        private int _final;
        private Vector2i _size;
        // Whether _final holds nothing occluded, as it must while the occlusion is off.
        private bool _open;

        /// <summary>This frame's occlusion, 1 open and 0 shut, once the pass has run.</summary>
        public int OcclusionTexture => _final;

        /// <summary>This frame's depth as metres along the view, negative on a canopy (Occlusion/Occlusion.glsl), once the first stage has run.</summary>
        public int DepthTexture => _depth;

        internal OcclusionRenderer() : base("Occlusion")
        {
            // Timed by its stages instead (GPU timers don't nest).
            trackPerformance = false;

            _depthShader.bind();
            _depthShader.loadUniformInt("gDepth", 0);
            _depthShader.loadUniformInt("gAlbedo", 1);
            _denoiseShader.bind();
            _denoiseShader.loadUniformInt("source", 0);
            _denoiseShader.loadUniformInt("edges", 1);
            _denoiseShader.unBind();

            CreateTextures(Engine.RenderResolution);
        }

        private void CreateTextures(Vector2i size)
        {
            DeleteTextures();
            _size = new Vector2i(Math.Max(size.X, 1), Math.Max(size.Y, 1));
            int levels = Math.Min(DepthLevels, 1 + (int)Math.Floor(Math.Log2(Math.Max(_size.X, _size.Y))));
            _depth = Texture(SizedInternalFormat.R32f, levels, TextureMinFilter.NearestMipmapNearest, TextureMagFilter.Nearest);
            _raw = Texture(SizedInternalFormat.R16f, 1, TextureMinFilter.Nearest, TextureMagFilter.Nearest);
            _edges = Texture(SizedInternalFormat.R8, 1, TextureMinFilter.Nearest, TextureMagFilter.Nearest);
            _between = Texture(SizedInternalFormat.R16f, 1, TextureMinFilter.Nearest, TextureMagFilter.Nearest);
            _final = Texture(SizedInternalFormat.R16f, 1, TextureMinFilter.Nearest, TextureMagFilter.Nearest);
            _open = false;
        }

        private int Texture(SizedInternalFormat format, int levels, TextureMinFilter min, TextureMagFilter mag)
        {
            int texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexStorage2D(TextureTarget2d.Texture2D, levels, format, _size.X, _size.Y);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)min);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)mag);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return texture;
        }

        private void DeleteTextures()
        {
            foreach (int texture in new[] { _depth, _raw, _edges, _between, _final })
            {
                if (texture != 0) GL.DeleteTexture(texture);
            }
            _depth = _raw = _edges = _between = _final = 0;
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        /// <summary>The metres the occlusion reaches this frame: only the contact while global illumination has everything further.</summary>
        public static float ReachOf(RenderEngine renderEngine)
        {
            float gi = renderEngine._globalIllumination.Active ? renderEngine._globalIllumination.Presence : 0f;
            return MathHelper.Lerp(renderEngine.Occlusion.Radius, renderEngine.GlobalIllumination.ContactReach, gi);
        }

        private ShaderProgram TraceShader(int slices, int steps)
        {
            if (_traceShaders.TryGetValue((slices, steps), out ShaderProgram? shader)) return shader;
            shader = new ShaderProgram("Occlusion/Occlusion_Trace.comp", new[] { "OCCLUSION_SLICES " + slices, "OCCLUSION_STEPS " + steps });
            shader.bind();
            shader.loadUniformInt("linearDepth", 0);
            shader.unBind();
            _traceShaders[(slices, steps)] = shader;
            return shader;
        }

        // Drawn in stages by RenderEngine.ScreenTerms, beside the contact shadows and the sun's soft shadow, with one barrier between stages rather
        // than one after every dispatch, as a barrier waits for everything before it.
        internal override void Render(RenderEngine renderEngine)
        {
        }

        private float _reach;

        /// <summary>
        /// The first stage: the depth as metres along the view, which contact shadows, the sun's soft shadow and the light passes read too. Its work is
        /// only there once a barrier has come after it.
        /// </summary>
        public void Depth(RenderEngine renderEngine)
        {
            _reach = Math.Max(ReachOf(renderEngine), 0.01f);
            Engine.PerformanceMonitor.startGPUTask("Occlusion Depth");
            FrameBuffer gBuffer = renderEngine.GBuffer;
            _depthShader.bind();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.getDepthAttachment());
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(0));
            int levels = Math.Min(DepthLevels, 1 + (int)Math.Floor(Math.Log2(Math.Max(_size.X, _size.Y))));
            for (int level = 0; level < DepthLevels; level++)
            {
                // Past the last level there's nothing to write, but the shader still names four: they point at the last there is.
                GL.BindImageTexture(level, _depth, Math.Min(level, levels - 1), false, 0, TextureAccess.WriteOnly, SizedInternalFormat.R32f);
            }
            _depthShader.loadUniformFloat("mipFalloff", _reach * 0.75f);
            GL.DispatchCompute((_size.X + 15) / 16, (_size.Y + 15) / 16, 1);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.UseProgram(0);
            Engine.PerformanceMonitor.finishGPUTask("Occlusion Depth");

            // Nothing occluded while it's off: cleared once, here, as a clear comes in order before anything that reads it.
            if (!renderEngine.Occlusion.Enabled && !_open)
            {
                GL.ClearTexImage(_final, 0, PixelFormat.Red, PixelType.Float, Open);
                _open = true;
            }
        }

        /// <summary>The second stage: the occlusion itself, from the depth.</summary>
        public void Trace(RenderEngine renderEngine)
        {
            OcclusionSettings settings = renderEngine.Occlusion;
            if (!settings.Enabled) return;
            _open = false;

            Engine.PerformanceMonitor.startGPUTask("Occlusion Trace");
            ShaderProgram trace = TraceShader(Math.Clamp(settings.Slices, 1, 8), Math.Clamp(settings.Steps, 1, 12));
            trace.bind();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _depth);
            GL.BindImageTexture(0, _raw, 0, false, 0, TextureAccess.WriteOnly, SizedInternalFormat.R16f);
            GL.BindImageTexture(1, _edges, 0, false, 0, TextureAccess.WriteOnly, SizedInternalFormat.R8);
            trace.loadUniformFloat("radius", _reach);
            trace.loadUniformFloat("fadeFrom", _reach * (1f - Math.Clamp(settings.Fade, 0.05f, 1f)));
            trace.loadUniformFloat("thickness", Math.Max(settings.Thickness, 0f));
            trace.loadUniformFloat("thinThickness", Math.Max(settings.ThinThickness, 0f));
            trace.loadUniformFloat("coarseFrom", settings.CoarseFrom);
            GL.DispatchCompute((_size.X + 7) / 8, (_size.Y + 7) / 8, 1);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.UseProgram(0);
            Engine.PerformanceMonitor.finishGPUTask("Occlusion Trace");
        }

        /// <summary>The third stage: the denoise across.</summary>
        public void DenoiseAcross(RenderEngine renderEngine)
        {
            if (!renderEngine.Occlusion.Enabled) return;
            Denoise(_raw, _between, new Vector2i(1, 0), finish: false, "Occlusion Denoise");
        }

        /// <summary>The fourth stage: the denoise up, into <see cref="OcclusionTexture"/>.</summary>
        public void DenoiseUp(RenderEngine renderEngine)
        {
            if (!renderEngine.Occlusion.Enabled) return;
            Denoise(_between, _final, new Vector2i(0, 1), finish: true, "Occlusion Denoise Up");
        }

        private void Denoise(int source, int target, Vector2i direction, bool finish, string task)
        {
            Engine.PerformanceMonitor.startGPUTask(task);
            _denoiseShader.bind();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, source);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _edges);
            GL.BindImageTexture(0, target, 0, false, 0, TextureAccess.WriteOnly, SizedInternalFormat.R16f);
            _denoiseShader.loadUniformVector2i("direction", direction);
            _denoiseShader.loadUniformInt("finish", finish ? 1 : 0);
            GL.DispatchCompute((_size.X + 7) / 8, (_size.Y + 7) / 8, 1);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.UseProgram(0);
            Engine.PerformanceMonitor.finishGPUTask(task);
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
            CreateTextures(eventArgs.Size);
        }

        public override void CleanUp()
        {
            _depthShader.cleanUp();
            foreach (ShaderProgram trace in _traceShaders.Values) trace.cleanUp();
            _traceShaders.Clear();
            _denoiseShader.cleanUp();
            DeleteTextures();
        }
    }
}
