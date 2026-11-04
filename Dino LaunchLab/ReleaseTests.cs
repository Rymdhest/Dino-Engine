using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Dino_Launcher.Update;

namespace Dino_LaunchLab
{
    /// <summary>
    /// The real release tool, from the outside. Everything it makes is then read by the launcher's own code on the launcher's own framework,
    /// so a manifest or a zip that only one side understands fails here rather than on a tester's machine.
    /// </summary>
    internal static class ReleaseTests
    {
        public static int Run()
        {
            var report = new Report();
            Log.Start(Path.Combine(Path.GetTempPath(), "Dino LaunchLab åäö", "logs"));

            using var folder = new TemporaryFolder();
            var tool = new ReleaseTool(folder.Path);
            string key = Path.Combine(folder.Path, "key.pem");
            string source = Path.Combine(folder.Path, "Feed.cs");

            report.Section("The signing key");
            File.WriteAllText(source, "public const string PublicKey = \"\";");
            int made = tool.Run("keygen", "--key", key, "--feed-source", source);
            string trust = Regex.Match(File.ReadAllText(source), "PublicKey = \"([^\"]*)\"").Groups[1].Value;
            report.Check("keygen makes a key and writes its public half into the source", made == 0 && File.Exists(key) && trust.Length == 88, tool.Output);

            string firstKey = File.ReadAllText(key);
            int again = tool.Run("keygen", "--key", key, "--feed-source", source);
            report.Check("It will never replace a key that exists", again == 1 && File.ReadAllText(key) == firstKey, tool.Output);

            string otherKey = Path.Combine(folder.Path, "other.pem");
            string otherSource = Path.Combine(folder.Path, "Other.cs");
            File.WriteAllText(otherSource, "public const string PublicKey = \"\";");
            tool.Run("keygen", "--key", otherKey, "--feed-source", otherSource);

            string launcher = Path.Combine(folder.Path, "launcher.exe");
            File.WriteAllBytes(launcher, new byte[] { 1, 2, 3, 4, 5 });

            report.Section("Into a folder, and out through the launcher's code");
            ThroughAFolder(report, tool, folder.Path, key, otherKey, trust, launcher);

            report.Section("Into a stand-in for GitHub");
            ThroughGitHub(report, tool, folder.Path, key, trust, launcher);

            return report.Finish();
        }

        private static string[] Common(string key, string trust, string game, string launcher, params string[] more)
        {
            var arguments = new List<string> { "publish", "--key", key, "--trust", trust, "--game", game, "--launcher", launcher, "--executable", "Dino Defenders.exe" };
            arguments.AddRange(more);
            return arguments.ToArray();
        }

        private static void ThroughAFolder(Report report, ReleaseTool tool, string work, string key, string otherKey, string trust, string launcher)
        {
            string feed = Path.Combine(work, "feed");
            string game = Path.Combine(work, "build");
            var paths = new InstallPaths(Path.Combine(work, "player"));
            Directory.CreateDirectory(paths.Root);

            using var server = new FaultyServer();
            server.Content = path =>
            {
                string file = Path.Combine(feed, path.Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(file) ? File.ReadAllBytes(file) : null;
            };

            UpdateResult Update() => new Updater(paths, server.Url, trust, new Downloader { FirstWaitMilliseconds = 5 }).Run(_ => { }, CancellationToken.None);
            int RuntimeFolders() => Directory.Exists(Path.Combine(feed, "download")) ? Directory.GetDirectories(Path.Combine(feed, "download"), "runtime-*").Length : 0;

            ReleaseTool.WriteBuild(game, TestFeed.Build(1));
            int code = tool.Run(Common(key, otherKeyTrust(work), game, launcher, "--folder", feed, "--notes", "x"));
            report.Check("A key launchers don't trust is stopped before anything goes out", code == 1 && !Directory.Exists(feed) && tool.Output.Contains("isn't the one launchers trust"), tool.Output);

            code = tool.Run(Common(key, trust, game, launcher, "--folder", feed, "--notes", "x", "--dry-run"));
            report.Check("A dry run sends nothing", code == 0 && !Directory.Exists(feed), tool.Output);

            code = tool.Run(Common(key, trust, game, launcher, "--folder", feed, "--notes", "The first one"));
            report.Check("The first release goes out", code == 0, tool.Output);
            report.Check("It's 0.1.0", tool.Output.Contains("0.1.0"));

            UpdateResult result = Update();
            Manifest? first = result.Build;
            report.Check("The launcher's code reads what the tool wrote, on its own framework", first?.Version == "0.1.0" && result.Problem == null, result.Problem ?? "");
            report.Check("The game unpacks whole", first != null && GameInstall.FindWrong(paths.Game, first, CancellationToken.None).Count == 0);
            report.Check("Notes come through", first?.Notes.FirstOrDefault()?.Text == "The first one");

            ReleaseTool.WriteBuild(game, TestFeed.Build(2));
            server.ClearRequests();
            code = tool.Run(Common(key, trust, game, launcher, "--folder", feed, "--notes", "The second one"));
            report.Check("The next release counts on by itself", code == 0 && tool.Output.Contains("0.1.1"), tool.Output);
            report.Check("With the same runtime, no new runtime goes up", RuntimeFolders() == 1, $"{RuntimeFolders()}");

            server.ClearRequests();
            result = Update();
            Manifest? second = result.Build;
            report.Check("Players get it", second?.Version == "0.1.1" && second.Sequence == 2, result.Problem ?? "");
            report.Check("Fetching only the game pack", server.CountOf("game.zip") == 1 && server.CountOf("runtime.zip") == 0);
            report.Check("With the old notes kept for anyone who was away", second?.Notes.Length == 2 && second.Notes[0].Text == "The second one", $"{second?.Notes.Length}");

            Dictionary<string, byte[]> newRuntime = TestFeed.Build(3);
            newRuntime["System.Private.CoreLib.dll"] = new byte[] { 9, 9, 9 };
            ReleaseTool.WriteBuild(game, newRuntime);
            code = tool.Run(Common(key, trust, game, launcher, "--folder", feed, "--notes", "New runtime"));
            report.Check("When the runtime changes, it goes up by itself", code == 0 && RuntimeFolders() == 2, tool.Output);
            server.ClearRequests();
            result = Update();
            report.Check("Players get both", result.Build?.Version == "0.1.2" && server.CountOf("runtime.zip") == 1 && server.CountOf("game.zip") == 1, result.Problem ?? "");

            code = tool.Run("rollback", "--to", "0.1.0", "--key", key, "--trust", trust, "--folder", feed);
            report.Check("A rollback goes out", code == 0, tool.Output);
            result = Update();
            report.Check("Everyone follows it", result.Build?.Version == "0.1.3" && result.Build.Sequence == 4, result.Problem ?? result.Build?.Version ?? "");
            report.Check("And they really have the old build", first != null && GameInstall.FindWrong(paths.Game, first, CancellationToken.None).Count == 0);

            code = tool.Run("rollback", "--to", "0.0.9", "--key", key, "--trust", trust, "--folder", feed);
            report.Check("Going back to something that doesn't exist is refused", code == 1, tool.Output);

            code = tool.Run(Common(key, trust, game, launcher, "--folder", feed, "--notes", "x", "--version", "0.1.1"));
            report.Check("Reusing a version is refused", code == 1 && tool.Output.Contains("already exists"), tool.Output);

            ReleaseTool.WriteBuild(game, TestFeed.Build(5));
            code = tool.Run(Common(key, trust, game, launcher, "--folder", feed, "--notes", "Forward again"));
            result = Update();
            report.Check("After a rollback, releases carry on from the top", code == 0 && result.Build?.Version == "0.1.4" && result.Build.Sequence == 5, result.Problem ?? tool.Output);

            code = tool.Run("status", "--trust", trust, "--folder", feed);
            report.Check("status says what's live", code == 0 && tool.Output.Contains("0.1.4"), tool.Output);
        }

        private static string otherKeyTrust(string work) => Regex.Match(File.ReadAllText(Path.Combine(work, "Other.cs")), "PublicKey = \"([^\"]*)\"").Groups[1].Value;

        private static void ThroughGitHub(Report report, ReleaseTool tool, string work, string key, string trust, string launcher)
        {
            using var github = new FakeGitHub();
            string game = Path.Combine(work, "gh-build");
            var paths = new InstallPaths(Path.Combine(work, "gh-player"));
            Directory.CreateDirectory(paths.Root);

            string[] Publish(string notes, params string[] more)
            {
                var arguments = new List<string>(Common(key, trust, game, launcher, "--api", github.ApiUrl, "--feed", github.FeedUrl.ToString(), "--notes", notes));
                arguments.AddRange(more);
                return arguments.ToArray();
            }

            UpdateResult Update() => new Updater(paths, github.FeedUrl, trust, new Downloader { FirstWaitMilliseconds = 5 }).Run(_ => { }, CancellationToken.None);

            ReleaseTool.WriteBuild(game, TestFeed.Build(1));

            tool.GitHubToken = "wrong-token";
            int code = tool.Run(Publish("Bad token"));
            report.Check("A bad token stops the release, and says why", code == 1 && tool.Output.Contains("401") && github.Latest == null, tool.Output);

            tool.GitHubToken = FakeGitHub.Token;
            code = tool.Run(Publish("First"));
            report.Check("The first release goes up", code == 0, tool.Output);

            List<string> calls = github.Calls;
            int created = calls.IndexOf("create v0.1.0");
            int published = calls.IndexOf("publish v0.1.0 latest=true");
            report.Check("Made as a draft, filled, and only then published", created >= 0 && published > created && calls.Skip(created).Take(published - created).Count(c => c.StartsWith("upload")) == 3, string.Join(", ", calls));
            report.Check("The runtime went up first, under its own tag", calls.Any(c => c.StartsWith("publish runtime-") && c.EndsWith("latest=false")) && calls.FindIndex(c => c.StartsWith("publish runtime-")) < created, string.Join(", ", calls));
            report.Check("The runtime is a prerelease, so it's never mistaken for the game", github.Releases.First(r => r.Tag.StartsWith("runtime-")).Prerelease);
            report.Check("Latest is the game", github.Latest?.Tag == "v0.1.0");
            report.Check("A new tester's link works: the launcher is on the latest release", github.Latest?.Assets.ContainsKey(Feed.LauncherName) == true);
            report.Check("GitHub had nothing to complain about", github.Complaints.Count == 0, string.Join("; ", github.Complaints));
            report.Check("The token never went anywhere but the API", !github.TokenSeenOnFeed);

            UpdateResult result = Update();
            report.Check("A launcher installs from it, through every redirect", result.Build?.Version == "0.1.0", result.Problem ?? "");

            ReleaseTool.WriteBuild(game, TestFeed.Build(2));
            github.Calls.Clear();
            github.RuntimeFetches = 0;
            code = tool.Run(Publish("Second"));
            report.Check("A release with the same runtime uploads nothing big", code == 0 && !github.Calls.Any(c => c.Contains(Feed.RuntimePackName)), string.Join(", ", github.Calls));
            report.Check("Or downloads it, just to see that it's there", github.RuntimeFetches == 0, $"{github.RuntimeFetches}");
            result = Update();
            report.Check("Launchers follow", result.Build?.Version == "0.1.1", result.Problem ?? "");

            ReleaseTool.WriteBuild(game, TestFeed.Build(3));
            github.FailUpload = Feed.GamePackName;
            github.Calls.Clear();
            code = tool.Run(Publish("Third"));
            report.Check("A failed upload stops the release", code == 1 && tool.Output.Contains("nothing was released"), tool.Output);
            report.Check("The half-made draft is removed", github.Calls.Contains("delete") && github.Releases.All(r => r.Tag != "v0.1.2"), string.Join(", ", github.Calls));
            report.Check("Nothing changed for anyone", github.Latest?.Tag == "v0.1.1");

            github.FailUpload = null;
            code = tool.Run(Publish("Third again"));
            report.Check("Running it again just works", code == 0 && github.Latest?.Tag == "v0.1.2", tool.Output);

            code = tool.Run("rollback", "--to", "0.1.0", "--key", key, "--trust", trust, "--api", github.ApiUrl, "--feed", github.FeedUrl.ToString());
            report.Check("A rollback through GitHub works", code == 0 && github.Latest?.Tag == "v0.1.3", tool.Output);
            result = Update();
            report.Check("Launchers follow that too", result.Build?.Version == "0.1.3", result.Problem ?? "");
            report.Check("GitHub still had nothing to complain about", github.Complaints.Count == 0, string.Join("; ", github.Complaints));
        }
    }
}
