using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Textures
{
    /// <summary>
    /// Every material's maps as pictures (--materialsheet folder), for judging a recipe with no world: colour, normal, height, ambient occlusion and roughness side by
    /// side, and the height tiled two by two, where a seam would show as a line. Read from the arrays the renderers read, at a quarter of their size, and drawn with
    /// v upwards, as on a wall.
    /// </summary>
    public static class MaterialSheet
    {
        private const int Level = 2;
        private const int Panels = 6;

        public static void Write(string folder, int albedoArray, int normalArray, int materialArray, int size, int layers)
        {
            int side = Math.Max(size >> Level, 1);
            byte[] albedo = Read(albedoArray, side, layers);
            byte[] normal = Read(normalArray, side, layers);
            byte[] material = Read(materialArray, side, layers);
            Directory.CreateDirectory(folder);

            int width = side * Panels;
            var overview = new byte[side / 2 * 8 * (side / 2 * ((layers + 7) / 8)) * 4];
            int overviewWidth = side / 2 * 8;
            foreach (MaterialSpec spec in MaterialCatalog.All)
            {
                int layer = spec.Index;
                byte[] sheet = new byte[width * side * 4];
                for (int y = 0; y < side; y++)
                {
                    for (int x = 0; x < side; x++)
                    {
                        int at = ((layer * side + y) * side + x) * 4;
                        int row = side - 1 - y;
                        // Colour, as stored: sRGB, which is what a picture is.
                        Put(sheet, width, x, row, albedo[at], albedo[at + 1], albedo[at + 2]);
                        // The normal, with z rebuilt as the renderers rebuild it.
                        float nx = normal[at] / 127.5f - 1f, ny = normal[at + 1] / 127.5f - 1f;
                        float nz = MathF.Sqrt(MathF.Max(1f - nx * nx - ny * ny, 0f));
                        Put(sheet, width, side + x, row, normal[at], normal[at + 1], (byte)(nz * 127.5f + 127.5f));
                        Put(sheet, width, 2 * side + x, row, material[at + 3], material[at + 3], material[at + 3]);
                        Put(sheet, width, 3 * side + x, row, normal[at + 3], normal[at + 3], normal[at + 3]);
                        Put(sheet, width, 4 * side + x, row, material[at], material[at], material[at]);
                        // The height tiled two by two, at half size.
                        int tx = (x * 2) % side, ty = (y * 2) % side;
                        int tiled = ((layer * side + ty) * side + tx) * 4;
                        Put(sheet, width, 5 * side + x, row, material[tiled + 3], material[tiled + 3], material[tiled + 3]);
                    }
                }
                PngWriter.Write(Path.Combine(folder, $"{layer:00}-{spec.Name}.png"), sheet, width, side);

                int ox = layer % 8 * (side / 2), oy = layer / 8 * (side / 2);
                for (int y = 0; y < side / 2; y++)
                {
                    for (int x = 0; x < side / 2; x++)
                    {
                        int at = ((layer * side + y * 2) * side + x * 2) * 4;
                        int to = ((oy + side / 2 - 1 - y) * overviewWidth + ox + x) * 4;
                        overview[to] = albedo[at];
                        overview[to + 1] = albedo[at + 1];
                        overview[to + 2] = albedo[at + 2];
                        overview[to + 3] = 255;
                    }
                }
            }
            PngWriter.Write(Path.Combine(folder, "all.png"), overview, overviewWidth, overview.Length / 4 / overviewWidth);
            Console.WriteLine($"Wrote {layers} material sheets to {folder}.");
        }

        private static void Put(byte[] sheet, int width, int x, int y, byte r, byte g, byte b)
        {
            int at = (y * width + x) * 4;
            sheet[at] = r;
            sheet[at + 1] = g;
            sheet[at + 2] = b;
            sheet[at + 3] = 255;
        }

        private static byte[] Read(int array, int side, int layers)
        {
            byte[] pixels = new byte[side * side * layers * 4];
            GL.BindTexture(TextureTarget.Texture2DArray, array);
            GL.PixelStore(PixelStoreParameter.PackAlignment, 1);
            GL.GetTexImage(TextureTarget.Texture2DArray, Level, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            GL.BindTexture(TextureTarget.Texture2DArray, 0);
            return pixels;
        }
    }
}
