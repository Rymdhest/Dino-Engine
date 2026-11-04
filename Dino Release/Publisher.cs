using System.Diagnostics;
using System.IO.Compression;
using Dino_Launcher.Update;

namespace Dino_Release
{
    /// <summary>
    /// Makes a release. A build is split in two: the runtime, about 33 MB zipped, which only changes when .NET or a library does, and the game,
    /// about 1 MB, which changes every time. The runtime goes up once under a tag of its own and every release after it points there,
    /// so a release is a small upload and an update is a small download. See LAUNCHER.md.
    /// </summary>
    internal sealed class Publisher
    {
        private const string GameProject = "Dino Defenders/Dino Defenders.csproj";
        private const string LauncherProject = "Dino Launcher/Dino Launcher.csproj";
        private const string Executable = "Dino Defenders.exe";
        private const string FirstVersion = "0.1.0";
        private const string GamePack = "game";
        private const string RuntimePack = "runtime";
        private const int NotesKept = 8;
        private const int SmokeSeconds = 180;

        private static readonly DateTimeOffset ZipTime = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly Options _options;
        private readonly IReleaseStore _store;
        private readonly string _trusted;

        public Publisher(Options options)
        {
            _options = options;
            _trusted = options.Text("trust") ?? Feed.PublicKey;
            _store = OpenStore(options);
        }

        private static IReleaseStore OpenStore(Options options)
        {
            string? folder = options.Text("folder");
            if (folder != null) return new FolderStore(folder);

            return new GitHubStore(options.Text("api") ?? "https://api.github.com", options.Text("feed") ?? Feed.Url,
                () => Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? Environment.GetEnvironmentVariable("GH_TOKEN") ?? Git.StoredGitHubToken());
        }

        public void Status()
        {
            byte[]? signed = _store.ReadLive();
            if (signed == null)
            {
                Console.WriteLine("Nothing has been released yet.");
                return;
            }

            Manifest live = Open(signed);
            Console.WriteLine($"Live: {live.Version} (release {live.Sequence}), from {live.Commit}, published {live.Published}");
            foreach (PackEntry pack in live.Packs) Console.WriteLine($"  {pack.Name}: {pack.Size / 1024} KB at {pack.Url}");
            Console.WriteLine($"  launcher: {live.Launcher.Size / 1024} KB at {live.Launcher.Url}");
            Console.WriteLine($"Send new testers: {Feed.LauncherLink}");
        }

        public void Publish()
        {
            bool real = _options.Text("game") == null;
            bool dirty = real && Git.HasUncommittedChanges();
            if (dirty && !_options.Flag("allow-dirty"))
            {
                throw new ReleaseException("There are uncommitted changes, so nobody could tell later what this build was. Commit them, or pass --allow-dirty.");
            }

            using SigningKey key = LoadKey();
            byte[]? signedLive = _store.ReadLive();
            Manifest? live = signedLive == null ? null : Open(signedLive);

            string version = _options.Text("version") ?? NextVersion(live);
            string tag = $"v{version}";
            if (_store.HasRelease(tag)) throw new ReleaseException($"{tag} already exists.");
            string commit = real ? Git.Head() + (dirty ? "+dirty" : "") : "test";

            Console.WriteLine(live == null ? $"First release: {version}" : $"Releasing {version}, after {live.Version}");

            string work = Path.GetFullPath(Path.Combine("out", "release", version));
            if (Directory.Exists(work)) Directory.Delete(work, true);
            Directory.CreateDirectory(work);

            string game = _options.Text("game") ?? BuildGame(version, work);
            string launcher = _options.Text("launcher") ?? BuildLauncher();
            if (real && !_options.Flag("no-smoke")) SmokeTest(game, work);

            List<FileEntry> files = ListFiles(game);
            if (!files.Any(f => f.Path == Executable) && real) throw new ReleaseException($"{Executable} isn't in the build.");

            string notes = _options.Text("notes") ?? NotesFromCommits(live);
            var manifest = new Manifest
            {
                Sequence = (live?.Sequence ?? 0) + 1,
                Version = version,
                Commit = commit,
                Published = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Notes = NotesWith(live, version, notes),
                Executable = real ? Executable : _options.Text("executable") ?? files[0].Path,
                Files = files.ToArray(),
            };

            manifest.Launcher = new LauncherEntry
            {
                Url = $"download/{tag}/{Feed.LauncherName}",
                Size = new FileInfo(launcher).Length,
                Sha256 = Hashing.OfFile(launcher),
            };

            string gameZip = Path.Combine(work, Feed.GamePackName);
            WriteZip(gameZip, game, files.Where(f => f.Pack == GamePack));
            PackEntry gamePack = PackFor(GamePack, $"download/{tag}/{Feed.GamePackName}", gameZip);

            (PackEntry runtimePack, Action? uploadRuntime) = PrepareRuntime(live, game, files, work, commit);
            manifest.Packs = new[] { runtimePack, gamePack };

            string signedPath = Path.Combine(work, Feed.ManifestName);
            WriteSigned(manifest, key, signedPath);

            long runtimeDownload = uploadRuntime != null ? runtimePack.Size : 0;
            Console.WriteLine($"Testers will download {(gamePack.Size + runtimeDownload) / 1024} KB. {files.Count} files, {files.Sum(f => f.Size) / (1024 * 1024)} MB installed.");
            Console.WriteLine(notes.Length > 0 ? $"Notes:\n{notes}" : "No notes.");

            if (_options.Flag("dry-run"))
            {
                Console.WriteLine($"Dry run: nothing was released. It's all in {work}");
                return;
            }

            uploadRuntime?.Invoke();
            var assets = new List<Asset>
            {
                new(Feed.GamePackName, gameZip),
                new(Feed.LauncherName, launcher),
                new(Feed.ManifestName, signedPath),
            };
            _store.Publish(tag, $"Dino Defenders {version}", notes, commit, true, assets);

            Confirm(manifest, uploadRuntime != null ? manifest.Packs : new[] { gamePack });
            Console.WriteLine($"\nReleased {version}. Testers get it the next time they open the launcher.");
            Console.WriteLine($"New testers: {Feed.LauncherLink}");
        }

        /// <summary>
        /// Everyone goes back to an earlier build, as a new release with a higher sequence: launchers follow the newest release, whatever is
        /// in it, and won't believe an old manifest. The launcher stays as it is now. The files are the old release's, wherever they are.
        /// </summary>
        public void Rollback()
        {
            string to = _options.Text("to") ?? throw new ReleaseException("Say which version to go back to, with --to.");

            using SigningKey key = LoadKey();
            byte[] signedLive = _store.ReadLive() ?? throw new ReleaseException("Nothing has been released yet.");
            Manifest live = Open(signedLive);

            byte[] signedOld = _store.Read($"download/v{to}/{Feed.ManifestName}") ?? throw new ReleaseException($"There's no release v{to}.");
            Manifest manifest = Open(signedOld);

            string version = NextVersion(live);
            string tag = $"v{version}";
            string notes = _options.Text("notes") ?? $"Back to the build of {to} while something is put right.";

            manifest.Sequence = live.Sequence + 1;
            manifest.Version = version;
            manifest.Published = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            manifest.Notes = NotesWith(live, version, notes);

            string work = Path.GetFullPath(Path.Combine("out", "release", version));
            Directory.CreateDirectory(work);

            byte[] launcherBytes = _store.Read(live.Launcher.Url) ?? throw new ReleaseException("The live launcher can't be read.");
            if (Hashing.OfBytes(launcherBytes) != live.Launcher.Sha256) throw new ReleaseException("The live launcher isn't what its manifest says.");
            string launcher = Path.Combine(work, Feed.LauncherName);
            File.WriteAllBytes(launcher, launcherBytes);

            manifest.Launcher = new LauncherEntry { Url = $"download/{tag}/{Feed.LauncherName}", Size = launcherBytes.Length, Sha256 = live.Launcher.Sha256 };

            string signedPath = Path.Combine(work, Feed.ManifestName);
            WriteSigned(manifest, key, signedPath);

            Console.WriteLine($"Going back to the build of {to}, as {version}.");
            _store.Publish(tag, $"Dino Defenders {version}", notes, manifest.Commit, true, new List<Asset> { new(Feed.LauncherName, launcher), new(Feed.ManifestName, signedPath) });
            Confirm(manifest, new PackEntry[0]);
            Console.WriteLine($"\nReleased {version}. Testers go back the next time they open the launcher.");
        }

        private SigningKey LoadKey()
        {
            SigningKey key = SigningKey.Load(_options.Text("key") ?? SigningKey.DefaultPath);
            if (key.PublicKey != _trusted)
            {
                key.Dispose();
                throw new ReleaseException("The signing key isn't the one launchers trust, so a release signed with it would be ignored by all of them.");
            }
            return key;
        }

        private Manifest Open(byte[] signed)
        {
            try
            {
                return Manifest.Parse(SignedFile.Open(signed, _trusted));
            }
            catch (ManifestException e)
            {
                throw new ReleaseException($"What's live can't be read: {e.Message}");
            }
        }

        private static string NextVersion(Manifest? live)
        {
            if (live == null) return FirstVersion;

            string[] parts = live.Version.Split('.');
            if (!int.TryParse(parts[^1], out int last)) throw new ReleaseException($"Can't count on from {live.Version}: say which version with --version.");
            parts[^1] = (last + 1).ToString();
            return string.Join('.', parts);
        }

        private static string NotesFromCommits(Manifest? live)
        {
            List<string> subjects = Git.SubjectsSince(live?.Commit);
            return string.Join('\n', subjects.Select(s => $"- {s}"));
        }

        private static ReleaseNote[] NotesWith(Manifest? live, string version, string notes)
        {
            var all = new List<ReleaseNote> { new() { Version = version, Text = notes } };
            if (live?.Notes != null) all.AddRange(live.Notes);
            return all.Take(NotesKept).ToArray();
        }

        private static string BuildGame(string version, string work)
        {
            string output = Path.Combine(work, "game");
            Console.WriteLine("Building the game");
            Dotnet($"publish \"{GameProject}\" -c Release -r win-x64 --self-contained true -p:Version={version} -o \"{output}\" --nologo -v q");
            return output;
        }

        // Not stamped with the release: the same source has to make the same bytes, or every release would replace every launcher.
        private static string BuildLauncher()
        {
            Console.WriteLine("Building the launcher");
            Dotnet($"build \"{LauncherProject}\" -c Release --nologo -v q");
            return Path.GetFullPath(Path.Combine("Dino Launcher", "bin", "Release", "net48", Feed.LauncherName));
        }

        private static void Dotnet(string arguments)
        {
            var start = new ProcessStartInfo("dotnet", arguments) { UseShellExecute = false };
            using Process process = Process.Start(start) ?? throw new ReleaseException("dotnet wouldn't start.");
            process.WaitForExit();
            if (process.ExitCode != 0) throw new ReleaseException($"dotnet {arguments} failed.");
        }

        /// <summary>
        /// Every tester gets whatever this makes, so it has to have been seen working first: the exact files, started somewhere else,
        /// through a world and a screenshot. A build that can't start is the worst thing this could send out.
        /// </summary>
        private static void SmokeTest(string game, string work)
        {
            Console.WriteLine("Starting the build to see that it works");
            string folder = Path.Combine(work, "smoke");
            Directory.CreateDirectory(folder);
            string shot = Path.Combine(folder, "smoke.png");

            var start = new ProcessStartInfo(Path.Combine(game, Executable), $"--bot true --seed 303 --archetype isle --frames 60 --shot \"{shot}\"")
            {
                WorkingDirectory = folder,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using Process process = Process.Start(start) ?? throw new ReleaseException("The build wouldn't start.");
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, _) => { };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(SmokeSeconds * 1000))
            {
                process.Kill();
                throw new ReleaseException($"The build was still going after {SmokeSeconds} seconds. Nothing was released.");
            }
            if (process.ExitCode != 0) throw new ReleaseException($"The build crashed, with code {process.ExitCode}. Nothing was released. Its log is in %LOCALAPPDATA%\\DinoDefenders\\logs");
            if (!File.Exists(shot)) throw new ReleaseException("The build closed without taking its screenshot. Nothing was released.");
        }

        /// <summary>Whatever comes from this repository changes with every build. All the rest came from NuGet and the .NET SDK.</summary>
        private static string PackOf(string path)
        {
            bool ours = path.StartsWith("Shaders/", StringComparison.Ordinal) || path.StartsWith("Dino ", StringComparison.Ordinal);
            return ours ? GamePack : RuntimePack;
        }

        private static List<FileEntry> ListFiles(string folder)
        {
            var files = new List<FileEntry>();
            foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                string path = Path.GetRelativePath(folder, file).Replace('\\', '/');
                if (!Manifest.IsSafePath(path)) throw new ReleaseException($"{path} isn't a name that can be released.");
                files.Add(new FileEntry { Path = path, Size = new FileInfo(file).Length, Sha256 = Hashing.OfFile(file), Pack = PackOf(path) });
            }
            files.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            return files;
        }

        /// <summary>
        /// The runtime's address depends on nothing but its files. If they're what's live, nothing is uploaded at all. If not, it goes up
        /// under a tag of its own, marked as a prerelease so that it's never mistaken for the latest game.
        /// </summary>
        private (PackEntry, Action?) PrepareRuntime(Manifest? live, string game, List<FileEntry> files, string work, string commit)
        {
            List<FileEntry> runtime = files.Where(f => f.Pack == RuntimePack).ToList();
            string identity = Hashing.OfBytes(System.Text.Encoding.UTF8.GetBytes(string.Join('\n', runtime.Select(f => $"{f.Path} {f.Sha256}"))));
            string tag = $"runtime-{identity.Substring(0, 12)}";
            string url = $"download/{tag}/{Feed.RuntimePackName}";

            PackEntry? same = live?.Packs.FirstOrDefault(p => p.Url == url);
            if (same != null && _store.SizeOf(url) == same.Size) return (same, null);

            // An earlier run may have got this far before something else failed. It's the same files, but the zip is what's believed.
            if (_store.HasRelease(tag))
            {
                byte[] existing = _store.Read(url) ?? throw new ReleaseException($"{tag} exists without its pack. Delete that release and try again.");
                return (new PackEntry { Name = RuntimePack, Url = url, Size = existing.Length, Sha256 = Hashing.OfBytes(existing) }, null);
            }

            string zip = Path.Combine(work, Feed.RuntimePackName);
            Console.WriteLine("The runtime has changed, so it goes up too");
            WriteZip(zip, game, runtime);
            PackEntry pack = PackFor(RuntimePack, url, zip);

            string notes = "The .NET runtime and libraries, which releases of the game point to. Don't delete this: launchers download it.";
            return (pack, () => _store.Publish(tag, $"Runtime {identity.Substring(0, 12)}", notes, commit, false, new List<Asset> { new(Feed.RuntimePackName, zip) }));
        }

        private static PackEntry PackFor(string name, string url, string zip)
        {
            return new PackEntry { Name = name, Url = url, Size = new FileInfo(zip).Length, Sha256 = Hashing.OfFile(zip) };
        }

        // Same files, same order, same times: the same zip.
        private static void WriteZip(string zip, string folder, IEnumerable<FileEntry> files)
        {
            using ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Create);
            foreach (FileEntry file in files)
            {
                ZipArchiveEntry entry = archive.CreateEntry(file.Path, CompressionLevel.Optimal);
                entry.LastWriteTime = ZipTime;
                using Stream output = entry.Open();
                using FileStream input = File.OpenRead(Path.Combine(folder, file.Path));
                input.CopyTo(output);
            }
        }

        /// <summary>Signs it, then reads it back the way a launcher will, as a release nobody can read is worse than none.</summary>
        private void WriteSigned(Manifest manifest, SigningKey key, string path)
        {
            byte[] payload = manifest.ToBytes();
            byte[] signed = SignedFile.Wrap(payload, key.Sign(payload));

            Manifest check = Manifest.Parse(SignedFile.Open(signed, _trusted));
            check.ValidateLauncher();
            check.ValidateGame();

            File.WriteAllBytes(path, signed);
        }

        /// <summary>
        /// Looks at what's really out there, as a stranger, because the only thing that matters is what launchers will see. What this
        /// release uploaded is read back whole; the rest was checked once already, and only has to still be there.
        /// </summary>
        private void Confirm(Manifest manifest, IReadOnlyList<PackEntry> uploaded)
        {
            Console.WriteLine("Checking what testers will see");
            Manifest? seen = null;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                byte[]? signed = _store.ReadLive();
                seen = signed == null ? null : Open(signed);
                if (seen?.Sequence == manifest.Sequence) break;
                Thread.Sleep(3000);
            }

            if (seen?.Sequence != manifest.Sequence)
            {
                throw new ReleaseException($"The release is up, but launchers are seeing {seen?.Version ?? "nothing"} instead of {manifest.Version}. Check that it's marked as the latest release.");
            }

            foreach (PackEntry pack in manifest.Packs)
            {
                bool good = uploaded.Contains(pack) ? _store.Read(pack.Url) is byte[] bytes && Hashing.OfBytes(bytes) == pack.Sha256 : _store.SizeOf(pack.Url) == pack.Size;
                if (!good) throw new ReleaseException($"The release is up, but {pack.Url} isn't what the manifest says. Roll back.");
            }
        }
    }
}
