using Dino_Engine.Sound.Dsp;
using Dino_Engine.World;

namespace Dino_Engine.Sound.Music
{
    /// <summary>
    /// The kinds of piece a land can have. Each is what's common to a whole genre of music, never any one piece: how it's scored, what its harmony does, how its tunes move and how
    /// much room it leaves.
    /// </summary>
    public enum Idiom
    {
        Reverie,    // A piano alone with a soft synthesiser under it, slow, with ninths in its chords and silence between its phrases.
        Hearth,     // A fingerpicked twelve-string, a flute or an ocarina, hand drums, and an echo on it all, often in three.
        Vigil,      // Muted strings holding a drone, a cello or an English horn singing low over it, voices far off, a drum off the beat.
        Wonder,     // A hymn in the low strings, clarinets, bassoons and horns, moving to the fourth and borrowing a flat seventh, the voices joining late.
        Drift,      // Short phrases going round on loops of their own lengths, never meeting the same way twice.
    }

    /// <summary>
    /// Draws a land's music. Nothing here is chosen by hand for one land: a land's character makes some kinds of piece likelier than others, and its seed decides the rest, the kind,
    /// a second kind that lends it colour, the key and mode, the speed and the metre, who sings the tune, how the accompaniment goes, the rhythm of its drums, the room it's in and the
    /// echo on it. So every land has a piece of its own, and a dark land can still have a bright one.
    /// </summary>
    public static class Arranger
    {
        private static Part P(Section section, PartWriter writer, float from, float level, float rise, float fader = 0f, int octave = 0, string name = "", MelodyUse use = MelodyUse.Always)
        {
            return new Part { Section = section, Writer = writer, From = from, Level = level, Rise = rise, Fader = fader, Octave = octave, Name = name, Use = use };
        }

        private static T Pick<T>(SoundRandom random, params (T value, float weight)[] choices)
        {
            float total = 0f;
            foreach ((T _, float weight) in choices) total += MathF.Max(weight, 0f);
            float roll = random.Float() * total;
            foreach ((T value, float weight) in choices)
            {
                roll -= MathF.Max(weight, 0f);
                if (roll <= 0f) return value;
            }
            return choices[^1].value;
        }

        /// <summary>How likely each kind of piece is in a land like this one.</summary>
        public static (Idiom idiom, float weight)[] Likelihoods(MusicPlace place)
        {
            float bright = 1f - place.Dark;
            return new[]
            {
                (Idiom.Reverie, 0.25f + 0.7f * bright * (1f - place.Open * 0.3f) + 0.5f * place.Cold),
                (Idiom.Hearth, 0.3f + 0.6f * bright * (1f - place.Cold)),
                (Idiom.Vigil, 0.12f + 0.9f * place.Dark + 0.15f * place.Cold),
                (Idiom.Wonder, 0.25f + 0.7f * bright * place.Open),
                (Idiom.Drift, 0.3f + 0.3f * place.Cold + 0.2f * (1f - place.Open)),
            };
        }

        public static Style Land(MusicPlace place, SoundRandom random)
        {
            var likelihoods = Likelihoods(place);
            Idiom main = Pick(random, likelihoods);
            Idiom colour = Pick(random, likelihoods.Where(choice => choice.idiom != main).ToArray());

            Style style = main switch
            {
                Idiom.Reverie => Reverie(random),
                Idiom.Hearth => Hearth(random),
                Idiom.Vigil => Vigil(random),
                Idiom.Wonder => Wonder(random),
                _ => Drift(random),
            };
            style.Mood = MusicMood.Land;
            style.Name = $"{main}, coloured by {colour}";
            style.Parts = style.Parts.Concat(Colours(colour, style, random)).ToArray();
            return style;
        }

        // ---- The kinds of piece. ----

        private static Style Reverie(SoundRandom random)
        {
            Meter meter = random.Chance(0.6f) ? Meter.Four : Meter.Three;
            var style = new Style
            {
                Meters = new[] { meter },
                Tempo = random.Range(54f, 76f),
                TempoSpread = 2f,
                TempoLift = 0.02f,
                Modes = new[] { Pick(random, (Mode.Ionian, 0.3f), (Mode.Lydian, 0.25f), (Mode.Mixolydian, 0.2f), (Mode.Dorian, 0.15f), (Mode.Aeolian, 0.1f)) },
                Melodic = random.Chance(0.7f) ? MelodicStyle.Sparse : MelodicStyle.Lyrical,
                Language = HarmonicLanguage.Ambient,
                Colour = 0.55f,
                Tide = 0.5f,
                Rests = true,
                RestBars = 8 + random.Int(8),
            };

            Section second = Pick(random, (Section.AltoFlute, 1f), (Section.Celesta, 0.6f), (Section.Clarinet, 0.6f), (Section.Piano, 0.8f));
            int high = random.Chance(0.35f) ? 1 : 0;
            var parts = new List<Part>
            {
                P(Section.Piano, Textures.PianoHands, 0f, 0.3f, 0.15f, -3f, name: random.Pick(new[] { "broken", "satie", "tenths", "pulse" })),
                P(Section.Piano, Textures.Melody, 0.05f, 0.4f, 0.2f, 2f, high, use: MelodyUse.First),
                P(second, Textures.Melody, 0.05f, 0.36f, 0.2f, second == Section.Piano ? 2f : 1f, use: MelodyUse.Second),
                P(Section.Piano, Textures.Melody, 0.05f, 0.38f, 0.2f, 2f, high, use: MelodyUse.Contrast),
                P(Pick(random, (Section.Pad, 0.6f), (Section.Voices, 0.25f), (Section.MutedViolins, 0.15f)), Textures.Pad, 0.2f, 0.2f, 0.15f, -9f),
            };
            if (random.Chance(0.5f)) parts.Add(P(Section.Celesta, Textures.Sparkle, 0.42f, 0.25f, 0.15f, -7f));
            else parts.Add(P(Section.MutedViolins, Textures.Pad, 0.48f, 0.12f, 0.12f, -9f, 1));
            style.Parts = parts.ToArray();
            style.Leads = new[] { Section.Piano, Section.AltoFlute, Section.Celesta, Section.Clarinet };
            style.Staging = new Staging { Room = Room(random, 1.8f, 2.6f, 0.75f, 0.85f) };
            return style;
        }

        private static Style Hearth(SoundRandom random)
        {
            Meter meter = Pick(random, (Meter.Three, 0.5f), (Meter.Six, 0.25f), (Meter.Four, 0.25f));
            float tempo = meter.Beats == 3 ? random.Range(78f, 100f) : meter.Divisions == 3 ? random.Range(52f, 64f) : random.Range(72f, 92f);
            var style = new Style
            {
                Meters = new[] { meter },
                Tempo = tempo,
                TempoSpread = 2f,
                TempoLift = 0.04f,
                Modes = new[] { Pick(random, (Mode.Dorian, 0.35f), (Mode.Aeolian, 0.3f), (Mode.Mixolydian, 0.25f), (Mode.Phrygian, 0.1f)) },
                Melodic = meter.Beats == 4 && meter.Divisions == 2 ? MelodicStyle.Flowing : MelodicStyle.Folk,
                Language = HarmonicLanguage.Modal,
                Colour = 0.2f,
                Tide = 0.6f,
                Rests = true,
                RestBars = 6 + random.Int(6),
            };

            Section[] leads = { Section.AltoFlute, Section.BassFlute, Section.Ocarina, Section.Flute, Section.Fiddle, Section.Clarinet };
            Section first = random.Pick(leads);
            Section answer = random.Pick(leads.Where(lead => lead != first).ToArray());
            var parts = new List<Part>
            {
                P(Section.Guitar, Textures.Fingerpick, 0f, 0.4f, 0.2f, -2f, name: random.Chance(0.5f) ? "lilt" : ""),
                P(first, Textures.Melody, 0.05f, 0.45f, 0.2f, 3f, use: MelodyUse.First),
                P(answer, Textures.Melody, 0.05f, 0.45f, 0.2f, 3f, use: MelodyUse.Second),
                P(random.Chance(0.5f) ? first : Section.Dulcimer, Textures.Melody, 0.05f, 0.42f, 0.2f, 2.5f, use: MelodyUse.Contrast),
                P(random.Chance(0.5f) ? Section.Pad : Section.MutedCellos, Textures.Drone, 0.2f, 0.18f, 0.12f, -6f, name: random.Chance(0.3f) ? "single" : ""),
            };
            (bool[] low, bool[] high, float step) = HandPatterns(meter, random);
            parts.Add(new Part { Section = Section.HandDrum, Writer = Textures.Steps, From = 0.35f, Level = 0.36f, Rise = 0.2f, Fader = -4f, Steps = low, StepBeats = step });
            parts.Add(new Part { Section = Section.RimDrum, Writer = Textures.Steps, From = 0.42f, Level = 0.3f, Rise = 0.2f, Fader = -6f, Steps = high, StepBeats = step });
            if (random.Chance(0.5f)) parts.Add(new Part { Section = Section.Shaker, Writer = Textures.Steps, From = 0.5f, Level = 0.26f, Rise = 0.15f, Fader = -9f, Steps = Textures.Euclid(meter.Beats * 2 - 1, meter.Beats * 2), StepBeats = 0.5f });
            parts.Add(random.Chance(0.5f)
                ? P(Section.Dulcimer, Textures.Echo, 0.45f, 0.3f, 0.15f, -4f, 1)
                : P(Section.Dulcimer, Textures.Arpeggio, 0.45f, 0.24f, 0.15f, -7f, name: "slow"));
            style.Parts = parts.ToArray();
            style.Leads = leads;

            // A guitar in a stone room with its notes coming back a dotted beat later: the echo is half the sound.
            float beat = 60f / tempo;
            var staging = new Staging { Room = Room(random, 1.4f, 2.2f, 0.6f, 0.7f), EchoSeconds = beat * (random.Chance(0.6f) ? 1.5f : 0.75f), EchoFeedback = random.Range(0.28f, 0.42f) };
            staging.Echoes[Section.Guitar] = 0.22f;
            staging.Echoes[first] = 0.18f;
            staging.Echoes[answer] = 0.18f;
            staging.Echoes[Section.Dulcimer] = 0.25f;
            style.Staging = staging;
            return style;
        }

        private static Style Vigil(SoundRandom random)
        {
            Meter meter = Pick(random, (Meter.Four, 0.6f), (Meter.Three, 0.3f), (Meter.Twelve, 0.1f));
            var style = new Style
            {
                Meters = new[] { meter },
                Tempo = meter.Divisions == 3 ? random.Range(40f, 52f) : random.Range(48f, 68f),
                TempoSpread = 2f,
                TempoLift = 0.03f,
                Modes = new[] { Pick(random, (Mode.Aeolian, 0.35f), (Mode.Phrygian, 0.3f), (Mode.HarmonicMinor, 0.15f), (Mode.Dorian, 0.2f)) },
                Melodic = random.Chance(0.6f) ? MelodicStyle.Sparse : MelodicStyle.Lyrical,
                Language = HarmonicLanguage.Dark,
                Colour = 0.15f,
                Tide = 0.6f,
                Rests = true,
                RestBars = 6 + random.Int(7),
            };

            Section[] leads = { Section.SoloCello, Section.EnglishHorn, Section.BassClarinet, Section.AltoFlute, Section.MutedViolins };
            Section first = random.Pick(leads);
            Section answer = random.Pick(leads.Where(lead => lead != first).ToArray());
            var parts = new List<Part>
            {
                P(Pick(random, (Section.MutedCellos, 0.55f), (Section.Pad, 0.25f), (Section.Voices, 0.2f)), Textures.Drone, 0f, 0.25f, 0.2f, -2f, name: random.Chance(0.3f) ? "single" : ""),
                P(Section.Basses, Textures.Drone, 0.1f, 0.2f, 0.15f, -4f, -1, "single"),
                P(first, Textures.Melody, 0.1f, 0.38f, 0.25f, 3f, use: MelodyUse.First),
                P(answer, Textures.Melody, 0.1f, 0.36f, 0.25f, 3f, answer == Section.MutedViolins ? 1 : 0, use: MelodyUse.Second),
                P(first, Textures.Melody, 0.1f, 0.36f, 0.25f, 3f, use: MelodyUse.Contrast),
                P(random.Chance(0.6f) ? Section.Voices : Section.Choir, Textures.Pad, 0.3f, 0.16f, 0.2f, -5f, random.Chance(0.3f) ? -1 : 0),
            };
            parts.Add(Pick(random,
                (P(Section.MutedViolins, Textures.Pad, 0.48f, 0.1f, 0.12f, -7f, 1, "tremolo"), 0.5f),
                (P(Section.Celesta, Textures.Sparkle, 0.48f, 0.2f, 0.12f, -9f), 0.25f),
                (P(Section.Harp, Textures.Arpeggio, 0.45f, 0.18f, 0.12f, -9f, name: "slow"), 0.25f)));
            int beats = meter.Beats * 2;
            parts.Add(new Part { Section = random.Chance(0.6f) ? Section.HandDrum : Section.FrameDrum, Writer = Textures.Steps, From = 0.35f, Level = 0.3f, Rise = 0.2f, Fader = -5f, Steps = Textures.Euclid(2 + random.Int(2), beats * 2, 1 + random.Int(3)), StepBeats = meter.Divisions == 3 ? 1f / 3f : 0.5f });
            parts.Add(random.Chance(0.6f)
                ? P(Section.Bells, Textures.Toll, 0.55f, 0.2f, 0.15f, -6f)
                : P(Section.Piano, Textures.Toll, 0.4f, 0.3f, 0.15f, -4f, -2));
            style.Parts = parts.ToArray();
            style.Leads = leads;
            style.Staging = new Staging { Room = Room(random, 3.2f, 4.2f, 1f, 1.1f) };
            return style;
        }

        private static Style Wonder(SoundRandom random)
        {
            Meter meter = random.Chance(0.7f) ? Meter.Four : Meter.Three;
            var style = new Style
            {
                Meters = new[] { meter },
                Tempo = random.Range(58f, 80f),
                TempoSpread = 2f,
                TempoLift = 0.04f,
                Modes = new[] { Pick(random, (Mode.Ionian, 0.55f), (Mode.Mixolydian, 0.25f), (Mode.Lydian, 0.2f)) },
                Melodic = MelodicStyle.Hymn,
                Language = HarmonicLanguage.Hymn,
                Colour = 0.2f,
                Tide = 0.9f,
                Rests = true,
                RestBars = 6 + random.Int(5),
            };

            Section first = Pick(random, (Section.Horns, 0.55f), (Section.Cellos, 0.2f), (Section.Violins, 0.15f), (Section.Clarinet, 0.1f));
            Section second = Pick(random, (Section.Violins, 0.5f), (Section.Horns, first == Section.Horns ? 0f : 0.3f), (Section.Flute, 0.2f), (Section.Oboe, 0.15f));
            Section contrast = Pick(random, (Section.Clarinet, 1f), (Section.Oboe, 0.7f), (Section.Cellos, first == Section.Cellos ? 0f : 0.8f), (Section.Flute, 0.5f), (Section.EnglishHorn, 0.4f));
            Section tenor = Pick(random, (Section.Bassoon, 0.5f), (Section.Violas, 0.3f), (Section.Horns, first == Section.Horns || second == Section.Horns ? 0f : 0.2f));
            Section alto = Pick(random, (Section.Clarinet, 0.5f), (Section.Violas, tenor == Section.Violas ? 0f : 0.3f), (Section.Oboe, 0.2f));
            var parts = new List<Part>
            {
                P(random.Chance(0.7f) ? Section.Cellos : Section.Bassoon, Textures.Hymn, 0f, 0.32f, 0.3f, -2f, name: "bass"),
                P(tenor, Textures.Hymn, 0f, 0.3f, 0.25f, -3f, name: "tenor"),
                P(alto, Textures.Hymn, 0f, 0.3f, 0.25f, -3f, name: "alto"),
                P(first, Textures.Melody, 0.05f, 0.4f, 0.35f, 3f, use: MelodyUse.First),
                P(second, Textures.Melody, 0.05f, 0.35f, 0.35f, 2f, use: MelodyUse.Second),
                P(contrast, Textures.Melody, 0.05f, 0.36f, 0.3f, 2.5f, use: MelodyUse.Contrast),
                P(tenor == Section.Violas ? Section.Horns : Section.Violas, Textures.Hymn, 0.45f, 0.28f, 0.25f, -3f, name: "tenor"),
                P(Section.Basses, Textures.Bass, 0.45f, 0.25f, 0.2f, -3f, -1, "long"),
                P(random.Chance(0.7f) ? Section.Choir : Section.Voices, Textures.Pad, random.Range(0.5f, 0.6f), 0.25f, 0.3f, -4f),
            };
            if (random.Chance(0.75f)) parts.Add(P(Section.Timpani, Textures.Drums, 0.5f, 0.26f, 0.2f, -3f, name: "cadence"));
            parts.Add(Pick(random,
                (P(Section.Harp, Textures.Arpeggio, 0.3f, 0.25f, 0.15f, -4f, name: "slow"), 0.4f),
                (P(Section.Harp, Textures.Arpeggio, 0.4f, 0.22f, 0.15f, -6f), 0.2f),
                (P(Section.Celesta, Textures.Sparkle, 0.45f, 0.22f, 0.12f, -8f), 0.2f),
                (P(Section.MutedViolins, Textures.Pad, 0.5f, 0.12f, 0.12f, -8f, 1, "tremolo"), 0.2f)));
            style.Parts = parts.ToArray();
            style.Leads = new[] { first, Section.Violins, Section.Horns, Section.Cellos, Section.Clarinet, Section.Oboe }.Distinct().ToArray();
            style.Staging = new Staging { Room = Room(random, 2.6f, 3.2f, 0.95f, 1f) };
            return style;
        }

        private static Style Drift(SoundRandom random)
        {
            var style = new Style
            {
                Meters = new[] { Meter.Four },
                Tempo = random.Range(60f, 80f),
                TempoSpread = 2f,
                TempoLift = 0.02f,
                Modes = new[] { Pick(random, (Mode.Lydian, 0.3f), (Mode.Ionian, 0.3f), (Mode.Dorian, 0.2f), (Mode.Mixolydian, 0.2f)) },
                Melodic = MelodicStyle.Sparse,
                Language = HarmonicLanguage.Ambient,
                Colour = 0.6f,
                Tide = 0.4f,
                Rests = true,
                RestBars = 6 + random.Int(7),
            };

            var parts = new List<Part> { P(Section.Pad, Textures.Pad, 0f, 0.24f, 0.12f, -4f) };
            Section[] voices = { Section.Piano, Section.Harp, Section.Celesta, Section.AltoFlute, Section.Voices, Section.MutedViolins, Section.Dulcimer, Section.Guitar };
            float[] lengths = { 5.5f, 6.25f, 7f, 7.75f, 9f, 10.5f, 11.25f, 13f };
            int count = 3 + random.Int(2);
            var used = new List<Section>();
            var staging = new Staging { Room = Room(random, 3f, 4.5f, 1f, 1.1f), EchoSeconds = random.Range(0.9f, 1.4f), EchoFeedback = random.Range(0.3f, 0.4f) };
            for (int i = 0; i < count; i++)
            {
                Section section = random.Pick(voices.Where(voice => !used.Contains(voice)).ToArray());
                used.Add(section);
                parts.Add(Loop(section, random.Pick(lengths.Where(length => !parts.Any(part => part.LoopBeats == length)).ToArray()), i * 0.15f, random));
                staging.Echoes[section] = 0.2f;
            }

            // The theme still comes by now and then, on the piano, so that even this land's music has a tune of its own.
            parts.Add(P(Section.Piano, Textures.Melody, 0.4f, 0.34f, 0.15f, 1f, use: MelodyUse.Second));
            style.Parts = parts.ToArray();
            style.Leads = new[] { Section.Piano, Section.AltoFlute, Section.Celesta };
            style.Staging = staging;
            return style;
        }

        private static Part Loop(Section section, float beats, float from, SoundRandom random)
        {
            int notes = 1 + random.Int(3);
            var times = new float[notes];
            var degrees = new int[notes];
            float at = random.Range(0f, 1f);
            int degree = random.Int(10);
            for (int i = 0; i < notes; i++)
            {
                times[i] = at;
                degrees[i] = degree;
                at += random.Pick(new[] { 0.5f, 1f, 1.5f, 2f });
                degree += random.Pick(new[] { -2, -1, 1, 2, 3 });
            }
            return new Part { Section = section, Writer = Textures.Loops, From = from, Level = 0.48f, Rise = 0.15f, Fader = 3f - random.Float() * 2f, LoopBeats = beats, LoopTimes = times, LoopDegrees = degrees };
        }

        // A second kind of piece lends one or two of its players to the first, late in the form, so a land's music gathers something unexpected as it goes on.
        private static IEnumerable<Part> Colours(Idiom idiom, Style style, SoundRandom random)
        {
            bool Has(Section section) => style.Parts.Any(part => part.Section == section);
            switch (idiom)
            {
                case Idiom.Reverie:
                    yield return P(Has(Section.Piano) ? Section.Celesta : Section.Piano, Textures.Sparkle, 0.45f, 0.22f, 0.12f, -6f);
                    break;
                case Idiom.Hearth:
                    if (!Has(Section.HandDrum) && style.Tempo > 56f)
                    {
                        (bool[] low, _, float step) = HandPatterns(style.Meters[0], random);
                        yield return new Part { Section = Section.HandDrum, Writer = Textures.Steps, From = 0.48f, Level = 0.3f, Rise = 0.15f, Fader = -6f, Steps = low, StepBeats = step };
                    }
                    else yield return P(Section.Guitar, Textures.Fingerpick, 0.45f, 0.24f, 0.12f, -6f);
                    break;
                case Idiom.Vigil:
                    yield return random.Chance(0.5f) && !Has(Section.MutedCellos) ? P(Section.MutedCellos, Textures.Drone, 0.4f, 0.16f, 0.1f, -7f) : P(Section.Voices, Textures.Pad, 0.5f, 0.14f, 0.12f, -8f);
                    break;
                case Idiom.Wonder:
                    yield return random.Chance(0.5f) ? P(Section.Horns, Textures.Pad, 0.5f, 0.18f, 0.15f, -6f, -1) : P(Section.Choir, Textures.Pad, 0.55f, 0.16f, 0.15f, -7f);
                    break;
                case Idiom.Drift:
                    Section voice = Has(Section.Harp) ? Section.Celesta : Section.Harp;
                    Part loop = Loop(voice, random.Pick(new[] { 7f, 9f, 11.25f }), 0.45f, random);
                    loop.Fader -= 3f;
                    yield return loop;
                    break;
            }
        }

        // A goblet drum's two strokes: the deep one on an even spread of a few, the sharp one filling some of the rest.
        private static (bool[] low, bool[] high, float step) HandPatterns(Meter meter, SoundRandom random)
        {
            int steps = meter.Beats * meter.Divisions;
            float step = 1f / meter.Divisions;
            bool[] low = Textures.Euclid(Math.Max(2, steps / 3 + random.Int(2) - 1), steps);
            bool[] high = Textures.Euclid(Math.Max(2, steps / 2 + random.Int(2)), steps, 1 + random.Int(steps - 1));
            for (int i = 0; i < steps; i++)
            {
                if (low[i]) high[i] = false;
            }
            return (low, high, step);
        }

        private static ReverbSettings Room(SoundRandom random, float shortest, float longest, float brightness, float wet)
        {
            float decay = random.Range(shortest, longest);
            return new ReverbSettings { DecayTime = decay, DecayHigh = decay * 0.45f * brightness, PreDelay = random.Range(0.018f, 0.04f), Wet = wet, EchoTime = 0.3f, EchoBrightness = 3000f };
        }

        // ---- Home. ----

        /// <summary>
        /// The Haven's own piece, the same on every visit, as a home's music is the one tune a player comes to know: a waltz on the twelve-string with a flute's tune over it, an
        /// echo in the square, and in daylight, the strings and horns of a hymn coming in under it.
        /// </summary>
        public static Style Haven(SoundRandom random)
        {
            var style = new Style
            {
                Mood = MusicMood.Haven,
                Name = "the Haven",
                Meters = new[] { Meter.Three },
                Tempo = 84f,
                TempoSpread = 0f,
                TempoLift = 0.03f,
                Modes = new[] { random.Chance(0.5f) ? Mode.Ionian : Mode.Mixolydian },
                Melodic = MelodicStyle.Folk,
                Language = HarmonicLanguage.Hymn,
                Colour = 0.25f,
                Tide = 0.7f,
                Rests = true,
                RestBars = 4 + random.Int(4),
            };
            (bool[] low, _, float step) = HandPatterns(Meter.Three, random);
            style.Parts = new[]
            {
                P(Section.Guitar, Textures.Fingerpick, 0f, 0.38f, 0.2f, 0f, name: "lilt"),
                P(Section.AltoFlute, Textures.Melody, 0.05f, 0.45f, 0.2f, 3f, use: MelodyUse.First),
                P(Section.Ocarina, Textures.Melody, 0.05f, 0.45f, 0.2f, 3f, use: MelodyUse.Second),
                P(Section.Clarinet, Textures.Melody, 0.05f, 0.42f, 0.2f, 2.5f, use: MelodyUse.Contrast),
                P(Section.Pad, Textures.Drone, 0.15f, 0.15f, 0.1f, -8f),
                P(Section.Dulcimer, Textures.Echo, 0.35f, 0.28f, 0.15f, -5f, 1),
                new Part { Section = Section.HandDrum, Writer = Textures.Steps, From = 0.5f, Level = 0.28f, Rise = 0.15f, Fader = -7f, Steps = low, StepBeats = step },
                P(Section.Cellos, Textures.Hymn, 0.5f, 0.24f, 0.2f, -5f, name: "bass"),
                P(Section.Violas, Textures.Hymn, 0.5f, 0.24f, 0.2f, -5f, name: "tenor"),
                P(Section.Horns, Textures.Pad, 0.58f, 0.18f, 0.15f, -6f, -1),
            };
            style.Leads = new[] { Section.AltoFlute, Section.Ocarina, Section.Clarinet, Section.Flute };
            var staging = new Staging { Room = new ReverbSettings { DecayTime = 2.1f, DecayHigh = 0.9f, PreDelay = 0.025f, Wet = 0.85f, EchoTime = 0.3f, EchoBrightness = 3000f }, EchoSeconds = 60f / 84f * 1.5f, EchoFeedback = 0.3f };
            staging.Echoes[Section.Guitar] = 0.2f;
            staging.Echoes[Section.AltoFlute] = 0.15f;
            staging.Echoes[Section.Ocarina] = 0.15f;
            staging.Echoes[Section.Dulcimer] = 0.25f;
            style.Staging = staging;
            return style;
        }

        // ---- Fights. ----

        /// <summary>
        /// A keep's tyrant. Built from the land's own piece, so the fight is the land's music turned to violence and not something from elsewhere: the same key, the theme's shape in a
        /// minor mode over driving strings, war drums, low brass circling, and the land's own lead instrument taking the theme's middle. Each land drives differently: its accents,
        /// its drums, its speed and its mode are its own.
        /// </summary>
        public static Style Boss(MusicPlace place, Style land, SoundRandom random)
        {
            bool gallop = random.Chance(0.25f);
            Meter meter = gallop ? Meter.Six : Meter.Four;
            var style = new Style
            {
                Mood = MusicMood.Boss,
                Name = gallop ? "a tyrant's hunt, galloping" : "a tyrant's hunt",
                Meters = new[] { meter },
                Tempo = gallop ? random.Range(92f, 110f) : random.Range(132f, 156f),
                TempoSpread = 2f,
                TempoLift = 0.05f,
                Modes = new[] { Pick(random, (Mode.Aeolian, 0.35f), (Mode.Phrygian, 0.15f + place.Dark * 0.3f), (Mode.HarmonicMinor, 0.3f), (Mode.Dorian, 0.2f)) },
                Melodic = MelodicStyle.Heroic,
                Language = HarmonicLanguage.Epic,
                TonicShift = random.Pick(new[] { 0, 1, 2, -2 }),
                Colour = 0.05f,
                Urgent = true,
                Tide = 1f,
            };

            bool[] accents = Textures.Euclid(random.Pick(new[] { 3, 5, 3 }), 8, random.Int(3));
            Section signature = land.Leads.Length > 0 ? land.Leads[0] : Section.Cellos;
            if (signature is Section.Piano or Section.Celesta) signature = Section.Violins;
            var parts = new List<Part>
            {
                Steady(P(Section.Cellos, Textures.Ostinato, 0f, 0.5f, 0.4f, -2f, name: "low"), accents),
                Steady(P(Section.Basses, Textures.Ostinato, 0f, 0.45f, 0.4f, -2f, -1, "low"), accents),
                Steady(P(Section.Violas, Textures.Ostinato, 0.2f, 0.45f, 0.3f, -2f, name: "middle"), accents),
                Steady(P(random.Chance(0.5f) ? Section.Tuba : Section.Bassoon, Textures.Prowl, 0.3f, 0.45f, 0.35f, -3f, name: random.Chance(0.5f) ? "wide" : ""), Textures.Euclid(5, 8, random.Int(4))),
                P(Section.Timpani, Textures.Drums, 0.15f, 0.36f, 0.5f, -2.5f, name: "war"),
                P(Section.Horns, Textures.Melody, 0.25f, 0.55f, 0.4f, 4.5f),
                P(Section.Trombones, Textures.Stabs, 0.45f, 0.5f, 0.4f, -4.5f, -1),
                Steady(P(Section.Violins, Textures.Ostinato, 0.55f, 0.44f, 0.3f, -2f, name: "high"), accents),
                P(Section.Cymbal, Textures.Swell, 0.45f, 0.4f, 0.4f, -1f),
                P(Section.Trumpets, Textures.Melody, 0.72f, 0.48f, 0.4f, 4.5f, 1),
                P(Section.Choir, Textures.Pad, 0.8f, 0.4f, 0.35f, -4.5f),
                P(signature, Textures.Melody, 0.2f, 0.5f, 0.35f, 4f, use: MelodyUse.Contrast),
            };
            parts.Add(random.Chance(0.5f)
                ? P(Section.BigDrum, Textures.Drums, 0.35f, 0.4f, 0.5f, -2.5f, name: "war")
                : new Part { Section = Section.HandDrum, Writer = Textures.Steps, From = 0.3f, Level = 0.42f, Rise = 0.4f, Fader = -2f, Steps = Textures.Euclid(random.Pick(new[] { 5, 7, 9 }), 16, random.Int(4)), StepBeats = meter.Divisions == 3 ? 1f / 3f : 0.25f });
            style.Parts = parts.ToArray();
            style.Leads = new[] { Section.Horns, signature, Section.Trumpets };
            style.Staging = new Staging { Room = Room(random, 2f, 2.6f, 0.9f, 0.95f) };
            return style;
        }

        private static Part Steady(Part part, bool[] steps)
        {
            part.Steps = steps;
            return part;
        }

        /// <summary>
        /// An arena's fight, the biggest music the game has: the whole band and the choir, brass fanfares and the deepest drums, from the moment the gates close. Each kind of arena
        /// colours it: the colosseum's trumpets, the cage's hand drums and circling low reeds, the sun-temple's flat second over a major third, the dig's buried voices, the cathedral's
        /// choir and bells.
        /// </summary>
        public static Style Arena(MusicPlace place, SoundRandom random)
        {
            ArenaKind kind = place.ArenaKind;
            Mode mode = kind switch
            {
                ArenaKind.SunTemple => Mode.PhrygianDominant,
                ArenaKind.Cathedral => random.Chance(0.5f) ? Mode.HarmonicMinor : Mode.Phrygian,
                ArenaKind.Cage => random.Chance(0.6f) ? Mode.Phrygian : Mode.Aeolian,
                _ => Pick(random, (Mode.Aeolian, 0.4f), (Mode.HarmonicMinor, 0.35f), (Mode.Dorian, 0.25f)),
            };
            var style = new Style
            {
                Mood = MusicMood.Arena,
                Name = $"the arena's fight, {kind}",
                Meters = new[] { Meter.Four },
                Tempo = kind == ArenaKind.Cathedral ? random.Range(112f, 128f) : random.Range(120f, 148f),
                TempoSpread = 2f,
                TempoLift = 0.04f,
                Modes = new[] { mode },
                Melodic = MelodicStyle.Heroic,
                Language = mode == Mode.PhrygianDominant ? HarmonicLanguage.Modal : HarmonicLanguage.Epic,
                TonicShift = random.Pick(new[] { 0, 1, -2 }),
                Colour = 0.05f,
                Urgent = true,
                Tide = 1f,
            };

            bool[] accents = Textures.Euclid(random.Pick(new[] { 3, 5 }), 8, random.Int(3));
            var parts = new List<Part>
            {
                Steady(P(Section.Cellos, Textures.Ostinato, 0f, 0.55f, 0.4f, -2f, name: "low"), accents),
                Steady(P(Section.Basses, Textures.Ostinato, 0f, 0.5f, 0.4f, -2f, -1, "low"), accents),
                Steady(P(Section.Violas, Textures.Ostinato, 0.1f, 0.48f, 0.3f, -2f, name: "middle"), accents),
                P(Section.Timpani, Textures.Drums, 0f, 0.42f, 0.5f, -2.5f, name: "war"),
                P(Section.BigDrum, Textures.Drums, 0.2f, 0.45f, 0.5f, -2.5f, name: "war"),
                P(Section.Horns, Textures.Melody, 0.1f, 0.58f, 0.4f, 4.5f),
                P(Section.Trombones, Textures.Stabs, 0.3f, 0.52f, 0.4f, -4.5f, -1),
                Steady(P(Section.Violins, Textures.Ostinato, 0.35f, 0.46f, 0.3f, -2f, name: "high"), accents),
                P(Section.Choir, Textures.Pad, 0.45f, 0.42f, 0.4f, -4f),
                P(Section.Cymbal, Textures.Swell, 0.35f, 0.42f, 0.4f, -1f),
                P(Section.Trumpets, Textures.Melody, 0.6f, 0.5f, 0.4f, 4.5f, 1),
                P(Section.Trumpets, Textures.Fanfare, 0.5f, 0.45f, 0.4f, -2f),
            };
            switch (kind)
            {
                case ArenaKind.Colosseum:
                    parts.Add(P(Section.Horns, Textures.Fanfare, 0.3f, 0.45f, 0.4f, -3f, -1));
                    break;
                case ArenaKind.Cage:
                    parts.Add(Steady(P(Section.Bassoon, Textures.Prowl, 0.2f, 0.48f, 0.35f, -3f, name: "wide"), Textures.Euclid(5, 8, random.Int(4))));
                    parts.Add(new Part { Section = Section.HandDrum, Writer = Textures.Steps, From = 0.15f, Level = 0.42f, Rise = 0.4f, Fader = -2f, Steps = Textures.Euclid(7, 16, random.Int(4)), StepBeats = 0.25f });
                    parts.Add(new Part { Section = Section.RimDrum, Writer = Textures.Steps, From = 0.3f, Level = 0.34f, Rise = 0.3f, Fader = -4f, Steps = Textures.Euclid(5, 16, 3), StepBeats = 0.25f });
                    break;
                case ArenaKind.SunTemple:
                    parts.Add(P(Section.FrameDrum, Textures.Drums, 0.15f, 0.42f, 0.25f, 5f, name: "dance"));
                    parts.Add(P(Section.AltoFlute, Textures.Melody, 0.2f, 0.5f, 0.3f, 4f, use: MelodyUse.Contrast));
                    parts.Add(P(Section.Dulcimer, Textures.Arpeggio, 0.35f, 0.3f, 0.2f, -5f));
                    break;
                case ArenaKind.Onslaught:
                    parts.Add(P(Section.Voices, Textures.Pad, 0.2f, 0.3f, 0.3f, -3f, -1));
                    parts.Add(P(Section.Tuba, Textures.Prowl, 0.3f, 0.45f, 0.35f, -3f));
                    break;
                case ArenaKind.Cathedral:
                    parts.Add(P(Section.Pad, Textures.Pad, 0f, 0.3f, 0.3f, -3f));
                    parts.Add(P(Section.Bells, Textures.Toll, 0.25f, 0.4f, 0.3f, -3f));
                    parts.Add(P(Section.Voices, Textures.Pad, 0.35f, 0.3f, 0.3f, -4f, 1));
                    break;
                default:
                    parts.Add(new Part { Section = Section.HandDrum, Writer = Textures.Steps, From = 0.25f, Level = 0.4f, Rise = 0.4f, Fader = -3f, Steps = Textures.Euclid(5, 16, random.Int(4)), StepBeats = 0.25f });
                    break;
            }
            style.Parts = parts.ToArray();
            style.Leads = new[] { Section.Horns, Section.Trumpets, Section.Violins };
            style.Staging = new Staging { Room = Room(random, kind == ArenaKind.Cathedral ? 3.4f : 2.4f, kind == ArenaKind.Cathedral ? 4f : 3f, 0.95f, 1f) };
            return style;
        }

        /// <summary>Hands the tune's repeat and middle to other players when a piece comes round again, so a long stay in a land doesn't hear the same instrument say everything.</summary>
        public static void Rotate(Style style, SoundRandom random)
        {
            if (style.Leads.Length < 2) return;
            Section first = style.Parts.FirstOrDefault(part => part.Writer == Textures.Melody && part.Use is MelodyUse.First or MelodyUse.Always)?.Section ?? style.Leads[0];
            foreach (Part part in style.Parts)
            {
                if (part.Writer != Textures.Melody || part.Use is not (MelodyUse.Second or MelodyUse.Contrast)) continue;
                Section[] others = style.Leads.Where(lead => lead != first).ToArray();
                if (others.Length > 0) part.Section = random.Pick(others);
            }
        }
    }
}
