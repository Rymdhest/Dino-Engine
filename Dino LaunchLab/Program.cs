using System;
using System.IO;
using System.Threading;

namespace Dino_LaunchLab
{
    /// <summary>
    /// Tests the launcher and Dino Release against a server that misbehaves on purpose, with no real network and nothing touched outside a
    /// temporary folder.
    ///   core      The update logic in this process: installing, repairing, bad lines, bad releases, power cuts, full disks.
    ///   release   The real Dino Release against a stand-in for GitHub, and the update logic reading what it made.
    ///   live      The real launcher exe, end to end, windows and all. It needs the real signing key, to sign for a local feed only.
    ///   serve folder [kilobytes a second]   Serves a feed made with Dino Release --folder, to try a launcher against by hand.
    /// Exit code 1 on any failure.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            // Stands in for the game in the live tests. Kept apart, as a copy of this exe does this with nothing beside it.
            if (Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName) == "Dino Defenders.exe") return FakeGame();

            return Run(args.Length > 0 ? args[0] : "core", args);
        }

        private static int Run(string command, string[] args)
        {
            switch (command)
            {
                case "serve":
                    return Serve(args[1], args.Length > 2 ? int.Parse(args[2]) : 0);
                case "core":
                    return CoreTests.Run();
                case "release":
                    return ReleaseTests.Run();
                case "live":
                    return LiveTests.Run();
                default:
                    Console.Error.WriteLine($"Unknown command '{command}'.");
                    return 2;
            }
        }

        private static int Serve(string folder, int kilobytes)
        {
            using var server = new FaultyServer { RedirectToCdn = true, ThrottleKilobytes = kilobytes };
            server.Content = path =>
            {
                string file = Path.Combine(folder, path.Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(file) ? File.ReadAllBytes(file) : null;
            };
            Console.WriteLine($"Serving {folder} at {server.Url}");
            Thread.Sleep(Timeout.Infinite);
            return 0;
        }

        private static int FakeGame()
        {
            string folder = AppDomain.CurrentDomain.BaseDirectory;
            File.WriteAllText(Path.Combine(folder, "played.txt"), Environment.CurrentDirectory);
            int code = int.Parse(File.ReadAllText(Path.Combine(folder, "exit-code.txt")).Trim());
            Thread.Sleep(500);
            return code;
        }
    }

    /// <summary>What passed and what didn't.</summary>
    internal sealed class Report
    {
        private int _passed;
        private int _failed;

        public void Section(string name) => Console.WriteLine($"\n{name}");

        public void Check(string what, bool passed, string detail = "")
        {
            if (passed) _passed++;
            else _failed++;
            Console.WriteLine($"  {(passed ? "ok  " : "FAIL")}  {what}{(passed || detail.Length == 0 ? "" : $"  ({detail})")}");
        }

        public void Note(string text) => Console.WriteLine($"        {text}");

        public int Finish()
        {
            Console.WriteLine($"\n{_passed} passed, {_failed} failed");
            return _failed == 0 ? 0 : 1;
        }
    }

    /// <summary>A folder that's gone afterwards. The name has what real ones have: spaces, and letters that aren't ASCII.</summary>
    internal sealed class TemporaryFolder : IDisposable
    {
        public readonly string Path;

        public TemporaryFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Dino LaunchLab åäö", Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    if (Directory.Exists(Path)) Directory.Delete(Path, true);
                    return;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    Thread.Sleep(200);
                }
            }
        }
    }
}
