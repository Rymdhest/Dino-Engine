namespace Dino_Engine.Sound.Music
{
    /// <summary>
    /// A mode: which seven of the twelve notes a piece lives in, and so what colour it has. Scores are written in steps of the mode and
    /// not in pitches, so one tune can be heard in any of them: that's how a theme stays itself and still turns dark in a graveyard.
    /// </summary>
    public sealed class Mode
    {
        public readonly string Name;
        public readonly int[] Steps;    // Semitones above the tonic of each of the seven degrees.

        private Mode(string name, params int[] steps)
        {
            Name = name;
            Steps = steps;
        }

        public static readonly Mode Ionian = new("Ionian", 0, 2, 4, 5, 7, 9, 11);       // Plain and glad.
        public static readonly Mode Dorian = new("Dorian", 0, 2, 3, 5, 7, 9, 10);       // Old, and unsure whether to be sad.
        public static readonly Mode Phrygian = new("Phrygian", 0, 1, 3, 5, 7, 8, 10);   // Dark and foreign: the flat second.
        public static readonly Mode Lydian = new("Lydian", 0, 2, 4, 6, 7, 9, 11);       // Wonder: the raised fourth.
        public static readonly Mode Mixolydian = new("Mixolydian", 0, 2, 4, 5, 7, 9, 10);   // Folk tunes, and open air.
        public static readonly Mode Aeolian = new("Aeolian", 0, 2, 3, 5, 7, 8, 10);     // The natural minor.
        public static readonly Mode HarmonicMinor = new("Harmonic minor", 0, 2, 3, 5, 7, 8, 11);    // The minor with a leading note, for tension.
        public static readonly Mode PhrygianDominant = new("Phrygian dominant", 0, 1, 4, 5, 7, 8, 10);  // The flat second over a major third: desert stone and old temples.

        public bool IsMinor => Steps[2] == 3;

        /// <summary>The pitch of a degree of the mode, as a MIDI note. Degrees go on past seven into the next octave, and below zero into the last.</summary>
        public int Pitch(int tonic, int degree)
        {
            int octave = degree >= 0 ? degree / 7 : (degree - 6) / 7;
            int index = degree - octave * 7;
            return tonic + octave * 12 + Steps[index];
        }

        /// <summary>The degree nearest to a pitch, for fitting something into the mode.</summary>
        public int Nearest(int tonic, int pitch)
        {
            int degree = (pitch - tonic) * 7 / 12;
            int best = degree;
            int closest = int.MaxValue;
            for (int d = degree - 2; d <= degree + 2; d++)
            {
                int distance = Math.Abs(Pitch(tonic, d) - pitch);
                if (distance >= closest) continue;
                closest = distance;
                best = d;
            }
            return best;
        }
    }

    /// <summary>
    /// A chord, as a degree of the mode with notes stacked in thirds on it. It's described by the mode's own steps, so the same progression
    /// sounds different in each mode, which is the point of them. <see cref="Colour"/> adds what makes one chord feel unlike another of the same name.
    /// </summary>
    public readonly struct Chord
    {
        public readonly int Root;              // A degree, from 0 for the tonic.
        public readonly ChordColour Colour;
        public readonly Mode? Borrow;          // A chord taken from the mode of the same tonic, as film music takes its flat seventh in a major key: for its bar, everything is in that mode.

        public Chord(int root, ChordColour colour = ChordColour.Triad, Mode? borrow = null)
        {
            Root = root;
            Colour = colour;
            Borrow = borrow;
        }

        public Chord With(ChordColour colour) => new(Root, colour, Borrow);

        /// <summary>Degrees above the tonic that make this chord, the root first.</summary>
        public int[] Degrees => Colour switch
        {
            ChordColour.Suspended2 => new[] { Root, Root + 1, Root + 4 },
            ChordColour.Suspended4 => new[] { Root, Root + 3, Root + 4 },
            ChordColour.Seventh => new[] { Root, Root + 2, Root + 4, Root + 6 },
            ChordColour.Added9 => new[] { Root, Root + 2, Root + 4, Root + 8 },
            ChordColour.Ninth => new[] { Root, Root + 2, Root + 4, Root + 6, Root + 8 },
            ChordColour.Open => new[] { Root, Root + 4 },
            _ => new[] { Root, Root + 2, Root + 4 },
        };

        /// <summary>Whether a degree, in any octave, belongs to the chord.</summary>
        public bool Contains(int degree)
        {
            foreach (int member in Degrees)
            {
                if (Wrap(member) == Wrap(degree)) return true;
            }
            return false;
        }

        public static int Wrap(int degree) => ((degree % 7) + 7) % 7;
    }

    public enum ChordColour
    {
        Triad,
        Seventh,
        Suspended2,
        Suspended4,
        Added9,
        Open,       // Root and fifth alone: bare, ancient, and neither major nor minor.
        Ninth,      // The seventh and the ninth both, the soft haze of slow piano music.
    }

    /// <summary>What a phrase's last bar does: how much it says "that's all".</summary>
    public enum Cadence
    {
        None,       // Runs on into what follows.
        Half,       // A question: stops somewhere that wants to go on.
        Full,       // An answer: home.
    }

    /// <summary>Time in a piece. Everything is in beats and bars until the very last moment, as that's what music is counted in.</summary>
    public readonly struct Meter
    {
        public readonly int Beats;              // Beats in a bar.
        public readonly int Divisions;          // Each beat divides into this many: two for simple time, three for a jig's lilt.

        public Meter(int beats, int divisions)
        {
            Beats = beats;
            Divisions = divisions;
        }

        public static readonly Meter Four = new(4, 2);
        public static readonly Meter Three = new(3, 2);
        public static readonly Meter Six = new(2, 3);       // 6/8: two beats of three.
        public static readonly Meter Twelve = new(4, 3);    // 12/8: a slow four with a roll in it.

        /// <summary>How strong a position in the bar is, from 1 on the downbeat down towards 0: what a downbeat feels like is the whole of metre.</summary>
        public float Weight(float beat)
        {
            float fraction = beat - MathF.Floor(beat);
            if (beat == 0f) return 1f;
            if (fraction == 0f)
            {
                // The middle of a four is the second strongest place there is.
                if (Beats == 4 && beat == 2f) return 0.8f;
                return 0.6f;
            }
            return fraction * Divisions % 1f == 0f ? 0.35f : 0.2f;
        }
    }
}
