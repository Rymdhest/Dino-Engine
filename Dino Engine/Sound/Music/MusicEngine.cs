using Dino_Engine.Sound.Content;
using Dino_Engine.Sound.Dsp;

namespace Dino_Engine.Sound.Music
{
    /// <summary>
    /// The conductor: asks the composer for each bar just before it's due and hands it to the band. A bar is written at the last moment so it can answer what's going on. If a
    /// fight breaks out mid-bar, this cuts in on the next beat, with what was playing dying away under the run-up.
    /// </summary>
    public sealed class MusicEngine
    {
        private const double Lookahead = 0.12;     // Seconds before a bar that it's written.

        private readonly Orchestra _band;
        private readonly Composer _composer;
        private long _barStart;
        private long _nextBar;
        private double _beat = 0.5;
        private MusicIntent _intent;

        public MusicEngine(ulong seed)
        {
            var score = new Score(seed);
            Score = score;
            _band = new Orchestra(seed);
            _composer = new Composer(score, seed * 31UL + 1UL);
        }

        public Score Score { get; }
        public Orchestra Band => _band;

        /// <summary>Called with every bar, for tools.</summary>
        public Action<Bar, long>? Listener;

        public void Set(in MusicIntent intent)
        {
            _intent = intent;
        }

        public void Render(float[] left, float[] right, int frames)
        {
            long now = _band.Now;

            if (_composer.WantsToCutIn(_intent))
            {
                // On the next beat of the bar that's playing, not the next bar: when a fight starts, a second is a long time.
                long beat = Math.Max((long)(_beat * AudioFormat.SampleRate), 1L);
                long cut = now <= _barStart ? _barStart : _barStart + ((now - _barStart) / beat + 1L) * beat;
                if (cut < _nextBar)
                {
                    _band.Cut(cut, Bridge.LetGo);
                    _nextBar = cut;
                }
            }

            long lead = (long)(Lookahead * AudioFormat.SampleRate);
            while (now + frames + lead >= _nextBar)
            {
                Bar bar = _composer.Next(_intent);
                _intent.Cue = MusicCue.None;
                foreach (Note note in bar.Notes) _band.Schedule(note, _nextBar);
                Listener?.Invoke(bar, _nextBar);
                _barStart = _nextBar;
                _nextBar += (long)(bar.Seconds * AudioFormat.SampleRate);
                _beat = bar.Seconds / bar.Beats;
            }

            _band.Render(left, right, frames);
        }
    }

    /// <summary>
    /// The band, heard. Playing and composing take far more than the audio thread should be asked for, so they run on a thread of their own, ahead of time, and this reads what's
    /// ready. If that thread falls behind the music has a gap, and every other sound carries on as it was.
    /// </summary>
    internal sealed class MusicStream : LiveSound
    {
        private const int RingLength = 1 << 15;     // About 0.68 seconds.
        private const int Mask = RingLength - 1;
        private const int Chunk = 1024;
        private const int Ahead = 14000;            // Samples kept ready: about 0.3 seconds, comfortably past any garbage collection.

        private readonly ulong _seed;
        private MusicEngine? _engine;
        private MusicIntent _intent;
        private readonly float[] _left = new float[RingLength];
        private readonly float[] _right = new float[RingLength];
        private readonly float[] _chunkLeft = new float[Chunk];
        private readonly float[] _chunkRight = new float[Chunk];
        private readonly Thread _thread;
        private long _written;
        private long _read;
        private long _stopAt = long.MaxValue;

        public int Gaps;

        public MusicStream(ulong seed) : base(0, seed)
        {
            _seed = seed;
            _thread = new Thread(Run) { Name = "Orchestra", IsBackground = true, Priority = ThreadPriority.AboveNormal };
            _thread.Start();
        }

        public void Set(in MusicIntent intent)
        {
            lock (_left)
            {
                MusicCue pending = _intent.Cue;
                _intent = intent;
                if (intent.Cue == MusicCue.None) _intent.Cue = pending;
            }
        }

        private void Run()
        {
            // Writing a score and building the instruments takes a moment, so it's done here, where nobody is waiting for it.
            var engine = new MusicEngine(_seed);
            _engine = engine;

            while (Environment.TickCount64 < Volatile.Read(ref _stopAt))
            {
                long written = _written;
                if (written - Volatile.Read(ref _read) > Ahead)
                {
                    Thread.Sleep(4);
                    continue;
                }

                lock (_left)
                {
                    engine.Set(_intent);
                    _intent.Cue = MusicCue.None;
                }
                engine.Render(_chunkLeft, _chunkRight, Chunk);
                for (int i = 0; i < Chunk; i++)
                {
                    _left[(written + i) & Mask] = _chunkLeft[i];
                    _right[(written + i) & Mask] = _chunkRight[i];
                }
                Volatile.Write(ref _written, written + Chunk);
            }
        }

        protected override void Process(Span<float> left, Span<float> right)
        {
            long read = _read;
            long available = Volatile.Read(ref _written) - read;
            int count = (int)Math.Min(left.Length, available);

            // The band takes a moment to arrive. That isn't a gap.
            if (_engine == null || read == 0L && available == 0L)
            {
                left.Clear();
                right.Clear();
                return;
            }
            for (int i = 0; i < count; i++)
            {
                left[i] = _left[(read + i) & Mask];
                right[i] = _right[(read + i) & Mask];
            }
            if (count < left.Length)
            {
                Gaps++;
                left.Slice(count).Clear();
                right.Slice(count).Clear();
            }
            Volatile.Write(ref _read, read + count);
        }

        /// <summary>Plays on until it has faded out of hearing and then stops by itself, so that nobody has to wait for it.</summary>
        public void Stop(float fadeSeconds)
        {
            Volatile.Write(ref _stopAt, Environment.TickCount64 + (long)(fadeSeconds * 1000f) + 500L);
        }
    }

    /// <summary>
    /// Music, from the game's side: say what's going on and it follows. A world's score is begun once, from its seed, and from then on the game only ever says what the moment is like.
    /// <code>
    /// Music.Begin(worldSeed);
    /// Music.Mood = MusicMood.Town;        // Takes effect at the end of the phrase, or at once for a fight.
    /// Music.Intensity = 0.7f;             // Adds players, loudness and speed, from the next bar.
    /// Music.Play(MusicCue.Victory);
    /// </code>
    /// </summary>
    public static class Music
    {
        private static MusicStream? _stream;
        private static SoundHandle _voice;
        private static MusicMood _mood = MusicMood.Silence;
        private static float _intensity = 0.4f;

        /// <summary>Set while someone's trying things out by hand, so that the game leaves the music alone.</summary>
        public static bool Manual;

        private static readonly LiveDefinition Definition = new("music", seed => new MusicStream(seed)) { Bus = SoundBus.Music, Priority = 99 };

        public static MusicMood Mood
        {
            get => _mood;
            set
            {
                _mood = value;
                Heard.Add(value);
                Update(MusicCue.None);
            }
        }

        public static float Intensity
        {
            get => _intensity;
            set
            {
                _intensity = Math.Clamp(value, 0f, 1f);
                Update(MusicCue.None);
            }
        }

        public static int Gaps => _stream?.Gaps ?? 0;

        /// <summary>Every mood asked for so far, as a scripted run only sees where it ended up.</summary>
        public static HashSet<MusicMood> Heard { get; } = new();

        /// <summary>Starts a world's score, replacing any other.</summary>
        public static void Begin(ulong seed)
        {
            End(1.5f);
            _voice = Audio.Start(Definition, gain: 1f, seed: seed + 1UL);
            _stream = Audio.LiveOf(_voice) as MusicStream;
            Update(MusicCue.None);
        }

        public static void End(float fadeSeconds = 1f)
        {
            _voice.Stop(fadeSeconds);
            _voice = default;
            _stream?.Stop(fadeSeconds);
            _stream = null;
        }

        public static void Play(MusicCue cue) => Update(cue);

        private static void Update(MusicCue cue)
        {
            _stream?.Set(new MusicIntent { Mood = _mood, Intensity = _intensity, Cue = cue });
        }
    }
}
