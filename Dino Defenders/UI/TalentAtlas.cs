using Dino_Defenders.Talents;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// Every picture on the tree, in one texture: each a signed distance field in its own cell (<see cref="TalentArt"/>), so one quad draws it crisp at any
    /// size. The pictures are worked out on a worker thread the first time the tree is opened and put on the GPU when they're ready; until then nodes are
    /// drawn without them.
    /// </summary>
    internal static class TalentAtlas
    {
        public const int Cell = 96;
        private const int Columns = 16;
        private const float Spread = 6f;

        private static readonly TalentGlyph[] Glyphs = Enum.GetValues<TalentGlyph>();
        private static Task<byte[]>? _building;
        private static int _texture;
        private static int _width;
        private static int _height;

        /// <summary>The texture, or nothing while it's still being made.</summary>
        public static int Texture
        {
            get
            {
                if (_texture != 0) return _texture;
                _building ??= Task.Run(Build);
                if (!_building.IsCompleted) return 0;
                Upload(_building.Result);
                return _texture;
            }
        }

        private static int Rows => (Glyphs.Length + Columns - 1) / Columns;

        private static byte[] Build()
        {
            _width = Columns * Cell;
            _height = Rows * Cell;
            var atlas = new byte[_width * _height];
            for (int i = 0; i < Glyphs.Length; i++)
            {
                if (Glyphs[i] == TalentGlyph.None) continue;
                TalentArt.Picture(Glyphs[i]).Write(atlas, _width, i % Columns * Cell, i / Columns * Cell, Cell, Spread);
            }
            return atlas;
        }

        private static void Upload(byte[] atlas)
        {
            _texture = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R8, _width, _height, 0, PixelFormat.Red, PixelType.UnsignedByte, atlas);
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        /// <summary>Where a picture is in the texture, top left and bottom right.</summary>
        public static (Vector2 from, Vector2 to) Of(TalentGlyph glyph)
        {
            int i = (int)glyph;     // The pictures are in the enum's order, which counts up from nothing.
            int x = i % Columns, y = i / Columns;
            var from = new Vector2(x * Cell / (float)_width, y * Cell / (float)_height);
            var to = new Vector2((x + 1) * Cell / (float)_width, (y + 1) * Cell / (float)_height);
            return (from, to);
        }
    }
}
