using System.Collections.Concurrent;
using System.Text;

namespace Dino_Engine.GUI.Debug
{
    /// <summary>
    /// Tees everything written to <see cref="Console.Out"/> into a queue the debug console can display,
    /// while still writing to the original console. Thread safe (Console wraps writers in a synchronized writer).
    /// </summary>
    public sealed class ConsoleCapture : TextWriter
    {
        private static ConsoleCapture? _instance;
        private readonly TextWriter _original;
        private readonly ConcurrentQueue<string> _lines = new();
        private readonly StringBuilder _current = new();
        private readonly object _lock = new();

        private ConsoleCapture(TextWriter original)
        {
            _original = original;
        }

        public override Encoding Encoding => _original.Encoding;

        /// <summary>Installs the capture once; later calls return the existing instance.</summary>
        public static ConsoleCapture Install()
        {
            if (_instance != null) return _instance;
            _instance = new ConsoleCapture(Console.Out);
            Console.SetOut(_instance);
            return _instance;
        }

        public bool TryDequeueLine(out string line) => _lines.TryDequeue(out line!);

        public override void Write(char value)
        {
            _original.Write(value);
            lock (_lock)
            {
                if (value == '\n')
                {
                    FlushLine();
                }
                else if (value != '\r')
                {
                    _current.Append(value);
                }
            }
        }

        public override void Write(string? value)
        {
            if (value == null) return;
            _original.Write(value);
            lock (_lock)
            {
                foreach (char c in value)
                {
                    if (c == '\n') FlushLine();
                    else if (c != '\r') _current.Append(c);
                }
            }
        }

        public override void WriteLine(string? value)
        {
            Write(value);
            Write('\n');
        }

        public override void Flush() => _original.Flush();

        private void FlushLine()
        {
            _lines.Enqueue(_current.ToString());
            _current.Clear();
            // Never grow without bound if nobody reads the queue.
            while (_lines.Count > 2000 && _lines.TryDequeue(out _)) { }
        }
    }
}
