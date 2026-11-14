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

                // The game's theme, behind the main menu: the Haven's own tune, as an overture. After the waltz, so as to take nothing from what the waltz draws.
                _settings[MusicMood.Title] = Set(Arranger.Overture(arranging));
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

            var brief = new TuneBrief { Mode = mode, Meter = meter, Style = style.Melodic, Harmony = harmony, Shape = _shape, Taste = _taste, Candidates = style.TuneCandidates };
            var answer = new TuneBrief { Mode = mode, Meter = meter, Style = style.Melodic, Harmony = contrast, LowestDegree = 0, HighestDegree = 11, Taste = _taste, Candidates = style.TuneCandidates };

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
            Style style = setting.Style;

            // A piece with a plan goes where it says, whatever the game asks for: an overture builds as it was written to.
            Movement? movement = style.Plan.Length > 0 ? style.Plan[_formIndex % style.Plan.Length] : null;
            if (movement is { Close: true }) return WriteClose(setting, movement);
            if (movement is { Surge: true }) return WriteSurge(setting, movement);

            MelodyUse section = movement?.Use ?? Form[_formIndex % Form.Length];
            _tune ??= TuneFor(movement?.Tune ?? section);
            Tune tune = _tune;

            float shaped;
            if (movement != null)
            {
                float along = MathF.Pow(_bar / (float)Math.Max(tune.Bars - 1, 1), movement.Curve);
                shaped = Math.Clamp(movement.From + (movement.To - movement.From) * along, 0f, 1f);
            }
            else
            {
                float arrival = 1f - ArrivalDip * _arriving / ArrivalBars;
                if (_arriving > 0) _arriving--;
                shaped = Math.Clamp(intensity * Tide(style, tune.Bars) * arrival, 0f, 1f);
            }
            float tempo = setting.Tempo * (movement?.Pace ?? 1f) * (1f + style.TempoLift * ((movement != null ? shaped : intensity) - 0.5f));
            _played = shaped;
            BarContext bar = _context;
            bar.Chord = tune.Harmony[_bar];
            bar.NextChord = tune.Harmony[(_bar + 1) % tune.Bars];
            bar.Home = setting.Mode;
            bar.Mode = bar.Chord.Borrow ?? setting.Mode;
            bar.Harmony = tune.Harmony;
            bar.AbsoluteBeat = _absolute;
            bar.Tonic = setting.Tonic + (movement?.Key ?? 0);
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

            // A part given a section plays only in it, accompaniment as well as tune: that's how an overture's last statement gets players the others don't.
            foreach (Part part in style.Parts)
            {
                if (part.Use != MelodyUse.Always && part.Use != section) continue;
                if (shaped >= part.From && shaped <= part.To) part.Writer(bar, part);
            }
            if (melody.Count > 0) bar.PreviousMelodyDegree = melody[^1].Degree;

            var written = new Bar { Seconds = beats * bar.BeatSeconds, Beats = (int)beats, Label = $"{style.Mood} {movement?.Name ?? section.ToString()} {_bar + 1}" };
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

            bool planned = style.Plan.Length > 0;
            bool whole = _formIndex % (planned ? style.Plan.Length : Form.Length) == 0;
            if (whole) _score.Refresh(style.Mood, _random);

            // An overture that has finished is left to ring out into a silence before it's begun again, with new variations on the same theme.
            if (planned)
            {
                if (whole) _rest = style.RestBars;
                return;
            }
            if (!style.Rests) return;
            if (whole || _phrasesSinceRest >= 2 && _random.Chance(0.4f))
            {
                _rest = (int)(style.RestBars * _random.Range(0.7f, 1.5f));
                _phrasesSinceRest = 0;
            }
        }

        // An overture's last three bars, which aren't a tune's: the subdominant, broad; the swell through the flat sixth and seventh; and the chord.
        private Bar WriteClose(Score.Setting setting, Movement movement)
        {
            float beat = 60f / (setting.Tempo * movement.Pace);
            int beats = setting.Meter.Beats;
            var bar = new Bar { Seconds = beats * beat, Beats = beats, Label = $"{setting.Style.Mood} {movement.Name} {_bar + 1}" };
            int tonic = setting.Tonic + movement.Key;
            if (_bar == 0) Close.Broaden(bar, tonic, beat, beats);
            else if (_bar == 1) Close.Approach(bar, tonic, beat, beats);
            else Close.Arrive(bar, tonic, beat, beats);
            _played = 1f;
            _absolute += beats;
            _bar++;
            if (_bar >= 3) EndPhrase(setting.Style);
            return bar;
        }

        // Bars with no tune, wound up on the dominant for what follows to break on.
        private Bar WriteSurge(Score.Setting setting, Movement movement)
        {
            float beat = 60f / (setting.Tempo * movement.Pace);
            int beats = setting.Meter.Beats;
            var bar = new Bar { Seconds = beats * beat, Beats = beats, Label = $"{setting.Style.Mood} {movement.Name} {_bar + 1}" };
            Surge.Write(bar, setting.Tonic + movement.Key, setting.Mode, beat, beats, _bar, Math.Max(movement.Bars, 1));
            _played = 1f;
            _absolute += beats;
            _bar++;
            if (_bar >= Math.Max(movement.Bars, 1)) EndPhrase(setting.Style);
            return bar;
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
    /// The bars before a drop, which are what make it one: nothing new is said, everything is wound tighter. The bass and low brass sit on the dominant and swell, the horns and
    /// voices hold it with the fourth hanging over the third and only let it fall at the last moment, the violins climb the scale in tremolo, a timpani roll closes in and the big
    /// drum counts in. Then everything stops for half a beat, all but a cymbal roll timed to break on the downbeat, and the theme comes in on the whole band. A person heard the
    /// overture without these and said it sounded as if it were about to begin and then didn't.
    /// </summary>
    internal static class Surge
    {
        private const float CymbalPeak = 1.6f;      // Seconds into its take that a cymbal roll is at its height.
        private const float Breath = 0.5f;          // Beats of silence before the drop.

        private static void Put(Bar bar, Section section, int pitch, float start, float length, float loudness, Articulation articulation, float fader = 0f)
        {
            bar.Notes.Add(new Note
            {
                Section = section,
                Pitch = Textures.Fit(section, pitch),
                Start = start,
                Length = length,
                Loudness = Math.Clamp(loudness, 0.02f, 1f),
                Fader = fader,
                Articulation = articulation,
            });
        }

        /// <summary>The index-th of count bars wound up on the dominant of the key whose tonic is given.</summary>
        public static void Write(Bar bar, int tonic, Mode mode, float beat, int beats, int index, int count)
        {
            float total = beats * count;
            float from = index * beats;
            bool last = index == count - 1;
            float Along(float at) => (from + at) / total;
            float barSeconds = beats * beat;
            float held = last ? (beats - Breath) * beat : barSeconds * 1.02f;
            float rising = 0.5f + 0.45f * (index + 1) / count;
            int dominant = tonic + 7;

            // The floor: the dominant in the basses, cellos, tuba and trombones, swelling bar by bar.
            Put(bar, Section.Basses, dominant - 36, 0f, held, rising, Articulation.Swell, -2f);
            Put(bar, Section.Cellos, dominant - 24, 0f, held, rising, Articulation.Tremolo, -2f);
            Put(bar, Section.Tuba, dominant - 36, 0f, held, rising - 0.05f, Articulation.Swell, -4f);
            Put(bar, Section.Trombones, dominant - 24, 0f, held, rising - 0.05f, Articulation.Swell, -3f);
            Put(bar, Section.Trombones, dominant - 17, 0f, held, rising - 0.05f, Articulation.Swell, -3f);
            Put(bar, Section.Violas, dominant - 12, 0f, held, rising - 0.1f, Articulation.Tremolo, -3f);

            // The horns and voices hold the dominant with its fourth hanging over, and only in the last half bar does it fall to the third.
            float fall = last ? beats / 2f * beat : held;
            foreach (int pitch in new[] { dominant - 12, dominant - 5 })
            {
                Put(bar, Section.Horns, pitch, 0f, held, rising, Articulation.Swell, -1f);
                Put(bar, Section.Choir, pitch + 12, 0f, held, rising - 0.12f, Articulation.Swell, -4f);
            }
            Put(bar, Section.Horns, tonic, 0f, fall, rising, Articulation.Swell, -1f);
            Put(bar, Section.Choir, tonic + 12, 0f, fall, rising - 0.12f, Articulation.Swell, -4f);
            if (last)
            {
                Put(bar, Section.Horns, tonic - 1, fall, held - fall, rising + 0.05f, Articulation.Normal, -1f);
                Put(bar, Section.Choir, tonic + 11, fall, held - fall, rising - 0.07f, Articulation.Normal, -4f);
                Put(bar, Section.Trumpets, dominant, 0f, held, rising - 0.1f, Articulation.Swell, -2f);
            }

            // The violins climbing the scale in tremolo, from the dominant below the tonic to the note under it an octave up, louder as they go.
            int steps = beats * 2;
            int span = count * steps;
            int lowest = count == 1 ? 0 : -3, highest = 6 + 7 * Math.Min(count - 1, 1);
            while (mode.Pitch(tonic, lowest) < Textures.Ranges[(int)Section.Violins].low) lowest++;
            for (int s = 0; s < steps; s++)
            {
                float at = s * 0.5f;
                if (last && at >= beats - Breath) break;
                int n = index * steps + s;
                int degree = lowest + (int)MathF.Round((highest - lowest) * n / (float)Math.Max(span - 2, 1));
                Put(bar, Section.Violins, mode.Pitch(tonic, degree), at * beat, 0.55f * beat, 0.32f + 0.55f * Along(at), Articulation.Tremolo, 0f);
            }

            // A timpani roll on the dominant closing in, the big drum counting the bars and then the last two beats in, and a cymbal breaking on the downbeat after the breath.
            int strokes = beats * 6;
            for (int s = 0; s < strokes; s++)
            {
                float rise = s / (float)strokes;
                float at = beats * (1f - MathF.Pow(1f - rise, 1.25f));
                if (last && at >= beats - 1f) break;
                float along = Along(at);
                Put(bar, Section.Timpani, dominant - 24, at * beat, 0.2f, 0.25f + 0.6f * along * along, Articulation.Normal, -3f);
            }
            Put(bar, Section.BigDrum, 36, 0f, 1f, 0.45f + 0.3f * Along(0f), Articulation.Normal, -4f);
            if (last)
            {
                Put(bar, Section.BigDrum, 36, (beats - 1f) * beat, 0.5f, 0.85f, Articulation.Normal, -3f);
                Put(bar, Section.BigDrum, 36, (beats - Breath - 0.05f) * beat, 0.5f, 0.95f, Articulation.Normal, -3f);
                Put(bar, Section.Timpani, dominant - 24, (beats - 1f) * beat, 0.5f, 0.9f, Articulation.Normal, -2f);
                Put(bar, Section.Timpani, dominant - 24, (beats - Breath - 0.05f) * beat, 0.5f, 1f, Articulation.Normal, -2f);
                Put(bar, Section.Cymbal, 60, MathF.Max(barSeconds - CymbalPeak, 0f), 2f, 0.72f, Articulation.Normal, -1f);
            }
        }
    }

    /// <summary>
    /// How an overture ends, after its last and broadest statement. The subdominant, broad and held, then the swell through the flat sixth and the flat seventh, the oldest way a
    /// film score says that home is coming, with the violins and trumpets climbing by step to the tonic chord's third, a timpani roll closing in and a cymbal timed to break on the
    /// downbeat. Then the tonic, struck by everyone at once with a harp swept up through it, held, and let go into the hall over its bass, as the recording it was measured from
    /// fades for thirteen seconds. Written in major chords, as the overture is in a major mode.
    /// </summary>
    internal static class Close
    {
        private const float CymbalPeak = 1.6f;      // Seconds into its take that a cymbal roll is at its height.
        private const float Ring = 7f;              // Seconds the last chord takes to die away once let go, and its bass longer.

        // Chords as semitones over the tonic, and the line climbing over them.
        private static readonly int[] Subdominant = { 5, 9, 12 };
        private static readonly int[] FlatSixth = { 8, 12, 15 };
        private static readonly int[] FlatSeventh = { 10, 14, 17 };
        private static readonly int[] Home = { 0, 4, 7 };

        private static void Put(Bar bar, Section section, int pitch, float start, float length, float loudness, Articulation articulation, float tail = 0f, float fader = 0f)
        {
            bar.Notes.Add(new Note
            {
                Section = section,
                Pitch = Textures.Fit(section, pitch),
                Start = start,
                Length = length,
                Loudness = Math.Clamp(loudness, 0.02f, 1f),
                Fader = fader,
                Articulation = articulation,
                Tail = tail,
            });
        }

        // One chord for the whole band, from the line at the top to the root at the bottom.
        private static void Chord(Bar bar, int tonic, int[] chord, int line, float at, float length, float loudness, Articulation articulation, float tail)
        {
            Put(bar, Section.Violins, tonic + line, at, length, loudness, articulation, tail, 2f);
            Put(bar, Section.Violins, tonic + line - 12, at, length, loudness - 0.05f, articulation, tail, 0f);
            Put(bar, Section.Trumpets, tonic + line - 12, at, length, loudness - 0.1f, articulation, tail, 0f);
            Put(bar, Section.Violas, tonic + chord[1], at, length, loudness - 0.05f, articulation, tail, -2f);
            Put(bar, Section.Horns, tonic + chord[0] - 12, at, length, loudness, articulation, tail, -1f);
            Put(bar, Section.Horns, tonic + chord[1] - 12, at, length, loudness, articulation, tail, -1f);
            Put(bar, Section.Horns, tonic + chord[2] - 12, at, length, loudness, articulation, tail, -1f);
            Put(bar, Section.Trombones, tonic + chord[0] - 24, at, length, loudness - 0.05f, articulation, tail, -3f);
            Put(bar, Section.Trombones, tonic + chord[0] - 17, at, length, loudness - 0.05f, articulation, tail, -3f);
            Put(bar, Section.Choir, tonic + chord[1], at, length, loudness - 0.1f, articulation, tail, -4f);
            Put(bar, Section.Choir, tonic + chord[2], at, length, loudness - 0.1f, articulation, tail, -4f);
            // The bass is the last thing heard, as the recording's fade is mostly its low end.
            Put(bar, Section.Cellos, tonic + chord[0] - 24, at, length, loudness, articulation, tail * 1.3f, -2f);
            Put(bar, Section.Basses, tonic + chord[0] - 36, at, length, loudness - 0.05f, articulation, tail * 1.6f, -3f);
            Put(bar, Section.Tuba, tonic + chord[0] - 36, at, length, loudness - 0.1f, articulation, tail * 1.4f, -5f);
        }

        // The subdominant after the last statement, broad and held, struck on the drums.
        public static void Broaden(Bar bar, int tonic, float beat, int beats)
        {
            Chord(bar, tonic, Subdominant, 12, 0f, beats * beat * 1.03f, 0.8f, Articulation.Normal, 0f);
            Put(bar, Section.Timpani, tonic - 19, 0f, 1f, 0.85f, Articulation.Normal, 0f, -2f);
            Put(bar, Section.BigDrum, 36, 0f, 1f, 0.75f, Articulation.Normal, 0f, -4f);
            Put(bar, Section.Crash, 60, 0f, 2f, 0.55f, Articulation.Normal, 0f, -3f);
        }

        public static void Approach(Bar bar, int tonic, float beat, int beats)
        {
            float half = beats / 2f * beat;
            Chord(bar, tonic, FlatSixth, 12, 0f, half * 1.05f, 0.78f, Articulation.Swell, 0f);
            Chord(bar, tonic, FlatSeventh, 14, half, half * 1.05f, 0.9f, Articulation.Normal, 0f);

            // A roll on the fifth, closing up and getting louder, and a cymbal that breaks on the downbeat.
            int strokes = beats * 6;
            for (int s = 0; s < strokes; s++)
            {
                float rise = s / (float)(strokes - 1);
                float at = beats * beat * (1f - MathF.Pow(1f - rise, 1.3f));
                Put(bar, Section.Timpani, tonic - 17, at, 0.2f, 0.25f + 0.6f * rise * rise, Articulation.Normal, 0f, -3f);
            }
            Put(bar, Section.Cymbal, 60, MathF.Max(beats * beat - CymbalPeak, 0f), 2f, 0.6f, Articulation.Normal, 0f, -2f);
        }

        public static void Arrive(Bar bar, int tonic, float beat, int beats)
        {
            float hold = (beats - 1) * beat;
            Chord(bar, tonic, Home, 16, 0f, hold, 0.92f, Articulation.Marcato, Ring);

            // The harp swept up through the chord, and the drums: one great stroke, and a roll under the chord dying away with it.
            for (int n = 0; n < 9; n++) Put(bar, Section.Harp, tonic - 12 + Home[n % 3] + n / 3 * 12, n * 0.045f, Ring, 0.6f + n * 0.03f, Articulation.Normal, 0f, -3f);
            Put(bar, Section.BigDrum, 36, 0f, 1f, 0.8f, Articulation.Normal, 0f, -4f);
            Put(bar, Section.Timpani, tonic - 24, 0f, 1f, 0.95f, Articulation.Normal, 0f, -2f);
            for (int s = 1; s < 24; s++)
            {
                float fade = 1f - s / 24f;
                Put(bar, Section.Timpani, tonic - 24, s * 0.12f, 0.2f, 0.15f + 0.4f * fade * fade, Articulation.Normal, 0f, -4f);
            }
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
