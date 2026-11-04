namespace Dino_Launcher.Update
{
    /// <summary>
    /// Where releases live and who is believed about them. Everything else a launcher knows comes from the feed. It is a folder of files
    /// over HTTPS: GitHub Releases happens to serve one, and any static host laid out the same way would do. See LAUNCHER.md.
    /// </summary>
    public static class Feed
    {
        public const string Owner = "Rymdhest";
        public const string Repository = "Dino-Engine";
        public const string Url = "https://github.com/" + Owner + "/" + Repository + "/releases/";

        public const string ManifestName = "manifest.signed";
        public const string LauncherName = "DinoDefendersLauncher.exe";
        public const string GamePackName = "game.zip";
        public const string RuntimePackName = "runtime.zip";

        /// <summary>Relative to <see cref="Url"/>. The only address a launcher has to know, and it never changes.</summary>
        public const string ManifestPath = "latest/download/" + ManifestName;

        /// <summary>What to send a new tester. It always gives the newest launcher.</summary>
        public const string LauncherLink = Url + "latest/download/" + LauncherName;

        /// <summary>A manifest is a few tens of KB. Anything much bigger isn't one.</summary>
        public const int MostManifestBytes = 4 * 1024 * 1024;

        /// <summary>The public half of the release signing key, as the X and Y of a P-256 point in base64. Written by Dino Release keygen.</summary>
        public const string PublicKey = "7BP5cktFHUXXIuEkPSAiLW0K8N3AdnWnbcoTl9XU+tHR5JUXi8/tZ69gkDPV1mcFZZ+U//qiWsFtnbHycMstVw==";
    }
}
