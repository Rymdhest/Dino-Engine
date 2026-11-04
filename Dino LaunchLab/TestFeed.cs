using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dino_Launcher.Update;

namespace Dino_LaunchLab
{
    /// <summary>
    /// A feed built in memory, with a key of its own, so that a test can publish things no honest release tool would make. Same format as
    /// the real thing, because it uses the same manifest and signature code.
    /// </summary>
    internal sealed class TestFeed : IDisposable
    {
        private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>();
        private long _sequence;

        public string PublicKey
        {
            get
            {
                ECParameters point = _key.ExportParameters(false);
                return Convert.ToBase64String(point.Q.X.Concat(point.Q.Y).ToArray());
            }
        }

        public byte[]? Get(string path) => _files.TryGetValue(path, out byte[]? bytes) ? bytes : null;

        public void Put(string path, byte[] bytes) => _files[path] = bytes;

        public static Dictionary<string, byte[]> Build(int seed, int runtimeBytes = 400000)
        {
            var game = new Dictionary<string, byte[]>();
            var random = new Random(seed);
            game["Dino Defenders.exe"] = Bytes(random, 60000);
            game["Dino Engine.dll"] = Bytes(random, 120000);
            game["Shaders/Terrain.frag"] = Encoding.UTF8.GetBytes($"// shader {seed}\n");
            game["Shaders/Grass/Grass.vert"] = Encoding.UTF8.GetBytes($"// grass {seed}\n");

            var stable = new Random(1);
            game["System.Private.CoreLib.dll"] = Bytes(stable, runtimeBytes);
            game["glfw3.dll"] = Bytes(stable, 50000);
            return game;
        }

        private static byte[] Bytes(Random random, int length)
        {
            var bytes = new byte[length];
            random.NextBytes(bytes);
            return bytes;
        }

        private static string PackOf(string path) => path.StartsWith("Shaders/") || path.StartsWith("Dino ") ? "game" : "runtime";

        /// <summary>Publishes a build as the latest release. <paramref name="tamper"/> gets a last look before it's signed.</summary>
        public Manifest Release(string version, Dictionary<string, byte[]> build, Action<Manifest>? tamper = null)
        {
            _sequence++;
            var manifest = new Manifest
            {
                Sequence = _sequence,
                Version = version,
                Commit = "lab",
                Published = "2026-01-01T00:00:00Z",
                Executable = "Dino Defenders.exe",
                Notes = new[] { new ReleaseNote { Version = version, Text = "Things changed." } },
                Launcher = new LauncherEntry { Url = $"download/v{version}/launcher.exe", Size = 3, Sha256 = Hashing.OfBytes(new byte[] { 1, 2, 3 }) },
                Files = build.OrderBy(f => f.Key, StringComparer.Ordinal)
                    .Select(f => new FileEntry { Path = f.Key, Size = f.Value.Length, Sha256 = Hashing.OfBytes(f.Value), Pack = PackOf(f.Key) }).ToArray(),
            };

            var packs = new List<PackEntry>();
            foreach (string name in new[] { "runtime", "game" })
            {
                byte[] zip = Zip(build.Where(f => PackOf(f.Key) == name));
                string url = $"download/{name}-{Hashing.OfBytes(zip).Substring(0, 8)}/{name}.zip";
                Put(url, zip);
                packs.Add(new PackEntry { Name = name, Url = url, Size = zip.Length, Sha256 = Hashing.OfBytes(zip) });
            }
            manifest.Packs = packs.ToArray();

            tamper?.Invoke(manifest);
            Publish(manifest);
            return manifest;
        }

        public void Publish(Manifest manifest)
        {
            byte[] payload = manifest.ToBytes();
            Put(Feed.ManifestPath, SignedFile.Wrap(payload, _key.SignData(payload, HashAlgorithmName.SHA256)));
        }

        public void PublishSignedBy(TestFeed other, Manifest manifest)
        {
            byte[] payload = manifest.ToBytes();
            Put(Feed.ManifestPath, SignedFile.Wrap(payload, other._key.SignData(payload, HashAlgorithmName.SHA256)));
        }

        public static byte[] Zip(IEnumerable<KeyValuePair<string, byte[]>> files)
        {
            using var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                foreach (KeyValuePair<string, byte[]> file in files)
                {
                    using Stream entry = zip.CreateEntry(file.Key).Open();
                    entry.Write(file.Value, 0, file.Value.Length);
                }
            }
            return stream.ToArray();
        }

        public void Dispose() => _key.Dispose();
    }
}
