using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Dino_Launcher.Update;

namespace Dino_LaunchLab
{
    /// <summary>Runs the real Dino Release, as a program, the way releases are really made.</summary>
    internal sealed class ReleaseTool
    {
        private readonly string _dll;
        private readonly string _workingFolder;

        public string Output = "";

        /// <summary>Set for the run, as Dino Release reads the token from here.</summary>
        public string? GitHubToken;

        public ReleaseTool(string workingFolder)
        {
            string lab = AppDomain.CurrentDomain.BaseDirectory;
            string configuration = new DirectoryInfo(lab).Parent!.Name;
            _dll = Path.GetFullPath(Path.Combine(lab, "..", "..", "..", "..", "Dino Release", "bin", configuration, "net8.0", "Dino Release.dll"));
            _workingFolder = workingFolder;
        }

        public int Run(params string[] arguments)
        {
            var start = new ProcessStartInfo("dotnet")
            {
                Arguments = $"\"{_dll}\" " + string.Join(" ", Array.ConvertAll(arguments, Quote)),
                WorkingDirectory = _workingFolder,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            start.EnvironmentVariables.Remove("GITHUB_TOKEN");
            start.EnvironmentVariables.Remove("GH_TOKEN");
            if (GitHubToken != null) start.EnvironmentVariables["GITHUB_TOKEN"] = GitHubToken;

            using Process process = Process.Start(start)!;
            var errors = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Output = output + errors.Result;
            return process.ExitCode;
        }

        private static string Quote(string argument) => argument.Length == 0 || argument.Contains(" ") ? $"\"{argument}\"" : argument;

        public static void WriteBuild(string folder, Dictionary<string, byte[]> build)
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            foreach (KeyValuePair<string, byte[]> file in build)
            {
                string path = Path.Combine(folder, file.Key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, file.Value);
            }
        }
    }
}
