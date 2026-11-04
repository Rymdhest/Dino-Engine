using System;
using System.IO;
using System.Threading;

namespace Dino_Launcher.Update
{
    /// <summary>File operations that allow for Windows: something scanning or indexing a file can make it briefly impossible to touch.</summary>
    public static class FileSystem
    {
        private const int Tries = 6;
        private const int WaitMilliseconds = 250;

        public static void Delete(string path) => Retrying(() =>
        {
            if (!File.Exists(path)) return;
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        });

        public static void DeleteFolder(string path) => Retrying(() =>
        {
            if (!Directory.Exists(path)) return;
            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(path, true);
        });

        public static void Move(string from, string to) => Retrying(() => File.Move(from, to));

        public static void TryDelete(string path) => Trying(() => Delete(path));

        public static void TryDeleteFolder(string path) => Trying(() => DeleteFolder(path));

        /// <summary>Written aside and swapped in, so that it's all the old file or all the new one.</summary>
        public static void WriteAtomically(string path, byte[] bytes)
        {
            string temporary = path + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            Retrying(() =>
            {
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            });
        }

        private static void Retrying(Action action)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception e) when ((e is IOException || e is UnauthorizedAccessException) && attempt < Tries)
                {
                    Thread.Sleep(WaitMilliseconds);
                }
            }
        }

        private static void Trying(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log.Write($"Left behind: {e.Message}");
            }
        }
    }
}
