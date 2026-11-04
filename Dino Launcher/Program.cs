using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Dino_Launcher.Ui;
using Dino_Launcher.Update;

namespace Dino_Launcher
{
    /// <summary>
    /// How the launcher was started. Playing needs none of it. These exist so that it can be tested without touching a real install: nothing
    /// here loosens what's believed, as a release from any feed still has to be signed with the key built into this exe.
    ///   --root path        Installs under this folder, and leaves shortcuts and the registry alone.
    ///   --feed url         Reads releases from here instead.
    ///   --quit-when-ready  Closes as soon as the game is up to date. Exit code 0 if it's playable.
    ///   --play             Presses Play as soon as it can.
    ///   --uninstall        What Windows runs from Installed Apps.
    ///   --handoff name     Proves this launcher works to the one replacing itself with it.
    /// </summary>
    internal sealed class LaunchOptions
    {
        public string? Root;
        public string? Feed;
        public bool QuitWhenReady;
        public bool Play;
        public bool Uninstall;
        public string? Handoff;

        public static LaunchOptions Parse(string[] args)
        {
            var options = new LaunchOptions();
            for (int i = 0; i < args.Length; i++)
            {
                string? value = i + 1 < args.Length ? args[i + 1] : null;
                switch (args[i].TrimStart('-'))
                {
                    case "root": options.Root = value; i++; break;
                    case "feed": options.Feed = value; i++; break;
                    case "handoff": options.Handoff = value; i++; break;
                    case "quit-when-ready": options.QuitWhenReady = true; break;
                    case "play": options.Play = true; break;
                    case "uninstall": options.Uninstall = true; break;
                }
            }
            return options;
        }

        /// <summary>The same again, for the launcher this one is about to start.</summary>
        public string Forwarded()
        {
            string arguments = "";
            if (Root != null) arguments += $" --root \"{Root}\"";
            if (Feed != null) arguments += $" --feed \"{Feed}\"";
            if (QuitWhenReady) arguments += " --quit-when-ready";
            if (Play) arguments += " --play";
            return arguments.Trim();
        }
    }

    internal static class Program
    {
        private const string Title = "Dino Defenders";

        private static bool _takingOver;
        private static InstallPaths? _paths;

        public static string LauncherVersion => Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        [STAThread]
        private static int Main(string[] args)
        {
            LaunchOptions options = LaunchOptions.Parse(args);
            _takingOver = options.Handoff != null;
            var paths = new InstallPaths(options.Root);
            _paths = paths;
            Log.Start(paths.Logs);
            Log.Write($"Launcher {LauncherVersion} started from {Application.ExecutablePath} {string.Join(" ", args)}");

            AppDomain.CurrentDomain.UnhandledException += (_, e) => Died(e.ExceptionObject);
            Application.ThreadException += (_, e) => Died(e.Exception);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                if (options.Uninstall) return Uninstall(paths);
                if (!IsInstalledCopy(paths)) return InstallAndStart(paths, options);
                return Run(paths, options);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                Died(e);
                return 1;
            }
        }

        private static bool IsInstalledCopy(InstallPaths paths)
        {
            return string.Equals(Path.GetFullPath(Application.ExecutablePath), paths.LauncherExe, StringComparison.OrdinalIgnoreCase);
        }

        private static int InstallAndStart(InstallPaths paths, LaunchOptions options)
        {
            using (var check = new InstanceLock(paths))
            {
                if (!check.Take())
                {
                    InstanceLock.ShowOther(paths);
                    return 0;
                }
            }

            var installer = new Installer(paths);
            if (installer.HasSoundLauncher(new GameInstall(paths).ReadInstalled(Feed.PublicKey))) Log.Write("Leaving the installed launcher as it is.");
            else installer.CopyLauncher(Application.ExecutablePath);
            if (options.Root == null) installer.AddToWindows(LauncherVersion);

            Log.Write($"Installed to {paths.Root}.");
            Process.Start(new ProcessStartInfo(paths.LauncherExe, options.Forwarded()) { UseShellExecute = false, WorkingDirectory = paths.Root })?.Dispose();
            return 0;
        }

        private static int Run(InstallPaths paths, LaunchOptions options)
        {
            using var instance = new InstanceLock(paths);
            bool ours = options.Handoff != null ? instance.TakeWithin(10000) : instance.Take();
            if (!ours)
            {
                // Started, and works, even if another launcher got in first. That's all a handoff asks.
                if (options.Handoff != null) Prove(options.Handoff);
                InstanceLock.ShowOther(paths);
                return 0;
            }

            var window = new LauncherWindow(paths, options, instance);
            Application.Run(window);
            return window.ExitCode;
        }

        /// <summary>Tells the launcher that started this one that it needn't put itself back.</summary>
        public static void Prove(string handoff)
        {
            _takingOver = false;
            if (EventWaitHandle.TryOpenExisting(handoff, out EventWaitHandle? proved))
            {
                using (proved) proved.Set();
            }
        }

        private static int Uninstall(InstallPaths paths)
        {
            using var instance = new InstanceLock(paths);
            if (!instance.Take())
            {
                MessageBox.Show("Close the game and the launcher first, then try again.", Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 1;
            }

            var ask = "Remove Dino Defenders from this computer?\n\nYour hero is kept, in case you come back.";
            if (MessageBox.Show(ask, Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 0;

            var installer = new Installer(paths);
            installer.RemoveFromWindows();
            instance.Release();
            installer.RemoveFilesAfterExit();
            return 0;
        }

        private static void Died(object problem)
        {
            Log.Write($"The launcher stopped: {problem}");

            // The launcher this is replacing is waiting to find out, and puts itself back the moment this goes. The player need never know.
            if (_takingOver) Environment.Exit(1);

            if (_paths != null) Installer.RememberDeath(_paths);
            string message = $"Something went wrong in the launcher.\n\n{(problem as Exception)?.Message}\n\nIf it keeps happening, get a fresh copy:\n{Feed.LauncherLink}";
            MessageBox.Show(message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        }
    }
}
