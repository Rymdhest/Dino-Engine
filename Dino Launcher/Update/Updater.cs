using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Dino_Launcher.Update
{
    public enum Phase
    {
        Checking,
        UpdatingLauncher,
        WaitingForGame,
        Downloading,
        Installing,
    }

    /// <summary>What the launcher is doing, in words a player can read. Done and Total are bytes, when counting them means anything.</summary>
    public readonly struct Progress
    {
        public readonly Phase Phase;
        public readonly string Text;
        public readonly long Done;
        public readonly long Total;

        public Progress(Phase phase, string text, long done = 0, long total = 0)
        {
            Phase = phase;
            Text = text;
            Done = done;
            Total = total;
        }
    }

    /// <summary>Where a run ended up.</summary>
    public sealed class UpdateResult
    {
        /// <summary>The build that's here and whole. Null means there's nothing to play.</summary>
        public Manifest? Build;

        /// <summary>What went wrong, for the player. With a build, it's what they're missing rather than what stops them.</summary>
        public string? Problem;

        /// <summary>A new launcher has taken over. This one should go.</summary>
        public bool HandedOver;
    }

    /// <summary>
    /// One pass of what a launcher is for: see what's out there, replace the launcher if that's changed, make the game match. Nothing that
    /// goes wrong here stops someone playing what they already have. Runs on a worker thread, and the window only ever watches.
    /// </summary>
    public sealed class Updater
    {
        private readonly InstallPaths _paths;
        private readonly Uri _feed;
        private readonly string _publicKey;
        private readonly Downloader _downloader;

        public readonly GameInstall Install;

        /// <summary>Replaces this launcher if a manifest calls for it, and says whether something else is now running. Null if this isn't one.</summary>
        public Func<LauncherEntry, CancellationToken, bool>? ReplaceLauncher;

        public Updater(InstallPaths paths, Uri feed, string publicKey, Downloader downloader)
        {
            _paths = paths;
            _feed = feed;
            _publicKey = publicKey;
            _downloader = downloader;
            Install = new GameInstall(paths);
        }

        public UpdateResult Run(Action<Progress> report, CancellationToken cancel)
        {
            report(new Progress(Phase.Checking, "Checking for updates"));
            Install.Recover();
            Manifest? installed = Install.ReadInstalled(_publicKey);

            byte[]? signed = null;
            Manifest? live = null;
            string? problem = null;
            try
            {
                signed = _downloader.Fetch(new Uri(_feed, Feed.ManifestPath), Feed.MostManifestBytes, patient: installed == null, cancel);
                live = Manifest.Parse(SignedFile.Open(signed, _publicKey));
                live.ValidateLauncher();
            }
            catch (DownloadException e)
            {
                Log.Write($"No manifest: {e.Message}");
                problem = "Couldn't reach the update server.";
            }
            catch (ManifestException e)
            {
                Log.Write($"Manifest refused: {e.Message}");
                live = null;
                problem = "The latest release didn't pass its security check, so it was ignored.";
            }

            // Something older than what's here is a stale cache, or someone replaying an old release. Going back is done with a new release.
            if (live != null && installed != null && live.Sequence < installed.Sequence)
            {
                Log.Write($"Ignoring release {live.Sequence}: {installed.Sequence} is installed.");
                live = null;
            }

            if (live != null && ReplaceLauncher != null)
            {
                report(new Progress(Phase.UpdatingLauncher, "Updating the launcher"));
                if (ReplaceLauncher(live.Launcher, cancel)) return new UpdateResult { HandedOver = true };
            }

            if (live != null)
            {
                try
                {
                    live.ValidateGame();
                }
                catch (ManifestException e)
                {
                    Log.Write($"Manifest refused: {e.Message}");
                    live = null;
                    problem = e.Message;
                }
            }

            Manifest? target = live ?? installed;
            if (target == null) return new UpdateResult { Problem = (problem ?? "There's nothing to install yet.") + " Check your connection and try again." };

            List<FileEntry> wrong = GameInstall.FindWrong(_paths.Game, target, cancel);
            if (wrong.Count == 0)
            {
                if (live != null && live.Sequence != installed?.Sequence) Record(signed!);
                return new UpdateResult { Build = target, Problem = problem };
            }

            if (live == null)
            {
                Log.Write($"{wrong.Count} files are damaged, starting with {wrong[0].Path}, and there's no manifest to repair them from.");
                return new UpdateResult { Problem = "The game's files are damaged and the update server can't be reached to repair them. Try again when you're online." };
            }

            Log.Write($"Going to {live.Version} (release {live.Sequence}): {wrong.Count} of {live.Files.Length} files need replacing.");
            try
            {
                WaitForGame(live, report, cancel);
                if (installed != null && installed.Version != live.Version) BackUpSave(installed.Version);
                Install.Apply(_feed, live, signed!, wrong, _downloader, report, cancel);
                Log.Write($"Now on {live.Version}.");
                return new UpdateResult { Build = live };
            }
            catch (Exception e) when (e is DownloadException || e is InstallException || e is IOException || e is UnauthorizedAccessException || e is InvalidDataException)
            {
                Log.Write($"Update failed: {e}");
                bool stillGood = installed != null && GameInstall.FindWrong(_paths.Game, installed, cancel).Count == 0;
                string reason = e is InstallException ? e.Message : "The update didn't finish.";
                return new UpdateResult { Build = stillGood ? installed : null, Problem = reason };
            }
        }

        // The files are right whether or not this is written down, and that's not worth stopping anyone playing over.
        private void Record(byte[] signed)
        {
            try
            {
                Install.Record(signed);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log.Write($"Couldn't record the release: {e.Message}");
            }
        }

        private void WaitForGame(Manifest manifest, Action<Progress> report, CancellationToken cancel)
        {
            while (GameProcess.IsRunning(_paths, manifest))
            {
                report(new Progress(Phase.WaitingForGame, "An update is ready. Close the game to install it."));
                cancel.WaitHandle.WaitOne(1000);
                cancel.ThrowIfCancellationRequested();
            }
        }

        // Testing means builds that may read an older save wrongly, and a save is the one thing here that can't be downloaded again.
        private void BackUpSave(string version)
        {
            const int kept = 5;
            try
            {
                string save = Path.Combine(_paths.SaveFolder, "save.json");
                if (!File.Exists(save)) return;

                string folder = Path.Combine(_paths.SaveFolder, "backups");
                Directory.CreateDirectory(folder);
                string backup = Path.Combine(folder, $"save-from-{version}.json");
                File.Copy(save, backup, true);

                // A copy keeps the save's own time, and what matters for keeping the newest is when this was taken.
                File.SetLastWriteTimeUtc(backup, DateTime.UtcNow);

                foreach (FileInfo old in new DirectoryInfo(folder).GetFiles("save-from-*.json").OrderByDescending(f => f.LastWriteTimeUtc).Skip(kept))
                {
                    old.Delete();
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log.Write($"Couldn't back up the save: {e.Message}");
            }
        }
    }

    /// <summary>The game, as a process.</summary>
    public static class GameProcess
    {
        public static bool IsRunning(InstallPaths paths, Manifest manifest)
        {
            string expected = paths.GameFile(manifest.Executable);
            foreach (Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(manifest.Executable)))
            {
                using (process)
                {
                    try
                    {
                        if (string.Equals(process.MainModule?.FileName, expected, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    catch (Exception e) when (e is Win32Exception || e is InvalidOperationException)
                    {
                        // Gone already, or someone else's.
                    }
                }
            }
            return false;
        }

        /// <summary>The game is a console program, for dedicated servers. Started from here, nobody needs to see that window.</summary>
        public static Process Start(InstallPaths paths, Manifest manifest)
        {
            var start = new ProcessStartInfo(paths.GameFile(manifest.Executable))
            {
                WorkingDirectory = paths.Game,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            return Process.Start(start);
        }
    }
}
