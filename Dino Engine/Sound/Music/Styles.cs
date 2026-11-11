using Dino_Engine.Sound.Dsp;
using Dino_Engine.World;

namespace Dino_Engine.Sound.Music
{
    /// <summary>What the music is for, at this moment. A land has one piece of its own for as long as the hero is in it, and only a tyrant's fight changes it.</summary>
    public enum MusicMood
    {
        Silence,
        Land,       // Out in a land: its own piece, the same whatever's happening.
        Haven,      // Home.
        Boss,       // The tyrant of a land's keep.
        Arena,      // One of the great arenas, every tenth level.
    }

    /// <summary>A moment that gets its own few bars, then hands back to whatever was going on.</summary>
    public enum MusicCue
    {
        None,
        Victory,
        Defeat,
    }

    public static class Moods
    {
        /// <summary>Whether a mood cuts in on the next beat, as a fight does, rather than waiting for the tune to reach somewhere it can stop.</summary>
        public static bool IsUrgent(MusicMood mood) => mood is MusicMood.Boss or MusicMood.Arena;
    }

    /// <summary>
    /// Where the music is being played, as far as music cares. Everything about a land's piece is drawn from its seed and leans on these: a grim land is likelier to get a dark
    /// piece, an open one a broad one, but any land can have any kind, which is what keeps one from sounding like the last.
    /// </summary>
    public sealed class MusicPlace
    {
        public ulong Seed;
        public bool Haven;
        public bool Arena;              // A land that's one great arena, every tenth level.
        public ArenaKind ArenaKind;
        public float Dark;              // 0 a bright, gentle land to 1 a grim one.
        public float Cold;              // Snow and ice.
        public float Open;              // 0 close woods to 1 open plain and shore.
        public int Level;

        public static MusicPlace Home => new() { Seed = HavenSeed, Haven = true, Dark = 0.15f, Open = 0.6f };

        /// <summary>The Haven's theme is always the same, as a home's should be: it's the one tune a player hears often enough to know.</summary>
        public const ulong HavenSeed = 1610;
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
        public bool[]? Steps;           // A rhythm on a grid, for drums and driving figures.
        public float StepBeats = 0.5f;  // How long a step of it is.
        public float LoopBeats;         // For a loop that goes round on its own, out of step with the bar: how long it is.
        public float[] LoopTimes = Array.Empty<float>();
        public int[] LoopDegrees = Array.Empty<int>();
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
    /// How one piece of music goes. A land's is drawn for it by <see cref="Arranger"/>, so no two lands are alike: a table of who plays what from which intensity, as game scores are
    /// layered, but of players and not of recordings, so they follow the harmony wherever it goes.
    /// </summary>
    public sealed class Style
    {
        public MusicMood Mood;
        public string Name = "";            // The kind of piece, for tools.
        public Meter[] Meters = { Meter.Four };
        public float Tempo = 90f;           // Beats a minute.
        public float TempoSpread = 8f;      // Each world's own.
        public float TempoLift = 0.06f;     // Share faster at full intensity.
        public Mode[] Modes = { Mode.Aeolian };
        public MelodicStyle Melodic = MelodicStyle.Flowing;
        public HarmonicLanguage Language = HarmonicLanguage.Modal;
        public int TonicShift;              // Semitones from the world's key. A boss a step up feels higher stakes.
        public float Colour = 0.3f;         // How many chords get something added.
        public bool Urgent;                 // Cuts in at once, not at the end of a phrase.
        public bool Prompt;                 // Comes in at the next bar, for what can't wait for a phrase but needn't interrupt a beat.
        public bool Rests;                  // Goes quiet between phrases, as exploring music should.
        public int RestBars = 12;
        public float Tide = 0.6f;           // How far the form rises and falls by itself, around what the game asks for.
        public Part[] Parts = Array.Empty<Part>();
        public Section[] Leads = Array.Empty<Section>();    // Who can carry the tune, passed round as the piece goes on.
        public Staging Staging = new() { Room = Orchestra.ConcertHall };
    }
}
