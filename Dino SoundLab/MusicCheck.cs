using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Music;
using Dino_Engine.World;
using System.Diagnostics;

namespace Dino_SoundLab
{
    /// <summary>
    /// What can be known about music without ears. Nothing here says a tune is good; it says when one is plainly wrong: out of tune with itself, out of an instrument's range, clashing
    /// with its own harmony, not doing what it was told, or the same as the last land's. The numbers it compares melodies with are what a thousand real folk tunes do.
    /// </summary>
    internal static class MusicCheck
    {
        private static int _failures;
        private static int _checks;

        private static void Expect(bool condition, string what)
        {
            _checks++;
            if (condition) return;
            _failures++;
            Console.WriteLine($"  FAIL {what}");
        }

        public static int Run(Options options)
        {
            var clock = Stopwatch.StartNew();
            int lands = options.Int("count", 12);

            // --part variety runs only that part, for a quick look.
            string? only = options.Text("part")?.ToLowerInvariant();
            void Part(string name, Action check)
            {
                if (only != null && only != name.ToLowerInvariant()) return;
                Console.WriteLine(name);
                check();
            }

            Part("Tunes", CheckTunes);
            Part("Instruments", CheckInstruments);
            Part("Scores", () =>
            {
                foreach (MusicPlace place in Places(lands).Concat(Specials())) CheckScore(place);
            });
            Part("Variety", () => CheckVariety(Math.Max(lands * 10, 120)));
            Part("Adapting", CheckAdapting);
            Part("Sound", CheckSound);
            Part("Balance", CheckBalance);
            Part("Transitions", () => CheckTransitions(options.Int("transitions", 4)));

            Console.WriteLine($"{_checks} checks, {_failures} failed, in {clock.Elapsed.TotalSeconds:0.0} s.");
            return _failures == 0 ? 0 : 1;
        }

        /// <summary>Lands of every character, as the game would make them: each its own seed, some bright and some grim, some cold, some open.</summary>
        public static IEnumerable<MusicPlace> Places(int count)
        {
            for (int i = 1; i <= count; i++)
            {
                var random = new SoundRandom((ulong)i * 7919UL + 3UL);
                yield return new MusicPlace { Seed = (ulong)i * 2654435761UL + 11UL, Dark = random.Float(), Cold = random.Chance(0.2f) ? random.Float() : 0f, Open = random.Float(), Level = 1 + random.Int(40) };
            }
        }

        // The Haven, and an arena of each kind.
        private static IEnumerable<MusicPlace> Specials()
        {
            yield return MusicPlace.Home;
            foreach (ArenaKind kind in Enum.GetValues<ArenaKind>()) yield return new MusicPlace { Seed = 900UL + (ulong)kind, Arena = true, ArenaKind = kind, Dark = 0.6f, Level = 10 };
        }

        private static void CheckTunes()
        {
            foreach (Mode mode in new[] { Mode.Aeolian, Mode.Dorian, Mode.Ionian, Mode.Mixolydian, Mode.Lydian, Mode.Phrygian })
            {
                foreach (MelodicStyle style in new[] { MelodicStyle.Flowing, MelodicStyle.Sparse, MelodicStyle.Hymn, MelodicStyle.Folk })
                {
                    int steps = 0, intervals = 0, hard = 0, strong = 0, strongHits = 0, wide = 0;
                    Meter meter = style == MelodicStyle.Folk ? Meter.Three : Meter.Four;
                    for (int i = 0; i < 40; i++)
                    {
                        var random = new SoundRandom((ulong)i * 977UL + 5UL);
                        Chord[] harmony = Harmony.Draw(random, mode);
                        Tune tune = Songwriter.Write(random, new TuneBrief { Mode = mode, Meter = meter, Style = style, Harmony = harmony, Candidates = 150 });
                        for (int n = 0; n < tune.Tones.Length; n++)
                        {
                            Tone tone = tune.Tones[n];
                            int bar = Math.Min((int)(tone.Beat / tune.Meter.Beats), harmony.Length - 1);
                            if (tune.Meter.Weight(tone.Beat - bar * tune.Meter.Beats) >= 0.6f)
                            {
                                strong++;
                                if (harmony[bar].Contains(tone.Degree)) strongHits++;
                            }
                            if (n == 0) continue;
                            int leap = Math.Abs(tune.Tones[n].Degree - tune.Tones[n - 1].Degree);
                            int semitones = Math.Abs(mode.Pitch(0, tune.Tones[n].Degree) - mode.Pitch(0, tune.Tones[n - 1].Degree));
                            intervals++;
                            if (leap == 1) steps++;
                            if (leap >= 5) wide++;
                            if (semitones is 6 or 10 or 11 || semitones > 12) hard++;
                        }
                        Expect(Chord.Wrap(tune.Tones[^1].Degree) == 0, $"a {style} tune in {mode.Name} doesn't end at home");
                        Expect(tune.Tones.Zip(tune.Tones.Skip(1)).All(pair => pair.Second.Beat >= pair.First.Beat + pair.First.Length - 1e-4f), $"a {style} tune in {mode.Name} has notes on top of each other");
                    }

                    float stepShare = steps / (float)intervals;
                    float strongShare = strongHits / (float)strong;
                    Expect(stepShare is > 0.36f and < 0.74f, $"{style} in {mode.Name}: {stepShare * 100f:0.0}% steps, where real tunes have 52.5%");
                    Expect(strongShare > 0.72f, $"{style} in {mode.Name}: only {strongShare * 100f:0.0}% chord tones on strong beats, where real tunes have 87%");
                    Expect(hard / (float)intervals < 0.02f, $"{style} in {mode.Name}: {hard / (float)intervals * 100f:0.0}% of its leaps are unsingable");
                    Expect(wide / (float)intervals < 0.07f, $"{style} in {mode.Name}: {wide / (float)intervals * 100f:0.0}% wide leaps");
                }
            }

            // The theme is what ties a land's music together: its shape has to survive into its tyrant's fight. On average over many lands, as one tune says little.
            float kinship = 0f;
            int lands = 0;
            foreach (MusicPlace place in Places(24))
            {
                var score = new Score(place);
                kinship += Correlation(Songwriter.ShapeOf(score.For(MusicMood.Land).First), Songwriter.ShapeOf(score.For(MusicMood.Boss).First));
                lands++;
            }
            kinship /= lands;
            Console.WriteLine($"  A land's theme and its tyrant's correlate by {kinship:0.00}.");
            Expect(kinship > 0.5f, $"the theme's shape doesn't survive into the fight ({kinship:0.00})");

            MusicPlace first = Places(5).Last();
            Expect(new Score(first).For(MusicMood.Land).First.Tones.SequenceEqual(new Score(first).For(MusicMood.Land).First.Tones), "one land gave two different themes");
        }

        private static float Correlation(int[] a, int[] b)
        {
            int n = Math.Min(a.Length, b.Length);
            float meanA = (float)a.Take(n).Average(), meanB = (float)b.Take(n).Average();
            float dot = 0f, ea = 0f, eb = 0f;
            for (int i = 0; i < n; i++)
            {
                dot += (a[i] - meanA) * (b[i] - meanB);
                ea += (a[i] - meanA) * (a[i] - meanA);
                eb += (b[i] - meanB) * (b[i] - meanB);
            }
            return dot / MathF.Sqrt(MathF.Max(ea * eb, 1e-6f));
        }

        // What each instrument can really play, kept apart from the arranger's own idea of it.
        private static readonly (int low, int high)[] Compass = BuildCompass();

        private static (int, int)[] BuildCompass()
        {
            var compass = new (int, int)[Enum.GetValues<Section>().Length];
            Array.Fill(compass, (20, 110));
            compass[(int)Section.Flute] = (59, 98);
            compass[(int)Section.Oboe] = (58, 92);
            compass[(int)Section.Clarinet] = (50, 92);
            compass[(int)Section.Horns] = (34, 78);
            compass[(int)Section.Trombones] = (34, 74);
            compass[(int)Section.Trumpets] = (54, 86);
            compass[(int)Section.Violins] = (55, 100);
            compass[(int)Section.Violas] = (48, 88);
            compass[(int)Section.Cellos] = (36, 78);
            compass[(int)Section.Basses] = (28, 62);
            compass[(int)Section.Fiddle] = (55, 100);
            compass[(int)Section.Choir] = (43, 84);
            compass[(int)Section.Piano] = (21, 108);
            compass[(int)Section.Guitar] = (40, 88);
            compass[(int)Section.Dulcimer] = (48, 93);
            compass[(int)Section.SoloCello] = (36, 81);
            compass[(int)Section.EnglishHorn] = (52, 84);
            compass[(int)Section.Bassoon] = (34, 75);
            compass[(int)Section.AltoFlute] = (55, 93);
            compass[(int)Section.BassFlute] = (48, 86);
            compass[(int)Section.Ocarina] = (60, 90);
            compass[(int)Section.Tuba] = (26, 65);
            compass[(int)Section.BassClarinet] = (34, 77);
            compass[(int)Section.MutedViolins] = (55, 100);
            compass[(int)Section.MutedCellos] = (36, 81);
            compass[(int)Section.Voices] = (43, 84);
            return compass;
        }

        // Plays a mood for a while on paper, with no sound: everything about the notes can be checked this way, and fast.
        private static List<Bar> Write(MusicPlace place, MusicMood mood, float intensity, int bars)
        {
            var score = new Score(place);
            var composer = new Composer(score, place.Seed);
            var written = new List<Bar>();
            var intent = new MusicIntent { Mood = mood, Intensity = intensity };
            for (int i = 0; i < bars; i++) written.Add(composer.Next(intent));
            return written;
        }

        private static IEnumerable<MusicMood> MoodsOf(Score score) => Enum.GetValues<MusicMood>().Where(mood => mood != MusicMood.Silence && score.Has(mood));

        private static void CheckScore(MusicPlace place)
        {
            var score = new Score(place);
            foreach (MusicMood mood in MoodsOf(score))
            {
                Score.Setting setting = score.For(mood);
                string name = $"{setting.Style.Name} ({mood}, seed {place.Seed})";
                Expect(setting.Tempo is > 36f and < 190f, $"{name} at {setting.Tempo} bpm");

                foreach (float intensity in new[] { 0.35f, 0.8f })
                {
                    List<Bar> bars = Write(place, mood, intensity, 48);
                    int notes = 0, clashes = 0, beats = 0;
                    foreach (Bar bar in bars)
                    {
                        foreach (Note note in bar.Notes)
                        {
                            notes++;
                            (int low, int high) = Compass[(int)note.Section];
                            Expect(note.Pitch >= low && note.Pitch <= high, $"{name}: {note.Section} asked for {MusicLab.NoteName(note.Pitch)}");
                            Expect(note.Length > 0f && note.Start >= 0f && float.IsFinite(note.Loudness) && float.IsFinite(note.Start), $"{name}: a nonsense note");
                        }
                        (int c, int b) = Clashes(bar);
                        clashes += c;
                        beats += b;
                    }
                    Expect(notes > 30, $"{name}: only {notes} notes in forty-eight bars at {intensity}");

                    // Notes that grate on a strong beat, in the sound's middle and low range, where the ear is least forgiving.
                    float share = clashes / (float)Math.Max(beats, 1);
                    Expect(share < 0.12f, $"{name}: {share * 100f:0.0}% of its strong moments clash at {intensity}");
                }
            }
        }

        // Two notes sounding together a semitone apart at the start of a bar, where passing dissonance has no excuse.
        private static (int clashes, int moments) Clashes(Bar bar)
        {
            var sounding = new List<int>();
            foreach (Note note in bar.Notes)
            {
                if (note.Start < 0.06f && !IsDrum(note.Section)) sounding.Add(note.Pitch);
            }
            if (sounding.Count < 2) return (0, 0);
            int clashes = 0;
            for (int a = 0; a < sounding.Count; a++)
            {
                for (int b = a + 1; b < sounding.Count; b++)
                {
                    int interval = Math.Abs(sounding[a] - sounding[b]);
                    if (interval == 1 || interval == 13) clashes = 1;
                }
            }
            return (clashes, 1);
        }

        private static bool IsDrum(Section section) => section is Section.Timpani or Section.BigDrum or Section.FrameDrum or Section.Tambourine or Section.Cymbal or Section.HandDrum or Section.RimDrum or Section.Shaker;

        // ---- Instruments. ----

        // A piano, measured as a real one is: its partials stretched by the string's stiffness, brighter when struck harder, dying faster up high than down low, and not before its time.
        private static void CheckInstruments()
        {
            foreach (int key in new[] { 33, 60, 84 })
            {
                (float[] quiet, _) = Note(Section.Piano, key, 0.15f, 3f);
                (float[] loud, _) = Note(Section.Piano, key, 0.95f, 3f);
                Expect(Centroid(loud, 0.4f) > Centroid(quiet, 0.4f) * 1.2f, $"a piano's {MusicLab.NoteName(key)} is no brighter struck hard ({Centroid(quiet, 0.4f):0} and {Centroid(loud, 0.4f):0} Hz)");
                Expect(quiet.All(float.IsFinite) && loud.All(float.IsFinite), $"a piano's {MusicLab.NoteName(key)} has NaN in it");

                // The twelfth partial of a stiff string is sharp of twelve times its fundamental by what Fletcher's formula says.
                float stretched = PianoVoice.PartialHertz(key, 12) / (12f * PianoVoice.PartialHertz(key, 1) / MathF.Sqrt(1f + PianoVoice.Inharmonicity(key)));
                Expect(stretched > 1.0005f, $"a piano's {MusicLab.NoteName(key)} has harmonic partials");
            }

            float Fall(int key)
            {
                (float[] note, _) = Note(Section.Piano, key, 0.6f, 4f);
                return Level(note, 0.5f, 0.3f) - Level(note, 2.5f, 0.3f);
            }
            float low = Fall(36), middle = Fall(60), high = Fall(88);
            Console.WriteLine($"  A piano falls {low:0} dB from half a second to two and a half low down, {middle:0} in the middle and {high:0} high up.");
            Expect(low < middle && middle < high, "a piano's notes don't die faster as they go up");
            Expect(middle is > 6f and < 30f, $"a middle C falls {middle:0} dB in two seconds, where a real one falls ten or so");

            // A note that isn't made of numbers is never played, and something that isn't a number getting into the band costs it a moment, not the rest of the session.
            var poisoned = new Orchestra(4);
            poisoned.Schedule(new Note { Section = Section.Violins, Pitch = 69, Start = 0.01f, Length = 0.5f, Loudness = float.NaN }, 0);
            var half = new float[AudioFormat.SampleRate / 2];
            var halfRight = new float[half.Length];
            poisoned.Render(half, halfRight, half.Length);
            Expect(half.All(value => MathF.Abs(value) < 1e-6f), "a note that isn't made of numbers was played");

            poisoned.Gains[(int)Section.Violins] = float.NaN;
            poisoned.Schedule(new Note { Section = Section.Violins, Pitch = 69, Start = 0.01f, Length = 0.3f, Loudness = 0.6f }, poisoned.Now);
            poisoned.Render(half, halfRight, half.Length);
            poisoned.Gains[(int)Section.Violins] = 1f;
            poisoned.Schedule(new Note { Section = Section.Flute, Pitch = 72, Start = 0.2f, Length = 0.6f, Loudness = 0.6f }, poisoned.Now);
            var mixed = new float[AudioFormat.SampleRate];
            var mixedRight = new float[mixed.Length];
            poisoned.Render(mixed, mixedRight, mixed.Length);
            Expect(half.All(float.IsFinite) && mixed.All(float.IsFinite) && mixedRight.All(float.IsFinite), "something that isn't a number poisoned the band");
            Expect(poisoned.Faults > 0, "the band didn't notice it had gone bad");
            Expect(mixed.Skip(AudioFormat.SampleRate / 4).Take(12000).Max(MathF.Abs) > 0.005f, "the band went quiet for good after going bad");

            // A twelve-string's every note is two strings.
            var band = new Orchestra(3);
            band.Schedule(new Note { Section = Section.Guitar, Pitch = 45, Start = 0.01f, Length = 2f, Loudness = 0.6f }, 0);
            var left = new float[AudioFormat.SampleRate];
            var right = new float[AudioFormat.SampleRate];
            band.Render(left, right, left.Length);
            Expect(left.All(float.IsFinite) && left.Max(MathF.Abs) > 0.005f, "a guitar note is silent");
            Expect(Centroid(left, 0.5f) > 200f, "a twelve-string's low note has no octave string over it");
        }

        private static (float[] left, float[] right) Note(Section section, int pitch, float loudness, float seconds)
        {
            var band = new Orchestra(5);
            band.Schedule(new Note { Section = section, Pitch = pitch, Start = 0.01f, Length = seconds - 0.5f, Loudness = loudness }, 0);
            int frames = (int)(seconds * AudioFormat.SampleRate);
            var left = new float[frames];
            var right = new float[frames];
            band.Render(left, right, frames);
            return (left, right);
        }

        private static float Centroid(float[] signal, float seconds)
        {
            int length = Math.Min(signal.Length, 1 << (int)MathF.Ceiling(MathF.Log2(seconds * AudioFormat.SampleRate)));
            var fft = new Fft(length);
            var real = new float[length];
            var imaginary = new float[length];
            for (int i = 0; i < length && i < signal.Length; i++) real[i] = signal[i] * (0.5f - 0.5f * MathF.Cos(MathF.Tau * i / length));
            fft.Transform(real, imaginary);
            double weighted = 0.0, total = 1e-12;
            for (int k = 1; k < length / 2; k++)
            {
                double power = real[k] * real[k] + imaginary[k] * imaginary[k];
                weighted += power * k * AudioFormat.SampleRate / (double)length;
                total += power;
            }
            return (float)(weighted / total);
        }

        private static float Level(float[] signal, float at, float span)
        {
            int start = (int)(at * AudioFormat.SampleRate), end = Math.Min(signal.Length, start + (int)(span * AudioFormat.SampleRate));
            double energy = 1e-12;
            for (int i = start; i < end; i++) energy += signal[i] * signal[i];
            return 10f * MathF.Log10((float)(energy / Math.Max(end - start, 1)));
        }

        // ---- Variety: no two lands may sound the same. ----

        private readonly record struct Character(string Kind, Section Lead, string Mode, int Beats, int Divisions, float Tempo, int Tonic, string Sections, int[] Theme, float Density, float Register, string Figures);

        private static Character Describe(MusicPlace place)
        {
            var score = new Score(place);
            Score.Setting setting = score.For(MusicMood.Land);
            Style style = setting.Style;
            Part lead = style.Parts.First(part => part.Writer == Textures.Melody);
            List<Bar> bars = Write(place, MusicMood.Land, 0.5f, 24);
            var notes = bars.SelectMany(bar => bar.Notes).ToList();
            float sounding = (float)bars.Where(bar => bar.Label != "rest").Sum(bar => bar.Seconds);
            string sections = string.Join(",", notes.Select(note => note.Section).Distinct().OrderBy(section => section));
            int[] theme = setting.First.Tones.Take(16).SelectMany(tone => new[] { tone.Degree, (int)(tone.Beat * 12f), (int)(tone.Length * 12f) }).ToArray();

            // How the accompaniment goes, which is heard at once: each part's figure and the rhythm of its drums.
            string figures = string.Join(";", style.Parts.Select(part => $"{part.Section}:{part.Name}:{(part.Steps == null ? "" : string.Concat(part.Steps.Select(on => on ? 'x' : '.')))}"));
            return new Character(style.Name.Split(',')[0], lead.Section, setting.Mode.Name, setting.Meter.Beats, setting.Meter.Divisions, setting.Tempo, setting.Tonic % 12, sections, theme,
                notes.Count / MathF.Max(sounding, 1f), notes.Count == 0 ? 0f : (float)notes.Average(note => note.Pitch), figures);
        }

        private static void CheckVariety(int count)
        {
            var places = Places(count).ToArray();
            var lands = new Character[places.Length];
            Parallel.For(0, places.Length, i => lands[i] = Describe(places[i]));

            float Entropy(IEnumerable<string> values)
            {
                var groups = values.GroupBy(value => value).Select(group => group.Count() / (float)count).ToArray();
                return -groups.Sum(p => p * MathF.Log2(p));
            }

            var kinds = lands.GroupBy(land => land.Kind).ToDictionary(group => group.Key, group => group.Count());
            Console.WriteLine($"  Over {count} lands: {string.Join(", ", kinds.OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Key} {pair.Value}"))}.");
            Console.WriteLine($"  Different kinds of land piece {kinds.Count}, leads {lands.Select(l => l.Lead).Distinct().Count()}, modes {lands.Select(l => l.Mode).Distinct().Count()}, metres {lands.Select(l => (l.Beats, l.Divisions)).Distinct().Count()}, keys {lands.Select(l => l.Tonic).Distinct().Count()}, bands {lands.Select(l => l.Sections).Distinct().Count()}.");
            Console.WriteLine($"  Entropy in bits: kind {Entropy(lands.Select(l => l.Kind)):0.00}, lead {Entropy(lands.Select(l => l.Lead.ToString())):0.00}, mode {Entropy(lands.Select(l => l.Mode)):0.00}, key {Entropy(lands.Select(l => l.Tonic.ToString())):0.00}.");
            Console.WriteLine($"  Tempo from {lands.Min(l => l.Tempo):0} to {lands.Max(l => l.Tempo):0} bpm, notes a second from {lands.Min(l => l.Density):0.0} to {lands.Max(l => l.Density):0.0}.");

            Expect(kinds.Count == Enum.GetValues<Idiom>().Length, $"only {kinds.Count} kinds of land piece came up in {count} lands");
            Expect(kinds.Values.Max() < count * 0.42f, $"one kind of piece is {kinds.Values.Max() * 100 / count}% of all lands");
            foreach (var same in lands.Select((land, i) => (land, i)).GroupBy(pair => pair.land.Theme, new SequenceComparer()).Where(group => group.Count() > 1))
            {
                Console.WriteLine($"    Lands {string.Join(" and ", same.Select(pair => pair.i + 1))} share a tune: {string.Join(", ", same.Select(pair => $"{pair.land.Kind} in {pair.land.Mode} {pair.land.Beats}/{pair.land.Divisions}"))}");
            }
            Expect(lands.Select(l => l.Theme).Distinct(new SequenceComparer()).Count() == count, "two lands share a theme");

            // Two lands are the same piece, but for its tune, when they have the same band playing the same figures, in the same mode, metre and key, at nearly the same speed: none may be.
            int twins = 0;
            for (int a = 0; a < count; a++)
            {
                for (int b = a + 1; b < count; b++)
                {
                    Character x = lands[a], y = lands[b];
                    if (x.Sections == y.Sections && x.Figures == y.Figures && x.Mode == y.Mode && x.Beats == y.Beats && x.Divisions == y.Divisions && x.Tonic == y.Tonic && MathF.Abs(x.Tempo - y.Tempo) < 4f)
                    {
                        twins++;
                        Console.WriteLine($"    Lands {a + 1} and {b + 1} are alike: {x.Kind} on {x.Lead}, {x.Mode}, {x.Tempo:0} and {y.Tempo:0} bpm, {x.Figures}");
                    }
                }
            }
            Expect(twins == 0, $"{twins} pairs of lands have the same band and figures, mode, metre, key and speed");

            // And a land's character shows: grim lands go dark more often than bright ones.
            float GrimShare(Func<MusicPlace, bool> which)
            {
                var chosen = places.Select((place, i) => (place, land: lands[i])).Where(pair => which(pair.place)).ToArray();
                return chosen.Length == 0 ? 0f : chosen.Count(pair => pair.land.Kind == nameof(Idiom.Vigil)) / (float)chosen.Length;
            }
            float grim = GrimShare(place => place.Dark > 0.7f), bright = GrimShare(place => place.Dark < 0.3f);
            Console.WriteLine($"  A vigil plays in {grim * 100f:0}% of grim lands and {bright * 100f:0}% of bright ones.");
            Expect(grim > bright * 1.5f, "a grim land is no likelier to get a dark piece than a bright one");
        }

        private sealed class SequenceComparer : IEqualityComparer<int[]>
        {
            public bool Equals(int[]? a, int[]? b) => a != null && b != null && a.SequenceEqual(b);
            public int GetHashCode(int[] values) => values.Aggregate(17, (hash, value) => hash * 31 + value);
        }

        // ---- Adapting. ----

        private static void CheckAdapting()
        {
            MusicPlace land = Places(9).Last();
            MusicPlace arena = Specials().Skip(2).First();

            // Intensity has to mean something: more players, playing more, playing louder.
            foreach ((MusicPlace place, MusicMood mood) in new[] { (land, MusicMood.Land), (land, MusicMood.Boss), (MusicPlace.Home, MusicMood.Haven), (arena, MusicMood.Arena) })
            {
                (float quietNotes, float quietLevel, int quietSections) = Measure(place, mood, 0.15f);
                (float loudNotes, float loudLevel, int loudSections) = Measure(place, mood, 0.95f);
                Expect(loudSections > quietSections, $"{mood}: {quietSections} sections at low intensity and {loudSections} at high");
                Expect(loudNotes > quietNotes, $"{mood}: no more notes at high intensity ({quietNotes:0.0} and {loudNotes:0.0} a bar)");
                Expect(loudLevel > quietLevel, $"{mood}: no louder at high intensity");
            }

            // A tyrant can't wait for a phrase: it comes in on a beat of what was playing, whenever it's asked for.
            var left = new float[1024];
            var right = new float[1024];
            for (int attempt = 0; attempt < 24; attempt++)
            {
                var engine = new MusicEngine(Places(attempt + 3).Last());
                Bar? playing = null;
                long playingAt = 0L;
                engine.Listener = (bar, at) =>
                {
                    playing = bar;
                    playingAt = at;
                };
                engine.Set(new MusicIntent { Mood = MusicMood.Land, Intensity = 0.5f });
                for (int i = 0; i < 90 + attempt * 61; i++) engine.Render(left, right, 1024);

                Bar? before = playing;
                long beforeAt = playingAt;
                long first = -1, landed = -1;
                long asked = engine.Band.Now;
                engine.Listener = (bar, at) =>
                {
                    if (first < 0 && bar.Label.Contains("Boss")) first = at;
                    else if (first < 0)
                    {
                        before = bar;
                        beforeAt = at;
                    }
                    if (landed < 0 && bar.Label.StartsWith("Boss")) landed = at;
                };
                engine.Set(new MusicIntent { Mood = MusicMood.Boss, Intensity = 0.8f });
                for (int i = 0; i < 400 && landed < 0; i++) engine.Render(left, right, 1024);

                float delay = (first - asked) / (float)AudioFormat.SampleRate;
                float arrival = (landed - asked) / (float)AudioFormat.SampleRate;
                Expect(first >= 0 && delay < 1.8f, $"a tyrant took {delay:0.00} s to reach the music");
                Expect(landed >= 0 && arrival < 5f, $"a tyrant's fight took {arrival:0.00} s to land");
                if (before == null || before.Label == "rest") continue;
                double beat = before.Seconds / before.Beats * AudioFormat.SampleRate;
                double beats = (first - beforeAt) / beat;
                Expect(Math.Abs(beats - Math.Round(beats)) < 0.01, $"a tyrant came in {beats:0.00} beats into a bar");
            }

            // A fight has a shape of its own: the middle drops away and the return lands on everything.
            foreach ((MusicPlace place, MusicMood mood) in new[] { (land, MusicMood.Boss), (arena, MusicMood.Arena) })
            {
                List<Bar> fight = Write(place, mood, 0.8f, 40);
                float Density(string section, Func<Bar, bool> which) => (float)fight.Where(b => b.Label.Contains(section) && which(b)).Select(b => b.Notes.Count).DefaultIfEmpty(0).Average();
                int contrast = fight.FindIndex(b => b.Label.Contains("Contrast"));
                Expect(contrast > 0, $"{mood}: no middle section");
                float falling = Density("Contrast", b => fight.IndexOf(b) < contrast + 2);
                float returning = Density("First", b => fight.IndexOf(b) > contrast);
                Expect(falling < returning * 0.8f, $"{mood}: the middle has {falling:0} notes a bar and the return {returning:0}");
                Expect(fight.All(b => b.Label != "rest"), $"{mood} fell silent");
            }

            // A land has one score: going back to it brings back its tunes.
            string Fingerprint(MusicPlace place) => string.Join(",", Write(place, MusicMood.Land, 0.6f, 24).SelectMany(b => b.Notes).Select(n => $"{n.Section}{n.Pitch}"));
            Expect(Fingerprint(land) == Fingerprint(land), "the same land wrote different music");

            // A land's music stops sometimes, or nobody can hear the land; silence has nothing in it.
            List<Bar> bars = Write(land, MusicMood.Land, 0.5f, 140);
            Expect(bars.Count(b => b.Label == "rest") > 4, "a land's music never rests");
            Expect(Write(land, MusicMood.Silence, 0.5f, 10).All(b => b.Notes.Count == 0), "silence had notes in it");

            // A piece that's gone round comes back with its tune passed to someone else, now and then.
            int passed = 0;
            foreach (MusicPlace place in Places(8))
            {
                var score = new Score(place);
                Section[] first = score.For(MusicMood.Land).Style.Parts.Where(p => p.Writer == Textures.Melody).Select(p => p.Section).ToArray();
                var round = new Composer(score, place.Seed);
                for (int i = 0; i < 200; i++) round.Next(new MusicIntent { Mood = MusicMood.Land, Intensity = 0.5f });
                if (!first.SequenceEqual(score.For(MusicMood.Land).Style.Parts.Where(p => p.Writer == Textures.Melody).Select(p => p.Section))) passed++;
            }
            Expect(passed >= 3, $"the tune changed hands in only {passed} of eight lands over two hundred bars");

            // The cues happen, and the music goes on after them.
            var composer = new Composer(new Score(land), 3);
            var intent = new MusicIntent { Mood = MusicMood.Boss, Intensity = 0.6f };
            for (int i = 0; i < 5; i++) composer.Next(intent);
            intent.Cue = MusicCue.Victory;
            Bar cue = composer.Next(intent);
            Expect(cue.Label == "Victory", $"a cue was answered with {cue.Label}");
            intent.Cue = MusicCue.None;
            Expect(composer.Next(intent).Label == "Victory", "a cue didn't finish");
            Expect(composer.Next(intent).Label.StartsWith("Boss"), "the music didn't go on after a cue");

            // Walking the edge of a keep mustn't send the music back and forth, a fight mustn't be kept waiting by that, and one that's over is left at once.
            var hold = new MoodHold();
            for (int i = 0; i < 300; i++) hold.Update(MusicMood.Land, 0.2f);
            Expect(hold.Update(MusicMood.Boss, 0.2f) == MusicMood.Boss, "a fight was held back");
            Expect(hold.Update(MusicMood.Land, 0.2f) == MusicMood.Land, "the music didn't leave a fight that was over");
        }

        private static (float notes, float level, int sections) Measure(MusicPlace place, MusicMood mood, float intensity)
        {
            float notes = 0f, level = 0f;
            int count = 0;
            var sections = new HashSet<Section>();
            List<Bar> bars = Write(place, mood, intensity, 32);
            int played = 0;
            foreach (Bar bar in bars)
            {
                if (bar.Notes.Count == 0) continue;
                played++;
                foreach (Note note in bar.Notes)
                {
                    notes++;
                    level += note.Loudness;
                    count++;
                    sections.Add(note.Section);
                }
            }
            return (notes / Math.Max(played, 1), level / Math.Max(count, 1), sections.Count);
        }

        // ---- Transitions. ----

        private readonly record struct Handover(string Name, ulong Seed, float Jump, float Drop, float Reached, int Stolen, int Bridges, int Clashes, bool Into, int Faults);

        // Into a tyrant's fight and out of it again, caught at different moments of what was playing, in lands of every kind. What's measured is what's heard: how far the level leaps
        // or falls in a second, against how far it moves when nothing changes at all.
        private static void CheckTransitions(int perKind)
        {
            var cases = new List<(MusicPlace place, MusicMood from, MusicMood to, int moment)>();
            int moment = 0;
            foreach (MusicPlace place in Places(perKind * 6))
            {
                cases.Add((place, MusicMood.Land, MusicMood.Boss, moment++));
                cases.Add((place, MusicMood.Boss, MusicMood.Land, moment++));
                cases.Add((place, MusicMood.Land, MusicMood.Land, moment++));
            }

            var results = new Handover[cases.Count];
            Parallel.For(0, cases.Count, i => results[i] = Hand(cases[i].place, cases[i].from, cases[i].to, cases[i].moment));

            Handover[] staying = results.Where(r => r.Bridges < 0).ToArray();
            Handover[] changes = results.Where(r => r.Bridges >= 0).ToArray();
            float stillUp = staying.Average(r => r.Jump), stillDown = staying.Average(r => r.Drop);
            Console.WriteLine($"  Staying put, the level moves up {stillUp:0.0} and down {stillDown:0.0} dB in a second.");
            foreach (var group in changes.GroupBy(r => r.Into ? "into a fight" : "out of a fight"))
            {
                Console.WriteLine($"    {group.Key}: up {group.Average(r => r.Jump):0.0}, down {group.Average(r => r.Drop):0.0} dB, arrives after {group.Average(r => r.Reached):0.0} s, at most {group.Max(r => r.Reached):0.0} s.");
            }
            foreach (Handover change in changes)
            {
                Expect(change.Reached < 14f, $"{change.Name}, seed {change.Seed}: took {change.Reached:0.0} s to arrive");
                Expect(change.Jump < 16f, $"{change.Name}, seed {change.Seed}: leapt {change.Jump:0.0} dB in a second");
                Expect(change.Drop < 13f, $"{change.Name}, seed {change.Seed}: fell {change.Drop:0.0} dB in a second");
                Expect(change.Clashes == 0, $"{change.Name}, seed {change.Seed}: {change.Clashes} held semitones in its bridge");
                Expect(change.Stolen == 0, $"{change.Name}, seed {change.Seed}: {change.Stolen} notes went without a player");
                Expect(change.Bridges > 0, $"{change.Name}, seed {change.Seed}: no bridge");
                Expect(change.Faults == 0, $"{change.Name}, seed {change.Seed}: the band went bad {change.Faults} times");
            }
        }

        // Two lines held a semitone apart for long enough to be heard as a sound and not as a passing note.
        private static int HeldClashes(Bar bar)
        {
            const float Heard = 0.3f;
            int clashes = 0;
            for (int a = 0; a < bar.Notes.Count; a++)
            {
                Note one = bar.Notes[a];
                if (IsDrum(one.Section) || one.Length < Heard) continue;
                for (int b = a + 1; b < bar.Notes.Count; b++)
                {
                    Note other = bar.Notes[b];
                    if (IsDrum(other.Section) || other.Length < Heard) continue;
                    int interval = Math.Abs(one.Pitch - other.Pitch) % 12;
                    if (interval is not (1 or 11)) continue;
                    float shared = MathF.Min(one.Start + one.Length, other.Start + other.Length) - MathF.Max(one.Start, other.Start);
                    if (shared >= Heard) clashes++;
                }
            }
            return clashes;
        }

        private static Handover Hand(MusicPlace place, MusicMood from, MusicMood to, int moment)
        {
            const int Step = 1024;
            const float Window = 0.5f;
            var engine = new MusicEngine(place);
            var left = new float[Step];
            var right = new float[Step];
            string name = $"{new Score(place).For(MusicMood.Land).Style.Name}, {from} to {to}";

            // Far enough in that it's playing, and caught somewhere different each time.
            engine.Set(new MusicIntent { Mood = from, Intensity = from == MusicMood.Boss ? 0.8f : 0.5f });
            int settle = (int)((7f + moment % 11 * 0.73f) * AudioFormat.SampleRate / Step);
            for (int i = 0; i < settle; i++) engine.Render(left, right, Step);

            long asked = engine.Band.Now;
            long reached = -1;
            int bridges = 0, clashes = 0;
            bool changed = false;
            engine.Listener = (bar, at) =>
            {
                if (bar.Label.StartsWith("Into"))
                {
                    bridges++;
                    clashes += HeldClashes(bar);
                }
                if (changed && reached < 0 && bar.Label.StartsWith(to.ToString())) reached = at;
            };

            int blocks = (int)(16f * AudioFormat.SampleRate / Step);
            var energy = new float[blocks];
            int before = (int)(2f * AudioFormat.SampleRate / Step);
            for (int i = 0; i < blocks; i++)
            {
                if (i == before)
                {
                    asked = engine.Band.Now;
                    changed = true;
                    engine.Set(new MusicIntent { Mood = to, Intensity = to == MusicMood.Boss ? 0.8f : 0.5f });
                }
                engine.Render(left, right, Step);
                double sum = 0.0;
                for (int n = 0; n < Step; n++) sum += left[n] * left[n] + right[n] * right[n];
                energy[i] = (float)(sum / (2 * Step));
            }

            // A second's change in the level, each side averaged over half a second: long enough that a single note doesn't count, short enough that a handover does.
            int span = (int)(Window * AudioFormat.SampleRate / Step);
            float Level(int start)
            {
                double sum = 0.0;
                for (int i = start; i < start + span; i++) sum += energy[i];
                return AudioFormat.GainToDecibels(MathF.Sqrt((float)(sum / span)) + 1e-5f);
            }
            // Only over the handover itself, from the moment it's asked for to a second after the new piece has begun: past that, how the level moves is the piece's own, and a
            // sparse piano swings fifteen decibels from a struck note to the silence after it.
            bool staying = from == to;
            int first = staying ? 0 : before - span;
            int last = staying || reached < 0 ? blocks : Math.Min(blocks, before + (int)((reached - asked) / (float)Step) + (int)(1f * AudioFormat.SampleRate / Step));
            float jump = 0f, drop = 0f;
            for (int i = Math.Max(first, 0); i + 2 * span < blocks && i + span <= last; i++)
            {
                float a = Level(i), b = Level(i + span);

                // A piece that goes quiet between phrases is doing what it should, and coming back from that says nothing about the handover.
                if (a < -50f || b < -50f) continue;
                jump = MathF.Max(jump, b - a);
                drop = MathF.Max(drop, a - b);
            }
            return new Handover(name, place.Seed, jump, drop, staying ? 0f : reached < 0 ? 99f : (reached - asked) / (float)AudioFormat.SampleRate, engine.Band.Stolen, staying ? -1 : bridges, clashes, to == MusicMood.Boss, engine.Band.Faults);
        }

        // ---- Balance. ----

        // A tune nobody can pick out isn't a tune. Whoever carries it has to come out louder than any other group of players, heard alone, in every kind of piece.
        private static void CheckBalance()
        {
            var places = Places(60).Where((place, i) => i % 6 == 0).Concat(Specials().Take(3)).ToArray();
            foreach (MusicPlace place in places)
            {
                var score = new Score(place);
                foreach (MusicMood mood in MoodsOf(score))
                {
                    Style style = score.For(mood).Style;
                    Part? lead = style.Parts.FirstOrDefault(part => part.Writer == Textures.Melody && part.Use is MelodyUse.First or MelodyUse.Always);
                    if (lead == null) continue;

                    // Where the one who carries the tune also accompanies it, as a pianist does, it can't be heard apart.
                    if (style.Parts.Any(part => part.Section == lead.Section && part.Writer != Textures.Melody)) continue;
                    float tune = LevelOf(place, mood, new[] { lead.Section }, 0, 16);
                    foreach (Section other in style.Parts.Select(part => part.Section).Distinct().Where(section => section != lead.Section && !style.Parts.Any(p => p.Section == section && p.Writer == Textures.Melody)))
                    {
                        float level = LevelOf(place, mood, new[] { other }, 0, 16);
                        Expect(tune > level - 1f, $"{style.Name} ({mood}): the tune on {lead.Section} is at {tune:0.0} dB and {other} at {level:0.0}");
                    }
                }
            }
        }

        // How loud some players come out over the tune's first statement, where it's the first player who has the tune.
        private static float LevelOf(MusicPlace place, MusicMood mood, Section[] sections, float from, float seconds)
        {
            var engine = new MusicEngine(place);
            for (int i = 0; i < engine.Band.Gains.Length; i++) engine.Band.Gains[i] = sections.Contains((Section)i) ? 1f : 0f;
            engine.Set(new MusicIntent { Mood = mood, Intensity = 0.7f });
            var left = new float[1024];
            var right = new float[1024];
            double energy = 0.0;
            for (int block = 0; block < seconds * AudioFormat.SampleRate / 1024; block++)
            {
                engine.Render(left, right, 1024);
                if (block * 1024f / AudioFormat.SampleRate < from) continue;
                for (int i = 0; i < 1024; i++) energy += left[i] * left[i] + right[i] * right[i];
            }
            return 10f * MathF.Log10((float)energy + 1e-9f);
        }

        // ---- Sound. ----

        // Recorded orchestras have under a tenth of their energy there.
        private static float LowEndShare(float[] signal)
        {
            var filter = Biquad.Lowpass(100f);
            double low = 0.0, all = 1e-9;
            foreach (float value in signal)
            {
                float under = filter.Process(value);
                low += under * under;
                all += value * value;
            }
            return (float)(low / all);
        }

        private static void CheckSound()
        {
            var cases = new List<(MusicPlace place, MusicMood mood)>();
            foreach (MusicPlace place in Places(30).Where((place, i) => i % 3 == 0)) cases.Add((place, MusicMood.Land));
            cases.Add((Places(4).Last(), MusicMood.Boss));
            cases.Add((MusicPlace.Home, MusicMood.Haven));
            foreach (MusicPlace arena in Specials().Skip(1)) cases.Add((arena, MusicMood.Arena));

            var lines = new string[cases.Count];
            Parallel.For(0, cases.Count, index =>
            {
                (MusicPlace place, MusicMood mood) = cases[index];
                var engine = new MusicEngine(place);
                string name = $"{engine.Score.For(mood).Style.Name} ({mood})";
                int notes = 0;
                engine.Listener = (bar, at) => notes += bar.Notes.Count;
                engine.Set(new MusicIntent { Mood = mood, Intensity = 0.95f });
                int frames = 20 * AudioFormat.SampleRate;
                var left = new float[frames];
                var right = new float[frames];
                var l = new float[1024];
                var r = new float[1024];
                var clock = Stopwatch.StartNew();
                for (int done = 0; done + 1024 <= frames; done += 1024)
                {
                    engine.Render(l, r, 1024);
                    Array.Copy(l, 0, left, done, 1024);
                    Array.Copy(r, 0, right, done, 1024);
                }
                float cost = (float)(clock.Elapsed.TotalSeconds / 20.0);
                float peak = 0f;
                foreach (float value in left) peak = MathF.Max(peak, MathF.Abs(value));
                foreach (float value in right) peak = MathF.Max(peak, MathF.Abs(value));
                bool finite = left.All(float.IsFinite) && right.All(float.IsFinite);
                float loudness = finite ? Loudness.Integrated(left, right) : float.NaN;
                lock (cases)
                {
                    Expect(cost < 0.3f, $"{name} takes {cost * 100f:0.0}% of a core");
                    Expect(engine.Band.Stolen <= notes / 100, $"{name} ran out of voices for {engine.Band.Stolen} of its {notes} notes");
                    Expect(finite && engine.Band.Faults == 0, $"{name} has NaN or infinity in it ({engine.Band.Faults} faults)");
                    Expect(peak > 0.01f, $"{name} is silent at full intensity");
                    Expect(LowEndShare(left) < 0.5f, $"{name} has {LowEndShare(left) * 100f:0}% of its energy under 100 Hz, which only drives the limiter");
                    Expect(peak <= AudioFormat.DecibelsToGain(-2f) + 0.001f, $"{name} went over the band's ceiling");
                }
                lines[index] = $"    {name}: {loudness:0.0} LUFS, {cost * 100f:0.0}% of a core (single threaded it'd be less), {notes} notes";
            });
            foreach (string line in lines) Console.WriteLine(line);
        }
    }
}
