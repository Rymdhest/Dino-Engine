using System.Text;

namespace Dino_Defenders
{
    /// <summary>
    /// Keeps what the game prints in a file, and why it died if it did, so that someone testing it has something to send. The launcher
    /// points them here, but this doesn't depend on it: a build from anywhere leaves the same log. Nothing here may stop the game starting.
    /// </summary>
    internal static class GameLog
    {
        private const long MostBytes = 16L * 1024 * 1024;
        private const long MostNetworkBytes = 1024 * 1024;     // Trimmed to half when it's grown past this, oldest first: months of evenings with friends.

        private static readonly object NetworkLock = new();
        private static string? _networkLog;
        private static bool _networkBegun;

        public static void Start()
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DinoDefenders", "logs");
                Directory.CreateDirectory(folder);

                var tee = new Tee(Console.Out, Open(folder));
                Console.SetOut(tee);
                Console.SetError(tee);
                AppDomain.CurrentDomain.UnhandledException += (_, e) => Console.Error.WriteLine($"The game stopped: {e.ExceptionObject}");

                Console.WriteLine($"{BuildInfo.Title}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}, {Environment.OSVersion}, .NET {Environment.Version}");

                _networkLog = Path.Combine(folder, "network.log");
                Trim(_networkLog);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Could not open the log: {e.Message}");
            }
        }

        /// <summary>
        /// A line about hosting or joining. It goes in game.log like anything else, and in network.log, which runs on from one start to the next: a friend who couldn't
        /// get in has usually started the game twice more before anyone asks, and game.log only keeps two runs. Times are UTC, to line up with the other machine's.
        /// Only the game keeps network.log, as only it calls <see cref="Start"/>: the labs' servers and clients print these lines and nothing more.
        /// </summary>
        public static void Network(string text)
        {
            string line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z {text}";
            Console.WriteLine(line);
            if (_networkLog == null) return;

            try
            {
                lock (NetworkLock)
                {
                    if (!_networkBegun)
                    {
                        _networkBegun = true;
                        File.AppendAllText(_networkLog, $"{Environment.NewLine}{BuildInfo.Title}, process {Environment.ProcessId}, {Environment.OSVersion}{Environment.NewLine}");
                    }
                    File.AppendAllText(_networkLog, line + Environment.NewLine);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Another copy of the game writing to it at the same moment: the line is still in game.log.
            }
        }

        private static void Trim(string path)
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length <= MostNetworkBytes) return;

            byte[] all = File.ReadAllBytes(path);
            int from = Array.IndexOf(all, (byte)'\n', all.Length - (int)(MostNetworkBytes / 2)) + 1;
            File.WriteAllBytes(path, all[from..]);
        }

        /// <summary>The last run's log is kept beside this one. A second game on this machine gets a log of its own.</summary>
        private static StreamWriter Open(string folder)
        {
            string path = Path.Combine(folder, "game.log");
            try
            {
                if (File.Exists(path)) File.Move(path, Path.Combine(folder, "game.previous.log"), overwrite: true);
                return Writer(path);
            }
            catch (IOException)
            {
                return Writer(Path.Combine(folder, $"game-{Environment.ProcessId}.log"));
            }
        }

        private static StreamWriter Writer(string path)
        {
            var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            return new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        }

        /// <summary>Writes to the console as before, and to the file until it has had enough.</summary>
        private sealed class Tee : TextWriter
        {
            private readonly TextWriter _console;
            private readonly StreamWriter _file;
            private long _written;

            public Tee(TextWriter console, StreamWriter file)
            {
                _console = console;
                _file = file;
            }

            public override Encoding Encoding => _console.Encoding;

            public override void Write(char value) => Write(value.ToString());

            public override void Write(string? value)
            {
                if (value == null) return;
                lock (_file)
                {
                    _console.Write(value);
                    if (_written > MostBytes) return;

                    try
                    {
                        _written += value.Length;
                        _file.Write(value);
                        if (_written > MostBytes) _file.WriteLine("The log is full. Nothing after this was kept.");
                    }
                    catch (Exception e) when (e is IOException or ObjectDisposedException)
                    {
                        _written = MostBytes + 1;
                    }
                }
            }

            public override void WriteLine(string? value) => Write(value + Environment.NewLine);
        }
    }
}
