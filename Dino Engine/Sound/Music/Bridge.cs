using Dino_Engine.Sound.Dsp;

namespace Dino_Engine.Sound.Music
{
    /// <summary>
    /// What's played between one style and the next, so that a change is something the band does and not something that happens to it. A pit orchestra going from a dance to a fight
    /// doesn't stop and start: it climbs into the fight on a drum roll, or slows onto a held chord and lets the next thing grow out of it. Everything here leans on the next style, in
    /// its key and at its speed, so the ear is there before the new music is.
    /// </summary>
    internal static class Bridge
    {
        /// <summary>Seconds what was playing takes to die away under a run-up.</summary>
        public const float LetGo = 1.5f;

        /// <summary>A bar of fewer beats than this is carried through a bridge twice, or it's over before anyone hears it.</summary>
        public const int ShortestCarry = 3;

        private const int RunUpBeats = 4;
        private const int RitardBeats = 2;          // What a fight adds, on the chord alone, after what was keeping time has stopped.
        private const float CarryFall = 8f;         // Decibels what's carried loses on its way through.
        private const float CymbalPeak = 1.6f;      // Seconds into its take that a cymbal roll is at its height.

        // A roll closes up as it rises: strokes in each beat of the run-up.
        private static readonly int[] RollStrokes = { 2, 3, 4, 6 };

        /// <summary>Writes the way from one style to another. <paramref name="from"/> is null if nothing was playing, and <paramref name="beat"/> is the length of the last beat heard.</summary>
        public static Bar Write(Score.Setting? from, Score.Setting to, float beat, float intensity, List<Note> carried, int carriedBeats, SoundRandom random)
        {
            return to.Style.Urgent ? RunUp(to, beat, intensity) : Ease(from, to, beat, intensity, carried, carriedBeats, random);
        }

        /// <summary>The chord a style is reached by. The dominant wants to go home, and where the mode's flat second would sour its suspension, bare fifths on the tonic say less and do no harm.</summary>
        public static Chord Pivot(Score.Setting to) => to.Mode.Steps[1] == 1 ? new Chord(0, ChordColour.Open) : new Chord(4, ChordColour.Suspended4);

        // When each beat falls, with the tempo gliding from one speed to the other, and where the last one ends.
        private static float[] Beats(float from, float to, int count)
        {
            var times = new float[count + 1];
            for (int i = 0; i < count; i++) times[i + 1] = times[i] + from * MathF.Pow(to / from, (i + 0.5f) / count);
            return times;
        }

        // Two speeds are rarely as far apart as they look: a walk at 72 and a fight at 138 are nearly one and two. Taking the old beat's half or third as the pulse carries the time
        // straight over, which is how a composer changes tempo without the floor giving way.
        private static float Pulse(float heard, float wanted)
        {
            float best = heard;
            for (int share = 2; share <= 3; share++)
            {
                if (MathF.Abs(MathF.Log(heard / share / wanted)) < MathF.Abs(MathF.Log(best / wanted))) best = heard / share;
            }
            return best;
        }

        private static void Put(Bar bar, Section section, int pitch, float at, float length, float loudness, Articulation articulation = Articulation.Normal, float tail = 0f, float fader = 0f)
        {
            bar.Notes.Add(new Note
            {
                Section = section,
                Pitch = Textures.Fit(section, pitch),
                Start = at,
                Length = length,
                Loudness = Math.Clamp(loudness, 0.02f, 1f),
                Fader = fader,
                Articulation = articulation,
                Tail = tail,
            });
        }

        // One bar counted in at the fight's own speed: a roll closing up on the fifth, low strings and horns swelling on it with nothing to say which way it'll fall, a cymbal timed to
        // peak on the downbeat, and the violins running up to the note the tune starts on.
        private static Bar RunUp(Score.Setting to, float heard, float intensity)
        {
            float beat = 60f / to.Tempo;
            float[] at = Beats(Pulse(heard, beat), beat, RunUpBeats);
            float seconds = at[RunUpBeats];
            var bar = new Bar { Seconds = seconds, Beats = RunUpBeats, Label = $"Into {to.Style.Mood}" };

            float level = 0.45f + 0.45f * intensity;
            int fifth = to.Mode.Pitch(to.Tonic, 4);

            int strokes = 0;
            foreach (int count in RollStrokes) strokes += count;
            int stroke = 0;
            for (int b = 0; b < RunUpBeats; b++)
            {
                for (int s = 0; s < RollStrokes[b]; s++)
                {
                    float rise = stroke++ / (float)(strokes - 1);
                    float time = at[b] + (at[b + 1] - at[b]) * s / RollStrokes[b];
                    Put(bar, Section.Timpani, fifth - 24, time, 0.2f, level * (0.3f + 0.6f * rise * rise));
                }
            }

            Put(bar, Section.Cymbal, 60, MathF.Max(seconds - CymbalPeak, 0f), 2f, level * 0.8f, fader: -1f);
            Put(bar, Section.Cellos, fifth - 24, 0f, seconds * 1.02f, level * 0.85f, Articulation.Swell);
            Put(bar, Section.Basses, fifth - 36, 0f, seconds * 1.02f, level * 0.8f, Articulation.Swell);
            Put(bar, Section.Horns, fifth - 12, at[1], seconds - at[1], level * 0.8f, Articulation.Swell);
            Put(bar, Section.Horns, fifth - 5, at[1], seconds - at[1], level * 0.8f, Articulation.Swell);

            // Eight notes of the new mode over the last two beats, up to the step below where the tune begins.
            int landing = to.First.Tones[0].Degree;
            while (to.Mode.Pitch(to.Tonic, landing) < 74) landing += 7;
            while (to.Mode.Pitch(to.Tonic, landing) > 86) landing -= 7;
            const int Run = 8;
            float from = at[RunUpBeats - 2];
            float step = (seconds - from) / Run;
            for (int n = 0; n < Run; n++)
            {
                int pitch = to.Mode.Pitch(to.Tonic, landing - Run + n);
                Put(bar, Section.Violins, pitch, from + n * step, step * 1.15f, level * (0.55f + 0.4f * n / Run), n == 0 ? Articulation.Normal : Articulation.Legato);
            }
            return bar;
        }

        // What's between two beats, in seconds, on a tempo that's changing.
        private static float Warp(float[] at, float beat)
        {
            int whole = Math.Clamp((int)beat, 0, at.Length - 2);
            return at[whole] + (at[whole + 1] - at[whole]) * (beat - whole);
        }

        // Slowing or quickening towards the next style, with the band that was playing going on and thinning out as a chord that leans on what's next rises under it. The chord is held
        // long enough to go on sounding under the new music's first bars, so that nobody hears where one ended.
        private static Bar Ease(Score.Setting? from, Score.Setting to, float heard, float intensity, List<Note> carried, int carriedBeats, SoundRandom random)
        {
            bool fight = from != null && from.Style.Urgent;
            int count = carriedBeats + (fight ? RitardBeats : 0);
            float[] at = Beats(heard, 60f / to.Tempo, count);
            float seconds = at[count];
            var bar = new Bar { Seconds = seconds, Beats = count, Label = $"Into {to.Style.Mood}" };

            Chord chord = Pivot(to);
            int[] voicing = Harmony.Voice(chord, to.Mode, to.Tonic, Array.Empty<int>(), 50, 72, 3);
            int root = to.Mode.Pitch(to.Tonic, chord.Root);

            foreach (Note written in carried)
            {
                Note note = written;
                float end = Warp(at, written.Start + written.Length);
                note.Start = Warp(at, written.Start);
                note.Length = MathF.Max(end - note.Start, 0.02f);
                note.Fader -= CarryFall * written.Start / carriedBeats;
                bar.Notes.Add(note);
            }

            float level = fight ? 0.4f + 0.3f * intensity : 0.26f + 0.3f * intensity;
            float tail = seconds * 0.4f + 2f;

            foreach (int pitch in voicing) Put(bar, Section.Violas, pitch, 0f, seconds, level, Articulation.Swell, tail, -2f);
            Put(bar, Section.Cellos, root - 24, 0f, seconds, level, Articulation.Swell, tail, -2f);

            if (fight)
            {
                Put(bar, Section.Basses, root - 36, 0f, seconds, level * 0.9f, Articulation.Swell, tail, -2f);
                Put(bar, Section.Horns, voicing[0], 0f, seconds, level, Articulation.Swell, tail, -2f);
                Put(bar, Section.Horns, voicing[^1] - 12, 0f, seconds, level, Articulation.Swell, tail, -2f);

                // The drums stop on a last soft stroke, a heartbeat slowing down.
                Put(bar, Section.Timpani, to.Tonic - 24, at[carriedBeats], 0.3f, level * 0.6f);
            }

            Pluck(bar, to, voicing, at, count, level, random);
            Pickup(bar, to, chord, at, count, intensity);
            return bar;
        }

        // The new style's own plucked or struck players, turning the page: its chord broken upwards over the second half.
        private static void Pluck(Bar bar, Score.Setting to, int[] voicing, float[] at, int count, float level, SoundRandom random)
        {
            foreach (Part part in to.Style.Parts)
            {
                if (part.Section is not (Section.Harp or Section.Lute or Section.Celesta)) continue;

                float start = at[count / 2];
                float step = (at[count] - start) / (voicing.Length + 1);
                for (int n = 0; n <= voicing.Length; n++)
                {
                    int pitch = n < voicing.Length ? voicing[n] : voicing[0] + 12;
                    Put(bar, part.Section, pitch + (part.Section == Section.Celesta ? 12 : 0), start + n * step + random.Gaussian() * 0.004f + 0.01f, step * 3f, level + 0.08f * n);
                }
                return;
            }
        }

        // Whoever's about to take the tune leads into it, as a singer breathes in, up through the notes of the chord that's holding, as in slow music anything else is heard grinding on it.
        private static void Pickup(Bar bar, Score.Setting to, Chord chord, float[] at, int count, float intensity)
        {
            foreach (Part part in to.Style.Parts)
            {
                if (part.Writer != Textures.Melody || part.Use is not (MelodyUse.Always or MelodyUse.First) || intensity < part.From) continue;

                Tone[] tones = to.First.Tones;
                int lowest = int.MaxValue, highest = int.MinValue;
                foreach (Tone tone in tones)
                {
                    lowest = Math.Min(lowest, tone.Degree);
                    highest = Math.Max(highest, tone.Degree);
                }
                int shift = Textures.Shift(part, to.Mode, to.Tonic, lowest, highest);

                float loudness = (part.Level + part.Rise * intensity) * 0.85f;
                float start = at[count - 1];
                float step = (at[count] - start) / 2f;
                int degree = tones[0].Degree + shift;
                for (int n = 1; n >= 0; n--)
                {
                    do degree--;
                    while (!chord.Contains(degree));
                    Put(bar, part.Section, to.Mode.Pitch(to.Tonic, degree), start + n * step, step * 1.05f, loudness + n * 0.05f, Articulation.Normal, 0f, part.Fader);
                }
                return;
            }
        }
    }

    /// <summary>
    /// Stops the music following every flicker. Walking along the edge of a village, or past something just out of reach, would otherwise send it back and forth for as long as it lasted,
    /// and no transition is smooth if there are six a minute. A fight goes straight through, as that's what the player has to be told at once, while anything calmer has to be wanted for
    /// a moment first, and what has only just arrived is left to say something.
    /// </summary>
    public sealed class MoodHold
    {
        private const float CalmWaits = 3f;         // Seconds a place has to go on being wanted.
        private const float UneaseWaits = 1.5f;
        private const float Stays = 12f;            // Seconds before a calm style makes way for another.

        private MusicMood _mood = MusicMood.Silence;
        private MusicMood _wanted = MusicMood.Silence;
        private float _wantedFor;
        private float _playedFor;

        public MusicMood Update(MusicMood wanted, float seconds)
        {
            _playedFor += seconds;
            _wantedFor = wanted == _wanted ? _wantedFor + seconds : 0f;
            _wanted = wanted;
            if (wanted == _mood) return _mood;

            bool urgent = wanted == MusicMood.Silence || Styles.For(wanted).Urgent;
            bool over = _mood == MusicMood.Silence || Styles.For(_mood).Urgent;
            float wait = Styles.For(wanted).Prompt ? UneaseWaits : CalmWaits;
            if (urgent || over || _wantedFor >= wait && _playedFor >= Stays)
            {
                _mood = wanted;
                _playedFor = 0f;
            }
            return _mood;
        }
    }
}
