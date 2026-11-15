using Dino_Engine.Core;
using Dino_Engine.Rendering.Renderers.Geometry;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.PosGeometry
{
    /// <summary>
    /// What the sea and the ground know about the coast around the camera (SHORE.md), worked out on the GPU whenever the loaded terrain changes:
    /// 1. A top-down capture of the terrain heights.
    /// 2. Seeds: which texels are sea and which are land (Water_Shore_Seed.frag).
    /// 3. A jump flood to the nearest land and nearest sea texel everywhere (Water_Shore_JumpFlood.frag).
    /// 4. A signed distance to the shoreline, smoothed, with its gradient (Finalize, Blur, Compose).
    /// 5. How open the water is: the share of sea among the known texels round each point, within a fixed reach, on a coarse grid (Water_Shore_Open.frag).
    /// The square it covers is fixed to the world and only ever moves by whole steps of <see cref="MoveTexels"/> (WaterSystem picks it), and its texels stay
    /// the size they are, so a build finds every texel where the last one had it, and only what really changed (ground streaming in or out at the far edge
    /// of the loaded square) can change. What the shaders read is then eased towards each new build (Water_Shore_Blend.frag), so that even that never jumps.
    /// A field laid over wherever the loaded chunks happened to be shifted its texels by a fraction of one, and its openness grid with them, every time the
    /// camera crossed a chunk, and the waves and the swash on the beach stuttered as anyone walked.
    /// Field layout: x signed distance (m, positive at sea), yz its gradient, w how open the water is (0..1).
    /// </summary>
    internal sealed class WaterShoreField
    {
        public const int Resolution = 768;
        /// <summary>The square only moves by whole multiples of this many texels: a whole number of coarse texels, so the openness grid stays where it is in the world.</summary>
        public const int MoveTexels = 128;
        /// <summary>Field texels to a texel of the coarse grid the openness is worked out on, each way: the openness texture's mip <see cref="CoarseLevel"/>.</summary>
        private const int CoarseTexels = 8;
        private const int CoarseLevel = 3;
        private const float CaptureRangeMetres = 1000f;
        private const float MaxDistance = 1000f;
        private const float BlurMetres = 1.5f;
        /// <summary>Metres round a point whose sea and land say how open the water is (a Gaussian of a third of this).</summary>
        public const float OpennessRadius = 20f;
        /// <summary>Seconds what's shown takes to come most of the way to a new build.</summary>
        private const float SettleSeconds = 0.3f;
        /// <summary>Metres round the middle of the view a stability check compares builds over: what's on screen.</summary>
        private const float CheckRadius = 40f;

        /// <summary>
        /// --render shorecheck=1: after every build, reads it back and logs how much it changed from the build before at the texels both cover within
        /// <see cref="CheckRadius"/> of the middle of the view, which should be nothing at all while the camera moves, as anything there would show in the
        /// waves as a jump. It stalls the GPU, so it's for checking alone.
        /// </summary>
        public static bool CheckStability;
        private float[]? _checkedBuild;
        private Vector4 _checkedRegion;

        private readonly ShaderProgram _seedShader = new ShaderProgram("Simple.vert", "Water/Water_Shore_Seed.frag");
        private readonly ShaderProgram _floodShader = new ShaderProgram("Simple.vert", "Water/Water_Shore_JumpFlood.frag");
        private readonly ShaderProgram _finalizeShader = new ShaderProgram("Simple.vert", "Water/Water_Shore_Finalize.frag");
        private readonly ShaderProgram _blurShader = new ShaderProgram("Simple.vert", "Water/Water_Shore_Blur.frag");
        private readonly ShaderProgram _openShader = new ShaderProgram("Simple.vert", "Water/Water_Shore_Open.frag");
        private readonly ShaderProgram _composeShader = new ShaderProgram("Simple.vert", "Water/Water_Shore_Compose.frag");
        private readonly ShaderProgram _blendShader = new ShaderProgram("Simple.vert", "Water/Water_Shore_Blend.frag");

        private readonly FrameBuffer _capture;
        private readonly FrameBuffer _seed;
        private readonly FrameBuffer[] _flood = new FrameBuffer[2];
        private readonly FrameBuffer _raw;
        private readonly FrameBuffer _blur;
        private readonly FrameBuffer[] _open = new FrameBuffer[2];
        private readonly FrameBuffer _built;
        private readonly FrameBuffer[] _shown = new FrameBuffer[2];

        private int _shownIndex;
        private bool _shownValid;
        private Vector4 _shownRegion;
        private Vector4 _builtRegion;
        private bool _settling;
        private float _settleAge;

        /// <summary>Whether there's a field to read at all.</summary>
        public bool Valid { get; private set; }
        /// <summary>The square of the newest build, which the terrain capture covers.</summary>
        public Vector2 Corner { get; private set; }
        public float Size { get; private set; }
        public float TexelMetres => Size / Resolution;
        public long Signature { get; private set; } = long.MinValue;

        /// <summary>What the shaders read (Water/ShoreData.glsl's shoreField), over <see cref="FieldRegion"/>.</summary>
        public int FieldTexture => _shown[_shownIndex].GetAttachment(0);
        /// <summary>xy world corner, zw size of what <see cref="FieldTexture"/> covers; zero when there's nothing.</summary>
        public Vector4 FieldRegion => Valid && _shownValid ? _shownRegion : Vector4.Zero;
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
            _openShader.bind();
            _openShader.loadUniformInt("source", 0);
            _composeShader.bind();
            _composeShader.loadUniformInt("source", 0);
            _composeShader.loadUniformInt("openness", 1);
            _blendShader.bind();
            _blendShader.loadUniformInt("built", 0);
            _blendShader.loadUniformInt("previous", 1);
            _blendShader.unBind();

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
                minFilterType = TextureMinFilter.NearestMipmapNearest,
                magFilterType = TextureMagFilter.Nearest,
                wrapMode = TextureWrapMode.ClampToEdge,
            });
            _seed = new FrameBuffer(seedSettings);

            for (int i = 0; i < _flood.Length; i++) _flood[i] = SingleBuffer(Resolution, nearest: true);
            _raw = SingleBuffer(Resolution, nearest: true);
            _blur = SingleBuffer(Resolution, nearest: true);
            for (int i = 0; i < _open.Length; i++) _open[i] = SingleBuffer(Resolution / CoarseTexels, nearest: false);
            _built = SingleBuffer(Resolution, nearest: true);
            for (int i = 0; i < _shown.Length; i++) _shown[i] = SingleBuffer(Resolution, nearest: false);
            _shown[0].unbind();

            // The openness mip chain has to exist before anything reads a level of it.
            GL.BindTexture(TextureTarget.Texture2D, _seed.GetAttachment(2));
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

        private static FrameBuffer SingleBuffer(int resolution, bool nearest)
        {
            FrameBufferSettings settings = new FrameBufferSettings(new Vector2i(resolution));
            settings.drawBuffers.Add(FieldBuffer(FramebufferAttachment.ColorAttachment0, nearest));
            return new FrameBuffer(settings);
        }

        /// <summary>Metres the square moves by at least, for a square of <paramref name="size"/> metres.</summary>
        public static float MoveMetres(float size) => size * MoveTexels / Resolution;

        public void Invalidate()
        {
            Valid = false;
            Signature = long.MinValue;
            _shownValid = false;
            _settling = false;
        }

        /// <summary>
        /// Works out the field for the terrain in the square [corner, corner + size] and a sea at <paramref name="seaLevel"/>. What's shown moves towards it
        /// over the next frames (<see cref="Settle"/>). The corner has to be a whole number of <see cref="MoveMetres"/> from any other the same size, which
        /// is what keeps every texel where it was.
        /// </summary>
        public void Build(TerrainChunkRenderData[] chunks, Vector2 corner, float size, float seaLevel, long signature, Vector2 viewCentre, RenderEngine renderEngine)
        {
            Signature = signature;
            if (chunks.Length == 0 || !(size > 0f))
            {
                Valid = false;
                _shownValid = false;
                _settling = false;
                return;
            }
            // A square of another size has texels of another size: nothing of the old one carries over.
            if (Valid && MathF.Abs(size - Size) > 1e-3f) _shownValid = false;
            Valid = true;
            Corner = corner;
            Size = size;

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

            // The share of known sea and of known ground, averaged over the coarse grid's texels: a mip of the square, so aligned with it, and so with the world.
            GL.BindTexture(TextureTarget.Texture2D, _seed.GetAttachment(2));
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            // Jump flood (Rong and Tan): steps of the largest power of two below the square, halving to 1, then two more at 1 to mend the rare misses.
            _floodShader.bind();
            int source = _seed.GetAttachment(0);
            int target = 0;
            int firstStep = 1;
            while (firstStep * 2 < Resolution) firstStep *= 2;
            int passes = 2;
            for (int s = firstStep; s >= 1; s /= 2) passes++;
            for (int pass = 0; pass < passes; pass++)
            {
                _flood[target].bind();
                _floodShader.loadUniformInt("stepSize", Math.Max(firstStep >> pass, 1));
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

            // How open: a Gaussian of known sea and of known ground on the coarse grid, one way then the other, their ratio taken where it's read.
            float coarseMetres = TexelMetres * CoarseTexels;
            float sigma = OpennessRadius / 3f / coarseMetres;
            _openShader.bind();
            _openShader.loadUniformFloat("sigma", sigma);
            _openShader.loadUniformInt("taps", Math.Clamp((int)MathF.Ceiling(sigma * 3f), 1, 24));
            _open[0].bind();
            _openShader.loadUniformInt("level", CoarseLevel);
            _openShader.loadUniformVector2f("direction", new Vector2(1f, 0f));
            GL.BindTexture(TextureTarget.Texture2D, _seed.GetAttachment(2));
            quad.Render(clearColor: false);
            _open[1].bind();
            _openShader.loadUniformInt("level", 0);
            _openShader.loadUniformVector2f("direction", new Vector2(0f, 1f));
            GL.BindTexture(TextureTarget.Texture2D, _open[0].GetAttachment(0));
            quad.Render(clearColor: false);

            _built.bind();
            _composeShader.bind();
            _composeShader.loadUniformFloat("texelMetres", TexelMetres);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _raw.GetAttachment(0));
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _open[1].GetAttachment(0));
            quad.Render(clearColor: false);
            GL.ActiveTexture(TextureUnit.Texture0);

            _builtRegion = new Vector4(corner.X, corner.Y, size, size);
            _settling = true;
            _settleAge = 0f;
            _composeShader.unBind();
            _built.unbind();
            if (CheckStability) CompareWithLastBuild(viewCentre);
        }

        /// <summary>Logs how much this build differs from the last at the texels both cover near the middle of the view (<see cref="CheckStability"/>).</summary>
        private void CompareWithLastBuild(Vector2 viewCentre)
        {
            float[] now = new float[Resolution * Resolution * 4];
            GL.BindTexture(TextureTarget.Texture2D, _built.GetAttachment(0));
            GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.Float, now);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            if (_checkedBuild != null && MathF.Abs(_checkedRegion.Z - Size) < 1e-3f)
            {
                float texel = TexelMetres;
                int shiftX = (int)MathF.Round((Corner.X - _checkedRegion.X) / texel);
                int shiftZ = (int)MathF.Round((Corner.Y - _checkedRegion.Y) / texel);
                float worstDistance = 0f, worstOpenness = 0f;
                int compared = 0;
                for (int z = 0; z < Resolution; z++)
                {
                    for (int x = 0; x < Resolution; x++)
                    {
                        Vector2 world = Corner + (new Vector2(x, z) + new Vector2(0.5f)) * texel;
                        if ((world - viewCentre).LengthSquared > CheckRadius * CheckRadius) continue;
                        int oldX = x + shiftX, oldZ = z + shiftZ;
                        if (oldX < 0 || oldZ < 0 || oldX >= Resolution || oldZ >= Resolution) continue;
                        int i = (z * Resolution + x) * 4, j = (oldZ * Resolution + oldX) * 4;
                        // Far out at sea or far inland nothing reads the field but to know it's far.
                        if (MathF.Abs(now[i]) > 60f && MathF.Abs(_checkedBuild[j]) > 60f) continue;
                        compared++;
                        worstDistance = MathF.Max(worstDistance, MathF.Abs(now[i] - _checkedBuild[j]));
                        worstOpenness = MathF.Max(worstOpenness, MathF.Abs(now[i + 3] - _checkedBuild[j + 3]));
                    }
                }
                Console.WriteLine($"Shore field build: moved {shiftX},{shiftZ} texels; near the view {compared} texels compared, changed by at most {worstDistance:0.0000} m and {worstOpenness:0.0000} in openness.");
            }
            _checkedBuild = now;
            _checkedRegion = new Vector4(Corner.X, Corner.Y, Size, Size);
        }

        /// <summary>Moves what's shown towards the newest build, by as much as <paramref name="deltaTime"/> seconds allow. Every frame, after any build.</summary>
        public void Settle(float deltaTime, RenderEngine renderEngine)
        {
            if (!Valid || !_settling) return;
            _settleAge += MathF.Max(deltaTime, 0f);
            // Nothing to come from: the build as it is. Near enough after a while: the build as it is, and nothing more to do until the next.
            float keep = _shownValid ? MathF.Exp(-MathF.Max(deltaTime, 0f) / SettleSeconds) : 0f;
            if (_settleAge >= SettleSeconds * 8f) keep = 0f;

            int target = 1 - _shownIndex;
            _shown[target].bind();
            _blendShader.bind();
            _blendShader.loadUniformVector4f("region", _builtRegion);
            _blendShader.loadUniformVector4f("previousRegion", _shownValid ? _shownRegion : Vector4.Zero);
            _blendShader.loadUniformFloat("keep", keep);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _built.GetAttachment(0));
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _shown[_shownIndex].GetAttachment(0));
            renderEngine.ScreenQuadRenderer.Render(clearColor: false);
            GL.ActiveTexture(TextureUnit.Texture0);
            _blendShader.unBind();
            _shown[target].unbind();

            _shownIndex = target;
            _shownRegion = _builtRegion;
            _shownValid = true;
            if (keep <= 0f) _settling = false;
        }

        public void CleanUp()
        {
            _seedShader.cleanUp();
            _floodShader.cleanUp();
            _finalizeShader.cleanUp();
            _blurShader.cleanUp();
            _openShader.cleanUp();
            _composeShader.cleanUp();
            _blendShader.cleanUp();
            _capture.cleanUp();
            _seed.cleanUp();
            foreach (FrameBuffer buffer in _flood) buffer.cleanUp();
            _raw.cleanUp();
            _blur.cleanUp();
            foreach (FrameBuffer buffer in _open) buffer.cleanUp();
            _built.cleanUp();
            foreach (FrameBuffer buffer in _shown) buffer.cleanUp();
        }
    }
}
