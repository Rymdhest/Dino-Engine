using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Win32;

namespace Dino_Launcher.Update
{
    /// <summary>
    /// There's no installer to run: the launcher is the installer. Started from wherever a browser put it, it copies itself into place, adds
    /// itself to Windows the ordinary way, and carries on from there. All of it is per user, so none of it needs administrator rights.
    /// </summary>
    public sealed class Installer
    {
        private const string DisplayName = "Dino Defenders";

        private readonly InstallPaths _paths;

        public string Desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        public string StartMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        public string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DinoDefenders";

        public Installer(InstallPaths paths)
        {
            _paths = paths;
        }

        private string DesktopShortcut => Path.Combine(Desktop, DisplayName + ".lnk");
        private string StartMenuShortcut => Path.Combine(StartMenu, DisplayName + ".lnk");

        /// <summary>
        /// Copies by reading and writing the bytes, as a file copy would bring the browser's mark of the web along. That mark is what
        /// makes Windows ask "are you sure": the player has answered it once, and shouldn't be asked again every time they play.
        /// </summary>
        public void CopyLauncher(string from)
        {
            Directory.CreateDirectory(_paths.Root);
            FileSystem.WriteAtomically(_paths.LauncherExe, File.ReadAllBytes(from));
        }

        /// <summary>
        /// Each of these is a convenience, and none is worth failing over. Windows' Controlled Folder Access, for one, stops programs it
        /// doesn't know writing to the desktop. Someone without a shortcut can still run what they downloaded.
        /// </summary>
        public void AddToWindows(string version)
        {
            Attempt("the desktop shortcut", () => CreateShortcut(DesktopShortcut));
            Attempt("the start menu shortcut", () =>
            {
                Directory.CreateDirectory(StartMenu);
                CreateShortcut(StartMenuShortcut);
            });
            Attempt("the Installed Apps entry", () =>
            {
                using RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKey);
                key.SetValue("DisplayName", DisplayName);
                key.SetValue("DisplayVersion", version);
                key.SetValue("DisplayIcon", _paths.LauncherExe);
                key.SetValue("Publisher", DisplayName);
                key.SetValue("InstallLocation", _paths.Root);
                key.SetValue("UninstallString", $"\"{_paths.LauncherExe}\" --uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            });
        }

        private static void Attempt(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is COMException || e is System.Security.SecurityException)
            {
                Log.Write($"Couldn't add {what}: {e.Message}");
            }
        }

        public void RemoveFromWindows()
        {
            FileSystem.TryDelete(DesktopShortcut);
            FileSystem.TryDelete(StartMenuShortcut);
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
        }

        /// <summary>A running exe can't delete itself, so something else does it once this has gone. Saves live elsewhere and stay.</summary>
        public void RemoveFilesAfterExit()
        {
            var remove = new ProcessStartInfo("cmd.exe", $"/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \"{_paths.Root}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetTempPath(),
            };
            Process.Start(remove)?.Dispose();
        }

        private void CreateShortcut(string path)
        {
            var link = (IShellLinkW)new ShellLink();
            link.SetPath(_paths.LauncherExe);
            link.SetWorkingDirectory(_paths.Root);
            link.SetDescription("Play Dino Defenders");
            link.SetIconLocation(_paths.LauncherExe, 0);
            ((IPersistFile)link).Save(path, true);
            Marshal.ReleaseComObject(link);
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink
        {
        }

        // Only the methods used here are called, but COM goes by position: every slot up to the last one has to be declared.
        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath(IntPtr file, int size, IntPtr data, int flags);
            void GetIDList(out IntPtr list);
            void SetIDList(IntPtr list);
            void GetDescription(IntPtr name, int size);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetWorkingDirectory(IntPtr directory, int size);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
            void GetArguments(IntPtr arguments, int size);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
            void GetHotkey(out short hotkey);
            void SetHotkey(short hotkey);
            void GetShowCmd(out int show);
            void SetShowCmd(int show);
            void GetIconLocation(IntPtr path, int size, out int icon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int icon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, int reserved);
            void Resolve(IntPtr window, int flags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
        }
    }
}
