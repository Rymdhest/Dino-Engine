using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Dino_LaunchLab
{
    /// <summary>
    /// The mark of the web: an alternate data stream a browser puts on what it downloads. .NET Framework refuses paths with a colon in
    /// them, so this goes straight to Windows.
    /// </summary>
    internal static class Streams
    {
        private const string Mark = ":Zone.Identifier";
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint OpenExisting = 3;
        private const uint CreateAlways = 2;

        public static void WriteMark(string path)
        {
            using SafeFileHandle handle = CreateFile(path + Mark, GenericWrite, 0, IntPtr.Zero, CreateAlways, 0, IntPtr.Zero);
            if (handle.IsInvalid) throw new Win32Exception();
            using var stream = new FileStream(handle, FileAccess.Write);
            byte[] text = Encoding.ASCII.GetBytes("[ZoneTransfer]\r\nZoneId=3\r\n");
            stream.Write(text, 0, text.Length);
        }

        public static bool HasMark(string path)
        {
            using SafeFileHandle handle = CreateFile(path + Mark, GenericRead, 1, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            return !handle.IsInvalid;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    }
}
