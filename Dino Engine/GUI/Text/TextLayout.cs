namespace Dino_Engine.GUI.Text
{
    /// <summary>One laid out line of text: a slice of the source string and its width in physical pixels.</summary>
    public readonly struct TextLine
    {
        public readonly int Start;
        public readonly int Length;
        public readonly float Width;

        public TextLine(int start, int length, float width)
        {
            Start = start;
            Length = length;
            Width = width;
        }
    }

    /// <summary>A visible glyph with its pen position in physical pixels from the start of its line (kerning applied, not rounded).</summary>
    public readonly struct PositionedGlyph
    {
        public readonly Glyph Glyph;
        public readonly float X;

        public PositionedGlyph(Glyph glyph, float x)
        {
            Glyph = glyph;
            X = x;
        }
    }

    /// <summary>
    /// Text measurement and line breaking. Works in the physical pixel units of a <see cref="Font"/>.
    /// Lines break on '\n'; with a finite max width they also wrap at spaces, and words longer than
    /// a line are broken between characters.
    /// </summary>
    public static class TextLayout
    {
        public static float MeasureWidth(Font font, string text, int start, int length)
        {
            float width = 0f;
            Glyph? previous = null;
            int end = start + length;
            for (int i = start; i < end; i++)
            {
                int codepoint = ReadCodepoint(text, ref i);
                if (codepoint < 32 && codepoint != '\t')
                {
                    previous = null;
                    continue;
                }
                Glyph glyph = font.GetGlyph(codepoint);
                if (previous != null) width += font.GetKerning(previous, glyph);
                width += font.GetAdvance(codepoint);
                previous = codepoint == '\t' ? null : glyph;
            }
            return width;
        }

        public static float MeasureWidth(Font font, string text) => MeasureWidth(font, text, 0, text.Length);

        /// <summary>
        /// Resolves glyphs, kerning and pen positions for one line so it can be drawn repeatedly without re-shaping.
        /// Glyphs without pixels (spaces) are left out; tabs and control characters only move the pen.
        /// </summary>
        public static void ShapeLine(Font font, string text, int start, int length, List<PositionedGlyph> output)
        {
            output.Clear();
            float penX = 0f;
            Glyph? previous = null;
            int end = start + length;
            for (int i = start; i < end; i++)
            {
                int codepoint = ReadCodepoint(text, ref i);
                if (codepoint < 32)
                {
                    if (codepoint == '\t') penX += font.GetAdvance('\t');
                    previous = null;
                    continue;
                }
                Glyph glyph = font.GetGlyph(codepoint);
                if (previous != null) penX += font.GetKerning(previous, glyph);
                if (!glyph.IsEmpty) output.Add(new PositionedGlyph(glyph, penX));
                penX += glyph.Advance;
                previous = glyph;
            }
        }

        public static List<TextLine> BreakLines(Font font, string text, float maxWidth = float.PositiveInfinity)
        {
            var lines = new List<TextLine>();
            BreakLines(font, text, maxWidth, lines);
            return lines;
        }

        public static void BreakLines(Font font, string text, float maxWidth, List<TextLine> lines)
        {
            lines.Clear();
            int lineStart = 0;
            for (int i = 0; i <= text.Length; i++)
            {
                if (i == text.Length || text[i] == '\n')
                {
                    int end = i;
                    if (end > lineStart && text[end - 1] == '\r') end--;
                    WrapParagraph(font, text, lineStart, end - lineStart, maxWidth, lines);
                    lineStart = i + 1;
                }
            }
        }

        private static void WrapParagraph(Font font, string text, int start, int length, float maxWidth, List<TextLine> lines)
        {
            if (float.IsPositiveInfinity(maxWidth) || length == 0)
            {
                lines.Add(new TextLine(start, length, MeasureWidth(font, text, start, length)));
                return;
            }
            if (maxWidth < 1f) maxWidth = 1f;

            int end = start + length;
            int lineStart = start;
            while (lineStart < end)
            {
                float width = 0f;
                int lastBreak = -1; // index of the last space at which we may break
                int i = lineStart;
                Glyph? previous = null;
                while (i < end)
                {
                    int charIndex = i;
                    int codepoint = ReadCodepoint(text, ref i);
                    float advance = font.GetAdvance(codepoint);
                    Glyph? glyph = codepoint >= 32 ? font.GetGlyph(codepoint) : null;
                    if (previous != null && glyph != null) advance += font.GetKerning(previous, glyph);
                    if (codepoint == ' ') lastBreak = charIndex;

                    if (width + advance > maxWidth && charIndex > lineStart && codepoint != ' ')
                    {
                        if (lastBreak > lineStart)
                        {
                            // Break at the last space; the space itself is dropped.
                            lines.Add(new TextLine(lineStart, lastBreak - lineStart, MeasureWidth(font, text, lineStart, lastBreak - lineStart)));
                            lineStart = lastBreak + 1;
                        }
                        else
                        {
                            // A single word longer than the line: break between characters.
                            lines.Add(new TextLine(lineStart, charIndex - lineStart, width));
                            lineStart = charIndex;
                        }
                        goto nextLine;
                    }
                    width += advance;
                    previous = codepoint == '\t' ? null : glyph;
                    i++;
                }
                lines.Add(new TextLine(lineStart, end - lineStart, width));
                return;
            nextLine:;
            }
        }

        /// <summary>
        /// X offset (physical pixels) of every caret position in a single line of text: result[i] is the position
        /// before character i, result[text.Length] is the end of the line. Surrogate pairs share their positions.
        /// </summary>
        public static float[] GetCaretPositions(Font font, string text)
        {
            float[] positions = new float[text.Length + 1];
            float x = 0f;
            Glyph? previous = null;
            for (int i = 0; i < text.Length; i++)
            {
                int start = i;
                int codepoint = ReadCodepoint(text, ref i);
                Glyph? glyph = codepoint >= 32 ? font.GetGlyph(codepoint) : null;
                if (previous != null && glyph != null) x += font.GetKerning(previous, glyph);
                positions[start] = x;
                x += font.GetAdvance(codepoint);
                if (i > start) positions[i] = x; // second half of a surrogate pair
                previous = codepoint == '\t' ? null : glyph;
            }
            positions[text.Length] = x;
            return positions;
        }

        /// <summary>Caret index closest to an x offset (physical pixels) in a single line of text.</summary>
        public static int HitTest(float[] caretPositions, float x)
        {
            for (int i = 0; i < caretPositions.Length - 1; i++)
            {
                float middle = (caretPositions[i] + caretPositions[i + 1]) * 0.5f;
                if (x < middle) return i;
            }
            return caretPositions.Length - 1;
        }

        /// <summary>Reads a code point at index i, advancing i past the low surrogate of a pair.</summary>
        public static int ReadCodepoint(string text, ref int i)
        {
            char c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                int codepoint = char.ConvertToUtf32(c, text[i + 1]);
                i++;
                return codepoint;
            }
            return c;
        }
    }
}
