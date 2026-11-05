namespace Dino_Engine.Sound.Music
{
    /// <summary>What the music is for, at this moment.</summary>
    public enum MusicMood
    {
        Silence,
        Wander,     // Out in the land.
        Town,       // Among people.
        Tension,    // Something's near.
        Battle,
        Boss,
        Sacred,     // A shrine.
        Haunted,    // The dead are close.
    }

    /// <summary>A moment that gets its own few bars, then hands back to whatever was going on.</summary>
    public enum MusicCue
    {
        None,
        Victory,
        Defeat,
    }

    /// <summary>One line in an arrangement: who plays, how, and from what intensity. Intensity adding players is how the music grows.</summary>
    public sealed class Part
    {
        public Section Section;
        public PartWriter Writer = null!;
        public float From;              // Plays once intensity reaches this.
        public float To = 2f;           // And stops past this.
        public float Level = 0.5f;      // 0 to 1, at the bottom of its range.
        public float Rise = 0.3f;       // Added at full intensity.
        public float Fader;             // Decibels in the mix. Measured, a melody played at the same level as the rest came out the quietest thing in the band.
        public int Octave;              // Moves a melody up or down.
        public string Name = "";        // What part of the tune or pattern it plays.
        public MelodyUse Use = MelodyUse.Always;
    }

    /// <summary>Which sections of the form a melody part plays in, so that a tune passes between instruments.</summary>
    public enum MelodyUse
    {
        Always,
        First,      // The tune's first statement.
        Second,     // Its repeat.
        Contrast,   // The middle section.
    }

    public delegate void PartWriter(BarContext bar, Part part);

    /// <summary>
    /// How one kind of music goes. Adding one is adding an entry here: nothing else has to know. The arrangement is a table of who plays what from which intensity, which is
    /// the way game scores are layered, but as these parts are written live, it's a table of players and not of recordings, and they can follow the harmony wherever it goes.
    /// </summary>
    public sealed class Style
    {
        public MusicMood Mood;
        public Meter[] Meters = { Meter.Four };
        public float Tempo = 90f;           // Beats a minute.
        public float TempoSpread = 8f;      // Each world's own.
        public float TempoLift = 0.06f;     // Share faster at full intensity.
        public Mode[] Modes = { Mode.Aeolian };
        public MelodicStyle Melodic = MelodicStyle.Flowing;
        public int TonicShift;              // Semitones from the world's key. A boss a step up feels higher stakes.
        public float Colour = 0.3f;         // How many chords get something added.
        public bool Urgent;                 // Cuts in at once, not at the end of a phrase.
        public bool Prompt;                 // Comes in at the next bar, for what can't wait for a phrase but needn't interrupt a beat.
        public bool Rests;                  // Goes quiet between phrases, as exploring music should.
        public int RestBars = 12;
        public float Tide = 0.6f;           // How far the form rises and falls by itself, around what the game asks for.
        public Part[] Parts = Array.Empty<Part>();
    }

    /// <summary>The styles of music a world has.</summary>
    public static class Styles
    {
        public static readonly Style Wander = new()
        {
            Mood = MusicMood.Wander,
            Meters = new[] { Meter.Four, Meter.Three, Meter.Twelve },
            Tempo = 72f,
            TempoSpread = 7f,
            Modes = new[] { Mode.Dorian, Mode.Aeolian, Mode.Ionian, Mode.Mixolydian, Mode.Lydian },
            Melodic = MelodicStyle.Lyrical,
            Rests = true,
            RestBars = 14,
            Parts = new[]
            {
                new Part { Section = Section.Harp, Writer = Textures.Arpeggio, Level = 0.4f, Rise = 0.2f },
                new Part { Section = Section.Violas, Writer = Textures.Pad, From = 0.15f, Level = 0.22f, Rise = 0.2f, Fader = -4f, Octave = -1 },
                new Part { Section = Section.Cellos, Writer = Textures.Bass, From = 0.2f, Level = 0.24f, Rise = 0.22f, Fader = -2f, Name = "long" },
                new Part { Section = Section.Flute, Writer = Textures.Melody, From = 0.1f, Level = 0.38f, Rise = 0.25f, Fader = 4f, Use = MelodyUse.First },
                new Part { Section = Section.Oboe, Writer = Textures.Melody, From = 0.1f, Level = 0.36f, Rise = 0.25f, Fader = 4f, Use = MelodyUse.Second },
                new Part { Section = Section.Clarinet, Writer = Textures.Melody, From = 0.1f, Level = 0.38f, Rise = 0.25f, Fader = 4f, Use = MelodyUse.Contrast },
                new Part { Section = Section.Violins, Writer = Textures.Pad, From = 0.45f, Level = 0.2f, Rise = 0.25f, Fader = -4f },
                new Part { Section = Section.Cellos, Writer = Textures.Countermelody, From = 0.6f, Level = 0.3f, Rise = 0.22f, Fader = -2f },
                new Part { Section = Section.Horns, Writer = Textures.Pad, From = 0.72f, Level = 0.22f, Rise = 0.3f, Fader = -4f, Octave = -1 },
                new Part { Section = Section.Timpani, Writer = Textures.Drums, From = 0.82f, Level = 0.26f, Rise = 0.2f, Name = "cadence" },
            },
        };

        public static readonly Style Town = new()
        {
            Mood = MusicMood.Town,
            Meters = new[] { Meter.Six, Meter.Three, Meter.Four },
            Tempo = 104f,
            TempoSpread = 10f,
            Modes = new[] { Mode.Mixolydian, Mode.Ionian, Mode.Dorian },
            Colour = 0.12f,
            Parts = new[]
            {
                new Part { Section = Section.Lute, Writer = Textures.Strum, Level = 0.5f, Rise = 0.22f },
                new Part { Section = Section.Pizzicato, Writer = Textures.Bass, From = 0.1f, Level = 0.5f, Rise = 0.2f, Name = "dance" },
                new Part { Section = Section.Fiddle, Writer = Textures.Melody, From = 0.1f, Level = 0.5f, Rise = 0.22f, Fader = 2.5f, Use = MelodyUse.First },
                new Part { Section = Section.Flute, Writer = Textures.Melody, From = 0.1f, Level = 0.5f, Rise = 0.22f, Fader = 2.5f, Use = MelodyUse.Second, Octave = 1 },
                new Part { Section = Section.Fiddle, Writer = Textures.Melody, From = 0.1f, Level = 0.5f, Rise = 0.22f, Fader = 2.5f, Use = MelodyUse.Contrast },
                new Part { Section = Section.FrameDrum, Writer = Textures.Drums, From = 0.25f, Level = 0.4f, Rise = 0.25f, Fader = 7f, Name = "dance" },
                new Part { Section = Section.Tambourine, Writer = Textures.Drums, From = 0.5f, Level = 0.3f, Rise = 0.3f, Fader = 3f, Name = "dance" },
                new Part { Section = Section.Flute, Writer = Textures.Melody, From = 0.7f, Level = 0.42f, Rise = 0.2f, Fader = 2.5f, Use = MelodyUse.First, Octave = 1 },
                new Part { Section = Section.Clarinet, Writer = Textures.Countermelody, From = 0.8f, Level = 0.34f, Rise = 0.16f },
            },
        };

        public static readonly Style Tension = new()
        {
            Mood = MusicMood.Tension,
            Prompt = true,
            Tempo = 84f,
            TempoSpread = 6f,
            TempoLift = 0.12f,
            Modes = new[] { Mode.Aeolian, Mode.Phrygian },
            Colour = 0.2f,
            Parts = new[]
            {
                new Part { Section = Section.Cellos, Writer = Textures.Pedal, Level = 0.3f, Rise = 0.3f },
                new Part { Section = Section.Basses, Writer = Textures.Pedal, Level = 0.3f, Rise = 0.3f, Octave = -1 },
                new Part { Section = Section.Timpani, Writer = Textures.Drums, From = 0.2f, Level = 0.28f, Rise = 0.3f, Name = "heart" },
                new Part { Section = Section.Violas, Writer = Textures.Pad, From = 0.3f, Level = 0.2f, Rise = 0.2f, Fader = -2f, Octave = -1 },
                new Part { Section = Section.Horns, Writer = Textures.Melody, From = 0.5f, Level = 0.3f, Rise = 0.3f, Fader = 2.5f, Use = MelodyUse.Second, Octave = -1 },
                new Part { Section = Section.Violins, Writer = Textures.Pad, From = 0.72f, Level = 0.16f, Rise = 0.3f, Fader = -2f, Octave = 1, Name = "tremolo" },
            },
        };

        public static readonly Style Battle = new()
        {
            Mood = MusicMood.Battle,
            Tempo = 138f,
            TempoSpread = 8f,
            TempoLift = 0.05f,
            Modes = new[] { Mode.Aeolian, Mode.Dorian },
            Melodic = MelodicStyle.Heroic,
            Colour = 0.1f,
            Urgent = true,
            Tide = 1f,
            Parts = new[]
            {
                new Part { Section = Section.Cellos, Writer = Textures.Ostinato, Level = 0.5f, Rise = 0.4f, Fader = -2f, Name = "low" },
                new Part { Section = Section.Basses, Writer = Textures.Ostinato, Level = 0.45f, Rise = 0.4f, Fader = -2f, Name = "low", Octave = -1 },
                new Part { Section = Section.BigDrum, Writer = Textures.Drums, From = 0.34f, Level = 0.4f, Rise = 0.5f, Fader = -2.5f, Name = "war" },
                new Part { Section = Section.Timpani, Writer = Textures.Drums, From = 0.15f, Level = 0.36f, Rise = 0.5f, Fader = -2.5f, Name = "war" },
                new Part { Section = Section.Violas, Writer = Textures.Ostinato, From = 0.2f, Level = 0.45f, Rise = 0.3f, Fader = -2f, Name = "middle" },
                new Part { Section = Section.Horns, Writer = Textures.Melody, From = 0.25f, Level = 0.55f, Rise = 0.4f, Fader = 4.5f },
                new Part { Section = Section.Trombones, Writer = Textures.Stabs, From = 0.4f, Level = 0.5f, Rise = 0.4f, Fader = -4.5f, Octave = -1 },
                new Part { Section = Section.Violins, Writer = Textures.Ostinato, From = 0.55f, Level = 0.44f, Rise = 0.3f, Fader = -2f, Name = "high" },
                new Part { Section = Section.Cymbal, Writer = Textures.Swell, From = 0.45f, Level = 0.4f, Rise = 0.4f, Fader = -1f },
                new Part { Section = Section.Trumpets, Writer = Textures.Melody, From = 0.72f, Level = 0.48f, Rise = 0.4f, Fader = 4.5f, Octave = 1 },
                new Part { Section = Section.Choir, Writer = Textures.Pad, From = 0.84f, Level = 0.4f, Rise = 0.35f, Fader = -4.5f },
            },
        };

        public static readonly Style Boss = new()
        {
            Mood = MusicMood.Boss,
            Tempo = 148f,
            TempoSpread = 6f,
            TempoLift = 0.04f,
            Modes = new[] { Mode.HarmonicMinor, Mode.Phrygian },
            Melodic = MelodicStyle.Heroic,
            TonicShift = 2,
            Colour = 0.05f,
            Urgent = true,
            Tide = 1f,
            Parts = new[]
            {
                new Part { Section = Section.Cellos, Writer = Textures.Ostinato, Level = 0.5f, Rise = 0.48f, Fader = -2f, Name = "low" },
                new Part { Section = Section.Basses, Writer = Textures.Ostinato, Level = 0.45f, Rise = 0.48f, Fader = -2f, Name = "low", Octave = -1 },
                new Part { Section = Section.Violas, Writer = Textures.Ostinato, Level = 0.42f, Rise = 0.46f, Fader = -2f, Name = "middle" },
                new Part { Section = Section.Violins, Writer = Textures.Ostinato, From = 0.3f, Level = 0.4f, Rise = 0.5f, Fader = -2f, Name = "high" },
                new Part { Section = Section.Timpani, Writer = Textures.Drums, From = 0.22f, Level = 0.45f, Rise = 0.45f, Fader = -2.5f, Name = "war" },
                new Part { Section = Section.BigDrum, Writer = Textures.Drums, From = 0.42f, Level = 0.5f, Rise = 0.5f, Fader = -2.5f, Name = "war" },
                new Part { Section = Section.Horns, Writer = Textures.Melody, Level = 0.5f, Rise = 0.48f, Fader = 4.5f },
                new Part { Section = Section.Trombones, Writer = Textures.Stabs, From = 0.5f, Level = 0.5f, Rise = 0.48f, Fader = -4.5f, Octave = -1 },
                new Part { Section = Section.Choir, Writer = Textures.Pad, From = 0.56f, Level = 0.36f, Rise = 0.56f, Fader = -4.5f },
                new Part { Section = Section.Cymbal, Writer = Textures.Swell, From = 0.48f, Level = 0.36f, Rise = 0.56f, Fader = -1f },
                new Part { Section = Section.Trumpets, Writer = Textures.Melody, From = 0.6f, Level = 0.42f, Rise = 0.5f, Fader = 4.5f, Octave = 1 },
                new Part { Section = Section.Bells, Writer = Textures.Toll, From = 0.7f, Level = 0.36f, Rise = 0.4f },
            },
        };

        public static readonly Style Sacred = new()
        {
            Mood = MusicMood.Sacred,
            Tide = 0.4f,
            Meters = new[] { Meter.Three, Meter.Four },
            Tempo = 60f,
            TempoSpread = 5f,
            Modes = new[] { Mode.Lydian, Mode.Ionian },
            Melodic = MelodicStyle.Lyrical,
            Colour = 0.35f,
            Rests = true,
            RestBars = 10,
            Parts = new[]
            {
                new Part { Section = Section.Choir, Writer = Textures.Pad, Level = 0.26f, Rise = 0.3f, Fader = -2f },
                new Part { Section = Section.Harp, Writer = Textures.Arpeggio, From = 0.15f, Level = 0.3f, Rise = 0.2f, Name = "slow" },
                new Part { Section = Section.Flute, Writer = Textures.Melody, From = 0.3f, Level = 0.32f, Rise = 0.2f, Fader = 2.5f },
                new Part { Section = Section.Celesta, Writer = Textures.Toll, From = 0.2f, Level = 0.3f, Rise = 0.2f, Octave = 1 },
                new Part { Section = Section.Violins, Writer = Textures.Pad, From = 0.6f, Level = 0.16f, Rise = 0.2f, Fader = -2f, Octave = 1 },
            },
        };

        public static readonly Style Haunted = new()
        {
            Mood = MusicMood.Haunted,
            Tempo = 54f,
            TempoSpread = 5f,
            Modes = new[] { Mode.Phrygian, Mode.Aeolian },
            Melodic = MelodicStyle.Lyrical,
            Colour = 0.25f,
            Rests = true,
            RestBars = 9,
            Parts = new[]
            {
                new Part { Section = Section.Basses, Writer = Textures.Pedal, Level = 0.16f, Rise = 0.2f, Octave = -1, Name = "still" },
                new Part { Section = Section.Celesta, Writer = Textures.Melody, Level = 0.42f, Rise = 0.2f, Fader = 2.5f, Octave = 1 },
                new Part { Section = Section.Choir, Writer = Textures.Pad, From = 0.15f, Level = 0.14f, Rise = 0.26f, Fader = -2f, Octave = -1 },
                new Part { Section = Section.Violins, Writer = Textures.Pad, From = 0.5f, Level = 0.1f, Rise = 0.18f, Fader = -2f, Octave = 2, Name = "tremolo" },
                new Part { Section = Section.Bells, Writer = Textures.Toll, From = 0.65f, Level = 0.2f, Rise = 0.2f },
            },
        };

        public static Style For(MusicMood mood) => mood switch
        {
            MusicMood.Town => Town,
            MusicMood.Tension => Tension,
            MusicMood.Battle => Battle,
            MusicMood.Boss => Boss,
            MusicMood.Sacred => Sacred,
            MusicMood.Haunted => Haunted,
            _ => Wander,
        };
    }
}
