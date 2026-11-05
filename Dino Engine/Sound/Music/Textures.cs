using Dino_Engine.Sound.Dsp;

namespace Dino_Engine.Sound.Music
{
    /// <summary>Everything a part needs to know to write its bar.</summary>
    public sealed class BarContext
    {
        public readonly List<Note> Notes = new();
        public readonly Dictionary<Part, int[]> Voicings = new();
        public SoundRandom Random = null!;
        public Mode Mode = Mode.Aeolian;
        public int Tonic;
        public Meter Meter;
        public float BeatSeconds;
        public float Intensity;
        public Chord Chord;
        public Chord NextChord;
        public int Bar;                     // Within its phrase.
        public int Bars;
        public MelodyUse Section;
        public IReadOnlyList<Tone> Melody = Array.Empty<Tone>();     // This bar's notes, with beats from the start of the bar.
        public int PreviousMelodyDegree;
        public int CounterDegree;
        public int TuneLowest, TuneHighest;     // The compass of the whole tune, as degrees, so that a part can choose one octave for all of it.

        public bool LastBar => Bar == Bars - 1;
        public bool Halfway => Bar == Bars / 2 - 1;

        public float Loudness(Part part) => Math.Clamp(part.Level + part.Rise * Intensity, 0f, 1f);

        public int Pitch(int degree) => Mode.Pitch(Tonic, degree);

        public void Add(Part part, int pitch, float beat, float beats, float loudness, Articulation articulation = Articulation.Normal)
        {
            Notes.Add(new Note
            {
                Section = part.Section,
                Pitch = pitch,
                Start = beat * BeatSeconds,
                Length = beats * BeatSeconds,
                Loudness = Math.Clamp(loudness, 0f, 1f),
                Fader = part.Fader,
                Articulation = articulation,
            });
        }
    }

    /// <summary>
    /// The things players do. Each writes one bar of one part, following whatever chord it's given, so any of them can go in any style. Good accompaniment doesn't
    /// draw attention: it keeps time, says the chord, and leaves room for the tune.
    /// </summary>
    public static class Textures
    {
        private static readonly (int low, int high)[] Ranges = BuildRanges();

        private static (int, int)[] BuildRanges()
        {
            var ranges = new (int, int)[Enum.GetValues<Section>().Length];
            ranges[(int)Section.Flute] = (60, 96);
            ranges[(int)Section.Oboe] = (58, 88);
            ranges[(int)Section.Clarinet] = (52, 88);
            ranges[(int)Section.Horns] = (41, 74);
            ranges[(int)Section.Trombones] = (36, 67);
            ranges[(int)Section.Trumpets] = (54, 84);
            ranges[(int)Section.Violins] = (57, 96);
            ranges[(int)Section.Violas] = (48, 76);
            ranges[(int)Section.Cellos] = (36, 67);
            ranges[(int)Section.Basses] = (28, 55);
            ranges[(int)Section.Fiddle] = (57, 91);
            ranges[(int)Section.Choir] = (45, 77);
            ranges[(int)Section.Harp] = (36, 88);
            ranges[(int)Section.Lute] = (40, 72);
            ranges[(int)Section.Pizzicato] = (28, 60);
            ranges[(int)Section.Celesta] = (60, 100);
            ranges[(int)Section.Bells] = (48, 72);
            ranges[(int)Section.Timpani] = (41, 57);
            ranges[(int)Section.BigDrum] = (24, 48);
            ranges[(int)Section.FrameDrum] = (36, 60);
            ranges[(int)Section.Tambourine] = (40, 80);
            ranges[(int)Section.Cymbal] = (40, 80);
            return ranges;
        }

        private static int Fit(Part part, int pitch) => Fit(part.Section, pitch);

        internal static int Fit(Section section, int pitch)
        {
            (int low, int high) = Ranges[(int)section];
            while (pitch < low) pitch += 12;
            while (pitch > high) pitch -= 12;
            return pitch;
        }

        /// <summary>The tune, with a slur between notes that step, as a player would phrase it.</summary>
        public static void Melody(BarContext bar, Part part)
        {
            if (part.Use != MelodyUse.Always && part.Use != bar.Section) return;

            // A tune moves to an instrument's range as a whole. Folding its notes one by one would tear its shape apart.
            int shift = Register(bar, part);
            float loudness = bar.Loudness(part);
            int previous = bar.PreviousMelodyDegree;
            for (int i = 0; i < bar.Melody.Count; i++)
            {
                Tone tone = bar.Melody[i];
                int step = Math.Abs(tone.Degree - previous);

                // A note that's close to the last is joined to it. A leap or a repeat is taken with a fresh attack.
                Articulation articulation = step is 1 or 2 && i + bar.Bar > 0 ? Articulation.Legato : Articulation.Normal;

                // To be joined a note has to still be sounding when the next arrives, so it's held over. One that isn't lets go a moment early, which is the breath before an attack.
                bool joins = i + 1 < bar.Melody.Count ? Math.Abs(bar.Melody[i + 1].Degree - tone.Degree) is 1 or 2 : !bar.LastBar;
                float length = joins ? tone.Length + 0.12f : tone.Length * 0.92f;

                // A melody rises and falls in loudness with its pitch, as a singer's does.
                float shape = (tone.Degree - 4) * 0.012f;
                bar.Add(part, Fit(part, bar.Pitch(tone.Degree + shift)), tone.Beat, length, loudness + shape, articulation);
                previous = tone.Degree;
            }
        }

        // The octave that puts a tune's whole compass in this instrument's range, nearest to where the part asked for it.
        private static int Register(BarContext bar, Part part) => Shift(part, bar.Mode, bar.Tonic, bar.TuneLowest, bar.TuneHighest);

        internal static int Shift(Part part, Mode mode, int tonic, int tuneLowest, int tuneHighest)
        {
            (int low, int high) = Ranges[(int)part.Section];
            int shift = part.Octave * 7;
            while (mode.Pitch(tonic, tuneLowest + shift) < low) shift += 7;

            // Its high point matters more than its low one, as that's where a tune is going. If the octaves don't fall right for both, the low end is what gets folded.
            while (mode.Pitch(tonic, tuneHighest + shift) > high) shift -= 7;
            return shift;
        }

        /// <summary>A second, slower line, going against the tune: long notes moving the other way, from the chord.</summary>
        public static void Countermelody(BarContext bar, Part part)
        {
            float loudness = bar.Loudness(part);
            int beats = bar.Meter.Beats;
            int count = beats >= 4 ? 2 : 1;
            float length = beats / (float)count;

            int melody = bar.Melody.Count > 0 ? bar.Melody[0].Degree : 4;
            int rising = melody - bar.PreviousMelodyDegree;
            int current = bar.CounterDegree;
            for (int i = 0; i < count; i++)
            {
                // Contrary motion: when the tune climbs this comes down. It's what makes two lines sound like a conversation.
                int want = current + (rising > 0 ? -1 : rising < 0 ? 1 : 0);
                int best = want;
                int closest = int.MaxValue;
                for (int degree = want - 3; degree <= want + 3; degree++)
                {
                    if (!bar.Chord.Contains(degree) || Math.Abs(degree - want) >= closest) continue;
                    closest = Math.Abs(degree - want);
                    best = degree;
                }

                // Back into range by a whole octave, which keeps it on the chord.
                while (best > 7) best -= 7;
                while (best < -7) best += 7;
                current = best;
                bar.Add(part, Fit(part, bar.Pitch(current - 7 + part.Octave * 7)), i * length, length * 0.98f, loudness, Articulation.Normal);
            }
            bar.CounterDegree = current;
        }

        /// <summary>Held chords, spaced by voice leading: each voice moves as little as it can.</summary>
        public static void Pad(BarContext bar, Part part)
        {
            (int low, int high) = Ranges[(int)part.Section];
            int centre = bar.Tonic + part.Octave * 12;
            int lowest = Math.Max(low, centre - 7);
            int highest = Math.Min(high, centre + 14);

            // Accompaniment stays under the tune. A pad in the melody's own register covers it, however quietly it's played.
            if (part.Octave <= 0 && bar.Melody.Count > 0)
            {
                int floor = int.MaxValue;
                foreach (Tone tone in bar.Melody) floor = Math.Min(floor, bar.Pitch(tone.Degree));
                highest = Math.Max(Math.Min(highest, floor - 2), lowest + 9);
            }

            bar.Voicings.TryGetValue(part, out int[]? previous);
            int[] voicing = Harmony.Voice(bar.Chord, bar.Mode, bar.Tonic + part.Octave * 12, previous ?? Array.Empty<int>(), lowest, highest, 3);
            bar.Voicings[part] = voicing;

            float loudness = bar.Loudness(part);
            float beats = bar.Meter.Beats;
            Articulation style = part.Name == "tremolo" ? Articulation.Tremolo : bar.Bar == 0 ? Articulation.Swell : Articulation.Normal;
            foreach (int pitch in voicing) bar.Add(part, pitch, 0f, beats * 1.04f, loudness, style);
        }

        /// <summary>A note held under everything, with the harmony moving above it. Nothing builds dread like a bass that won't move.</summary>
        public static void Pedal(BarContext bar, Part part)
        {
            float loudness = bar.Loudness(part);
            int pitch = Fit(part, bar.Tonic - 12 + part.Octave * 12);

            // Where the chord has a note a semitone from it, a held bass would grind all bar long, so here it gives way to the chord's own root.
            foreach (int degree in bar.Chord.Degrees)
            {
                int interval = ((bar.Pitch(degree) - bar.Tonic) % 12 + 12) % 12;
                if (interval is 1 or 11) pitch = Fit(part, bar.Pitch(bar.Chord.Root - 14 + part.Octave * 7));
            }

            bool still = part.Name == "still";
            bar.Add(part, pitch, 0f, bar.Meter.Beats * 1.02f, loudness, still ? Articulation.Swell : Articulation.Tremolo);
        }

        /// <summary>The chord broken into a flowing line, as a harp plays it: up from a low root and turning back.</summary>
        public static void Arpeggio(BarContext bar, Part part)
        {
            float loudness = bar.Loudness(part);
            int[] degrees = bar.Chord.Degrees;
            bool slow = part.Name == "slow";
            int beats = bar.Meter.Beats;
            int divisions = bar.Meter.Divisions;
            int notes = beats * (slow ? 1 : divisions);
            float step = beats / (float)notes;

            int root = bar.Chord.Root;
            int bass = Fit(part, bar.Pitch(root - 14));
            bar.Add(part, bass, 0f, beats, loudness + 0.06f);

            // Chord tones in order up two octaves, turning back at the top: the line of a broken chord that never quite lands.
            var line = new List<int>();
            for (int octave = 0; octave < 2; octave++)
            {
                foreach (int degree in degrees) line.Add(degree + octave * 7);
            }
            int position = 1;
            int direction = 1;
            for (int n = 1; n < notes; n++)
            {
                int pitch = Fit(part, bar.Pitch(line[position] - 7));
                float weight = bar.Meter.Weight(n * step);
                bar.Add(part, pitch, n * step, step * 3f, loudness - 0.06f + weight * 0.08f);
                position += direction;
                if (position >= line.Count - 1) direction = -1;
                else if (position <= 0) direction = 1;
            }
        }

        /// <summary>Chords struck across the strings. It's spread by a few milliseconds a string, as a hand can't reach them all at once.</summary>
        public static void Strum(BarContext bar, Part part)
        {
            float loudness = bar.Loudness(part);
            int[] degrees = bar.Chord.Degrees;
            var pitches = new List<int>();
            foreach (int degree in degrees) pitches.Add(Fit(part, bar.Pitch(degree - 7)));
            pitches.Add(Fit(part, bar.Pitch(degrees[0])));
            pitches.Sort();

            // A chord after each beat's bass note: the bounce of a dance.
            int beats = bar.Meter.Beats;
            int divisions = bar.Meter.Divisions;
            float unit = 1f / divisions;
            if (divisions == 3)
            {
                for (int beat = 0; beat < beats; beat++)
                {
                    Stroke(bar, part, pitches, beat, 0.95f, loudness + 0.05f, down: true);
                    Stroke(bar, part, pitches, beat + 2f * unit, 0.3f, loudness - 0.12f, down: false);
                }
                return;
            }
            for (int beat = 0; beat < beats; beat++)
            {
                bool strong = beat == 0 || (beats == 4 && beat == 2);
                Stroke(bar, part, pitches, beat, 0.9f, loudness + (strong ? 0.06f : -0.04f), down: true);
                if (beat % 2 == 1) Stroke(bar, part, pitches, beat + 0.5f, 0.4f, loudness - 0.12f, down: false);
            }
        }

        private static void Stroke(BarContext bar, Part part, List<int> pitches, float beat, float length, float loudness, bool down)
        {
            float spread = 0.012f / bar.BeatSeconds;
            for (int i = 0; i < pitches.Count; i++)
            {
                int index = down ? i : pitches.Count - 1 - i;
                bar.Add(part, pitches[index], beat + i * spread, length, loudness - i * 0.015f);
            }
        }

        /// <summary>The bottom of the harmony.</summary>
        public static void Bass(BarContext bar, Part part)
        {
            float loudness = bar.Loudness(part);
            int root = Fit(part, bar.Pitch(bar.Chord.Root - 14 + part.Octave * 7));
            int fifth = Fit(part, bar.Pitch(bar.Chord.Root - 10 + part.Octave * 7));
            int beats = bar.Meter.Beats;

            if (part.Name == "long")
            {
                bar.Add(part, root, 0f, beats * 1.02f, loudness);
                return;
            }

            // A dance: root on the strong beat and fifth on the next, as a bass walks.
            if (bar.Meter.Divisions == 3)
            {
                bar.Add(part, root, 0f, 1f, loudness + 0.08f, Articulation.Staccato);
                if (beats > 1) bar.Add(part, fifth, 1f, 1f, loudness - 0.05f, Articulation.Staccato);
                return;
            }
            bar.Add(part, root, 0f, 1f, loudness + 0.08f, Articulation.Staccato);
            if (beats >= 3) bar.Add(part, fifth, beats == 4 ? 2f : 1f, 1f, loudness, Articulation.Staccato);
        }

        // A driving figure, 3 + 3 + 2: it's what makes fast string writing lean forward instead of ticking.
        private static readonly float[] Accents = { 1f, 0.5f, 0.55f, 1f, 0.5f, 0.55f, 1f, 0.6f };

        /// <summary>Strings driving in repeated notes: what keeps a fight moving.</summary>
        public static void Ostinato(BarContext bar, Part part)
        {
            float loudness = bar.Loudness(part);
            int chord = bar.Chord.Root;
            int[] degrees = bar.Chord.Degrees;
            int notes = bar.Meter.Beats * 2;
            bool high = part.Name == "high";
            bool middle = part.Name == "middle";
            int octave = high ? 0 : middle ? -7 : -14;

            for (int n = 0; n < notes; n++)
            {
                // The low strings keep to the root, and those above move between chord tones.
                int degree = chord + octave;
                if (high) degree = degrees[(n / 2) % degrees.Length] + octave;
                else if (middle) degree = degrees[n % 2 == 0 ? 0 : Math.Min(1, degrees.Length - 1)] + octave;
                degree += part.Octave * 7;

                float accent = Accents[n % Accents.Length];
                bar.Add(part, Fit(part, bar.Pitch(degree)), n * 0.5f, 0.42f, loudness * (0.72f + 0.28f * accent), accent >= 0.9f ? Articulation.Marcato : Articulation.Staccato);
            }
        }

        /// <summary>Brass on the strong moments: the chord, thrown like a punch.</summary>
        public static void Stabs(BarContext bar, Part part)
        {
            float loudness = bar.Loudness(part);
            bool sparse = bar.Intensity < 0.6f;
            float[] beats = sparse ? new[] { 0f } : new[] { 0f, 1.5f, 3f };
            // Low down, a third is a blur: arrangers keep the bottom open, root and fifth, and put the third up where it can be heard.
            int root = Fit(part, bar.Pitch(bar.Chord.Root - 14));
            if (root > 50) root -= 12;
            int[] degrees = bar.Chord.Degrees;
            int[] voicing = { root, root + 7, Fit(part, bar.Pitch(degrees[Math.Min(1, degrees.Length - 1)])) };
            foreach (float beat in beats)
            {
                foreach (int pitch in voicing) bar.Add(part, pitch, beat, beat == 0f ? 1.2f : 0.6f, loudness, Articulation.Marcato);
            }
        }

        /// <summary>A cymbal rising into the next phrase, which is what makes an arrival feel like one.</summary>
        public static void Swell(BarContext bar, Part part)
        {
            if (!bar.LastBar) return;
            bar.Add(part, 60, bar.Meter.Beats - 1.6f / bar.BeatSeconds, 2f, bar.Loudness(part));
        }

        /// <summary>A bell or a chime on the tonic, marking the top of a phrase.</summary>
        public static void Toll(BarContext bar, Part part)
        {
            if (bar.Bar % 4 != 0) return;
            bar.Add(part, Fit(part, bar.Pitch(bar.Chord.Root + part.Octave * 7)), 0f, 3f, bar.Loudness(part));
        }

        // Patterns for the drums, as (beat, how hard). The drums of a fight are written to answer the strings, not to double them.
        private static readonly (float beat, float hard)[] WarLow = { (0f, 1f), (2f, 0.8f), (2.5f, 0.6f) };
        private static readonly (float beat, float hard)[] WarHigh = { (0f, 1f), (1f, 0.6f), (1.5f, 0.75f), (2f, 0.9f), (3f, 0.7f), (3.5f, 0.8f) };

        /// <summary>The drums.</summary>
        public static void Drums(BarContext bar, Part part)
        {
            float loudness = bar.Loudness(part);
            int pitch = part.Section == Section.Timpani ? Fit(part, bar.Tonic - 24) : 40;
            int beats = bar.Meter.Beats;

            switch (part.Name)
            {
                case "cadence":
                    if (bar.LastBar || bar.Halfway) bar.Add(part, pitch, beats - 1f, 1f, loudness);
                    if (bar.Bar == 0) bar.Add(part, pitch, 0f, 1f, loudness);
                    break;

                case "heart":
                    // Two soft blows and a wait, at a resting pulse.
                    bar.Add(part, pitch, 0f, 0.5f, loudness);
                    bar.Add(part, pitch, 0.6f, 0.5f, loudness * 0.7f);
                    break;

                case "war":
                    var pattern = bar.Intensity > 0.55f ? WarHigh : WarLow;
                    foreach ((float beat, float hard) in pattern)
                    {
                        // The timpani takes the fifth of the chord on its off beats, as a player with two drums would.
                        int note = part.Section == Section.Timpani && beat % 1f != 0f ? Fit(part, pitch + 7) : pitch;
                        bar.Add(part, note, beat, 0.5f, loudness * hard);
                    }
                    if (bar.LastBar) Fill(bar, part, pitch, loudness);
                    break;

                case "dance":
                    Dance(bar, part, pitch, loudness);
                    break;
            }
        }

        private static void Fill(BarContext bar, Part part, int pitch, float loudness)
        {
            for (int i = 0; i < 4; i++) bar.Add(part, pitch, bar.Meter.Beats - 1f + i * 0.25f, 0.25f, loudness * (0.6f + i * 0.12f));
        }

        private static void Dance(BarContext bar, Part part, int pitch, float loudness)
        {
            int beats = bar.Meter.Beats;
            bool compound = bar.Meter.Divisions == 3;
            if (part.Section == Section.Tambourine)
            {
                for (int beat = 0; beat < beats; beat++)
                {
                    bar.Add(part, pitch, beat, 0.3f, loudness * (beat == 0 ? 1f : 0.8f));
                    bar.Add(part, pitch, beat + (compound ? 2f / 3f : 0.5f), 0.3f, loudness * 0.6f);
                }
                return;
            }
            for (int beat = 0; beat < beats; beat++)
            {
                bar.Add(part, pitch, beat, 0.5f, loudness * (beat == 0 ? 1f : 0.72f));
                if (compound) bar.Add(part, pitch, beat + 2f / 3f, 0.3f, loudness * 0.55f);
            }
        }
    }
}
