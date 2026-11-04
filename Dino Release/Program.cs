namespace Dino_Release
{
    /// <summary>
    /// Sends a build to everyone with a launcher. Run from the root of the repository, or use release.cmd there. See LAUNCHER.md.
    ///   publish  [--notes text] [--version x.y.z] [--dry-run] [--allow-dirty] [--no-smoke]   Builds, checks, signs and releases what's committed.
    ///   rollback --to x.y.z [--notes text]   Sends everyone back to an earlier build, as a new release.
    ///   status                               What's out there now.
    ///   keygen                               Makes the signing key, once. Never needed again unless it's lost.
    /// For testing without touching GitHub or the real key:
    ///   --key path        Signs with this key instead.
    ///   --trust base64    Expects launchers to believe this public key instead of the one in Feed.cs.
    ///   --folder path     Releases into a folder laid out like the feed, instead of to GitHub.
    ///   --api url --feed url    Talks to something pretending to be GitHub.
    ///   --game path --launcher path    Uses these instead of building them.
    /// Exit code 1 on any failure. Nothing goes live until every step before it has worked.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string command = args.Length > 0 ? args[0] : "";
            try
            {
                var options = new Options(args);
                switch (command)
                {
                    case "publish":
                        new Publisher(options).Publish();
                        return 0;
                    case "rollback":
                        new Publisher(options).Rollback();
                        return 0;
                    case "status":
                        new Publisher(options).Status();
                        return 0;
                    case "keygen":
                        SigningKey.Generate(options.Text("key") ?? SigningKey.DefaultPath, options.Text("feed-source"));
                        return 0;
                    default:
                        Console.Error.WriteLine("Commands: publish, rollback, status, keygen. See LAUNCHER.md.");
                        return 2;
                }
            }
            catch (ReleaseException e)
            {
                Console.Error.WriteLine($"\nStopped: {e.Message}");
                return 1;
            }
            catch (Exception e) when (e is HttpRequestException or AggregateException or IOException or TaskCanceledException)
            {
                Console.Error.WriteLine($"\nStopped: {(e as AggregateException)?.InnerException?.Message ?? e.Message}");
                return 1;
            }
        }
    }

    /// <summary>A reason not to go on, in words. Thrown before anything has gone live, unless it says otherwise.</summary>
    internal sealed class ReleaseException : Exception
    {
        public ReleaseException(string message) : base(message) { }
    }

    internal sealed class Options
    {
        private static readonly HashSet<string> Flags = new() { "dry-run", "allow-dirty", "no-smoke" };

        private readonly Dictionary<string, string> _values = new();
        private readonly HashSet<string> _flags = new();

        public Options(string[] args)
        {
            for (int i = 1; i < args.Length; i++)
            {
                string name = args[i].TrimStart('-');
                if (Flags.Contains(name)) _flags.Add(name);
                else if (i + 1 < args.Length) _values[name] = args[++i];
                else throw new ReleaseException($"--{name} needs a value.");
            }
        }

        public string? Text(string name) => _values.GetValueOrDefault(name);
        public bool Flag(string name) => _flags.Contains(name);
    }
}
