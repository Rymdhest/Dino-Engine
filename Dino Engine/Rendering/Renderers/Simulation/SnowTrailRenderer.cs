using Dino_Engine.Core;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.Simulation
{
    /// <summary>What presses into snow, which decides the shape it leaves (Shaders/Snow/SnowTrail_Stamp.frag).</summary>
    public enum SnowPrint
    {
        Boot,       // A person's sole: a heel and a ball, the arch shallower between them.
        ThreeToed,  // A theropod's: three toes spread from a pad, a claw at each tip, and a dew claw behind.
        Pad,        // A sauropod's or anything else saurian on four: a broad pad with nubs of toes round its front.
        Paw,        // A beast's: a pad, and four toes apart from it.
        Trench,     // What a body ploughs through deep snow: a furrow between banks.
        Groove,     // What a foot drags through it on its way to the next step.
    }

    /// <summary>One press into the snow, on the xz plane, in metres.</summary>
    public struct SnowStamp
    {
        public Vector2 Center;
        public float Yaw;           // The way it points, as Frames.Forward: +z at 0.
        public float HalfWidth;
        public float HalfLength;
        public SnowPrint Kind;
        public float Depth;         // Metres its floor goes down below the snow's own level, in snow as deep as the Snowfield's.
        public float Press;         // 0..1: how much of the snow's own surface its floor takes the place of, 1 for a print and less for a churned furrow.
        public float Rim;           // Metres of snow it heaps up round its edge.
        public float Churn;         // 0..1: how thickly it scatters clumps of what it kicked up.
        public Vector2 Push;        // The way it was going, at up to 1 for a run, which heaps its rim and throws its clumps that way.
    }

    /// <summary>
    /// Snow that remembers who went through it: a map of the ground around the camera that every foot, and every body ploughing through deep snow, presses its shape
    /// into, and that falling snow fills in again. The terrain reads it in its parallax march (Terrain.frag, Shaders/Snow/SnowTrail.glsl), so a print is a real hollow
    /// that the sun shadows and the sky lights less, and not a picture of one. Read SNOW.md.
    ///
    /// It wraps round: a texel is a place in the world for as long as it's in the region, and the region moves by clearing only the strips it moves into, so nothing
    /// is ever copied. Prints are drawn with a MAX blend, as snow pressed down stays down however many feet come after, and falling snow is taken off them all by a
    /// subtracting blend, so there's one texture and no ping-pong.
    ///
    /// Texel channels: r how deep the floor is (of <see cref="Deepest"/>), g how much of the snow's own surface it replaces, b how high the snow is heaped round it
    /// (of <see cref="Highest"/>), a how thickly kicked-up clumps lie on it.
    /// </summary>
    public class SnowTrailRenderer : RenderPassRenderer
    {
        public const int Resolution = 2048;
        public const float Size = 96f;                  // Metres across: what the camera sees at its furthest zoom, with prints fading out before its edge.
        public const float Texel = Size / Resolution;
        public const float Deepest = 0.6f;              // Metres a full r is, as SNOW_TRAIL_DEEPEST in SnowTrail.glsl.
        public const float Highest = 0.25f;             // Metres a full b is, as SNOW_TRAIL_HIGHEST.
        public const int Unit = 16;                     // The texture unit the terrain reads it from, past the weather's (WeatherRenderer.SurfaceMapUnit and on).
        public const int Levels = 4;                    // Mips below the trail, as SNOW_TRAIL_NEAR_LOD: the last, at 0.75 m a texel, says whether anything's near, and still keeps a lone clump in eight bits.

        private const int StampsPerPass = 32;           // As STAMPS in SnowTrail_Stamp.vert.
        private const int MostStamps = 1024;

        private readonly ShaderProgram _stampShader = new ShaderProgram("Snow/SnowTrail_Stamp.vert", "Snow/SnowTrail_Stamp.frag");
        private readonly ShaderProgram _fillShader = new ShaderProgram("Simple.vert", "Snow/SnowTrail_Fill.frag");
        private readonly FrameBuffer _trail;
        private readonly int _emptyVao;

        private readonly SnowStamp[] _stamps = new SnowStamp[MostStamps];
        private int _stampCount;
        private readonly float[] _place = new float[StampsPerPass * 4];
        private readonly float[] _size = new float[StampsPerPass * 4];
        private readonly float[] _more = new float[StampsPerPass * 4];
        private readonly float[] _wrap = new float[StampsPerPass * 4];

        private Vector2 _center;
        private Vector2i _origin;           // The texel at the region's least corner.
        private bool _placed;
        private bool _clearAll = true;
        private readonly List<Vector4i> _clears = new();    // Texel rectangles of the texture to empty before anything's drawn: x, y, width, height.

        private Vector4 _fillOwed;          // What falling snow has filled in and hasn't been taken off yet, in parts of a whole channel.
        private bool _anything;             // Whether anything has been pressed in since the trail was last emptied, which is all that makes the terrain read it.

        public SnowTrailRenderer() : base("Snow Trail")
        {
            FrameBufferSettings settings = new FrameBufferSettings(new Vector2i(Resolution));
            settings.drawBuffers.Add(new DrawBufferSettings(FramebufferAttachment.ColorAttachment0)
            {
                formatInternal = PixelInternalFormat.Rgba8,
                pixelType = PixelType.UnsignedByte,
                wrapMode = TextureWrapMode.Repeat,
                minFilterType = TextureMinFilter.LinearMipmapNearest,
            });
            _trail = new FrameBuffer(settings);
            _trail.unbind();
            GL.BindTexture(TextureTarget.Texture2D, Texture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, Levels);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            _emptyVao = GL.GenVertexArray();
        }

        /// <summary>The trail: see the class for its channels. It repeats, so read it at world xz / <see cref="Size"/>.</summary>
        public int Texture => _trail.GetAttachment(0);

        /// <summary>For SnowTrail.glsl's snowTrailRegion: the region's centre, its size, and 1 while anything has been pressed into it.</summary>
        public Vector4 Region => new Vector4(_center.X, _center.Y, Size, _anything ? 1f : 0f);

        /// <summary>Moves the region to be centred here, in whole texels, emptying what it moves onto. Call once a frame, before stamping.</summary>
        public void Follow(Vector2 center)
        {
            Vector2i origin = new Vector2i(
                (int)MathF.Floor((center.X - Size * 0.5f) / Texel),
                (int)MathF.Floor((center.Y - Size * 0.5f) / Texel));
            _center = new Vector2((origin.X + Resolution * 0.5f) * Texel, (origin.Y + Resolution * 0.5f) * Texel);
            if (!_placed)
            {
                _origin = origin;
                _placed = true;
                _clearAll = true;
                return;
            }
            if (origin == _origin) return;

            Vector2i from = _origin;
            _origin = origin;
            if (Math.Abs(origin.X - from.X) >= Resolution || Math.Abs(origin.Y - from.Y) >= Resolution)
            {
                _clearAll = true;
                return;
            }
            // Columns it's moved onto, the whole height, then rows, the whole width.
            if (origin.X > from.X) ClearColumns(from.X + Resolution, origin.X + Resolution);
            else if (origin.X < from.X) ClearColumns(origin.X, from.X);
            if (origin.Y > from.Y) ClearRows(from.Y + Resolution, origin.Y + Resolution);
            else if (origin.Y < from.Y) ClearRows(origin.Y, from.Y);
        }

        /// <summary>Forgets every print, as when a world is left.</summary>
        public void Clear()
        {
            _clearAll = true;
            _stampCount = 0;
            _fillOwed = Vector4.Zero;
        }

        /// <summary>Snow falling, and what's kicked up settling, take this much off each channel a second, in parts of a whole one.</summary>
        public void Fill(Vector4 perSecond, float deltaTime)
        {
            _fillOwed += perSecond * Math.Clamp(deltaTime, 0f, 0.25f);
        }

        /// <summary>Presses something into the snow this frame. Pressing again where it's already been changes nothing, so a foot can press every frame it stands.</summary>
        public void Stamp(in SnowStamp stamp)
        {
            if (_stampCount >= MostStamps) return;
            Vector2 from = stamp.Center - _center;
            float reach = Reach(stamp);
            float inside = Size * 0.5f + reach;
            if (MathF.Abs(from.X) > inside || MathF.Abs(from.Y) > inside) return;
            _stamps[_stampCount++] = stamp;
        }

        public override void Update()
        {
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {
            // Where nothing has been pressed since it was emptied, the strips it moves onto are empty already, and there's nothing for snow to fill.
            if (!_anything) _clears.Clear();
            bool changed = _clearAll || _stampCount > 0 || (_anything && (_clears.Count > 0 || _fillOwed.X + _fillOwed.Y + _fillOwed.Z + _fillOwed.W >= 1f / 255f));
            if (!changed) return;
            bool culling = GL.IsEnabled(EnableCap.CullFace);
            _trail.bind();
            GL.Disable(EnableCap.DepthTest);
            GL.DepthMask(false);
            GL.Disable(EnableCap.CullFace);

            EmptyWhatsNew();
            TakeOffFill(renderEngine);
            DrawStamps();

            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.Disable(EnableCap.Blend);
            if (culling) GL.Enable(EnableCap.CullFace);
            _trail.unbind();

            GL.BindTexture(TextureTarget.Texture2D, Texture);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        private void EmptyWhatsNew()
        {
            GL.ClearColor(0f, 0f, 0f, 0f);
            if (_clearAll)
            {
                GL.Clear(ClearBufferMask.ColorBufferBit);
                _clearAll = false;
                _clears.Clear();
                _anything = false;
                return;
            }
            if (_clears.Count == 0) return;
            GL.Enable(EnableCap.ScissorTest);
            foreach (Vector4i rect in _clears)
            {
                GL.Scissor(rect.X, rect.Y, rect.Z, rect.W);
                GL.Clear(ClearBufferMask.ColorBufferBit);
            }
            GL.Disable(EnableCap.ScissorTest);
            _clears.Clear();
        }

        // Takes whole steps of the texture's precision off every texel, once enough is owed: snow falls a little at a time, and eight bits can only take it in steps.
        private void TakeOffFill(RenderEngine renderEngine)
        {
            const float Step = 1f / 255f;
            Vector4 steps = new Vector4(
                MathF.Floor(_fillOwed.X / Step), MathF.Floor(_fillOwed.Y / Step),
                MathF.Floor(_fillOwed.Z / Step), MathF.Floor(_fillOwed.W / Step)) * Step;
            if (steps.X + steps.Y + steps.Z + steps.W <= 0f) return;
            _fillOwed -= steps;
            if (!_anything) return;

            GL.Enable(EnableCap.Blend);
            GL.BlendEquation(BlendEquationMode.FuncReverseSubtract);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            _fillShader.bind();
            _fillShader.loadUniformVector4f("fill", steps);
            renderEngine.ScreenQuadRenderer.DrawQuad();
            _fillShader.unBind();
        }

        private void DrawStamps()
        {
            if (_stampCount == 0) return;
            _anything = true;

            GL.Enable(EnableCap.Blend);
            GL.BlendEquation(BlendEquationMode.Max);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            _stampShader.bind();
            GL.BindVertexArray(_emptyVao);

            int count = 0;
            for (int i = 0; i < _stampCount; i++)
            {
                ref SnowStamp stamp = ref _stamps[i];
                float reach = Reach(stamp);
                // Where in the repeating texture it falls, and a copy across each edge it runs over.
                float u = stamp.Center.X / Size, v = stamp.Center.Y / Size;
                float baseU = -MathF.Floor(u), baseV = -MathF.Floor(v);
                float fu = u + baseU, fv = v + baseV, r = reach / Size;
                int uCopies = fu + r > 1f ? -1 : fu - r < 0f ? 1 : 0;
                int vCopies = fv + r > 1f ? -1 : fv - r < 0f ? 1 : 0;
                for (int cu = 0; cu <= (uCopies != 0 ? 1 : 0); cu++)
                {
                    for (int cv = 0; cv <= (vCopies != 0 ? 1 : 0); cv++)
                    {
                        if (count == StampsPerPass) count = Flush(count);
                        Write(count++, stamp, baseU + cu * uCopies, baseV + cv * vCopies, reach);
                    }
                }
            }
            Flush(count);
            _stampCount = 0;

            GL.BindVertexArray(0);
            _stampShader.unBind();
        }

        private void Write(int slot, in SnowStamp stamp, float wrapU, float wrapV, float reach)
        {
            int o = slot * 4;
            _place[o] = stamp.Center.X; _place[o + 1] = stamp.Center.Y; _place[o + 2] = stamp.Yaw; _place[o + 3] = (float)stamp.Kind;
            _size[o] = MathF.Max(stamp.HalfWidth, 0.01f); _size[o + 1] = MathF.Max(stamp.HalfLength, 0.01f);
            _size[o + 2] = Math.Clamp(stamp.Depth / Deepest, 0f, 1f); _size[o + 3] = Math.Clamp(stamp.Press, 0f, 1f);
            _more[o] = stamp.Push.X; _more[o + 1] = stamp.Push.Y;
            _more[o + 2] = Math.Clamp(stamp.Rim / Highest, 0f, 1f); _more[o + 3] = Math.Clamp(stamp.Churn, 0f, 1f);
            _wrap[o] = wrapU; _wrap[o + 1] = wrapV; _wrap[o + 2] = reach; _wrap[o + 3] = 0f;
        }

        private int Flush(int count)
        {
            if (count == 0) return 0;
            _stampShader.loadUniformVector4fArray("stampPlace", _place, count);
            _stampShader.loadUniformVector4fArray("stampSize", _size, count);
            _stampShader.loadUniformVector4fArray("stampMore", _more, count);
            _stampShader.loadUniformVector4fArray("stampWrap", _wrap, count);
            _stampShader.loadUniformFloat("trailSize", Size);
            GL.DrawArraysInstanced(PrimitiveType.TriangleStrip, 0, 4, count);
            return 0;
        }

        // Metres out from its centre that a stamp can reach, clumps thrown ahead of it included: the half side of the square it's drawn on.
        private static float Reach(in SnowStamp stamp)
        {
            return MathF.Max(stamp.HalfWidth, stamp.HalfLength) * (1.6f + 1.2f * MathF.Min(stamp.Push.Length, 1f)) + 0.15f;
        }

        // Texel indices [from, to) of the world, the whole height of the texture.
        private void ClearColumns(int from, int to)
        {
            int start = Mod(from, Resolution), length = to - from;
            if (start + length <= Resolution) _clears.Add(new Vector4i(start, 0, length, Resolution));
            else
            {
                _clears.Add(new Vector4i(start, 0, Resolution - start, Resolution));
                _clears.Add(new Vector4i(0, 0, start + length - Resolution, Resolution));
            }
        }

        private void ClearRows(int from, int to)
        {
            int start = Mod(from, Resolution), length = to - from;
            if (start + length <= Resolution) _clears.Add(new Vector4i(0, start, Resolution, length));
            else
            {
                _clears.Add(new Vector4i(0, start, Resolution, Resolution - start));
                _clears.Add(new Vector4i(0, 0, Resolution, start + length - Resolution));
            }
        }

        private static int Mod(int a, int n) => ((a % n) + n) % n;

        public override void CleanUp()
        {
            _stampShader.cleanUp();
            _fillShader.cleanUp();
            _trail.cleanUp();
            GL.DeleteVertexArray(_emptyVao);
        }
    }
}
