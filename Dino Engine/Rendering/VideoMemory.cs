using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dino_Engine.Rendering
{
    /// <summary>
    /// How much video memory the game holds, two ways. The driver's own figure for this process (<see cref="Read"/>) is everything: textures, render targets, meshes and whatever
    /// the driver keeps for itself. It comes from DXGI, which every card on Windows answers, as an OpenGL context's memory is the system's like any other: the NVIDIA extension
    /// PerfRecorder used to read says nothing on AMD. And what meshes take (<see cref="Meshes"/>), counted as their buffers are made and deleted, with whatever has said how much of
    /// that is its own (<see cref="Tally"/>), which is how to tell what a world's plants or its creatures cost. Read on the main thread.
    /// </summary>
    public static unsafe class VideoMemory
    {
        /// <summary>What the driver says of this process on the adapter it draws with, in bytes. <see cref="Budget"/> is how much it may have before the system starts moving it out.</summary>
        public readonly record struct Usage(string Adapter, long Dedicated, long Used, long Budget, long SharedUsed, long SharedBudget);

        /// <summary>One holder of meshes and what it holds now, for the F3 window.</summary>
        public readonly record struct Holder(string Name, long Bytes, int Models);

        private const double ReadEvery = 0.5;     // Seconds a reading is kept: it's a call into the kernel, and nothing it says moves faster.

        private static long _meshBytes;
        private static int _meshModels;
        private static readonly List<(string Name, Func<(long Bytes, int Models)> Read)> Holders = new();

        private static bool _opened;
        private static IntPtr[] _adapters = Array.Empty<IntPtr>();
        private static string[] _names = Array.Empty<string>();
        private static long[] _dedicated = Array.Empty<long>();
        private static readonly Stopwatch Clock = new();
        private static Usage? _last;

        /// <summary>Bytes in every mesh's buffers now, and how many models hold them.</summary>
        public static (long Bytes, int Models) Meshes => (Interlocked.Read(ref _meshBytes), Volatile.Read(ref _meshModels));

        internal static void Counted(long bytes, int models)
        {
            Interlocked.Add(ref _meshBytes, bytes);
            Interlocked.Add(ref _meshModels, models);
        }

        /// <summary>Says how much of <see cref="Meshes"/> something holds, by asking it whenever the figure's wanted. A second tally by the same name takes the first one's place.</summary>
        public static void Tally(string name, Func<(long Bytes, int Models)> read)
        {
            lock (Holders)
            {
                Holders.RemoveAll(holder => holder.Name == name);
                Holders.Add((name, read));
            }
        }

        /// <summary>What each holder that has said so holds now.</summary>
        public static List<Holder> ByHolder()
        {
            var found = new List<Holder>();
            lock (Holders)
            {
                foreach ((string name, Func<(long Bytes, int Models)> read) in Holders)
                {
                    (long bytes, int models) = read();
                    found.Add(new Holder(name, bytes, models));
                }
            }
            return found;
        }

        /// <summary>The driver's figures for this process, or nothing where it can't say (not Windows, or no DXGI 1.4, which came with Windows 10). Kept for half a second.</summary>
        public static Usage? Read()
        {
            if (Clock.IsRunning && Clock.Elapsed.TotalSeconds < ReadEvery) return _last;
            Clock.Restart();
            _last = Query();
            return _last;
        }

        private static Usage? Query()
        {
            if (!OperatingSystem.IsWindows()) return null;
            try
            {
                if (!_opened) Open();
                // The adapter this process draws with is the one it holds memory on. A laptop's other adapter shows next to nothing, and asking every time costs nothing next to
                // ReadEvery, so it's never wrong if the game moves.
                Usage? best = null;
                for (int i = 0; i < _adapters.Length; i++)
                {
                    MemoryInfo local, shared;
                    // Node 0, the local segment group (the card's own memory) and then the non-local one (system memory it reaches over the bus).
                    if (QueryVideoMemoryInfo(_adapters[i], 0, 0, &local) < 0 || QueryVideoMemoryInfo(_adapters[i], 0, 1, &shared) < 0) continue;
                    if (best is Usage known && (long)local.CurrentUsage <= known.Used) continue;
                    best = new Usage(_names[i], _dedicated[i], (long)local.CurrentUsage, (long)local.Budget, (long)shared.CurrentUsage, (long)shared.Budget);
                }
                return best;
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or SEHException)
            {
                _adapters = Array.Empty<IntPtr>();
                return null;
            }
        }

        // Every hardware adapter there is, as IDXGIAdapter3, kept for good: there are two or three, and they're asked again twice a second.
        private static void Open()
        {
            _opened = true;
            Guid factoryId = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");    // IDXGIFactory1
            Guid adapter3Id = new Guid("645967a4-1392-4310-a798-8053ce3e93fd");   // IDXGIAdapter3
            IntPtr factory;
            if (CreateDXGIFactory1(&factoryId, &factory) < 0 || factory == IntPtr.Zero) return;

            var adapters = new List<IntPtr>();
            var names = new List<string>();
            var dedicated = new List<long>();
            for (uint index = 0; ; index++)
            {
                IntPtr adapter;
                if (EnumAdapters1(factory, index, &adapter) < 0) break;     // DXGI_ERROR_NOT_FOUND once there are no more.
                AdapterDesc1 desc;
                bool software = GetDesc1(adapter, &desc) < 0 || (desc.Flags & 2) != 0;    // DXGI_ADAPTER_FLAG_SOFTWARE: the basic renderer, which nobody draws with.
                IntPtr adapter3 = IntPtr.Zero;
                if (!software) QueryInterface(adapter, &adapter3Id, &adapter3);
                Release(adapter);
                if (adapter3 == IntPtr.Zero) continue;
                adapters.Add(adapter3);
                names.Add(new string(desc.Description).TrimEnd('\0'));
                dedicated.Add((long)desc.DedicatedVideoMemory);
            }
            Release(factory);
            _adapters = adapters.ToArray();
            _names = names.ToArray();
            _dedicated = dedicated.ToArray();
        }

        /// <summary>One thing on the card, as <see cref="Census"/> found it: what kind of object, its name, its size and shape, and its bytes as the driver describes it.</summary>
        public readonly record struct Held(string Kind, int Name, string Shape, long Bytes);

        /// <summary>
        /// Everything the GL context holds, by asking the driver about every name it has given out: each texture with all its levels, layers and samples, each buffer and each renderbuffer.
        /// Nothing in the engine is labelled, so what each one is has to be read off its shape. Bytes are what the formats say (bits a texel times texels), so the driver's own padding
        /// and compression aren't in them. A diagnostic, for F3 and <c>--vramlist</c>: it asks about hundreds of thousands of names, which takes a moment. Render thread only.
        /// </summary>
        public static List<Held> Census()
        {
            var found = new List<Held>();
            const int GiveUpAfter = 50000;     // Names come in order, with gaps where things were deleted: this many free in a row is the end of them.
            int misses = 0;
            for (int name = 1; misses < GiveUpAfter; name++)
            {
                bool any = false;
                if (GL.IsTexture(name)) { any = true; if (TextureOf(name) is Held texture) found.Add(texture); }
                if (GL.IsBuffer(name))
                {
                    any = true;
                    GL.GetNamedBufferParameter(name, BufferParameterName.BufferSize, out long size);
                    found.Add(new Held("buffer", name, "", size));
                }
                if (GL.IsRenderbuffer(name))
                {
                    any = true;
                    GL.GetNamedRenderbufferParameter(name, RenderbufferParameterName.RenderbufferWidth, out int width);
                    GL.GetNamedRenderbufferParameter(name, RenderbufferParameterName.RenderbufferHeight, out int height);
                    GL.GetNamedRenderbufferParameter(name, RenderbufferParameterName.RenderbufferSamples, out int samples);
                    GL.GetNamedRenderbufferParameter(name, RenderbufferParameterName.RenderbufferInternalFormat, out int format);
                    int bits = 0;
                    foreach (RenderbufferParameterName part in new[] { RenderbufferParameterName.RenderbufferRedSize, RenderbufferParameterName.RenderbufferGreenSize, RenderbufferParameterName.RenderbufferBlueSize,
                        RenderbufferParameterName.RenderbufferAlphaSize, RenderbufferParameterName.RenderbufferDepthSize, RenderbufferParameterName.RenderbufferStencilSize })
                    {
                        GL.GetNamedRenderbufferParameter(name, part, out int partBits);
                        bits += partBits;
                    }
                    found.Add(new Held("renderbuffer", name, $"{width}x{height} {FormatName(format)} x{Math.Max(samples, 1)}", (long)width * height * Math.Max(samples, 1) * bits / 8));
                }
                misses = any ? 0 : misses + 1;
            }
            found.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
            return found;
        }

        // A texture's size over all its levels, layers and samples, or nothing for a buffer texture, whose bytes are its buffer's.
        private static Held? TextureOf(int name)
        {
            GL.GetTextureParameter(name, (GetTextureParameter)0x1006, out int target);     // GL_TEXTURE_TARGET
            var kind = (TextureTarget)target;
            if (kind == TextureTarget.TextureBuffer) return null;
            bool cube = kind is TextureTarget.TextureCubeMap;
            // A multisampled texture has one level, but the driver answers for any level asked of it, and the census counted it twenty times over.
            int levels = kind is TextureTarget.Texture2DMultisample or TextureTarget.Texture2DMultisampleArray or TextureTarget.TextureRectangle ? 1 : 20;
            long bytes = 0;
            string shape = "";
            for (int level = 0; level < levels; level++)
            {
                GL.GetTextureLevelParameter(name, level, GetTextureParameter.TextureWidth, out int width);
                if (width == 0) break;
                GL.GetTextureLevelParameter(name, level, GetTextureParameter.TextureHeight, out int height);
                GL.GetTextureLevelParameter(name, level, GetTextureParameter.TextureDepth, out int depth);
                GL.GetTextureLevelParameter(name, level, GetTextureParameter.TextureSamples, out int samples);
                GL.GetTextureLevelParameter(name, level, GetTextureParameter.TextureCompressed, out int compressed);
                long texels = (long)width * Math.Max(height, 1) * Math.Max(depth, 1) * Math.Max(samples, 1) * (cube ? 6 : 1);
                if (compressed != 0)
                {
                    GL.GetTextureLevelParameter(name, level, GetTextureParameter.TextureCompressedImageSize, out int size);
                    bytes += size * (cube ? 6L : 1L);
                }
                else
                {
                    int bits = 0;
                    foreach (GetTextureParameter part in new[] { GetTextureParameter.TextureRedSize, GetTextureParameter.TextureGreenSize, GetTextureParameter.TextureBlueSize,
                        GetTextureParameter.TextureAlphaSize, GetTextureParameter.TextureDepthSize, GetTextureParameter.TextureStencilSize })
                    {
                        GL.GetTextureLevelParameter(name, level, part, out int partBits);
                        bits += partBits;
                    }
                    bytes += texels * bits / 8;
                }
                if (level == 0)
                {
                    GL.GetTextureLevelParameter(name, 0, GetTextureParameter.TextureInternalFormat, out int format);
                    shape = $"{kind.ToString().Replace("Texture", "")} {width}x{height}{(depth > 1 ? $"x{depth}" : "")} {FormatName(format)}{(samples > 1 ? $" x{samples}" : "")}";
                }
                else shape = shape.Split(" levels")[0] + $" levels {level + 1}";
            }
            return new Held("texture", name, shape, bytes);
        }

        private static string FormatName(int format) => Enum.IsDefined(typeof(SizedInternalFormat), format) ? ((SizedInternalFormat)format).ToString() : Enum.IsDefined(typeof(PixelInternalFormat), format) ? ((PixelInternalFormat)format).ToString() : $"0x{format:X}";

        /// <summary><see cref="Census"/> as lines for the console: totals by kind, the same shape counted together, largest first.</summary>
        public static string CensusText(int lines = 60)
        {
            const double megabyte = 1024.0 * 1024.0;
            List<Held> all = Census();
            var text = new System.Text.StringBuilder();
            text.AppendLine(FormattableString.Invariant($"Video memory census: {all.Sum(h => h.Bytes) / megabyte:0} MB in {all.Count} objects"));
            foreach (var kind in all.GroupBy(h => h.Kind).OrderByDescending(g => g.Sum(h => h.Bytes)))
            {
                text.AppendLine(FormattableString.Invariant($"  {kind.Key,-13}{kind.Sum(h => h.Bytes) / megabyte,8:0} MB in {kind.Count()}"));
            }
            // Buffers are grouped by size band, as their shape says nothing; textures by shape.
            string GroupOf(Held held) => held.Kind == "buffer"
                ? held.Bytes >= 16 * 1024 * 1024 ? $"buffer {held.Bytes / megabyte:0} MB" : held.Bytes >= 1024 * 1024 ? "buffers 1-16 MB" : "buffers under 1 MB"
                : $"{held.Kind} {held.Shape}";
            foreach (var group in all.GroupBy(GroupOf).OrderByDescending(g => g.Sum(h => h.Bytes)).Take(lines))
            {
                text.AppendLine(FormattableString.Invariant($"  {group.Sum(h => h.Bytes) / megabyte,8:0.0} MB  {group.Count(),5} x  {group.Key}"));
            }
            return text.ToString().TrimEnd();
        }

        [DllImport("dxgi.dll", ExactSpelling = true)]
        private static extern int CreateDXGIFactory1(Guid* riid, IntPtr* factory);

        // COM by its vtable, as nothing else is needed of it and nothing here then rests on the runtime's COM support. Slots: IUnknown 0 to 2, IDXGIObject 3 to 6, IDXGIFactory 7 to 11,
        // IDXGIFactory1's EnumAdapters1 12; IDXGIAdapter 7 to 9, IDXGIAdapter1's GetDesc1 10, IDXGIAdapter2's GetDesc2 11, IDXGIAdapter3's QueryVideoMemoryInfo 14.
        private static IntPtr Slot(IntPtr obj, int slot) => (*(IntPtr**)obj)[slot];

        private static int QueryInterface(IntPtr obj, Guid* id, IntPtr* found) => ((delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)Slot(obj, 0))(obj, id, found);
        private static uint Release(IntPtr obj) => ((delegate* unmanaged[Stdcall]<IntPtr, uint>)Slot(obj, 2))(obj);
        private static int EnumAdapters1(IntPtr factory, uint index, IntPtr* adapter) => ((delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)Slot(factory, 12))(factory, index, adapter);
        private static int GetDesc1(IntPtr adapter, AdapterDesc1* desc) => ((delegate* unmanaged[Stdcall]<IntPtr, AdapterDesc1*, int>)Slot(adapter, 10))(adapter, desc);
        private static int QueryVideoMemoryInfo(IntPtr adapter, uint node, int group, MemoryInfo* info) => ((delegate* unmanaged[Stdcall]<IntPtr, uint, int, MemoryInfo*, int>)Slot(adapter, 14))(adapter, node, group, info);

        // DXGI_QUERY_VIDEO_MEMORY_INFO.
        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryInfo
        {
            public ulong Budget;
            public ulong CurrentUsage;
            public ulong AvailableForReservation;
            public ulong CurrentReservation;
        }

        // DXGI_ADAPTER_DESC1.
        [StructLayout(LayoutKind.Sequential)]
        private struct AdapterDesc1
        {
            public fixed char Description[128];
            public uint VendorId;
            public uint DeviceId;
            public uint SubSysId;
            public uint Revision;
            public nuint DedicatedVideoMemory;
            public nuint DedicatedSystemMemory;
            public nuint SharedSystemMemory;
            public uint LuidLow;
            public int LuidHigh;
            public uint Flags;
        }
    }
}
