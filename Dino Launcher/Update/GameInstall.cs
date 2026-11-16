using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace Dino_Launcher.Update
{
    /// <summary>Thrown when the game can't be changed right now. The installed game is as it was.</summary>
    public sealed class InstallException : Exception
    {
        public InstallException(string message) : base(message) { }
    }

    /// <summary>
    /// Makes the game's folder match a manifest. Installing, updating and repairing are one operation: find the files that aren't right,
    /// get the packs holding them, build the whole new folder beside the old one, check it, and swap the two by renaming. Two renames
    /// is the only moment anything changes, so a crash or a power cut leaves the old game, the new game, or something
    /// <see cref="Recover"/> turns into one of them. Never half of each. A folder held open by a window but with every file free is swapped
    /// file by file instead, behind a list that Recover finishes from.
    /// </summary>
    public sealed class GameInstall
    {
        private const long SpareBytes = 32L * 1024 * 1024;
        private const int SwapTries = 10;
        private const int SwapWaitMilliseconds = 500;

        private readonly InstallPaths _paths;

        /// <summary>How much room there is. For a test to pretend the disk is full.</summary>
        public Func<long> FreeSpace;

        /// <summary>Called between the steps of a swap. For a test to pull the plug.</summary>
        public Action<string> Checkpoint = _ => { };

        public int SwapWait = SwapWaitMilliseconds;

        public GameInstall(InstallPaths paths)
        {
            _paths = paths;
            FreeSpace = () => new DriveInfo(Path.GetPathRoot(paths.Root)).AvailableFreeSpace;
        }

        /// <summary>Finishes or undoes a swap that something interrupted, and clears away what was left lying around.</summary>
        public void Recover()
        {
            // A swap file by file is only ever finished, never undone: everything it still has to move in is in the staged folder, checked.
            if (File.Exists(_paths.Swapping))
            {
                if (Directory.Exists(_paths.Game) && Directory.Exists(_paths.Staging)) MoveFilesIn(ReadSwapList());
                EndFileSwap();
            }

            if (!Directory.Exists(_paths.Game))
            {
                if (File.Exists(_paths.StagingReady) && Directory.Exists(_paths.Staging)) Directory.Move(_paths.Staging, _paths.Game);
                else if (Directory.Exists(_paths.Retired)) Directory.Move(_paths.Retired, _paths.Game);
            }

            FileSystem.TryDelete(_paths.StagingReady);
            FileSystem.TryDeleteFolder(_paths.Staging);
            if (Directory.Exists(_paths.Game)) FileSystem.TryDeleteFolder(_paths.Retired);
        }

        /// <summary>What's installed, if a good manifest says so. Null means nothing to play, or nothing that can be vouched for.</summary>
        public Manifest? ReadInstalled(string publicKey)
        {
            try
            {
                // Halfway through a swap file by file, the folder is some of each build: nothing to play until Recover has finished it.
                if (File.Exists(_paths.Swapping)) return null;
                if (!File.Exists(_paths.InstalledManifest)) return null;
                Manifest manifest = Manifest.Parse(SignedFile.Open(File.ReadAllBytes(_paths.InstalledManifest), publicKey));
                manifest.ValidateLauncher();
                manifest.ValidateGame();
                return manifest;
            }
            catch (Exception e) when (e is ManifestException || e is IOException || e is UnauthorizedAccessException)
            {
                Log.Write($"The installed manifest is no good: {e.Message}");
                return null;
            }
        }

        /// <summary>Every file in the manifest that isn't there, or isn't right. Files it doesn't mention don't count.</summary>
        public static List<FileEntry> FindWrong(string folder, Manifest manifest, CancellationToken cancel)
        {
            var wrong = new List<FileEntry>();
            foreach (FileEntry file in manifest.Files)
            {
                cancel.ThrowIfCancellationRequested();
                string path = Path.Combine(folder, file.Path.Replace('/', Path.DirectorySeparatorChar));
                try
                {
                    var info = new FileInfo(path);
                    if (!info.Exists || info.Length != file.Size || Hashing.OfFile(path) != file.Sha256) wrong.Add(file);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    wrong.Add(file);
                }
            }
            return wrong;
        }

        /// <summary>The signed manifest becomes the installed one, for a release that needs no files changed.</summary>
        public void Record(byte[] signedManifest) => FileSystem.WriteAtomically(_paths.InstalledManifest, signedManifest);

        public void Apply(Uri feed, Manifest manifest, byte[] signedManifest, List<FileEntry> wrong, Downloader downloader, Action<Progress> report, CancellationToken cancel)
        {
            var wrongPacks = new HashSet<string>(wrong.Select(f => f.Pack));
            List<PackEntry> packs = manifest.Packs.Where(p => wrongPacks.Contains(p.Name)).ToList();
            long downloadBytes = packs.Sum(p => p.Size);
            long neededBytes = downloadBytes + manifest.Files.Sum(f => f.Size) + SpareBytes;
            if (FreeSpace() < neededBytes)
            {
                throw new InstallException($"There isn't enough disk space: this needs {neededBytes / (1024 * 1024)} MB free.");
            }

            string title = Directory.Exists(_paths.Game) ? $"Updating to {manifest.Version}" : $"Downloading Dino Defenders {manifest.Version}";
            Directory.CreateDirectory(_paths.Downloads);
            long finished = 0;
            foreach (PackEntry pack in packs)
            {
                long before = finished;
                downloader.FetchFile(new Uri(feed, pack.Url), PackPath(pack), pack.Size, pack.Sha256,
                    done => report(new Progress(Phase.Downloading, title, before + done, downloadBytes)), cancel);
                finished += pack.Size;
            }

            report(new Progress(Phase.Installing, "Installing"));
            FileSystem.DeleteFolder(_paths.Staging);
            FileSystem.Delete(_paths.StagingReady);
            FileSystem.DeleteFolder(_paths.Retired);
            Stage(manifest, signedManifest, wrong, packs, cancel);
            Swap();

            FileSystem.TryDeleteFolder(_paths.Downloads);
        }

        private string PackPath(PackEntry pack) => Path.Combine(_paths.Downloads, pack.Sha256 + ".zip");

        private void Stage(Manifest manifest, byte[] signedManifest, List<FileEntry> wrong, List<PackEntry> packs, CancellationToken cancel)
        {
            Directory.CreateDirectory(_paths.Staging);

            var replaced = new HashSet<string>(wrong.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
            foreach (FileEntry file in manifest.Files.Where(f => !replaced.Contains(f.Path)))
            {
                cancel.ThrowIfCancellationRequested();
                string target = StagedPath(file);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(_paths.GameFile(file.Path), target);
            }

            foreach (PackEntry pack in packs)
            {
                using ZipArchive zip = ZipFile.OpenRead(PackPath(pack));
                foreach (FileEntry file in wrong.Where(f => f.Pack == pack.Name))
                {
                    cancel.ThrowIfCancellationRequested();
                    Extract(zip, file);
                }
            }

            File.WriteAllBytes(Path.Combine(_paths.Staging, Feed.ManifestName), signedManifest);

            // Checked once more where it stands, as that's what is about to become the game.
            List<FileEntry> stillWrong = FindWrong(_paths.Staging, manifest, cancel);
            if (stillWrong.Count > 0) throw new InstallException($"{stillWrong[0].Path} didn't come out right, and {stillWrong.Count - 1} more.");

            File.WriteAllBytes(_paths.StagingReady, new byte[0]);
        }

        private string StagedPath(FileEntry file) => Path.Combine(_paths.Staging, file.Path.Replace('/', Path.DirectorySeparatorChar));

        // The path comes from the manifest and never from the zip, so a name in a zip can't reach anywhere it shouldn't.
        private void Extract(ZipArchive zip, FileEntry file)
        {
            ZipArchiveEntry entry = zip.GetEntry(file.Path) ?? throw new InstallException($"{file.Path} isn't in its pack.");
            if (entry.Length != file.Size) throw new InstallException($"{file.Path} isn't the size it should be.");

            string target = StagedPath(file);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            using Stream input = entry.Open();
            using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }

        private void Swap()
        {
            Checkpoint("staged");

            bool replacing = Directory.Exists(_paths.Game);
            if (replacing && !TryMove(_paths.Game, _paths.Retired, out bool filesFree))
            {
                // A folder can't be renamed while anything has it or a folder in it open, an Explorer window or a terminal among them, though every
                // file in it is free: a person found an update refused for a window open on its shaders. Then the files are swapped one at a time.
                if (!filesFree) throw new InstallException("Something is still using the game's files. Close the game and try again.");
                SwapFiles();
                return;
            }
            Checkpoint("retired");

            if (!TryMove(_paths.Staging, _paths.Game, out _))
            {
                if (replacing) Directory.Move(_paths.Retired, _paths.Game);
                throw new InstallException("The new files couldn't be moved into place. Is an antivirus scanning them? Try again in a moment.");
            }
            Checkpoint("swapped");

            FileSystem.TryDelete(_paths.StagingReady);
            Checkpoint("marked");
            FileSystem.TryDeleteFolder(_paths.Retired);
        }

        // A virus scanner can hold a file for a moment after it's written, which fails a rename that would work a second later. A folder whose
        // files are all free is only held open as a folder, which waiting won't change, so that's said at once (filesFree).
        private bool TryMove(string from, string to, out bool filesFree)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    Directory.Move(from, to);
                    filesFree = true;
                    return true;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    filesFree = FilesFree(from);
                    if (filesFree || attempt >= SwapTries)
                    {
                        Log.Write($"Couldn't move {from}: {e.Message}{(filesFree ? " Its files are free, so they're swapped one at a time." : "")}");
                        return false;
                    }
                    Thread.Sleep(SwapWait);
                }
            }
        }

        // Whether every file in a folder could be moved now: none is open, as a running game holds its own.
        private static bool FilesFree(string folder)
        {
            foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                try
                {
                    using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    return false;
                }
            }
            return true;
        }

        // The game's folder stays where it is, and its files change. The list of the new build's files is written down first (Swapping), so that
        // a crash anywhere in this is finished by Recover: anything not on it goes, and everything on it still in the staged folder comes in.
        // Until then ReadInstalled says there's nothing to play. The manifest is moved in last, so it's the last thing to say which build it is.
        private void SwapFiles()
        {
            List<string> list = Directory.GetFiles(_paths.Staging, "*", SearchOption.AllDirectories)
                .Select(path => path.Substring(_paths.Staging.Length + 1))
                .OrderBy(path => string.Equals(path, Feed.ManifestName, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            FileSystem.WriteAtomically(_paths.Swapping, System.Text.Encoding.UTF8.GetBytes(string.Join("\n", list)));
            Checkpoint("listed");

            try
            {
                MoveFilesIn(list);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log.Write($"Swapping files one at a time stopped: {e.Message}");
                if (!TryMoveFilesBack(list)) throw new InstallException("The update stopped halfway. Open the launcher again to finish it.");
                throw new InstallException("Something is still using the game's files. Close the game and try again.");
            }
            EndFileSwap();
        }

        private List<string> ReadSwapList() =>
            File.ReadAllText(_paths.Swapping).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        // Anything in the game's folder that isn't on the list goes to the retired folder, then each listed file still staged takes the place of
        // the old one. Each step can be done again from wherever it stopped.
        private void MoveFilesIn(List<string> list)
        {
            var listed = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
            foreach (string path in Directory.GetFiles(_paths.Game, "*", SearchOption.AllDirectories))
            {
                string relative = path.Substring(_paths.Game.Length + 1);
                if (!listed.Contains(relative)) Retire(relative);
            }

            for (int i = 0; i < list.Count; i++)
            {
                string staged = Path.Combine(_paths.Staging, list[i]);
                if (!File.Exists(staged)) continue;
                Retire(list[i]);
                string target = Path.Combine(_paths.Game, list[i]);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                FileSystem.Move(staged, target);
                if (i == list.Count / 2) Checkpoint("halfway");
            }
        }

        private void Retire(string relative)
        {
            string path = Path.Combine(_paths.Game, relative);
            if (!File.Exists(path)) return;
            string retired = Path.Combine(_paths.Retired, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(retired));
            FileSystem.Delete(retired);
            FileSystem.Move(path, retired);
        }

        // What was moved in goes back to the staged folder, and the old files back where they were, so the old build is whole again.
        private bool TryMoveFilesBack(List<string> list)
        {
            try
            {
                foreach (string relative in list)
                {
                    string staged = Path.Combine(_paths.Staging, relative);
                    string path = Path.Combine(_paths.Game, relative);
                    if (File.Exists(staged) || !File.Exists(path)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(staged));
                    FileSystem.Move(path, staged);
                }
                if (Directory.Exists(_paths.Retired))
                {
                    foreach (string retired in Directory.GetFiles(_paths.Retired, "*", SearchOption.AllDirectories))
                    {
                        string path = Path.Combine(_paths.Game, retired.Substring(_paths.Retired.Length + 1));
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        FileSystem.Move(retired, path);
                    }
                }
                FileSystem.Delete(_paths.Swapping);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log.Write($"Couldn't put the old build back, so the next start finishes the new one: {e.Message}");
                return false;
            }
        }

        // Folders the new build left empty go, where they can (one held open stays, empty, and harms nothing), then the list, last.
        private void EndFileSwap()
        {
            Checkpoint("moved");
            if (Directory.Exists(_paths.Game))
            {
                foreach (string folder in Directory.GetDirectories(_paths.Game, "*", SearchOption.AllDirectories).OrderByDescending(f => f.Length))
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    {
                    }
                }
            }
            FileSystem.TryDelete(_paths.StagingReady);
            FileSystem.Delete(_paths.Swapping);
            Checkpoint("marked");
            FileSystem.TryDeleteFolder(_paths.Staging);
            FileSystem.TryDeleteFolder(_paths.Retired);
        }
    }
}
