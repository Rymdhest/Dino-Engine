using OpenTK.Mathematics;
using System.Text;

namespace Dino_Engine.GUI.Text
{
    /// <summary>
    /// A parsed TrueType font face (glyf/loca outlines). Loaded straight from .ttf/.ttc bytes,
    /// no external libraries. Holds unscaled data only; see <see cref="Font"/> for a sized instance.
    /// Supported: cmap formats 4 and 12, simple and composite glyphs, legacy 'kern' format 0, TrueType collections.
    /// Not supported: CFF outlines (.otf with 'OTTO' signature), GPOS kerning, hinting.
    /// </summary>
    public class FontFace
    {
        private readonly byte[] _data;
        private readonly Dictionary<string, int> _tables = new();
        private readonly Dictionary<int, int> _charToGlyph = new();
        private readonly Dictionary<uint, short> _kerning = new();

        private int _cmapOffset = -1;
        private int _cmapFormat = -1;
        private int _locaOffset;
        private int _glyfOffset;
        private int _glyfLength;
        private int _hmtxOffset;
        private int _numberOfHMetrics;
        private bool _longLocaFormat;

        public string FamilyName { get; private set; } = "Unknown";
        public string SubfamilyName { get; private set; } = "Regular";
        public string SourcePath { get; private set; } = "";
        public int UnitsPerEm { get; private set; }
        public int Ascender { get; private set; }
        public int Descender { get; private set; }
        public int LineGap { get; private set; }
        public int GlyphCount { get; private set; }
        public bool HasKerning => _kerning.Count > 0;

        public FontFace(byte[] data, int faceIndex = 0, string sourcePath = "")
        {
            _data = data;
            SourcePath = sourcePath;
            int offset = GetFaceOffset(data, faceIndex);
            ReadTableDirectory(offset);
            Parse();
        }

        public static FontFace FromFile(string path, int faceIndex = 0)
        {
            return new FontFace(File.ReadAllBytes(path), faceIndex, path);
        }

        /// <summary>Number of faces in a font file (1 for plain .ttf, n for .ttc collections).</summary>
        public static int GetFaceCount(byte[] data)
        {
            if (data.Length >= 12 && ReadTag(data, 0) == "ttcf") return (int)ReadU32(data, 8);
            return 1;
        }

        private static int GetFaceOffset(byte[] data, int faceIndex)
        {
            if (data.Length < 12) throw new InvalidDataException("Font file is too small.");
            string tag = ReadTag(data, 0);
            if (tag == "ttcf")
            {
                int numFonts = (int)ReadU32(data, 8);
                if (faceIndex < 0 || faceIndex >= numFonts) throw new ArgumentOutOfRangeException(nameof(faceIndex));
                return (int)ReadU32(data, 12 + faceIndex * 4);
            }
            if (faceIndex != 0) throw new ArgumentOutOfRangeException(nameof(faceIndex));
            return 0;
        }

        private void ReadTableDirectory(int offset)
        {
            uint version = ReadU32(_data, offset);
            if (version == 0x4F54544F) throw new NotSupportedException("CFF based OpenType fonts ('OTTO') are not supported, use a TrueType outline font.");
            if (version != 0x00010000 && version != 0x74727565) throw new InvalidDataException("Not a TrueType font.");

            int numTables = ReadU16(_data, offset + 4);
            for (int i = 0; i < numTables; i++)
            {
                int record = offset + 12 + i * 16;
                string tag = ReadTag(_data, record);
                int tableOffset = (int)ReadU32(_data, record + 8);
                int tableLength = (int)ReadU32(_data, record + 12);
                if (tableOffset < 0 || tableOffset + tableLength > _data.Length) continue;
                _tables[tag] = tableOffset;
                if (tag == "glyf") _glyfLength = tableLength;
            }
        }

        private int Table(string tag)
        {
            if (!_tables.TryGetValue(tag, out int offset)) throw new InvalidDataException($"Font is missing required table '{tag}'.");
            return offset;
        }

        private void Parse()
        {
            int head = Table("head");
            UnitsPerEm = ReadU16(_data, head + 18);
            if (UnitsPerEm == 0) UnitsPerEm = 1000;
            _longLocaFormat = ReadI16(_data, head + 50) != 0;

            int maxp = Table("maxp");
            GlyphCount = ReadU16(_data, maxp + 4);

            int hhea = Table("hhea");
            Ascender = ReadI16(_data, hhea + 4);
            Descender = ReadI16(_data, hhea + 6);
            LineGap = ReadI16(_data, hhea + 8);
            _numberOfHMetrics = ReadU16(_data, hhea + 34);

            _hmtxOffset = Table("hmtx");
            _locaOffset = Table("loca");
            _glyfOffset = Table("glyf");

            ParseCmap();
            if (_tables.ContainsKey("kern")) ParseKern(_tables["kern"]);
            if (_tables.ContainsKey("name")) ParseNames(_tables["name"]);
        }

        // ---------------------------------------------------------------- cmap

        private void ParseCmap()
        {
            int cmap = Table("cmap");
            int numSubtables = ReadU16(_data, cmap + 2);
            int bestOffset = -1;
            int bestScore = -1;
            for (int i = 0; i < numSubtables; i++)
            {
                int record = cmap + 4 + i * 8;
                int platform = ReadU16(_data, record);
                int encoding = ReadU16(_data, record + 2);
                int subOffset = cmap + (int)ReadU32(_data, record + 4);
                int format = ReadU16(_data, subOffset);
                if (format != 4 && format != 12) continue;

                int score = -1;
                if (platform == 3 && encoding == 10 && format == 12) score = 4;
                else if (platform == 0 && format == 12) score = 3;
                else if (platform == 3 && encoding == 1 && format == 4) score = 2;
                else if (platform == 0 && format == 4) score = 1;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestOffset = subOffset;
                }
            }
            if (bestOffset < 0) throw new InvalidDataException("Font has no supported Unicode cmap subtable.");
            _cmapOffset = bestOffset;
            _cmapFormat = ReadU16(_data, bestOffset);
        }

        /// <summary>Maps a Unicode code point to a glyph index. Returns 0 (.notdef) if the font lacks it.</summary>
        public int GetGlyphIndex(int codepoint)
        {
            if (_charToGlyph.TryGetValue(codepoint, out int cached)) return cached;
            int glyph = _cmapFormat == 4 ? LookupFormat4(codepoint) : LookupFormat12(codepoint);
            if (glyph < 0 || glyph >= GlyphCount) glyph = 0;
            _charToGlyph[codepoint] = glyph;
            return glyph;
        }

        public bool HasGlyph(int codepoint) => GetGlyphIndex(codepoint) != 0;

        private int LookupFormat4(int codepoint)
        {
            if (codepoint > 0xFFFF) return 0;
            int t = _cmapOffset;
            int segCount = ReadU16(_data, t + 6) / 2;
            int endCodes = t + 14;
            int startCodes = endCodes + segCount * 2 + 2;
            int idDeltas = startCodes + segCount * 2;
            int idRangeOffsets = idDeltas + segCount * 2;

            // binary search for the first segment with endCode >= codepoint
            int lo = 0, hi = segCount - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (ReadU16(_data, endCodes + mid * 2) < codepoint) lo = mid + 1;
                else hi = mid;
            }
            int seg = lo;
            if (ReadU16(_data, endCodes + seg * 2) < codepoint) return 0;
            int start = ReadU16(_data, startCodes + seg * 2);
            if (start > codepoint) return 0;

            int delta = ReadU16(_data, idDeltas + seg * 2);
            int rangeOffsetPos = idRangeOffsets + seg * 2;
            int rangeOffset = ReadU16(_data, rangeOffsetPos);
            if (rangeOffset == 0) return (codepoint + delta) & 0xFFFF;

            int glyphAddress = rangeOffsetPos + rangeOffset + (codepoint - start) * 2;
            if (glyphAddress + 1 >= _data.Length) return 0;
            int glyph = ReadU16(_data, glyphAddress);
            if (glyph == 0) return 0;
            return (glyph + delta) & 0xFFFF;
        }

        private int LookupFormat12(int codepoint)
        {
            int t = _cmapOffset;
            int numGroups = (int)ReadU32(_data, t + 12);
            int lo = 0, hi = numGroups - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                int group = t + 16 + mid * 12;
                uint startChar = ReadU32(_data, group);
                uint endChar = ReadU32(_data, group + 4);
                if ((uint)codepoint < startChar) hi = mid - 1;
                else if ((uint)codepoint > endChar) lo = mid + 1;
                else return (int)(ReadU32(_data, group + 8) + ((uint)codepoint - startChar));
            }
            return 0;
        }

        // ---------------------------------------------------------------- metrics

        public int GetAdvanceWidth(int glyphIndex)
        {
            if (_numberOfHMetrics == 0) return 0;
            if (glyphIndex >= _numberOfHMetrics) glyphIndex = _numberOfHMetrics - 1;
            return ReadU16(_data, _hmtxOffset + glyphIndex * 4);
        }

        public int GetKerning(int leftGlyph, int rightGlyph)
        {
            if (_kerning.Count == 0) return 0;
            return _kerning.TryGetValue(((uint)leftGlyph << 16) | (uint)rightGlyph, out short value) ? value : 0;
        }

        private void ParseKern(int kern)
        {
            // Only the Microsoft style table (version 0) with format 0 horizontal subtables.
            if (ReadU16(_data, kern) != 0) return;
            int nTables = ReadU16(_data, kern + 2);
            int offset = kern + 4;
            for (int i = 0; i < nTables; i++)
            {
                if (offset + 6 > _data.Length) return;
                int length = ReadU16(_data, offset + 2);
                int coverage = ReadU16(_data, offset + 4);
                int format = coverage >> 8;
                bool horizontal = (coverage & 1) != 0;
                bool minimum = (coverage & 2) != 0;
                bool crossStream = (coverage & 4) != 0;
                if (format == 0 && horizontal && !minimum && !crossStream)
                {
                    int nPairs = ReadU16(_data, offset + 6);
                    int pairs = offset + 14;
                    for (int p = 0; p < nPairs; p++)
                    {
                        int pair = pairs + p * 6;
                        if (pair + 6 > _data.Length) break;
                        uint key = ((uint)ReadU16(_data, pair) << 16) | (uint)ReadU16(_data, pair + 2);
                        _kerning[key] = ReadI16(_data, pair + 4);
                    }
                }
                if (length == 0) return;
                offset += length;
            }
        }

        // ---------------------------------------------------------------- names

        private void ParseNames(int name)
        {
            string? family = null, subfamily = null, typoFamily = null, typoSubfamily = null;
            int count = ReadU16(_data, name + 2);
            int stringOffset = name + ReadU16(_data, name + 4);
            int bestFamilyScore = -1, bestSubScore = -1, bestTypoFamScore = -1, bestTypoSubScore = -1;
            for (int i = 0; i < count; i++)
            {
                int record = name + 6 + i * 12;
                int platform = ReadU16(_data, record);
                int encoding = ReadU16(_data, record + 2);
                int language = ReadU16(_data, record + 4);
                int nameId = ReadU16(_data, record + 6);
                int length = ReadU16(_data, record + 8);
                int offset = stringOffset + ReadU16(_data, record + 10);
                if (nameId != 1 && nameId != 2 && nameId != 16 && nameId != 17) continue;
                if (offset + length > _data.Length) continue;

                string? value = null;
                int score = 0;
                if (platform == 3 || platform == 0)
                {
                    value = Encoding.BigEndianUnicode.GetString(_data, offset, length);
                    score = (platform == 3 && language == 0x409) ? 3 : 2;
                }
                else if (platform == 1 && encoding == 0)
                {
                    value = Encoding.ASCII.GetString(_data, offset, length);
                    score = 1;
                }
                if (value == null) continue;

                switch (nameId)
                {
                    case 1: if (score > bestFamilyScore) { bestFamilyScore = score; family = value; } break;
                    case 2: if (score > bestSubScore) { bestSubScore = score; subfamily = value; } break;
                    case 16: if (score > bestTypoFamScore) { bestTypoFamScore = score; typoFamily = value; } break;
                    case 17: if (score > bestTypoSubScore) { bestTypoSubScore = score; typoSubfamily = value; } break;
                }
            }
            FamilyName = typoFamily ?? family ?? FamilyName;
            SubfamilyName = typoSubfamily ?? subfamily ?? SubfamilyName;
        }

        // ---------------------------------------------------------------- outlines

        public struct OutlinePoint
        {
            public Vector2 Position;
            public bool OnCurve;
            public OutlinePoint(Vector2 position, bool onCurve)
            {
                Position = position;
                OnCurve = onCurve;
            }
        }

        /// <summary>
        /// Returns the outline of a glyph as a list of contours in font units (y up).
        /// Composite glyphs are flattened into their transformed components.
        /// </summary>
        public List<List<OutlinePoint>> GetGlyphOutline(int glyphIndex)
        {
            var contours = new List<List<OutlinePoint>>();
            AppendGlyphOutline(glyphIndex, Matrix2.Identity, Vector2.Zero, contours, 0);
            return contours;
        }

        private bool GetGlyphLocation(int glyphIndex, out int offset, out int length)
        {
            offset = 0;
            length = 0;
            if (glyphIndex < 0 || glyphIndex >= GlyphCount) return false;
            int start, end;
            if (_longLocaFormat)
            {
                start = (int)ReadU32(_data, _locaOffset + glyphIndex * 4);
                end = (int)ReadU32(_data, _locaOffset + glyphIndex * 4 + 4);
            }
            else
            {
                start = ReadU16(_data, _locaOffset + glyphIndex * 2) * 2;
                end = ReadU16(_data, _locaOffset + glyphIndex * 2 + 2) * 2;
            }
            if (end <= start || end > _glyfLength) return false;
            offset = _glyfOffset + start;
            length = end - start;
            return true;
        }

        private void AppendGlyphOutline(int glyphIndex, Matrix2 transform, Vector2 translation, List<List<OutlinePoint>> contours, int depth)
        {
            if (depth > 8) return; // guard against malformed recursive composites
            if (!GetGlyphLocation(glyphIndex, out int g, out _)) return; // empty glyph (e.g. space)

            int numberOfContours = ReadI16(_data, g);
            if (numberOfContours >= 0) AppendSimpleGlyph(g, numberOfContours, transform, translation, contours);
            else AppendCompositeGlyph(g, transform, translation, contours, depth);
        }

        private void AppendSimpleGlyph(int g, int numberOfContours, Matrix2 transform, Vector2 translation, List<List<OutlinePoint>> contours)
        {
            if (numberOfContours == 0) return;
            int endPtsOffset = g + 10;
            int[] endPts = new int[numberOfContours];
            for (int i = 0; i < numberOfContours; i++) endPts[i] = ReadU16(_data, endPtsOffset + i * 2);
            int numPoints = endPts[numberOfContours - 1] + 1;

            int instructionLength = ReadU16(_data, endPtsOffset + numberOfContours * 2);
            int p = endPtsOffset + numberOfContours * 2 + 2 + instructionLength;

            byte[] flags = new byte[numPoints];
            for (int i = 0; i < numPoints;)
            {
                byte flag = _data[p++];
                flags[i++] = flag;
                if ((flag & 8) != 0)
                {
                    int repeat = _data[p++];
                    for (int r = 0; r < repeat && i < numPoints; r++) flags[i++] = flag;
                }
            }

            int[] xs = new int[numPoints];
            int value = 0;
            for (int i = 0; i < numPoints; i++)
            {
                byte flag = flags[i];
                if ((flag & 2) != 0)
                {
                    int dx = _data[p++];
                    value += (flag & 16) != 0 ? dx : -dx;
                }
                else if ((flag & 16) == 0)
                {
                    value += ReadI16(_data, p);
                    p += 2;
                }
                xs[i] = value;
            }

            int[] ys = new int[numPoints];
            value = 0;
            for (int i = 0; i < numPoints; i++)
            {
                byte flag = flags[i];
                if ((flag & 4) != 0)
                {
                    int dy = _data[p++];
                    value += (flag & 32) != 0 ? dy : -dy;
                }
                else if ((flag & 32) == 0)
                {
                    value += ReadI16(_data, p);
                    p += 2;
                }
                ys[i] = value;
            }

            int startPoint = 0;
            for (int c = 0; c < numberOfContours; c++)
            {
                int endPoint = endPts[c];
                var contour = new List<OutlinePoint>(endPoint - startPoint + 1);
                for (int i = startPoint; i <= endPoint && i < numPoints; i++)
                {
                    Vector2 local = new Vector2(xs[i], ys[i]);
                    Vector2 transformed = new Vector2(
                        transform.M11 * local.X + transform.M12 * local.Y,
                        transform.M21 * local.X + transform.M22 * local.Y) + translation;
                    contour.Add(new OutlinePoint(transformed, (flags[i] & 1) != 0));
                }
                if (contour.Count > 0) contours.Add(contour);
                startPoint = endPoint + 1;
            }
        }

        private void AppendCompositeGlyph(int g, Matrix2 transform, Vector2 translation, List<List<OutlinePoint>> contours, int depth)
        {
            const int ARG_1_AND_2_ARE_WORDS = 0x0001;
            const int ARGS_ARE_XY_VALUES = 0x0002;
            const int WE_HAVE_A_SCALE = 0x0008;
            const int MORE_COMPONENTS = 0x0020;
            const int WE_HAVE_AN_X_AND_Y_SCALE = 0x0040;
            const int WE_HAVE_A_TWO_BY_TWO = 0x0080;
            const int SCALED_COMPONENT_OFFSET = 0x0800;

            int p = g + 10;
            int flags;
            do
            {
                flags = ReadU16(_data, p);
                int componentGlyph = ReadU16(_data, p + 2);
                p += 4;

                float dx = 0, dy = 0;
                if ((flags & ARG_1_AND_2_ARE_WORDS) != 0)
                {
                    if ((flags & ARGS_ARE_XY_VALUES) != 0)
                    {
                        dx = ReadI16(_data, p);
                        dy = ReadI16(_data, p + 2);
                    }
                    p += 4;
                }
                else
                {
                    if ((flags & ARGS_ARE_XY_VALUES) != 0)
                    {
                        dx = (sbyte)_data[p];
                        dy = (sbyte)_data[p + 1];
                    }
                    p += 2;
                }
                // Point-matching composites (ARGS_ARE_XY_VALUES not set) are rare; they get a zero offset.

                float a = 1, b = 0, c = 0, d = 1;
                if ((flags & WE_HAVE_A_SCALE) != 0)
                {
                    a = d = ReadF2Dot14(p);
                    p += 2;
                }
                else if ((flags & WE_HAVE_AN_X_AND_Y_SCALE) != 0)
                {
                    a = ReadF2Dot14(p);
                    d = ReadF2Dot14(p + 2);
                    p += 4;
                }
                else if ((flags & WE_HAVE_A_TWO_BY_TWO) != 0)
                {
                    a = ReadF2Dot14(p);
                    b = ReadF2Dot14(p + 2);
                    c = ReadF2Dot14(p + 4);
                    d = ReadF2Dot14(p + 6);
                    p += 8;
                }

                // component point (x, y) -> (a*x + c*y, b*x + d*y) + offset
                Matrix2 componentMatrix = new Matrix2(a, c, b, d);
                Vector2 offset = new Vector2(dx, dy);
                if ((flags & SCALED_COMPONENT_OFFSET) != 0)
                {
                    offset = new Vector2(a * dx + c * dy, b * dx + d * dy);
                }

                // combine with parent transform: parent(component(x)) = P*C*x + P*offset + parentTranslation
                Matrix2 combined = Multiply(transform, componentMatrix);
                Vector2 combinedTranslation = new Vector2(
                    transform.M11 * offset.X + transform.M12 * offset.Y,
                    transform.M21 * offset.X + transform.M22 * offset.Y) + translation;

                AppendGlyphOutline(componentGlyph, combined, combinedTranslation, contours, depth + 1);
            } while ((flags & MORE_COMPONENTS) != 0);
        }

        private static Matrix2 Multiply(Matrix2 l, Matrix2 r)
        {
            return new Matrix2(
                l.M11 * r.M11 + l.M12 * r.M21, l.M11 * r.M12 + l.M12 * r.M22,
                l.M21 * r.M11 + l.M22 * r.M21, l.M21 * r.M12 + l.M22 * r.M22);
        }

        // ---------------------------------------------------------------- binary helpers

        private float ReadF2Dot14(int offset) => ReadI16(_data, offset) / 16384f;
        private static int ReadU16(byte[] d, int o) => (d[o] << 8) | d[o + 1];
        private static short ReadI16(byte[] d, int o) => (short)((d[o] << 8) | d[o + 1]);
        private static uint ReadU32(byte[] d, int o) => ((uint)d[o] << 24) | ((uint)d[o + 1] << 16) | ((uint)d[o + 2] << 8) | d[o + 3];
        private static string ReadTag(byte[] d, int o) => Encoding.ASCII.GetString(d, o, 4);

        public override string ToString() => $"{FamilyName} {SubfamilyName}";
    }
}
