using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;

namespace Dino_SoundLab
{
    /// <summary>Plain WAV files, in 32 bit float so nothing is lost on the way to being measured.</summary>
    internal static class WavFile
    {
        public static void Write(string path, SoundBuffer sound) => Write(path, sound.Left.AsSpan(0, sound.Length), sound.Right != null ? sound.Right.AsSpan(0, sound.Length) : default);

        public static void Write(string path, ReadOnlySpan<float> left, ReadOnlySpan<float> right)
        {
            int channels = right.IsEmpty ? 1 : 2;
            int bytes = left.Length * channels * 4;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write("RIFF"u8);
            writer.Write(36 + bytes);
            writer.Write("WAVEfmt "u8);
            writer.Write(16);
            writer.Write((short)3);
            writer.Write((short)channels);
            writer.Write(AudioFormat.SampleRate);
            writer.Write(AudioFormat.SampleRate * channels * 4);
            writer.Write((short)(channels * 4));
            writer.Write((short)32);
            writer.Write("data"u8);
            writer.Write(bytes);
            for (int i = 0; i < left.Length; i++)
            {
                writer.Write(left[i]);
                if (channels == 2) writer.Write(right[i]);
            }
        }
    }
}
