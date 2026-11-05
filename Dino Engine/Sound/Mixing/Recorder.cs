using Dino_Engine.Sound.Dsp;

namespace Dino_Engine.Sound.Mixing
{
    /// <summary>
    /// Writes what the mixer sends out to a WAV file, as it happens. The audio thread only drops samples into a ring; a thread of its own does the
    /// slow part. This is how a scripted run of the game can be listened to, or measured, afterwards.
    /// </summary>
    internal sealed class Recorder : IDisposable
    {
        private const int RingLength = 1 << 22;     // About 43 seconds of stereo: far more than the writer ever falls behind.

        private readonly float[] _ring = new float[RingLength];
        private readonly FileStream _file;
        private readonly Thread _writer;
        private readonly byte[] _bytes = new byte[1 << 16];
        private long _written;
        private long _read;
        private long _samples;
        private volatile bool _stopping;

        public Recorder(string path)
        {
            string? folder = Path.GetDirectoryName(Path.GetFullPath(path));
            if (folder != null) Directory.CreateDirectory(folder);
            _file = File.Create(path);
            WriteHeader(0);
            _writer = new Thread(Run) { Name = "Sound recorder", IsBackground = true };
            _writer.Start();
        }

        /// <summary>Audio thread.</summary>
        public void Write(float[] interleaved, int frames)
        {
            long position = _written;
            int count = frames * 2;
            for (int i = 0; i < count; i++) _ring[(position + i) & (RingLength - 1)] = interleaved[i];
            Volatile.Write(ref _written, position + count);
        }

        private void Run()
        {
            while (true)
            {
                long written = Volatile.Read(ref _written);
                while (_read < written)
                {
                    int count = (int)Math.Min(written - _read, _bytes.Length / 4);
                    for (int i = 0; i < count; i++)
                    {
                        BitConverter.TryWriteBytes(_bytes.AsSpan(i * 4), _ring[(_read + i) & (RingLength - 1)]);
                    }
                    _file.Write(_bytes, 0, count * 4);
                    _read += count;
                    _samples += count;
                }
                if (_stopping && _read == Volatile.Read(ref _written)) return;
                Thread.Sleep(20);
            }
        }

        private void WriteHeader(long samples)
        {
            long bytes = samples * 4;
            using var writer = new BinaryWriter(_file, System.Text.Encoding.ASCII, leaveOpen: true);
            _file.Position = 0;
            writer.Write("RIFF"u8);
            writer.Write((int)(36 + bytes));
            writer.Write("WAVEfmt "u8);
            writer.Write(16);
            writer.Write((short)3);
            writer.Write((short)2);
            writer.Write(AudioFormat.SampleRate);
            writer.Write(AudioFormat.SampleRate * 8);
            writer.Write((short)8);
            writer.Write((short)32);
            writer.Write("data"u8);
            writer.Write((int)bytes);
        }

        public void Dispose()
        {
            _stopping = true;
            _writer.Join(2000);
            WriteHeader(_samples);
            _file.Dispose();
        }
    }
}
