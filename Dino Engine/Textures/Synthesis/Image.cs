using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Textures.Synthesis
{
    /// <summary>How an image holds its texels. Heights need all of a float: a height of 16 bits changes by a step every few texels on a gentle slope, which the normals
    /// show as terraces.</summary>
    public enum ImageFormat
    {
        Scalar,     // R32F: a field.
        Vector,     // RGBA32F: a pattern's channels.
        Colour,     // RGBA16F: a paint.
    }

    /// <summary>A square texture the size of the canvas, from <see cref="ImagePool"/>, wrapping at its edges so that whatever reads past one reads the other side.</summary>
    internal sealed class Image
    {
        public readonly int Texture;
        public readonly ImageFormat Format;
        public readonly int Size;
        public readonly int Levels;

        public Image(ImageFormat format, int size, int levels)
        {
            Format = format;
            Size = size;
            Levels = levels;
            Texture = GL.GenTexture();
            Synthesizer.Edit(Texture);
            GL.TexStorage2D(TextureTarget2d.Texture2D, levels, Internal(format), size, size);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)(levels > 1 ? TextureMinFilter.LinearMipmapLinear : TextureMinFilter.Linear));
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            Synthesizer.Edit(0);
        }

        public static SizedInternalFormat Internal(ImageFormat format) => format switch
        {
            ImageFormat.Scalar => SizedInternalFormat.R32f,
            ImageFormat.Vector => SizedInternalFormat.Rgba32f,
            _ => SizedInternalFormat.Rgba16f,
        };

        public void Delete() => GL.DeleteTexture(Texture);
    }

    /// <summary>
    /// Images given out and taken back while a material is made. A material's graph is evaluated keeping only what's still to be read (<see cref="Canvas"/>), so the
    /// pool holds as many images as the widest point of a graph needs and no more, and the next material reuses them.
    /// </summary>
    internal sealed class ImagePool
    {
        private readonly List<Image> _free = new();
        private int _made;

        public int Made => _made;

        public Image Take(ImageFormat format, int size, int levels = 1)
        {
            for (int i = _free.Count - 1; i >= 0; i--)
            {
                Image image = _free[i];
                if (image.Format == format && image.Size == size && image.Levels == levels)
                {
                    _free.RemoveAt(i);
                    return image;
                }
            }
            _made++;
            return new Image(format, size, levels);
        }

        public void Give(Image image) => _free.Add(image);

        public void Dispose()
        {
            foreach (Image image in _free) image.Delete();
            _free.Clear();
        }
    }
}
