using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dino_Defenders
{
    /// <summary>
    /// Notices when the main loop has stopped, and leaves behind what it was doing. A hang says nothing for itself: the log just stops and Windows
    /// only knows that the window went quiet. So once frames stop coming this says which threads are burning the processor, and has the runtime write
    /// a dump of every stack beside the log. It only ever looks on, from a thread of its own, and nothing here may stop the game.
    /// </summary>
    internal static class HangWatch
    {
        private const double StuckSeconds = 30.0;     // Past anything loading a world can take on a slow machine, as each dump is several gigabytes.
        private const int SampleMilliseconds = 2000;
        private const int BusiestThreads = 6;
        public const string DumpArgument = "--writedump";
        private const int DumpContents = 0x2 | 0x4 | 0x20 | 0x800 | 0x1000;   // All memory, handles, unloaded modules, memory map, thread times. Private memory alone fails with error 1 here.

        private static long _lastBeat;

        public static void Start()
        {
            Beat();
            new Thread(Watch) { Name = "Hang watch", IsBackground = true }.Start();
        }

        /// <summary>The main loop is still going.</summary>
        public static void Beat() => Volatile.Write(ref _lastBeat, Stopwatch.GetTimestamp());

        private static double SecondsSinceBeat => Stopwatch.GetElapsedTime(Volatile.Read(ref _lastBeat)).TotalSeconds;

        private static void Watch()
        {
            bool dumped = false;
            while (true)
            {
                Thread.Sleep(1000);
                if (SecondsSinceBeat < StuckSeconds) continue;

                long stuckBeat = Volatile.Read(ref _lastBeat);
                try
                {
                    Console.WriteLine($"The main loop has stopped for {SecondsSinceBeat:0} s, at {DateTime.Now:HH:mm:ss}.");
                    ReportThreads();
                    if (!dumped) dumped = Dump();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Could not look into the hang: {e.Message}");
                }

                while (Volatile.Read(ref _lastBeat) == stuckBeat) Thread.Sleep(250);
                Console.WriteLine($"The main loop came back at {DateTime.Now:HH:mm:ss}.");
            }
        }

        // What each thread did with the last moment: one that's using a whole core is spinning, and one that's using none is waiting on something.
        private static void ReportThreads()
        {
            Dictionary<int, TimeSpan> before = new();
            using (Process process = Process.GetCurrentProcess())
            {
                foreach (ProcessThread thread in process.Threads) before[thread.Id] = Used(thread);
            }

            Thread.Sleep(SampleMilliseconds);

            using (Process process = Process.GetCurrentProcess())
            {
                var busy = new List<(double Percent, int Id, string State)>();
                foreach (ProcessThread thread in process.Threads)
                {
                    if (!before.TryGetValue(thread.Id, out TimeSpan was)) continue;
                    double percent = (Used(thread) - was).TotalMilliseconds / SampleMilliseconds * 100.0;
                    busy.Add((percent, thread.Id, thread.ThreadState == System.Diagnostics.ThreadState.Wait ? $"Wait {thread.WaitReason}" : thread.ThreadState.ToString()));
                }

                Console.WriteLine($"{busy.Count} threads, {process.WorkingSet64 / (1024 * 1024)} MB in use. The busiest:");
                foreach (var thread in busy.OrderByDescending(t => t.Percent).Take(BusiestThreads))
                {
                    Console.WriteLine($"  thread {thread.Id}: {thread.Percent:0}% of a core, {thread.State}");
                }
            }
        }

        private static TimeSpan Used(ProcessThread thread)
        {
            try
            {
                return thread.TotalProcessorTime;
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                return TimeSpan.Zero;
            }
        }

        // One dump a run, and each run's replaces the last, as they're large. A process can't dump itself reliably, so a second copy of the game does it.
        private static bool Dump()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DinoDefenders", "logs");
            string path = Path.Combine(folder, "hang.dmp");

            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(DumpArgument);
            start.ArgumentList.Add(Environment.ProcessId.ToString());
            start.ArgumentList.Add(path);

            using Process writer = Process.Start(start)!;
            writer.WaitForExit();
            if (writer.ExitCode != 0)
            {
                Console.WriteLine($"Could not write a dump: error {writer.ExitCode}.");
                return false;
            }

            Console.WriteLine($"Wrote {path}, {new FileInfo(path).Length / (1024 * 1024)} MB.");
            return true;
        }

        /// <summary>The second copy's whole job. Holds every stack and the managed heap, which is what reading a dump needs. Returns 0, or why not.</summary>
        public static int WriteDump(int processId, string path)
        {
            using Process process = Process.GetProcessById(processId);
            bool written;
            int error;
            using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                written = MiniDumpWriteDump(process.Handle, processId, file.SafeFileHandle, DumpContents, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                error = Marshal.GetLastPInvokeError();
            }
            if (written) return 0;

            File.Delete(path);
            return error != 0 ? error : -1;
        }

        [DllImport("dbghelp.dll", SetLastError = true)]
        private static extern bool MiniDumpWriteDump(IntPtr process, int processId, SafeHandle file, int type, IntPtr exception, IntPtr userStream, IntPtr callback);
    }
}
