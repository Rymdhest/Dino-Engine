using Dino_Engine.Sound.Content;
using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace Dino_Engine.Sound.Mixing
{
    internal enum CommandKind
    {
        Play,
        Start,
        Stop,
        Move,
        SetParameter,
        SetGain,
        Listener,
        BusVolume,
        Environment,
        Muffle,
        StopAll,
    }

    internal struct AudioCommand
    {
        public CommandKind Kind;
        public int Voice;
        public object? Payload;         // A SoundPool, or a live sound with its definition.
        public object? Extra;
        public Vector3 A, B, C, D, E;
        public float X, Y, Z;
        public bool Flag;
        public ReverbSettings Reverb;
    }

    /// <summary>
    /// Where everything meets, on the audio thread. Each block it does what the game asked, works out where every voice is relative to the
    /// listener, plays them into their buses, adds the reverb and hands the result to the limiter. Nothing here allocates, locks or
    /// waits: all the game can do is leave a note in the queue, read at the start of the next block.
    /// </summary>
    public sealed class Mixer
    {
        public const int MostVoices = 96;
        private const int Block = 128;
        private const int QueueLength = 4096;
        private const float SpeedOfSound = 343f;
        private const float FocusRadius = 4.5f;             // Metres. Closer than this a sound pulls in towards the middle, so a hero's own feet aren't in one ear.
        private const float WidestPan = 0.92f;
        private const float WindowRelease = 9f;             // Decibels a second the mix comes back up after something loud.
        private const float AmbienceYield = 7f;             // Decibels the ambience gives way to a fight at its height.
        private const float MaxAirCutoff = 19000f;
        private const float MaxInterauralDelay = 0.00032f;  // Seconds. A real head gives 0.65 ms, but on speakers much of that only smears.
        private const float MuffleCutoff = 900f;

        private static readonly int BusCount = Enum.GetValues<SoundBus>().Length;

        private readonly AudioCommand[] _queue = new AudioCommand[QueueLength];
        private int _queueHead;     // Next to read: audio thread only.
        private int _queueTail;     // Next to write: game thread only, under _writeLock.
        private readonly object _writeLock = new();
        private int _nextVoiceId = 1;

        private readonly Voice[] _voices = new Voice[MostVoices];
        private readonly float[][] _busLeft;
        private readonly float[][] _busRight;
        private readonly float[] _busVolumes;
        private readonly float[] _busGains;
        private readonly float[] _busLevels;
        private readonly float[] _sendLeft = new float[Block];
        private readonly float[] _sendRight = new float[Block];
        private readonly float[] _sourceLeft = new float[Block * 2];
        private readonly float[] _sourceRight = new float[Block * 2];
        private readonly float[] _mixLeft = new float[Block];
        private readonly float[] _mixRight = new float[Block];
        private readonly Reverb _reverb = new(7);
        private readonly Limiter _limiter = new(-1f, 0.002f, 0.14f);
        private readonly SoundRandom _random = new((ulong)Environment.TickCount64);
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private Vector3 _listener, _right = Vector3.UnitX, _forward = -Vector3.UnitZ, _up = Vector3.UnitY, _listenerVelocity;
        private ReverbSettings _environment = new() { DecayTime = 0.8f, DecayHigh = 0.3f, PreDelay = 0.012f, Wet = 0.5f, EchoTime = 0.3f, EchoBrightness = 2500f };
        private ReverbSettings _environmentTarget;
        private int _environmentCountdown;
        private float _window;
        private OnePole _combatLevel;
        private float _muffleTarget;
        private float _muffle;
        private OnePole _muffleLeft, _muffleRight;
        private long _time;
        private bool _headphones;

        public float MasterVolume = 1f;

        /// <summary>Whether the listener wears headphones: the ears then get what a real head gives them.</summary>
        public bool Headphones { get => _headphones; set => _headphones = value; }

        // --- What can be read from outside. A moment stale, which is what a meter is. ---

        public int ActiveVoices { get; private set; }
        public int MostActiveVoices { get; private set; }
        public int Dropped { get; private set; }
        public float PeakLeft { get; private set; }
        public float PeakRight { get; private set; }
        public float WindowDecibels => _window;
        public float Reduction { get; private set; } = 1f;
        public float Load { get; private set; }             // Share of real time spent mixing.
        public long FramesMixed => _time;
        public float BusLevel(SoundBus bus) => _busLevels[(int)bus];

        /// <summary>Set to get a copy of what's sent out, after the limiter.</summary>
        public Action<float[], int>? Tap;

        public Mixer()
        {
            for (int i = 0; i < MostVoices; i++) _voices[i] = new Voice();

            _busLeft = new float[BusCount][];
            _busRight = new float[BusCount][];
            _busVolumes = new float[BusCount];
            _busGains = new float[BusCount];
            _busLevels = new float[BusCount];
            for (int bus = 0; bus < BusCount; bus++)
            {
                _busLeft[bus] = new float[Block];
                _busRight[bus] = new float[Block];
                _busVolumes[bus] = 1f;
                _busGains[bus] = 1f;
            }

            _environmentTarget = _environment;
            _reverb.Configure(_environment);
            _combatLevel.SetTime(0.25f, AudioFormat.SampleRate / (float)Block);
        }

        // --- Asking. Any thread. ---

        internal int Reserve()
        {
            return Interlocked.Increment(ref _nextVoiceId);
        }

        internal void Post(in AudioCommand command)
        {
            lock (_writeLock)
            {
                int next = (_queueTail + 1) % QueueLength;

                // A full queue means the audio thread has stopped. Better to lose a sound than to wait for it.
                if (next == Volatile.Read(ref _queueHead)) return;
                _queue[_queueTail] = command;
                Volatile.Write(ref _queueTail, next);
            }
        }

        // --- Rendering. Audio thread. ---

        /// <summary>Fills interleaved stereo. Any number of frames can be asked for.</summary>
        public void Render(float[] output, int frames)
        {
            long began = Stopwatch.GetTimestamp();
            int done = 0;
            while (done < frames)
            {
                int count = Math.Min(Block, frames - done);
                ReadQueue();
                MixBlock(count);
                _limiter.Process(_mixLeft.AsSpan(0, count), _mixRight.AsSpan(0, count));

                for (int i = 0; i < count; i++)
                {
                    float left = _mixLeft[i];
                    float right = _mixRight[i];
                    output[(done + i) * 2] = left;
                    output[(done + i) * 2 + 1] = right;
                    PeakLeft = MathF.Max(PeakLeft * 0.99998f, MathF.Abs(left));
                    PeakRight = MathF.Max(PeakRight * 0.99998f, MathF.Abs(right));
                }
                done += count;
                _time += count;
            }

            Tap?.Invoke(output, frames);
            Reduction = _limiter.Reduction;
            _limiter.Reduction = 1f;

            double spent = (Stopwatch.GetTimestamp() - began) / (double)Stopwatch.Frequency;
            Load += ((float)(spent / (frames / (double)AudioFormat.SampleRate)) - Load) * 0.02f;
        }

        private void ReadQueue()
        {
            int tail = Volatile.Read(ref _queueTail);
            while (_queueHead != tail)
            {
                ref AudioCommand command = ref _queue[_queueHead];
                Execute(ref command);
                command.Payload = null;
                command.Extra = null;
                Volatile.Write(ref _queueHead, (_queueHead + 1) % QueueLength);
            }
        }

        private void Execute(ref AudioCommand command)
        {
            switch (command.Kind)
            {
                case CommandKind.Play:
                    StartOneShot(ref command);
                    break;
                case CommandKind.Start:
                    StartLive(ref command);
                    break;
                case CommandKind.Stop:
                    if (Find(command.Voice) is Voice stopping) Stop(stopping, command.X);
                    break;
                case CommandKind.Move:
                    if (Find(command.Voice) is Voice moving)
                    {
                        moving.Location = command.A;
                        moving.Velocity = command.B;
                    }
                    break;
                case CommandKind.SetParameter:
                    if (Find(command.Voice) is Voice live) live.LiveSource?.SetTarget((int)command.X, command.Y);
                    break;
                case CommandKind.SetGain:
                    if (Find(command.Voice) is Voice loud) loud.Gain = command.X;
                    break;
                case CommandKind.Listener:
                    _listener = command.A;
                    _right = command.B;
                    _forward = command.C;
                    _up = command.D;
                    _listenerVelocity = command.E;
                    break;
                case CommandKind.BusVolume:
                    _busVolumes[command.Voice] = command.X;
                    break;
                case CommandKind.Environment:
                    _environmentTarget = command.Reverb;
                    break;
                case CommandKind.Muffle:
                    _muffleTarget = command.X;
                    break;
                case CommandKind.StopAll:
                    foreach (Voice voice in _voices)
                    {
                        if (voice.Active && voice.Bus is not (SoundBus.Interface or SoundBus.Music)) Stop(voice, command.X);
                    }
                    break;
            }
        }

        private Voice? Find(int id)
        {
            foreach (Voice voice in _voices)
            {
                if (voice.Active && voice.Id == id) return voice;
            }
            return null;
        }

        private void Stop(Voice voice, float seconds)
        {
            voice.Stopping = true;
            voice.FadeStep = 1f / MathF.Max(seconds * AudioFormat.SampleRate, 1f);
        }

        private void StartOneShot(ref AudioCommand command)
        {
            var pool = (SoundPool)command.Payload!;
            SoundDefinition definition = pool.Definition;

            // Manners first: not too soon after the last, and not too many at once.
            if ((_time - pool.LastStart) / (float)AudioFormat.SampleRate < definition.Spacing) return;
            if (pool.Playing >= definition.MostAtOnce)
            {
                Voice? oldest = null;
                foreach (Voice voice in _voices)
                {
                    if (voice.Active && voice.Pool == pool && !voice.Stopping && (oldest == null || voice.Started < oldest.Started)) oldest = voice;
                }
                if (oldest != null) Stop(oldest, 0.02f);
            }

            bool positional = definition.Positional && command.Flag;
            if (positional)
            {
                // Out of earshot now and over before it could matter: nothing to do.
                float distance = (command.A - _listener).Length;
                if (distance > definition.FarDistance) return;
            }

            SoundBuffer? take = pool.Next(_random);
            if (take == null) return;

            Voice? free = Claim(definition.Priority);
            if (free == null)
            {
                Dropped++;
                return;
            }

            free.Reset();
            free.Active = true;
            free.Id = command.Voice;
            free.Pool = pool;
            free.Buffer = take;
            free.Bus = definition.Bus;
            free.Priority = definition.Priority;
            free.Presence = definition.Presence;
            free.Positional = positional;
            free.NearDistance = definition.NearDistance;
            free.FarDistance = definition.FarDistance;
            free.ReverbSend = definition.ReverbSend;
            free.Doppler = definition.Doppler;
            free.Location = command.A;
            free.Velocity = command.B;
            free.Started = _time;

            // Each playing varies a little on top of each take being different.
            float pitch = definition.PitchSpread > 0f ? _random.Range(-definition.PitchSpread, definition.PitchSpread) : 0f;
            float gain = definition.GainSpread > 0f ? _random.Range(-definition.GainSpread, definition.GainSpread) : 0f;
            free.Rate = command.Y * AudioFormat.Semitones(pitch);
            free.Gain = command.X * AudioFormat.DecibelsToGain(gain);

            pool.LastStart = _time;
            pool.Playing++;
        }

        private void StartLive(ref AudioCommand command)
        {
            var definition = (LiveDefinition)command.Extra!;
            Voice? free = Claim(definition.Priority);
            if (free == null)
            {
                Dropped++;
                return;
            }

            free.Reset();
            free.Active = true;
            free.Id = command.Voice;
            free.Live = definition;
            free.LiveSource = (LiveSound)command.Payload!;
            free.Bus = definition.Bus;
            free.Priority = definition.Priority;
            free.Presence = 0f;
            free.Positional = definition.Positional && command.Flag;
            free.NearDistance = definition.NearDistance;
            free.FarDistance = definition.FarDistance;
            free.ReverbSend = definition.ReverbSend;
            free.Location = command.A;
            free.Velocity = Vector3.Zero;
            free.Started = _time;
            free.Gain = command.X * definition.Gain;
        }

        // A free voice, or the least important one playing, if it matters less than what's asking.
        private Voice? Claim(int priority)
        {
            Voice? least = null;
            foreach (Voice voice in _voices)
            {
                if (!voice.Active) return voice;
                if (voice.LiveSource != null) continue;
                if (least == null || voice.Priority < least.Priority || (voice.Priority == least.Priority && voice.Started < least.Started)) least = voice;
            }
            if (least == null || least.Priority > priority) return null;
            Finish(least);
            return least;
        }

        private void Finish(Voice voice)
        {
            if (voice.Pool != null) voice.Pool.Playing--;
            voice.Active = false;
            voice.Buffer = null;
            voice.LiveSource = null;
        }

        private void MixBlock(int count)
        {
            for (int bus = 0; bus < BusCount; bus++)
            {
                Array.Clear(_busLeft[bus], 0, count);
                Array.Clear(_busRight[bus], 0, count);
            }
            Array.Clear(_sendLeft, 0, count);
            Array.Clear(_sendRight, 0, count);

            float blockSeconds = count / (float)AudioFormat.SampleRate;

            // The loudest thing happening sets the top of the mix, and everything else sits under it, as an ear adapts: an idea from
            // DICE's HDR audio. Loud moments stay loud without anything going over, and quiet things duck out of their way.
            float top = 0f;
            int active = 0;
            foreach (Voice voice in _voices)
            {
                if (!voice.Active) continue;
                active++;
                float heard = voice.Presence;
                if (voice.Positional && heard > 0f) heard += AudioFormat.GainToDecibels(Attenuation(voice, (voice.Location - _listener).Length));
                top = MathF.Max(top, heard);
            }
            _window = top >= _window ? top : MathF.Max(_window - WindowRelease * blockSeconds, top);
            ActiveVoices = active;
            MostActiveVoices = Math.Max(MostActiveVoices, active);

            foreach (Voice voice in _voices)
            {
                if (voice.Active) RenderVoice(voice, count);
            }

            UpdateBusGains(count);

            Array.Clear(_mixLeft, 0, count);
            Array.Clear(_mixRight, 0, count);
            for (int bus = 0; bus < BusCount; bus++)
            {
                float[] left = _busLeft[bus];
                float[] right = _busRight[bus];
                float gain = _busGains[bus];
                float level = 0f;
                for (int i = 0; i < count; i++)
                {
                    float l = left[i] * gain;
                    float r = right[i] * gain;
                    _mixLeft[i] += l;
                    _mixRight[i] += r;
                    level = MathF.Max(level, MathF.Max(MathF.Abs(l), MathF.Abs(r)));
                }
                _busLevels[bus] = MathF.Max(level, _busLevels[bus] * 0.97f);
            }

            UpdateEnvironment();
            _reverb.Process(_sendLeft.AsSpan(0, count), _sendRight.AsSpan(0, count), _mixLeft.AsSpan(0, count), _mixRight.AsSpan(0, count));

            ApplyMuffle(count);

            float master = MasterVolume;
            for (int i = 0; i < count; i++)
            {
                _mixLeft[i] *= master;
                _mixRight[i] *= master;
            }
        }

        // Going to a menu the world steps back, behind glass. The interface is above that, on its own.
        private void ApplyMuffle(int count)
        {
            _muffle += (_muffleTarget - _muffle) * 0.02f;
            if (_muffle < 0.001f) return;

            float cutoff = MaxAirCutoff * MathF.Pow(MuffleCutoff / MaxAirCutoff, _muffle);
            _muffleLeft.SetCutoff(cutoff);
            _muffleRight.SetCutoff(cutoff);
            float[] interfaceLeft = _busLeft[(int)SoundBus.Interface];
            float[] interfaceRight = _busRight[(int)SoundBus.Interface];
            float gain = _busGains[(int)SoundBus.Interface];
            float world = 1f - 0.35f * _muffle;
            for (int i = 0; i < count; i++)
            {
                float uiLeft = interfaceLeft[i] * gain;
                float uiRight = interfaceRight[i] * gain;
                _mixLeft[i] = _muffleLeft.Lowpass(_mixLeft[i] - uiLeft) * world + uiLeft;
                _mixRight[i] = _muffleRight.Lowpass(_mixRight[i] - uiRight) * world + uiRight;
            }
        }

        private void UpdateEnvironment()
        {
            if (--_environmentCountdown > 0) return;
            _environmentCountdown = 24;
            _environment = ReverbSettings.Lerp(_environment, _environmentTarget, 0.06f);
            _reverb.Configure(_environment);
        }

        private void UpdateBusGains(int count)
        {
            // Ambience gives way to a fight, not all at once. It's what lets a quiet wood sound quiet and a battle sound like one.
            float combat = _busLevels[(int)SoundBus.Combat] + _busLevels[(int)SoundBus.Voice];
            float busy = _combatLevel.Lowpass(MathF.Min(combat * 2.5f, 1f));

            for (int bus = 0; bus < BusCount; bus++)
            {
                float gain = _busVolumes[bus];
                if (bus == (int)SoundBus.Ambience) gain *= AudioFormat.DecibelsToGain(-AmbienceYield * busy);
                _busGains[bus] = gain;
            }
        }

        private void RenderVoice(Voice voice, int count)
        {
            // Where it is, as the two ears would have it.
            float distance = 0f;
            float pan = 0f;
            float attenuation = 1f;
            float cutoff = MaxAirCutoff;
            float rate = voice.Rate;

            if (voice.Positional)
            {
                // Something flying keeps going between the game's words to it, so it passes as it should.
                if (voice.Doppler) voice.Location += voice.Velocity * (count / (float)AudioFormat.SampleRate);

                Vector3 offset = voice.Location - _listener;
                distance = offset.Length;
                float x = Vector3.Dot(offset, _right);
                float z = Vector3.Dot(offset, _forward);

                pan = x / MathF.Sqrt(x * x + z * z + FocusRadius * FocusRadius);
                pan = Math.Clamp(pan, -WidestPan, WidestPan);
                attenuation = Attenuation(voice, distance);

                // Air takes the top off with distance. Scaled up from the real figures: a game's hundred metres has to read as a real kilometre.
                cutoff = MaxAirCutoff / (1f + distance * 0.022f);

                if (voice.Doppler && distance > 0.01f)
                {
                    Vector3 toward = offset / distance;
                    float closing = Vector3.Dot(voice.Velocity - _listenerVelocity, -toward);
                    float bend = SpeedOfSound / MathF.Max(SpeedOfSound - closing, 40f);
                    voice.DopplerRate += (Math.Clamp(bend, 0.6f, 1.7f) - voice.DopplerRate) * 0.25f;
                    rate *= voice.DopplerRate;
                }
            }

            // The loudest thing is at the top of the window; everything else is under it by what separates them.
            float windowGain = voice.Presence > 0f && voice.Presence >= _window ? 1f : AudioFormat.DecibelsToGain(MathF.Min(voice.Presence - _window, 0f));
            float level = voice.Gain * attenuation * windowGain;

            // A compensated pan law: the middle stays where it was, and a hard pan doesn't go over.
            float angle = (pan + 1f) * (MathF.PI * 0.25f);
            float lawLeft = MathF.Cos(angle) * 1.41421f;
            float lawRight = MathF.Sin(angle) * 1.41421f;
            float law = 1f / (1f + 0.41421f * MathF.Abs(pan));
            float targetLeft = level * lawLeft * law;
            float targetRight = level * lawRight * law;

            // Reverb falls off slower than the direct sound, which is how an ear hears that something is far away.
            float targetSend = voice.ReverbSend * voice.Gain * windowGain * MathF.Sqrt(attenuation) * (voice.Positional ? 1f + MathF.Min(distance / 40f, 1.2f) : 1f);

            if (!voice.Begun)
            {
                voice.GainLeft = targetLeft;
                voice.GainRight = targetRight;
                voice.Send = targetSend;
                voice.DelayLeft = pan > 0f ? pan * InterauralDelay : 0f;
                voice.DelayRight = pan < 0f ? -pan * InterauralDelay : 0f;
                voice.Begun = true;
            }

            bool ended = voice.LiveSource != null ? RenderLive(voice, count) : RenderTake(voice, count, rate);

            float shadowLeft = cutoff * (1f - 0.62f * MathF.Max(pan, 0f));
            float shadowRight = cutoff * (1f - 0.62f * MathF.Max(-pan, 0f));
            voice.AirLeft.SetCutoff(shadowLeft);
            voice.AirRight.SetCutoff(shadowRight);

            float[] busLeft = _busLeft[(int)voice.Bus];
            float[] busRight = _busRight[(int)voice.Bus];

            float stepLeft = (targetLeft - voice.GainLeft) / count;
            float stepRight = (targetRight - voice.GainRight) / count;
            float stepSend = (targetSend - voice.Send) / count;
            float gainLeft = voice.GainLeft, gainRight = voice.GainRight, send = voice.Send;

            float delayTargetLeft = pan > 0f ? pan * InterauralDelay : 0f;
            float delayTargetRight = pan < 0f ? -pan * InterauralDelay : 0f;
            float delayStepLeft = (delayTargetLeft - voice.DelayLeft) / count;
            float delayStepRight = (delayTargetRight - voice.DelayRight) / count;
            float delayLeft = voice.DelayLeft, delayRight = voice.DelayRight;
            float[] history = voice.History;
            int position = voice.HistoryPosition;

            bool mono = voice.Positional && (voice.Buffer == null || voice.Buffer.Right == null);
            float fade = voice.Fade;

            for (int i = 0; i < count; i++)
            {
                float left = _sourceLeft[i];
                float right = _sourceRight[i];

                if (voice.Stopping)
                {
                    fade -= voice.FadeStep;
                    if (fade <= 0f)
                    {
                        fade = 0f;
                        ended = true;
                    }
                    left *= fade;
                    right *= fade;
                }

                if (voice.Positional)
                {
                    if (mono)
                    {
                        // One source, reaching the far ear a moment after the near one.
                        history[position] = left;
                        left = ReadDelayed(history, position, delayLeft);
                        right = ReadDelayed(history, position, delayRight);
                        position = (position + 1) & Voice.HistoryMask;
                        delayLeft += delayStepLeft;
                        delayRight += delayStepRight;
                    }
                    left = voice.AirLeft.Lowpass(left);
                    right = voice.AirRight.Lowpass(right);
                }

                busLeft[i] += left * gainLeft;
                busRight[i] += right * gainRight;
                _sendLeft[i] += left * send;
                _sendRight[i] += right * send;

                gainLeft += stepLeft;
                gainRight += stepRight;
                send += stepSend;
            }

            voice.GainLeft = gainLeft;
            voice.GainRight = gainRight;
            voice.Send = send;
            voice.DelayLeft = delayLeft;
            voice.DelayRight = delayRight;
            voice.HistoryPosition = position;
            voice.Fade = fade;

            if (ended) Finish(voice);
        }

        private float InterauralDelay => (_headphones ? 0.00062f : MaxInterauralDelay) * AudioFormat.SampleRate;

        private static float ReadDelayed(float[] history, int position, float delay)
        {
            int whole = (int)delay;
            float fraction = delay - whole;
            float a = history[(position - whole) & Voice.HistoryMask];
            float b = history[(position - whole - 1) & Voice.HistoryMask];
            return a + (b - a) * fraction;
        }

        private static float Attenuation(Voice voice, float distance)
        {
            if (distance <= voice.NearDistance) return 1f;

            // Inverse distance, as sound spreads, brought smoothly to nothing at the far limit so nothing is ever cut off audibly.
            float spread = voice.NearDistance / distance;
            float along = Math.Clamp((distance - voice.NearDistance) / (voice.FarDistance - voice.NearDistance), 0f, 1f);
            float end = 1f - along * along;
            return spread * end * end;
        }

        private bool RenderLive(Voice voice, int count)
        {
            voice.LiveSource!.Render(_sourceLeft.AsSpan(0, count), _sourceRight.AsSpan(0, count));
            return false;
        }

        private bool RenderTake(Voice voice, int count, float rate)
        {
            SoundBuffer take = voice.Buffer!;
            float[] left = take.Left;
            float[]? right = take.Right;
            int length = take.Length;
            double position = voice.Position;

            // At exactly the recorded pitch, nothing needs interpolating, and nothing is lost.
            if (rate == 1f && position == Math.Floor(position))
            {
                int whole = (int)position;
                int available = Math.Max(Math.Min(count, length - whole), 0);
                Array.Copy(left, whole, _sourceLeft, 0, available);
                Array.Copy(right ?? left, whole, _sourceRight, 0, available);
                Array.Clear(_sourceLeft, available, count - available);
                Array.Clear(_sourceRight, available, count - available);
                voice.Position = position + count;
                return voice.Position >= length;
            }

            for (int i = 0; i < count; i++)
            {
                int whole = (int)position;
                float fraction = (float)(position - whole);
                if (whole >= length)
                {
                    _sourceLeft[i] = 0f;
                    _sourceRight[i] = 0f;
                }
                else
                {
                    float value = Resampler.Read(left, length, whole, fraction);
                    _sourceLeft[i] = value;
                    _sourceRight[i] = right != null ? Resampler.Read(right, length, whole, fraction) : value;
                }
                position += rate;
            }
            voice.Position = position;
            return position >= length;
        }
    }
}
