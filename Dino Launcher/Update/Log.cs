using System;
using System.IO;

namespace Dino_Launcher.Update
{
    /// <summary>What the launcher did, for when someone says it didn't work. Failing to write here must never be what goes wrong.</summary>
    public static class Log
    {
        private const long MostBytes = 1024 * 1024;

        private static readonly object Lock = new object();
        private static string? _path;

        public static void Start(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                _path = Path.Combine(folder, "launcher.log");

                var file = new FileInfo(_path);
                if (file.Exists && file.Length > MostBytes) File.Copy(_path, _path + ".old", true);
                if (file.Exists && file.Length > MostBytes) File.Delete(_path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _path = null;
            }
        }

        public static void Write(string text)
        {
            if (_path == null) return;
            lock (Lock)
            {
                try
                {
                    File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {text}{Environment.NewLine}");
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
