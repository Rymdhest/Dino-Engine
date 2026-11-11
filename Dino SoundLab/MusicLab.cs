using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Music;
using Dino_Engine.World;

namespace Dino_SoundLab
{
    /// <summary>Looking at what the composer writes, as notes on a page, before anything is heard.</summary>
    internal static class MusicLab
    {
        private static readonly string[] Names = { "C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B" };

        public static string NoteName(int midi) => Names[((midi % 12) + 12) % 12] + (midi / 12 - 1);

        private static Mode ModeNamed(string? name) => name?.ToLowerInvariant() switch
        {
            "ionian" => Mode.Ionian,
            "dorian" => Mode.Dorian,
            "phrygian" => Mode.Phrygian,
            "lydian" => Mode.Lydian,
            "mixolydian" => Mode.Mixolydian,
            _ => Mode.Aeolian,
        };

        private static Meter MeterNamed(string? name) => name switch
        {
            "3" => Meter.Three,
            "6" => Meter.Six,
            "12" => Meter.Twelve,
            _ => Meter.Four,
        };

        /// <summary>
        /// The place music is asked for: --land N for the Nth of the lands the checks use, --place haven, --place arena --arena Cathedral, or a land described by hand with --seed,
        /// --dark, --cold and --open.
        /// </summary>
        public static MusicPlace PlaceFrom(Options options)
        {
            string? place = options.Text("place");
            if (place == "haven") return MusicPlace.Home;
            if (place == "arena")
            {
                var kind = Enum.TryParse(options.Text("arena"), true, out ArenaKind parsed) ? parsed : ArenaKind.Pit;
                return new MusicPlace { Seed = (ulong)options.Int("seed", 900 + (int)kind), Arena = true, ArenaKind = kind, Dark = 0.6f, Level = 10 };
            }
            if (options.Text("land") != null) return MusicCheck.Places(options.Int("land", 1)).Last();
            return new MusicPlace { Seed = (ulong)options.Int("seed", 1) * 2654435761UL + 11UL, Dark = options.Float("dark", 0.4f), Cold = options.Float("cold", 0f), Open = options.Float("open", 0.5f), Level = options.Int("level", 5) };
        }

        /// <summary>
        /// Plays the band into a file, for as long as it's told. --story runs through what a session might do, so that transitions can be heard and measured.
        /// A list of every note goes beside it, which can be read like a score.
        /// </summary>
        public static int Play(Options options)
        {
            MusicPlace place = PlaceFrom(options);
            float seconds = options.Float("seconds", 40f);
            float intensity = options.Float("intensity", 0.5f);
            string path = options.Text("out") ?? "music.wav";
            var engine = new MusicEngine(place);
            var mood = Enum.TryParse(options.Text("mood"), true, out MusicMood parsed) ? parsed : engine.Score.Main;
            bool story = options.Text("story") == "true";
            bool sweep = options.Text("sweep") == "true";

            // --to Battle --at 14 changes style part of the way through, and --back 30 changes back, to hear how one style hands over to another.
            var to = Enum.TryParse(options.Text("to"), true, out MusicMood next) ? next : mood;
            float at = options.Float("at", 14f);
            float back = options.Float("back", float.MaxValue);

            var clock = System.Diagnostics.Stopwatch.StartNew();

            // --solo Cellos,Basses leaves only those sections sounding, to see what a mix is made of.
            if (options.Text("solo") is string solo)
            {
                for (int i = 0; i < engine.Band.Gains.Length; i++) engine.Band.Gains[i] = solo.Split(',').Contains(((Section)i).ToString()) ? 1f : 0f;
            }

            Console.WriteLine($"Score written in {clock.Elapsed.TotalMilliseconds:0} ms. Key {NoteName(engine.Score.Tonic)}.");
            foreach (MusicMood each in Enum.GetValues<MusicMood>())
            {
                if (each == MusicMood.Silence || !engine.Score.Has(each)) continue;
                Score.Setting setting = engine.Score.For(each);
                Console.WriteLine($"  {each}: {setting.Style.Name}; {setting.Mode.Name}, {setting.Tempo:0} bpm, {setting.Meter.Beats}x{setting.Meter.Divisions}, key {NoteName(setting.Tonic)}");
            }

            var notes = new List<string> { "seconds,section,pitch,length,loudness,articulation,bar" };
            engine.Listener = (bar, at) =>
            {
                double start = at / (double)AudioFormat.SampleRate;
                foreach (Note note in bar.Notes) notes.Add(FormattableString.Invariant($"{start + note.Start:0.000},{note.Section},{note.Pitch},{note.Length:0.000},{note.Loudness:0.00},{note.Articulation},{bar.Label}"));
                if (bar.Label == "rest") notes.Add(FormattableString.Invariant($"{start:0.000},Rest,0,{bar.Seconds:0.000},0,Normal,rest"));
            };

            int frames = (int)(seconds * AudioFormat.SampleRate);
            var left = new float[frames];
            var right = new float[frames];
            const int Step = 1024;
            var chunkLeft = new float[Step];
            var chunkRight = new float[Step];
            clock.Restart();
            for (int done = 0; done < frames; done += Step)
            {
                float t = done / (float)AudioFormat.SampleRate;
                var intent = new MusicIntent { Mood = t >= at && t < back ? to : mood, Intensity = sweep ? t / seconds : intensity };
                if (story) intent = Scene(t);
                engine.Set(intent);
                engine.Render(chunkLeft, chunkRight, Step);
                int count = Math.Min(Step, frames - done);
                Array.Copy(chunkLeft, 0, left, done, count);
                Array.Copy(chunkRight, 0, right, done, count);
            }
            double spent = clock.Elapsed.TotalSeconds;

            WavFile.Write(path, left, right);
            File.WriteAllLines(Path.ChangeExtension(path, ".csv"), notes);
            float peak = 0f;
            foreach (float value in left) peak = MathF.Max(peak, MathF.Abs(value));
            Console.WriteLine($"{seconds:0} s of music, {spent / seconds * 100.0:0.0}% of a core, peak {AudioFormat.GainToDecibels(peak):0.0} dBFS, {Loudness.Integrated(left, right):0.0} LUFS, {notes.Count} notes");
            return 0;
        }

        private static MusicIntent Scene(float t)
        {
            // Out in a land, into its keep and its tyrant's fight, the tyrant falling, and the land again.
            if (t < 40f) return new MusicIntent { Mood = MusicMood.Land, Intensity = 0.5f };
            if (t < 75f) return new MusicIntent { Mood = MusicMood.Boss, Intensity = 0.72f + (t - 40f) * 0.006f };
            if (t < 75.2f) return new MusicIntent { Mood = MusicMood.Land, Intensity = 0.5f, Cue = MusicCue.Victory };
            return new MusicIntent { Mood = MusicMood.Land, Intensity = 0.5f };
        }

        /// <summary>One note of each instrument, to compare with recordings of the real thing.</summary>
        public static int Notes(Options options)
        {
            string folder = options.Text("out") ?? "notes";
            Directory.CreateDirectory(folder);
            float seconds = options.Float("seconds", 3f);
            (Section section, int pitch, string name)[] tests =
            {
                (Section.Flute, 72, "Flute.C5"), (Section.Flute, 81, "Flute.A5"), (Section.Oboe, 69, "Oboe.A4"), (Section.Horns, 48, "Horn.C3"), (Section.Horns, 60, "Horn.C4"), (Section.Horns, 67, "Horn.G4"),
                (Section.Trombones, 48, "Trombone.C3"), (Section.Trombones, 60, "Trombone.C4"), (Section.Violins, 69, "Violin.A4"), (Section.Violins, 76, "Violin.E5"), (Section.Cellos, 43, "Cello.G2"), (Section.Cellos, 50, "Cello.D3"),
                (Section.Fiddle, 69, "Fiddle.A4"), (Section.Choir, 60, "Choir.C4"), (Section.Harp, 60, "Harp.C4"), (Section.Harp, 48, "Harp.C3"), (Section.Lute, 52, "Lute.E3"), (Section.Pizzicato, 43, "Pizz.G2"),
                (Section.Timpani, 45, "Timpani.A2"), (Section.BigDrum, 36, "BigDrum.C2"), (Section.FrameDrum, 48, "Frame.C3"), (Section.Celesta, 72, "Celesta.C5"), (Section.Bells, 60, "Bells.C4"), (Section.Tambourine, 60, "Tambourine"), (Section.Cymbal, 60, "Cymbal"),
                (Section.Piano, 33, "Piano.A1"), (Section.Piano, 48, "Piano.C3"), (Section.Piano, 60, "Piano.C4"), (Section.Piano, 69, "Piano.A4"), (Section.Piano, 84, "Piano.C6"), (Section.Piano, 96, "Piano.C7"),
                (Section.Guitar, 45, "Guitar.A2"), (Section.Guitar, 64, "Guitar.E4"), (Section.Dulcimer, 69, "Dulcimer.A4"), (Section.Pad, 60, "Pad.C4"), (Section.SoloCello, 50, "SoloCello.D3"),
                (Section.EnglishHorn, 65, "EnglishHorn.F4"), (Section.Bassoon, 46, "Bassoon.Bb2"), (Section.AltoFlute, 67, "AltoFlute.G4"), (Section.BassFlute, 60, "BassFlute.C4"), (Section.Ocarina, 76, "Ocarina.E5"),
                (Section.Tuba, 38, "Tuba.D2"), (Section.BassClarinet, 45, "BassClarinet.A2"), (Section.MutedViolins, 74, "MutedViolins.D5"), (Section.MutedCellos, 48, "MutedCellos.C3"), (Section.Voices, 60, "Voices.C4"),
                (Section.HandDrum, 48, "HandDrum"), (Section.RimDrum, 60, "RimDrum"), (Section.Shaker, 60, "Shaker"),
            };
            foreach ((Section section, int pitch, string name) in tests)
            {
                foreach ((string dynamic, float loudness) in new[] { ("pp", 0.1f), ("mf", 0.5f), ("ff", 0.95f) })
                {
                    var band = new Orchestra(5);
                    band.Schedule(new Note { Section = section, Pitch = pitch, Start = 0.02f, Length = seconds - 0.7f, Loudness = loudness, Articulation = Articulation.Normal }, 0);
                    int frames = (int)(seconds * AudioFormat.SampleRate);
                    var left = new float[frames];
                    var right = new float[frames];
                    band.Render(left, right, frames);
                    WavFile.Write(Path.Combine(folder, $"{name}.{dynamic}.wav"), left, right);
                }
            }
            Console.WriteLine($"{tests.Length * 3} notes to {folder}");
            return 0;
        }

        /// <summary>Writes tunes, prints a few, and compares a lot of them with what real tunes were measured to do.</summary>
        public static int Tunes(Options options)
        {
            int count = options.Int("count", 300);
            int show = options.Int("show", 3);
            ulong seed = (ulong)options.Int("seed", 1);
            Mode mode = ModeNamed(options.Text("mode"));
            Meter meter = MeterNamed(options.Text("meter"));
            var style = Enum.TryParse(options.Text("style"), true, out MelodicStyle parsed) ? parsed : MelodicStyle.Flowing;
            int tonic = options.Int("tonic", 62);

            int[] bins = new int[5];
            float strongHits = 0f, strongTotal = 0f, weakHits = 0f, weakTotal = 0f;
            int leaps = 0, reversals = 0, sameRhythm = 0, samePitch = 0, bars = 0, homeEnd = 0;
            var spans = new List<int>();

            for (int i = 0; i < count; i++)
            {
                var random = new SoundRandom(seed * 100003UL + (ulong)i);
                Chord[] harmony = Harmony.Draw(random, mode);
                var brief = new TuneBrief { Meter = meter, Mode = mode, Style = style, Harmony = harmony };
                Tune tune = Songwriter.Write(random, brief);
                if (i < show) Print(tune, mode, tonic, style);

                Tone[] tones = tune.Tones;
                int lowest = int.MaxValue, highest = int.MinValue;
                for (int n = 0; n < tones.Length; n++)
                {
                    int pitch = mode.Pitch(tonic, tones[n].Degree);
                    lowest = Math.Min(lowest, pitch);
                    highest = Math.Max(highest, pitch);

                    int bar = Math.Min((int)(tones[n].Beat / meter.Beats), harmony.Length - 1);
                    bool member = harmony[bar].Contains(tones[n].Degree);
                    if (meter.Weight(tones[n].Beat - bar * meter.Beats) >= 0.6f)
                    {
                        strongTotal++;
                        if (member) strongHits++;
                    }
                    else
                    {
                        weakTotal++;
                        if (member) weakHits++;
                    }

                    if (n == 0) continue;
                    int step = Math.Abs(tones[n].Degree - tones[n - 1].Degree);
                    bins[Math.Min(step == 0 ? 0 : step == 1 ? 1 : step == 2 ? 2 : step <= 4 ? 3 : 4, 4)]++;
                    if (n >= 2)
                    {
                        int before = tones[n - 1].Degree - tones[n - 2].Degree;
                        if (Math.Abs(before) >= 3)
                        {
                            leaps++;
                            if (before * (tones[n].Degree - tones[n - 1].Degree) < 0) reversals++;
                        }
                    }
                }
                spans.Add(highest - lowest);
                if (Chord.Wrap(tones[^1].Degree) == 0) homeEnd++;

                var rhythms = new HashSet<string>();
                var shapes = new HashSet<string>();
                for (int bar = 0; bar < harmony.Length; bar++)
                {
                    var inBar = tones.Where(t => (int)(t.Beat / meter.Beats) == bar).ToArray();
                    if (inBar.Length < 2) continue;
                    string rhythm = string.Join(",", inBar.Select(t => t.Length.ToString("0.00")));
                    string shape = rhythm + "|" + string.Join(",", inBar.Select(t => t.Degree - inBar[0].Degree));
                    bars++;
                    if (!rhythms.Add(rhythm)) sameRhythm++;
                    if (!shapes.Add(shape)) samePitch++;
                }
            }

            float total = bins.Sum();
            spans.Sort();
            Console.WriteLine($"{count} tunes in {mode.Name}, compared with 1034 real ones:");
            Console.WriteLine($"  repeats {bins[0] / total * 100f,5:0.0}%   (10.8)");
            Console.WriteLine($"  steps   {bins[1] / total * 100f,5:0.0}%   (52.5)");
            Console.WriteLine($"  thirds  {bins[2] / total * 100f,5:0.0}%   (22.9)");
            Console.WriteLine($"  4th/5th {bins[3] / total * 100f,5:0.0}%   ( 9.7)");
            Console.WriteLine($"  wider   {bins[4] / total * 100f,5:0.0}%   ( 4.1)");
            Console.WriteLine($"  turns back after a leap {reversals / (float)Math.Max(leaps, 1) * 100f,5:0.0}%   (62.7)");
            Console.WriteLine($"  chord tones, strong beats {strongHits / strongTotal * 100f,5:0.0}%   (87.3), others {weakHits / Math.Max(weakTotal, 1f) * 100f,5:0.0}%   (66.5)");
            Console.WriteLine($"  bars reusing a rhythm {sameRhythm / (float)bars * 100f,5:0.0}%   (69), reusing notes too {samePitch / (float)bars * 100f,5:0.0}%   (33)");
            Console.WriteLine($"  ends at home {homeEnd / (float)count * 100f,5:0.0}%   (80), span median {spans[spans.Count / 2]} semitones   (17)");
            return 0;
        }

        private static void Print(Tune tune, Mode mode, int tonic, MelodicStyle style)
        {
            Console.WriteLine($"{mode.Name} on {NoteName(tonic)}, {style}, score {tune.Score:0.00}");
            for (int bar = 0; bar < tune.Bars; bar++)
            {
                var notes = tune.Tones.Where(t => (int)(t.Beat / tune.Meter.Beats) == bar).Select(t => $"{NoteName(mode.Pitch(tonic, t.Degree))}({t.Length:0.##})");
                Chord chord = tune.Harmony[bar];
                Console.WriteLine($"  {bar + 1}. [{NoteName(mode.Pitch(tonic, chord.Root))} {chord.Colour}] {string.Join(" ", notes)}");
            }
        }
    }
}
