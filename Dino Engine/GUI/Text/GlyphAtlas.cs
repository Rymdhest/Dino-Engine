using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.GUI.Text
{
    /// <summary>A packed location inside a <see cref="GlyphAtlas"/> page.</summary>
    public readonly struct AtlasRegion
    {
        public readonly int Texture;
        public readonly Vector2 UV0;
        public readonly Vector2 UV1;

        public AtlasRegion(int texture, Vector2 uv0, Vector2 uv1)
        {
            Texture = texture;
            UV0 = uv0;
            UV1 = uv1;
        }
    }

    /// <summary>
    /// Single channel (R8) texture atlas shared by every font. Glyphs are packed into horizontal shelves;
    /// when a page fills up a new page (texture) is created, so the atlas never runs out of space.
    /// </summary>
    public class GlyphAtlas : IDisposable
    {
        private const int Gap = 1;

        private class Shelf
        {
            public int Y;
            public int Height;
            public int X;
        }

        private class Page
        {
            public int Texture;
            public int NextShelfY;
            public readonly List<Shelf> Shelves = new();
        }

        private readonly List<Page> _pages = new();
        private readonly int _pageSize;

        public int PageSize => _pageSize;
        public int PageCount => _pages.Count;
        public IEnumerable<int> PageTextures => _pages.Select(p => p.Texture);

        public GlyphAtlas(int pageSize = 1024)
        {
            _pageSize = pageSize;
        }

        public AtlasRegion Add(int width, int height, byte[] pixels)
        {
            if (width <= 0 || height <= 0) return default;
            if (width + Gap * 2 > _pageSize || height + Gap * 2 > _pageSize)
                throw new ArgumentException($"Glyph of {width}x{height} does not fit in a {_pageSize} atlas page.");

            foreach (var page in _pages)
            {
                if (TryPack(page, width, height, out int x, out int y)) return Upload(page, x, y, width, height, pixels);
            }
            var newPage = CreatePage();
            if (!TryPack(newPage, width, height, out int nx, out int ny))
                throw new InvalidOperationException("Failed to pack glyph into an empty atlas page.");
            return Upload(newPage, nx, ny, width, height, pixels);
        }

        private bool TryPack(Page page, int width, int height, out int x, out int y)
        {
            int paddedWidth = width + Gap;
            int paddedHeight = height + Gap;

            // Best fitting existing shelf: tall enough, not wastefully tall, with horizontal room.
            Shelf? best = null;
            foreach (var shelf in page.Shelves)
            {
                if (shelf.Height < paddedHeight) continue;
                if (shelf.Height > paddedHeight * 1.5f + 4) continue;
                if (shelf.X + paddedWidth > _pageSize) continue;
                if (best == null || shelf.Height < best.Height) best = shelf;
            }

            if (best == null)
            {
                if (page.NextShelfY + paddedHeight > _pageSize)
                {
                    x = y = 0;
                    return false;
                }
                best = new Shelf { Y = page.NextShelfY, Height = paddedHeight, X = Gap };
                page.Shelves.Add(best);
                page.NextShelfY += paddedHeight;
                if (best.X + paddedWidth > _pageSize)
                {
                    x = y = 0;
                    return false;
                }
            }

            x = best.X;
            y = best.Y + Gap;
            best.X += paddedWidth;
            return true;
        }

        private Page CreatePage()
        {
            var page = new Page { NextShelfY = 0 };
            page.Texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, page.Texture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R8, _pageSize, _pageSize, 0, PixelFormat.Red, PixelType.UnsignedByte, new byte[_pageSize * _pageSize]);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            _pages.Add(page);
            return page;
        }

        private AtlasRegion Upload(Page page, int x, int y, int width, int height, byte[] pixels)
        {
            GL.GetInteger(GetPName.UnpackAlignment, out int previousAlignment);
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            GL.BindTexture(TextureTarget.Texture2D, page.Texture);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, x, y, width, height, PixelFormat.Red, PixelType.UnsignedByte, pixels);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, previousAlignment);

            float inv = 1f / _pageSize;
            return new AtlasRegion(page.Texture, new Vector2(x * inv, y * inv), new Vector2((x + width) * inv, (y + height) * inv));
        }

        public void Dispose()
        {
            foreach (var page in _pages) GL.DeleteTexture(page.Texture);
            _pages.Clear();
        }
    }
}
