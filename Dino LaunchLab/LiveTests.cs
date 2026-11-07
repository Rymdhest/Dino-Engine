using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Dino_Launcher.Update;

namespace Dino_LaunchLab
{
    /// <summary>
    /// The exe a tester would run, and nothing else: real windows, real processes, real self-replacement, against a local feed. It trusts
    /// only the real public key, so these sign with the real private key. That doubles as a check that the two belong together. What's
    /// signed never leaves this machine, and everything is installed under a temporary folder with --root.
    /// </summary>
    internal static class LiveTests
    {
        private static string _root = "";
        private static string _feed = "";
        private static string _work = "";
        private static string _gameFolder = "";
        private static string _keyPath = "";
        private static FaultyServer _server = null!;
        private static ReleaseTool _tool = null!;
        private static InstallPaths _paths = null!;
        private static string _realLauncher = "";

        public static int Run()
        {
            var report = new Report();

            _keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dino-release", "signing-key.pem");
            if (!File.Exists(_keyPath))
            {
                report.Note($"No signing key at {_keyPath}: these drive the shipped exe, which only believes that key. Nothing tested.");
                return report.Finish();
            }

            _realLauncher = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Feed.LauncherName);
            using var folder = new TemporaryFolder();
            _work = folder.Path;
            _root = Path.Combine(_work, "Player Föld€r");
            _feed = Path.Combine(_work, "feed");
            _gameFolder = Path.Combine(_work, "build");
            _paths = new InstallPaths(_root);
            _tool = new ReleaseTool(_work);

            using var server = new FaultyServer();
            _server = server;
            server.RedirectToCdn = true;
            server.Content = path =>
            {
                string file = Path.Combine(_feed, path.Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(file) ? File.ReadAllBytes(file) : null;
            };

            try
            {
                report.Section("A tester's first run");
                FirstRun(report);

                report.Section("Opening it again");
                NothingNew(report);
                RunningAKeptCopy(report);

                report.Section("The launcher replacing itself");
                ReplacingItself(report);
                ReplacedByGarbage(report);
                ReplacedByOneThatDies(report);
                TakingOverQuietly(report);

                report.Section("Playing");
                Playing(report);
                Crashing(report);

                report.Section("One at a time");
                OneAtATime(report);

                report.Section("The window");
                Resizing(report);
                ReadingTheNotes(report);

                report.Section("What's believed");
                ForgedFeed(report);

                report.Section("No connection");
                Offline(report);
            }
            finally
            {
                foreach (Process stray in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Feed.LauncherName)))
                {
                    using (stray)
                    {
                        if (stray.MainModule?.FileName.StartsWith(_work, StringComparison.OrdinalIgnoreCase) == true) stray.Kill();
                    }
                }
            }

            return report.Finish();
        }

        private static void Release(int build, string launcher, int exitCode = 0, string? notes = null)
        {
            Dictionary<string, byte[]> game = TestFeed.Build(build);
            game["Dino Defenders.exe"] = File.ReadAllBytes(Process.GetCurrentProcess().MainModule!.FileName);
            game["exit-code.txt"] = Encoding.ASCII.GetBytes($"{exitCode}");
            game["Shaders/Build.txt"] = Encoding.ASCII.GetBytes($"{build}");
            ReleaseTool.WriteBuild(_gameFolder, game);

            int code = _tool.Run("publish", "--key", _keyPath, "--game", _gameFolder, "--launcher", launcher, "--executable", "Dino Defenders.exe", "--folder", _feed, "--notes", notes ?? $"Build {build}");
            if (code != 0) throw new InvalidOperationException(_tool.Output);
        }

        private static string Variant(string name, byte tail)
        {
            string path = Path.Combine(_work, name);
            byte[] bytes = File.ReadAllBytes(_realLauncher);
            File.WriteAllBytes(path, bytes.Concat(new[] { tail, tail, tail }).ToArray());
            return path;
        }

        /// <summary>Runs a launcher to its end, and waits for whatever it handed over to.</summary>
        private static int Launch(string exe, string extra, int seconds = 60)
        {
            var start = new ProcessStartInfo(exe, $"--root \"{_root}\" --feed \"{_server.Url}\" {extra}") { UseShellExecute = false };
            using Process process = Process.Start(start)!;
            if (!process.WaitForExit(seconds * 1000))
            {
                process.Kill();
                return -1;
            }
            int code = process.ExitCode;
            WaitForLauncherToClose(seconds);
            return code;
        }

        private static void WaitForLauncherToClose(int seconds)
        {
            Thread.Sleep(300);
            DateTime giveUp = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < giveUp)
            {
                using var instance = new InstanceLock(_paths);
                if (instance.Take()) return;
                Thread.Sleep(100);
            }
        }

        private static string LauncherLog() => File.Exists(Path.Combine(_paths.Logs, "launcher.log")) ? ReadShared(Path.Combine(_paths.Logs, "launcher.log")) : "";

        private static string ReadShared(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static Manifest? Installed() => new GameInstall(_paths).ReadInstalled(Feed.PublicKey);

        private static bool Whole(Manifest? manifest) => manifest != null && GameInstall.FindWrong(_paths.Game, manifest, CancellationToken.None).Count == 0;

        private static void FirstRun(Report report)
        {
            Release(1, _realLauncher);

            string downloads = Path.Combine(_work, "Downloads");
            Directory.CreateDirectory(downloads);
            string download = Path.Combine(downloads, Feed.LauncherName);
            File.Copy(_realLauncher, download);
            Streams.WriteMark(download);

            int code = Launch(download, "--quit-when-ready");
            Manifest? installed = Installed();
            report.Check("The downloaded exe installs itself and hands over", code == 0 && File.Exists(_paths.LauncherExe), LauncherLog());
            report.Check("The real public key opens what the real private key signed", installed?.Version == "0.1.0", LauncherLog());
            report.Check("The game arrives whole, in a folder with spaces and non-ASCII letters", Whole(installed));
            report.Check("The installed launcher is the one that was run", Hashing.OfFile(_paths.LauncherExe) == Hashing.OfFile(_realLauncher));
            report.Check("Without the mark of the web", !Streams.HasMark(_paths.LauncherExe));

            string game = _paths.GameFile("Dino Defenders.exe");
            report.Check("And so is the game, so Windows has nothing to warn about", !Streams.HasMark(game));
        }

        private static void NothingNew(Report report)
        {
            _server.ClearRequests();
            int code = Launch(_paths.LauncherExe, "--quit-when-ready");
            report.Check("It opens and is ready", code == 0, LauncherLog());
            report.Check("Having fetched only the manifest", _server.CountOf(Feed.ManifestName) == 1 && _server.CountOf(".zip") == 0 && _server.CountOf(".exe") == 0, string.Join(" ", _server.Requests.Select(r => r.Path)));
        }

        private static void RunningAKeptCopy(Report report)
        {
            string sound = Hashing.OfFile(_paths.LauncherExe);
            DateTime written = File.GetLastWriteTimeUtc(_paths.LauncherExe);
            string kept = Variant("kept-launcher.exe", 0x22);

            // Going by the hash alone proves nothing: a launcher copied over the install replaces itself straight back.
            int code = Launch(kept, "--quit-when-ready");
            report.Check("A copy kept after installing leaves the installed launcher untouched", code == 0 && File.GetLastWriteTimeUtc(_paths.LauncherExe) == written && !LauncherLog().Contains("Replacing the launcher"), LauncherLog());

            Installer.RememberDeath(_paths);
            code = Launch(kept, "--quit-when-ready");
            report.Check("Unless that one died: then it goes in, and the release's launcher follows", code == 0 && LauncherLog().Contains("Replacing the launcher") && Hashing.OfFile(_paths.LauncherExe) == sound, LauncherLog());
            report.Check("Which starts with a clean slate", !File.Exists(_paths.LauncherDied));
        }

        private static void ReplacingItself(Report report)
        {
            string next = Variant("next-launcher.exe", 0x11);
            Release(2, next);

            int code = Launch(_paths.LauncherExe, "--quit-when-ready");
            report.Check("A new launcher takes over from the old one", Hashing.OfFile(_paths.LauncherExe) == Hashing.OfFile(next), LauncherLog());
            report.Check("Then the game updates, in the same sitting", Installed()?.Version == "0.1.1" && Whole(Installed()), LauncherLog());
            report.Check("The old launcher closes cleanly", code == 0, $"{code}");

            DateTime giveUp = DateTime.UtcNow.AddSeconds(5);
            while (File.Exists(_paths.LauncherExe + ".old") && DateTime.UtcNow < giveUp) Thread.Sleep(100);
            Launch(_paths.LauncherExe, "--quit-when-ready");
            report.Check("Nothing of it is left behind", !File.Exists(_paths.LauncherExe + ".old") && !File.Exists(_paths.LauncherExe + ".new"));

            _server.ClearRequests();
            Launch(_paths.LauncherExe, "--quit-when-ready");
            report.Check("It doesn't replace itself again", _server.CountOf(".exe") == 0, string.Join(" ", _server.Requests.Select(r => r.Path)));
        }

        private static void ReplacedByGarbage(Report report)
        {
            string before = Hashing.OfFile(_paths.LauncherExe);
            string garbage = Path.Combine(_work, "garbage.exe");
            var bytes = new byte[50000];
            new Random(3).NextBytes(bytes);
            File.WriteAllBytes(garbage, bytes);
            Release(3, garbage);

            int code = Launch(_paths.LauncherExe, "--quit-when-ready");
            report.Check("A release with a broken launcher can't brick anyone", Hashing.OfFile(_paths.LauncherExe) == before, LauncherLog());
            report.Check("The old launcher carries on and updates the game", code == 0 && Installed()?.Version == "0.1.2", LauncherLog());
            report.Check("It remembers not to try that one again", File.Exists(_paths.RefusedLaunchers) && File.ReadAllText(_paths.RefusedLaunchers).Contains(Hashing.OfFile(garbage)));

            _server.ClearRequests();
            code = Launch(_paths.LauncherExe, "--quit-when-ready");
            report.Check("And doesn't", code == 0 && _server.CountOf(".exe") == 0, string.Join(" ", _server.Requests.Select(r => r.Path)));
        }

        private static void TakingOverQuietly(Report report)
        {
            // A real failure on the way up, in the exe itself: a feed that isn't an address throws while the window is being made.
            // Taking over from another launcher, that must end at once and in silence. A dialog here would sit in front of the
            // player, with the old launcher left waiting for this one to give up. It would show as this never finishing.
            var start = new ProcessStartInfo(_paths.LauncherExe, $"--root \"{_root}\" --feed \"not an address\" --handoff Local\\DinoLaunchLabNobody") { UseShellExecute = false };
            using Process process = Process.Start(start)!;
            bool ended = process.WaitForExit(8000);
            if (!ended) process.Kill();
            report.Check("A launcher that fails while taking over goes at once, with no dialog", ended && process.ExitCode == 1, ended ? $"exit {process.ExitCode}" : "still there, showing a dialog");
        }

        private static void ReplacedByOneThatDies(Report report)
        {
            string before = Hashing.OfFile(_paths.LauncherExe);

            // A real program, which runs and quits with an error, never opening a window. This lab, given a command it doesn't know.
            string dies = Path.Combine(_work, "dies.exe");
            File.Copy(Process.GetCurrentProcess().MainModule!.FileName, dies);
            Release(4, dies);

            int code = Launch(_paths.LauncherExe, "--quit-when-ready");
            report.Check("A launcher that starts and dies is put back too", Hashing.OfFile(_paths.LauncherExe) == before, LauncherLog());
            report.Check("And the game still updates", code == 0 && Installed()?.Version == "0.1.3", $"{code} {Installed()?.Version}");
        }

        private static void Playing(Report report)
        {
            string launcher = Variant("good-launcher.exe", 0x22);
            Release(5, launcher);

            int code = Launch(_paths.LauncherExe, "--play");
            string marker = _paths.GameFile("played.txt");
            report.Check("Play starts the game", File.Exists(marker), LauncherLog());
            report.Check("With its own folder as the working directory", File.Exists(marker) && File.ReadAllText(marker).Trim().Equals(_paths.Game, StringComparison.OrdinalIgnoreCase), File.Exists(marker) ? File.ReadAllText(marker) : "");
            report.Check("When it closes normally, so does the launcher", code == 0, $"{code}");
        }

        private static void Crashing(Report report)
        {
            Release(6, Path.Combine(_work, "good-launcher.exe"), exitCode: 42);

            int code = Launch(_paths.LauncherExe, "--play --quit-when-ready");
            report.Check("A game that dies is noticed", code == 4, $"{code}");
            report.Check("And its exit code is logged", LauncherLog().Contains("closed with code 42"), LauncherLog());
        }

        private static void OneAtATime(Report report)
        {
            Release(7, Path.Combine(_work, "good-launcher.exe"));

            var start = new ProcessStartInfo(_paths.LauncherExe, $"--root \"{_root}\" --feed \"{_server.Url}\"") { UseShellExecute = false };
            using Process first = Process.Start(start)!;
            DateTime giveUp = DateTime.UtcNow.AddSeconds(20);
            while (!File.Exists(_paths.InstanceLock) && DateTime.UtcNow < giveUp) Thread.Sleep(50);
            Thread.Sleep(1500);

            var second = new ProcessStartInfo(_paths.LauncherExe, $"--root \"{_root}\" --feed \"{_server.Url}\"") { UseShellExecute = false };
            using Process other = Process.Start(second)!;
            bool leftAtOnce = other.WaitForExit(5000);
            report.Check("A second launcher leaves the first in charge", leftAtOnce && other.ExitCode == 0 && !first.HasExited);

            giveUp = DateTime.UtcNow.AddSeconds(20);
            while (first.MainWindowHandle == IntPtr.Zero && DateTime.UtcNow < giveUp)
            {
                Thread.Sleep(50);
                first.Refresh();
            }
            report.Check("The first one has a window to come back to", first.MainWindowHandle != IntPtr.Zero);

            first.CloseMainWindow();
            report.Check("Closing the window ends it", first.WaitForExit(10000));
            if (!first.HasExited) first.Kill();
        }

        private static Process OpenWindow()
        {
            var start = new ProcessStartInfo(_paths.LauncherExe, $"--root \"{_root}\" --feed \"{_server.Url}\"") { UseShellExecute = false };
            Process process = Process.Start(start)!;
            DateTime giveUp = DateTime.UtcNow.AddSeconds(20);
            while (process.MainWindowHandle == IntPtr.Zero && DateTime.UtcNow < giveUp)
            {
                Thread.Sleep(50);
                process.Refresh();
            }
            return process;
        }

        private static void CloseWindow(Process process)
        {
            process.CloseMainWindow();
            if (!process.WaitForExit(10000)) process.Kill();
            process.Dispose();
            WaitForLauncherToClose(10);
        }

        private static Size SizeOf(IntPtr window)
        {
            Native.GetWindowRect(window, out Native.Rect rect);
            return new Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        private static void SetSize(IntPtr window, int width, int height)
        {
            Native.SetWindowPos(window, IntPtr.Zero, 0, 0, width, height, Native.NoMove | Native.NoZOrder | Native.NoActivate);
            Thread.Sleep(300);
        }

        /// <summary>What the window looks like now, as something to compare. Only the message that asks a window to draw itself can be read.</summary>
        private static string Picture(IntPtr window)
        {
            Size size = SizeOf(window);
            using var bitmap = new Bitmap(size.Width, size.Height);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                IntPtr context = graphics.GetHdc();
                Native.PrintWindow(window, context, 2);
                graphics.ReleaseHdc(context);
            }
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return Hashing.OfBytes(stream.ToArray());
        }

        private static void Resizing(Report report)
        {
            Process process = OpenWindow();
            IntPtr window = process.MainWindowHandle;
            long style = Native.GetWindowLongPtr(window, Native.Style).ToInt64();
            report.Check("It has edges to drag and a button to maximize it", (style & Native.ThickFrame) != 0 && (style & Native.MaximizeBox) != 0, $"{style:X}");

            SetSize(window, 100, 100);
            Size smallest = SizeOf(window);
            report.Check("It won't go smaller than Play and the notes can sit in", smallest.Width >= 760 && smallest.Height >= 520, $"{smallest}");

            SetSize(window, 1234, 777);
            report.Check("It takes a size it's given", SizeOf(window) == new Size(1234, 777), $"{SizeOf(window)}");
            CloseWindow(process);
            report.Check("Closing it leaves that size behind", WindowMemory.Read(_paths.WindowSize) != null, "nothing written");

            process = OpenWindow();
            report.Check("And it opens at that size", SizeOf(process.MainWindowHandle) == new Size(1234, 777), $"{SizeOf(process.MainWindowHandle)}");
            CloseWindow(process);

            new WindowMemory { Width = 99999, Height = 99999 }.Write(_paths.WindowSize);
            process = OpenWindow();
            Size opened = SizeOf(process.MainWindowHandle);
            var screens = new Size(Native.GetSystemMetrics(Native.VirtualWidth), Native.GetSystemMetrics(Native.VirtualHeight));
            report.Check("A size from a bigger screen doesn't leave it hanging off this one", opened.Width <= screens.Width && opened.Height <= screens.Height, $"{opened} on {screens}");
            CloseWindow(process);

            new WindowMemory { Width = 1000, Height = 720, Maximized = true }.Write(_paths.WindowSize);
            process = OpenWindow();
            report.Check("Left maximized, it opens maximized", Native.IsZoomed(process.MainWindowHandle), $"{SizeOf(process.MainWindowHandle)}");
            CloseWindow(process);
            File.Delete(_paths.WindowSize);
        }

        private static void ReadingTheNotes(Report report)
        {
            Release(8, Path.Combine(_work, "good-launcher.exe"), notes: string.Join("\n", Enumerable.Range(1, 300).Select(i => $"- Change number {i}")));

            Process process = OpenWindow();
            IntPtr window = process.MainWindowHandle;
            DateTime giveUp = DateTime.UtcNow.AddSeconds(20);
            while (Installed()?.Notes[0].Text.Contains("Change number 300") != true && DateTime.UtcNow < giveUp) Thread.Sleep(100);
            Thread.Sleep(1500);

            string first = Picture(window);
            Native.PostMessage(window, Native.MouseWheel, new IntPtr(unchecked((short)-360) << 16), IntPtr.Zero);
            Thread.Sleep(800);
            string scrolled = Picture(window);
            report.Check("The wheel moves the notes, wherever the mouse is", scrolled != first);

            Native.PostMessage(window, Native.KeyDown, new IntPtr(Native.Home), new IntPtr(1));
            Thread.Sleep(800);
            report.Check("Home goes back to exactly the top", Picture(window) == first);

            IntPtr search = Native.FindChild(window, "EDIT");
            report.Check("There's a search box", search != IntPtr.Zero);
            string played = _paths.GameFile("played.txt");
            File.Delete(played);

            Native.PostMessage(search, Native.ButtonDown, new IntPtr(1), new IntPtr(5 << 16 | 5));
            Native.PostMessage(search, Native.ButtonUp, IntPtr.Zero, new IntPtr(5 << 16 | 5));
            Thread.Sleep(200);
            foreach (char letter in "number 123") Native.PostMessage(search, Native.Char, new IntPtr(letter), new IntPtr(1));
            Thread.Sleep(400);
            report.Check("Typing cuts the page down", Picture(window) != first);

            Native.PostMessage(search, Native.KeyDown, new IntPtr(Native.Enter), new IntPtr(1));
            Thread.Sleep(1500);
            report.Check("Enter in the search box doesn't start the game", !File.Exists(played) && Native.IsWindowVisible(window));

            Native.PostMessage(search, Native.KeyDown, new IntPtr(Native.Escape), new IntPtr(1));
            Thread.Sleep(600);
            Native.PostMessage(window, Native.KeyDown, new IntPtr(Native.Enter), new IntPtr(1));
            report.Check("Escape ends the search, and then Enter does", process.WaitForExit(10000) && File.Exists(played), File.Exists(played) ? "" : "never played");
            if (!process.HasExited) process.Kill();
            process.Dispose();
            WaitForLauncherToClose(10);
        }

        private static class Native
        {
            public const int Style = -16;
            public const long ThickFrame = 0x00040000;
            public const long MaximizeBox = 0x00010000;
            public const uint NoMove = 0x2;
            public const uint NoZOrder = 0x4;
            public const uint NoActivate = 0x10;
            public const int VirtualWidth = 78;
            public const int VirtualHeight = 79;
            public const uint KeyDown = 0x100;
            public const uint Char = 0x102;
            public const uint ButtonDown = 0x201;
            public const uint ButtonUp = 0x202;
            public const uint MouseWheel = 0x20A;
            public const int Enter = 0x0D;
            public const int Escape = 0x1B;
            public const int Home = 0x24;

            [StructLayout(LayoutKind.Sequential)]
            public struct Rect
            {
                public int Left, Top, Right, Bottom;
            }

            public delegate bool EachWindow(IntPtr window, IntPtr data);

            [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
            [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
            [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
            [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr window, IntPtr context, uint flags);
            [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
            [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr window);
            [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
            [DllImport("user32.dll", EntryPoint = "PostMessageW")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
            [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr window, EachWindow each, IntPtr data);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int size);

            public static IntPtr FindChild(IntPtr window, string kind)
            {
                IntPtr found = IntPtr.Zero;
                EnumChildWindows(window, (child, _) =>
                {
                    var name = new StringBuilder(256);
                    GetClassName(child, name, name.Capacity);
                    if (name.ToString().IndexOf(kind, StringComparison.OrdinalIgnoreCase) >= 0) found = child;
                    return found == IntPtr.Zero;
                }, IntPtr.Zero);
                return found;
            }
        }

        private static void ForgedFeed(Report report)
        {
            Manifest? before = Installed();
            using var forger = new TestFeed();
            Manifest evil = forger.Release("6.6.6", TestFeed.Build(66));
            string path = Path.Combine(_feed, Feed.ManifestPath.Replace('/', Path.DirectorySeparatorChar));
            byte[] honest = File.ReadAllBytes(path);
            File.WriteAllBytes(path, forger.Get(Feed.ManifestPath)!);

            int code = Launch(_paths.LauncherExe, "--quit-when-ready");
            report.Check("Pointing the shipped exe at another feed doesn't get anything past it", Installed()?.Sequence == before?.Sequence && LauncherLog().Contains("Manifest refused"), LauncherLog());
            report.Check("And the game stays playable", code == 0 && Whole(before));
            File.WriteAllBytes(path, honest);
        }

        private static void Offline(Report report)
        {
            _server.Dispose();
            DateTime started = DateTime.UtcNow;
            int code = Launch(_paths.LauncherExe, "--quit-when-ready", seconds: 120);
            report.Check("With the server gone, the game is still ready", code == 0, $"{code}");
            report.Note($"Took {(DateTime.UtcNow - started).TotalSeconds:0.0}s to give up looking.");
        }
    }
}
