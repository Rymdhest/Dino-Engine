using Dino_Engine.Sound.Dsp;

namespace Dino_Engine.Sound.Music
{
    /// <summary>What the game wants of the music right now.</summary>
    public struct MusicIntent
    {
        public MusicMood Mood;
        public float Intensity;
        public MusicCue Cue;
    }

    /// <summary>A written bar, ready to be played.</summary>
    public sealed class Bar
    {
        public readonly List<Note> Notes = new();
        public double Seconds;
        public int Beats = 4;
        public string Label = "";
        public Staging? Staging;        // Where a new piece moves the band to, on its first bar.
    }

    /// <summary>
    /// A land's music, before any of it is played: its key, its theme, its piece and its tyrant's fight. Everything comes from one seed, so a land always sounds like itself and no two
    /// sound alike. What ties a good score together is one tune heard many ways, so the theme's skeleton is what every tune in it is written over: a land's quiet piece and its
    /// tyrant's fight are the same melody dressed differently, and the ear knows it without knowing why. Only the shape travels, which is why a major hymn and a minor hunt can share one.
    /// </summary>
    public sealed class Score
    {
        public sealed class Setting
        {
            public Style Style = null!;
            public Meter Meter;
            public Mode Mode = Mode.Aeolian;
            public int Tonic;
            public float Tempo;
            public Tune First = null!;      // The theme.
            public Tune Second = null!;     // The same shape, with new figures: a variation.
            public Tune Contrast = null!;   // Something else, for the middle.
        }

        private readonly Dictionary<MusicMood, Setting> _settings = new();
        private readonly int[] _shape;
        private readonly Taste _taste;

        public readonly int Tonic;
        public readonly ulong Seed;
        public readonly MusicPlace Place;
        public readonly MusicMood Main;     // What plays here when nothing's happening.

        public Score(MusicPlace place)
        {
            Place = place;
            Seed = place.Seed;
            var random = new SoundRandom(Seed);

            // Any key from middle C up a seventh, where a theme sits comfortably on a flute, a horn or a cello an octave down.
            Tonic = 60 + random.Int(12);

            // The theme itself: written once, in a neutral way, for the sake of its shape, by a composer with leanings of their own.
            _taste = Taste.Draw(random);
            Chord[] harmony = Harmony.Draw(random, Mode.Aeolian, 0f);
            Tune theme = Songwriter.Write(random, new TuneBrief { Mode = Mode.Aeolian, Style = MelodicStyle.Lyrical, Harmony = harmony, Candidates = 400, Taste = _taste });
            _shape = Songwriter.ShapeOf(theme);

            var arranging = new SoundRandom(Seed * 0x9E3779B97F4A7C15UL + 7UL);
            if (place.Haven)
            {
                Main = MusicMood.Haven;
                _settings[Main] = Set(Arranger.Haven(arranging));
            }
            else if (place.Arena)
            {
                Main = MusicMood.Arena;
                _settings[Main] = Set(Arranger.Arena(place, arranging));
            }
            else
            {
                Main = MusicMood.Land;
                Style land = Arranger.Land(place, arranging);
                _settings[Main] = Set(land);
                _settings[MusicMood.Boss] = Set(Arranger.Boss(place, land, arranging));
            }
        }

        /// <summary>The setting for a mood, or this place's own where it has none of that kind: the Haven has no tyrant.</summary>
        public Setting For(MusicMood mood) => _settings.TryGetValue(mood, out Setting? setting) ? setting : _settings[Main];

        public bool Has(MusicMood mood) => _settings.ContainsKey(mood);

        /// <summary>New variations and a new middle over the same theme, so that a long stay in one place keeps moving while it stays recognisably itself.</summary>
        public void Refresh(MusicMood mood, SoundRandom random)
        {
            Setting setting = For(mood);
            Style style = setting.Style;
            Chord[] contrast = Harmony.Write(random, style.Language, setting.Mode, style.Colour);
            Arranger.Rotate(style, random);
            var brief = new TuneBrief { Mode = setting.Mode, Meter = setting.Meter, Style = style.Melodic, Harmony = setting.First.Harmony, Shape = _shape, Candidates = 120, Taste = _taste };
            var answer = new TuneBrief { Mode = setting.Mode, Meter = setting.Meter, Style = style.Melodic, Harmony = contrast, LowestDegree = 0, HighestDegree = 11, Candidates = 120, Taste = _taste };
            setting.Second = Songwriter.Write(random, brief);
            setting.Contrast = Songwriter.Write(random, answer);
        }

        private Setting Set(Style style)
        {
            var random = new SoundRandom(Seed * 2654435761UL + (ulong)style.Mood * 97UL);
            Mode mode = random.Pick(style.Modes);
            Meter meter = random.Pick(style.Meters);

            Chord[] harmony = Harmony.Write(random, style.Language, mode, style.Colour);
            Chord[] contrast = Harmony.Write(random, style.Language, mode, style.Colour);

            var brief = new TuneBrief { Mode = mode, Meter = meter, Style = style.Melodic, Harmony = harmony, Shape = _shape, Taste = _taste };
            var answer = new TuneBrief { Mode = mode, Meter = meter, Style = style.Melodic, Harmony = contrast, LowestDegree = 0, HighestDegree = 11, Taste = _taste };

            return new Setting
            {
                Style = style,
                Meter = meter,
                Mode = mode,
                Tonic = Tonic + style.TonicShift,
                Tempo = style.Tempo + random.Range(-style.TempoSpread, style.TempoSpread),
                First = Songwriter.Write(random, brief),
                Second = Songwriter.Write(random, brief),
                Contrast = Songwriter.Write(random, answer),
            };
        }
    }

    /// <summary>
    /// Writes the music a bar at a time, just before it's played, so that it can be told at the last moment what's going on. A phrase goes through a form, tune and variation and a
    /// contrast, and a style that wants to leaves silence between them: music for wandering a land should stop sometimes, or nobody can hear the land.
    /// </summary>
    public sealed class Composer
    {
        private const int ArrivalBars = 4;          // A style that's eased into builds up over these.
        private const float ArrivalDip = 0.3f;     // Share of its intensity held back at the start of them.

        private static readonly MelodyUse[] Form = { MelodyUse.First, MelodyUse.Second, MelodyUse.Contrast, MelodyUse.First };

        private readonly Score _score;
        private readonly SoundRandom _random;
        private readonly BarContext _context = new();
        private readonly Performer _performer;
        private Score.Setting? _setting;
        private MusicMood _mood = MusicMood.Silence;
        private Tune? _tune;
        private int _formIndex;
        private int _bar;
        private int _rest;
        private int _phrasesSinceRest;
        private int _cueBars;
        private int _arriving;
        private float _beat = 0.5f;
        private float _played;
        private float _absolute;
        private bool _staged;
        private MusicCue _cue;

        public Composer(Score score, ulong seed)
        {
            _score = score;
            _random = new SoundRandom(seed);
            _context.Random = _random.Fork();
            _performer = new Performer(_random.Fork());
        }

        public MusicMood Mood => _mood;

        /// <summary>Whether what's asked for can't wait for the end of the phrase.</summary>
        public bool WantsToCutIn(in MusicIntent intent)
        {
            MusicMood mood = Resolve(intent.Mood);
            return intent.Cue != MusicCue.None || (mood != _mood && mood != MusicMood.Silence && Moods.IsUrgent(mood));
        }

        // A mood this place has no piece for is its own piece: the game can still be asking for a tyrant's fight as the Haven begins, after a land was lost at its keep, and the Haven's
        // theme taken for a fight once ran up to it from nothing and stopped the game.
        private MusicMood Resolve(MusicMood mood) => mood == MusicMood.Silence || _score.Has(mood) ? mood : _score.Main;

        public Bar Next(in MusicIntent asked)
        {
            MusicIntent intent = asked;
            intent.Mood = Resolve(asked.Mood);
            if (intent.Cue != MusicCue.None && _cue == MusicCue.None) BeginCue(intent.Cue);
            if (_cueBars > 0) return WriteCue();

            if (intent.Mood != _mood && Ready(intent.Mood))
            {
                Bar? bridge = Move(intent);
                if (bridge != null)
                {
                    bridge.Staging = _setting?.Style.Staging;
                    _staged = true;
                    return bridge;
                }
            }

            if (_mood == MusicMood.Silence || _setting == null) return Rest(2.0);

            if (_rest > 0)
            {
                _rest--;
                return Rest(_setting.Meter.Beats * 60.0 / _setting.Tempo);
            }

            return WriteBar(intent.Intensity);
        }

        // A calm style waits for the tune to reach a place it can stop, as cutting one off is rude, though its phrases are too long to wait for the end of one: slow music would answer a
        // change of place a quarter of a minute late.
        private bool Ready(MusicMood next)
        {
            if (Moods.IsUrgent(next) || next == MusicMood.Silence) return true;
            return _rest > 0 || _tune == null || _bar % 2 == 0;
        }

        // Goes over to another style, by way of a bridge if there's anything to bridge from. Out of a silence a calm style just begins, and a fight is always run up to.
        private Bar? Move(in MusicIntent intent)
        {
            Score.Setting? from = _rest > 0 ? null : _setting;
            bool urgent = Moods.IsUrgent(intent.Mood);
            List<Note> carried = from != null && !urgent && intent.Mood != MusicMood.Silence ? Accompaniment(from, _score.For(intent.Mood)) : new List<Note>();
            int carriedBeats = from == null ? 0 : from.Meter.Beats * (from.Meter.Beats < Bridge.ShortestCarry ? 2 : 1);

            Begin(intent.Mood);
            if (_setting == null) return null;

            float beat = 60f / _setting.Tempo;
            Bar? bridge = from != null || urgent ? Bridge.Write(from, _setting, from != null ? _beat : beat, intent.Intensity, carried, carriedBeats, _context.Random) : null;
            _arriving = urgent ? 0 : ArrivalBars;
            _beat = beat;
            return bridge;
        }

        // One more bar of what was keeping time, for the band to play through a bridge: stopping the rhythm section is what makes a change sound like a change of record. Its rhythm is
        // the old style's and its harmony already the new one's, which is how a band modulates, and leaves nothing to grind against the chord rising under it. Written in beats, as how
        // long a beat is going to be isn't this style's to say. The tune has already stopped, and a plain bar has no fills in it.
        private List<Note> Accompaniment(Score.Setting from, Score.Setting to)
        {
            Style style = from.Style;
            int bars = from.Meter.Beats < Bridge.ShortestCarry ? 2 : 1;
            Tune tune = _tune ?? from.First;
            var notes = new List<Note>();

            BarContext bar = _context;
            bar.Mode = to.Mode;
            bar.Home = to.Mode;
            bar.Tonic = to.Tonic;
            bar.Meter = from.Meter;
            bar.BeatSeconds = 1f;
            bar.Intensity = _played;
            bar.Harmony = Array.Empty<Chord>();
            bar.HymnAt = -1f;
            bar.Bars = tune.Bars;
            bar.Section = MelodyUse.First;
            bar.Melody = Array.Empty<Tone>();
            for (int b = 0; b < bars; b++)
            {
                bar.Bar = 1;
                bar.AbsoluteBeat = _absolute + b * from.Meter.Beats;
                bar.Chord = Bridge.Pivot(to);
                bar.NextChord = bar.Chord;
                bar.Notes.Clear();
                foreach (Part part in style.Parts)
                {
                    // The tune has stopped, and what moves by semitone would hang on, slowed, against the chord rising under it.
                    if (part.Writer == Textures.Melody || part.Writer == Textures.Countermelody || part.Writer == Textures.Prowl || part.Writer == Textures.Fanfare) continue;
                    if (_played >= part.From && _played <= part.To) part.Writer(bar, part);
                }
                foreach (Note written in bar.Notes)
                {
                    Note note = written;
                    note.Start += b * from.Meter.Beats;
                    notes.Add(note);
                }
            }
            return notes;
        }

        private static Bar Rest(double seconds) => new Bar { Seconds = seconds, Label = "rest" };

        private void Begin(MusicMood mood)
        {
            _mood = mood;
            _bar = 0;
            _formIndex = 0;
            _rest = 0;
            _tune = null;
            _phrasesSinceRest = 0;
            _setting = mood == MusicMood.Silence ? null : _score.For(mood);
            _context.Voicings.Clear();
            _context.HymnAt = -1f;
            _absolute = 0f;
            _staged = false;
        }

        private Tune TuneFor(MelodyUse section) => section switch
        {
            MelodyUse.Second => _setting!.Second,
            MelodyUse.Contrast => _setting!.Contrast,
            _ => _setting!.First,
        };

        private Bar WriteBar(float intensity)
        {
            Score.Setting setting = _setting!;
            MelodyUse section = Form[_formIndex % Form.Length];
            _tune ??= TuneFor(section);
            Tune tune = _tune;

            Style style = setting.Style;
            float tempo = setting.Tempo * (1f + style.TempoLift * (intensity - 0.5f));
            float arrival = 1f - ArrivalDip * _arriving / ArrivalBars;
            if (_arriving > 0) _arriving--;
            float shaped = Math.Clamp(intensity * Tide(style, tune.Bars) * arrival, 0f, 1f);
            _played = shaped;
            BarContext bar = _context;
            bar.Chord = tune.Harmony[_bar];
            bar.NextChord = tune.Harmony[(_bar + 1) % tune.Bars];
            bar.Home = setting.Mode;
            bar.Mode = bar.Chord.Borrow ?? setting.Mode;
            bar.Harmony = tune.Harmony;
            bar.AbsoluteBeat = _absolute;
            bar.Tonic = setting.Tonic;
            bar.Meter = setting.Meter;
            bar.BeatSeconds = 60f / tempo;
            _beat = bar.BeatSeconds;
            bar.Intensity = shaped;
            bar.Bar = _bar;
            bar.Bars = tune.Bars;
            bar.Section = section;
            bar.Notes.Clear();

            float beats = setting.Meter.Beats;
            var melody = new List<Tone>();
            foreach (Tone tone in tune.Tones)
            {
                if (tone.Beat >= _bar * beats && tone.Beat < (_bar + 1) * beats) melody.Add(new Tone(tone.Beat - _bar * beats, tone.Length, tone.Degree));
            }
            bar.Melody = melody;
            bar.TuneLowest = int.MaxValue;
            bar.TuneHighest = int.MinValue;
            foreach (Tone tone in tune.Tones)
            {
                bar.TuneLowest = Math.Min(bar.TuneLowest, tone.Degree);
                bar.TuneHighest = Math.Max(bar.TuneHighest, tone.Degree);
            }

            // Who has the tune in this bar, and so where it really is, for the accompaniment to keep out of its way.
            bar.MelodyShift = null;
            foreach (Part part in style.Parts)
            {
                if (part.Writer != Textures.Melody || shaped < part.From || shaped > part.To || (part.Use != MelodyUse.Always && part.Use != section)) continue;
                bar.MelodyShift = Textures.TuneShift(bar, part);
                break;
            }

            foreach (Part part in style.Parts)
            {
                if (shaped >= part.From && shaped <= part.To) part.Writer(bar, part);
            }
            if (melody.Count > 0) bar.PreviousMelodyDegree = melody[^1].Degree;

            var written = new Bar { Seconds = beats * bar.BeatSeconds, Beats = (int)beats, Label = $"{style.Mood} {section} {_bar + 1}" };
            float ritard = _performer.Perform(bar, written);
            written.Seconds *= ritard;
            _absolute += beats;
            if (!_staged)
            {
                written.Staging = style.Staging;
                _staged = true;
            }

            _bar++;
            if (_bar >= tune.Bars) EndPhrase(style);
            return written;
        }

        // A piece at full stretch all the way has nowhere to go: held at what the game asked for, a fight stayed within two decibels, where a recorded orchestra swings fifteen or more.
        // So the form has a shape of its own on top of the game's intensity. The first statement holds something back, its repeat has it all, the middle drops away and builds back
        // bar by bar, late rather than early, and the return lands on everything.
        private float Tide(Style style, int bars)
        {
            float along = _bar / (float)Math.Max(bars - 1, 1);
            float shape = (_formIndex % Form.Length) switch
            {
                0 => 0.84f,
                1 => 1f,
                2 => 0.4f + 0.6f * along * along,
                _ => 1.14f,
            };
            return 1f + (shape - 1f) * style.Tide;
        }

        private void EndPhrase(Style style)
        {
            _bar = 0;
            _tune = null;
            _formIndex++;
            _phrasesSinceRest++;

            bool whole = _formIndex % Form.Length == 0;
            if (whole) _score.Refresh(style.Mood, _random);
            if (!style.Rests) return;
            if (whole || _phrasesSinceRest >= 2 && _random.Chance(0.4f))
            {
                _rest = (int)(style.RestBars * _random.Range(0.7f, 1.5f));
                _phrasesSinceRest = 0;
            }
        }

        private void BeginCue(MusicCue cue)
        {
            _cue = cue;
            _cueBars = 2;
        }

        private Bar WriteCue()
        {
            Score.Setting setting = _setting ?? _score.For(_score.Main);
            int tonic = setting.Tonic;
            var bar = new Bar { Label = _cue.ToString() };
            float beat = 60f / (_cue == MusicCue.Victory ? 104f : 58f);
            bar.Seconds = beat * 4;
            int step = 2 - _cueBars;

            if (_cue == MusicCue.Victory) Cues.Victory(bar, tonic, beat, step);
            else Cues.Defeat(bar, tonic, beat, step);

            _cueBars--;
            if (_cueBars == 0)
            {
                _cue = MusicCue.None;
                _bar = 0;
                _tune = null;
                _rest = setting.Style.Rests ? setting.Style.RestBars : 0;
            }
            return bar;
        }
    }

    /// <summary>Short moments with their own few bars, set in the world's key.</summary>
    internal static class Cues
    {
        public static void Victory(Bar bar, int tonic, float beat, int step)
        {
            // Horns and strings on a plain major chord: after a minor fight, the third going up is the whole of the feeling.
            int[] chord = step == 0 ? new[] { 0, 4, 7 } : new[] { 0, 4, 7, 12 };
            float[] rhythm = step == 0 ? new[] { 0f, 0.75f, 1f, 2f } : new[] { 0f };
            int[] line = step == 0 ? new[] { 7, 7, 12, 16 } : new[] { 19 };
            for (int n = 0; n < rhythm.Length; n++)
            {
                bar.Notes.Add(new Note { Section = Section.Trumpets, Pitch = tonic + line[n], Start = rhythm[n] * beat, Length = (step == 0 ? 0.9f : 3.5f) * beat, Loudness = 0.8f, Articulation = Articulation.Marcato });
                bar.Notes.Add(new Note { Section = Section.Horns, Pitch = tonic + line[n] - 12, Start = rhythm[n] * beat, Length = (step == 0 ? 0.9f : 3.5f) * beat, Loudness = 0.8f, Articulation = Articulation.Marcato });
            }
            foreach (int interval in chord)
            {
                bar.Notes.Add(new Note { Section = Section.Violins, Pitch = tonic + interval, Start = (step == 0 ? 2f : 0f) * beat, Length = (step == 0 ? 2f : 3.6f) * beat, Loudness = 0.7f, Articulation = Articulation.Normal });
                bar.Notes.Add(new Note { Section = Section.Trombones, Pitch = tonic + interval - 24, Start = (step == 0 ? 2f : 0f) * beat, Length = (step == 0 ? 2f : 3.4f) * beat, Loudness = 0.65f, Articulation = Articulation.Normal });
            }
            bar.Notes.Add(new Note { Section = Section.Timpani, Pitch = tonic - 24, Start = (step == 0 ? 2f : 0f) * beat, Length = beat, Loudness = 0.8f });
            if (step == 1) bar.Notes.Add(new Note { Section = Section.Cymbal, Pitch = 60, Start = 0f, Length = beat, Loudness = 0.3f });
        }

        public static void Defeat(Bar bar, int tonic, float beat, int step)
        {
            // A cello falling, alone, over a low held note.
            int[] line = step == 0 ? new[] { 7, 5, 3, 2 } : new[] { 3, 2, 0 };
            float[] lengths = step == 0 ? new[] { 1.5f, 0.5f, 1f, 1f } : new[] { 1f, 1f, 2f };
            float at = 0f;
            for (int n = 0; n < line.Length; n++)
            {
                bar.Notes.Add(new Note { Section = Section.Cellos, Pitch = tonic - 12 + line[n], Start = at * beat, Length = lengths[n] * beat * 0.97f, Loudness = 0.5f - n * 0.03f, Articulation = n == 0 ? Articulation.Normal : Articulation.Legato });
                at += lengths[n];
            }
            bar.Notes.Add(new Note { Section = Section.Basses, Pitch = tonic - 24, Start = 0f, Length = 4f * beat, Loudness = 0.3f, Articulation = Articulation.Swell });
            bar.Notes.Add(new Note { Section = Section.Choir, Pitch = tonic - 12, Start = 0f, Length = 4f * beat, Loudness = 0.2f, Articulation = Articulation.Swell });
            bar.Notes.Add(new Note { Section = Section.Choir, Pitch = tonic - 5, Start = 0f, Length = 4f * beat, Loudness = 0.2f, Articulation = Articulation.Swell });
        }
    }

    /// <summary>
    /// The difference between notes and a performance. A sequencer plays what's written; a player leans on the strong beats, breathes between phrases and slows at the end, and these are
    /// the rules Sundberg, Friberg and Bresin distilled from what real musicians do: phrase arch, final ritardando, duration contrast, high loud. Timing drifts as players' does, each
    /// moment close to the last, which listeners measurably prefer to the same amount of random jitter (Hennig and others).
    /// </summary>
    internal sealed class Performer
    {
        private const float Drift = 0.009f;         // Seconds. Enough to be felt, not heard.
        private readonly SoundRandom _random;
        private readonly float[] _timing = new float[Enum.GetValues<Section>().Length];
        private Wander _feel;

        public Performer(SoundRandom random)
        {
            _random = random;
            _feel.Set(6f, 2f);
        }

        /// <summary>Plays what the arrangement wrote into a bar, and says how much longer than written the bar turns out.</summary>
        public float Perform(BarContext bar, Bar output)
        {
            float place = bar.Bar / (float)Math.Max(bar.Bars - 1, 1);

            // Louder towards the middle of a phrase and away again.
            float arch = MathF.Sin(place * MathF.PI) * 0.07f;

            // The end of a phrase slows, as a runner stopping does: by the square root of what's left, and more at the end of a section than half way.
            float ritard = 1f;
            if (bar.LastBar) ritard = 1.07f;
            else if (bar.Bar == bar.Bars - 2) ritard = 1.025f;
            else if (bar.Halfway) ritard = 1.02f;

            float feel = _feel.Next(_random, 0.5f);
            for (int s = 0; s < _timing.Length; s++)
            {
                _timing[s] += (feel * Drift + _random.Gaussian() * 0.002f - _timing[s]) * 0.4f;
            }

            foreach (Note written in bar.Notes)
            {
                Note note = written;
                float beat = note.Start / bar.BeatSeconds;
                float weight = bar.Meter.Weight(beat);

                note.Loudness = Math.Clamp(note.Loudness + arch + (weight - 0.45f) * 0.1f + _random.Gaussian() * 0.012f, 0.02f, 1f);

                // Duration contrast: short notes shorter, long ones longer.
                if (note.Length < bar.BeatSeconds * 0.6f) note.Length *= 0.95f;

                // Everything arrives when it means to: a bowed note or a voice speaks late, so a player starts it early.
                float lead = Speaks(note.Section);
                note.Start = MathF.Max(note.Start * ritard + _timing[(int)note.Section] - lead + _random.Gaussian() * 0.0025f, 0f);
                note.Length *= ritard;
                output.Notes.Add(note);
            }
            return ritard;
        }

        private static float Speaks(Section section) => section switch
        {
            Section.Choir => 0.05f,
            Section.Violins or Section.Violas or Section.Cellos or Section.Basses => 0.02f,
            Section.Horns or Section.Trombones or Section.Trumpets => 0.018f,
            Section.Flute or Section.Oboe or Section.Clarinet => 0.012f,
            _ => 0f,
        };
    }
}
