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
        public int Holds;           // One more than the index of the place it holds, and nothing for anything that holds none: all a client needs to mark whoever the land asks to be beaten.

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

        // Whether it's getting anywhere. Something that stops dead against what's in its way finds a path round it, and if that doesn't help either, steps aside.
        public Vector2 Last;        // Where it was a tick ago.
        public float Asked;         // How fast it meant to walk then.
        public float Stalled;       // Seconds it's been going nowhere.
        public double DetourUntil;  // Until then it keeps to a path, whatever the straight line looks like.
        public double SideUntil;
        public float Side;          // Which way it steps aside: 1 or -1, and the other way next time.
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

    /// <summary>
    /// A chest, which stays where it stood once it's opened, with its lid thrown back and nothing left to use. The server swaps the closed one for a body and a lid on its hinge, and a client raises the
    /// lid, as a ghost's turn is sent as a yaw alone: swinging it up if it's opening, so the moment it's opened needs nothing sent but the two of them.
    /// </summary>
    public static class ChestLook
    {
        public const string Closed = "chest";
        public const string Open = "chest:open";
        public const string Lid = "chest:lid";                  // Thrown back already: a chest opened before anyone here came.
        public const string Opening = "chest:lid:opening";      // Comes up as it's seen.
        public const float Swing = 0.55f;                       // Seconds for the lid to come up, before anything comes out.
        public const float Thrown = 1.9f;                       // Radians it goes back from shut, past upright, to rest.
        public static readonly Vector3 Hinge = new Vector3(0f, 1.2f, -0.66f);  // Along the back of the top, in the model's own space, where the lid's own model starts.

        /// <summary>How far up the lid is a moment after the chest was opened: thrown, quickly, a little past where it rests, and back.</summary>
        public static float AngleAt(float seconds)
        {
            float t = Math.Clamp(seconds / Swing, 0f, 1f);
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            return Thrown * eased + 0.18f * MathF.Sin(MathF.PI * t) * t;
        }

        /// <summary>A lid on a chest turned <paramref name="yaw"/>, raised by <paramref name="angle"/> about its hinge.</summary>
        public static Quaternion Raised(Quaternion yaw, float angle) => yaw * Quaternion.FromAxisAngle(Vector3.UnitX, -angle);
    }

    /// <summary>
    /// The old roads: a land's waygates are dark until someone comes near one, when it's lit, and a lit one can be travelled to from any other, or from anywhere by calling them, which is standing still and
    /// unhurt for <see cref="Rulebook.CallRoads"/>. Both ends watch a call by the same rule, so what a client shows of one is what the server does.
    /// </summary>
    public static class Roads
    {
        public const string Lit = "waygate:lit";    // What stands in a gate once it's lit, with its light, as the gate itself is the world's and the same for everyone.

        /// <summary>
        /// Whether a call that began at <paramref name="from"/> has been broken by moving, by doing anything, or by being hurt: losing a share of Life from <paramref name="health"/>, the most they've had
        /// since it began, as a blow does and the little a prediction is put right by doesn't.
        /// </summary>
        public static bool Broken(in HeroSimState hero, Vector2 from, float health, float maxHealth) =>
            (hero.Position - from).LengthSquared > Rulebook.CallStill * Rulebook.CallStill || hero.Health < health - maxHealth * Rulebook.CallHurt || hero.BusyFor > 0f;
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

        /// <summary>What it's thought of as, for what it runs into: a little across.</summary>
        public const float Girth = 0.3f;

        /// <summary>Whether the world stops what's flying here: the ground, or something solid that stands taller than it's flying. The server decides by this and a client shows by it.</summary>
        public static bool Stopped(Dino_Engine.World.WorldData world, Vector3 position)
        {
            float above = position.Y - world.Height.Sample(position.Xz);
            return above < 0f || world.Placement.BlocksFlight(position.Xz, Girth, above);
        }
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
