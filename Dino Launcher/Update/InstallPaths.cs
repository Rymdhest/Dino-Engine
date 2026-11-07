using System;
using System.IO;

namespace Dino_Launcher.Update
{
    /// <summary>
    /// Where everything goes. All of it is in the player's own folders, so nothing ever needs administrator rights. The game keeps one
    /// path for good, as Windows Firewall remembers a program by its path: a new folder per version would ask again after every update.
    /// </summary>
    public sealed class InstallPaths
    {
        public readonly string Root;
        public readonly string Game;
        public readonly string Staging;
        public readonly string Retired;
        public readonly string StagingReady;
        public readonly string Downloads;
        public readonly string Logs;
        public readonly string LauncherExe;
        public readonly string InstanceLock;
        public readonly string RefusedLaunchers;
        public readonly string LauncherDied;
        public readonly string WindowSize;

        /// <summary>Where the game keeps its save. Updates leave it alone.</summary>
        public string SaveFolder;

        public InstallPaths(string? root = null)
        {
            Root = Path.GetFullPath(root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DinoDefenders"));
            Game = Path.Combine(Root, "game");
            Staging = Path.Combine(Root, "game.new");
            Retired = Path.Combine(Root, "game.old");
            StagingReady = Path.Combine(Root, "game.new.ready");
            Downloads = Path.Combine(Root, "downloads");
            Logs = Path.Combine(Root, "logs");
            LauncherExe = Path.Combine(Root, Feed.LauncherName);
            InstanceLock = Path.Combine(Root, "launcher.lock");
            RefusedLaunchers = Path.Combine(Root, "launcher.refused");
            LauncherDied = Path.Combine(Root, "launcher.died");
            WindowSize = Path.Combine(Root, "launcher.window");
            SaveFolder = root != null ? Path.Combine(Root, "saves") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DinoDefenders");
        }

        /// <summary>Inside the game's folder, so that it changes at the same instant as the files it describes.</summary>
        public string InstalledManifest => Path.Combine(Game, Feed.ManifestName);

        public string GameFile(string relative) => Path.Combine(Game, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
