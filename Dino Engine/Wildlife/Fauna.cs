using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Wildlife;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Wildlife
{
    /// <summary>One thing to be drawn many times: its mesh, made when it's first wanted, and how many bones each one takes. The renderer keeps what it needs on it.</summary>
    public sealed class WildlifeShape
    {
        public readonly string Id;
        public readonly int Bones;
        public readonly Func<Mesh> Build;

        /// <summary>Whatever the renderer made of it.</summary>
        public object? Drawn;

        public WildlifeShape(string id, int bones, Func<Mesh> build)
        {
            Id = id;
            Bones = bones;
            Build = build;
        }
    }

    /// <summary>
    /// A kind of animal. This is all data: where it lives, how many there are, and how close anyone can come. A new animal of a shape there already is, a blue finch or a white hare, is a new entry in <see cref="Fauna"/>
    /// and a line in a biome, and no code at all. Every number about how shy something is goes by someone running at it: someone standing still is half as frightening.
    /// </summary>
    public abstract class Creature
    {
        public readonly string Id;
        public readonly WildlifeShape Shape;

        public Habitat Lives;
        public float Density = 0.3f;        // The chance that a place it could live in has some in it.
        public IntRange Group = new IntRange(1, 1);
        public float Shy = 11f;             // Metres at which someone running at it makes it go. Real flight distances, at the scale of this world.
        public FloatRange Size = new FloatRange(0.92f, 1.08f);
        public float Variation = 0.1f;      // How far one strays in colour from the next.
        public bool Nocturnal;
        public bool AllHours;               // Goes on as it was when night falls, where birds would go to sleep.
        public float Rain = 0.15f;          // How many are still about in a downpour, as a share.
        public float BackAfter = 70f;       // Seconds before a place that's been frightened has any again.

        protected Creature(string id, int bones, Func<Mesh> build)
        {
            Id = id;
            Shape = new WildlifeShape(id, bones, build);
        }

        internal abstract Party Settle(WildlifeSim sim, Resident home);
    }

    public sealed class RabbitKind : Creature
    {
        public readonly RabbitLook Look;
        public readonly Skeleton Bones = RabbitBody.NewSkeleton();
        public float BoltSpeed = 1f;        // A hare goes faster and further than a rabbit does.

        public RabbitKind(string id, RabbitLook look) : base(id, RabbitBody.Count, () => RabbitBody.Build(look))
        {
            Look = look;
            Lives = Habitat.Meadow | Habitat.Edge;
            Shy = 12f;
            AllHours = true;
            Group = new IntRange(1, 3);
            Size = new FloatRange(1.05f, 1.28f);
        }

        internal override Party Settle(WildlifeSim sim, Resident home) => new Warren(sim, this, home);
    }

    public sealed class CrabKind : Creature
    {
        public readonly CrabLook Look;
        public readonly Skeleton Bones = CrabBody.NewSkeleton();

        public CrabKind(string id, CrabLook look) : base(id, CrabBody.Count, () => CrabBody.Build(look))
        {
            Look = look;
            Lives = Habitat.Beach;
            Shy = 8f;
            Group = new IntRange(3, 7);
            Size = new FloatRange(0.85f, 1.2f);
            AllHours = true;
            Density = 0.6f;
            Rain = 1f;
        }

        internal override Party Settle(WildlifeSim sim, Resident home) => new CrabColony(sim, this, home);
    }

    /// <summary>A bird, and how it goes about being one.</summary>
    public sealed class BirdKind : Creature
    {
        public readonly BirdLook Look;
        public readonly Skeleton Bones;

        public float Cruise = 13f;          // Metres a second.
        public float Wingbeats = 8f;        // Hz. Real small birds go at 15 to 25, which at 60 frames a second would strobe, so a blur is drawn as what can be seen.
        public float Glides;                // Share of the time its wings are still.
        public bool Bounds;                 // Shuts its wings between bursts, as a finch does, and dips.
        public float Height = 4.5f;         // Metres over the ground it travels at. The camera's only about 30 m up, so a bird high in the sky is one the size of a person, across the middle of a fight.
        public float Hold = 0.05f;          // Radians its wings are held up.
        public bool Walks;                  // Walks, with its head going, and doesn't hop. What walks stays out of long grass; what hops sits up on top of it, on the seed heads.
        public bool Roosts;                 // Sits up in trees, unseen until something sets it off.
        public float Turning = 2.6f;        // Radians a second.
        public bool Calls;                  // Cries out when it goes.
        public float Scatter = 1f;          // How wide a flock feeds.

        public BirdKind(string id, BirdLook look) : base(id, BirdBody.Count, () => BirdBody.Build(look))
        {
            Look = look;
            Bones = BirdBody.NewSkeleton(look);
            Lives = Habitat.Meadow | Habitat.Edge;
            Group = new IntRange(4, 11);
        }

        internal override Party Settle(WildlifeSim sim, Resident home) => new Flock(sim, this, home);
    }

    public sealed class ButterflyKind : Creature
    {
        public readonly ButterflyLook Look;
        public readonly Skeleton Bones = ButterflyBody.NewSkeleton();

        public ButterflyKind(string id, ButterflyLook look) : base(id, ButterflyBody.Count, () => ButterflyBody.Build(look))
        {
            Look = look;
            Lives = Habitat.Meadow | Habitat.Edge;
            Group = new IntRange(3, 7);
            Shy = 3.5f;
            Density = 0.5f;
            Rain = 0f;
            Variation = 0.05f;
            BackAfter = 20f;
        }

        internal override Party Settle(WildlifeSim sim, Resident home) => new Swarm(sim, this, home);
    }

    public sealed class DragonflyKind : Creature
    {
        public readonly DragonflyLook Look;
        public readonly Skeleton Bones;

        public DragonflyKind(string id, DragonflyLook look) : base(id, DragonflyBody.Count, () => DragonflyBody.Build(look))
        {
            Look = look;
            Bones = DragonflyBody.NewSkeleton(look);
            Lives = Habitat.Bank;
            Group = new IntRange(2, 4);
            Shy = 3f;
            Density = 0.7f;
            Rain = 0f;
            BackAfter = 15f;
        }

        internal override Party Settle(WildlifeSim sim, Resident home) => new Patrol(sim, this, home);
    }

    public sealed class FrogKind : Creature
    {
        public readonly FrogLook Look;
        public readonly Skeleton Bones = FrogBody.NewSkeleton();

        public FrogKind(string id, FrogLook look) : base(id, FrogBody.Count, () => FrogBody.Build(look))
        {
            Look = look;
            Lives = Habitat.Bank;
            Group = new IntRange(2, 5);
            Shy = 8f;
            Density = 0.6f;
            Rain = 1f;
            AllHours = true;
            Size = new FloatRange(1.05f, 1.35f);
            BackAfter = 45f;
        }

        internal override Party Settle(WildlifeSim sim, Resident home) => new Chorus(sim, this, home);
    }

    /// <summary>What's seen of a fish is the moment it leaves the water, so it isn't anywhere until then.</summary>
    public sealed class FishKind : Creature
    {
        public readonly FishLook Look;
        public readonly Skeleton Bones;

        public FishKind(string id, FishLook look) : base(id, FishBody.Count, () => FishBody.Build(look))
        {
            Look = look;
            Bones = FishBody.NewSkeleton(look);
            Lives = Habitat.Sea | Habitat.Pond;
            Density = 0.5f;
            Rain = 1f;
            AllHours = true;
            Size = new FloatRange(0.8f, 1.2f);
        }

        internal override Party Settle(WildlifeSim sim, Resident home) => new Shoal(sim, this, home);
    }

    /// <summary>A bat is a bird as far as its bones go, and nothing like one in how it flies.</summary>
    public sealed class BatKind : Creature
    {
        public readonly BirdLook Look;
        public readonly Skeleton Bones;

        public BatKind(string id, BirdLook look) : base(id, BirdBody.Count, () => BirdBody.Build(look))
        {
            Look = look;
            Bones = BirdBody.NewSkeleton(look);
            Lives = Habitat.Edge | Habitat.Wood | Habitat.Yard | Habitat.Meadow | Habitat.Bank;
            Nocturnal = true;
            Group = new IntRange(3, 6);
            Density = 0.5f;
            Rain = 0f;
        }

        internal override Party Settle(WildlifeSim sim, Resident home) => new Colony(sim, this, home);
    }

    /// <summary>
    /// Everything that lives anywhere, and the lists biomes draw from. Sizes are the game's: people here are 2.7 times real, so that's where everything starts, and what's small gets a little more than that or
    /// there's nothing of it to see from the camera. Colours are albedos under a sun of about 13, so pale is well short of white and anything bright is deep and saturated.
    /// </summary>
    public static class Fauna
    {
        private static Vector3 V(float r, float g, float b) => new Vector3(r, g, b);

        // --- What hops ---
        public static readonly RabbitKind Rabbit = new RabbitKind("rabbit", new RabbitLook()) { Density = 0.42f };

        public static readonly RabbitKind Hare = new RabbitKind("hare", new RabbitLook { Back = V(0.2f, 0.15f, 0.09f), Belly = V(0.4f, 0.33f, 0.23f), EarTip = V(0.015f, 0.012f, 0.01f), Ears = 0.5f })
        { Density = 0.26f, Group = new IntRange(1, 2), Shy = 16f, BoltSpeed = 1.3f, Lives = Habitat.Meadow | Habitat.Barren | Habitat.Field };

        public static readonly RabbitKind SnowHare = new RabbitKind("hare.snow", new RabbitLook { Back = V(0.64f, 0.64f, 0.62f), Belly = V(0.72f, 0.72f, 0.7f), Scut = V(0.76f, 0.76f, 0.74f), EarTip = V(0.015f, 0.012f, 0.012f), Ears = 0.46f })
        { Density = 0.25f, Group = new IntRange(1, 2), Shy = 15f, BoltSpeed = 1.25f, Lives = Habitat.Meadow | Habitat.Barren | Habitat.Edge };

        public static readonly RabbitKind DesertHare = new RabbitKind("hare.desert", new RabbitLook { Back = V(0.3f, 0.21f, 0.12f), Belly = V(0.46f, 0.38f, 0.26f), EarTip = V(0.05f, 0.03f, 0.02f), Ears = 0.58f })
        { Density = 0.14f, Group = new IntRange(1, 2), Shy = 17f, BoltSpeed = 1.35f, Lives = Habitat.Meadow | Habitat.Barren };

        // --- Crabs ---
        public static readonly CrabKind ShoreCrab = new CrabKind("crab.shore", new CrabLook());

        public static readonly CrabKind SandCrab = new CrabKind("crab.sand", new CrabLook { Shell = V(0.42f, 0.33f, 0.2f), Under = V(0.58f, 0.52f, 0.42f), Leg = V(0.4f, 0.32f, 0.2f), Claw = V(0.48f, 0.38f, 0.24f), ClawTip = V(0.1f, 0.06f, 0.04f) })
        { Shy = 10f };

        // --- Birds. Small ones feed in the open and sit in trees; big ones are few. ---
        public static readonly BirdKind Sparrow = new BirdKind("sparrow", new BirdLook())
        { Bounds = true, Roosts = true, Shy = 13f, Density = 0.4f, Lives = Habitat.Meadow | Habitat.Edge | Habitat.Yard | Habitat.Field };

        public static readonly BirdKind Goldfinch = new BirdKind("goldfinch", new BirdLook
        {
            Back = V(0.3f, 0.2f, 0.05f), Belly = V(0.42f, 0.36f, 0.14f), Crown = V(0.34f, 0.03f, 0.02f), Wing = V(0.02f, 0.02f, 0.02f), Tips = V(0.015f, 0.015f, 0.015f), TailColour = V(0.02f, 0.02f, 0.02f), Bars = V(0.55f, 0.42f, 0.02f), Beak = V(0.45f, 0.36f, 0.3f),
        })
        { Bounds = true, Roosts = true, Shy = 12f, Density = 0.3f, Group = new IntRange(3, 8), Lives = Habitat.Meadow | Habitat.Edge };

        public static readonly BirdKind Bunting = new BirdKind("bunting.snow", new BirdLook { Back = V(0.32f, 0.28f, 0.22f), Belly = V(0.68f, 0.68f, 0.66f), Crown = V(0.62f, 0.6f, 0.56f), Wing = V(0.66f, 0.66f, 0.64f), Tips = V(0.02f, 0.02f, 0.02f), TailColour = V(0.05f, 0.05f, 0.05f) })
        { Bounds = true, Shy = 13f, Density = 0.28f, Group = new IntRange(5, 12), Lives = Habitat.Meadow | Habitat.Barren | Habitat.Edge };

        public static readonly BirdKind Pigeon = new BirdKind("pigeon", new BirdLook
        {
            Body = 0.58f, Girth = 0.2f, Neck = 0.13f, Head = 0.105f, Bill = 0.075f, BillDepth = 0.032f, Tail = 0.34f, TailWidth = 0.17f, Arm = 0.36f, Hand = 0.48f, Chord = 0.34f, Point = 0.62f, Legs = 0.13f,
            Back = V(0.25f, 0.27f, 0.31f), Belly = V(0.31f, 0.33f, 0.37f), Crown = V(0.1f, 0.15f, 0.19f), Wing = V(0.34f, 0.36f, 0.4f), Tips = V(0.06f, 0.06f, 0.07f), TailColour = V(0.16f, 0.17f, 0.2f), Bars = V(0.03f, 0.03f, 0.035f), Beak = V(0.05f, 0.05f, 0.05f), Foot = V(0.45f, 0.08f, 0.06f),
        })
        { Walks = true, Wingbeats = 6.5f, Cruise = 15f, Height = 6f, Shy = 11f, Density = 0.55f, Group = new IntRange(4, 10), Lives = Habitat.Yard | Habitat.Field, Turning = 2.2f };

        public static readonly BirdKind Crow = new BirdKind("crow", new BirdLook
        {
            Body = 0.72f, Girth = 0.21f, Neck = 0.14f, Head = 0.125f, Bill = 0.17f, BillDepth = 0.06f, Tail = 0.5f, TailWidth = 0.18f, Arm = 0.5f, Hand = 0.68f, Chord = 0.48f, Point = 0.24f, Fingers = 3, Legs = 0.17f,
            Back = V(0.016f, 0.016f, 0.02f), Belly = V(0.014f, 0.014f, 0.018f), Crown = V(0.014f, 0.014f, 0.018f), Wing = V(0.018f, 0.018f, 0.024f), Tips = V(0.01f, 0.01f, 0.012f), TailColour = V(0.012f, 0.012f, 0.016f), Beak = V(0.01f, 0.01f, 0.01f), Foot = V(0.012f, 0.012f, 0.012f),
        })
        { Walks = true, Wingbeats = 3.6f, Cruise = 13f, Height = 7f, Shy = 24f, Density = 0.2f, Group = new IntRange(2, 5), Lives = Habitat.Field | Habitat.Meadow | Habitat.Barren, Calls = true, Scatter = 2.2f, Turning = 1.8f };

        public static readonly BirdKind Gull = new BirdKind("gull", new BirdLook
        {
            Body = 0.74f, Girth = 0.2f, Neck = 0.13f, Head = 0.125f, Bill = 0.14f, BillDepth = 0.05f, Tail = 0.36f, TailWidth = 0.18f, Arm = 0.64f, Hand = 0.96f, Chord = 0.42f, Point = 0.82f, Legs = 0.18f,
            Back = V(0.3f, 0.33f, 0.37f), Belly = V(0.72f, 0.72f, 0.7f), Crown = V(0.74f, 0.74f, 0.72f), Wing = V(0.36f, 0.39f, 0.43f), Tips = V(0.012f, 0.012f, 0.014f), TailColour = V(0.72f, 0.72f, 0.7f), Beak = V(0.55f, 0.38f, 0.02f), Foot = V(0.5f, 0.3f, 0.16f),
        })
        { Walks = true, Wingbeats = 2.9f, Glides = 0.65f, Cruise = 12f, Height = 7f, Hold = 0.16f, Shy = 18f, Density = 0.6f, Group = new IntRange(3, 7), Lives = Habitat.Beach, Calls = true, Scatter = 2f, Turning = 1.5f, Rain = 0.6f };

        public static readonly BirdKind Egret = new BirdKind("egret", new BirdLook
        {
            Body = 1.05f, Girth = 0.24f, Neck = 0.9f, Head = 0.12f, Bill = 0.4f, BillDepth = 0.05f, Tail = 0.3f, TailWidth = 0.2f, Arm = 0.9f, Hand = 1.15f, Chord = 0.72f, Point = 0.2f, Fingers = 3, Legs = 1.05f,
            Back = V(0.74f, 0.74f, 0.72f), Belly = V(0.76f, 0.76f, 0.74f), Crown = V(0.76f, 0.76f, 0.74f), Wing = V(0.74f, 0.74f, 0.72f), Tips = V(0.7f, 0.7f, 0.68f), TailColour = V(0.74f, 0.74f, 0.72f), Beak = V(0.52f, 0.36f, 0.03f), Foot = V(0.03f, 0.03f, 0.03f),
        })
        { Walks = true, Wingbeats = 2.1f, Glides = 0.2f, Cruise = 10f, Height = 6f, Hold = 0.04f, Shy = 22f, Density = 0.42f, Group = new IntRange(1, 3), Lives = Habitat.Bank, Scatter = 3f, Turning = 1.2f, Rain = 0.8f };

        public static readonly BirdKind Parrot = new BirdKind("parrot", new BirdLook
        {
            Body = 0.5f, Girth = 0.16f, Neck = 0.1f, Head = 0.125f, Bill = 0.09f, BillDepth = 0.08f, Tail = 0.72f, TailWidth = 0.09f, Arm = 0.36f, Hand = 0.5f, Chord = 0.3f, Point = 0.55f, Legs = 0.08f,
            Back = V(0.03f, 0.27f, 0.05f), Belly = V(0.05f, 0.32f, 0.06f), Crown = V(0.42f, 0.03f, 0.02f), Wing = V(0.02f, 0.18f, 0.34f), Tips = V(0.015f, 0.05f, 0.4f), TailColour = V(0.48f, 0.04f, 0.02f), Beak = V(0.42f, 0.36f, 0.24f),
        })
        { Wingbeats = 6f, Cruise = 14f, Height = 6.5f, Roosts = true, Shy = 14f, Density = 0.5f, Group = new IntRange(3, 7), Lives = Habitat.Edge | Habitat.Wood, Calls = true };

        public static readonly BirdKind Vulture = new BirdKind("vulture", new BirdLook
        {
            Body = 1f, Girth = 0.3f, Neck = 0.32f, Head = 0.1f, Bill = 0.15f, BillDepth = 0.06f, Tail = 0.42f, TailWidth = 0.3f, Arm = 1.1f, Hand = 1.4f, Chord = 0.85f, Point = 0.08f, Fingers = 4, Legs = 0.3f,
            Back = V(0.05f, 0.04f, 0.035f), Belly = V(0.06f, 0.05f, 0.04f), Crown = V(0.3f, 0.12f, 0.1f), Wing = V(0.05f, 0.04f, 0.035f), Tips = V(0.02f, 0.016f, 0.014f), TailColour = V(0.04f, 0.03f, 0.03f),
        })
        { Walks = true, Wingbeats = 1.6f, Glides = 0.9f, Cruise = 9f, Height = 9f, Hold = 0.2f, Shy = 26f, Density = 0.12f, Group = new IntRange(1, 3), Lives = Habitat.Barren | Habitat.Crag, Scatter = 3f, Turning = 0.8f, Rain = 0.3f };

        // --- What flutters ---
        public static readonly ButterflyKind Tortoiseshell = new ButterflyKind("butterfly.orange", new ButterflyLook());
        public static readonly ButterflyKind CabbageWhite = new ButterflyKind("butterfly.white", new ButterflyLook { Ground = V(0.68f, 0.68f, 0.62f), Border = V(0.2f, 0.2f, 0.2f), Spot = V(0.08f, 0.08f, 0.08f) });
        public static readonly ButterflyKind Brimstone = new ButterflyKind("butterfly.yellow", new ButterflyLook { Ground = V(0.56f, 0.46f, 0.03f), Border = V(0.36f, 0.3f, 0.04f), Spot = V(0.44f, 0.16f, 0.02f) });
        public static readonly ButterflyKind Morpho = new ButterflyKind("butterfly.blue", new ButterflyLook { Span = 0.52f, Ground = V(0.02f, 0.14f, 0.58f), Border = V(0.012f, 0.012f, 0.03f), Spot = V(0.02f, 0.3f, 0.6f) }) { Lives = Habitat.Edge | Habitat.Wood };
        public static readonly DragonflyKind Darter = new DragonflyKind("dragonfly.blue", new DragonflyLook());
        public static readonly DragonflyKind Skimmer = new DragonflyKind("dragonfly.red", new DragonflyLook { Body = V(0.42f, 0.05f, 0.03f), Head = V(0.16f, 0.04f, 0.03f) });

        // --- What's in and by the water ---
        public static readonly FrogKind GreenFrog = new FrogKind("frog", new FrogLook());
        public static readonly FrogKind Toad = new FrogKind("toad", new FrogLook { Back = V(0.12f, 0.09f, 0.05f), Belly = V(0.3f, 0.26f, 0.18f), Eye = V(0.35f, 0.2f, 0.03f) }) { Shy = 6f };

        public static readonly FishKind Trout = new FishKind("fish.trout", new FishLook());
        public static readonly FishKind Mullet = new FishKind("fish.mullet", new FishLook { Back = V(0.1f, 0.13f, 0.15f), Belly = V(0.6f, 0.62f, 0.62f), Length = 1.2f }) { Lives = Habitat.Sea };
        public static readonly FishKind Koi = new FishKind("fish.koi", new FishLook { Back = V(0.5f, 0.1f, 0.02f), Belly = V(0.62f, 0.6f, 0.54f), Length = 1.2f }) { Lives = Habitat.Pond };

        public static readonly BatKind Bat = new BatKind("bat", new BirdLook
        {
            Body = 0.26f, Girth = 0.085f, Neck = 0.02f, Head = 0.075f, Bill = 0.012f, BillDepth = 0.02f, Tail = 0.03f, TailWidth = 0.05f, Arm = 0.26f, Hand = 0.36f, Chord = 0.28f, Point = 0.15f, Fingers = 3, Legs = 0.03f, Ears = true,
            Back = V(0.05f, 0.04f, 0.035f), Belly = V(0.07f, 0.055f, 0.045f), Crown = V(0.05f, 0.04f, 0.035f), Wing = V(0.035f, 0.03f, 0.03f), Tips = V(0.02f, 0.018f, 0.018f), TailColour = V(0.03f, 0.025f, 0.025f), Foot = V(0.04f, 0.03f, 0.03f),
        });

        // --- What came with the new country: each a bird, a crab, a hare or a butterfly already, in the colours of where it lives. ---

        /// <summary>A jay: pink-brown, a flash of blue on the wing and a white rump going away, screeching through the wildwood.</summary>
        public static readonly BirdKind Jay = new BirdKind("jay", new BirdLook
        {
            Body = 0.4f, Girth = 0.125f, Neck = 0.08f, Head = 0.085f, Bill = 0.06f, BillDepth = 0.034f, Tail = 0.26f, TailWidth = 0.1f, Arm = 0.24f, Hand = 0.3f, Chord = 0.24f, Point = 0.3f,
            Back = V(0.34f, 0.22f, 0.18f), Belly = V(0.42f, 0.3f, 0.25f), Crown = V(0.36f, 0.3f, 0.28f), Wing = V(0.3f, 0.2f, 0.16f), Tips = V(0.02f, 0.02f, 0.025f), TailColour = V(0.02f, 0.02f, 0.025f), Bars = V(0.06f, 0.18f, 0.5f), Beak = V(0.04f, 0.04f, 0.04f),
        })
        { Roosts = true, Shy = 16f, Density = 0.3f, Group = new IntRange(1, 3), Lives = Habitat.Wood | Habitat.Edge, Calls = true, Wingbeats = 7f };

        /// <summary>A kingfisher: a streak of electric blue low over the water, orange underneath.</summary>
        public static readonly BirdKind Kingfisher = new BirdKind("kingfisher", new BirdLook
        {
            Body = 0.28f, Girth = 0.1f, Neck = 0.05f, Head = 0.085f, Bill = 0.09f, BillDepth = 0.026f, Tail = 0.12f, TailWidth = 0.06f, Arm = 0.16f, Hand = 0.2f, Chord = 0.16f, Point = 0.4f, Legs = 0.05f,
            Back = V(0.03f, 0.22f, 0.5f), Belly = V(0.52f, 0.2f, 0.04f), Crown = V(0.03f, 0.16f, 0.38f), Wing = V(0.02f, 0.14f, 0.32f), Tips = V(0.015f, 0.05f, 0.12f), TailColour = V(0.03f, 0.22f, 0.5f), Beak = V(0.03f, 0.03f, 0.03f), Foot = V(0.45f, 0.1f, 0.05f),
        })
        { Shy = 14f, Density = 0.35f, Group = new IntRange(1, 1), Lives = Habitat.Bank, Height = 2.4f, Wingbeats = 10f, Cruise = 15f, Rain = 0.5f };

        /// <summary>A grey heron, standing still at the water's edge until it isn't.</summary>
        public static readonly BirdKind Heron = new BirdKind("heron", new BirdLook
        {
            Body = 1.1f, Girth = 0.25f, Neck = 0.95f, Head = 0.12f, Bill = 0.36f, BillDepth = 0.05f, Tail = 0.3f, TailWidth = 0.22f, Arm = 0.95f, Hand = 1.2f, Chord = 0.78f, Point = 0.18f, Fingers = 3, Legs = 1.1f,
            Back = V(0.28f, 0.29f, 0.31f), Belly = V(0.5f, 0.5f, 0.5f), Crown = V(0.06f, 0.06f, 0.07f), Wing = V(0.3f, 0.31f, 0.33f), Tips = V(0.05f, 0.05f, 0.06f), TailColour = V(0.28f, 0.29f, 0.31f), Beak = V(0.48f, 0.38f, 0.08f), Foot = V(0.3f, 0.24f, 0.1f),
        })
        { Walks = true, Wingbeats = 2f, Glides = 0.25f, Cruise = 10f, Height = 6f, Hold = 0.05f, Shy = 24f, Density = 0.36f, Group = new IntRange(1, 2), Lives = Habitat.Bank, Scatter = 3f, Turning = 1.1f, Rain = 0.8f };

        /// <summary>A scarlet ibis, wading in the mangroves in a colour nothing else there has.</summary>
        public static readonly BirdKind Ibis = new BirdKind("ibis", new BirdLook
        {
            Body = 0.9f, Girth = 0.22f, Neck = 0.6f, Head = 0.11f, Bill = 0.42f, BillDepth = 0.04f, Tail = 0.26f, TailWidth = 0.2f, Arm = 0.8f, Hand = 1f, Chord = 0.6f, Point = 0.25f, Fingers = 3, Legs = 0.8f,
            Back = V(0.56f, 0.06f, 0.04f), Belly = V(0.6f, 0.09f, 0.06f), Crown = V(0.56f, 0.06f, 0.04f), Wing = V(0.54f, 0.06f, 0.04f), Tips = V(0.04f, 0.02f, 0.02f), TailColour = V(0.52f, 0.06f, 0.04f), Beak = V(0.22f, 0.08f, 0.06f), Foot = V(0.5f, 0.12f, 0.08f),
        })
        { Walks = true, Wingbeats = 2.6f, Glides = 0.3f, Cruise = 11f, Height = 5f, Hold = 0.05f, Shy = 20f, Density = 0.4f, Group = new IntRange(3, 7), Lives = Habitat.Bank | Habitat.Beach, Scatter = 2.4f, Turning = 1.3f, Rain = 0.7f };

        /// <summary>An eagle over the high pastures and the sea cliffs: dark, huge in the wing, turning slow circles on the updraught.</summary>
        public static readonly BirdKind Eagle = new BirdKind("eagle", new BirdLook
        {
            Body = 0.95f, Girth = 0.28f, Neck = 0.18f, Head = 0.13f, Bill = 0.12f, BillDepth = 0.07f, Tail = 0.45f, TailWidth = 0.28f, Arm = 1.15f, Hand = 1.45f, Chord = 0.8f, Point = 0.1f, Fingers = 5, Legs = 0.26f,
            Back = V(0.12f, 0.08f, 0.05f), Belly = V(0.14f, 0.09f, 0.05f), Crown = V(0.34f, 0.24f, 0.1f), Wing = V(0.11f, 0.075f, 0.05f), Tips = V(0.03f, 0.025f, 0.02f), TailColour = V(0.1f, 0.07f, 0.05f), Beak = V(0.4f, 0.32f, 0.06f), Foot = V(0.46f, 0.36f, 0.06f),
        })
        { Walks = true, Wingbeats = 1.8f, Glides = 0.88f, Cruise = 10f, Height = 10f, Hold = 0.18f, Shy = 30f, Density = 0.1f, Group = new IntRange(1, 1), Lives = Habitat.Crag | Habitat.Barren | Habitat.Meadow, Scatter = 3f, Turning = 0.7f, Rain = 0.2f };

        /// <summary>A cockatoo: pale, loud and never alone, with a crest of deep yellow, over the red scrub.</summary>
        public static readonly BirdKind Cockatoo = new BirdKind("cockatoo", new BirdLook
        {
            Body = 0.5f, Girth = 0.17f, Neck = 0.1f, Head = 0.13f, Bill = 0.09f, BillDepth = 0.085f, Tail = 0.4f, TailWidth = 0.13f, Arm = 0.38f, Hand = 0.52f, Chord = 0.34f, Point = 0.4f, Legs = 0.08f,
            Back = V(0.62f, 0.62f, 0.6f), Belly = V(0.64f, 0.64f, 0.62f), Crown = V(0.6f, 0.46f, 0.04f), Wing = V(0.6f, 0.6f, 0.58f), Tips = V(0.56f, 0.5f, 0.3f), TailColour = V(0.6f, 0.6f, 0.58f), Beak = V(0.06f, 0.06f, 0.07f),
        })
        { Wingbeats = 5.5f, Cruise = 13f, Height = 6.5f, Roosts = true, Shy = 18f, Density = 0.4f, Group = new IntRange(4, 9), Lives = Habitat.Edge | Habitat.Wood | Habitat.Barren, Calls = true, Scatter = 1.6f };

        /// <summary>A marmot: a fat grey-brown thing that sits up on its rock to look, and is gone down its hole before anyone's near.</summary>
        public static readonly RabbitKind Marmot = new RabbitKind("marmot", new RabbitLook { Back = V(0.2f, 0.15f, 0.1f), Belly = V(0.32f, 0.25f, 0.16f), Scut = V(0.12f, 0.09f, 0.06f), EarTip = V(0.08f, 0.06f, 0.05f), Ears = 0.14f })
        { Density = 0.3f, Group = new IntRange(1, 3), Shy = 20f, BoltSpeed = 0.85f, Lives = Habitat.Meadow | Habitat.Barren | Habitat.Crag };

        /// <summary>A fiddler crab: one claw much bigger than the rest of it, and red, waving it at the mangrove mud.</summary>
        public static readonly CrabKind FiddlerCrab = new CrabKind("crab.fiddler", new CrabLook { Shell = V(0.1f, 0.12f, 0.16f), Under = V(0.36f, 0.32f, 0.26f), Leg = V(0.32f, 0.16f, 0.06f), Claw = V(0.55f, 0.1f, 0.03f), ClawTip = V(0.5f, 0.42f, 0.3f) })
        { Lives = Habitat.Beach | Habitat.Bank, Density = 0.7f, Group = new IntRange(4, 9), Shy = 7f };

        public static readonly ButterflyKind Apollo = new ButterflyKind("butterfly.apollo", new ButterflyLook { Span = 0.44f, Ground = V(0.62f, 0.62f, 0.58f), Border = V(0.08f, 0.08f, 0.08f), Spot = V(0.5f, 0.05f, 0.03f) }) { Lives = Habitat.Meadow | Habitat.Barren };
        public static readonly ButterflyKind Swallowtail = new ButterflyKind("butterfly.swallowtail", new ButterflyLook { Span = 0.48f, Ground = V(0.56f, 0.46f, 0.08f), Border = V(0.02f, 0.02f, 0.02f), Spot = V(0.08f, 0.12f, 0.42f) });

        private static readonly Creature[] Coast = { ShoreCrab, Gull, Mullet };
        private static readonly Creature[] Ponds = { GreenFrog, Darter, Skimmer, Trout, Egret };

        /// <summary>What most of the green parts of the world have, as a start for a biome's list.</summary>
        public static readonly Creature[] Temperate = Join(Coast, Ponds, new Creature[] { Rabbit, Sparrow, Goldfinch, Pigeon, Crow, Tortoiseshell, CabbageWhite, Brimstone, Bat });

        public static readonly Creature[] Grassland = Join(Coast, Ponds, new Creature[] { Hare, Rabbit, Sparrow, Pigeon, Crow, Brimstone, CabbageWhite, Bat });
        public static readonly Creature[] Conifer = Join(Coast, new Creature[] { GreenFrog, Darter, Trout, Rabbit, Sparrow, Crow, Bat });
        public static readonly Creature[] Blossom = Join(Coast, new Creature[] { GreenFrog, Darter, Skimmer, Koi, Egret, Rabbit, Goldfinch, Pigeon, CabbageWhite, Tortoiseshell, Bat });
        public static readonly Creature[] Mire = Join(Ponds, new Creature[] { Toad, Crow, Sparrow, Brimstone, Bat });
        public static readonly Creature[] Upland = Join(Coast, new Creature[] { Hare, Crow, Sparrow, Toad, Darter, Trout, Vulture, Tortoiseshell, Bat });
        public static readonly Creature[] Frozen = { SnowHare, Bunting, Crow, Mullet };
        public static readonly Creature[] Sands = { SandCrab, DesertHare, Gull, Vulture, Mullet };
        public static readonly Creature[] Scorched = { DesertHare, Crow, Vulture, Bat };
        public static readonly Creature[] Burnt = { Crow, Bat };
        public static readonly Creature[] Haunted = Join(new Creature[] { Toad, Crow, Bat, Darter, Trout });
        public static readonly Creature[] Tropics = Join(Coast, new Creature[] { GreenFrog, Skimmer, Darter, Koi, Egret, Parrot, Morpho, Brimstone, Bat });
        public static readonly Creature[] Plains = Join(Coast, Ponds, new Creature[] { Hare, Vulture, Sparrow, Crow, Brimstone });

        // --- The new country's ---
        public static readonly Creature[] Wildwood = Join(Coast, Ponds, new Creature[] { Rabbit, Jay, Sparrow, Goldfinch, Crow, Tortoiseshell, CabbageWhite, Brimstone, Bat });
        public static readonly Creature[] Riverside = Join(Ponds, new Creature[] { Kingfisher, Heron, Toad, Rabbit, Sparrow, Goldfinch, Pigeon, CabbageWhite, Tortoiseshell, Brimstone, Bat });
        public static readonly Creature[] Taiga = Join(Coast, new Creature[] { SnowHare, Hare, Jay, Crow, Bunting, Darter, Trout, Bat });
        public static readonly Creature[] Alpine = Join(new Creature[] { Marmot, Hare, Eagle, Crow, Apollo, Tortoiseshell, Darter, Trout });
        public static readonly Creature[] Maquis = Join(Coast, new Creature[] { Hare, Rabbit, Sparrow, Goldfinch, Crow, Swallowtail, Brimstone, CabbageWhite, Bat });
        public static readonly Creature[] Windward = Join(Coast, new Creature[] { Rabbit, Eagle, Crow, Sparrow, Tortoiseshell });
        public static readonly Creature[] Mangroves = Join(new Creature[] { FiddlerCrab, Ibis, Heron, Egret, Mullet, GreenFrog, Skimmer, Morpho, Bat });
        public static readonly Creature[] Fernland = Join(Coast, new Creature[] { GreenFrog, Darter, Skimmer, Trout, Egret, Parrot, Morpho, Swallowtail, Bat });
        public static readonly Creature[] CloudForest = Join(new Creature[] { GreenFrog, Toad, Darter, Trout, Parrot, Morpho, Jay, Bat });
        public static readonly Creature[] Bloomfield = Join(Coast, Ponds, new Creature[] { Rabbit, Hare, Goldfinch, Sparrow, Tortoiseshell, CabbageWhite, Brimstone, Swallowtail, Apollo, Bat });
        public static readonly Creature[] RedScrub = Join(new Creature[] { DesertHare, Cockatoo, Vulture, Crow, Brimstone, Bat, SandCrab, Gull });
        public static readonly Creature[] Bog = Join(new Creature[] { Toad, GreenFrog, Darter, Heron, Hare, Crow, Bunting, Trout });

        /// <summary>Everything, by id.</summary>
        public static readonly Creature[] All =
        {
            Rabbit, Hare, SnowHare, DesertHare, ShoreCrab, SandCrab, Sparrow, Goldfinch, Bunting, Pigeon, Crow, Gull, Egret, Parrot, Vulture,
            Tortoiseshell, CabbageWhite, Brimstone, Morpho, Darter, Skimmer, GreenFrog, Toad, Trout, Mullet, Koi, Bat,
            Jay, Kingfisher, Heron, Ibis, Eagle, Cockatoo, Marmot, FiddlerCrab, Apollo, Swallowtail,
        };

        private static Creature[] Join(params Creature[][] lists) => lists.SelectMany(list => list).Distinct().ToArray();

        public static Creature? Named(string id) => Array.Find(All, creature => creature.Id == id);
    }
}
