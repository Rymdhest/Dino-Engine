using Dino_Defenders.Progression;
using Dino_Engine.ECS.ECS_Architecture;
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

    public struct HealthComponent : IComponent
    {
        public float Current;
        public float Max;

        public HealthComponent(float max)
        {
            Current = max;
            Max = max;
        }
    }

    public struct EnemyComponent : IComponent
    {
        public string Name;
        public EnemyRank Rank;
        public int Level;
        public int SiteIndex;
        public int SpawnIndex;
        public Vector2 Home;
        public EnemyState State;
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
        public float NextPathTime;
        public Vector3 DeathScale;
        public Quaternion DeathRotation;
        public Vector3 FallAxis;
    }

    /// <summary>For as long as this runs, nothing can touch the hero: a dodge's window of safety.</summary>
    public struct HeroInvulnerableComponent : IComponent
    {
        public float Time;
    }

    public struct VillagerComponent : IComponent
    {
        public int SiteIndex;
        public Vector2 Home;
        public Vector2 Goal;
        public float Wait;
        public float WanderRadius;
        public float NextSpeech;
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

    public struct InteractableComponent : IComponent
    {
        public InteractKind Kind;
        public int SiteIndex;
        public int SpawnIndex;
        public float Radius;
        public string Prompt;
    }

    public struct PickupComponent : IComponent
    {
        public Item? Item;
        public int Gold;
        public float Age;
        public Vector3 Rest;
    }

    public struct ProjectileComponent : IComponent
    {
        public float Damage;
        public float Life;
    }
}
