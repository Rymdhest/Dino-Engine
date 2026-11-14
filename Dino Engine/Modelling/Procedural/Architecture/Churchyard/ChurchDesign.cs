using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Churchyard
{
    /// <summary>What stands at a church's west end.</summary>
    public enum ChurchTower
    {
        Spire,      // A tower of three stages under a parapet with a pinnacle at each corner, and an octagonal spire rising inside it.
        Broach,     // A tower with an octagonal spire straight off its top, the corners carried up into it by broaches.
        Pinnacles,  // A tower under a battlemented parapet, tall pinnacles at its corners and gargoyles under them.
        Bellcote,   // No tower: the west gable carried up into a cote of two bells.
    }

    /// <summary>How a church's windows are made, bay by bay.</summary>
    public enum ChurchWindows
    {
        Lancets,    // Two tall narrow lancets under one hood.
        TwoLight,   // Two lights under a pointed head, a circle of tracery over them.
        ThreeLight, // Three lights under a wider head, with three circles over them.
    }

    /// <summary>
    /// A parish church, all of it worked out from a seed, so that the layout that stands it in its churchyard and the forge that builds it read the same plan. Its nave runs along x with
    /// its tower (or bellcote) at -x, which is its west end, and its chancel at +x, the east, as a church is oriented; its south side, with the porch, faces +z, which is what the camera
    /// sees, as the layout turns it to face south (CHURCHYARD.md). Model y = 0 is its floor, and the layout levels the ground to it. Every number is in metres at the world's scale,
    /// twice life, and comes from a fork of its own by name, so a new one moves nothing else.
    /// </summary>
    public sealed class ChurchDesign
    {
        public readonly uint Seed;

        public readonly ChurchTower Tower;
        public readonly ChurchWindows Windows;
        public readonly int Palette;            // Which glass it has (ChurchForge.Palettes).

        public readonly int Bays;
        public readonly float Bay;
        public readonly float NaveHalf;         // Half the nave's width to the outside of its walls.
        public readonly float NaveWall;         // Height of its walls to the eaves.
        public readonly float Pitch;            // Rise of a roof over half its span.
        public readonly bool Parapets;          // A parapet round the nave's eaves with pinnacles on its buttresses, rather than plain eaves.
        public readonly bool StoneSlates;       // Roofed in stone slates the colour of the walls, rather than blue slate.

        public readonly bool Aisles;
        public readonly float AisleWidth;
        public readonly float AisleWall;

        public readonly int ChancelBays;
        public readonly float ChancelBay;
        public readonly float ChancelHalf;
        public readonly float ChancelWall;
        public readonly bool Apse;              // A half-octagon at its east end rather than a flat end with a great window.

        public readonly float TowerHalf;
        public readonly float TowerTop;         // Height of its parapet's top, or of the bellcote's.
        public readonly float SpireHeight;      // From the tower's top to the spire's point.
        public readonly bool StoneSpire;
        public readonly bool Clock;

        public readonly int PorchBay;           // Which of the nave's bays, from the west, the porch stands at.
        public readonly float PorchHalf;
        public readonly float PorchDepth;
        public readonly float PorchWall;

        // Where everything starts and ends along x, worked out from the rest so that the whole church is centred on the origin.
        public readonly float TowerFrom, TowerTo, NaveFrom, NaveTo, ChancelFrom, ChancelTo, ApseDepth;

        private ChurchDesign(uint seed)
        {
            Seed = seed;
            var random = new WorldRandom(seed).Fork("church");
            float Range(string name, float low, float high) => random.Fork(name).Range(low, high);
            bool Chance(string name, float p) => random.Fork(name).Chance(p);
            int Int(string name, int low, int high) => random.Fork(name).Int(low, high);

            float tower = random.Fork("tower").Float();
            Tower = tower < 0.32f ? ChurchTower.Spire : tower < 0.52f ? ChurchTower.Broach : tower < 0.82f ? ChurchTower.Pinnacles : ChurchTower.Bellcote;
            float windows = random.Fork("windows").Float();
            Windows = windows < 0.32f ? ChurchWindows.Lancets : windows < 0.78f ? ChurchWindows.TwoLight : ChurchWindows.ThreeLight;
            Palette = Int("glass", 0, 3);

            Bays = Int("bays", 3, 6);
            Bay = Range("bay", 4.5f, 5.3f);
            NaveHalf = Range("nave", 5.6f, 6.5f);
            Aisles = Bays >= 5 && Chance("aisles", 0.55f);
            AisleWidth = Range("aisle", 3.6f, 4.3f);
            AisleWall = Range("aisle wall", 5.6f, 6.4f);
            NaveWall = Aisles ? AisleWall + Range("clerestory", 3.4f, 4.2f) : Range("nave wall", 8.2f, 9.6f);
            Pitch = Range("pitch", 1.0f, 1.22f);
            Parapets = Chance("parapets", 0.38f);
            StoneSlates = Chance("stone slates", 0.22f);

            ChancelBays = Int("chancel bays", 2, 3);
            ChancelBay = Range("chancel bay", 4.1f, 4.7f);
            ChancelHalf = NaveHalf - Range("chancel narrows", 1.3f, 1.9f);
            ChancelWall = MathF.Min(NaveWall, Range("chancel wall", 7.6f, 8.8f));
            Apse = Chance("apse", 0.4f);

            TowerHalf = Range("tower half", 3.6f, 4.3f);
            // A tower stands well clear of the nave's ridge, as a tower that only just tops its roof reads as a chimney.
            TowerTop = Tower == ChurchTower.Bellcote ? Ridge + Range("cote", 2.6f, 3.4f) : Ridge + Range("tower rise", 4.6f, 7f);
            SpireHeight = Tower switch
            {
                ChurchTower.Spire => Range("spire", 9.5f, 12f),
                ChurchTower.Broach => Range("broach", 10.5f, 13f),
                _ => 0f,
            };
            StoneSpire = Chance("stone spire", 0.55f);
            Clock = Tower != ChurchTower.Bellcote && Chance("clock", 0.55f);

            PorchBay = Int("porch bay", 0, 1);
            PorchHalf = Range("porch half", 2.8f, 3.3f);
            PorchDepth = Range("porch depth", 4.6f, 5.6f);
            PorchWall = Range("porch wall", 5.2f, 6f);

            ApseDepth = Apse ? ChancelHalf * 0.82f : 0f;
            float length = (Tower == ChurchTower.Bellcote ? 0f : TowerHalf * 2f - 0.6f) + Bays * Bay + ChancelBays * ChancelBay + ApseDepth;
            float x = -length * 0.5f;
            if (Tower == ChurchTower.Bellcote)
            {
                TowerFrom = TowerTo = x;
            }
            else
            {
                TowerFrom = x;
                TowerTo = x + TowerHalf * 2f;
                x = TowerTo - 0.6f;
            }
            NaveFrom = x;
            NaveTo = NaveFrom + Bays * Bay;
            ChancelFrom = NaveTo;
            ChancelTo = ChancelFrom + ChancelBays * ChancelBay;
        }

        private static readonly Dictionary<uint, ChurchDesign> Made = new();

        /// <summary>The church grown from <paramref name="seed"/>. The same seed is always the same church.</summary>
        public static ChurchDesign Of(uint seed)
        {
            lock (Made)
            {
                if (!Made.TryGetValue(seed, out ChurchDesign? design)) Made[seed] = design = new ChurchDesign(seed);
                return design;
            }
        }

        public bool HasTower => Tower != ChurchTower.Bellcote;

        /// <summary>How far a roof's eaves stand out past the wall.</summary>
        public const float Eave = 0.6f;

        /// <summary>The height of the nave's ridge.</summary>
        public float Ridge => NaveWall + (NaveHalf + Eave) * Pitch;

        /// <summary>The height of the chancel's ridge.</summary>
        public float ChancelRidge => ChancelWall + (ChancelHalf + Eave) * Pitch;

        /// <summary>Half the width of the nave with its aisles.</summary>
        public float BodyHalf => NaveHalf + (Aisles ? AisleWidth : 0f);

        /// <summary>The middle of the tower along x.</summary>
        public float TowerX => (TowerFrom + TowerTo) * 0.5f;

        /// <summary>The middle of the porch along x.</summary>
        public float PorchX => NaveFrom + Bay * (PorchBay + 0.5f) + (HasTower ? 0.3f : 0f);

        /// <summary>The outside of the porch's mouth, on the ground: where the path to the door ends.</summary>
        public Vector2 PorchMouth => new Vector2(PorchX, BodyHalf + PorchDepth);

        /// <summary>The outside of the west door, in the tower's foot or the west front, on the ground.</summary>
        public Vector2 WestDoor => new Vector2((HasTower ? TowerFrom : NaveFrom) - 0.2f, 0f);

        /// <summary>The highest point of it: the spire's cross, the tower's pinnacles or the bellcote.</summary>
        public float Top => Tower switch
        {
            ChurchTower.Spire or ChurchTower.Broach => TowerTop + SpireHeight + 2.6f,
            ChurchTower.Pinnacles => TowerTop + 3.8f,
            _ => TowerTop + 1.6f,
        };

        /// <summary>Metres its buttresses and plinth stand out from the walls the footprints are drawn round.</summary>
        public const float Reach = 1.25f;

        /// <summary>The ground each part stands on, as boxes in its own frame, square to its axes: the tower, the nave and its aisles, the chancel, its apse and the porch.</summary>
        public IEnumerable<(Vector2 Centre, Vector2 Half)> Parts()
        {
            if (HasTower) yield return (new Vector2(TowerX, 0f), new Vector2(TowerHalf, TowerHalf));
            yield return (new Vector2((NaveFrom + NaveTo) * 0.5f, 0f), new Vector2((NaveTo - NaveFrom) * 0.5f, BodyHalf));
            yield return (new Vector2((ChancelFrom + ChancelTo) * 0.5f, 0f), new Vector2((ChancelTo - ChancelFrom) * 0.5f, ChancelHalf));
            if (Apse) yield return (new Vector2(ChancelTo + ApseDepth * 0.5f, 0f), new Vector2(ApseDepth * 0.5f, ChancelHalf * 0.75f));
            yield return (new Vector2(PorchX, BodyHalf + PorchDepth * 0.5f), new Vector2(PorchHalf, PorchDepth * 0.5f));
        }

        /// <summary>
        /// The ground that's solid, buttresses and all, as boxes that don't overlap one another, as no two buildings may stand in each other (WorldValidator): the nave with its aisles
        /// from its west end to the chancel, the chancel and its apse, the tower beyond the nave's west end, and the porch beyond its south side.
        /// </summary>
        public IEnumerable<(Vector2 Centre, Vector2 Half)> Solids()
        {
            const float Gap = 0.04f;
            static (Vector2, Vector2) Box(float x0, float z0, float x1, float z1) => (new Vector2((x0 + x1) * 0.5f, (z0 + z1) * 0.5f), new Vector2((x1 - x0) * 0.5f, (z1 - z0) * 0.5f));
            float west = NaveFrom - (HasTower ? 0f : Reach);
            float body = BodyHalf + Reach;
            yield return Box(west, -body, NaveTo, body);
            float east = ChancelTo + (Apse ? 0f : Reach);
            yield return Box(NaveTo + Gap, -(ChancelHalf + Reach), east, ChancelHalf + Reach);
            if (Apse) yield return Box(east + Gap, -(ChancelHalf * 0.75f + Reach), ChancelTo + ApseDepth + Reach, ChancelHalf * 0.75f + Reach);
            if (HasTower) yield return Box(TowerFrom - Reach, -(TowerHalf + Reach), west - Gap, TowerHalf + Reach);
            yield return Box(PorchX - PorchHalf - Reach * 0.6f, body + Gap, PorchX + PorchHalf + Reach * 0.6f, BodyHalf + PorchDepth + 0.3f);
        }

        /// <summary>The box round all of it, buttresses and all: from its least corner to its most along x and z.</summary>
        public (Vector2 Min, Vector2 Max) Bounds
        {
            get
            {
                float west = (HasTower ? TowerFrom : NaveFrom) - Reach;
                float east = ChancelTo + ApseDepth + Reach;
                float north = -MathF.Max(BodyHalf, HasTower ? TowerHalf : 0f) - Reach;
                float south = BodyHalf + PorchDepth + 0.3f;
                return (new Vector2(west, north), new Vector2(east, south));
            }
        }
    }
}
