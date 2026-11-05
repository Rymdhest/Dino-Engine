using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Output;
using OpenTK.Mathematics;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dino_SoundLab
{
    /// <summary>
    /// Plays through the real device, quietly, and listens to what the machine actually sends to it, by WASAPI loopback. Everything else here tests
    /// what the mixer makes; this tests that it arrives. Exit code 1 if it doesn't.
    /// </summary>
    internal static class DeviceCheck
    {
        private const float Volume = 0.06f;

        public static int Run(Options options)
        {
            float seconds = options.Float("seconds", 6f);
            var sent = new List<float>();
            var capture = new LoopbackCapture();
            capture.Start();

            Audio.Start();
            Audio.MasterVolume = Volume;
            var clock = Stopwatch.StartNew();
            while (!Audio.Ready && clock.Elapsed.TotalSeconds < 20) Thread.Sleep(10);
            Console.WriteLine($"Takes ready after {clock.Elapsed.TotalSeconds:0.00} s ({Audio.TakesMade} made)");

            Audio.Mixer!.Tap = (samples, frames) =>
            {
                lock (sent)
                {
                    for (int i = 0; i < frames; i++) sent.Add(samples[i * 2] + samples[i * 2 + 1]);
                }
            };

            Audio.SetListener(Vector3.Zero, Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY);
            var random = new SoundRandom(5);
            clock.Restart();
            var definitions = SoundCatalog.All.Where(d => d.Bus != SoundBus.Music).ToArray();
            double next = 0;
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                if (clock.Elapsed.TotalSeconds >= next)
                {
                    next += 0.12;
                    SoundDefinition definition = random.Pick(definitions);
                    Audio.Play(definition, new Vector3(random.Range(-10f, 10f), 0f, random.Range(-10f, 10f)));
                }
                Thread.Sleep(2);
            }
            Thread.Sleep(300);

            string? device = Audio.Output!.Device;
            int underruns = Audio.Output.Underruns;
            float latency = Audio.Output.Latency;
            float load = Audio.Mixer.Load;
            Audio.Stop();
            float[] heard = capture.Stop();

            Console.WriteLine($"Device: {device ?? "none"}");
            Console.WriteLine($"Queue {latency * 1000f:0.0} ms, {underruns} underruns, mixer load {load * 100f:0.00}%");
            Console.WriteLine($"Sent {sent.Count / (float)AudioFormat.SampleRate:0.00} s, heard {heard.Length / (float)capture.Rate:0.00} s at {capture.Rate} Hz, {capture.Channels} channels");

            if (device == null)
            {
                Console.WriteLine("FAIL: no device was opened.");
                return 1;
            }

            return Compare(sent.ToArray(), heard, capture.Rate) ? 0 : 1;
        }

        // Finds what was sent inside what was heard: the height of the match says it got there, and where it is says how late.
        private static bool Compare(float[] sent, float[] heard, int rate)
        {
            double energy = 0.0;
            foreach (float value in heard) energy += (double)value * value;
            float rms = MathF.Sqrt((float)(energy / Math.Max(heard.Length, 1)));
            Console.WriteLine($"Heard level {AudioFormat.GainToDecibels(rms):0.0} dBFS");
            if (rms < 1e-6f)
            {
                Console.WriteLine("FAIL: the device got silence.");
                return false;
            }

            // Bring both to one rate, then slide a loud stretch of what was sent along what was heard.
            float[] matched = rate == AudioFormat.SampleRate ? heard : Resample(heard, rate, AudioFormat.SampleRate);
            int window = AudioFormat.SampleRate;
            int start = Math.Min(sent.Length / 3, Math.Max(sent.Length - window, 0));
            int size = Fft.NextPowerOfTwo(window + matched.Length);
            var fft = new Fft(size);
            var ar = new float[size];
            var ai = new float[size];
            var br = new float[size];
            var bi = new float[size];
            Array.Copy(matched, ar, Math.Min(matched.Length, size));
            for (int i = 0; i < window && start + i < sent.Length; i++) br[i] = sent[start + i];
            fft.Transform(ar, ai);
            fft.Transform(br, bi);
            for (int i = 0; i < size; i++)
            {
                float real = ar[i] * br[i] + ai[i] * bi[i];
                ai[i] = ai[i] * br[i] - ar[i] * bi[i];
                ar[i] = real;
            }
            fft.Transform(ar, ai, inverse: true);

            int best = 0;
            float highest = 0f;
            for (int i = 0; i < matched.Length; i++)
            {
                if (MathF.Abs(ar[i]) <= highest) continue;
                highest = MathF.Abs(ar[i]);
                best = i;
            }

            double sentEnergy = 0.0;
            double heardEnergy = 0.0;
            for (int i = 0; i < window && start + i < sent.Length; i++)
            {
                sentEnergy += (double)sent[start + i] * sent[start + i];
                if (best + i < matched.Length) heardEnergy += (double)matched[best + i] * matched[best + i];
            }
            float match = highest / MathF.Sqrt((float)Math.Max(sentEnergy * heardEnergy, 1e-20));
            Console.WriteLine($"Match {match:0.000} at {best} samples into the capture");
            if (match < 0.5f)
            {
                Console.WriteLine("FAIL: what was heard isn't what was sent.");
                return false;
            }
            Console.WriteLine("OK: what the mixer sent reached the device.");
            return true;
        }

        private static float[] Resample(float[] samples, int from, int to)
        {
            var result = new float[(int)((long)samples.Length * to / from)];
            for (int i = 0; i < result.Length; i++)
            {
                double position = i * (double)from / to;
                int whole = (int)position;
                float fraction = (float)(position - whole);
                float a = samples[Math.Min(whole, samples.Length - 1)];
                float b = samples[Math.Min(whole + 1, samples.Length - 1)];
                result[i] = a + (b - a) * fraction;
            }
            return result;
        }
    }

    /// <summary>Records whatever the default output device is being sent, by anyone.</summary>
    internal sealed class LoopbackCapture
    {
        private readonly List<float> _samples = new();
        private Thread? _thread;
        private volatile bool _stopping;
        private volatile bool _running;
        private Exception? _failure;

        public int Rate { get; private set; }
        public int Channels { get; private set; }

        public void Start()
        {
            _thread = new Thread(Capture) { IsBackground = true, Name = "Loopback" };
            _thread.Start();
            while (!_running && _failure == null) Thread.Sleep(1);
            if (_failure != null) throw new InvalidOperationException("Loopback capture couldn't start.", _failure);
        }

        public float[] Stop()
        {
            _stopping = true;
            _thread?.Join(1000);
            return _samples.ToArray();
        }

        private void Capture()
        {
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorObject();
                Wasapi.Check(enumerator.GetDefaultAudioEndpoint(DataFlow.Render, DeviceRole.Console, out IMMDevice device), "default device");
                Guid clientId = Wasapi.AudioClient;
                Wasapi.Check(device.Activate(ref clientId, Wasapi.ClsCtxAll, IntPtr.Zero, out object clientObject), "activate");
                var client = (IAudioClient)clientObject;

                Wasapi.Check(client.GetMixFormat(out IntPtr formatPointer), "mix format");
                var mix = Marshal.PtrToStructure<WaveFormat>(formatPointer);
                Rate = (int)mix.SamplesPerSecond;
                Channels = mix.Channels;

                // Asking for float stereo at the device's own rate: the shared engine always mixes in float.
                WaveFormat format = mix;
                Wasapi.Check(InitializeFromPointer(client, formatPointer), "initialize");
                Guid captureId = Wasapi.CaptureClient;
                Wasapi.Check(client.GetService(ref captureId, out object captureObject), "capture client");
                var capture = (IAudioCaptureClient)captureObject;
                Wasapi.Check(client.Start(), "start");
                _running = true;

                while (!_stopping)
                {
                    Thread.Sleep(5);
                    capture.GetNextPacketSize(out uint packet);
                    while (packet > 0)
                    {
                        Wasapi.Check(capture.GetBuffer(out IntPtr data, out uint frames, out uint flags, out _, out _), "packet");
                        bool silent = (flags & 2) != 0;
                        var buffer = new float[frames * Channels];
                        if (!silent) Marshal.Copy(data, buffer, 0, buffer.Length);
                        for (int i = 0; i < frames; i++)
                        {
                            float sum = 0f;
                            for (int c = 0; c < Math.Min(Channels, 2); c++) sum += buffer[i * Channels + c];
                            _samples.Add(sum);
                        }
                        capture.ReleaseBuffer(frames);
                        capture.GetNextPacketSize(out packet);
                    }
                }
                client.Stop();
            }
            catch (Exception e)
            {
                _failure = e;
                _running = true;
                Console.WriteLine($"Loopback: {e.Message}");
            }
        }

        // The mix format is an extended structure, so it's passed on as it was given.
        private static int InitializeFromPointer(IAudioClient client, IntPtr format)
        {
            WaveFormat copy = Marshal.PtrToStructure<WaveFormat>(format);
            int size = 18 + copy.ExtraSize;
            byte[] raw = new byte[size];
            Marshal.Copy(format, raw, 0, size);
            return NativeInitialize(client, raw);
        }

        private static int NativeInitialize(IAudioClient client, byte[] raw)
        {
            // Plain float is accepted in place of the extended form the device reports.
            var plain = WaveFormat.Float((int)BitConverter.ToUInt32(raw, 4), BitConverter.ToUInt16(raw, 2));
            return client.Initialize(Wasapi.ShareModeShared, Wasapi.StreamFlagsLoopback, 2_000_000, 0, ref plain, IntPtr.Zero);
        }
    }
}
