using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Dino_Launcher.Update
{
    /// <summary>
    /// A launcher replacing itself, the one update that can't be repaired afterwards if it goes wrong: a broken launcher can't fetch the
    /// fix for itself. So the new one has to prove it can start, by opening its window and saying so. If it doesn't, the old one puts
    /// itself back and remembers never to try that build again. Windows lets a running exe be renamed, though not overwritten.
    /// </summary>
    public sealed class LauncherUpdate
    {
        private const int ProofMilliseconds = 30000;

        private readonly InstallPaths _paths;
        private readonly Uri _feed;
        private readonly Downloader _downloader;
        private readonly string _arguments;
        private readonly Action _releaseInstance;
        private readonly Action _retakeInstance;

        public int ProofWait = ProofMilliseconds;

        /// <param name="arguments">What the new launcher should be started with, besides the proof it's asked for.</param>
        /// <param name="releaseInstance">Lets go of being the one launcher running, so that the new one can be.</param>
        /// <param name="retakeInstance">Takes it back, if the new one turns out not to work.</param>
        public LauncherUpdate(InstallPaths paths, Uri feed, Downloader downloader, string arguments, Action releaseInstance, Action retakeInstance)
        {
            _paths = paths;
            _feed = feed;
            _downloader = downloader;
            _arguments = arguments;
            _releaseInstance = releaseInstance;
            _retakeInstance = retakeInstance;
        }

        private string Replaced => _paths.LauncherExe + ".old";
        private string Incoming => _paths.LauncherExe + ".new";

        /// <summary>Removes the launcher this one replaced, which may take a moment to finish closing.</summary>
        public static void CleanUp(InstallPaths paths) => FileSystem.TryDelete(paths.LauncherExe + ".old");

        /// <summary>True if a new launcher is now running and this one should go.</summary>
        public bool Replace(LauncherEntry entry, CancellationToken cancel)
        {
            try
            {
                if (Hashing.OfFile(_paths.LauncherExe) == entry.Sha256 || Refused().Contains(entry.Sha256)) return false;

                Log.Write($"Replacing the launcher with {entry.Sha256}.");
                _downloader.FetchFile(new Uri(_feed, entry.Url), Incoming, entry.Size, entry.Sha256, _ => { }, cancel);

                FileSystem.Delete(Replaced);
                FileSystem.Move(_paths.LauncherExe, Replaced);
                FileSystem.Move(Incoming, _paths.LauncherExe);
            }
            catch (Exception e) when (e is DownloadException || e is IOException || e is UnauthorizedAccessException)
            {
                // A launcher that can't update today works no worse than it did yesterday.
                Log.Write($"The launcher couldn't be replaced: {e.Message}");
                PutBack();
                return false;
            }

            if (StartedAndProved()) return true;

            Log.Write($"The new launcher {entry.Sha256} never started. Going back.");
            _retakeInstance();
            PutBack();
            Refuse(entry.Sha256);
            return false;
        }

        private bool StartedAndProved()
        {
            string proof = $"Local\\DinoDefendersLauncher-{Guid.NewGuid():N}";
            using var proved = new EventWaitHandle(false, EventResetMode.ManualReset, proof);

            _releaseInstance();

            Process? process = null;
            try
            {
                process = Process.Start(new ProcessStartInfo(_paths.LauncherExe, $"{_arguments} --handoff {proof}") { UseShellExecute = false, WorkingDirectory = _paths.Root });
                if (process == null) return false;

                // It's over as soon as it proves itself, or it dies, or time runs out.
                var exited = new ManualResetEvent(false);
                process.EnableRaisingEvents = true;
                process.Exited += (_, __) => exited.Set();
                if (process.HasExited) exited.Set();

                int which = WaitHandle.WaitAny(new WaitHandle[] { proved, exited }, ProofWait);
                if (which == 0) return true;

                if (!process.HasExited) process.Kill();
                process.WaitForExit(5000);
                return false;
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception || e is InvalidOperationException || e is IOException)
            {
                Log.Write($"The new launcher wouldn't start: {e.Message}");
                return false;
            }
            finally
            {
                process?.Dispose();
            }
        }

        private void PutBack()
        {
            if (!File.Exists(Replaced)) return;
            FileSystem.Delete(_paths.LauncherExe);
            FileSystem.Move(Replaced, _paths.LauncherExe);
        }

        private List<string> Refused()
        {
            return File.Exists(_paths.RefusedLaunchers) ? File.ReadAllLines(_paths.RefusedLaunchers).ToList() : new List<string>();
        }

        private void Refuse(string sha256)
        {
            try
            {
                File.AppendAllText(_paths.RefusedLaunchers, sha256 + Environment.NewLine);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log.Write($"Couldn't remember the launcher that failed: {e.Message}");
            }
        }
    }
}
