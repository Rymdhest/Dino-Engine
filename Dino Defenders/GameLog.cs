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
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Could not open the log: {e.Message}");
            }
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
