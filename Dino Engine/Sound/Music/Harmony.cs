using Dino_Engine.Sound.Dsp;

namespace Dino_Engine.Sound.Music
{
    /// <summary>
    /// Chord progressions. Harmony is the slowest thing a listener follows, and much of a piece's character is here: a fantasy score leans on modes and on
    /// chords a step or a third apart, not the dominant-to-tonic of a hymn. Each mode has progressions that show it off, in eight bar periods with a pause
    /// half way, as measured tunes change chord about once a bar.
    /// </summary>
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

        public static int[][] Progressions(Mode mode)
        {
            if (mode == Mode.HarmonicMinor) return HarmonicMinor;
            if (mode == Mode.Ionian) return Ionian;
            if (mode == Mode.Mixolydian) return Mixolydian;
            if (mode == Mode.Dorian) return Dorian;
            if (mode == Mode.Lydian) return Lydian;
            if (mode == Mode.Phrygian) return Phrygian;
            return Aeolian;
        }

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
            for (int v = 0; v < voices; v++)
            {
                int from = previous.Length == voices && previous[v] != 0 ? previous[v] : lowest + (highest - lowest) * (v + 1) / (voices + 1);
                int best = pitches[0];
                int closest = int.MaxValue;
                foreach (int pitch in pitches)
                {
                    int cost = Math.Abs(pitch - from) + (used.Contains(pitch % 12) ? 7 : 0);
                    if (cost >= closest) continue;
                    closest = cost;
                    best = pitch;
                }
                result[v] = best;
                used.Add(best % 12);
            }
            Array.Sort(result);
            return result;
        }
    }
}
