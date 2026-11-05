using System.Runtime.InteropServices;

namespace Dino_Engine.Sound.Output
{
    // The part of Windows Core Audio this needs, declared by hand so the engine depends on nothing that isn't already on the machine.
    // The order of methods in each interface is its vtable and must not change, unused ones included.

    internal enum DataFlow
    {
        Render = 0,
        Capture = 1,
    }

    internal enum DeviceRole
    {
        Console = 0,
        Multimedia = 1,
    }

    internal static class Wasapi
    {
        public const uint ClsCtxAll = 0x17;
        public const int ShareModeShared = 0;

        public const uint StreamFlagsLoopback = 0x00020000;
        public const uint StreamFlagsEventCallback = 0x00040000;
        public const uint StreamFlagsAutoConvertPcm = 0x80000000;
        public const uint StreamFlagsSrcDefaultQuality = 0x08000000;

        public const int DeviceInvalidated = unchecked((int)0x88890004);
        public const int ResourcesInvalidated = unchecked((int)0x88890026);
        public const int ServiceNotRunning = unchecked((int)0x88890010);
        public const int ElementNotFound = unchecked((int)0x80070490);

        public static readonly Guid AudioClient = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
        public static readonly Guid RenderClient = new("F294ACFC-3146-4483-A7BF-ADDCA7C260E2");
        public static readonly Guid CaptureClient = new("C8ADBD64-E71E-48A0-A4DE-185C395CD317");

        public static void Check(int result, string what)
        {
            if (result < 0) throw new COMException($"{what} failed (0x{result:X8}).", result);
        }

        [DllImport("avrt.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr AvSetMmThreadCharacteristics(string taskName, ref uint taskIndex);

        [DllImport("avrt.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AvRevertMmThreadCharacteristics(IntPtr handle);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    internal struct WaveFormat
    {
        public ushort FormatTag;
        public ushort Channels;
        public uint SamplesPerSecond;
        public uint AverageBytesPerSecond;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort ExtraSize;

        public const ushort IeeeFloat = 3;

        public static WaveFormat Float(int rate, int channels) => new WaveFormat
        {
            FormatTag = IeeeFloat,
            Channels = (ushort)channels,
            SamplesPerSecond = (uint)rate,
            AverageBytesPerSecond = (uint)(rate * channels * 4),
            BlockAlign = (ushort)(channels * 4),
            BitsPerSample = 32,
        };
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorObject
    {
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(DataFlow dataFlow, uint stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(DataFlow dataFlow, DeviceRole role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid interfaceId, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object result);
        [PreserveSig] int OpenPropertyStore(uint access, out IntPtr store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, uint streamFlags, long bufferDuration, long periodicity, ref WaveFormat format, IntPtr audioSessionGuid);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint frames);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr eventHandle);
        [PreserveSig] int GetService(ref Guid interfaceId, [MarshalAs(UnmanagedType.IUnknown)] out object result);
    }

    [ComImport, Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioRenderClient
    {
        [PreserveSig] int GetBuffer(uint frames, out IntPtr data);
        [PreserveSig] int ReleaseBuffer(uint frames, uint flags);
    }

    [ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devicePosition, out ulong performanceCounter);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }
}
