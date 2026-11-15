namespace Dino_Engine.Util
{
    /// <summary>Writes Windows icon files (.ico) from pictures, for an application's icon: an exe's, a window's, a shortcut's.</summary>
    public static class IcoWriter
    {
        /// <summary>
        /// Each picture is square, rows from the top, four bytes a pixel with straight alpha. Those 64 pixels and under go in as plain bitmaps, which
        /// everything that reads an icon has always read, and the larger as PNG, which Windows has read since Vista and keeps a 256 pixel icon small.
        /// </summary>
        public static void Write(string path, IReadOnlyList<(int size, byte[] rgba)> pictures)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var entries = new List<byte[]>(pictures.Count);
            foreach ((int size, byte[] rgba) in pictures) entries.Add(size <= 64 ? Bitmap(size, rgba) : Png(size, rgba));

            using var file = File.Create(path);
            using var writer = new BinaryWriter(file);
            writer.Write((ushort)0);                // reserved
            writer.Write((ushort)1);                // an icon, not a cursor
            writer.Write((ushort)pictures.Count);
            int offset = 6 + 16 * pictures.Count;
            for (int i = 0; i < pictures.Count; i++)
            {
                int size = pictures[i].size;
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)0);              // no palette
                writer.Write((byte)0);              // reserved
                writer.Write((ushort)1);            // colour planes
                writer.Write((ushort)32);           // bits a pixel
                writer.Write(entries[i].Length);
                writer.Write(offset);
                offset += entries[i].Length;
            }
            foreach (byte[] entry in entries) writer.Write(entry);
        }

        /// <summary>A 32-bit bitmap with its rows from the bottom, blue first, and the mask of where it's clear after it, as an icon holds one.</summary>
        private static byte[] Bitmap(int size, byte[] rgba)
        {
            int maskStride = (size + 31) / 32 * 4;
            var bytes = new byte[40 + size * size * 4 + maskStride * size];
            using (var writer = new BinaryWriter(new MemoryStream(bytes)))
            {
                writer.Write(40);                   // this header's size
                writer.Write(size);
                writer.Write(size * 2);             // the picture and the mask under it
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(0);                    // no compression
                writer.Write(size * size * 4 + maskStride * size);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
            }
            int at = 40;
            for (int y = size - 1; y >= 0; y--)
            {
                for (int x = 0; x < size; x++)
                {
                    int s = (y * size + x) * 4;
                    bytes[at++] = rgba[s + 2];
                    bytes[at++] = rgba[s + 1];
                    bytes[at++] = rgba[s];
                    bytes[at++] = rgba[s + 3];
                }
            }
            for (int y = size - 1; y >= 0; y--)
            {
                int row = at;
                for (int x = 0; x < size; x++)
                {
                    if (rgba[(y * size + x) * 4 + 3] == 0) bytes[row + x / 8] |= (byte)(0x80 >> (x % 8));
                }
                at += maskStride;
            }
            return bytes;
        }

        private static byte[] Png(int size, byte[] rgba)
        {
            string temporary = Path.GetTempFileName();
            try
            {
                PngWriter.Write(temporary, rgba, size, size);
                return File.ReadAllBytes(temporary);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
    }
}
