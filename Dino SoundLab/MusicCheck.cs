using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Music;
using System.Diagnostics;

namespace Dino_SoundLab
{
    /// <summary>
    /// What can be known about music without ears. Nothing here says a tune is good; it says when one is plainly wrong: out of tune with itself, out of an instrument's range, clashing
    /// with its own harmony, or not doing what it was told. The numbers it compares melodies with are what a thousand real folk tunes do.
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
            int seeds = options.Int("count", 12);

            Console.WriteLine("Tunes");
            CheckTunes();
            Console.WriteLine("Scores");
            for (int seed = 1; seed <= seeds; seed++) CheckScore((ulong)seed);
            Console.WriteLine("Adapting");
            CheckAdapting();
            Console.WriteLine("Sound");
            CheckSound();
            Console.WriteLine("Balance");
            CheckBalance();
            Console.WriteLine("Transitions");
            CheckTransitions(options.Int("transitions", 2));

            Console.WriteLine($"{_checks} checks, {_failures} failed, in {clock.Elapsed.TotalSeconds:0.0} s.");
            return _failures == 0 ? 0 : 1;
        }

        private static void CheckTunes()
        {
            foreach (Mode mode in new[] { Mode.Aeolian, Mode.Dorian, Mode.Ionian, Mode.Mixolydian, Mode.Lydian, Mode.Phrygian })
            {
                int steps = 0, intervals = 0, hard = 0, strong = 0, strongHits = 0, wide = 0;
                for (int i = 0; i < 60; i++)
                {
                    var random = new SoundRandom((ulong)i * 977UL + 5UL);
                    Chord[] harmony = Harmony.Draw(random, mode);
                    Tune tune = Songwriter.Write(random, new TuneBrief { Mode = mode, Harmony = harmony, Candidates = 150 });
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
                    Expect(Chord.Wrap(tune.Tones[^1].Degree) == 0, $"a tune in {mode.Name} doesn't end at home");
                }

                float stepShare = steps / (float)intervals;
                float strongShare = strongHits / (float)strong;
                Expect(stepShare is > 0.38f and < 0.68f, $"{mode.Name}: {stepShare * 100f:0.0}% steps, where real tunes have 52.5%");
                Expect(strongShare > 0.75f, $"{mode.Name}: only {strongShare * 100f:0.0}% chord tones on strong beats, where real tunes have 87%");
                Expect(hard / (float)intervals < 0.015f, $"{mode.Name}: {hard / (float)intervals * 100f:0.0}% of its leaps are unsingable");
                Expect(wide / (float)intervals < 0.06f, $"{mode.Name}: {wide / (float)intervals * 100f:0.0}% wide leaps");
            }

            // The theme is what ties a world's music together: its shape has to survive in another style.
            // On average over many worlds, as one tune in eight notes says little.
            float kinship = 0f;
            for (ulong seed = 1; seed <= 24; seed++)
            {
                var score = new Score(seed);
                kinship += Correlation(Songwriter.ShapeOf(score.For(MusicMood.Town).First), Songwriter.ShapeOf(score.For(MusicMood.Battle).First)) / 24f;
            }
            Console.WriteLine($"  A town's theme and a battle's correlate by {kinship:0.00}.");
            Expect(kinship > 0.5f, $"the theme's shape doesn't survive between styles ({kinship:0.00})");

            var a = new Score(5);
            var b = new Score(5);
            Expect(a.For(MusicMood.Town).First.Tones.SequenceEqual(b.For(MusicMood.Town).First.Tones), "one seed gave two different themes");
            var c = new Score(6);
            Expect(!a.For(MusicMood.Town).First.Tones.SequenceEqual(c.For(MusicMood.Town).First.Tones), "two seeds gave the same theme");
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
            return compass;
        }

        // Plays a mood for a while on paper, with no sound: everything about the notes can be checked this way, and fast.
        private static List<Bar> Write(ulong seed, MusicMood mood, float intensity, int bars)
        {
            var score = new Score(seed);
            var composer = new Composer(score, seed);
            var written = new List<Bar>();
            var intent = new MusicIntent { Mood = mood, Intensity = intensity };
            for (int i = 0; i < bars; i++) written.Add(composer.Next(intent));
            return written;
        }

        private static void CheckScore(ulong seed)
        {
            var score = new Score(seed);
            foreach (MusicMood mood in Enum.GetValues<MusicMood>())
            {
                if (mood == MusicMood.Silence) continue;
                Score.Setting setting = score.For(mood);
                Expect(setting.Tempo is > 40f and < 190f, $"{mood} at {setting.Tempo} bpm");

                List<Bar> bars = Write(seed, mood, 0.8f, 40);
                int notes = 0;
                int clashes = 0;
                int beats = 0;
                foreach (Bar bar in bars)
                {
                    foreach (Note note in bar.Notes)
                    {
                        notes++;
                        (int low, int high) = Compass[(int)note.Section];
                        Expect(note.Pitch >= low && note.Pitch <= high, $"{mood}, seed {seed}: {note.Section} asked for {MusicLab.NoteName(note.Pitch)}");
                        Expect(note.Length > 0f && note.Start >= 0f && float.IsFinite(note.Loudness), $"{mood}, seed {seed}: a nonsense note");
                    }
                    (int c, int b) = Clashes(bar);
                    clashes += c;
                    beats += b;
                }
                Expect(notes > 20, $"{mood}, seed {seed}: only {notes} notes in forty bars");

                // Notes that grate on a strong beat, in the sound's middle and low range, where the ear is least forgiving.
                float share = clashes / (float)Math.Max(beats, 1);
                Expect(share < 0.12f, $"{mood}, seed {seed}: {share * 100f:0.0}% of its strong moments clash");
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

        private static bool IsDrum(Section section) => section is Section.Timpani or Section.BigDrum or Section.FrameDrum or Section.Tambourine or Section.Cymbal;

        private static void CheckAdapting()
        {
            // Intensity has to mean something: more players, playing more, playing louder.
            foreach (MusicMood mood in new[] { MusicMood.Wander, MusicMood.Town, MusicMood.Battle, MusicMood.Tension })
            {
                (float quietNotes, float quietLevel, int quietSections) = Measure(mood, 0.1f);
                (float loudNotes, float loudLevel, int loudSections) = Measure(mood, 0.95f);
                Expect(loudSections > quietSections, $"{mood}: {quietSections} sections at low intensity and {loudSections} at high");
                Expect(loudNotes > quietNotes, $"{mood}: no more notes at high intensity ({quietNotes:0.0} and {loudNotes:0.0} a bar)");
                Expect(loudLevel > quietLevel, $"{mood}: no louder at high intensity");
            }

            // A fight can't wait for a phrase.
            var engine = new MusicEngine(4);
            var left = new float[1024];
            var right = new float[1024];
            engine.Set(new MusicIntent { Mood = MusicMood.Wander, Intensity = 0.5f });
            for (int i = 0; i < 300; i++) engine.Render(left, right, 1024);

            // It comes in on a beat of what was playing, whenever it's asked for, so that the change sounds meant.
            long firstFight = -1;
            long asked = 0L;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                engine = new MusicEngine(4);
                Bar? playing = null;
                long playingAt = 0L;
                engine.Listener = (bar, at) =>
                {
                    playing = bar;
                    playingAt = at;
                };
                engine.Set(new MusicIntent { Mood = MusicMood.Town, Intensity = 0.5f });
                for (int i = 0; i < 90 + attempt * 61; i++) engine.Render(left, right, 1024);

                Bar? before = playing;
                long beforeAt = playingAt;
                firstFight = -1;
                asked = engine.Band.Now;
                engine.Listener = (bar, at) =>
                {
                    if (firstFight < 0 && bar.Label.Contains("Battle")) firstFight = at;
                    else if (firstFight < 0)
                    {
                        before = bar;
                        beforeAt = at;
                    }
                };
                engine.Set(new MusicIntent { Mood = MusicMood.Battle, Intensity = 0.8f });
                for (int i = 0; i < 300 && firstFight < 0; i++) engine.Render(left, right, 1024);

                float delay = (firstFight - asked) / (float)AudioFormat.SampleRate;
                Expect(firstFight >= 0 && delay < 1.2f, $"a fight took {delay:0.00} s to reach the music");
                if (before == null) continue;
                double beat = before.Seconds / before.Beats * AudioFormat.SampleRate;
                double beats = (firstFight - beforeAt) / beat;
                Expect(Math.Abs(beats - Math.Round(beats)) < 0.01, $"a fight came in {beats:0.00} beats into a bar");
            }

            // Tension comes in by the next bar, whatever the phrase is doing.
            engine = new MusicEngine(4);
            engine.Set(new MusicIntent { Mood = MusicMood.Wander, Intensity = 0.5f });
            for (int i = 0; i < 300; i++) engine.Render(left, right, 1024);
            firstFight = -1;
            asked = engine.Band.Now;
            engine.Listener = (bar, at) =>
            {
                if (firstFight < 0 && bar.Label.Contains("Tension")) firstFight = at;
            };
            engine.Set(new MusicIntent { Mood = MusicMood.Tension, Intensity = 0.5f });
            for (int i = 0; i < 400 && firstFight < 0; i++) engine.Render(left, right, 1024);
            float uneasy = (firstFight - asked) / (float)AudioFormat.SampleRate;
            Expect(firstFight >= 0 && uneasy < 4f, $"unease took {uneasy:0.00} s to reach the music");

            // A fight has a shape of its own: the middle drops away and the return lands on everything.
            foreach (MusicMood mood in new[] { MusicMood.Battle, MusicMood.Boss })
            {
                for (ulong seed = 1; seed <= 6; seed++)
                {
                    List<Bar> fight = Write(seed, mood, 0.8f, 40);
                    float Density(string section, Func<Bar, bool> which) => (float)fight.Where(b => b.Label.Contains(section) && which(b)).Select(b => b.Notes.Count).DefaultIfEmpty(0).Average();
                    int contrast = fight.FindIndex(b => b.Label.Contains("Contrast"));
                    Expect(contrast > 0, $"{mood}, seed {seed}: no middle section");
                    float falling = Density("Contrast", b => fight.IndexOf(b) < contrast + 2);
                    float returning = Density("First", b => fight.IndexOf(b) > contrast);
                    Expect(falling < returning * 0.8f, $"{mood}, seed {seed}: the middle has {falling:0} notes a bar and the return {returning:0}");
                }
            }

            // A world has one score: going back to it brings back its tunes.
            string Fingerprint(ulong seed) => string.Join(",", Write(seed, MusicMood.Town, 0.6f, 24).SelectMany(b => b.Notes).Select(n => $"{n.Section}{n.Pitch}"));
            Expect(Fingerprint(11) == Fingerprint(11), "the same seed wrote different music");
            Expect(Fingerprint(11) != Fingerprint(12), "two seeds wrote the same music");

            // Music for wandering should stop sometimes, or nobody can hear the land.
            List<Bar> bars = Write(2, MusicMood.Wander, 0.5f, 120);
            Expect(bars.Count(b => b.Label == "rest") > 8, "the music for wandering never rests");
            bars = Write(2, MusicMood.Battle, 0.5f, 60);
            Expect(bars.All(b => b.Label != "rest"), "a fight fell silent");
            bars = Write(2, MusicMood.Silence, 0.5f, 10);
            Expect(bars.All(b => b.Notes.Count == 0), "silence had notes in it");

            // The cues happen, and the music goes on after them.
            var composer = new Composer(new Score(3), 3);
            var intent = new MusicIntent { Mood = MusicMood.Battle, Intensity = 0.6f };
            for (int i = 0; i < 5; i++) composer.Next(intent);
            intent.Cue = MusicCue.Victory;
            Bar cue = composer.Next(intent);
            Expect(cue.Label == "Victory", $"a cue was answered with {cue.Label}");
            intent.Cue = MusicCue.None;
            Expect(composer.Next(intent).Label == "Victory", "a cue didn't finish");
            Expect(composer.Next(intent).Label.StartsWith("Battle"), "the music didn't go on after a cue");
        }

        private static (float notes, float level, int sections) Measure(MusicMood mood, float intensity)
        {
            float notes = 0f, level = 0f;
            int count = 0;
            var sections = new HashSet<Section>();
            List<Bar> bars = Write(7, mood, intensity, 32);
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

        private readonly record struct Handover(MusicMood From, MusicMood To, ulong Seed, float Jump, float Drop, float Reached, int Stolen, int Bridges, int Clashes);

        // Every change from one style to another, caught at different moments of what was playing. What's measured is what's heard: how far the level leaps or falls in a second.
        private static void CheckTransitions(int seeds)
        {
            MusicMood[] moods = Enum.GetValues<MusicMood>().Where(m => m != MusicMood.Silence).ToArray();
            var cases = new List<(MusicMood from, MusicMood to, ulong seed)>();
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                foreach (MusicMood from in moods)
                {
                    foreach (MusicMood to in moods) cases.Add((from, to, seed));
                }
            }

            var results = new Handover[cases.Count];
            Parallel.For(0, cases.Count, i => results[i] = Hand(cases[i].from, cases[i].to, cases[i].seed, i));

            // Music that's just going on moves by this much anyway, which is what a change has to be judged against.
            Handover[] staying = results.Where(r => r.From == r.To).ToArray();
            Handover[] changes = results.Where(r => r.From != r.To).ToArray();
            float stillUp = staying.Average(r => r.Jump);
            float stillDown = staying.Average(r => r.Drop);
            Console.WriteLine($"  Staying put, the level moves up {stillUp:0.0} and down {stillDown:0.0} dB in a second.");

            Handover worstJump = changes.MaxBy(r => r.Jump);
            Handover worstDrop = changes.MaxBy(r => r.Drop);
            Console.WriteLine($"  {changes.Length} changes: up {changes.Average(r => r.Jump):0.0} dB on average, at worst {worstJump.Jump:0.0} ({worstJump.From} to {worstJump.To}, seed {worstJump.Seed}); down {changes.Average(r => r.Drop):0.0}, at worst {worstDrop.Drop:0.0} ({worstDrop.From} to {worstDrop.To}, seed {worstDrop.Seed}).");
            foreach (var group in changes.GroupBy(r => Styles.For(r.To).Urgent ? "into a fight" : Styles.For(r.From).Urgent ? "out of a fight" : "between calm styles"))
            {
                Console.WriteLine($"    {group.Key}: up {group.Average(r => r.Jump):0.0}, down {group.Average(r => r.Drop):0.0} dB, arrives after {group.Average(r => r.Reached):0.0} s, at most {group.Max(r => r.Reached):0.0} s.");
            }

            foreach (Handover change in changes)
            {
                string name = $"{change.From} to {change.To}, seed {change.Seed}";
                Expect(change.Reached < 14f, $"{name}: took {change.Reached:0.0} s to arrive");
                Expect(change.Jump < 15f, $"{name}: leapt {change.Jump:0.0} dB in a second");
                Expect(change.Drop < 11f, $"{name}: fell {change.Drop:0.0} dB in a second");
                Expect(change.Clashes == 0, $"{name}: {change.Clashes} held semitones in its bridge");
                Expect(change.Stolen == 0, $"{name}: {change.Stolen} notes went without a player");
            }
            Expect(changes.Average(r => r.Drop) < stillDown + 3f, $"on average a change falls {changes.Average(r => r.Drop):0.0} dB in a second, against {stillDown:0.0} with no change at all");
            Expect(changes.Average(r => r.Jump) < stillUp + 3f, $"on average a change leaps {changes.Average(r => r.Jump):0.0} dB in a second, against {stillUp:0.0} with no change at all");
            Expect(changes.Count(r => r.Bridges > 0) > changes.Length * 0.6f, "most changes went by with no bridge");

            // Walking the edge of a place mustn't send the music back and forth, and a fight mustn't be kept waiting by that.
            var hold = new MoodHold();
            for (int i = 0; i < 300; i++) hold.Update(MusicMood.Wander, 0.2f);
            int turns = 0;
            MusicMood last = MusicMood.Wander;
            for (int i = 0; i < 600; i++)
            {
                MusicMood heard = hold.Update(i / 10 % 2 == 0 ? MusicMood.Town : MusicMood.Wander, 0.2f);
                if (heard != last) turns++;
                last = heard;
            }
            Expect(turns == 0, $"two minutes along the edge of a town changed the music {turns} times");
            Expect(hold.Update(MusicMood.Battle, 0.2f) == MusicMood.Battle, "a fight was held back");
            Expect(hold.Update(MusicMood.Wander, 0.2f) == MusicMood.Wander, "the music didn't leave a fight that was over");
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

        private static Handover Hand(MusicMood from, MusicMood to, ulong seed, int moment)
        {
            const int Step = 1024;
            const float Window = 0.5f;
            var engine = new MusicEngine(seed);
            var left = new float[Step];
            var right = new float[Step];

            // Far enough in that it's playing, and caught somewhere different each time.
            engine.Set(new MusicIntent { Mood = from, Intensity = 0.6f });
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
            engine.Set(new MusicIntent { Mood = from, Intensity = 0.6f });
            int before = (int)(2f * AudioFormat.SampleRate / Step);
            for (int i = 0; i < blocks; i++)
            {
                if (i == before)
                {
                    asked = engine.Band.Now;
                    changed = true;
                    engine.Set(new MusicIntent { Mood = to, Intensity = 0.6f });
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
            float jump = 0f, drop = 0f;
            for (int i = 0; i + 2 * span < blocks; i++)
            {
                float a = Level(i), b = Level(i + span);

                // A style that goes quiet between phrases is doing what it should, and coming back from that says nothing about the handover.
                if (a < -50f || b < -50f) continue;
                jump = MathF.Max(jump, b - a);
                drop = MathF.Max(drop, a - b);
            }
            return new Handover(from, to, seed, jump, drop, reached < 0 ? 99f : (reached - asked) / (float)AudioFormat.SampleRate, engine.Band.Stolen, bridges, clashes);
        }

        // A tune nobody can pick out isn't a tune. Whoever carries it has to come out louder than any other group, heard alone.
        private static void CheckBalance()
        {
            var moods = new (MusicMood mood, Section[] melody, Section[][] others)[]
            {
                (MusicMood.Wander, new[] { Section.Flute, Section.Oboe, Section.Clarinet },
                    new[] { new[] { Section.Violins, Section.Violas, Section.Cellos, Section.Horns }, new[] { Section.Harp } }),
                (MusicMood.Town, new[] { Section.Fiddle, Section.Flute, Section.Clarinet },
                    new[] { new[] { Section.Lute, Section.Pizzicato }, new[] { Section.FrameDrum, Section.Tambourine } }),
                (MusicMood.Battle, new[] { Section.Horns, Section.Trumpets },
                    new[] { new[] { Section.Trombones }, new[] { Section.Cellos, Section.Basses, Section.Violas, Section.Violins }, new[] { Section.BigDrum, Section.Timpani } }),
                (MusicMood.Boss, new[] { Section.Horns, Section.Trumpets },
                    new[] { new[] { Section.Trombones }, new[] { Section.Cellos, Section.Basses, Section.Violas, Section.Violins }, new[] { Section.BigDrum, Section.Timpani }, new[] { Section.Choir } }),
            };
            foreach ((MusicMood mood, Section[] melody, Section[][] others) in moods)
            {
                float tune = LevelOf(mood, melody);
                foreach (Section[] group in others)
                {
                    float level = LevelOf(mood, group);
                    Expect(tune > level, $"{mood}: the tune is at {tune:0.0} dB and {group[0]} and the like at {level:0.0}");
                }
            }
        }

        private static float LevelOf(MusicMood mood, Section[] sections)
        {
            var engine = new MusicEngine(5);
            for (int i = 0; i < engine.Band.Gains.Length; i++) engine.Band.Gains[i] = sections.Contains((Section)i) ? 1f : 0f;
            engine.Set(new MusicIntent { Mood = mood, Intensity = 0.8f });
            var left = new float[1024];
            var right = new float[1024];
            double energy = 0.0;
            for (int block = 0; block < 30 * AudioFormat.SampleRate / 1024; block++)
            {
                engine.Render(left, right, 1024);
                for (int i = 0; i < 1024; i++) energy += left[i] * left[i] + right[i] * right[i];
            }
            return 10f * MathF.Log10((float)energy + 1e-9f);
        }

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
            foreach (MusicMood mood in new[] { MusicMood.Wander, MusicMood.Town, MusicMood.Battle, MusicMood.Boss, MusicMood.Sacred, MusicMood.Haunted, MusicMood.Tension })
            {
                var engine = new MusicEngine(9);
                int notes = 0;
                engine.Listener = (bar, at) => notes += bar.Notes.Count;
                engine.Set(new MusicIntent { Mood = mood, Intensity = 1f });
                int frames = 16 * AudioFormat.SampleRate;
                var left = new float[frames];
                var right = new float[frames];
                var clock = Stopwatch.StartNew();
                for (int done = 0; done + 1024 <= frames; done += 1024)
                {
                    float[] l = new float[1024], r = new float[1024];
                    engine.Render(l, r, 1024);
                    Array.Copy(l, 0, left, done, 1024);
                    Array.Copy(r, 0, right, done, 1024);
                }
                float cost = (float)(clock.Elapsed.TotalSeconds / 16.0);
                Expect(cost < 0.3f, $"{mood} takes {cost * 100f:0.0}% of a core");
                Expect(engine.Band.Stolen <= notes / 100, $"{mood} ran out of voices for {engine.Band.Stolen} of its {notes} notes");
                Expect(left.All(float.IsFinite) && right.All(float.IsFinite), $"{mood} has NaN or infinity in it");

                float peak = 0f;
                foreach (float value in left) peak = MathF.Max(peak, MathF.Abs(value));
                foreach (float value in right) peak = MathF.Max(peak, MathF.Abs(value));
                Expect(peak > 0.01f, $"{mood} is silent at full intensity");
                Expect(LowEndShare(left) < 0.5f, $"{mood} has {LowEndShare(left) * 100f:0}% of its energy under 100 Hz, which only drives the limiter");
                Expect(peak <= AudioFormat.DecibelsToGain(-2f) + 0.001f, $"{mood} went over the band's ceiling");
            }
        }
    }
}
