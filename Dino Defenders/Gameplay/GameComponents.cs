using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using OpenTK.Mathematics;

namespace Dino_Defenders.Gameplay
{
    public enum EnemyRank
    {
        Normal,
        Elite,
        Boss,
    }

    public enum EnemyState
    {
        Idle,       // Standing watch, or wandering near home.
        Chasing,
        WindingUp,  // The swing has started and can still be stepped away from.
        Recovering,
        Returning,  // Lost interest; walking home.
        Dead,       // Toppling over, and soon gone.
    }

    [Replicated]
    public struct HealthComponent : IComponent
    {
        [NetStep(0.25f), NetSnap] public float Current;
        [NetStep(0.25f), NetSnap] public float Max;

        public HealthComponent(float max)
        {
            Current = max;
            Max = max;
        }
    }

    /// <summary>What anyone can see of an enemy. What it means to do next is in <see cref="EnemyBrainComponent"/>, on the server alone.</summary>
    [Replicated]
    public struct EnemyComponent : IComponent
    {
        public string Name;
        public EnemyRank Rank;
        public int Level;
        public EnemyState State;
        public bool Friendly;       // Someone's minion: the same creature, on the other side.

        /// <summary>How hard it was thrown as it died, in metres a second. A blow that was far too much for it sends it flying.</summary>
        [NetStep(1f / 8f), NetSnap] public Vector3 Thrown;

        // How long it's been down, on the client.
        [NotReplicated] public float DeadFor;

        // What its sounds have got to, on the client.
        [NotReplicated] public EnemyState Heard;
    }

    public struct EnemyBrainComponent : IComponent
    {
        public int SiteIndex;
        public int SpawnIndex;
        public Vector2 Home;
        public float StateTime;
        public float Cooldown;
        public float AggroRadius;
        public float LeashRadius;
        public bool KeepsAway;
        public int Skill;           // Which of its skills it's winding up.
        public float WindUp;

        // What it has committed to. All of this is fixed when the wind-up starts: what's on the ground is what lands, and walking out of it is what it's there for.
        public Vector2 AimFrom;
        public Vector2 AimWay;
        public float AimReach;
        public Entity Mark;
        public int Experience;
        public Vector2 Wander;
        public Vector2 Apart;       // Which way it's being crowded, as last worked out.
        public List<Vector2>? Path;
        public int PathStep;
        public double NextPathTime;
    }

    /// <summary>A hero, whoever's it is.</summary>
    [Replicated]
    public struct HeroComponent : IComponent
    {
        public string Name;
        public string Title;        // What their tree makes them. There are few of these, so a table that only grows is fine.
        public int Player;
        public int Level;

        /// <summary>How hard they were thrown as they fell, in metres a second.</summary>
        [NetStep(1f / 8f), NetSnap] public Vector3 Thrown;

        [NotReplicated] public bool HeardDown;
    }

    public enum VisualKind
    {
        Character,
        Prop,
    }

    /// <summary>
    /// What something looks like, as a name for the client to find a model by. It is all a server needs to say for a new kind of
    /// thing to appear: <see cref="Client.GhostPresenter"/> builds the rest.
    /// </summary>
    [Replicated]
    public struct VisualComponent : IComponent
    {
        public string Model;
        public VisualKind Kind;

        public VisualComponent(string model, VisualKind kind)
        {
            Model = model;
            Kind = kind;
        }
    }

    public struct VillagerComponent : IComponent
    {
        public int SiteIndex;
        public Vector2 Home;
        public Vector2 Goal;
        public float Wait;
        public float WanderRadius;
        public double NextSpeech;
        public bool Stationary;
    }

    public enum InteractKind
    {
        Shrine,
        Chest,
        Waygate,
        Merchant,
        Elder,
    }

    [Replicated]
    public struct InteractableComponent : IComponent
    {
        public InteractKind Kind;
        public int SiteIndex;
        public int SpawnIndex;
        public float Radius;
        public string Prompt;
    }

    /// <summary>
    /// Loot, on the ground or on its way there. Everything a client needs to show it falling is here and sent once, so a drop is worked out from these and can't be missed the
    /// way an event can: someone who walks up later just finds it lying there. What a find is comes by <see cref="Net.Message.GroundItem"/>, as names can't go in a component.
    /// </summary>
    [Replicated]
    public struct PickupComponent : IComponent
    {
        public int Find;            // Which find this is, to ask for it by. Nobody asks for gold, so it has none.
        public LootGrade Grade;
        public ItemSlot Slot;
        public uint Born;           // The tick it leaves what it came out of.
        public Vector3 From;

        // What it looks like: see ItemLook. A seed is 64 bits and a lane is 32.
        public uint SeedLow;
        public uint SeedHigh;
        public uint Traits;

        [NotReplicated] public bool Dressed;
        [NotReplicated] public float Fit;
        [NotReplicated] public int Gold;
        [NotReplicated] public Vector3 Rest;

        // How far through falling it is, on the client.
        [NotReplicated] public bool Launched;
        [NotReplicated] public bool Landed;
        [NotReplicated] public float LandedFor;
        [NotReplicated] public float NextMote;

        public readonly ulong Seed => SeedLow | (ulong)SeedHigh << 32;
    }

    /// <summary>How far through its sounds an arrow is, on the client.</summary>
    public enum ArrowHeard : byte
    {
        Nothing,
        Loosed,
        Passing,
        Landed,
    }

    /// <summary>
    /// Flies in a straight line from a known start, so clients can work out where it is for themselves. Sent once when it is loosed
    /// and never again, however long it flies: a screen full of these costs almost nothing.
    /// </summary>
    [Replicated(Replaces = typeof(Dino_Engine.ECS.Components.PositionComponent))]
    public struct BallisticComponent : IComponent
    {
        public Vector3 Origin;
        public Vector3 Velocity;
        public uint StartTick;
        public bool FromHeroes;     // Friendly fire isn't a thing, so these never need to vanish at a hero's feet.
        [NotReplicated] public ArrowHeard Heard;

        public readonly Vector3 PositionAt(double tick) => Origin + Velocity * (float)((tick - StartTick) * NetTime.TickSecondsPrecise);
    }

    /// <summary>What ailments someone is under, for those watching. The numbers behind them stay on the server.</summary>
    [Replicated]
    public struct AfflictedComponent : IComponent
    {
        public uint Flags;

        [NotReplicated] public float NextSpark;

        public readonly bool Has(Condition condition) => ((Condition)Flags & condition) != 0;
    }

    /// <summary>A patch of ground that's doing something. Sent once: everything else about it can be worked out from that.</summary>
    [Replicated]
    public struct ZoneComponent : IComponent
    {
        public int Skill;
        public bool Hostile;
        [NetStep(0.25f)] public float Radius;
        [NetStep(0.25f)] public float Life;

        [NotReplicated] public float Age;
        [NotReplicated] public float NextMote;
    }
}
