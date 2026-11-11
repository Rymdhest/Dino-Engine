using Dino_Engine.Sound.Dsp;

namespace Dino_Engine.Sound.Music
{
    /// <summary>
    /// Chord progressions. Harmony is the slowest thing a listener follows, and much of a piece's character is here: a fantasy score leans on modes and on
    /// chords a step or a third apart, not the dominant-to-tonic of a hymn. Each mode has progressions that show it off, in eight bar periods with a pause
    /// half way, as measured tunes change chord about once a bar.
    /// </summary>
    /// <summary>The kind of harmony a piece speaks: which chords it uses and how fast they change.</summary>
    public enum HarmonicLanguage
    {
        Modal,      // Folk modes, chords a step apart: the old tables.
        Hymn,       // Plain major chords moving to the fourth and home, with a borrowed flat seventh for wonder, as a film's hymn to something vast does.
        Ambient,    // Two or three soft chords a phrase, each held two bars, with ninths and sevenths in them.
        Dark,       // Long stays on the tonic, and the flat second and sixth over it.
        Epic,       // The minor-key engines of film battle music: down by thirds and steps to a dominant that drives back round.
    }

    public static class Harmony
    {
        private static readonly int[][] Ionian =
        {
            new[] { 0, 4, 5, 3, 0, 3, 4, 0 },   // I V vi IV I IV V I
            new[] { 0, 3, 0, 4, 5, 3, 4, 0 },   // I IV I V vi IV V I
            new[] { 0, 5, 3, 4, 0, 5, 1, 0 },   // I vi IV V I vi ii I
            new[] { 0, 3, 5, 4, 3, 0, 4, 0 },
        };

        private static readonly int[][] Mixolydian =
        {
            new[] { 0, 6, 3, 0, 0, 6, 3, 0 },   // I bVII IV I
            new[] { 0, 3, 6, 3, 0, 3, 6, 0 },
            new[] { 0, 6, 0, 3, 5, 6, 3, 0 },
        };

        private static readonly int[][] Dorian =
        {
            new[] { 0, 3, 0, 3, 6, 3, 6, 0 },   // i IV i IV bVII IV bVII i
            new[] { 0, 6, 3, 0, 2, 6, 3, 0 },
            new[] { 0, 3, 6, 2, 0, 3, 6, 0 },
        };

        private static readonly int[][] Aeolian =
        {
            new[] { 0, 5, 2, 6, 0, 5, 3, 0 },   // i VI III VII i VI iv i
            new[] { 0, 3, 6, 2, 5, 3, 4, 0 },   // i iv VII III VI iv v i
            new[] { 0, 6, 5, 6, 0, 6, 5, 0 },   // i VII VI VII
            new[] { 0, 2, 6, 3, 5, 2, 6, 0 },
        };

        private static readonly int[][] Lydian =
        {
            new[] { 0, 1, 0, 1, 5, 1, 4, 0 },   // I II I II vi II V I
            new[] { 0, 4, 1, 0, 5, 1, 4, 0 },
        };

        private static readonly int[][] Phrygian =
        {
            new[] { 0, 1, 0, 6, 0, 1, 6, 0 },   // i bII i bVII
            new[] { 0, 5, 1, 0, 3, 1, 6, 0 },
        };

        // The minor with a leading note is all about its dominant, so it has progressions of its own: its other chords on the mediant and the seventh are augmented and diminished.
        private static readonly int[][] HarmonicMinor =
        {
            new[] { 0, 5, 3, 4, 0, 5, 3, 4 },   // i VI iv V
            new[] { 0, 3, 4, 0, 5, 3, 4, 0 },   // i iv V i VI iv V i
            new[] { 0, 4, 5, 3, 0, 3, 4, 0 },
        };

        // The flat second over a major tonic is the whole of this mode's colour, so its progressions turn on it.
        private static readonly int[][] PhrygianDominant =
        {
            new[] { 0, 1, 0, 1, 6, 1, 0, 0 },   // I bII I bII bvii bII I
            new[] { 0, 1, 6, 1, 0, 3, 1, 0 },   // I bII bvii bII I iv bII I
            new[] { 0, 3, 1, 0, 0, 3, 1, 0 },
        };

        public static int[][] Progressions(Mode mode)
        {
            if (mode == Mode.HarmonicMinor) return HarmonicMinor;
            if (mode == Mode.PhrygianDominant) return PhrygianDominant;
            if (mode == Mode.Ionian) return Ionian;
            if (mode == Mode.Mixolydian) return Mixolydian;
            if (mode == Mode.Dorian) return Dorian;
            if (mode == Mode.Lydian) return Lydian;
            if (mode == Mode.Phrygian) return Phrygian;
            return Aeolian;
        }

        private static Chord C(int root, Mode? borrow = null, ChordColour colour = ChordColour.Triad) => new(root, colour, borrow);

        // The fourth and home is the hymn's motion, more than the fifth and home, and the flat seventh borrowed from the mixolydian is what sounds like something unearthly
        // arriving: a chord with no place in the key, that the ear still accepts as kin. Measured on film themes, a bright major hymn hardly uses a minor chord.
        private static readonly Chord[][] Hymn =
        {
            new[] { C(0, null, ChordColour.Added9), C(3), C(0), C(4), C(0), C(3), C(6, Mode.Mixolydian), C(0) },
            new[] { C(0), C(3), C(0), C(5), C(3), C(0), C(4, null, ChordColour.Suspended4), C(0) },
            new[] { C(0), C(4), C(3), C(0), C(0), C(3), C(6, Mode.Mixolydian), C(0) },
            new[] { C(0, null, ChordColour.Added9), C(6, Mode.Mixolydian), C(3), C(0), C(5), C(3), C(4), C(0) },
            new[] { C(0), C(3), C(5), C(3), C(0), C(3, Mode.Aeolian), C(0), C(0) },
        };

        // Slow piano music holds a chord for two bars and has few of them; the colour is in what's added. A borrowed flat seventh or minor fourth gives it its ache.
        private static readonly Chord[][] AmbientMajor =
        {
            new[] { C(0), C(0), C(3), C(3), C(5), C(5), C(3), C(3) },
            new[] { C(0), C(0), C(6, Mode.Mixolydian), C(6, Mode.Mixolydian), C(3), C(3), C(0), C(0) },
            new[] { C(0), C(0), C(2), C(2), C(3), C(3), C(3, Mode.Aeolian), C(0) },
            new[] { C(5), C(5), C(3), C(3), C(0), C(0), C(4, null, ChordColour.Suspended4), C(0) },
            new[] { C(0), C(0), C(1), C(1), C(3), C(3), C(0), C(0) },
        };

        private static readonly Chord[][] AmbientMinor =
        {
            new[] { C(0), C(0), C(5), C(5), C(2), C(2), C(6), C(6) },
            new[] { C(0), C(0), C(3), C(3), C(0), C(0), C(5), C(6) },
            new[] { C(0), C(0), C(3, Mode.Dorian), C(3, Mode.Dorian), C(5), C(5), C(0), C(0) },
        };

        // Dread is staying put: the tonic held, with the chords a semitone above and the minor sixth leaning on it.
        private static readonly Chord[][] DarkTables =
        {
            new[] { C(0), C(0), C(5), C(5), C(0), C(0), C(1, Mode.Phrygian), C(0) },
            new[] { C(0), C(3), C(0), C(5), C(0), C(3), C(4, Mode.HarmonicMinor), C(0) },
            new[] { C(0), C(5), C(3), C(4, Mode.HarmonicMinor), C(0), C(5), C(1, Mode.Phrygian), C(0) },
            new[] { C(0), C(0), C(1, Mode.Phrygian), C(1, Mode.Phrygian), C(0), C(0), C(6), C(0) },
            new[] { C(0), C(0, null, ChordColour.Open), C(5), C(5), C(3), C(3), C(0), C(0) },
        };

        // A fight's harmony has to drive round and round without ever quite settling, which is what these do: each ends on a dominant, or a chord a step away, that wants the start again.
        private static readonly Chord[][] EpicTables =
        {
            new[] { C(0), C(5), C(2), C(6), C(0), C(5), C(3), C(4, Mode.HarmonicMinor) },
            new[] { C(0), C(6), C(5), C(4, Mode.HarmonicMinor), C(0), C(6), C(5), C(4, Mode.HarmonicMinor) },
            new[] { C(0), C(3), C(5), C(4, Mode.HarmonicMinor), C(0), C(3), C(5), C(6) },
            new[] { C(0), C(1, Mode.Phrygian), C(0), C(6), C(0), C(1, Mode.Phrygian), C(5), C(4, Mode.HarmonicMinor) },
            new[] { C(0), C(0), C(5), C(5), C(3), C(3), C(4, Mode.HarmonicMinor), C(4, Mode.HarmonicMinor) },
        };

        /// <summary>
        /// An eight bar progression in a language, coloured as the language likes it. Where a chord is borrowed from another mode, everything in its bar follows that mode, the tune
        /// included, so nothing grinds against it.
        /// </summary>
        public static Chord[] Write(SoundRandom random, HarmonicLanguage language, Mode mode, float colour)
        {
            bool minor = mode.IsMinor;
            Chord[][]? tables = language switch
            {
                HarmonicLanguage.Hymn when !minor => Hymn,
                HarmonicLanguage.Ambient => minor ? AmbientMinor : AmbientMajor,
                HarmonicLanguage.Dark when minor => DarkTables,
                HarmonicLanguage.Epic when minor => EpicTables,
                _ => null,
            };
            if (tables == null) return Draw(random, mode, colour);

            Chord[] picked = random.Pick(tables);
            var chords = new Chord[picked.Length];
            for (int i = 0; i < picked.Length; i++)
            {
                Chord chord = picked[i];

                // A borrowed chord means nothing in a mode that already has it, and a mode that hasn't got the chord it's borrowing from loses nothing by keeping its own.
                if (chord.Borrow == mode) chord = new Chord(chord.Root, chord.Colour);

                // The tables are written in the plain major and minor. Where the piece's own mode makes a chord of them diminished, as the dorian does of the flat sixth and the
                // lydian of the fourth, it's taken from the plain mode instead, which is what any composer would do with it.
                Mode plain = minor ? Mode.Aeolian : Mode.Ionian;
                if (chord.Borrow == null && !PerfectFifth(mode, chord.Root) && PerfectFifth(plain, chord.Root)) chord = new Chord(chord.Root, chord.Colour, plain);
                if (chord.Colour == ChordColour.Triad && i > 0 && random.Chance(colour))
                {
                    Mode own = chord.Borrow ?? mode;
                    float roll = random.Float();
                    ChordColour kind = language == HarmonicLanguage.Ambient
                        ? (roll < 0.4f ? ChordColour.Added9 : roll < 0.75f ? ChordColour.Seventh : ChordColour.Ninth)
                        : (roll < 0.5f ? ChordColour.Added9 : roll < 0.75f ? ChordColour.Suspended2 : ChordColour.Seventh);
                    if (Sweet(own, chord.Root, kind)) chord = chord.With(kind);
                }
                chords[i] = chord;
            }
            return chords;
        }

        private static bool PerfectFifth(Mode mode, int root) => mode.Pitch(0, root + 4) - mode.Pitch(0, root) == 7;

        /// <summary>An eight bar progression, coloured a little so that not every chord is a plain triad.</summary>
        public static Chord[] Draw(SoundRandom random, Mode mode, float colour = 0.3f)
        {
            int[] roots = random.Pick(Progressions(mode));
            var chords = new Chord[roots.Length];
            for (int i = 0; i < roots.Length; i++)
            {
                ChordColour kind = ChordColour.Triad;
                bool home = i == roots.Length - 1 || i == 0;
                if (!home && random.Chance(colour))
                {
                    float roll = random.Float();
                    kind = roll < 0.35f ? ChordColour.Added9 : roll < 0.65f ? ChordColour.Seventh : roll < 0.85f ? ChordColour.Suspended2 : ChordColour.Suspended4;
                    if (!Sweet(mode, roots[i], kind)) kind = ChordColour.Triad;
                }
                chords[i] = new Chord(roots[i], kind);
            }
            return chords;
        }

        // What a note added to a chord does depends on the mode: the same colour is a soft ninth on one degree and a grinding minor ninth on the next.
        private static bool Sweet(Mode mode, int root, ChordColour colour)
        {
            int Above(int steps) => mode.Pitch(0, root + steps) - mode.Pitch(0, root);
            return colour switch
            {
                ChordColour.Seventh => Above(6) == 10,
                ChordColour.Added9 => Above(1) == 2 && Above(2) == 4,       // On a minor chord the ninth lands a semitone from the third.
                ChordColour.Ninth => Above(1) == 2 && Above(2) == 4 && Above(6) is 10 or 11,
                ChordColour.Suspended2 => Above(1) == 2,
                ChordColour.Suspended4 => Above(3) == 5,
                _ => true,
            };
        }

        /// <summary>
        /// Spaces a chord for several players: each voice moves as little as it can from where it was, as a good arranger writes, and the top keeps clear of the tune.
        /// </summary>
        public static int[] Voice(in Chord chord, Mode mode, int tonic, int[] previous, int lowest, int highest, int voices)
        {
            int[] degrees = chord.Degrees;
            var pitches = new List<int>();
            for (int octave = -3; octave <= 4; octave++)
            {
                foreach (int degree in degrees)
                {
                    int pitch = mode.Pitch(tonic, degree + octave * 7);
                    if (pitch >= lowest && pitch <= highest) pitches.Add(pitch);
                }
            }
            pitches.Sort();

            var result = new int[voices];
            if (pitches.Count == 0) return result;

            var used = new HashSet<int>();
            var chosen = new List<int>();
            for (int v = 0; v < voices; v++)
            {
                int from = previous.Length == voices && previous[v] != 0 ? previous[v] : lowest + (highest - lowest) * (v + 1) / (voices + 1);
                int best = pitches[0];
                int closest = int.MaxValue;
                foreach (int pitch in pitches)
                {
                    // Two voices a semitone apart are a cluster, not a chord: a seventh chord is spaced so its seventh doesn't sit under its root.
                    int rub = 0;
                    foreach (int other in chosen)
                    {
                        if (Math.Abs(pitch - other) is 1 or 11 or 13) rub = 12;
                    }
                    int cost = Math.Abs(pitch - from) + (used.Contains(pitch % 12) ? 7 : 0) + rub;
                    if (cost >= closest) continue;
                    closest = cost;
                    best = pitch;
                }
                result[v] = best;
                used.Add(best % 12);
                chosen.Add(best);
            }
            Array.Sort(result);
            return result;
        }
    }
}
