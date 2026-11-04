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

    /// <summary>A number off a blow.</summary>
    [NetEvent]
    public struct DamageEvent
    {
        public Vector3 Position;
        [NetStep(1f)] public float Amount;
        public bool Critical;
        public bool ToHero;
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
    }

    /// <summary>A hero's ability going off, for everyone but its owner, who saw it the moment they pressed.</summary>
    [NetEvent]
    public struct HeroMoveEvent
    {
        public Vector3 Position;
        [NetStep(1f / 64f)] public Vector2 Aim;
        public HeroStepEvents Move;
    }
}
