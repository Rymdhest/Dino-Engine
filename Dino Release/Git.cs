using System.Diagnostics;

namespace Dino_Release
{
    /// <summary>What the repository says about what's being released.</summary>
    internal static class Git
    {
        public static string Head() => Run("rev-parse HEAD").Trim();

        /// <summary>Changes to tracked files. Stray untracked files don't make it into a build.</summary>
        public static bool HasUncommittedChanges() => Run("status --porcelain --untracked-files=no").Trim().Length > 0;

        public static bool IsPushed(string commit) => TryRun($"branch -r --contains {commit}", out string branches) && branches.Trim().Length > 0;

        /// <summary>The subjects of the commits since the last release, which read well enough as notes. Newest first.</summary>
        public static List<string> SubjectsSince(string? commit)
        {
            string? known = commit?.Split('+')[0];
            string range = known != null && TryRun($"cat-file -e {known}^{{commit}}", out _) ? $"{known}..HEAD" : "-n 10 HEAD";
            return Run($"log --no-merges --pretty=%s {range}").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        /// <summary>
        /// The token git already uses to push here, so that releasing needs no setup of its own. Asked of git's credential helper, the
        /// same way git asks for it. Never printed.
        /// </summary>
        public static string? StoredGitHubToken()
        {
            var start = new ProcessStartInfo("git", "credential fill")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using Process? process = Process.Start(start);
            if (process == null) return null;

            process.StandardInput.Write("protocol=https\nhost=github.com\n\n");
            process.StandardInput.Close();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) return null;

            foreach (string line in output.Split('\n'))
            {
                if (line.StartsWith("password=")) return line.Substring("password=".Length).Trim();
            }
            return null;
        }

        private static string Run(string arguments)
        {
            if (!TryRun(arguments, out string output)) throw new ReleaseException($"git {arguments} failed: {output}");
            return output;
        }

        private static bool TryRun(string arguments, out string output)
        {
            var start = new ProcessStartInfo("git", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using Process process = Process.Start(start) ?? throw new ReleaseException("git wouldn't start.");
            Task<string> errors = process.StandardError.ReadToEndAsync();
            output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode == 0) return true;

            output = errors.Result;
            return false;
        }
    }
}
