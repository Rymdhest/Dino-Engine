using OpenTK.Mathematics;

namespace Dino_Engine.GUI.Text
{
    /// <summary>A glyph rasterized at a specific pixel size and stored in the atlas. All values in physical pixels.</summary>
    public class Glyph
    {
        public int GlyphIndex;
        public float Advance;
        public int OffsetX;
        public int OffsetY;
        public int Width;
        public int Height;
        public AtlasRegion Region;
        public bool IsEmpty => Width == 0 || Height == 0;
    }

    /// <summary>
    /// A <see cref="FontFace"/> at one pixel size. Glyphs are rasterized lazily on first use and cached in the shared atlas.
    /// All metrics are in physical pixels. Size is the em size (like CSS font-size).
    /// </summary>
    public class Font
    {
        public const int TabSpaces = 4;

        private readonly Dictionary<int, Glyph> _glyphsByCodepoint = new();
        private readonly Dictionary<int, Glyph> _glyphsByIndex = new();
        private readonly GlyphAtlas _atlas;

        public FontFace Face { get; }
        public float PixelSize { get; }
        public float Scale { get; }
        /// <summary>Distance from the top of the line box to the baseline.</summary>
        public float Ascent { get; }
        /// <summary>Distance from the baseline to the bottom of the line box (positive).</summary>
        public float Descent { get; }
        public float LineHeight { get; }

        public Font(FontFace face, float pixelSize, GlyphAtlas atlas)
        {
            Face = face;
            PixelSize = pixelSize;
            _atlas = atlas;
            Scale = pixelSize / face.UnitsPerEm;
            Ascent = MathF.Ceiling(face.Ascender * Scale);
            Descent = MathF.Ceiling(-face.Descender * Scale);
            LineHeight = Ascent + Descent + MathF.Max(0f, MathF.Round(face.LineGap * Scale));
        }

        public Glyph GetGlyph(int codepoint)
        {
            if (_glyphsByCodepoint.TryGetValue(codepoint, out Glyph? glyph)) return glyph;
            int glyphIndex = Face.GetGlyphIndex(codepoint);
            if (!_glyphsByIndex.TryGetValue(glyphIndex, out glyph))
            {
                glyph = CreateGlyph(glyphIndex);
                _glyphsByIndex[glyphIndex] = glyph;
            }
            _glyphsByCodepoint[codepoint] = glyph;
            return glyph;
        }

        public float GetKerning(Glyph left, Glyph right)
        {
            if (!Face.HasKerning) return 0f;
            return Face.GetKerning(left.GlyphIndex, right.GlyphIndex) * Scale;
        }

        /// <summary>Advance of a character including tab expansion. Control characters have no width.</summary>
        public float GetAdvance(int codepoint)
        {
            if (codepoint == '\t') return GetGlyph(' ').Advance * TabSpaces;
            if (codepoint < 32) return 0f;
            return GetGlyph(codepoint).Advance;
        }

        private Glyph CreateGlyph(int glyphIndex)
        {
            var glyph = new Glyph
            {
                GlyphIndex = glyphIndex,
                Advance = Face.GetAdvanceWidth(glyphIndex) * Scale,
            };

            GlyphBitmap bitmap;
            try
            {
                bitmap = GlyphRasterizer.Rasterize(Face.GetGlyphOutline(glyphIndex), Scale);
            }
            catch (Exception exception) when (exception is IndexOutOfRangeException || exception is ArgumentException)
            {
                // Corrupt glyph data: render nothing rather than crash the frame.
                Console.WriteLine($"GUI: failed to rasterize glyph {glyphIndex} of {Face}: {exception.Message}");
                bitmap = GlyphBitmap.Empty;
            }

            if (!bitmap.IsEmpty)
            {
                glyph.Width = bitmap.Width;
                glyph.Height = bitmap.Height;
                glyph.OffsetX = bitmap.OffsetX;
                glyph.OffsetY = bitmap.OffsetY;
                glyph.Region = _atlas.Add(bitmap.Width, bitmap.Height, bitmap.Pixels);
            }
            return glyph;
        }

        /// <summary>Rasterizes all printable ASCII glyphs up front to avoid hitches the first time text is shown.</summary>
        public void Prewarm()
        {
            for (int c = 32; c < 127; c++) GetGlyph(c);
        }

        public override string ToString() => $"{Face} {PixelSize}px";
    }
}
