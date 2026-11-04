using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Dino_Release
{
    /// <summary>
    /// The key every release is signed with. It lives outside the repository and never goes anywhere near GitHub, which is the point: taking
    /// over the account isn't enough to send testers anything. Lose it and existing launchers can't be updated, only replaced by hand.
    /// </summary>
    internal sealed class SigningKey : IDisposable
    {
        private const string FeedSource = "Dino Launcher/Update/Feed.cs";

        public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dino-release", "signing-key.pem");

        private readonly ECDsa _key;

        private SigningKey(ECDsa key)
        {
            _key = key;
        }

        /// <summary>The public half, as Feed.PublicKey has it.</summary>
        public string PublicKey => PublicKeyOf(_key);

        public byte[] Sign(byte[] payload) => _key.SignData(payload, HashAlgorithmName.SHA256);

        public void Dispose() => _key.Dispose();

        public static SigningKey Load(string path)
        {
            if (!File.Exists(path)) throw new ReleaseException($"There's no signing key at {path}. If this is a new machine, restore it from your backup. Otherwise run keygen.");

            var key = ECDsa.Create();
            key.ImportFromPem(File.ReadAllText(path));
            return new SigningKey(key);
        }

        /// <summary>Makes the key and writes the public half into the launcher's source. Will not replace a key that exists.</summary>
        public static void Generate(string path, string? feedSource)
        {
            if (File.Exists(path)) throw new ReleaseException($"{path} already exists. Replacing it would cut off every launcher already out there.");

            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, key.ExportECPrivateKeyPem());

            string source = feedSource ?? FeedSource;
            string text = File.ReadAllText(source);
            string updated = Regex.Replace(text, "PublicKey = \"[^\"]*\";", $"PublicKey = \"{PublicKeyOf(key)}\";");
            if (updated == text) throw new ReleaseException($"Couldn't find PublicKey in {source}.");
            File.WriteAllText(source, updated);

            Console.WriteLine($"Signing key written to {path}");
            Console.WriteLine($"Its public half is now in {source}.");
            Console.WriteLine("Back the key file up somewhere safe, like a password manager. It can't be made again.");
        }

        private static string PublicKeyOf(ECDsa key)
        {
            ECParameters parameters = key.ExportParameters(false);
            return Convert.ToBase64String(parameters.Q.X!.Concat(parameters.Q.Y!).ToArray());
        }
    }
}
