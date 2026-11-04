using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Dino_Launcher.Update
{
    /// <summary>Thrown for a manifest that can't be believed or can't be used. The launcher carries on with what it has.</summary>
    public sealed class ManifestException : Exception
    {
        public ManifestException(string message) : base(message) { }
    }

    /// <summary>
    /// What a release is: every file of the game with its hash, which pack each comes in, and which launcher goes with it. Shared with
    /// Dino Release, which writes these, so the two can't disagree. Once a launcher is out there it reads these forever: <see cref="Schema"/>
    /// and <see cref="Launcher"/> must always mean what they mean now, so that an old launcher can at least replace itself.
    /// </summary>
    [DataContract]
    public sealed class Manifest
    {
        public const int CurrentSchema = 1;

        [DataMember(Name = "schema", Order = 0)] public int Schema = CurrentSchema;

        /// <summary>Only ever goes up, even to go back to an older build. A launcher ignores anything lower than what it has.</summary>
        [DataMember(Name = "sequence", Order = 1)] public long Sequence;

        [DataMember(Name = "version", Order = 2)] public string Version = "";
        [DataMember(Name = "commit", Order = 3)] public string Commit = "";
        [DataMember(Name = "published", Order = 4)] public string Published = "";
        [DataMember(Name = "notes", Order = 5)] public ReleaseNote[] Notes = new ReleaseNote[0];
        [DataMember(Name = "launcher", Order = 6)] public LauncherEntry Launcher = new LauncherEntry();

        /// <summary>Relative to the game's folder, like every path here.</summary>
        [DataMember(Name = "executable", Order = 7)] public string Executable = "";

        [DataMember(Name = "packs", Order = 8)] public PackEntry[] Packs = new PackEntry[0];
        [DataMember(Name = "files", Order = 9)] public FileEntry[] Files = new FileEntry[0];

        public static Manifest Parse(byte[] json)
        {
            try
            {
                using var stream = new MemoryStream(json);
                var manifest = (Manifest?)new DataContractJsonSerializer(typeof(Manifest)).ReadObject(stream);
                return manifest ?? throw new ManifestException("The manifest is empty.");
            }
            catch (SerializationException e)
            {
                throw new ManifestException($"The manifest can't be read: {e.Message}");
            }
        }

        public byte[] ToBytes()
        {
            using var stream = new MemoryStream();
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, new UTF8Encoding(false), false, true))
            {
                new DataContractJsonSerializer(typeof(Manifest)).WriteObject(writer, this);
            }
            return stream.ToArray();
        }

        /// <summary>Enough to replace the launcher with, which is all a manifest from a newer era has to offer.</summary>
        public void ValidateLauncher()
        {
            if (Sequence <= 0) throw new ManifestException("The manifest has no sequence.");
            if (string.IsNullOrEmpty(Version)) throw new ManifestException("The manifest has no version.");
            if (Launcher == null || !IsHash(Launcher.Sha256) || Launcher.Size <= 0 || string.IsNullOrEmpty(Launcher.Url))
            {
                throw new ManifestException("The manifest says nothing usable about the launcher.");
            }
        }

        /// <summary>A release is signed, so this isn't what keeps it honest. It keeps a mistake in one from reaching the disk.</summary>
        public void ValidateGame()
        {
            if (Schema > CurrentSchema) throw new ManifestException("This release needs a newer launcher.");
            if (Packs == null || Packs.Length == 0 || Files == null || Files.Length == 0) throw new ManifestException("The manifest lists no game.");

            var packs = new HashSet<string>();
            foreach (PackEntry pack in Packs)
            {
                if (pack == null || string.IsNullOrEmpty(pack.Name) || string.IsNullOrEmpty(pack.Url) || !IsHash(pack.Sha256) || pack.Size <= 0)
                {
                    throw new ManifestException("A pack in the manifest is incomplete.");
                }
                if (!packs.Add(pack.Name)) throw new ManifestException($"Pack {pack.Name} is listed twice.");
            }

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (FileEntry file in Files)
            {
                if (file == null || !IsSafePath(file.Path) || !IsHash(file.Sha256) || file.Size < 0 || file.Pack == null || !packs.Contains(file.Pack))
                {
                    throw new ManifestException($"A file in the manifest is unusable: {file?.Path}");
                }
                if (!paths.Add(file.Path)) throw new ManifestException($"{file.Path} is listed twice.");
            }

            if (string.IsNullOrEmpty(Executable) || !paths.Contains(Executable)) throw new ManifestException("The manifest doesn't say what to start.");
        }

        /// <summary>Stays inside the folder it's joined to, whatever Windows would make of it.</summary>
        public static bool IsSafePath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length > 200) return false;
            if (path.IndexOfAny(new[] { '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0) return false;

            foreach (string part in path.Split('/'))
            {
                if (part.Length == 0 || part == "." || part == "..") return false;
                if (part.EndsWith(".") || part.EndsWith(" ") || part.StartsWith(" ")) return false;
                foreach (char c in part)
                {
                    if (c < ' ') return false;
                }
            }
            return true;
        }

        private static bool IsHash(string text)
        {
            if (text == null || text.Length != 64) return false;
            foreach (char c in text)
            {
                if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) return false;
            }
            return true;
        }
    }

    [DataContract]
    public sealed class FileEntry
    {
        [DataMember(Name = "path", Order = 0)] public string Path = "";
        [DataMember(Name = "size", Order = 1)] public long Size;
        [DataMember(Name = "sha256", Order = 2)] public string Sha256 = "";
        [DataMember(Name = "pack", Order = 3)] public string Pack = "";
    }

    /// <summary>A zip of files that change together. Its address is relative to the feed.</summary>
    [DataContract]
    public sealed class PackEntry
    {
        [DataMember(Name = "name", Order = 0)] public string Name = "";
        [DataMember(Name = "url", Order = 1)] public string Url = "";
        [DataMember(Name = "size", Order = 2)] public long Size;
        [DataMember(Name = "sha256", Order = 3)] public string Sha256 = "";
    }

    [DataContract]
    public sealed class LauncherEntry
    {
        [DataMember(Name = "url", Order = 0)] public string Url = "";
        [DataMember(Name = "size", Order = 1)] public long Size;
        [DataMember(Name = "sha256", Order = 2)] public string Sha256 = "";
    }

    /// <summary>What changed in one release. A manifest carries the last few, for anyone who has been away.</summary>
    [DataContract]
    public sealed class ReleaseNote
    {
        [DataMember(Name = "version", Order = 0)] public string Version = "";
        [DataMember(Name = "text", Order = 1)] public string Text = "";
    }
}
