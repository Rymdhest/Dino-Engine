using Dino_Defenders.Progression;
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
        [NetStep(1f / 64f), NetSnap] public Vector3 FallAxis;

        // How its fall is drawn, on the client.
        [NotReplicated] public float DeadFor;
        [NotReplicated] public Vector3 DeathScale;
        [NotReplicated] public Quaternion DeathRotation;
    }

    public struct EnemyBrainComponent : IComponent
    {
        public int SiteIndex;
        public int SpawnIndex;
        public Vector2 Home;
        public float StateTime;
        public float Cooldown;
        public float Damage;
        public float Armour;
        public float MoveSpeed;
        public float AggroRadius;
        public float LeashRadius;
        public float AttackRange;
        public float WindUp;
        public bool Ranged;
        public int Experience;
        public Vector2 Wander;
        public List<Vector2>? Path;
        public int PathStep;
        public double NextPathTime;
    }

    /// <summary>A hero, whoever's it is.</summary>
    [Replicated]
    public struct HeroComponent : IComponent
    {
        public string Name;
        public int Player;
        public int Level;

        // How its fall is drawn, on the client.
        [NotReplicated] public float DownFor;
        [NotReplicated] public Quaternion FellFrom;
    }

    /// <summary>A swing, for those watching. Each new one changes the serial, so that one lost update can't lose the blow.</summary>
    [Replicated]
    public struct SwingComponent : IComponent
    {
        public byte Serial;
        [NetStep(1f / 32f), NetSnap] public float Speed;
        [NotReplicated] public byte Played;
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

    /// <summary>Loot on the ground. Who gets it is the server's business; what shows is whether it's gold or a find.</summary>
    [Replicated]
    public struct PickupComponent : IComponent
    {
        [NotReplicated] public Item? Item;
        [NotReplicated] public int Gold;
        [NotReplicated] public float Age;
        [NotReplicated] public Vector3 Rest;
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

        public readonly Vector3 PositionAt(double tick) => Origin + Velocity * (float)((tick - StartTick) * NetTime.TickSecondsPrecise);
    }

    public struct ProjectileComponent : IComponent
    {
        public float Damage;
        public float Life;
    }
}
