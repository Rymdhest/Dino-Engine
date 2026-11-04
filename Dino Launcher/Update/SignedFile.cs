using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Dino_Launcher.Update
{
    /// <summary>
    /// A manifest with its signature, in one file so that the two can't be caught apart mid-release. The first line is the signature, and
    /// everything after it is what was signed, byte for byte. The hashes inside then vouch for every pack and file, so this one check
    /// covers all of it: whoever gets hold of the host can't send testers anything, as they don't have the key.
    /// </summary>
    public static class SignedFile
    {
        private const string Header = "dino-signed-1 ";

        public static byte[] Wrap(byte[] payload, byte[] signature)
        {
            byte[] line = Encoding.ASCII.GetBytes(Header + Convert.ToBase64String(signature) + "\n");
            var file = new byte[line.Length + payload.Length];
            Buffer.BlockCopy(line, 0, file, 0, line.Length);
            Buffer.BlockCopy(payload, 0, file, line.Length, payload.Length);
            return file;
        }

        /// <summary>Gives what was signed, or throws. Nothing in the file is looked at until the signature has checked out.</summary>
        public static byte[] Open(byte[] file, string publicKey)
        {
            int lineEnd = Array.IndexOf(file, (byte)'\n');
            if (lineEnd < 0 || lineEnd > 512) throw new ManifestException("That isn't a signed manifest.");

            string line = Encoding.ASCII.GetString(file, 0, lineEnd);
            if (!line.StartsWith(Header, StringComparison.Ordinal)) throw new ManifestException("That isn't a signed manifest.");

            byte[] signature;
            try
            {
                signature = Convert.FromBase64String(line.Substring(Header.Length));
            }
            catch (FormatException)
            {
                throw new ManifestException("The manifest's signature is damaged.");
            }

            var payload = new byte[file.Length - lineEnd - 1];
            Buffer.BlockCopy(file, lineEnd + 1, payload, 0, payload.Length);

            using ECDsa key = PublicKeyFrom(publicKey);
            if (!key.VerifyData(payload, signature, HashAlgorithmName.SHA256)) throw new ManifestException("The manifest's signature is wrong.");
            return payload;
        }

        private static ECDsa PublicKeyFrom(string publicKey)
        {
            byte[] point;
            try
            {
                point = Convert.FromBase64String(publicKey);
            }
            catch (FormatException)
            {
                throw new ManifestException("This launcher has no usable key to check releases with.");
            }
            if (point.Length != 64) throw new ManifestException("This launcher has no usable key to check releases with.");

            var x = new byte[32];
            var y = new byte[32];
            Buffer.BlockCopy(point, 0, x, 0, 32);
            Buffer.BlockCopy(point, 32, y, 0, 32);
            return ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = x, Y = y } });
        }
    }

    public static class Hashing
    {
        public static string OfFile(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 20, FileOptions.SequentialScan);
            using SHA256 sha = Create();
            return Hex(sha.ComputeHash(stream));
        }

        public static string OfBytes(byte[] bytes)
        {
            using SHA256 sha = Create();
            return Hex(sha.ComputeHash(bytes));
        }

        public static string Hex(byte[] bytes)
        {
            var text = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) text.Append(b.ToString("x2"));
            return text.ToString();
        }

        // The whole game is hashed every time the launcher opens, and the default on .NET Framework is the slow managed one.
#if NETFRAMEWORK
        private static SHA256 Create() => new SHA256Cng();
#else
        private static SHA256 Create() => SHA256.Create();
#endif
    }
}
