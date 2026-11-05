using Dino_Engine.Sound.Dsp;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dino_Engine.Sound.Output
{
    /// <summary>Asked for sound by whatever plays it: fills a buffer of interleaved stereo float frames.</summary>
    public delegate void AudioSource(float[] interleaved, int frames);

    /// <summary>
    /// Sends sound to the machine's default device through WASAPI, on a thread of its own that nothing else may hold up. It keeps going
    /// whatever happens to the hardware: with headphones pulled out, the device gone or Windows audio restarting, it follows
    /// the new default, or if there is none it keeps time on its own, so the mixer goes on running and sound returns by itself. A game with
    /// no sound card plays exactly as one with, only silently.
    /// </summary>
    public sealed class AudioOutput : IDisposable
    {
        private const int Channels = 2;
        private const long BufferDuration = 1_000_000;      // 100 ms, in 100 ns units. Room to grow into, not latency: see TargetFrames.
        private const float StartingLatency = 0.022f;       // Seconds kept queued. Long enough to ride out a garbage collection.
        private const float MostLatency = 0.08f;
        private const int StepFrames = 240;
        private const double DeviceCheckInterval = 2.0;
        private const double RecoveryInterval = 6.0;        // Seconds of unbroken sound before the queue is shortened a little again.

        private readonly AudioSource _source;
        private readonly float[] _block = new float[8192 * Channels];
        private readonly Thread _thread;
        private volatile bool _stopping;
        private string? _deviceId;
        private readonly int _leastFrames = (int)(StartingLatency * AudioFormat.SampleRate);
        private int _targetFrames = (int)(StartingLatency * AudioFormat.SampleRate);

        /// <summary>Times the device ran out of sound. Each one is an audible gap, and makes the queue longer.</summary>
        public int Underruns { get; private set; }

        /// <summary>Seconds of sound queued, which is how late anything played now is heard.</summary>
        public float Latency => _targetFrames / (float)AudioFormat.SampleRate;

        /// <summary>Times the mixer has thrown. It should stay at nothing, but if it doesn't, the game goes on.</summary>
        public int Faults { get; private set; }

        /// <summary>What's playing it, for showing to a person, or null with nothing to play on.</summary>
        public string? Device { get; private set; }

        /// <summary>When set, the device is left alone, though everything else runs: for when sound is off, and for tests.</summary>
        public volatile bool Silent;

        public AudioOutput(AudioSource source, bool silent = false)
        {
            _source = source;
            Silent = silent;
            _thread = new Thread(Run)
            {
                Name = "Audio",
                IsBackground = true,
                Priority = ThreadPriority.Highest,
            };
            _thread.Start();
        }

        private void Run()
        {
            // Asks Windows to treat this thread as audio, which is what keeps it running on time under load.
            IntPtr task = IntPtr.Zero;
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    uint index = 0;
                    task = Wasapi.AvSetMmThreadCharacteristics("Pro Audio", ref index);
                }
            }
            catch (DllNotFoundException)
            {
            }

            while (!_stopping)
            {
                bool played = false;
                try
                {
                    if (OperatingSystem.IsWindows() && !Silent) played = PlayOnDefaultDevice();
                }
                catch (Exception e) when (e is COMException or InvalidCastException or DllNotFoundException or EntryPointNotFoundException)
                {
                    Console.WriteLine($"Audio device lost: {e.Message}");
                }

                Device = null;
                if (!played && !_stopping) KeepTime(0.5);
            }

            if (task != IntPtr.Zero) Wasapi.AvRevertMmThreadCharacteristics(task);
        }

        // With nothing to play on, sounds still have to end and queues still have to empty.
        private void KeepTime(double seconds)
        {
            var clock = Stopwatch.StartNew();
            long rendered = 0;
            while (!_stopping && clock.Elapsed.TotalSeconds < seconds)
            {
                long due = (long)(clock.Elapsed.TotalSeconds * AudioFormat.SampleRate);
                while (rendered < due)
                {
                    int frames = (int)Math.Min(due - rendered, StepFrames);
                    Render(frames);
                    rendered += frames;
                }
                Thread.Sleep(5);
            }
        }

        // A fault in a sound costs that moment of sound, not the game: an exception left to run off the end of this thread would end the process.
        private void Render(int frames)
        {
            try
            {
                _source(_block, frames);
            }
            catch (Exception e)
            {
                Array.Clear(_block, 0, frames * Channels);
                if (Faults++ == 0) Console.WriteLine($"Sound fault: {e}");
            }
        }

        /// <summary>Plays until the device goes away or changes. Returns whether it got as far as playing.</summary>
        private bool PlayOnDefaultDevice()
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorObject();
            try
            {
                int result = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, DeviceRole.Console, out IMMDevice device);
                if (result == Wasapi.ElementNotFound) return false;
                Wasapi.Check(result, "Finding the audio device");
                device.GetId(out string id);
                _deviceId = id;

                Guid clientId = Wasapi.AudioClient;
                Wasapi.Check(device.Activate(ref clientId, Wasapi.ClsCtxAll, IntPtr.Zero, out object clientObject), "Opening the audio device");
                var client = (IAudioClient)clientObject;

                // Windows converts from this to whatever the device runs at, in rate and in channels, so a 7.1 card at 96 kHz needs nothing from us.
                WaveFormat format = WaveFormat.Float(AudioFormat.SampleRate, Channels);
                uint flags = Wasapi.StreamFlagsEventCallback | Wasapi.StreamFlagsAutoConvertPcm | Wasapi.StreamFlagsSrcDefaultQuality;
                Wasapi.Check(client.Initialize(Wasapi.ShareModeShared, flags, BufferDuration, 0, ref format, IntPtr.Zero), "Starting the audio stream");
                Wasapi.Check(client.GetBufferSize(out uint bufferFrames), "Sizing the audio buffer");

                using var ready = new AutoResetEvent(false);
                SafeWaitHandle handle = ready.SafeWaitHandle;
                Wasapi.Check(client.SetEventHandle(handle.DangerousGetHandle()), "Setting the audio event");

                Guid renderId = Wasapi.RenderClient;
                Wasapi.Check(client.GetService(ref renderId, out object renderObject), "Getting the audio renderer");
                var render = (IAudioRenderClient)renderObject;

                Device = id;
                Console.WriteLine($"Sound on {id}, {bufferFrames} frames of buffer.");

                Fill(client, render, bufferFrames);
                Wasapi.Check(client.Start(), "Starting the audio device");
                try
                {
                    Pump(enumerator, client, render, ready, bufferFrames);
                }
                finally
                {
                    client.Stop();
                    Marshal.ReleaseComObject(render);
                    Marshal.ReleaseComObject(client);
                    Marshal.ReleaseComObject(device);
                }
                return true;
            }
            finally
            {
                Marshal.ReleaseComObject(enumerator);
            }
        }

        private void Pump(IMMDeviceEnumerator enumerator, IAudioClient client, IAudioRenderClient render, AutoResetEvent ready, uint bufferFrames)
        {
            var sinceCheck = Stopwatch.StartNew();
            var sinceGap = Stopwatch.StartNew();
            while (!_stopping && !Silent)
            {
                // A wait with a limit, so a stalled device can't stall the thread.
                ready.WaitOne(100);

                int padding = Fill(client, render, bufferFrames);
                if (padding < 0) return;

                // Ran dry: the sound had a gap. Queue a little more from now on, so that it doesn't happen again for the same reason.
                if (padding == 0)
                {
                    Underruns++;
                    _targetFrames = Math.Min(_targetFrames + AudioFormat.SampleRate / 200, (int)(MostLatency * AudioFormat.SampleRate));
                    sinceGap.Restart();
                }

                // Whatever caused it has likely passed. A hiccup while a world loads shouldn't leave every sound late for the rest of the session.
                if (sinceGap.Elapsed.TotalSeconds > RecoveryInterval && _targetFrames > _leastFrames)
                {
                    _targetFrames = Math.Max(_targetFrames - AudioFormat.SampleRate / 400, _leastFrames);
                    sinceGap.Restart();
                }

                if (sinceCheck.Elapsed.TotalSeconds < DeviceCheckInterval) continue;
                sinceCheck.Restart();
                if (DefaultDeviceChanged(enumerator)) return;
            }
        }

        private bool DefaultDeviceChanged(IMMDeviceEnumerator enumerator)
        {
            int result = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, DeviceRole.Console, out IMMDevice device);
            if (result < 0) return true;
            device.GetId(out string id);
            Marshal.ReleaseComObject(device);
            return id != _deviceId;
        }

        // Tops the device's queue up to the target. Returns what was in it on arrival, or -1 if the device has gone.
        private int Fill(IAudioClient client, IAudioRenderClient render, uint bufferFrames)
        {
            int result = client.GetCurrentPadding(out uint padding);
            if (Gone(result)) return -1;
            Wasapi.Check(result, "Reading the audio queue");

            int wanted = Math.Min(_targetFrames, (int)bufferFrames) - (int)padding;
            while (wanted > 0)
            {
                int frames = Math.Min(wanted, _block.Length / Channels);
                result = render.GetBuffer((uint)frames, out IntPtr data);
                if (Gone(result)) return -1;
                Wasapi.Check(result, "Getting the audio buffer");

                Render(frames);
                Marshal.Copy(_block, 0, data, frames * Channels);

                result = render.ReleaseBuffer((uint)frames, 0);
                if (Gone(result)) return -1;
                Wasapi.Check(result, "Releasing the audio buffer");
                wanted -= frames;
            }
            return (int)padding;
        }

        private static bool Gone(int result)
        {
            return result is Wasapi.DeviceInvalidated or Wasapi.ResourcesInvalidated or Wasapi.ServiceNotRunning;
        }

        public void Dispose()
        {
            _stopping = true;
            _thread.Join(1000);
        }
    }
}
