using System.Reflection;

namespace Dino_Defenders
{
    /// <summary>Which build this is. Dino Release stamps the version; anything built by hand keeps the SDK's 1.0.0.</summary>
    public static class BuildInfo
    {
        private const string Unstamped = "1.0.0";

        public const string Name = "Dino Defenders";

        public static readonly string Version = Read();

        /// <summary>What the window is called, so that a screenshot in a bug report says what it came from.</summary>
        public static string Title => Version == Unstamped ? Name : $"{Name} {Version}";

        private static string Read()
        {
            string? version = typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (version == null) return Unstamped;

            // The SDK puts the commit after a plus. Two builds of one version are the same build.
            int plus = version.IndexOf('+');
            return plus < 0 ? version : version.Substring(0, plus);
        }
    }
}
