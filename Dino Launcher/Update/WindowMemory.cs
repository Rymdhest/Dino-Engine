using System;
using System.Globalization;
using System.IO;

namespace Dino_Launcher.Update
{
    /// <summary>
    /// The size someone left the window at, in units that don't change with the screen's scaling. Where it stood isn't kept, as a monitor
    /// that has since gone would leave the window out of reach. It's only a convenience, so anything wrong with the file means starting over.
    /// </summary>
    public sealed class WindowMemory
    {
        private const int Largest = 100000;

        public int Width;
        public int Height;
        public bool Maximized;

        public static WindowMemory? Read(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string[] parts = File.ReadAllText(path).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3) return null;
                if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int width)) return null;
                if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int height)) return null;
                if (width <= 0 || height <= 0 || width > Largest || height > Largest) return null;
                return new WindowMemory { Width = width, Height = height, Maximized = parts[2] == "1" };
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }

        public void Write(string path)
        {
            try
            {
                File.WriteAllText(path, string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", Width, Height, Maximized ? 1 : 0));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log.Write($"Couldn't remember the window's size: {e.Message}");
            }
        }
    }
}
