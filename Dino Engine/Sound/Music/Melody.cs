using Dino_Engine.Sound.Dsp;

namespace Dino_Engine.Sound.Music
{
    /// <summary>One note of a tune. Its pitch is a degree of the mode, not a pitch, so the tune can be set in any mode or key.</summary>
    public readonly struct Tone
    {
        public readonly float Beat;         // Where it starts, from the start of the tune.
        public readonly float Length;       // In beats.
        public readonly int Degree;

        public Tone(float beat, float length, int degree)
        {
            Beat = beat;
            Length = length;
            Degree = degree;
        }
    }

    /// <summary>A rhythm for one bar: the note lengths in beats. Bars are the unit a tune repeats by, far more than notes are.</summary>
    public sealed class Cell
    {
        public readonly float[] Lengths;
        public readonly float Weight;

        public Cell(float weight, params float[] lengths)
        {
            Weight = weight;
            Lengths = lengths;
        }

        public int Notes => Lengths.Length;
    }

    /// <summary>
    /// The rhythms a tune is built from, by metre. A tune doesn't use many: in a thousand folk tunes, two bars in three repeat the rhythm of an
    /// earlier bar, and only one in three repeat its notes. Rhythm is what holds a tune together while its pitches wander.
    /// </summary>
    public static class Rhythms
    {
        // Simple time: quarter = 1.
        private static readonly Cell[] FourFlowing =
        {
            new(3f, 1f, 1f, 1f, 1f), new(4f, 1f, 0.5f, 0.5f, 1f, 1f), new(3f, 1.5f, 0.5f, 1f, 1f), new(3f, 2f, 1f, 1f),
            new(3f, 1f, 1f, 2f), new(2f, 0.5f, 0.5f, 1f, 2f), new(2f, 1.5f, 0.5f, 2f), new(2f, 1f, 0.5f, 0.5f, 2f),
            new(1f, 0.5f, 0.5f, 0.5f, 0.5f, 1f, 1f), new(1f, 3f, 1f),
        };

        private static readonly Cell[] FourLyrical =
        {
            new(4f, 2f, 1f, 1f), new(3f, 3f, 1f), new(3f, 1f, 1f, 2f), new(3f, 1.5f, 0.5f, 2f), new(2f, 2f, 2f), new(2f, 1f, 3f), new(1f, 1.5f, 0.5f, 1f, 1f),
        };

        private static readonly Cell[] FourHeroic =
        {
            new(4f, 1.5f, 0.5f, 2f), new(3f, 1f, 0.5f, 0.5f, 2f), new(3f, 2f, 1f, 1f), new(3f, 1.5f, 0.5f, 1f, 1f), new(2f, 3f, 0.5f, 0.5f), new(2f, 1f, 1f, 2f), new(1f, 0.75f, 0.25f, 1f, 2f),
        };

        private static readonly Cell[] ThreeFlowing =
        {
            new(4f, 1f, 1f, 1f), new(4f, 2f, 1f), new(3f, 1.5f, 0.5f, 1f), new(2f, 1f, 0.5f, 0.5f, 1f), new(2f, 1f, 2f), new(1f, 0.5f, 0.5f, 1f, 1f),
        };

        // Compound time: the beat is 1 and splits in three, so an eighth is a third of a beat.
        private const float E = 1f / 3f;

        private static readonly Cell[] Jig =
        {
            new(5f, E, E, E, E, E, E), new(5f, 2f * E, E, 2f * E, E), new(3f, 1f, E, E, E), new(3f, E, E, E, 1f), new(2f, 2f * E, E, E, E, E), new(2f, 1f, 2f * E, E), new(2f, 1f, 1f),
        };

        private static readonly Cell[] RollingFour =
        {
            new(4f, 1f, 2f * E, E, 1f, 1f), new(4f, 2f * E, E, 1f, 2f), new(3f, 1f, 1f, 2f), new(3f, 2f, 2f * E, E, 1f), new(2f, 2f * E, E, 2f * E, E, 2f), new(2f, 3f, 1f),
        };

        public static Cell[] For(Meter meter, MelodicStyle style)
        {
            if (meter.Divisions == 3) return meter.Beats == 2 ? Jig : RollingFour;
            if (meter.Beats == 3) return ThreeFlowing;
            return style switch
            {
                MelodicStyle.Lyrical => FourLyrical,
                MelodicStyle.Heroic => FourHeroic,
                _ => FourFlowing,
            };
        }

        /// <summary>What a tune finishes on: a long note, so that it's plain it has finished.</summary>
        public static Cell Closing(Meter meter) => meter.Divisions == 3
            ? new Cell(1f, meter.Beats == 2 ? new[] { 1f, 1f } : new[] { 1f, 3f })
            : meter.Beats == 3 ? new Cell(1f, 1f, 2f) : new Cell(1f, 1f, 3f);
    }

    public enum MelodicStyle
    {
        Flowing,    // Moves along.
        Lyrical,    // Long notes that sing.
        Heroic,     // Dotted and declamatory, with leaps.
    }

    /// <summary>What a tune is being written to fit.</summary>
    public sealed class TuneBrief
    {
        public Meter Meter = Meter.Four;
        public Mode Mode = Mode.Aeolian;
        public MelodicStyle Style = MelodicStyle.Flowing;
        public Chord[] Harmony = Array.Empty<Chord>();  // One chord a bar.
        public int[]? Shape;            // A skeleton to follow, from another tune: this is how a theme stays itself in another style.
        public int Candidates = 300;
        public int LowestDegree = -3;
        public int HighestDegree = 10;
    }

    /// <summary>A tune: an eight bar period of a question and an answer, and the harmony it was written over.</summary>
    public sealed class Tune
    {
        public readonly Tone[] Tones;
        public readonly Chord[] Harmony;
        public readonly Meter Meter;
        public readonly float Score;

        public Tune(Tone[] tones, Chord[] harmony, Meter meter, float score)
        {
            Tones = tones;
            Harmony = harmony;
            Meter = meter;
            Score = score;
        }

        public int Bars => Harmony.Length;
        public int Beats => Bars * Meter.Beats;
    }

    /// <summary>
    /// Writes melodies. The hard part of generating a tune isn't making notes, it's making notes that have a reason. Notes drawn one at a time, however true their
    /// statistics, wander. So a tune is written from the top down, as music is built: a slow skeleton of chord tones moving smoothly, one to a bar, rising to one
    /// high point and coming home; then a small figure for each rhythm, laid on the skeleton wherever that rhythm comes back. The figure is what the ear
    /// recognises, and the skeleton carries it somewhere, which is all a motif and a sequence are. And as nothing guarantees that any one attempt is
    /// good, it writes hundreds and keeps the one most like real music, by what a thousand real tunes were measured to do.
    /// </summary>
    public static class Songwriter
    {
        // Measured from the 1034 tunes of the Nottingham Music Database. Only these numbers came from there: never a note.
        public const float RepeatShare = 0.108f;
        public const float StepShare = 0.525f;
        public const float ThirdShare = 0.229f;
        public const float FourthFifthShare = 0.097f;
        public const float WideShare = 0.041f;
        public const float StrongChordTones = 0.87f;
        public const float WeakChordTones = 0.665f;
        public const float ReversalAfterLeap = 0.63f;
        public const float ReusedBars = 0.33f;

        /// <summary>The slow line of a tune: its first note of each bar. It's what's left when everything that changes between one setting and another is taken away.</summary>
        public static int[] ShapeOf(Tune tune)
        {
            var shape = new int[tune.Bars];
            float barBeats = tune.Meter.Beats;
            foreach (Tone tone in tune.Tones)
            {
                int bar = (int)(tone.Beat / barBeats);
                if (bar < shape.Length && tone.Beat == bar * barBeats) shape[bar] = tone.Degree;
            }
            return shape;
        }

        public static Tune Write(SoundRandom random, TuneBrief brief)
        {
            Tune? best = null;
            for (int i = 0; i < brief.Candidates; i++)
            {
                Tone[] tones = Draft(random, brief);
                float score = Judge(tones, brief);
                if (best == null || score > best.Score) best = new Tune(tones, brief.Harmony, brief.Meter, score);
            }
            return best!;
        }

        // Which bars share a rhythm, as letters. Real tunes reuse a rhythm in two bars of three, so a period has few, and they come back in a pattern.
        private static readonly string[] Forms =
        {
            "ABABABAC", "ABACABAD", "AABAAABC", "ABABCBAD", "AABBAABC", "ABCBABCD",
        };

        private static Cell[] Plan(SoundRandom random, TuneBrief brief)
        {
            Cell[] cells = Rhythms.For(brief.Meter, brief.Style);
            string form = random.Pick(Forms);
            var chosen = new Dictionary<char, Cell>();
            int bars = brief.Harmony.Length;
            var plan = new Cell[bars];
            for (int bar = 0; bar < bars; bar++)
            {
                char letter = form[bar % form.Length];
                if (!chosen.TryGetValue(letter, out Cell? cell))
                {
                    cell = Pick(random, cells);
                    chosen[letter] = cell;
                }
                plan[bar] = cell;
            }
            plan[bars - 1] = Rhythms.Closing(brief.Meter);
            return plan;
        }

        private static Cell Pick(SoundRandom random, Cell[] cells)
        {
            float total = 0f;
            foreach (Cell cell in cells) total += cell.Weight;
            float choice = random.Float() * total;
            foreach (Cell cell in cells)
            {
                choice -= cell.Weight;
                if (choice <= 0f) return cell;
            }
            return cells[^1];
        }

        private const float ParallelPeriods = 0.8f;
        private const float WeakBends = 0.4f;       // Share of notes off the chord that are moved onto it on weak beats.

        // The slow line under the tune: one chord tone a bar, each close to the last, up to a peak about two thirds through and home to the tonic.
        private static int[] Skeleton(SoundRandom random, TuneBrief brief)
        {
            int bars = brief.Harmony.Length;
            var line = new int[bars];
            float peakAt = random.Range(0.5f, 0.72f);

            // Real tunes mostly begin on the fifth, then the tonic, then the third.
            float begin = random.Float();
            int start = begin < 0.5f ? 4 : begin < 0.72f ? 0 : begin < 0.91f ? 2 : 7;
            int peak = Math.Min(start + 3 + random.Int(4), brief.HighestDegree);
            peak = Math.Max(peak, 6);

            int[]? shape = brief.Shape;
            int current = NearestOf(shape != null ? shape[0] : start, brief.Harmony[0]);
            line[0] = current;
            for (int bar = 1; bar < bars; bar++)
            {
                float along = bar / (float)(bars - 1);
                float target = along < peakAt
                    ? start + (peak - start) * Curve01(along / peakAt)
                    : peak * (1f - Curve01((along - peakAt) / (1f - peakAt)));
                if (shape != null) target = shape[bar % shape.Length];

                // Every chord tone nearby is a candidate; the line prefers what's close to it and close to where it's meant to be.
                Chord chord = brief.Harmony[bar];
                int best = current;
                float lowest = float.MaxValue;
                for (int degree = current - 5; degree <= current + 5; degree++)
                {
                    if (!chord.Contains(degree) || degree < brief.LowestDegree || degree > brief.HighestDegree) continue;
                    float cost = MathF.Abs(degree - target) * (shape != null ? 1.5f : 0.7f) + MathF.Abs(degree - current) * 0.6f + random.Float() * 1.3f;
                    if (cost >= lowest) continue;
                    lowest = cost;
                    best = degree;
                }
                line[bar] = best;
                current = best;
            }

            // The commonest plan there is, and much of what makes a tune stick: the second half opens as the first did, then goes somewhere else. A tune on a world's theme has this
            // from the theme, and its shape isn't this one's to change.
            if (shape == null && random.Chance(ParallelPeriods))
            {
                int half = bars / 2;
                for (int bar = 0; bar < 2 && bar + half < bars - 1; bar++)
                {
                    if (brief.Harmony[bar + half].Contains(line[bar])) line[bar + half] = line[bar];
                }
            }

            line[bars - 1] = NearestTonic(line[bars - 2]);
            return line;
        }

        // A figure: where each note of a rhythm sits relative to the first. Small, as that's what makes it singable, and the same each time it comes back.
        private static int[] Figure(SoundRandom random, Cell cell)
        {
            var figure = new int[cell.Notes];
            int position = 0;
            int direction = random.Chance(0.5f) ? 1 : -1;
            for (int n = 1; n < cell.Notes; n++)
            {
                float roll = random.Float();
                int move = roll < 0.05f ? 0 : roll < 0.62f ? 1 : roll < 0.93f ? 2 : 3;
                if (random.Chance(0.3f)) direction = -direction;

                // Figures that have gone a way out come back, and leaps turn round.
                if (Math.Abs(position) >= 3) direction = position > 0 ? -1 : 1;
                position += direction * move;
                figure[n] = position;
                if (move >= 2) direction = -direction;
            }
            return figure;
        }

        private static Tone[] Draft(SoundRandom random, TuneBrief brief)
        {
            Cell[] plan = Plan(random, brief);
            int[] line = Skeleton(random, brief);
            int bars = plan.Length;
            float barBeats = brief.Meter.Beats;
            var tones = new List<Tone>();
            var figures = new Dictionary<Cell, int[]>();
            var bends = new Dictionary<Cell, float[]>();

            for (int bar = 0; bar < bars; bar++)
            {
                Cell cell = plan[bar];
                Chord chord = brief.Harmony[bar];
                int from = line[bar];
                int to = bar + 1 < bars ? line[bar + 1] : from;
                bool lastBar = bar == bars - 1;

                if (!figures.TryGetValue(cell, out int[]? figure))
                {
                    figure = Figure(random, cell);
                    figures[cell] = figure;

                    // What it does about notes off the chord is settled with the figure, so that a bar coming back on the same chord comes back the same.
                    var bend = new float[cell.Notes];
                    for (int n = 0; n < bend.Length; n++) bend[n] = random.Float();
                    bends[cell] = bend;
                }

                float beat = 0f;
                for (int n = 0; n < cell.Notes; n++)
                {
                    int degree = from + figure[n];

                    // A note on a strong beat usually belongs to the chord, and one on a weak beat often, so the figure bends a little to fit where it's landed.
                    bool strong = brief.Meter.Weight(beat) >= 0.6f;
                    if (!chord.Contains(degree) && bends[cell][n] < (strong ? StrongChordTones : WeakBends)) degree = NearestOf(degree, chord);

                    // The last note leads on to the next bar, so the figure and the skeleton join up.
                    if (n == cell.Notes - 1 && n > 0 && !lastBar && Math.Abs(to - degree) > 2)
                    {
                        degree = to > degree ? to - 1 - random.Int(2) : to + 1 + random.Int(2);
                    }

                    // The last bar comes home from a note of its chord, down to the tonic itself: a downbeat here is heard against everything at once. On any chord but the tonic
                    // it's a half close instead, stopping on a note of that chord for the next phrase to answer, which is what drives a fight round its loop.
                    if (lastBar)
                    {
                        int end = NearestOf(line[bar], chord);
                        if (n == cell.Notes - 1) degree = end;
                        else if (n == 0) degree = NearestOf(end + (random.Chance(0.7f) ? 2 : 4), chord);
                        else degree = end + 1;
                    }

                    degree = Math.Clamp(degree, brief.LowestDegree, brief.HighestDegree);
                    tones.Add(new Tone(bar * barBeats + beat, cell.Lengths[n], degree));
                    beat += cell.Lengths[n];
                }
            }
            return tones.ToArray();
        }

        private static float Curve01(float x)
        {
            x = Math.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        private static int NearestTonic(int degree)
        {
            int below = degree - Chord.Wrap(degree);
            return degree - below <= 3 ? below : below + 7;
        }

        private static int NearestOf(int degree, in Chord chord)
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

        /// <summary>
        /// How much a draft sounds like a real tune, the higher the better. Each term is what measured tunes do, or a fault a listener would hear: a
        /// tune too like the average of all tunes would be as dull as one unlike any, so the measures are met loosely and the faults punished hard.
        /// </summary>
        public static float Judge(Tone[] tones, TuneBrief brief)
        {
            int count = tones.Length;
            if (count < 6) return float.NegativeInfinity;

            int repeats = 0, steps = 0, thirds = 0, wide = 0, leaps = 0, reversals = 0, straight = 0, run = 0, longestRun = 0;
            int same = 0, longestSame = 0, awkward = 0;
            int highest = int.MinValue, lowest = int.MaxValue, highestCount = 0, peakIndex = 0;
            float strongHits = 0f, strongTotal = 0f, weakHits = 0f, weakTotal = 0f;
            float barBeats = brief.Meter.Beats;

            for (int i = 0; i < count; i++)
            {
                int degree = tones[i].Degree;
                if (degree > highest)
                {
                    highest = degree;
                    highestCount = 1;
                    peakIndex = i;
                }
                else if (degree == highest) highestCount++;
                lowest = Math.Min(lowest, degree);

                int bar = Math.Min((int)(tones[i].Beat / barBeats), brief.Harmony.Length - 1);
                float position = tones[i].Beat - bar * barBeats;
                bool member = brief.Harmony[bar].Contains(degree);
                if (brief.Meter.Weight(position) >= 0.6f)
                {
                    strongTotal += 1f;
                    if (member) strongHits += 1f;
                }
                else
                {
                    weakTotal += 1f;
                    if (member) weakHits += 1f;
                }

                if (i == 0) continue;
                int step = degree - tones[i - 1].Degree;
                int size = Math.Abs(step);

                // Some leaps are simply hard to sing, and stand out as mistakes. A real tune all but never has one.
                int semitones = Math.Abs(brief.Mode.Pitch(0, degree) - brief.Mode.Pitch(0, tones[i - 1].Degree));
                if (semitones is 6 or 10 or 11 || semitones > 12) awkward++;

                if (size == 0)
                {
                    repeats++;
                    same++;
                    longestSame = Math.Max(longestSame, same);
                }
                else same = 0;

                if (size == 1) steps++;
                else if (size == 2) thirds++;
                else if (size >= 5) wide++;

                if (i >= 2)
                {
                    int before = tones[i - 1].Degree - tones[i - 2].Degree;
                    if (Math.Abs(before) >= 3)
                    {
                        leaps++;
                        if (before * step < 0) reversals++;
                        if (before * step > 0 && size >= 2) straight++;
                    }
                    run = before * step > 0 ? run + 1 : 0;
                    longestRun = Math.Max(longestRun, run);
                }
            }

            float intervals = count - 1f;
            float score = 0f;

            // Whole bars coming back is what lets a tune be remembered: real ones do it in a third of their bars.
            score -= 5f * Difference(ShareOfBarsReused(tones, barBeats), ReusedBars);

            score -= 6f * Difference(repeats / intervals, RepeatShare);
            score -= 8f * Difference(steps / intervals, StepShare);
            score -= 6f * Difference(thirds / intervals, ThirdShare);
            score -= 8f * Difference(wide / intervals, WideShare);
            if (strongTotal > 0f) score -= 8f * Difference(strongHits / strongTotal, StrongChordTones);
            if (weakTotal > 0f) score -= 3f * Difference(weakHits / weakTotal, WeakChordTones);
            if (leaps > 0) score -= 3f * Difference(reversals / (float)leaps, ReversalAfterLeap);

            // A tune has one high point, not a ceiling it keeps bumping against, and it comes after the middle.
            score -= (highestCount - 1) * 0.28f;
            float peakPlace = peakIndex / (float)count;
            score -= 4f * Difference(peakPlace, 0.65f);

            // A span of ten to twelve steps of the mode is about what real tunes use. Too narrow is a chant; too wide isn't singable.
            int span = highest - lowest;
            if (span < 6) score -= (6 - span) * 0.5f;
            if (span > 11) score -= (span - 11) * 0.5f;

            // The faults: an unsingable leap, leaping twice the same way, sitting on one note, and running on in one direction for too long.
            score -= awkward * 1.6f;
            score -= straight * 0.35f;
            if (longestSame >= 3) score -= (longestSame - 2) * 0.7f;
            if (longestRun >= 5) score -= (longestRun - 4) * 0.4f;

            // It finishes at home, by step if it can.
            int last = tones[count - 1].Degree;
            if (Chord.Wrap(last) != 0) score -= 3f;
            if (Math.Abs(last - tones[count - 2].Degree) <= 2) score += 0.5f;
            return score;
        }

        private static float Difference(float value, float target) => MathF.Abs(value - target);

        private static float ShareOfBarsReused(Tone[] tones, float barBeats)
        {
            var seen = new HashSet<int>();
            int reused = 0, bars = 0, bar = 0, signature = 17;
            foreach (Tone tone in tones)
            {
                int place = (int)(tone.Beat / barBeats);
                if (place != bar)
                {
                    bars++;
                    if (!seen.Add(signature)) reused++;
                    signature = 17;
                    bar = place;
                }
                signature = signature * 31 + tone.Degree * 64 + (int)((tone.Beat - place * barBeats) * 8f);
            }
            return reused / (float)Math.Max(bars, 1);
        }
    }
}
