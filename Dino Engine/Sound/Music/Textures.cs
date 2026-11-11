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
        public Chord[] Harmony = Array.Empty<Chord>();  // The whole phrase's chords, for parts that look ahead.
        public Mode Home = Mode.Aeolian;        // The piece's own mode, where this bar's chord may be borrowed from another.
        public float AbsoluteBeat;              // Beats since the piece began, at the start of this bar, for what goes round out of step with the bars.
        public float HymnAt = -1f;              // Where the hymn's voicing was last worked out, and what it was.
        public int? MelodyShift;                // Degrees the tune is moved by, by whoever is playing it in this bar, so accompaniment knows where it really is.
        public int[] Hymn = Array.Empty<int>();

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
            ranges[(int)Section.Piano] = (33, 96);
            ranges[(int)Section.Guitar] = (40, 79);
            ranges[(int)Section.Dulcimer] = (52, 88);
            ranges[(int)Section.Pad] = (40, 84);
            ranges[(int)Section.SoloCello] = (36, 76);
            ranges[(int)Section.EnglishHorn] = (52, 81);
            ranges[(int)Section.Bassoon] = (34, 72);
            ranges[(int)Section.AltoFlute] = (55, 91);
            ranges[(int)Section.BassFlute] = (48, 84);
            ranges[(int)Section.Ocarina] = (60, 88);
            ranges[(int)Section.Tuba] = (28, 58);
            ranges[(int)Section.BassClarinet] = (34, 70);
            ranges[(int)Section.MutedViolins] = (55, 96);
            ranges[(int)Section.MutedCellos] = (36, 72);
            ranges[(int)Section.Voices] = (45, 77);
            ranges[(int)Section.HandDrum] = (36, 60);
            ranges[(int)Section.RimDrum] = (36, 80);
            ranges[(int)Section.Shaker] = (40, 80);
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

        /// <summary>
        /// Onsets spread as evenly as they'll go over a number of steps, turned round by some: Toussaint found that this one rule gives the timelines of dozens of the world's
        /// traditions, from the tresillo at three in eight to the five in eight of a cumbia's bell.
        /// </summary>
        public static bool[] Euclid(int onsets, int steps, int rotation = 0)
        {
            var pattern = new bool[steps];
            for (int i = 0; i < steps; i++)
            {
                int at = ((i + rotation) % steps + steps) % steps;
                pattern[i] = (at * onsets) % steps < onsets;
            }
            return pattern;
        }

        // A pitch folded by octaves into a window.
        private static int FitInto(int pitch, int low, int high)
        {
            while (pitch < low) pitch += 12;
            while (pitch > high && pitch - 12 >= low) pitch -= 12;
            return pitch;
        }

        // Every pitch of the bar's chord between two pitches, from the bottom.
        private static List<int> ChordTones(BarContext bar, int low, int high)
        {
            var pitches = new List<int>();
            foreach (int degree in bar.Chord.Degrees)
            {
                for (int octave = -4; octave <= 4; octave++)
                {
                    int pitch = bar.Pitch(degree + octave * 7);
                    if (pitch >= low && pitch <= high && !pitches.Contains(pitch)) pitches.Add(pitch);
                }
            }
            pitches.Sort();

            // A note a semitone above another is a cluster, as the root of a major seventh is over its seventh: players leave the upper one out.
            for (int i = pitches.Count - 1; i > 0; i--)
            {
                if (pitches[i] - pitches[i - 1] == 1) pitches.RemoveAt(i);
            }
            return pitches;
        }

        // The chord's degree nearest to one.
        private static int NearestChordDegree(in Chord chord, int degree)
        {
            int best = degree;
            int closest = int.MaxValue;
            for (int d = degree - 4; d <= degree + 4; d++)
            {
                if (!chord.Contains(d) || Math.Abs(d - degree) >= closest) continue;
                closest = Math.Abs(d - degree);
                best = d;
            }
            return best;
        }

        // The lowest note the tune has in this bar, as the tune's own part plays it, so an accompaniment can keep under it.
        private static int MelodyFloor(BarContext bar, int otherwise)
        {
            int floor = int.MaxValue;
            foreach (Tone tone in bar.Melody) floor = Math.Min(floor, bar.Pitch(tone.Degree + (bar.MelodyShift ?? 0)));
            if (floor == int.MaxValue) return otherwise;
            if (bar.MelodyShift == null)
            {
                while (floor < 60) floor += 12;
            }
            return floor;
        }

        /// <summary>Where the tune is moved to by the part that plays it, for the accompaniment's sake.</summary>
        internal static int TuneShift(BarContext bar, Part part) => Register(bar, part);

        /// <summary>
        /// A pianist's left hand. Slow piano music lives on a few of these: a low root held and the chord on the beats after it, as Satie's slow dances go; a wide chord rolled
        /// upwards from the bass and left to ring; the chord broken into a running figure under the pedal; or a soft chord simply repeated on each beat.
        /// </summary>
        public static void PianoHands(BarContext bar, Part part)
        {
            float loudness = bar.Loudness(part);
            int beats = bar.Meter.Beats;
            int bass = FitInto(bar.Pitch(bar.Chord.Root - 14), 36, 50);
            int ceiling = Math.Min(MelodyFloor(bar, 67) - 2, 66);
            List<int> tones = ChordTones(bar, bass + 5, Math.Max(ceiling, bass + 14));
            if (tones.Count == 0) tones.Add(bass + 12);
            float ring = beats + 0.1f;

            switch (part.Name)
            {
                case "satie":
                    bar.Add(part, bass, 0f, ring, loudness + 0.05f);
                    for (int beat = 1; beat < beats; beat++)
                    {
                        for (int i = Math.Max(0, tones.Count - 3); i < tones.Count; i++) bar.Add(part, tones[i], beat + (i - tones.Count) * 0.012f, 0.95f, loudness - 0.06f);
                    }
                    break;

                case "tenths":
                    // Root, fifth and tenth, rolled from the bottom, and once more halfway through a long bar, softer.
                    int[] wide = { bass, bass + bar.Pitch(bar.Chord.Root + 4) - bar.Pitch(bar.Chord.Root), FitInto(bar.Pitch(bar.Chord.Degrees[Math.Min(1, bar.Chord.Degrees.Length - 1)]), bass + 12, bass + 18) };
                    for (int i = 0; i < wide.Length; i++) bar.Add(part, wide[i], i * 0.09f, ring - i * 0.09f, loudness - i * 0.03f);
                    if (beats >= 4) bar.Add(part, tones[tones.Count / 2], beats / 2f, beats / 2f + 0.1f, loudness - 0.1f);
                    break;

                case "pulse":
                    bar.Add(part, bass, 0f, ring, loudness);
                    for (int beat = 0; beat < beats; beat++)
                    {
                        for (int i = Math.Max(0, tones.Count - 2); i < tones.Count; i++) bar.Add(part, tones[i], beat, 0.9f, loudness - (beat == 0 ? 0.02f : 0.1f));
                    }
                    break;

                default:
                    // Broken under the pedal: up through the chord and turning back, each note left to ring to the end of the bar.
                    int notes = beats * bar.Meter.Divisions;
                    float step = beats / (float)notes;
                    var line = new List<int> { bass };
                    line.AddRange(tones);
                    int position = 0, direction = 1;
                    for (int n = 0; n < notes; n++)
                    {
                        float at = n * step;
                        bar.Add(part, line[position], at, ring - at, loudness - (n == 0 ? 0f : 0.08f) + bar.Meter.Weight(at) * 0.05f);
                        position += direction;
                        if (position >= line.Count - 1) direction = -1;
                        else if (position <= 0) direction = 1;
                        position = Math.Clamp(position, 0, line.Count - 1);
                    }
                    break;
            }
        }

        /// <summary>
        /// Fingers on a steel guitar: the thumb on a bass string and the fingers on the treble, every note left to ring. In four, the thumb alternates root and fifth under the
        /// fingers' off beats, as a Travis picker does; in three, a bass and two pinches, the waltz of a criollo guitar, or the chord picked up and down in eighths; in six, the
        /// fingers roll in threes over the thumb.
        /// </summary>
        public static void Fingerpick(BarContext bar, Part part)
        {
            float loudness = bar.Loudness(part);
            int beats = bar.Meter.Beats;
            int bass = FitInto(bar.Pitch(bar.Chord.Root - 14), 40, 52);
            int[] degrees = bar.Chord.Degrees;
            int fifth = FitInto(bar.Pitch(degrees[Math.Min(2, degrees.Length - 1)] - 14), 43, 55);
            List<int> treble = ChordTones(bar, 55, 71);
            if (treble.Count == 0) treble.Add(bass + 12);
            while (treble.Count < 3) treble.Add(treble[^1] + 12 <= 76 ? treble[^1] + 12 : treble[0]);
            float end = beats + 0.15f;

            void Thumb(int pitch, float at) => bar.Add(part, pitch, at, end - at, loudness + 0.05f);
            void Finger(int pitch, float at) => bar.Add(part, pitch, at, end - at, loudness - 0.06f + bar.Meter.Weight(at) * 0.04f);

            if (bar.Meter.Divisions == 3)
            {
                for (int beat = 0; beat < beats; beat++)
                {
                    Thumb(beat % 2 == 0 ? bass : fifth, beat);
                    Finger(treble[0], beat + 1f / 3f);
                    Finger(treble[beat % 2 == 0 ? 1 : 2], beat + 2f / 3f);
                }
                return;
            }

            if (beats == 3)
            {
                Thumb(bass, 0f);
                if (part.Name == "lilt")
                {
                    int[] order = { 0, 1, 2, 1, 0 };
                    for (int i = 0; i < order.Length; i++) Finger(treble[order[i]], 0.5f + i * 0.5f);
                }
                else
                {
                    for (int beat = 1; beat < 3; beat++)
                    {
                        Finger(treble[1], beat);
                        Finger(treble[2], beat + 0.01f);
                    }
                }
                return;
            }

            for (int beat = 0; beat < beats; beat++)
            {
                Thumb(beat % 2 == 0 ? bass : fifth, beat);
                Finger(treble[(beat + 2) % 3], beat + 0.5f);
            }
            Finger(treble[2], 0.02f);
        }

        /// <summary>
        /// Chords sung together, moving when the tune moves: a hymn's texture, in which every voice keeps the tune's rhythm, which is what makes a theme sound reverent rather than
        /// accompanied. Its bass keeps to the chord's root; the voices above take the nearest notes of the chord under the tune.
        /// </summary>
        public static void Hymn(BarContext bar, Part part)
        {
            float loudness = bar.Loudness(part);
            int beats = bar.Meter.Beats;
            if (bar.HymnAt != bar.AbsoluteBeat)
            {
                bar.HymnAt = bar.AbsoluteBeat;
                int top = Math.Min(MelodyFloor(bar, 69) - 1, 67);
                bar.Voicings.TryGetValue(HymnVoices, out int[]? previous);
                bar.Hymn = Harmony.Voice(bar.Chord, bar.Mode, bar.Tonic, previous ?? Array.Empty<int>(), Math.Min(48, top - 10), top, 2);
                bar.Voicings[HymnVoices] = bar.Hymn;
            }

            int pitch = part.Name switch
            {
                "bass" => Fit(part, FitInto(bar.Pitch(bar.Chord.Root - 14), 36, 50)),
                "tenor" => Fit(part, bar.Hymn[0]),
                _ => Fit(part, bar.Hymn[^1]),
            };

            // Where the tune has notes, the chord moves with them; where it rests, on the strong beats.
            var onsets = new List<float>();
            foreach (Tone tone in bar.Melody) onsets.Add(tone.Beat);
            if (onsets.Count == 0)
            {
                onsets.Add(0f);
                if (beats == 4) onsets.Add(2f);
            }
            for (int i = 0; i < onsets.Count; i++)
            {
                float next = i + 1 < onsets.Count ? onsets[i + 1] : beats;
                bar.Add(part, pitch, onsets[i], (next - onsets[i]) * 1.02f, loudness + bar.Meter.Weight(onsets[i]) * 0.06f, i == 0 ? Articulation.Normal : Articulation.Legato);
            }
        }

        private static readonly Part HymnVoices = new();

        /// <summary>
        /// The tonic and its fifth held on and on under the harmony, as a drone holds: it gives way to a chord's own root only where the chord has a note a semitone from it, and
        /// for as long as that chord lasts, so it never grinds.
        /// </summary>
        public static void Drone(BarContext bar, Part part)
        {
            float loudness = bar.Loudness(part);

            // What's held in a bar, as semitones above the tonic: the tonic itself unless the chord has a note a semitone from it, when it gives way to the chord's root, and the
            // fifth above whichever it is, unless that's the one the chord rubs against, when it's simply left out.
            (int root, bool fifth) Held(int index)
            {
                Chord chord = bar.Harmony.Length > 0 ? bar.Harmony[index % bar.Harmony.Length] : bar.Chord;
                Mode mode = chord.Borrow ?? bar.Home;
                bool Rubs(int note)
                {
                    foreach (int degree in chord.Degrees)
                    {
                        int apart = Math.Abs(((mode.Pitch(0, degree) % 12) + 12) % 12 - ((note % 12) + 12) % 12);
                        if (apart is 1 or 11) return true;
                    }
                    return false;
                }
                int root = Rubs(0) ? mode.Pitch(0, chord.Root) % 12 : 0;
                return (root, !Rubs(root + 7));
            }

            (int root, bool fifth) here = Held(bar.Bar);
            (int root, bool fifth)? before = bar.Bar > 0 ? Held(bar.Bar - 1) : null;
            int low = Fit(part, bar.Tonic + here.root - 24 + part.Octave * 12);

            // Each held note goes on for as long as the bars ahead keep it, and is only struck again when it has to be.
            if (before == null || before.Value.root != here.root)
            {
                int bars = 1;
                while (bar.Bar + bars < bar.Bars && Held(bar.Bar + bars).root == here.root) bars++;
                bar.Add(part, low, 0f, bars * bar.Meter.Beats + 0.1f, loudness, Articulation.Swell);
            }
            if (part.Name != "single" && here.fifth && (before == null || before.Value.root != here.root || !before.Value.fifth))
            {
                int bars = 1;
                while (bar.Bar + bars < bar.Bars && Held(bar.Bar + bars) is var next && next.root == here.root && next.fifth) bars++;
                bar.Add(part, Fit(part, low + 7), 0f, bars * bar.Meter.Beats + 0.1f, loudness - 0.05f, Articulation.Swell);
            }
        }

        /// <summary>The end of what the tune just said, said again higher and softer a moment later: an answer across a valley, or the hall giving it back.</summary>
        public static void Echo(BarContext bar, Part part)
        {
            if (bar.Melody.Count < 2) return;
            Tone last = bar.Melody[^1];
            if (last.Length < 1f) return;

            int shift = Register(bar, part);
            float delay = part.StepBeats > 0.6f ? part.StepBeats : 1.5f;
            float loudness = bar.Loudness(part);
            for (int i = Math.Max(0, bar.Melody.Count - 2); i < bar.Melody.Count; i++)
            {
                Tone tone = bar.Melody[i];
                bar.Add(part, Fit(part, bar.Pitch(tone.Degree + shift)), tone.Beat + delay, MathF.Min(tone.Length, 2f), loudness - 0.08f * (bar.Melody.Count - i));
            }
        }

        /// <summary>
        /// A short phrase going round on a loop of its own length, out of step with the bars and with every other loop, as Eno built his airport music from tape loops of
        /// lengths that never line up: the same few notes meet each other differently every time round, for as long as anyone listens. Each note is moved to the nearest note of the
        /// chord it lands in, so that however they fall, they agree.
        /// </summary>
        public static void Loops(BarContext bar, Part part)
        {
            float loop = part.LoopBeats;
            if (loop <= 0f || part.LoopTimes.Length == 0) return;
            float start = bar.AbsoluteBeat;
            float end = start + bar.Meter.Beats;
            float loudness = bar.Loudness(part);
            for (int round = (int)MathF.Floor(start / loop); round * loop < end; round++)
            {
                for (int i = 0; i < part.LoopTimes.Length; i++)
                {
                    float at = round * loop + part.LoopTimes[i];
                    if (at < start || at >= end) continue;
                    int degree = NearestChordDegree(bar.Chord, part.LoopDegrees[i]);
                    bar.Add(part, Fit(part, bar.Pitch(degree + part.Octave * 7)), at - start, MathF.Min(loop * 0.6f, 4f), loudness - i * 0.03f);
                }
            }
        }

        /// <summary>A rhythm on a grid, for drums and shakers. It runs on through the bars by itself, so a pattern longer than a bar comes round as it should.</summary>
        public static void Steps(BarContext bar, Part part)
        {
            bool[]? steps = part.Steps;
            if (steps == null || steps.Length == 0) return;
            float loudness = bar.Loudness(part);
            float span = steps.Length * part.StepBeats;
            float start = bar.AbsoluteBeat;
            int pitch = part.Section == Section.Timpani ? Fit(part, bar.Tonic - 24) : Fit(part, 48 + part.Octave * 12);
            int first = (int)MathF.Floor(start / part.StepBeats);
            for (int index = first; index * part.StepBeats < start + bar.Meter.Beats; index++)
            {
                float at = index * part.StepBeats - start;
                if (at < -1e-4f) continue;
                int step = ((index % steps.Length) + steps.Length) % steps.Length;
                if (!steps[step]) continue;

                // The first stroke of the pattern leans, and so does anything on a beat.
                bool downbeat = step == 0 || MathF.Abs(at - MathF.Round(at)) < 1e-3f;
                bar.Add(part, pitch, at, part.StepBeats, loudness * (step == 0 ? 1f : downbeat ? 0.82f : 0.64f));
            }
            _ = span;
        }

        /// <summary>A few high notes of the chord, here and there where the bar is quiet, as light catches on water.</summary>
        public static void Sparkle(BarContext bar, Part part)
        {
            float loudness = bar.Loudness(part);
            int count = bar.Random.Float() < 0.45f ? 1 : bar.Random.Float() < 0.5f ? 2 : 0;
            if (bar.Intensity > 0.6f) count++;
            List<int> tones = ChordTones(bar, 72, 91);
            if (tones.Count == 0) return;
            for (int i = 0; i < count; i++)
            {
                float at = 0.5f + bar.Random.Int(Math.Max(bar.Meter.Beats * 2 - 1, 1)) * 0.5f;
                bar.Add(part, Fit(part, tones[bar.Random.Int(tones.Count)] + part.Octave * 12), at, 1.5f, loudness - 0.05f);
            }
        }

        /// <summary>
        /// Something low that won't stay on its note: off the root by a semitone and back, short and dry, as something circling does. Short enough that it never sits on a clash, on
        /// a rhythm of its own.
        /// </summary>
        public static void Prowl(BarContext bar, Part part)
        {
            bool[] steps = part.Steps ?? Euclid(5, 8);
            float loudness = bar.Loudness(part);
            int root = Fit(part, FitInto(bar.Pitch(bar.Chord.Root - 14), 31, 46));
            int[] turn = part.Name == "wide" ? new[] { 0, 1, 0, 3 } : new[] { 0, 1, 0, -1 };
            int played = 0;
            for (int step = 0; step * part.StepBeats < bar.Meter.Beats; step++)
            {
                if (!steps[step % steps.Length]) continue;
                int pitch = Fit(part, root + turn[played++ % turn.Length]);
                bar.Add(part, pitch, step * part.StepBeats, part.StepBeats * 0.8f, loudness * (step == 0 ? 1f : 0.78f), step == 0 ? Articulation.Marcato : Articulation.Staccato);
            }
        }

        /// <summary>Brass calling: the chord's top flung out at the head of a phrase, and a triplet climbing into the next at its end.</summary>
        public static void Fanfare(BarContext bar, Part part)
        {
            float loudness = bar.Loudness(part);
            int[] degrees = bar.Chord.Degrees;
            if (bar.Bar % 4 == 0)
            {
                int top = Fit(part, bar.Pitch(degrees[0] + part.Octave * 7));
                bar.Add(part, top, 0f, 1.5f, loudness, Articulation.Marcato);
                bar.Add(part, Fit(part, bar.Pitch(degrees[Math.Min(2, degrees.Length - 1)] + part.Octave * 7)), 1.5f, 0.5f, loudness - 0.08f, Articulation.Staccato);
                bar.Add(part, top, 2f, 1.8f, loudness - 0.04f, Articulation.Marcato);
            }
            if (bar.LastBar || bar.Halfway)
            {
                int[] next = bar.NextChord.Degrees;
                float beat = bar.Meter.Beats - 1f;
                for (int i = 0; i < 3; i++) bar.Add(part, Fit(part, bar.Pitch(next[0] - 3 + i + part.Octave * 7)), beat + i / 3f, 0.3f, loudness - 0.1f + i * 0.05f, Articulation.Staccato);
            }
        }

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

                // A land's fight has its own accents, an even spread of strong strokes over the bar, where it's been given one.
                float accent = part.Steps != null ? (part.Steps[n % part.Steps.Length] ? 1f : 0.5f) : Accents[n % Accents.Length];
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

            // The chord's own fifth, which isn't always a perfect one.
            int fifth = root + bar.Pitch(bar.Chord.Root + 4) - bar.Pitch(bar.Chord.Root);
            int[] voicing = { root, fifth, Fit(part, bar.Pitch(degrees[Math.Min(1, degrees.Length - 1)])) };
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
