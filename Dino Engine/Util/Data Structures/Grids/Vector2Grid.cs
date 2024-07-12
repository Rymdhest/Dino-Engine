using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Util.Data_Structures.Grids
{
    public class Vector2Grid : Grid<Vector2>
    {
        public Vector2Grid(Vector2i resolution) : base(resolution)
        {
        }

        public override Vector2 BilinearInterpolate(Vector2 p)
        {
            int x1 = (int)Math.Floor(p.X);
            int x2 = x1 + 1;
            int y1 = (int)Math.Floor(p.Y);
            int y2 = y1 + 1;

            x1 = Math.Clamp(x1, 0, Resolution.X - 1);
            x2 = Math.Clamp(x2, 0, Resolution.X - 1);
            y1 = Math.Clamp(y1, 0, Resolution.Y - 1);
            y2 = Math.Clamp(y2, 0, Resolution.Y - 1);

            float t_x = p.X - x1;
            float t_y = p.Y - y1;

            Vector2 heightQ11 = Values[x1, y1];
            Vector2 heightQ12 = Values[x2, y1];
            Vector2 heightQ21 = Values[x1, y2];
            Vector2 heightQ22 = Values[x2, y2];

            // Interpolate along the x-axis (bottom and top rows)
            Vector2 interpolatedHeight1 = heightQ11 * (1 - t_x) + heightQ12 * t_x;
            Vector2 interpolatedHeight2 = heightQ21 * (1 - t_x) + heightQ22 * t_x;

            // Interpolate along the y-axis (left and right columns)
            Vector2 interpolatedHeight = interpolatedHeight1 * (1 - t_y) + interpolatedHeight2 * t_y;

            return interpolatedHeight;
        }

        protected override int GenerateTexture()
        {

            var pixels = new float[2 * Resolution.X * Resolution.Y];
            for (int y = 0; y < Resolution.Y; y++)
            {
                for (int x = 0; x < Resolution.X; x++)
                {
                    int i = y * Resolution.Y + x;
                    pixels[i * 2 + 0] = Values[x, y].X;
                    pixels[i * 2 + 1] = Values[x, y].Y;
                }
            }
            int texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, Resolution.X, Resolution.Y, 0, PixelFormat.Rg, PixelType.Float, pixels);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (float)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (float)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (float)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (float)TextureWrapMode.ClampToEdge);

            return texture;
        }
    }
}
