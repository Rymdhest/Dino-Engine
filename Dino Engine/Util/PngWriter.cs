using System.IO.Compression;

namespace Dino_Engine.Util
{
    /// <summary>Writes 8-bit RGBA PNG files, for screenshots and generated maps.</summary>
    public static class PngWriter
    {
        private static readonly uint[] CrcTable = BuildCrcTable();

        /// <param name="rgba">Rows from the top of the image, four bytes per pixel.</param>
        public static void Write(string path, byte[] rgba, int width, int height)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            using var file = File.Create(path);
            file.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

            byte[] header = new byte[13];
            WriteBigEndian(header, 0, (uint)width);
            WriteBigEndian(header, 4, (uint)height);
            header[8] = 8;  // Bits per channel.
            header[9] = 6;  // RGBA.
            WriteChunk(file, "IHDR", header);

            using (var compressed = new MemoryStream())
            {
                using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
                {
                    int stride = width * 4;
                    for (int y = 0; y < height; y++)
                    {
                        zlib.WriteByte(0);  // No row filter.
                        zlib.Write(rgba, y * stride, stride);
                    }
                }
                WriteChunk(file, "IDAT", compressed.ToArray());
            }

            WriteChunk(file, "IEND", Array.Empty<byte>());
        }

        private static void WriteChunk(Stream stream, string type, byte[] data)
        {
            byte[] length = new byte[4];
            WriteBigEndian(length, 0, (uint)data.Length);
            stream.Write(length);

            byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            stream.Write(typeBytes);
            stream.Write(data);

            uint crc = 0xFFFFFFFFu;
            crc = UpdateCrc(crc, typeBytes);
            crc = UpdateCrc(crc, data);
            byte[] crcBytes = new byte[4];
            WriteBigEndian(crcBytes, 0, crc ^ 0xFFFFFFFFu);
            stream.Write(crcBytes);
        }

        private static void WriteBigEndian(byte[] target, int offset, uint value)
        {
            target[offset] = (byte)(value >> 24);
            target[offset + 1] = (byte)(value >> 16);
            target[offset + 2] = (byte)(value >> 8);
            target[offset + 3] = (byte)value;
        }

        private static uint UpdateCrc(uint crc, byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                crc = CrcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            }
            return crc;
        }

        private static uint[] BuildCrcTable()
        {
            uint[] table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                }
                table[n] = c;
            }
            return table;
        }
    }
}
