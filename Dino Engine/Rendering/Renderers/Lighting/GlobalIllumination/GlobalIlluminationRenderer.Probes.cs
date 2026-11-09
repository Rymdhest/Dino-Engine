using Dino_Engine.Core;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.Lighting.GlobalIllumination
{
    // The radiance cascades: probes in the world near what the camera sees, each cascade twice as far apart as the one before, with four
    // times the directions, looking four times as far (Sannikov 2023; Freeman, Sannikov and Pitt-Francis 2026). Which probes there are is
    // worked out again every frame, from what's on screen; what a probe found out about where it stands is remembered from frame to frame,
    // as the world doesn't change, and the coarse cascades, which see far and whose light changes slowly, look again a few frames apart.
    // The finest two look every frame, so what a fire or a spell does nearby is seen the frame it does it.
    public partial class GlobalIlluminationRenderer
    {
        private const int MarkStride = 4;   // Pixels apart that the G-buffer is read for which probes are wanted.

        /// <summary>Frames between the times a remembered probe of each cascade looks along its directions again.</summary>
        private static readonly int[] RefreshPeriod = { 1, 1, 2, 4, 8 };

        private readonly Vector2i[] _columnOrigin = new Vector2i[Cascades];
        private readonly Vector2i[] _previousColumnOrigin = new Vector2i[Cascades];

        // What's built every frame, twice over, as this frame's probes are found among last frame's: [current] and [1 - current].
        private readonly int[] _columnMask = new int[2];
        private readonly int[] _columnBase = new int[2];
        private readonly int[] _columnOffset = new int[2];
        private readonly int[] _probePlace = new int[2];
        private readonly int[,] _intervals = new int[2, Cascades];
        private int _current;
        private bool _remembered;

        private int _probeKey;
        private int _probeFresh;
        private int _probeParent;
        private int _probePrevious;
        private int _previousStart;
        private int _probeSH;
        private readonly int[] _merged = new int[Cascades];
        private int _gathered;
        private int _gatheredBefore;            // Last frame's, which this frame's settles from (GI_Gather.comp).
        private bool _gatheredHistory;          // Whether last frame's was gathered, at this size, in this world.
        private Matrix4 _previousView, _previousViewProjection;
        private Vector2i _gatherSize;

        private ShaderProgram _probeClearShader = null!;
        private ShaderProgram _probeMarkShader = null!;
        private ShaderProgram _probeParentsShader = null!;
        private ShaderProgram _probeCountShader = null!;
        private ShaderProgram _probeArgsShader = null!;
        private ShaderProgram _probeListShader = null!;
        private ShaderProgram _probeRecallShader = null!;
        private ShaderProgram _probeInitShader = null!;
        private ShaderProgram _probeFreshArgsShader = null!;
        private ShaderProgram _probePlaceShader = null!;
        private ShaderProgram _probeSeeShader = null!;
        private ShaderProgram _probeTraceShader = null!;
        private ShaderProgram _probeMergeShader = null!;
        private ShaderProgram _probeSHShader = null!;
        private ShaderProgram _gatherShader = null!;

        private static int Outward(int c) => c == 0 ? DirectionCount[0] : DirectionCount[c - 1];

        private void CreateProbeResources()
        {
            int columns = ColumnStart[Cascades];
            int probes = ProbeBase[Cascades];
            for (int i = 0; i < 2; i++)
            {
                _columnMask[i] = CreateBuffer(columns * sizeof(uint));
                _columnBase[i] = CreateBuffer(columns * sizeof(int));
                _columnOffset[i] = CreateBuffer(columns * sizeof(uint));
                _probePlace[i] = CreateBuffer(probes * 4 * sizeof(float));
                for (int c = 0; c < Cascades; c++)
                {
                    // Only what's kept from frame to frame needs a second copy.
                    if (i == 1 && RefreshPeriod[c] == 1) _intervals[i, c] = _intervals[0, c];
                    else _intervals[i, c] = CreateBuffer(ProbeCapacity[c] * DirectionCount[c] * sizeof(uint));
                }
            }
            _probeKey = CreateBuffer(probes * 2 * sizeof(uint));
            _probeFresh = CreateBuffer(probes * sizeof(int));
            _probeParent = CreateBuffer(probes * 8 * sizeof(int));
            _probePrevious = CreateBuffer(probes * sizeof(int));
            _previousStart = CreateBuffer(8 * sizeof(uint));
            _probeSH = CreateBuffer(ProbeBase[2] * 3 * 4 * sizeof(float));
            for (int c = 0; c < Cascades; c++) _merged[c] = CreateBuffer(ProbeCapacity[c] * Outward(c) * sizeof(uint));
            _report = CreateBuffer(ReportLength * sizeof(float));
            CreateGathered();

            _probeClearShader = new ShaderProgram("GI/GI_Probe_Clear.comp");
            _probeMarkShader = new ShaderProgram("GI/GI_Probe_Mark.comp");
            _probeParentsShader = new ShaderProgram("GI/GI_Probe_Parents.comp");
            _probeCountShader = new ShaderProgram("GI/GI_Probe_Count.comp");
            _probeArgsShader = new ShaderProgram("GI/GI_Probe_Args.comp");
            _probeListShader = new ShaderProgram("GI/GI_Probe_List.comp");
            _probeRecallShader = new ShaderProgram("GI/GI_Probe_Recall.comp");
            _probeInitShader = new ShaderProgram("GI/GI_Probe_Init.comp");
            _probeFreshArgsShader = new ShaderProgram("GI/GI_Probe_FreshArgs.comp");
            _probePlaceShader = new ShaderProgram("GI/GI_Probe_Place.comp");
            _probeSeeShader = new ShaderProgram("GI/GI_Probe_See.comp");
            _probeTraceShader = new ShaderProgram("GI/GI_Probe_Trace.comp");
            _probeMergeShader = new ShaderProgram("GI/GI_Probe_Merge.comp");
            _probeSHShader = new ShaderProgram("GI/GI_Probe_SH.comp");
            _gatherShader = new ShaderProgram("GI/GI_Gather.comp");

            _probeClearShader.bind();
            LinkShared(_probeClearShader, height: true);
            _probeMarkShader.bind();
            _probeMarkShader.loadUniformInt("gDepth", UnitDepth);
            _probeMarkShader.loadUniformInt("gNormal", UnitNormal);
            _probePlaceShader.bind();
            LinkShared(_probePlaceShader, bricks: true);
            _probeSeeShader.bind();
            LinkShared(_probeSeeShader, bricks: true);
            _probeTraceShader.bind();
            LinkShared(_probeTraceShader, bricks: true, index: true);
            _probeMergeShader.bind();
            _probeMergeShader.loadUniformInt("skyMap", UnitSky);
            _gatherShader.bind();
            LinkShared(_gatherShader, bricks: true);
            _gatherShader.loadUniformInt("gDepth", UnitDepth);
            _gatherShader.loadUniformInt("gNormal", UnitNormal);
            _gatherShader.loadUniformInt("previousGathered", UnitHistory);
            _gatherShader.unBind();
        }

        private void CreateGathered()
        {
            if (_gathered != 0) GL.DeleteTexture(_gathered);
            if (_gatheredBefore != 0) GL.DeleteTexture(_gatheredBefore);
            _gatherSize = new Vector2i((Engine.Resolution.X + 1) / 2, (Engine.Resolution.Y + 1) / 2);
            _gathered = NewGathered();
            _gatheredBefore = NewGathered();
            _gatheredHistory = false;
        }

        private int NewGathered()
        {
            int texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, texture);
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, 1, SizedInternalFormat.Rgba16f, _gatherSize.X, _gatherSize.Y, 4);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2DArray, 0);
            return texture;
        }

        /// <summary>Forgets every probe: the next frame works them all out afresh, and nothing gathered before is carried on.</summary>
        private void ForgetProbes()
        {
            _remembered = false;
            _gatheredHistory = false;
        }

        private void BindProbeBuffers()
        {
            int now = _current, then = 1 - _current;
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 5, _columnMask[now]);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 6, _columnBase[now]);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 7, _columnOffset[now]);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 8, _probeKey);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 9, _probePlace[now]);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 10, _probeParent);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 16, _probeSH);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 17, _columnMask[then]);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 18, _columnBase[then]);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 19, _columnOffset[then]);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 20, _probePlace[then]);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 22, _previousStart);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 23, _probePrevious);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 27, _probeFresh);
        }

        private static IntPtr DispatchOffset(int cascade, int pass) => (IntPtr)((CountProbeDispatch + (cascade * 3 + pass) * 4) * sizeof(uint));

        // The passes over the probes placed afresh: 0 to place them, 1 to ask which parents they can see (GI_Probe_FreshArgs.comp).
        private static IntPtr FreshOffset(int pass) => (IntPtr)((CountFreshDispatch + pass * 4) * sizeof(uint));

        private void UpdateProbes(RenderEngine engine)
        {
           int columns = ColumnStart[Cascades];
            int now = _current, then = 1 - _current;
            BindProbeBuffers();
            BindTexture(UnitDepth, TextureTarget.Texture2D, engine.GBuffer.getDepthAttachment());
            BindTexture(UnitNormal, TextureTarget.Texture2D, engine.GBuffer.GetAttachment(1));
            BindTexture(UnitSky, TextureTarget.Texture2D, engine._weatherRenderer.SkyMapTexture);
            GL.ActiveTexture(TextureUnit.Texture0);

            // Which probes are wanted: around what's on screen, then the parents of all of them.
            Engine.PerformanceMonitor.startGPUTask("GI Probe Find");
            _probeClearShader.bind();
            Dispatch(Groups(columns, 256));
            Barrier();

            _probeMarkShader.bind();
            _probeMarkShader.loadUniformInt("stride", MarkStride);
            Dispatch(Groups(Groups(Engine.Resolution.X, MarkStride), 8), Groups(Groups(Engine.Resolution.Y, MarkStride), 8));
            Barrier();

            _probeParentsShader.bind();
            for (int c = 0; c < Cascades - 1; c++)
            {
                _probeParentsShader.loadUniformInt("cascade", c);
                Dispatch(Groups(ColumnStart[c + 1] - ColumnStart[c], 64));
                Barrier();
            }

            // Numbered, laid out, and how big each pass is.
            _probeCountShader.bind();
            Dispatch(Groups(columns, 256));
            Barrier();
            int groups = Scan(_columnOffset[now], columns);
            BindProbeBuffers();
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 14, _scanSums);
            _probeArgsShader.bind();
            _probeArgsShader.loadUniformInt("groups", groups);
            Dispatch(1);
            Barrier();
            _probeListShader.bind();
            Dispatch(Groups(columns, 256));
            Barrier();

            // Each found among last frame's.
            _probeRecallShader.bind();
            _probeRecallShader.loadUniformInt("remembered", _remembered ? 1 : 0);
            for (int c = 0; c < Cascades; c++)
            {
                _probeRecallShader.loadUniformInt("cascade", c);
                _probeRecallShader.loadUniformVector2i("previousOrigin", _previousColumnOrigin[c]);
                GL.DispatchComputeIndirect(DispatchOffset(c, 0));
            }
            Barrier();

            Engine.PerformanceMonitor.finishGPUTask("GI Probe Find");
            Engine.PerformanceMonitor.startGPUTask("GI Probe Setup");
            // Where each stands: where it stood last frame, or else somewhere it has room, and then which of its parents it can see.
            // Every cascade at once each time, as nothing in one waits on another until the parents are asked about.
            _probeInitShader.bind();
            for (int c = 0; c < Cascades; c++)
            {
                _probeInitShader.loadUniformInt("cascade", c);
                GL.DispatchComputeIndirect(DispatchOffset(c, 0));
            }
            Barrier();
            _probeFreshArgsShader.bind();
            Dispatch(1);
            Barrier();
            _probePlaceShader.bind();
            GL.DispatchComputeIndirect(FreshOffset(0));
            Barrier();
            _probeSeeShader.bind();
            GL.DispatchComputeIndirect(FreshOffset(1));
            Barrier();
            Engine.PerformanceMonitor.finishGPUTask("GI Probe Setup");

            // What each sees over its own stretch, or saw a few frames ago.
            Engine.PerformanceMonitor.startGPUTask("GI Probe Trace");
            _probeTraceShader.bind();
            _probeTraceShader.loadUniformInt("frame", _frame);
            for (int c = 0; c < Cascades; c++)
            {
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 11, _intervals[now, c]);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 21, _intervals[then, c]);
                _probeTraceShader.loadUniformInt("cascade", c);
                _probeTraceShader.loadUniformInt("period", _remembered ? RefreshPeriod[c] : 1);
                GL.DispatchComputeIndirect(DispatchOffset(c, 1));
            }
            Barrier();
            Engine.PerformanceMonitor.finishGPUTask("GI Probe Trace");

            Engine.PerformanceMonitor.startGPUTask("GI Probe Merge");
            // Merged from the coarsest down.
            _probeMergeShader.bind();
            for (int c = Cascades - 1; c >= 0; c--)
            {
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 11, _intervals[now, c]);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 12, _merged[c]);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 13, _merged[Math.Min(c + 1, Cascades - 1)]);
                _probeMergeShader.loadUniformInt("cascade", c);
                GL.DispatchComputeIndirect(DispatchOffset(c, 2));
                Barrier();
            }

            // As light on a surface facing any way, for the finest and, to fall back on, the next.
            _probeSHShader.bind();
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 11, _intervals[now, 0]);
            for (int c = 0; c < 2; c++)
            {
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 12, _merged[c]);
                _probeSHShader.loadUniformInt("cascade", c);
                GL.DispatchComputeIndirect(DispatchOffset(c, 0));
            }
            Barrier();
            Engine.PerformanceMonitor.finishGPUTask("GI Probe Merge");
        }

        // This frame's probes become last frame's: where each cascade's start, and which way round the buffers are.
        private void RememberProbes()
        {
            GL.BindBuffer(BufferTarget.CopyReadBuffer, _counts);
            GL.BindBuffer(BufferTarget.CopyWriteBuffer, _previousStart);
            GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, (IntPtr)(20 * sizeof(uint)), IntPtr.Zero, 8 * sizeof(uint));
            GL.BindBuffer(BufferTarget.CopyReadBuffer, 0);
            GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0);
            Array.Copy(_columnOrigin, _previousColumnOrigin, Cascades);
            _current = 1 - _current;
            _remembered = true;
        }

        private void Gather(RenderEngine engine)
        {
            BindProbeBuffers();
            BindTexture(UnitDepth, TextureTarget.Texture2D, engine.GBuffer.getDepthAttachment());
            BindTexture(UnitNormal, TextureTarget.Texture2D, engine.GBuffer.GetAttachment(1));
            (_gathered, _gatheredBefore) = (_gatheredBefore, _gathered);
            BindTexture(UnitHistory, TextureTarget.Texture2DArray, _gatheredBefore);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindImageTexture(0, _gathered, 0, true, 0, TextureAccess.WriteOnly, SizedInternalFormat.Rgba16f);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 26, _report);
            _gatherShader.bind();
            _gatherShader.loadUniformVector2i("outSize", _gatherSize);
            _gatherShader.loadUniformInt("nearest", _settings.Debug == 5 ? 1 : 0);
            _gatherShader.loadUniformVector2i("probePixel", ReportedTexel());
            // Each probe on its own is shown as it is now, with nothing settling.
            bool settle = _gatheredHistory && _settings.Settle > 0f && _settings.Debug != 5;
            _gatherShader.loadUniformFloat("keep", settle ? MathF.Exp(-Engine.Delta / _settings.Settle) : 0f);
            _gatherShader.loadUniformMatrix4f("previousView", _previousView);
            _gatherShader.loadUniformMatrix4f("previousViewProjection", _previousViewProjection);
            Dispatch(Groups(_gatherSize.X, 8), Groups(_gatherSize.Y, 8));
            Barrier();
            _previousView = engine.context.viewMatrix;
            _previousViewProjection = engine.context.viewMatrix * engine.context.projectionMatrix;
            _gatheredHistory = true;
        }

        // ---- What a pixel's probes did, for the console while a debug view is up (--render giprobe=x:y).

        private const int ReportLength = (4 + 16 * 5) * 4;
        private int _report;

        private Vector2i ReportedTexel()
        {
            if (_settings.Debug == 0 || _settings.ProbeX < 0 || _settings.ProbeY < 0) return new Vector2i(-1, -1);
            return new Vector2i(_settings.ProbeX / 2, (Engine.Resolution.Y - 1 - _settings.ProbeY) / 2);
        }

        private static readonly string[] Reasons = { "none", "buried", "behind", "weightless", "blocked", "used" };

        private void ReportProbes()
        {
            if (ReportedTexel().X < 0) return;
            var data = new float[ReportLength];
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _report);
            GL.GetBufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, data.Length * sizeof(float), data);
            Vector4 At(int i) => new Vector4(data[i * 4], data[i * 4 + 1], data[i * 4 + 2], data[i * 4 + 3]);
            Vector4 world = At(0), surface = At(1), normal = At(2), from = At(3);
            Console.WriteLine($"GI probe report at ({_settings.ProbeX}, {_settings.ProbeY}): world {world.Xyz:0.00} surface {surface.Xyz:0.00} " +
                $"normal {normal.Xyz:0.00} from {from.Xyz:0.00} total {from.W:0.000}, last frame's counted {normal.W:0.00}");
            for (int k = 0; k < 16; k++)
            {
                int at = 4 + k * 5;
                Vector4 place = At(at), info = At(at + 1);
                int reason = (int)info.Z;
                if (reason == 0) continue;
                int c = (int)info.X;
                int slot = BitConverter.SingleToInt32Bits(info.Y);
                uint flags = BitConverter.SingleToUInt32Bits(place.W);
                Vector4 r = At(at + 2), g = At(at + 3), b = At(at + 4);
                Vector3 up = new Vector3(r.X + r.Z, g.X + g.Z, b.X + b.Z) / MathF.PI;
                Vector3 n = normal.Xyz;
                Vector3 lit = new Vector3(r.X + Vector3.Dot(r.Yzw, n), g.X + Vector3.Dot(g.Yzw, n), b.X + Vector3.Dot(b.Yzw, n)) / MathF.PI;
                Console.WriteLine($"  c{c} corner {k & 7} {Reasons[Math.Clamp(reason, 0, 5)]} w {info.W:0.000} at {place.Xyz:0.00} " +
                    $"seen {Convert.ToString((flags >> 8) & 0xFF, 2).PadLeft(8, '0')} usable {Convert.ToString((flags >> 16) & 0xFF, 2).PadLeft(8, '0')} " +
                    $"open {flags >> 24} buried {flags & 1}; E/pi up {up.X:0.00} {up.Y:0.00} {up.Z:0.00}, on the normal {lit.X:0.00} {lit.Y:0.00} {lit.Z:0.00}");
                if (reason != 5 || c > 1) continue;
                int local = slot - ProbeBase[c];
                var merged = new uint[12];
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _merged[c]);
                GL.GetBufferSubData(BufferTarget.ShaderStorageBuffer, (IntPtr)(local * 12 * sizeof(uint)), 12 * sizeof(uint), merged);
                var own = new uint[12];
                if (c == 0)
                {
                    GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _intervals[0, 0]);
                    GL.GetBufferSubData(BufferTarget.ShaderStorageBuffer, (IntPtr)(local * 12 * sizeof(uint)), 12 * sizeof(uint), own);
                }
                for (int d = 0; d < 12; d++)
                {
                    Vector3 dir = HealPix.Centre(1, d);
                    Vector3 m = UnpackRadiance(merged[d]);
                    string mine = c != 0 ? "" : own[d] == 0xFFFFFFFFu ? " own: open" : $" own: {UnpackRadiance(own[d]):0.00}";
                    Console.WriteLine($"    dir {dir:0.00}: {m:0.00}{mine}");
                }
            }
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        private static Vector3 UnpackRadiance(uint p)
        {
            static float Half(uint bits) => (float)BitConverter.UInt16BitsToHalf((ushort)bits);
            return new Vector3(Half((p & 0x7FFu) << 4), Half(((p >> 11) & 0x7FFu) << 4), Half(((p >> 22) & 0x3FFu) << 5));
        }

        private void CleanUpProbes()
        {
            var buffers = new HashSet<int> { _probeKey, _probeFresh, _probeParent, _probePrevious, _previousStart, _probeSH, _report };
            for (int i = 0; i < 2; i++)
            {
                buffers.Add(_columnMask[i]);
                buffers.Add(_columnBase[i]);
                buffers.Add(_columnOffset[i]);
                buffers.Add(_probePlace[i]);
                for (int c = 0; c < Cascades; c++) buffers.Add(_intervals[i, c]);
            }
            foreach (int buffer in _merged) buffers.Add(buffer);
            foreach (int buffer in buffers) GL.DeleteBuffer(buffer);
            if (_gathered != 0) GL.DeleteTexture(_gathered);
            if (_gatheredBefore != 0) GL.DeleteTexture(_gatheredBefore);
            _gatheredBefore = 0;
            foreach (ShaderProgram shader in new[] { _probeClearShader, _probeMarkShader, _probeParentsShader, _probeCountShader, _probeArgsShader,
                _probeListShader, _probeRecallShader, _probeInitShader, _probeFreshArgsShader, _probePlaceShader, _probeSeeShader, _probeTraceShader,
                _probeMergeShader, _probeSHShader, _gatherShader })
            {
                shader.cleanUp();
            }
        }
    }
}
