using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Dino_Launcher.Update
{
    /// <summary>
    /// Being the one launcher running. Two at once could each swap the game out from under the other. A held file rather than a mutex, as
    /// a mutex must be let go by the thread that took it, and this is let go by the worker thread during a self-update. Windows
    /// releases it if the launcher dies, so a crash can't leave it stuck.
    /// </summary>
    public sealed class InstanceLock : IDisposable
    {
        private readonly InstallPaths _paths;
        private FileStream? _file;

        public InstanceLock(InstallPaths paths)
        {
            _paths = paths;
        }

        public bool Take()
        {
            try
            {
                Directory.CreateDirectory(_paths.Root);
                _file = new FileStream(_paths.InstanceLock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>For a launcher being handed over to, which may start a moment before the old one has let go.</summary>
        public bool TakeWithin(int milliseconds)
        {
            int waited = 0;
            while (!Take())
            {
                if (waited >= milliseconds) return false;
                Thread.Sleep(100);
                waited += 100;
            }
            return true;
        }

        public void Release()
        {
            _file?.Dispose();
            _file = null;
        }

        public void Dispose() => Release();

        /// <summary>Tells the launcher that's running to come to the front. Each install has its own, by where it is.</summary>
        public static string ShowEventName(InstallPaths paths)
        {
            string root = Hashing.OfBytes(Encoding.UTF8.GetBytes(paths.Root.ToLowerInvariant()));
            return $"Local\\DinoDefendersLauncherShow-{root.Substring(0, 16)}";
        }

        public static void ShowOther(InstallPaths paths)
        {
            if (EventWaitHandle.TryOpenExisting(ShowEventName(paths), out EventWaitHandle? show))
            {
                using (show) show.Set();
            }
        }
    }
}
