using Dino_Defenders.Stats;
using Dino_Engine.Networking;
using OpenTK.Mathematics;

namespace Dino_Defenders.Gameplay
{
    public enum SparkKind
    {
        Blood,
        Blade,
        Whirl,
        Shrine,
    }

    /// <summary>What a blow landed on, which is what it sounds like.</summary>
    public enum HitBody
    {
        Flesh,
        Bone,
        Armour,
        Beast,
    }

    /// <summary>What became of a blow that didn't land as one.</summary>
    public enum HitNote : byte
    {
        None,
        Evaded,
        Blocked,
        Dodged,
    }

    /// <summary>A number off a blow.</summary>
    [NetEvent]
    public struct DamageEvent
    {
        public Vector3 Position;
        [NetStep(1f)] public float Amount;
        public bool Critical;
        public bool ToHero;
        public HitBody Body;
        public DamageType Type;
        public HitNote Note;
        public uint Target;                     // Whose body it landed on, by ghost.
        [NetStep(1f / 16f)] public Vector2 Way; // Which way it was going.
        [NetStep(1f / 16f)] public float Weight;// How much of them it took, as a share of all they have.
    }

    public enum SkillPhase : byte
    {
        Burst,      // It's gone off here.
    }

    /// <summary>A skill going off somewhere, for everyone to see. What it looks like comes from the skill.</summary>
    [NetEvent]
    public struct SkillEvent
    {
        public Vector3 Position;
        public int Skill;
        public SkillPhase Phase;
        [NetStep(0.25f)] public float Radius;
        [NetStep(0.1f)] public float Time;
    }

    /// <summary>Something crossing from one place to another all at once: a bolt leaping, a lance of ice.</summary>
    [NetEvent]
    public struct LinkEvent
    {
        public Vector3 From;
        public Vector3 To;
        public DamageType Look;
        public bool Wide;
    }

    /// <summary>What two things can do when they meet on someone, that neither does alone.</summary>
    public enum Reaction : byte
    {
        Scald,      // Fire on the chilled.
        Crackle,    // Cold on the burning.
        Flashfire,  // Fire on venom.
        Shatter,    // A blow on the frozen.
        Conduction, // Lightning in water.
    }

    [NetEvent]
    public struct ReactionEvent
    {
        public Vector3 Position;
        public Reaction Kind;
        [NetStep(0.25f)] public float Radius;
    }

    [NetEvent]
    public struct SparkEvent
    {
        public Vector3 Position;
        public SparkKind Kind;
        public int Count;
        [NetStep(0.25f)] public float Speed;
    }

    /// <summary>Words that float up from something: a villager's, or what a pickup was worth.</summary>
    [NetEvent]
    public struct TextEvent
    {
        public Vector3 Position;
        public string Text;
        [NetStep(1f / 32f)] public Vector3 Colour;
        [NetStep(0.5f)] public float Size;
        [NetStep(0.1f)] public float Life;
        public bool Speech;     // Someone saying it, and not just a number going up.
    }

    public enum SoundCue
    {
        Coins,
        Chest,
        Flask,
        ShrineKindled,
        ShrineRest,
        LevelUp,
        Objective,
        Liberated,
        Discovered,
    }

    /// <summary>Something that happened that has a sound of its own and no other way of being known. Value is whatever the cue needs: how much gold, what rarity.</summary>
    [NetEvent]
    public struct SoundEvent
    {
        public Vector3 Position;
        public SoundCue Cue;
        public int Value;
    }

    /// <summary>What a body can do that isn't a skill.</summary>
    public enum Flourish : byte
    {
        Drink,
        Use,
    }

    /// <summary>Someone doing something that changes nothing about how they stand in a fight. It's only there to be seen, so it can go missing.</summary>
    [NetEvent]
    public struct FlourishEvent
    {
        public uint Who;
        public Flourish Kind;
    }

    /// <summary>A hero's ability going off, for everyone but its owner, who saw it the moment they pressed.</summary>
    [NetEvent]
    public struct HeroMoveEvent
    {
        public Vector3 Position;
        [NetStep(1f / 64f)] public Vector2 Aim;
        public HeroStepEvents Move;
        public int Skill;
    }
}
