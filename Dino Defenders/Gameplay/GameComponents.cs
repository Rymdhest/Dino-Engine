using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using OpenTK.Mathematics;

namespace Dino_Defenders.Gameplay
{
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
        public Rarity Rarity;
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
        public int Book;            // Which of its books it's winding up.
        public int Abilities;       // How many of its books, from the first, its traits gave it.
        public int WindUpTicks;     // Until it lets go. A mark has said when, so nothing that happens to it after that changes this.

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

        /// <summary>Made to watch a fight from an arena's stands, looking towards where it is.</summary>
        public bool Crowd;
        public Vector2 Watch;
    }

    public enum InteractKind
    {
        Shrine,
        Chest,
        Waygate,
        Merchant,
        Elder,
        Stash,          // Where what's put away is got at.
        MapTable,       // Where the next land is chosen, in the Haven.
        Portal,         // The way open to a land, in the Haven's great waygate.
        BetaVendor,     // Gives anything away, for testing the game, in the Haven.
    }

    /// <summary>The way out of the Haven, open to a land: what's seen in the great waygate's arch. Its colour is the land's first biome, so that what's ahead shows in it.</summary>
    [Replicated]
    public struct PortalComponent : IComponent
    {
        public int Biome;
    }

    /// <summary>Something that stands to be hit, to say how hard: a training dummy, in the Haven.</summary>
    [Replicated]
    public struct DummyComponent : IComponent
    {
        public int Group;   // Which of the training ground's groups it belongs to, so that what's dealt to all of them can be told together.
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
        public Rarity Rarity;
        public ItemSlot Slot;
        public uint Born;           // The tick it leaves what it came out of.
        public Vector3 From;

        // What it looks like: see ItemLook. A seed is 64 bits and a lane is 32.
        public uint SeedLow;
        public uint SeedHigh;
        public uint Traits;
        public bool Laid;           // A hero put it down, so it's nothing to get excited about.

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

    /// <summary>What a hero has thrown, which is the very thing in their hands, and sent the way anything lying on the ground is: see <see cref="PickupComponent"/>.</summary>
    [Replicated]
    public struct ThrownComponent : IComponent
    {
        public uint SeedLow;
        public uint SeedHigh;
        public uint Traits;

        public readonly ulong Seed => SeedLow | (ulong)SeedHigh << 32;
    }

    /// <summary>A patch of ground that's doing something. Sent once: everything else about it can be worked out from that.</summary>
    [Replicated]
    public struct ZoneComponent : IComponent
    {
        public int Sign;
        public bool Hostile;
        public ShapeKind Kind;
        [NetStep(1f / 128f), NetSnap] public Vector2 Facing;
        [NetStep(0.25f)] public float Length;
        [NetStep(1f / 128f)] public float Width;
        [NetStep(0.25f)] public float Life;

        [NotReplicated] public float Age;
        [NotReplicated] public float NextMote;

        public readonly GroundShape ShapeAt(Vector2 origin) => new GroundShape(Kind, origin, Facing, Length, Width);
    }
}
