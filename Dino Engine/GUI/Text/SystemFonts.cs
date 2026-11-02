using System.Text;

namespace Dino_Engine.GUI.Text
{
    /// <summary>
    /// Finds and loads TrueType fonts that are already installed on the operating system,
    /// so the engine can render text without shipping any font assets.
    /// </summary>
    public static class SystemFonts
    {
        /// <summary>Proportional UI fonts in order of preference, covering Windows, Linux and macOS.</summary>
        public static readonly string[] DefaultSansFamilies =
        {
            "Segoe UI", "Arial", "Tahoma", "Verdana", "Helvetica", "DejaVu Sans", "Liberation Sans", "Noto Sans", "Ubuntu", "Cantarell",
        };

        /// <summary>Monospace fonts in order of preference.</summary>
        public static readonly string[] DefaultMonoFamilies =
        {
            "Consolas", "Cascadia Mono", "Lucida Console", "Courier New", "Menlo", "DejaVu Sans Mono", "Liberation Mono", "Noto Sans Mono", "Ubuntu Mono",
        };

        // Fast path: well known file names so we rarely need to scan the font directories.
        private static readonly Dictionary<string, string[]> KnownFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Segoe UI"] = new[] { "segoeui.ttf" },
            ["Segoe UI Bold"] = new[] { "segoeuib.ttf" },
            ["Arial"] = new[] { "arial.ttf", "Arial.ttf" },
            ["Arial Bold"] = new[] { "arialbd.ttf", "Arial Bold.ttf" },
            ["Tahoma"] = new[] { "tahoma.ttf", "Tahoma.ttf" },
            ["Verdana"] = new[] { "verdana.ttf", "Verdana.ttf" },
            ["Consolas"] = new[] { "consola.ttf" },
            ["Cascadia Mono"] = new[] { "CascadiaMono.ttf" },
            ["Lucida Console"] = new[] { "lucon.ttf" },
            ["Courier New"] = new[] { "cour.ttf", "Courier New.ttf" },
            ["DejaVu Sans"] = new[] { "DejaVuSans.ttf" },
            ["DejaVu Sans Mono"] = new[] { "DejaVuSansMono.ttf" },
            ["Liberation Sans"] = new[] { "LiberationSans-Regular.ttf" },
            ["Liberation Mono"] = new[] { "LiberationMono-Regular.ttf" },
            ["Noto Sans"] = new[] { "NotoSans-Regular.ttf" },
            ["Noto Sans Mono"] = new[] { "NotoSansMono-Regular.ttf" },
            ["Ubuntu"] = new[] { "Ubuntu-R.ttf", "Ubuntu-Regular.ttf" },
            ["Ubuntu Mono"] = new[] { "UbuntuMono-R.ttf", "UbuntuMono-Regular.ttf" },
        };

        private static readonly Dictionary<string, FontFace> LoadedFaces = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, (string path, int index)>? _scannedIndex;
        private static List<string>? _fontFiles;

        public static IEnumerable<string> FontDirectories()
        {
            var directories = new List<string>();
            if (OperatingSystem.IsWindows())
            {
                directories.Add(Environment.GetFolderPath(Environment.SpecialFolder.Fonts));
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                directories.Add(Path.Combine(localAppData, "Microsoft", "Windows", "Fonts"));
            }
            else if (OperatingSystem.IsMacOS())
            {
                directories.Add("/System/Library/Fonts");
                directories.Add("/System/Library/Fonts/Supplemental");
                directories.Add("/Library/Fonts");
                directories.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Fonts"));
            }
            else
            {
                directories.Add("/usr/share/fonts");
                directories.Add("/usr/local/share/fonts");
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                directories.Add(Path.Combine(home, ".fonts"));
                directories.Add(Path.Combine(home, ".local", "share", "fonts"));
            }
            return directories.Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d));
        }

        /// <summary>Loads the first family that is installed. Returns null if none of them are.</summary>
        public static FontFace? TryLoad(params string[] families)
        {
            foreach (string family in families)
            {
                FontFace? face = TryLoadFamily(family);
                if (face != null) return face;
            }
            return null;
        }

        /// <summary>Loads the first installed family, falling back to any usable TrueType font on the system.</summary>
        public static FontFace Load(params string[] families)
        {
            FontFace? face = TryLoad(families);
            if (face != null) return face;

            foreach (string file in GetFontFiles())
            {
                face = TryLoadFile(file, 0);
                if (face != null)
                {
                    Console.WriteLine($"GUI: none of [{string.Join(", ", families)}] found, falling back to {face} ({file})");
                    return face;
                }
            }
            throw new FileNotFoundException("GUI: no usable TrueType (.ttf/.ttc) font found in the system font directories: " + string.Join(", ", FontDirectories()));
        }

        public static FontFace LoadDefaultSans() => Load(DefaultSansFamilies);
        public static FontFace LoadDefaultMono() => Load(DefaultMonoFamilies.Concat(DefaultSansFamilies).ToArray());

        private static FontFace? TryLoadFamily(string family)
        {
            if (LoadedFaces.TryGetValue(family, out FontFace? cached)) return cached;

            FontFace? face = null;
            if (KnownFiles.TryGetValue(family, out string[]? fileNames))
            {
                foreach (string directory in FontDirectories())
                {
                    foreach (string fileName in fileNames)
                    {
                        string path = Path.Combine(directory, fileName);
                        if (File.Exists(path)) face = TryLoadFile(path, 0);
                        if (face != null) break;
                    }
                    if (face != null) break;
                }
            }

            if (face == null && GetScannedIndex().TryGetValue(family, out var location))
            {
                face = TryLoadFile(location.path, location.index);
            }

            if (face != null) LoadedFaces[family] = face;
            return face;
        }

        private static FontFace? TryLoadFile(string path, int faceIndex)
        {
            try
            {
                return FontFace.FromFile(path, faceIndex);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is InvalidDataException || exception is NotSupportedException || exception is IndexOutOfRangeException || exception is ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        private static List<string> GetFontFiles()
        {
            if (_fontFiles != null) return _fontFiles;
            _fontFiles = new List<string>();
            foreach (string directory in FontDirectories())
            {
                try
                {
                    foreach (string file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
                    {
                        string extension = Path.GetExtension(file).ToLowerInvariant();
                        if (extension == ".ttf" || extension == ".ttc") _fontFiles.Add(file);
                    }
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    // Unreadable directory, skip it.
                }
            }
            return _fontFiles;
        }

        /// <summary>
        /// Builds family name -> file lookup by reading only the 'name' table of each installed font.
        /// Regular styles win over other styles of the same family. Only done once, and only if the fast path misses.
        /// </summary>
        private static Dictionary<string, (string path, int index)> GetScannedIndex()
        {
            if (_scannedIndex != null) return _scannedIndex;
            _scannedIndex = new Dictionary<string, (string path, int index)>(StringComparer.OrdinalIgnoreCase);
            var isRegular = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (string file in GetFontFiles())
            {
                try
                {
                    using var stream = File.OpenRead(file);
                    foreach (var (index, family, subfamily) in ReadFaceNames(stream))
                    {
                        bool regular = subfamily.Equals("Regular", StringComparison.OrdinalIgnoreCase) || subfamily.Equals("Normal", StringComparison.OrdinalIgnoreCase) || subfamily.Equals("Roman", StringComparison.OrdinalIgnoreCase) || subfamily.Equals("Book", StringComparison.OrdinalIgnoreCase);
                        if (!_scannedIndex.ContainsKey(family) || (regular && !isRegular[family]))
                        {
                            _scannedIndex[family] = (file, index);
                            isRegular[family] = regular;
                        }
                        string fullName = family + " " + subfamily;
                        if (!_scannedIndex.ContainsKey(fullName)) _scannedIndex[fullName] = (file, index);
                    }
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is EndOfStreamException || exception is ArgumentException)
                {
                    // Not a readable font, skip it.
                }
            }
            return _scannedIndex;
        }

        private static IEnumerable<(int index, string family, string subfamily)> ReadFaceNames(Stream stream)
        {
            var results = new List<(int, string, string)>();
            var reader = new BinaryReader(stream);
            uint tag = ReadU32(reader, 0);
            var faceOffsets = new List<uint>();
            if (tag == 0x74746366) // 'ttcf'
            {
                uint numFonts = ReadU32(reader, 8);
                for (uint i = 0; i < Math.Min(numFonts, 64u); i++) faceOffsets.Add(ReadU32(reader, 12 + i * 4));
            }
            else if (tag == 0x00010000 || tag == 0x74727565)
            {
                faceOffsets.Add(0);
            }

            for (int faceIndex = 0; faceIndex < faceOffsets.Count; faceIndex++)
            {
                uint offset = faceOffsets[faceIndex];
                uint version = ReadU32(reader, offset);
                if (version != 0x00010000 && version != 0x74727565) continue;
                int numTables = ReadU16(reader, offset + 4);
                uint nameOffset = 0;
                bool hasGlyf = false;
                for (int t = 0; t < numTables; t++)
                {
                    uint record = offset + 12 + (uint)t * 16;
                    uint tableTag = ReadU32(reader, record);
                    if (tableTag == 0x6E616D65) nameOffset = ReadU32(reader, record + 8); // 'name'
                    if (tableTag == 0x676C7966) hasGlyf = true; // 'glyf'
                }
                if (nameOffset == 0 || !hasGlyf) continue;

                string? family = null, subfamily = null, typoFamily = null, typoSubfamily = null;
                int count = ReadU16(reader, nameOffset + 2);
                uint stringOffset = nameOffset + (uint)ReadU16(reader, nameOffset + 4);
                for (int i = 0; i < count; i++)
                {
                    uint record = nameOffset + 6 + (uint)i * 12;
                    int platform = ReadU16(reader, record);
                    int language = ReadU16(reader, record + 4);
                    int nameId = ReadU16(reader, record + 6);
                    int length = ReadU16(reader, record + 8);
                    uint stringStart = stringOffset + (uint)ReadU16(reader, record + 10);
                    if (platform != 3 || (language != 0x409 && language != 0)) continue;
                    if (nameId != 1 && nameId != 2 && nameId != 16 && nameId != 17) continue;
                    stream.Position = stringStart;
                    string value = Encoding.BigEndianUnicode.GetString(reader.ReadBytes(length));
                    switch (nameId)
                    {
                        case 1: family ??= value; break;
                        case 2: subfamily ??= value; break;
                        case 16: typoFamily ??= value; break;
                        case 17: typoSubfamily ??= value; break;
                    }
                }
                string? resolvedFamily = typoFamily ?? family;
                if (resolvedFamily != null) results.Add((faceIndex, resolvedFamily, typoSubfamily ?? subfamily ?? "Regular"));
            }
            return results;
        }

        private static uint ReadU32(BinaryReader reader, uint offset)
        {
            reader.BaseStream.Position = offset;
            byte[] bytes = reader.ReadBytes(4);
            if (bytes.Length < 4) throw new EndOfStreamException();
            return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        }

        private static int ReadU16(BinaryReader reader, uint offset)
        {
            reader.BaseStream.Position = offset;
            byte[] bytes = reader.ReadBytes(2);
            if (bytes.Length < 2) throw new EndOfStreamException();
            return (bytes[0] << 8) | bytes[1];
        }
    }
}
