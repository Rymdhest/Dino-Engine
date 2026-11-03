using Dino_Engine.Core;
using Dino_Engine.Rendering.Renderers.Geometry;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.PosGeometry
{
    /// <summary>
    /// What the sea needs to know about the coast around the camera, rebuilt on the GPU whenever the loaded terrain changes:
    /// 1. A top-down capture of the terrain heights.
    /// 2. Seeds: which texels are sea and which are land (Water_Shore_Seed.frag).
    /// 3. A jump flood to the nearest land and nearest sea texel everywhere (Water_Shore_JumpFlood.frag).
    /// 4. A signed distance to the shoreline, smoothed, with its gradient (Finalize, Blur, Gradient).
    /// 5. How open the water is: the mipmapped share of sea among the known texels around each point.
    /// Field layout: x signed distance (m, positive at sea), y depth below sea level (m), zw distance gradient.
    /// </summary>
    internal sealed class WaterShoreField
    {
        public const int Resolution = 512;
        private const float CaptureRangeMetres = 1000f;
        private const float MaxDistance = 1000f;
        private const float BlurMetres = 1.5f;

        private readonly ShaderProgram _seedShader = new ShaderProgram("Simple.vert", "Water/Water_Shore_Seed.frag");
        private readonly ShaderProgram _floodShader = new ShaderProgram("Simple.vert", "Water/Water_Shore_JumpFlood.frag");
        private readonly ShaderProgram _finalizeShader = new ShaderProgram("Simple.vert", "Water/Water_Shore_Finalize.frag");
        private readonly ShaderProgram _blurShader = new ShaderProgram("Simple.vert", "Water/Water_Shore_Blur.frag");
        private readonly ShaderProgram _gradientShader = new ShaderProgram("Simple.vert", "Water/Water_Shore_Gradient.frag");

        private readonly FrameBuffer _capture;
        private readonly FrameBuffer _seed;
        private readonly FrameBuffer[] _flood = new FrameBuffer[2];
        private readonly FrameBuffer _raw;
        private readonly FrameBuffer _blur;
        private readonly FrameBuffer _field;

        public bool Valid { get; private set; }
        public Vector2 Corner { get; private set; }
        public float Size { get; private set; }
        public float TexelMetres => Size / Resolution;
        public long Signature { get; private set; } = long.MinValue;

        public int FieldTexture => _field.GetAttachment(0);
        public int OpennessTexture => _seed.GetAttachment(2);
        /// <summary>
        /// The captured terrain mesh, without parallax relief: depth below <see cref="CaptureTop"/> over <see cref="CaptureRange"/>
        /// metres, 1 where there is none. Water decides where it stands from this rather than from the parallax-mapped depth.
        /// </summary>
        public int HeightTexture => _capture.getDepthAttachment();
        public float CaptureTop { get; private set; }
        public float CaptureRange => CaptureRangeMetres;

        public WaterShoreField()
        {
            _seedShader.bind();
            _seedShader.loadUniformInt("terrainDepth", 0);
            _floodShader.bind();
            _floodShader.loadUniformInt("seeds", 0);
            _finalizeShader.bind();
            _finalizeShader.loadUniformInt("seeds", 0);
            _finalizeShader.loadUniformInt("info", 1);
            _blurShader.bind();
            _blurShader.loadUniformInt("source", 0);
            _gradientShader.bind();
            _gradientShader.loadUniformInt("source", 0);
            _gradientShader.unBind();

            Vector2i resolution = new Vector2i(Resolution);
            FrameBufferSettings captureSettings = new FrameBufferSettings(resolution);
            captureSettings.depthAttachmentSettings = new DepthAttachmentSettings { isTexture = true };
            _capture = new FrameBuffer(captureSettings);

            FrameBufferSettings seedSettings = new FrameBufferSettings(resolution);
            seedSettings.drawBuffers.Add(FieldBuffer(FramebufferAttachment.ColorAttachment0, nearest: true));
            seedSettings.drawBuffers.Add(FieldBuffer(FramebufferAttachment.ColorAttachment1, nearest: true));
            seedSettings.drawBuffers.Add(new DrawBufferSettings(FramebufferAttachment.ColorAttachment2)
            {
                formatInternal = PixelInternalFormat.Rg8,
                formatExternal = PixelFormat.Rg,
                pixelType = PixelType.UnsignedByte,
                minFilterType = TextureMinFilter.LinearMipmapLinear,
                magFilterType = TextureMagFilter.Linear,
                wrapMode = TextureWrapMode.ClampToEdge,
            });
            _seed = new FrameBuffer(seedSettings);

            for (int i = 0; i < _flood.Length; i++) _flood[i] = SingleBuffer(nearest: true);
            _raw = SingleBuffer(nearest: true);
            _blur = SingleBuffer(nearest: true);
            _field = SingleBuffer(nearest: false);
            _field.unbind();

            // The openness mip chain has to exist before anything samples it with a mipmapped filter.
            GL.BindTexture(TextureTarget.Texture2D, OpennessTexture);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        private static DrawBufferSettings FieldBuffer(FramebufferAttachment attachment, bool nearest)
        {
            return new DrawBufferSettings(attachment)
            {
                formatInternal = PixelInternalFormat.Rgba16f,
                pixelType = PixelType.Float,
                minFilterType = nearest ? TextureMinFilter.Nearest : TextureMinFilter.Linear,
                magFilterType = nearest ? TextureMagFilter.Nearest : TextureMagFilter.Linear,
                wrapMode = TextureWrapMode.ClampToEdge,
            };
        }

        private static FrameBuffer SingleBuffer(bool nearest)
        {
            FrameBufferSettings settings = new FrameBufferSettings(new Vector2i(Resolution));
            settings.drawBuffers.Add(FieldBuffer(FramebufferAttachment.ColorAttachment0, nearest));
            return new FrameBuffer(settings);
        }

        /// <summary>Mip level of the openness texture whose texels are about <paramref name="radius"/> metres across.</summary>
        public float OpennessLod(float radius)
        {
            return Math.Clamp(MathF.Log2(MathF.Max(radius, 1e-3f) / MathF.Max(TexelMetres, 1e-3f)), 0f, MathF.Log2(Resolution));
        }

        public void Invalidate()
        {
            Valid = false;
            Signature = long.MinValue;
        }

        /// <summary>Analyses the terrain in the square [corner, corner + size] for a sea at <paramref name="seaLevel"/>.</summary>
        public void Build(TerrainChunkRenderData[] chunks, Vector2 corner, float size, float seaLevel, long signature, RenderEngine renderEngine)
        {
            Corner = corner;
            Size = size;
            Signature = signature;
            Valid = chunks.Length > 0 && size > 0f;
            if (!Valid) return;

            // Top-down capture. clip x, y = world x, z across the square; depth runs down from captureTop over the range.
            float captureTop = seaLevel + CaptureRangeMetres * 0.5f;
            CaptureTop = captureTop;
            Matrix4 capture = Matrix4.Zero;
            capture.M11 = 2f / size;
            capture.M41 = -1f - 2f * corner.X / size;
            capture.M32 = 2f / size;
            capture.M42 = -1f - 2f * corner.Y / size;
            capture.M23 = -2f / CaptureRangeMetres;
            capture.M43 = 2f * captureTop / CaptureRangeMetres - 1f;
            capture.M44 = 1f;
            _capture.ClearDepth();
            renderEngine._terrainRenderer.RenderHeightCapture(chunks, capture);

            ScreenQuadRenderer quad = renderEngine.ScreenQuadRenderer;

            _seed.bind();
            _seedShader.bind();
            _seedShader.loadUniformFloat("captureTop", captureTop);
            _seedShader.loadUniformFloat("captureRange", CaptureRangeMetres);
            _seedShader.loadUniformFloat("seaLevel", seaLevel);
            _seedShader.loadUniformFloat("surfaceOffset", TerrainRenderer.SurfaceOffset);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _capture.getDepthAttachment());
            quad.Render(clearColor: false);

            GL.BindTexture(TextureTarget.Texture2D, OpennessTexture);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            // Jump flood: steps of Resolution / 2, / 4, ... 1, then one more pass at 1 to mend the rare misses of plain
            // jump flooding.
            _floodShader.bind();
            int source = _seed.GetAttachment(0);
            int target = 0;
            int passes = (int)MathF.Round(MathF.Log2(Resolution));
            for (int pass = 0; pass <= passes; pass++)
            {
                _flood[target].bind();
                _floodShader.loadUniformInt("stepSize", Math.Max(Resolution >> (pass + 1), 1));
                GL.BindTexture(TextureTarget.Texture2D, source);
                quad.Render(clearColor: false);
                source = _flood[target].GetAttachment(0);
                target = 1 - target;
            }

            _raw.bind();
            _finalizeShader.bind();
            _finalizeShader.loadUniformFloat("texelMetres", TexelMetres);
            _finalizeShader.loadUniformFloat("maxDistance", MaxDistance);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, source);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _seed.GetAttachment(1));
            quad.Render(clearColor: false);

            // The kernel's standard deviation is 2 taps.
            float tapStep = MathF.Max(BlurMetres / (2f * TexelMetres), 1f);
            _blurShader.bind();
            GL.ActiveTexture(TextureUnit.Texture0);
            _blur.bind();
            _blurShader.loadUniformVector2f("stepTexels", new Vector2(tapStep, 0f));
            GL.BindTexture(TextureTarget.Texture2D, _raw.GetAttachment(0));
            quad.Render(clearColor: false);
            _raw.bind();
            _blurShader.loadUniformVector2f("stepTexels", new Vector2(0f, tapStep));
            GL.BindTexture(TextureTarget.Texture2D, _blur.GetAttachment(0));
            quad.Render(clearColor: false);

            _field.bind();
            _gradientShader.bind();
            _gradientShader.loadUniformFloat("texelMetres", TexelMetres);
            GL.BindTexture(TextureTarget.Texture2D, _raw.GetAttachment(0));
            quad.Render(clearColor: false);

            _gradientShader.unBind();
            _field.unbind();
        }

        public void CleanUp()
        {
            _seedShader.cleanUp();
            _floodShader.cleanUp();
            _finalizeShader.cleanUp();
            _blurShader.cleanUp();
            _gradientShader.cleanUp();
            _capture.cleanUp();
            _seed.cleanUp();
            foreach (FrameBuffer buffer in _flood) buffer.cleanUp();
            _raw.cleanUp();
            _blur.cleanUp();
            _field.cleanUp();
        }
    }
}
