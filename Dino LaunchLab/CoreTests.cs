using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Dino_Launcher.Update;
using Microsoft.Win32;

namespace Dino_LaunchLab
{
    /// <summary>A player's machine and the feed it's talking to, with nothing real in either.</summary>
    internal sealed class Bench : IDisposable
    {
        public readonly TemporaryFolder Folder = new TemporaryFolder();
        public readonly TestFeed Feed = new TestFeed();
        public readonly FaultyServer Server = new FaultyServer();
        public readonly InstallPaths Paths;

        public Bench()
        {
            Paths = new InstallPaths(Path.Combine(Folder.Path, "install"));
            Directory.CreateDirectory(Paths.Root);
            Server.Content = Feed.Get;
        }

        public Updater Updater() => new Updater(Paths, Server.Url, Feed.PublicKey, new Downloader { FirstWaitMilliseconds = 5, TimeoutMilliseconds = 1500 });

        public UpdateResult Run(Updater? updater = null, CancellationToken cancel = default) => (updater ?? Updater()).Run(_ => { }, cancel);

        /// <summary>Whether the game on disk is exactly this release.</summary>
        public bool IsWhole(Manifest manifest) => GameInstall.FindWrong(Paths.Game, manifest, CancellationToken.None).Count == 0;

        public void Dispose()
        {
            Server.Dispose();
            Feed.Dispose();
            Folder.Dispose();
        }
    }

    internal sealed class PowerCut : Exception
    {
    }

    internal static class CoreTests
    {
        public static int Run()
        {
            var report = new Report();
            Log.Start(Path.Combine(Path.GetTempPath(), "Dino LaunchLab åäö", "logs"));

            report.Section("Reading a release");
            Rules(report);
            Signatures(report);

            report.Section("Installing and updating");
            FreshInstall(report);
            UpdatingFetchesOnlyWhatChanged(report);
            NothingToDo(report);
            OnlyNotesChanged(report);

            report.Section("Repairing");
            Repairing(report);

            report.Section("Bad lines");
            Resuming(report);
            ServerIgnoresRange(report);
            ServerErrors(report);
            Stalling(report);
            CorruptDownload(report);
            Redirects(report);
            ResumingInALaterRun(report);

            report.Section("Bad releases");
            Tampering(report);
            StaleRelease(report);
            GoingBack(report);
            MissingPack(report);
            WrongPack(report);
            NastyPack(report);
            NewerSchema(report);

            report.Section("Power cuts");
            PowerCuts(report);
            FirstInstallPowerCut(report);

            report.Section("Things in the way");
            GameRunning(report);
            FullDisk(report);
            LeftoversFromEarlier(report);

            report.Section("No connection");
            Offline(report);
            GivingUpQuickly(report);
            BookkeepingFails(report);

            report.Section("Saves");
            SavesAreKept(report);

            report.Section("Windows");
            Shortcuts(report);
            StrippingTheMark(report);
            ACopyKeptAfterInstalling(report);

            return report.Finish();
        }

        private static void Rules(Report report)
        {
            foreach (string path in new[] { "../evil.dll", "a/../../evil.dll", "C:/evil.dll", "/evil.dll", "a\\b.dll", "a//b.dll", "evil.dll:stream", "a/./b", "trailing.", "trailing ", "", "nul\0.dll" })
            {
                report.Check($"Refuses the path \"{path.Replace("\0", "\\0")}\"", !Manifest.IsSafePath(path));
            }
            foreach (string path in new[] { "Dino Defenders.exe", "Shaders/Grass/Grass.vert", "runtimes/win-x64/native/glfw3.dll" })
            {
                report.Check($"Accepts the path \"{path}\"", Manifest.IsSafePath(path));
            }

            using var feed = new TestFeed();
            Manifest good = feed.Release("1.0.0", TestFeed.Build(1));
            good.ValidateLauncher();
            good.ValidateGame();

            report.Check("A file naming a pack that doesn't exist is refused", Refused(feed, m => m.Files[0].Pack = "nothing"));
            report.Check("A file listed twice, differing only by case, is refused", Refused(feed, m =>
            {
                m.Files[1].Path = m.Files[0].Path.ToUpperInvariant();
            }));
            report.Check("A malformed hash is refused", Refused(feed, m => m.Files[0].Sha256 = "abc"));
            report.Check("An executable that isn't in the release is refused", Refused(feed, m => m.Executable = "other.exe"));
            report.Check("A manifest with no files is refused", Refused(feed, m => m.Files = new FileEntry[0]));
        }

        private static bool Refused(TestFeed feed, Action<Manifest> tamper)
        {
            Manifest manifest = feed.Release("1.0.0", TestFeed.Build(1), tamper);
            try
            {
                Manifest.Parse(manifest.ToBytes()).ValidateGame();
                return false;
            }
            catch (ManifestException)
            {
                return true;
            }
        }

        private static void Signatures(Report report)
        {
            using var feed = new TestFeed();
            using var stranger = new TestFeed();
            feed.Release("1.0.0", TestFeed.Build(1));
            byte[] signed = feed.Get(Feed.ManifestPath)!;

            report.Check("A release opens with its own key", Opens(signed, feed.PublicKey));
            report.Check("A release doesn't open with someone else's key", !Opens(signed, stranger.PublicKey));

            int checks = 0, refused = 0;
            var random = new Random(5);
            for (int i = 0; i < 400; i++)
            {
                var damaged = (byte[])signed.Clone();
                damaged[random.Next(damaged.Length)] ^= (byte)(1 << random.Next(8));
                checks++;
                if (!Opens(damaged, feed.PublicKey)) refused++;
            }
            report.Check($"{checks} single-bit changes anywhere in the file are all noticed", refused == checks, $"{refused} of {checks}");

            report.Check("Half a file is refused", !Opens(signed.Take(signed.Length / 2).ToArray(), feed.PublicKey));
            report.Check("An empty file is refused", !Opens(new byte[0], feed.PublicKey));
            report.Check("A web page saying 'not found' is refused", !Opens(System.Text.Encoding.UTF8.GetBytes("<html>Not Found</html>\n"), feed.PublicKey));
            report.Check("A launcher with no key refuses everything", !Opens(signed, ""));
        }

        private static bool Opens(byte[] file, string key)
        {
            try
            {
                SignedFile.Open(file, key);
                return true;
            }
            catch (ManifestException)
            {
                return false;
            }
        }

        private static void FreshInstall(Report report)
        {
            using var bench = new Bench();
            Manifest manifest = bench.Feed.Release("1.0.0", TestFeed.Build(1));

            var phases = new List<Phase>();
            long lastDone = 0;
            bool backwards = false;
            UpdateResult result = bench.Updater().Run(p =>
            {
                if (!phases.Contains(p.Phase)) phases.Add(p.Phase);
                if (p.Phase == Phase.Downloading)
                {
                    if (p.Done < lastDone) backwards = true;
                    lastDone = p.Done;
                }
            }, CancellationToken.None);

            report.Check("A fresh install ends up playable", result.Build?.Version == "1.0.0" && result.Problem == null, result.Problem ?? "");
            report.Check("Every file is there and right", bench.IsWhole(manifest));
            report.Check("The manifest is kept with the game", File.Exists(bench.Paths.InstalledManifest));
            report.Check("Both packs were fetched", bench.Server.CountOf("runtime.zip") == 1 && bench.Server.CountOf("game.zip") == 1);
            report.Check("Progress goes through the phases in order", phases.SequenceEqual(new[] { Phase.Checking, Phase.Downloading, Phase.Installing }), string.Join(",", phases));
            report.Check("Progress never runs backwards", !backwards);
            report.Check("Nothing is left lying around", !Directory.Exists(bench.Paths.Staging) && !Directory.Exists(bench.Paths.Retired) && !Directory.Exists(bench.Paths.Downloads) && !File.Exists(bench.Paths.StagingReady));
        }

        private static void UpdatingFetchesOnlyWhatChanged(Report report)
        {
            using var bench = new Bench();
            bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Run();
            bench.Server.ClearRequests();

            Dictionary<string, byte[]> next = TestFeed.Build(2);
            next["Shaders/New.frag"] = new byte[] { 1, 2, 3 };
            next.Remove("Shaders/Grass/Grass.vert");
            Manifest manifest = bench.Feed.Release("1.0.1", next);
            File.WriteAllText(bench.Paths.GameFile("stray.txt"), "nobody asked for this");

            UpdateResult result = bench.Run();
            report.Check("An update ends up on the new version", result.Build?.Version == "1.0.1", result.Problem ?? "");
            report.Check("The new build is whole", bench.IsWhole(manifest));
            report.Check("Only the game pack was fetched, not the runtime", bench.Server.CountOf("game.zip") == 1 && bench.Server.CountOf("runtime.zip") == 0);
            report.Check("A file added by the release arrives", File.Exists(bench.Paths.GameFile("Shaders/New.frag")));
            report.Check("A file dropped from the release goes", !File.Exists(bench.Paths.GameFile("Shaders/Grass/Grass.vert")));
            report.Check("Leftovers go with the old folder", !File.Exists(bench.Paths.GameFile("stray.txt")));
        }

        private static void NothingToDo(Report report)
        {
            using var bench = new Bench();
            bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Run();
            bench.Server.ClearRequests();
            DateTime before = Directory.GetCreationTimeUtc(bench.Paths.Game);

            UpdateResult result = bench.Run();
            report.Check("With nothing new, the only request is for the manifest", bench.Server.Requests.Count == 1, $"{bench.Server.Requests.Count} requests");
            report.Check("It's still playable", result.Build?.Version == "1.0.0");
            report.Check("The game's folder isn't touched", Directory.GetCreationTimeUtc(bench.Paths.Game) == before);
        }

        private static void OnlyNotesChanged(Report report)
        {
            using var bench = new Bench();
            bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Run();
            bench.Server.ClearRequests();

            bench.Feed.Release("1.0.1", TestFeed.Build(1));
            UpdateResult result = bench.Run();
            Manifest? installed = new GameInstall(bench.Paths).ReadInstalled(bench.Feed.PublicKey);
            report.Check("A release with the same files downloads nothing", bench.Server.Requests.Count == 1);
            report.Check("It's still recorded as installed", result.Build?.Version == "1.0.1" && installed?.Version == "1.0.1", installed?.Version ?? "none");
        }

        private static void Repairing(Report report)
        {
            using var bench = new Bench();
            Manifest manifest = bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Run();

            bench.Server.ClearRequests();
            File.Delete(bench.Paths.GameFile("Shaders/Terrain.frag"));
            UpdateResult result = bench.Run();
            report.Check("A deleted file comes back", result.Build != null && bench.IsWhole(manifest), result.Problem ?? "");
            report.Check("Only its own pack was fetched", bench.Server.CountOf("game.zip") == 1 && bench.Server.CountOf("runtime.zip") == 0);

            bench.Server.ClearRequests();
            byte[] dll = File.ReadAllBytes(bench.Paths.GameFile("glfw3.dll"));
            dll[100] ^= 1;
            File.WriteAllBytes(bench.Paths.GameFile("glfw3.dll"), dll);
            result = bench.Run();
            report.Check("A file changed by one bit, at the same size, is put right", result.Build != null && bench.IsWhole(manifest), result.Problem ?? "");
            report.Check("Only its own pack was fetched", bench.Server.CountOf("runtime.zip") == 1 && bench.Server.CountOf("game.zip") == 0);

            Directory.Delete(bench.Paths.Game, true);
            result = bench.Run();
            report.Check("With the whole folder gone, it installs again", result.Build != null && bench.IsWhole(manifest), result.Problem ?? "");

            File.SetAttributes(bench.Paths.GameFile("glfw3.dll"), FileAttributes.ReadOnly);
            bench.Feed.Release("1.0.1", TestFeed.Build(2));
            result = bench.Run();
            report.Check("A read-only file doesn't stop an update", result.Build?.Version == "1.0.1", result.Problem ?? "");
        }

        private static void Resuming(Report report)
        {
            using var bench = new Bench();
            Manifest manifest = bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Server.Inject("runtime.zip", new Fault(FaultKind.DropAfter, 100000), new Fault(FaultKind.DropAfter, 100000), new Fault(FaultKind.DropAfter, 100000));

            UpdateResult result = bench.Run();
            List<Request> requests = bench.Server.Requests.Where(r => r.Path.EndsWith("runtime.zip")).ToList();

            report.Check("A download that keeps being cut off gets there", result.Build != null && bench.IsWhole(manifest), result.Problem ?? "");
            report.Check("It took four requests", requests.Count == 4, $"{requests.Count}");
            report.Check("Each one carried on from where the last stopped", requests.Skip(1).All(r => r.Header("Range").StartsWith("bytes=")), string.Join(" ", requests.Select(r => r.Header("Range"))));
            report.Check("Nothing was sent twice", requests.Sum(r => r.BytesSent) == manifest.Packs.First(p => p.Name == "runtime").Size, $"{requests.Sum(r => r.BytesSent)}");
        }

        private static void ServerIgnoresRange(Report report)
        {
            using var bench = new Bench();
            Manifest manifest = bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Server.Inject("runtime.zip", new Fault(FaultKind.DropAfter, 100000), new Fault(FaultKind.IgnoreRange));

            UpdateResult result = bench.Run();
            report.Check("A server that sends the whole file when asked for part of it still works", result.Build != null && bench.IsWhole(manifest), result.Problem ?? "");
        }

        private static void ServerErrors(Report report)
        {
            using var bench = new Bench();
            Manifest manifest = bench.Feed.Release("1.0.0", TestFeed.Build(1));

            bench.Server.Inject(Feed.ManifestPath, new Fault(FaultKind.Status, 503), new Fault(FaultKind.Status, 502));
            bench.Server.Inject("game.zip", new Fault(FaultKind.Status, 500), new Fault(FaultKind.Status, 429), new Fault(FaultKind.Status, 403));
            UpdateResult result = bench.Run();
            report.Check("Passing server errors are waited out on a first install", result.Build != null && bench.IsWhole(manifest), result.Problem ?? "");

            Manifest next = bench.Feed.Release("1.0.1", TestFeed.Build(2));
            bench.Server.Inject(Feed.ManifestPath, new Fault(FaultKind.Status, 503));
            result = bench.Run();
            report.Check("With the game installed, one is still shrugged off", result.Build?.Version == "1.0.1" && bench.IsWhole(next), result.Problem ?? "");

            bench.Feed.Release("1.0.2", TestFeed.Build(3));
            bench.Server.Inject(Feed.ManifestPath, new Fault(FaultKind.Status, 503), new Fault(FaultKind.Status, 503));
            result = bench.Run();
            report.Check("But it won't keep a player waiting to play what they have", result.Build?.Version == "1.0.1" && result.Problem != null, result.Problem ?? "");

            using var dead = new Bench();
            dead.Feed.Release("1.0.0", TestFeed.Build(1));
            dead.Server.Inject(Feed.ManifestPath, Enumerable.Repeat(new Fault(FaultKind.Status, 500), 50).ToArray());
            result = dead.Run();
            report.Check("A server that stays broken is given up on", result.Build == null && result.Problem != null);
        }

        private static void Stalling(Report report)
        {
            using var bench = new Bench();
            Manifest manifest = bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Server.Inject("runtime.zip", new Fault(FaultKind.Stall, 50000));

            DateTime started = DateTime.UtcNow;
            UpdateResult result = bench.Run();
            double seconds = (DateTime.UtcNow - started).TotalSeconds;
            report.Check("A line that goes silent is noticed and picked up again", result.Build != null && bench.IsWhole(manifest), result.Problem ?? "");
            report.Check("Without waiting for it forever", seconds < 12, $"{seconds:0.0}s");
        }

        private static void CorruptDownload(Report report)
        {
            using var bench = new Bench();
            Manifest first = bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Run();

            Manifest manifest = bench.Feed.Release("1.0.1", TestFeed.Build(2));
            bench.Server.Inject("game.zip", new Fault(FaultKind.Corrupt));
            UpdateResult result = bench.Run();
            report.Check("A download damaged on the way is fetched again", result.Build?.Version == "1.0.1" && bench.IsWhole(manifest), result.Problem ?? "");

            Manifest third = bench.Feed.Release("1.0.2", TestFeed.Build(3));
            bench.Server.Inject("game.zip", Enumerable.Repeat(new Fault(FaultKind.Corrupt), 50).ToArray());
            result = bench.Run();
            report.Check("If it never arrives whole, the update is abandoned", result.Problem != null && result.Build?.Version == "1.0.1", result.Problem ?? "no problem reported");
            report.Check("And the installed game is untouched", bench.IsWhole(manifest));
        }

        private static void Redirects(Report report)
        {
            using var bench = new Bench();
            Manifest manifest = bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Server.RedirectToCdn = true;
            bench.Server.Inject("runtime.zip", new Fault(FaultKind.DropAfter, 100000));

            UpdateResult result = bench.Run();
            List<Request> onCdn = bench.Server.Requests.Where(r => r.Path.StartsWith("/cdn/") && r.Path.EndsWith("runtime.zip")).ToList();
            report.Check("Everything arrives by way of a redirect to another host, as on GitHub", result.Build != null && bench.IsWhole(manifest), result.Problem ?? "");
            report.Check("A resumed download keeps its range across the redirect", onCdn.Count == 2 && onCdn[1].Header("Range").StartsWith("bytes="), string.Join(" | ", onCdn.Select(r => r.Header("Range"))));
        }

        private static void ResumingInALaterRun(Report report)
        {
            using var bench = new Bench();
            Manifest manifest = bench.Feed.Release("1.0.0", TestFeed.Build(1, runtimeBytes: 3000000));
            bench.Server.Inject("runtime.zip", new Fault(FaultKind.Stall, 1000000));

            using var closing = new CancellationTokenSource();
            bool stopped = false;
            try
            {
                bench.Updater().Run(p =>
                {
                    if (p.Phase == Phase.Downloading && p.Done > 500000) closing.Cancel();
                }, closing.Token);
            }
            catch (OperationCanceledException)
            {
                stopped = true;
            }

            report.Check("Closing the launcher mid-download stops it at once", stopped);
            report.Check("The game isn't half there", !Directory.Exists(bench.Paths.Game));

            bench.Server.ClearRequests();
            UpdateResult result = bench.Run();
            Request runtime = bench.Server.Requests.First(r => r.Path.EndsWith("runtime.zip"));
            report.Check("Opening it again finishes the job", result.Build != null && bench.IsWhole(manifest), result.Problem ?? "");
            report.Check("From where it had got to", runtime.Header("Range").StartsWith("bytes="), runtime.Header("Range"));
        }

        private static void Tampering(Report report)
        {
            using var bench = new Bench();
            Manifest first = bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Run();

            bench.Feed.Release("1.0.1", TestFeed.Build(2));
            byte[] signed = bench.Feed.Get(Feed.ManifestPath)!;
            byte[] forged = (byte[])signed.Clone();
            forged[forged.Length - 40] ^= 1;
            bench.Feed.Put(Feed.ManifestPath, forged);

            bench.Server.ClearRequests();
            UpdateResult result = bench.Run();
            report.Check("A release changed after signing is ignored", result.Build?.Version == "1.0.0" && result.Problem != null, result.Problem ?? "");
            report.Check("Nothing it pointed to was even fetched", bench.Server.CountOf(".zip") == 0);
            report.Check("The game stays playable", bench.IsWhole(first));

            using var stranger = new TestFeed();
            Manifest evil = bench.Feed.Release("6.6.6", TestFeed.Build(6));
            bench.Feed.PublishSignedBy(stranger, evil);
            result = bench.Run();
            report.Check("A release signed by anyone else is ignored", result.Build?.Version == "1.0.0" && result.Problem != null, result.Build?.Version ?? "none");

            bench.Feed.Put(Feed.ManifestPath, System.Text.Encoding.UTF8.GetBytes("<html>Sorry, this site is blocked</html>"));
            result = bench.Run();
            report.Check("A captive portal's web page is ignored", result.Build?.Version == "1.0.0" && result.Problem != null);
        }

        private static void StaleRelease(Report report)
        {
            using var bench = new Bench();
            bench.Feed.Release("1.0.0", TestFeed.Build(1));
            byte[] old = bench.Feed.Get(Feed.ManifestPath)!;
            Manifest second = bench.Feed.Release("1.0.1", TestFeed.Build(2));
            bench.Run();

            bench.Feed.Put(Feed.ManifestPath, old);
            UpdateResult result = bench.Run();
            report.Check("An older signed release, from a cache or an attacker, is ignored", result.Build?.Version == "1.0.1" && bench.IsWhole(second), result.Build?.Version ?? "none");
            report.Check("Quietly: it's nothing the player needs to hear about", result.Problem == null, result.Problem ?? "");
        }

        private static void GoingBack(Report report)
        {
            using var bench = new Bench();
            bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Feed.Release("1.0.1", TestFeed.Build(2));
            bench.Run();

            Manifest back = bench.Feed.Release("1.0.2", TestFeed.Build(1));
            UpdateResult result = bench.Run();
            report.Check("Going back to an older build, as a new release, works", result.Build?.Version == "1.0.2" && bench.IsWhole(back), result.Problem ?? "");
        }

        private static void MissingPack(Report report)
        {
            using var bench = new Bench();
            Manifest first = bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Run();

            Manifest next = bench.Feed.Release("1.0.1", TestFeed.Build(2));
            bench.Feed.Put(next.Packs.First(p => p.Name == "game").Url, new byte[0]);
            bench.Server.Content = path => path.EndsWith("game.zip") ? null : bench.Feed.Get(path);

            bench.Server.ClearRequests();
            UpdateResult result = bench.Run();
            report.Check("A pack that isn't there fails cleanly", result.Problem != null && result.Build?.Version == "1.0.0", result.Problem ?? "");
            report.Check("Without hammering the server: a 404 won't change", bench.Server.CountOf("game.zip") == 1, $"{bench.Server.CountOf("game.zip")} tries");
            report.Check("The old build is intact", bench.IsWhole(first));
        }

        private static void WrongPack(Report report)
        {
            using var bench = new Bench();
            Manifest first = bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Run();

            Manifest next = bench.Feed.Release("1.0.1", TestFeed.Build(2));
            PackEntry pack = next.Packs.First(p => p.Name == "game");
            byte[] other = TestFeed.Zip(TestFeed.Build(9).Where(f => f.Key.StartsWith("Dino ")));
            bench.Feed.Put(pack.Url, other);

            UpdateResult result = bench.Run();
            report.Check("Files swapped on the host are refused by their hash", result.Problem != null && result.Build?.Version == "1.0.0", result.Problem ?? "");
            report.Check("The old build is intact", bench.IsWhole(first));
        }

        private static void NastyPack(Report report)
        {
            using var bench = new Bench();
            Dictionary<string, byte[]> build = TestFeed.Build(1);

            // Correctly signed, with something extra in the zip trying to get out of the game's folder.
            Manifest manifest = bench.Feed.Release("1.0.0", build, m =>
            {
                PackEntry pack = m.Packs.First(p => p.Name == "game");
                var entries = build.Where(f => f.Key.StartsWith("Dino ") || f.Key.StartsWith("Shaders/")).ToList();
                entries.Add(new KeyValuePair<string, byte[]>("../../escaped.txt", new byte[] { 1 }));
                entries.Add(new KeyValuePair<string, byte[]>("C:/Windows/escaped.txt", new byte[] { 1 }));
                byte[] zip = TestFeed.Zip(entries);
                pack.Size = zip.Length;
                pack.Sha256 = Hashing.OfBytes(zip);
                bench.Feed.Put(pack.Url, zip);
            });

            UpdateResult result = bench.Run();
            report.Check("Names in a zip that point outside the folder go nowhere", !File.Exists(Path.Combine(bench.Paths.Root, "escaped.txt")) && !File.Exists(Path.Combine(bench.Folder.Path, "escaped.txt")));
            report.Check("Only what the manifest lists is unpacked", result.Build != null && !Directory.GetFiles(bench.Paths.Game, "escaped.txt", SearchOption.AllDirectories).Any());

            using var liar = new Bench();
            Dictionary<string, byte[]> small = TestFeed.Build(2);
            liar.Feed.Release("1.0.0", small, m =>
            {
                // The manifest says one thing about a file, the zip holds another: a mistake, or a zip that won't stop inflating.
                m.Files.First(f => f.Path == "Dino Engine.dll").Size = 5;
            });
            result = liar.Run();
            report.Check("A file that isn't the size the manifest says is refused", result.Build == null && result.Problem != null, result.Problem ?? "");
            report.Check("And nothing is installed", !Directory.Exists(liar.Paths.Game));
        }

        private static void NewerSchema(Report report)
        {
            using var bench = new Bench();
            bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Run();

            bench.Feed.Release("2.0.0", TestFeed.Build(2), m => m.Schema = Manifest.CurrentSchema + 1);

            bool asked = false;
            Updater updater = bench.Updater();
            updater.ReplaceLauncher = (entry, _) =>
            {
                asked = true;
                return false;
            };
            UpdateResult result = bench.Run(updater);
            report.Check("A release from the future can still replace the launcher", asked);
            report.Check("If that doesn't happen, it's left alone and the game still plays", result.Build?.Version == "1.0.0" && result.Problem != null, result.Problem ?? "");
        }

        private static void PowerCuts(Report report)
        {
            // Which build each cut must leave, by recovery alone. Asked of Recover with no network, as the next run would otherwise
            // download its way out of any mistake here, and someone offline after a power cut can't.
            foreach ((string step, bool becomesNew) in new[] { ("staged", false), ("retired", true), ("swapped", true), ("marked", true) })
            {
                using var bench = new Bench();
                Manifest old = bench.Feed.Release("1.0.0", TestFeed.Build(1));
                bench.Run();
                Manifest next = bench.Feed.Release("1.0.1", TestFeed.Build(2));

                Updater updater = bench.Updater();
                updater.Install.Checkpoint = at =>
                {
                    if (at == step) throw new PowerCut();
                };

                try
                {
                    bench.Run(updater);
                }
                catch (PowerCut)
                {
                }

                bench.Server.ClearRequests();
                var install = new GameInstall(bench.Paths);
                install.Recover();
                report.Check($"Cut at '{step}': recovery alone leaves the {(becomesNew ? "new" : "old")} game, whole", bench.IsWhole(becomesNew ? next : old));
                report.Check($"Cut at '{step}': the manifest matches the files", install.ReadInstalled(bench.Feed.PublicKey) is Manifest installed && bench.IsWhole(installed));

                UpdateResult result = bench.Run();
                report.Check($"Cut at '{step}': the next run ends on the new version", result.Build?.Version == "1.0.1" && bench.IsWhole(next), result.Problem ?? "");
                report.Check($"Cut at '{step}': nothing is left over", !Directory.Exists(bench.Paths.Staging) && !Directory.Exists(bench.Paths.Retired) && !File.Exists(bench.Paths.StagingReady));
            }
        }

        private static void FirstInstallPowerCut(Report report)
        {
            foreach (string step in new[] { "staged", "swapped" })
            {
                using var bench = new Bench();
                Manifest manifest = bench.Feed.Release("1.0.0", TestFeed.Build(1));

                Updater updater = bench.Updater();
                updater.Install.Checkpoint = at =>
                {
                    if (at == step) throw new PowerCut();
                };
                try
                {
                    bench.Run(updater);
                }
                catch (PowerCut)
                {
                }

                UpdateResult result = bench.Run();
                report.Check($"First install cut at '{step}': the next run finishes it", result.Build != null && bench.IsWhole(manifest), result.Problem ?? "");
            }
        }

        private static void GameRunning(Report report)
        {
            using var bench = new Bench();
            Manifest first = bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Run();
            Manifest next = bench.Feed.Release("1.0.1", TestFeed.Build(2));

            // What a running exe does to its own folder: it can't be renamed, as a file in it is held open.
            var held = new FileStream(bench.Paths.GameFile("Dino Engine.dll"), FileMode.Open, FileAccess.Read, FileShare.Read);
            Updater updater = bench.Updater();
            updater.Install.SwapWait = 5;
            UpdateResult result = bench.Run(updater);
            report.Check("With the game's files in use, the update stops and says why", result.Problem != null && result.Problem.Contains("using"), result.Problem ?? "");
            report.Check("The installed game is unharmed, and playable", result.Build?.Version == "1.0.0" && bench.IsWhole(first), result.Build?.Version ?? "none");

            held.Dispose();
            bench.Server.ClearRequests();
            result = bench.Run();
            report.Check("It goes through once they're let go", result.Build?.Version == "1.0.1" && bench.IsWhole(next), result.Problem ?? "");
            report.Check("Using what it already downloaded", bench.Server.CountOf("game.zip") == 0, $"{bench.Server.CountOf("game.zip")} fetches");
        }

        private static void FullDisk(Report report)
        {
            using var bench = new Bench();
            Manifest first = bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Run();
            bench.Feed.Release("1.0.1", TestFeed.Build(2));

            bench.Server.ClearRequests();
            Updater updater = bench.Updater();
            updater.Install.FreeSpace = () => 1000;
            UpdateResult result = bench.Run(updater);
            report.Check("With no room, it says so before downloading anything", result.Problem != null && result.Problem.Contains("disk space") && bench.Server.CountOf(".zip") == 0, result.Problem ?? "");
            report.Check("And the game still plays", result.Build?.Version == "1.0.0" && bench.IsWhole(first));
        }

        private static void LeftoversFromEarlier(Report report)
        {
            using var bench = new Bench();
            Manifest first = bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Run();

            Directory.CreateDirectory(bench.Paths.Staging);
            File.WriteAllText(Path.Combine(bench.Paths.Staging, "half.dll"), "half");
            Directory.CreateDirectory(bench.Paths.Retired);
            UpdateResult result = bench.Run();
            report.Check("A half-built folder from an earlier crash is cleared away", !Directory.Exists(bench.Paths.Staging) && !Directory.Exists(bench.Paths.Retired));
            report.Check("Without disturbing the game", result.Build != null && bench.IsWhole(first));

            // Recovery alone again: the next run would hide a mistake here by downloading the game.
            Directory.Move(bench.Paths.Game, bench.Paths.Retired);
            Directory.CreateDirectory(bench.Paths.Staging);
            File.WriteAllText(Path.Combine(bench.Paths.Staging, "half.dll"), "half");
            new GameInstall(bench.Paths).Recover();
            report.Check("A staging folder that was never finished is never swapped in: the old game comes back", bench.IsWhole(first) && !File.Exists(bench.Paths.GameFile("half.dll")));
            report.Check("Cleared away, with nothing else lost", !Directory.Exists(bench.Paths.Staging) && !Directory.Exists(bench.Paths.Retired));

            bench.Server.Dispose();
            result = bench.Run();
            report.Check("And it plays with no connection at all", result.Build?.Version == "1.0.0", result.Problem ?? "");
        }

        private static void Offline(Report report)
        {
            using var bench = new Bench();
            Manifest first = bench.Feed.Release("1.0.0", TestFeed.Build(1));

            using (var gone = new Bench())
            {
                gone.Server.Dispose();
                UpdateResult nothing = gone.Run();
                report.Check("Offline with nothing installed: nothing to play, and it says why", nothing.Build == null && nothing.Problem != null, nothing.Problem ?? "");
            }

            bench.Run();
            bench.Server.Dispose();

            UpdateResult result = bench.Run();
            report.Check("Offline with the game installed: it plays", result.Build?.Version == "1.0.0", result.Problem ?? "");
            report.Check("And mentions that it couldn't check", result.Problem != null);

            File.Delete(bench.Paths.GameFile("glfw3.dll"));
            result = bench.Run();
            report.Check("Offline and damaged: it won't start a broken game, and says why", result.Build == null && result.Problem != null && result.Problem.Contains("damaged"), result.Problem ?? "");
        }

        private static void BookkeepingFails(Report report)
        {
            using var bench = new Bench();
            bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Run();
            bench.Feed.Release("1.0.1", TestFeed.Build(1));

            // The files are already right, so all that's left is writing that down, which something is stopping.
            using var held = new FileStream(bench.Paths.InstalledManifest, FileMode.Open, FileAccess.Read, FileShare.None);
            UpdateResult result = bench.Run();
            report.Check("Failing to write down the release doesn't stop anyone playing", result.Build?.Version == "1.0.1", result.Problem ?? "no build");
        }

        private static void GivingUpQuickly(Report report)
        {
            using var bench = new Bench();
            bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Run();
            bench.Server.Dispose();

            // As the launcher has it, not as the other tests speed it up.
            var updater = new Updater(bench.Paths, bench.Server.Url, bench.Feed.PublicKey, new Downloader());
            DateTime started = DateTime.UtcNow;
            UpdateResult result = bench.Run(updater);
            double seconds = (DateTime.UtcNow - started).TotalSeconds;
            report.Check("Offline, Play lights up in a few seconds, not after a long wait", result.Build != null && seconds < 5, $"{seconds:0.0}s");
        }

        private static void SavesAreKept(Report report)
        {
            using var bench = new Bench();
            Directory.CreateDirectory(bench.Paths.SaveFolder);
            string save = Path.Combine(bench.Paths.SaveFolder, "save.json");

            bench.Feed.Release("1.0.0", TestFeed.Build(1));
            bench.Run();
            File.WriteAllText(save, "my hero");

            bench.Feed.Release("1.0.1", TestFeed.Build(2));
            bench.Run();
            string backup = Path.Combine(bench.Paths.SaveFolder, "backups", "save-from-1.0.0.json");
            report.Check("The save is left where it is", File.ReadAllText(save) == "my hero");
            report.Check("A copy is put aside first, named for the build that wrote it", File.Exists(backup) && File.ReadAllText(backup) == "my hero");

            for (int i = 2; i < 10; i++)
            {
                bench.Feed.Release($"1.0.{i}", TestFeed.Build(i + 1));
                bench.Run();
            }
            string[] kept = Directory.GetFiles(Path.Combine(bench.Paths.SaveFolder, "backups")).Select(Path.GetFileName).ToArray();
            report.Check("Only the last few are kept", kept.Length == 5, string.Join(" ", kept));
            report.Check("Those being the newest, even with nothing played between updates", kept.Contains("save-from-1.0.8.json") && !kept.Contains("save-from-1.0.0.json"), string.Join(" ", kept));

            int before = Directory.GetFiles(Path.Combine(bench.Paths.SaveFolder, "backups")).Length;
            File.Delete(bench.Paths.GameFile("glfw3.dll"));
            bench.Run();
            report.Check("A repair isn't an update, and takes no copy", Directory.GetFiles(Path.Combine(bench.Paths.SaveFolder, "backups")).Length == before);
        }

        private static void Shortcuts(Report report)
        {
            using var bench = new Bench();
            string desktop = Path.Combine(bench.Folder.Path, "Desktop");
            string menu = Path.Combine(bench.Folder.Path, "Start Menu");
            Directory.CreateDirectory(desktop);
            string key = @"Software\Dino LaunchLab Test Uninstall";

            var installer = new Installer(bench.Paths) { Desktop = desktop, StartMenu = menu, UninstallKey = key };
            File.WriteAllBytes(bench.Paths.LauncherExe, new byte[] { 1 });
            installer.AddToWindows("1.0.0");

            string link = Path.Combine(desktop, "Dino Defenders.lnk");
            byte[] bytes = File.Exists(link) ? File.ReadAllBytes(link) : new byte[0];
            string text = System.Text.Encoding.Unicode.GetString(bytes);
            report.Check("A desktop shortcut is made", bytes.Length > 0);
            report.Check("It points at the installed launcher", text.Contains(bench.Paths.LauncherExe), text.Length.ToString());
            report.Check("A start menu shortcut is made", File.Exists(Path.Combine(menu, "Dino Defenders.lnk")));

            using (RegistryKey? entry = Registry.CurrentUser.OpenSubKey(key))
            {
                report.Check("It appears in Installed Apps", entry?.GetValue("DisplayName") as string == "Dino Defenders");
                report.Check("With a way to uninstall it", (entry?.GetValue("UninstallString") as string)?.Contains("--uninstall") == true);
            }

            installer.RemoveFromWindows();
            report.Check("Uninstalling removes the shortcuts", !File.Exists(link) && !File.Exists(Path.Combine(menu, "Dino Defenders.lnk")));

            // As when Controlled Folder Access stops a program it doesn't know from writing to the desktop.
            string blocked = Path.Combine(bench.Folder.Path, "Blocked");
            File.WriteAllText(blocked, "a file where a folder should be");
            var refused = new Installer(bench.Paths) { Desktop = blocked, StartMenu = menu, UninstallKey = key };
            bool survived = true;
            try
            {
                refused.AddToWindows("1.0.0");
            }
            catch (Exception)
            {
                survived = false;
            }
            report.Check("Being refused the desktop doesn't stop an install", survived);
            report.Check("And the rest still happens", File.Exists(Path.Combine(menu, "Dino Defenders.lnk")));
            installer.RemoveFromWindows();
            using (RegistryKey? entry = Registry.CurrentUser.OpenSubKey(key)) report.Check("And the Installed Apps entry", entry == null);

            string saves = Path.Combine(bench.Folder.Path, "Roaming");
            Directory.CreateDirectory(saves);
            File.WriteAllText(Path.Combine(saves, "save.json"), "my hero");
            Directory.CreateDirectory(bench.Paths.Game);
            File.WriteAllText(bench.Paths.GameFile("a.dll"), "a");
            installer.RemoveFilesAfterExit();
            DateTime giveUp = DateTime.UtcNow.AddSeconds(15);
            while (Directory.Exists(bench.Paths.Root) && DateTime.UtcNow < giveUp) Thread.Sleep(100);
            report.Check("The install folder is removed, spaces and non-ASCII letters and all", !Directory.Exists(bench.Paths.Root));
            report.Check("Saves aren't in it, so they stay", File.Exists(Path.Combine(saves, "save.json")));
        }

        private static void StrippingTheMark(Report report)
        {
            using var bench = new Bench();
            string download = Path.Combine(bench.Folder.Path, Feed.LauncherName);
            File.WriteAllBytes(download, new byte[] { 1, 2, 3 });
            Streams.WriteMark(download);

            report.Check("The test file really has a mark of the web", Streams.HasMark(download));
            new Installer(bench.Paths).CopyLauncher(download);
            report.Check("The installed copy doesn't, so Windows won't warn every time", File.Exists(bench.Paths.LauncherExe) && !Streams.HasMark(bench.Paths.LauncherExe));
        }

        private static void ACopyKeptAfterInstalling(Report report)
        {
            using var bench = new Bench();
            var installer = new Installer(bench.Paths);
            var game = new GameInstall(bench.Paths);
            byte[] released = { 1, 2, 3 };  // What TestFeed says every launcher is.

            bench.Feed.Release("0.1.0", TestFeed.Build(1));
            File.WriteAllBytes(bench.Paths.LauncherExe, released);
            report.Check("With no game yet, a download goes in", !installer.HasSoundLauncher(game.ReadInstalled(bench.Feed.PublicKey)));

            bench.Run();
            Manifest? installed = game.ReadInstalled(bench.Feed.PublicKey);
            report.Check("The launcher its release names is left alone", installer.HasSoundLauncher(installed));

            File.WriteAllBytes(bench.Paths.LauncherExe, new byte[] { 9 });
            report.Check("Any other launcher is replaced, as it may be damaged", !installer.HasSoundLauncher(installed));

            File.Delete(bench.Paths.LauncherExe);
            report.Check("As is a missing one", !installer.HasSoundLauncher(installed));

            File.WriteAllBytes(bench.Paths.LauncherExe, released);
            Installer.RememberDeath(bench.Paths);
            report.Check("Even the right one is replaced once it has died, or a fix could never get in", !installer.HasSoundLauncher(installed));

            string download = Path.Combine(bench.Folder.Path, "download.exe");
            File.WriteAllBytes(download, released);
            installer.CopyLauncher(download);
            report.Check("And a launcher put in place starts with a clean slate", installer.HasSoundLauncher(installed));
        }
    }
}
