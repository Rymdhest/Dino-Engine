using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Halls;
using Dino_Engine.Modelling.Procedural.Architecture.Plaza;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Town
{
    /// <summary>
    /// A home of the Haven (HAVEN.md), built for the one camera that ever sees it, high in the south and looking north. From there a house is its roof first, so the roof is what's
    /// made with care: one smooth coat that sags a little along its ridge and weathers in soft patches, darker and mossier towards the eaves and on the side away from the sun, with
    /// a real thickness at its edges, a ridge laid as that roofing's is (a thatcher's block ridge under hazel rods, half-round tiles, a roll of lead), eaves lifted in a brow over a
    /// loft's window, and its chimney built up outside a gable from a broad foot. Its front wall is seen at a slant under the eaves, which is why they're kept short and its windows
    /// low: timber and lime on a plinth of field stone, shutters and a door in one of the Haven's dyes, flowers under the windows, something grown up the wall, and a wing whose own
    /// gable faces whoever's looking. Front towards +z, y = 0 where people stand at its foot. Headless, and nothing in it is black iron, oxblood or brass (LORE.md).
    /// </summary>
    internal static class TownHouse
    {
        private const float Floor = 0.36f;          // Its sill above the ground, a step up.
        private const float PlinthTop = 0.95f;
        private const float DoorHalf = 1.3f;
        private const float DoorHead = 5.5f;        // Over the floor: a little over a person's 4.7 m.
        private const float T = 0.13f;              // Half the width of a timber of its frame.
        private const float Proud = 0.06f;          // How far its frame stands out of the lime between.

        /// <summary>The Haven's dyes, as paint on boards: teal, sage, saffron, deep blue, blossom and cream.</summary>
        private static readonly Vector3[] Paints =
        {
            new Vector3(52, 104, 100), new Vector3(92, 116, 78), new Vector3(196, 142, 58), new Vector3(58, 82, 132), new Vector3(178, 108, 122), new Vector3(208, 198, 170),
        };

        private sealed class Look
        {
            public Roofing Roofing;
            public Walling Walling;
            public HouseFeature Features;
            public VertexMaterial Lime, Timber, Stone, Plinth, Boards, Roof, Ridge, Cut, Door, Shutter, Joinery, Glass, Brick, Rod, Leaf;
            public Vector3 RoofColour, Moss, Fresh;
            public float Pitch, Eave, Verge, Thick, Lumps, Sag, Mossy;
            public bool CloseStuds;
            public WorldNoise Noise = null!;
            public float NoiseAt;
            public List<AssetLight> Lights = null!;
            public List<Vector3> Chimneys = null!;
            public int WindowLights = 2;

            public bool Has(HouseFeature feature) => (Features & feature) != 0;
        }

        /// <summary>One wall of a block: where it is, which way along it runs seen from outside, and which way is out.</summary>
        internal readonly struct Wall
        {
            public readonly Vector3 Origin, A, N;
            public readonly float Half;

            public Wall(Vector3 origin, Vector3 along, Vector3 out_, float half)
            {
                Origin = origin;
                A = along;
                N = out_;
                Half = half;
            }

            public Vector3 At(float along, float y, float proud = 0f) => Origin + A * along + Vector3.UnitY * y + N * proud;

            public static Wall Front(float cx, float cz, float hx, float hz) => new Wall(new Vector3(cx, 0f, cz + hz), Vector3.UnitX, Vector3.UnitZ, hx);
            public static Wall Back(float cx, float cz, float hx, float hz) => new Wall(new Vector3(cx, 0f, cz - hz), -Vector3.UnitX, -Vector3.UnitZ, hx);
            public static Wall Right(float cx, float cz, float hx, float hz) => new Wall(new Vector3(cx + hx, 0f, cz), -Vector3.UnitZ, Vector3.UnitX, hz);
            public static Wall Left(float cx, float cz, float hx, float hz) => new Wall(new Vector3(cx - hx, 0f, cz), Vector3.UnitZ, -Vector3.UnitX, hz);
        }

        /// <summary>Something let into a wall: where along it, how wide, and from what height to what.</summary>
        private readonly record struct Gap(float Along, float Half, float Sill, float Head);

        // ---------------------------------------------------------------- The house

        public static void Build(MeshBuilder b, in TownSpec spec, List<AssetLight> lights, List<Vector3> chimneys)
        {
            var random = new WorldRandom(spec.Seed).Fork("house");
            float hx = MathF.Max(spec.Metres(0), 6f) * 0.5f, hz = MathF.Max(spec.Metres(1), 6f) * 0.5f;
            var design = (HouseDesign)Math.Clamp(spec.Int(2), 0, (int)HouseDesign.Workshop);
            Look k = LookOf(spec, random);
            k.Lights = lights;
            k.Chimneys = chimneys;

            bool two = design == HouseDesign.Townhouse;
            float storey = two ? random.Range(6.3f, 6.7f) : 0f;
            float wallTop = two ? storey + random.Range(4.9f, 5.3f) : design switch
            {
                HouseDesign.Fisher => random.Range(5.7f, 6.1f),
                HouseDesign.Longhouse => random.Range(6.3f, 6.7f),
                _ => random.Range(6.5f, 7.1f),
            };
            float jetty = two && k.Has(HouseFeature.Jetty) ? 0.55f : 0f;

            // Where its door is: off the middle in a cottage, as its hearth takes the middle of one, and in the middle of anything grander.
            float doorX = design switch
            {
                HouseDesign.Townhouse => k.Has(HouseFeature.Awning) ? -hx + 2.6f : 0f,
                HouseDesign.Longhouse => -hx * 0.42f,
                HouseDesign.Workshop => hx - 3.1f,
                _ => (random.Chance(0.5f) ? 1f : -1f) * hx * random.Range(0.18f, 0.3f),
            };
            // A wing stands out of the front, beside the door, its own gable to whoever's looking.
            bool wing = k.Has(HouseFeature.Wing) && hx > 5.5f;
            float wingHalf = MathF.Min(hx * 0.36f, 3.3f);
            float wingX = doorX > 0f ? -hx + wingHalf + 0.5f : hx - wingHalf - 0.5f;
            const float WingOut = 2.6f;
            if (wing && MathF.Abs(doorX - wingX) < wingHalf + DoorHalf + 0.9f) doorX = wingX - MathF.Sign(wingX) * (wingHalf + DoorHalf + 1f);

            PlinthOf(b, k, 0f, 0f, hx, hz);
            var front = new List<Gap>();
            var back = new List<Gap>();
            var right = new List<Gap>();
            var left = new List<Gap>();

            // The front: the door, and windows either side of it wherever there's wall for one.
            var frontWall = Wall.Front(0f, 0f, hx, hz);
            front.Add(new Gap(doorX, DoorHalf, Floor, Floor + DoorHead));
            float sill = Floor + 1.75f, head = sill + 2.05f;
            var wanted = new List<float>();
            for (float x = -hx + 2.2f; x <= hx - 2.2f + 0.01f; x += MathF.Max(3.1f, (2f * hx - 4.4f) / MathF.Max(1f, MathF.Floor((2f * hx - 4.4f) / 3.4f)))) wanted.Add(x);
            bool shop = two && k.Has(HouseFeature.Awning);
            bool open = design == HouseDesign.Workshop;
            float shopFrom = doorX + DoorHalf + 0.9f, shopTo = hx - 1.1f;
            float bayFrom = -hx + 1f, bayTo = doorX - DoorHalf - 1f;
            foreach (float x in wanted)
            {
                if (MathF.Abs(x - doorX) < DoorHalf + 1.5f) continue;
                if (wing && MathF.Abs(x - wingX) < wingHalf + 1.2f) continue;
                if (shop && x > shopFrom - 1f) continue;
                if (open && x < bayTo + 1f) continue;
                front.Add(new Gap(x, 0.85f, sill, head));
            }
            if (shop) front.Add(new Gap((shopFrom + shopTo) * 0.5f, (shopTo - shopFrom) * 0.5f, Floor + 1.5f, Floor + 4.3f));
            if (open) front.Add(new Gap((bayFrom + bayTo) * 0.5f, (bayTo - bayFrom) * 0.5f, Floor, Floor + 5.2f));
            // The back and the ends, plainer.
            for (float x = -hx + 2.6f; x <= hx - 2.6f + 0.01f; x += 4.2f) if (random.Chance(0.7f)) back.Add(new Gap(-x, 0.8f, sill + 0.2f, head));
            if (!k.Has(HouseFeature.LeanRight) && !(k.Has(HouseFeature.EndStack))) right.Add(new Gap(0f, 0.8f, sill, head));
            if (!k.Has(HouseFeature.LeanLeft) && !(k.Has(HouseFeature.EndStack) && k.Has(HouseFeature.TwoStacks))) left.Add(new Gap(0f, 0.8f, sill, head));

            float groundTop = two ? storey : wallTop;
            VertexMaterial groundWall = k.Walling switch { Walling.Stone or Walling.StoneFoot => k.Stone, Walling.Boarded => k.Boards, _ => k.Lime };
            bool groundFramed = k.Walling == Walling.Framed;
            b.Box(new Vector3(0f, (PlinthTop + groundTop) * 0.5f, 0f), new Vector3(hx, (groundTop - PlinthTop) * 0.5f, hz), groundWall);
            (Wall wall, List<Gap> gaps)[] sides =
            {
                (frontWall, front), (Wall.Back(0f, 0f, hx, hz), back), (Wall.Right(0f, 0f, hx, hz), right), (Wall.Left(0f, 0f, hx, hz), left),
            };
            foreach ((Wall wall, List<Gap> gaps) in sides)
            {
                if (groundFramed) Frame(b, k, wall, PlinthTop, groundTop, gaps, random.Fork("frame"), k.CloseStuds && !two);
                else if (k.Walling == Walling.Boarded) CornerPosts(b, k, wall, PlinthTop, groundTop);
                else Quoins(b, k, wall, PlinthTop, groundTop);
            }

            // What's in the front wall.
            Door(b, k, frontWall, doorX, random.Fork("door"));
            foreach (Gap gap in front)
            {
                if (gap.Along == doorX && gap.Half == DoorHalf) continue;
                if (shop && gap.Half > 1.5f) ShopFront(b, k, frontWall, gap, random.Fork("shop"));
                else if (open && gap.Half > 1.5f) OpenBay(b, k, frontWall, gap, hz, random.Fork("bay"));
                else Window(b, k, frontWall, gap, shutters: true, flowers: k.Has(HouseFeature.Boxes), shines: true, random.Fork((int)(gap.Along * 10f)));
            }
            foreach (Gap gap in back) Window(b, k, sides[1].wall, gap, shutters: false, flowers: false, shines: false, random);
            foreach (Gap gap in right) Window(b, k, sides[2].wall, gap, shutters: true, flowers: false, shines: false, random);
            foreach (Gap gap in left) Window(b, k, sides[3].wall, gap, shutters: true, flowers: false, shines: false, random);

            // An upper storey, of timber and lime whatever's under it, out over the street on the ends of its joists.
            float roofCz = 0f, roofHz = hz;
            if (two)
            {
                roofCz = jetty * 0.5f;
                roofHz = hz + jetty * 0.5f;
                b.Box(new Vector3(0f, (storey + wallTop) * 0.5f, roofCz), new Vector3(hx, (wallTop - storey) * 0.5f, roofHz), k.Lime);
                var upperFront = Wall.Front(0f, roofCz, hx, roofHz);
                var upper = new List<Gap>();
                int count = Math.Max(2, (int)(hx * 2f / 3.6f));
                for (int i = 0; i < count; i++) upper.Add(new Gap(-hx + 2f * hx * (i + 0.5f) / count, 0.8f, storey + 1.5f, storey + 3.4f));
                Frame(b, k, upperFront, storey, wallTop, upper, random.Fork("upper"), true);
                foreach (Gap gap in upper) Window(b, k, upperFront, gap, shutters: random.Chance(0.6f), flowers: k.Has(HouseFeature.Boxes) && random.Chance(0.5f), shines: false, random.Fork((int)(gap.Along * 7f) + 99));
                Frame(b, k, Wall.Back(0f, roofCz, hx, roofHz), storey, wallTop, new List<Gap>(), random.Fork("upper back"), true);
                Frame(b, k, Wall.Right(0f, roofCz, hx, roofHz), storey, wallTop, new List<Gap>(), random.Fork("upper right"), false);
                Frame(b, k, Wall.Left(0f, roofCz, hx, roofHz), storey, wallTop, new List<Gap>(), random.Fork("upper left"), false);
                if (jetty > 0f)
                {
                    // The beam its front stands on, and the ends of the joists that carry it.
                    HallDressing.Box(b, new Vector3(0f, storey - 0.02f, hz + jetty - 0.1f), Vector3.UnitX, Vector3.UnitZ, hx + 0.05f, 0.2f, 0.16f, k.Timber);
                    for (float x = -hx + 0.3f; x <= hx - 0.3f + 0.01f; x += 0.95f) b.Box(new Vector3(x, storey - 0.36f, hz + jetty * 0.5f), new Vector3(0.11f, 0.15f, jetty * 0.5f + 0.05f), k.Timber);
                }
            }

            // The roof, and what breaks it: brows over a loft's windows in thatch, dormers in anything laid in courses.
            var brows = new List<(float X, float Half)>();
            if (k.Has(HouseFeature.Dormers))
            {
                int count = hx > 9f ? 2 : 1;
                for (int i = 0; i < count; i++)
                {
                    float x = count == 1 ? -doorX * 0.6f : (i == 0 ? -1f : 1f) * hx * 0.48f;
                    if (wing && MathF.Abs(x - wingX) < wingHalf + 1.8f) x = -wingX * 0.6f;
                    brows.Add((x, 1.15f));
                }
            }
            float sootAt = float.NaN;
            float stackX = 0f;
            if (!k.Has(HouseFeature.EndStack))
            {
                stackX = Math.Clamp(-doorX * 0.5f + random.Range(-0.8f, 0.8f), -hx + 2f, hx - 2f);
                sootAt = stackX;
            }
            float ridge = Roof(b, k, 0f, roofCz, hx, roofHz, wallTop, random.Fork("roof"), brows, sootAt, gables: true, mended: k.Has(HouseFeature.Mended));
            foreach ((float x, float half) in brows)
            {
                if (k.Roofing == Roofing.Thatch) BrowWindow(b, k, Wall.Front(0f, roofCz, hx, roofHz), x, half, wallTop);
                else Dormer(b, k, x, roofCz, roofHz, wallTop, half, random.Fork((int)(x * 13f)));
            }

            // Its fires.
            if (k.Has(HouseFeature.EndStack))
            {
                EndStack(b, k, hx, hz, wallTop, ridge, 1f, random.Fork("stack"));
                if (k.Has(HouseFeature.TwoStacks)) EndStack(b, k, hx, hz, wallTop, ridge, -1f, random.Fork("stack two"));
            }
            else
            {
                RidgeStack(b, k, new Vector3(stackX, 0f, roofCz), wallTop, ridge, random.Fork("stack"));
                if (k.Has(HouseFeature.TwoStacks)) RidgeStack(b, k, new Vector3(-stackX + (stackX > 0f ? -2.4f : 2.4f), 0f, roofCz), wallTop, ridge, random.Fork("stack two"));
            }

            if (wing)
            {
                // Built in its own frame, its ridge running out from the house, and turned a quarter: its far gable is the one that faces the street.
                float wingTop = two ? storey + 1.4f : wallTop - 0.9f;
                // Far enough into the house that its ridge runs into the house's own roof, and the two meet in a valley.
                float along = (WingOut + wingHalf + 0.6f) * 0.5f;
                Hall.At(b, new Vector3(wingX, 0f, hz + WingOut - along), -MathF.PI * 0.5f, lights, () =>
                {
                    PlinthOf(b, k, 0f, 0f, along, wingHalf);
                    b.Box(new Vector3(0f, (PlinthTop + wingTop) * 0.5f, 0f), new Vector3(along, (wingTop - PlinthTop) * 0.5f, wingHalf), k.Lime);
                    // Its own +x is the house's +z, so its street gable is its right wall.
                    var gable = Wall.Right(0f, 0f, along, wingHalf);
                    var gaps = new List<Gap> { new Gap(0f, 0.95f, sill, head) };
                    Frame(b, k, gable, PlinthTop, wingTop, gaps, random.Fork("wing"), false);
                    Window(b, k, gable, gaps[0], shutters: true, flowers: k.Has(HouseFeature.Boxes), shines: true, random.Fork("wing window"));
                    Frame(b, k, Wall.Front(0f, 0f, along, wingHalf), PlinthTop, wingTop, new List<Gap>(), random.Fork("wing a"), false);
                    Frame(b, k, Wall.Back(0f, 0f, along, wingHalf), PlinthTop, wingTop, new List<Gap>(), random.Fork("wing b"), false);
                    float saved = k.Sag;
                    k.Sag = 0f;
                    Roof(b, k, 0f, 0f, along, wingHalf, wingTop, random.Fork("wing roof"), new List<(float, float)>(), float.NaN, gables: true, mended: false, loftWindow: true);
                    k.Sag = saved;
                });
            }

            if (k.Has(HouseFeature.Porch)) Hood(b, k, frontWall, doorX, random.Fork("hood"));
            if (k.Has(HouseFeature.LeanRight)) LeanTo(b, k, hx, hz, wallTop, 1f, random.Fork("lean right"));
            if (k.Has(HouseFeature.LeanLeft)) LeanTo(b, k, hx, hz, wallTop, -1f, random.Fork("lean left"));
            if (k.Has(HouseFeature.Bench))
            {
                float side = doorX > 0f ? -1f : 1f;
                float x = doorX + side * (DoorHalf + 2.2f);
                if (!(wing && MathF.Abs(x - wingX) < wingHalf + 1.4f)) Seat(b, k, new Vector3(x, 0f, hz + 0.5f), 1.5f);
            }
            if (k.Has(HouseFeature.Climber))
            {
                float side = doorX > 0f ? 1f : -1f;
                float x = Math.Clamp(doorX + side * (DoorHalf + 0.9f), -hx + 0.8f, hx - 0.8f);
                Climber(b, k, frontWall, x, two ? storey - 0.4f : wallTop - 1.1f, random.Fork("climber"));
            }
            if (k.Has(HouseFeature.Mended)) Ladder(b, k, new Vector3(hx * 0.3f, 0f, hz + jetty + k.Eave + 1.5f), wallTop + 0.6f, random);
            // A butt under the eaves at one corner, as the roof's water is worth keeping.
            WaterButt(b, k, new Vector3((doorX > 0f ? -1f : 1f) * (hx - 0.9f), 0f, hz + 0.75f), random.Fork("butt"));
        }

        /// <summary>
        /// A roof of thatch over walls of half (<paramref name="hx"/>, <paramref name="hz"/>) that stand to <paramref name="wallTop"/>, as a house's is laid, for a hall that's
        /// roofed like the homes round it: the coat, its block ridge, its gables in <paramref name="wall"/>, and a brow lifted over a loft's window at each of
        /// <paramref name="brows"/> along its front. Returns how high its ridge stands.
        /// </summary>
        public static float ThatchOver(MeshBuilder b, float hx, float hz, float wallTop, float pitch, uint seed, IReadOnlyList<float> brows, VertexMaterial wall, VertexMaterial timber,
            List<AssetLight> lights, float sag)
        {
            var random = new WorldRandom(seed).Fork("thatch");
            Look k = LookOf(TownSpec.House(seed, hx * 2f, hz * 2f, HouseDesign.Cottage, Roofing.Thatch, Walling.Boarded, HouseFeature.None, 0), random);
            k.Lights = lights;
            k.Chimneys = new List<Vector3>();
            k.Pitch = pitch;
            k.Sag = sag;
            k.Lime = wall;
            k.Boards = wall;
            k.Timber = timber;
            k.WindowLights = 0;
            var lifted = brows.Select(x => (x, 1.3f)).ToList();
            float ridge = Roof(b, k, 0f, 0f, hx, hz, wallTop, random.Fork("roof"), lifted, float.NaN, gables: true, mended: false);
            Wall front = Wall.Front(0f, 0f, hx, hz);
            foreach (float x in brows) BrowWindow(b, k, front, x, 1.3f, wallTop);
            return ridge;
        }

        /// <summary>How far a thatched roof's eaves reach past its walls.</summary>
        public const float ThatchEaves = 0.62f;

        /// <summary>The ground a house keeps anyone off, beyond its own walls, in its own frame: its wing, its chimney's foot and whatever leans against it.</summary>
        public static void Solids(in TownSpec spec, List<HitPart> parts)
        {
            parts.Clear();
            float hx = MathF.Max(spec.Metres(0), 6f) * 0.5f, hz = MathF.Max(spec.Metres(1), 6f) * 0.5f;
            var features = (HouseFeature)spec.Int(5);
            parts.Add(new HitPart(Vector2.Zero, new Vector2(hx, hz), PlacementIndex.Tall));
            if ((features & HouseFeature.EndStack) != 0)
            {
                parts.Add(new HitPart(new Vector2(hx + 0.6f, 0f), new Vector2(0.6f, 1.35f), PlacementIndex.Tall));
                if ((features & HouseFeature.TwoStacks) != 0) parts.Add(new HitPart(new Vector2(-hx - 0.6f, 0f), new Vector2(0.6f, 1.35f), PlacementIndex.Tall));
            }
            // A lean-to's posts are walked round, and what's stacked under it stands in the way.
            if ((features & HouseFeature.LeanRight) != 0) parts.Add(new HitPart(new Vector2(hx + 1.1f, -0.4f), new Vector2(1f, hz - 1.4f), 2.6f));
            if ((features & HouseFeature.LeanLeft) != 0) parts.Add(new HitPart(new Vector2(-hx - 1.1f, -0.4f), new Vector2(1f, hz - 1.4f), 2.6f));
        }

        /// <summary>Where a house's wing stands, in its own frame, if it has one: its middle and half its size. Worked out as <see cref="Build"/> does.</summary>
        public static bool WingOf(in TownSpec spec, out Vector2 centre, out Vector2 half)
        {
            centre = half = Vector2.Zero;
            float hx = MathF.Max(spec.Metres(0), 6f) * 0.5f, hz = MathF.Max(spec.Metres(1), 6f) * 0.5f;
            if (((HouseFeature)spec.Int(5) & HouseFeature.Wing) == 0 || hx <= 5.5f) return false;
            float doorX = DoorAt(spec);
            float wingHalf = MathF.Min(hx * 0.36f, 3.3f);
            float wingX = doorX > 0f ? -hx + wingHalf + 0.5f : hx - wingHalf - 0.5f;
            centre = new Vector2(wingX, hz + 1.3f);
            half = new Vector2(wingHalf, 1.3f);
            return true;
        }

        /// <summary>Where along its front a house's door is, in its own frame, as <see cref="Build"/> puts it: a path is laid to it.</summary>
        public static float DoorAt(in TownSpec spec)
        {
            var random = new WorldRandom(spec.Seed).Fork("house");
            float hx = MathF.Max(spec.Metres(0), 6f) * 0.5f;
            var design = (HouseDesign)Math.Clamp(spec.Int(2), 0, (int)HouseDesign.Workshop);
            var features = (HouseFeature)spec.Int(5);
            // The same draws, in the same order, as Build makes before it places the door.
            LookOf(spec, random);
            bool two = design == HouseDesign.Townhouse;
            if (two) { random.Range(6.3f, 6.7f); random.Range(4.9f, 5.3f); }
            else random.Range(0f, 1f);
            float doorX = design switch
            {
                HouseDesign.Townhouse => (features & HouseFeature.Awning) != 0 ? -hx + 2.6f : 0f,
                HouseDesign.Longhouse => -hx * 0.42f,
                HouseDesign.Workshop => hx - 3.1f,
                _ => (random.Chance(0.5f) ? 1f : -1f) * hx * random.Range(0.18f, 0.3f),
            };
            bool wing = (features & HouseFeature.Wing) != 0 && hx > 5.5f;
            float wingHalf = MathF.Min(hx * 0.36f, 3.3f);
            float wingX = doorX > 0f ? -hx + wingHalf + 0.5f : hx - wingHalf - 0.5f;
            if (wing && MathF.Abs(doorX - wingX) < wingHalf + DoorHalf + 0.9f) doorX = wingX - MathF.Sign(wingX) * (wingHalf + DoorHalf + 1f);
            return doorX;
        }

        // ---------------------------------------------------------------- What it's made of

        private static Look LookOf(in TownSpec spec, WorldRandom random)
        {
            var k = new Look
            {
                Roofing = (Roofing)Math.Clamp(spec.Int(3), 0, (int)Roofing.Tile),
                Walling = (Walling)Math.Clamp(spec.Int(4), 0, (int)Walling.Boarded),
                Features = (HouseFeature)spec.Int(5),
                Noise = new WorldNoise(random.Fork("noise")),
                NoiseAt = random.Range(0f, 400f),
            };
            // Lime, washed cream, warm white, ochre or the faintest blush, and oak gone silver-brown or tarred dark.
            Vector3[] limes = { new(226, 210, 182), new(232, 222, 200), new(218, 190, 140), new(226, 200, 184) };
            Vector3 lime = limes[random.Int(limes.Length)] * random.Range(0.95f, 1.02f);
            Vector3[] oaks = { new(96, 68, 46), new(124, 102, 82), new(74, 54, 40), new(108, 80, 56) };
            Vector3 oak = oaks[random.Int(oaks.Length)] * random.Range(0.93f, 1.06f);
            Vector3 paint = Paints[Math.Clamp(spec.Int(6), 0, Paints.Length - 1)];
            k.Lime = Hall.Plaster((int)lime.X, (int)lime.Y, (int)lime.Z);
            k.Timber = Hall.Wood((int)oak.X, (int)oak.Y, (int)oak.Z);
            float stone = random.Range(0.92f, 1.05f);
            k.Stone = Hall.Stone((int)(176 * stone), (int)(166 * stone), (int)(150 * stone));
            k.Plinth = Hall.Stone((int)(140 * stone), (int)(134 * stone), (int)(124 * stone));
            float tar = random.Range(0.85f, 1.1f);
            k.Boards = spec.Int(2) == (int)HouseDesign.Fisher ? Hall.Wood((int)(84 * tar), (int)(70 * tar), (int)(60 * tar)) : Hall.Wood((int)(150 * tar), (int)(128 * tar), (int)(104 * tar));
            k.Door = Hall.Wood((int)(paint.X * 0.9f), (int)(paint.Y * 0.9f), (int)(paint.Z * 0.9f));
            k.Shutter = Hall.Wood((int)paint.X, (int)paint.Y, (int)paint.Z);
            k.Joinery = Hall.Wood((int)(oak.X * 0.9f), (int)(oak.Y * 0.9f), (int)(oak.Z * 0.9f));
            k.Glass = Hall.Glow(1f, 0.6f, 0.26f, 0.5f);
            k.Brick = Hall.Brick(158, 112, 92);
            k.Rod = Hall.Wood(86, 64, 44);
            k.Leaf = TownPieces.Leaf(random);
            k.CloseStuds = random.Chance(0.4f);
            k.Mossy = random.Range(0.35f, 1f);
            k.Sag = random.Range(0.06f, 0.2f);
            switch (k.Roofing)
            {
                case Roofing.Thatch:
                    // Reed gone grey-gold with the weather, its ridge laid later and brighter.
                    k.RoofColour = Vector3.Lerp(new Vector3(0.74f, 0.68f, 0.56f), new Vector3(0.96f, 0.88f, 0.7f), random.Float());
                    k.Fresh = new Vector3(1.06f, 0.98f, 0.74f);
                    k.Roof = new VertexMaterial(MaterialCatalog.Thatch.Index, new Colour(k.RoofColour));
                    k.Ridge = new VertexMaterial(MaterialCatalog.Thatch.Index, new Colour(Vector3.Lerp(k.RoofColour, k.Fresh, 0.6f)));
                    k.Cut = new VertexMaterial(MaterialCatalog.Thatch.Index, new Colour(k.RoofColour * 0.66f));
                    k.Moss = new Vector3(0.62f, 0.86f, 0.5f);
                    k.Pitch = random.Range(1.04f, 1.18f);
                    k.Eave = 0.62f;
                    k.Verge = 0.5f;
                    k.Thick = 0.52f;
                    k.Lumps = 0.07f;
                    break;
                case Roofing.Shingle:
                    k.RoofColour = Vector3.Lerp(new Vector3(0.84f, 0.73f, 0.62f), new Vector3(0.72f, 0.7f, 0.66f), random.Float());
                    k.Fresh = new Vector3(1.02f, 0.86f, 0.66f);
                    k.Roof = new VertexMaterial(MaterialCatalog.Shingle.Index, new Colour(k.RoofColour));
                    k.Ridge = Hall.Wood(104, 82, 62);
                    k.Cut = new VertexMaterial(MaterialCatalog.Shingle.Index, new Colour(k.RoofColour * 0.7f));
                    k.Moss = new Vector3(0.7f, 0.9f, 0.6f);
                    k.Pitch = random.Range(0.9f, 1.02f);
                    k.Eave = 0.5f;
                    k.Verge = 0.42f;
                    k.Thick = 0.2f;
                    break;
                case Roofing.Slate:
                    k.RoofColour = new Vector3(0.69f, 0.68f, 0.68f) * random.Range(0.94f, 1.06f);
                    k.Fresh = new Vector3(0.78f, 0.78f, 0.8f);
                    k.Roof = new VertexMaterial(MaterialCatalog.Slate.Index, new Colour(k.RoofColour));
                    k.Ridge = Hall.Stone(128, 126, 122);
                    k.Cut = new VertexMaterial(MaterialCatalog.Slate.Index, new Colour(k.RoofColour * 0.75f));
                    k.Moss = new Vector3(0.78f, 0.92f, 0.7f);
                    k.Pitch = random.Range(0.8f, 0.9f);
                    k.Eave = 0.42f;
                    k.Verge = 0.36f;
                    k.Thick = 0.15f;
                    k.Sag *= 0.5f;
                    break;
                default:
                    // Clay from the island's own pits, more rose than red.
                    k.RoofColour = Vector3.Lerp(new Vector3(0.82f, 0.56f, 0.46f), new Vector3(0.7f, 0.5f, 0.44f), random.Float());
                    k.Fresh = new Vector3(0.92f, 0.6f, 0.46f);
                    k.Roof = new VertexMaterial(MaterialCatalog.RoofTiles.Index, new Colour(k.RoofColour));
                    k.Ridge = new VertexMaterial(MaterialCatalog.RoofTiles.Index, new Colour(k.RoofColour * 0.86f));
                    k.Cut = new VertexMaterial(MaterialCatalog.RoofTiles.Index, new Colour(k.RoofColour * 0.72f));
                    k.Moss = new Vector3(0.8f, 0.92f, 0.68f);
                    k.Pitch = random.Range(0.76f, 0.86f);
                    k.Eave = 0.45f;
                    k.Verge = 0.38f;
                    k.Thick = 0.2f;
                    k.Sag *= 0.6f;
                    break;
            }
            return k;
        }

        // ---------------------------------------------------------------- Walls

        // A plinth of field stone, a hand proud of the wall, going well below the ground so that a slope never shows daylight under it.
        private static void PlinthOf(MeshBuilder b, Look k, float cx, float cz, float hx, float hz)
        {
            b.Box(new Vector3(cx, (PlinthTop - 2.4f) * 0.5f, cz), new Vector3(hx + 0.13f, (PlinthTop + 2.4f) * 0.5f, hz + 0.13f), k.Plinth);
        }

        /// <summary>
        /// A wall's frame, standing proud of the lime between its timbers: a sill on the plinth, a plate under the roof, a rail between where there's height for one, posts at its
        /// ends and either side of whatever's let into it, and between them either braces in the end bays or studs set close, as whoever built it liked.
        /// </summary>
        private static void Frame(MeshBuilder b, Look k, Wall wall, float y0, float y1, List<Gap> gaps, WorldRandom random, bool close)
        {
            float from = -wall.Half, to = wall.Half;
            float tall = y1 - y0;
            float rail = tall > 4.4f ? y0 + tall * 0.5f : float.NaN;
            void Along(float a0, float a1, float y, float half)
            {
                if (a1 - a0 < 0.1f) return;
                HallDressing.Box(b, wall.At((a0 + a1) * 0.5f, y, Proud * 0.5f), wall.A, wall.N, (a1 - a0) * 0.5f, half, Proud, k.Timber);
            }
            void Up(float a, float ya, float yb, float half)
            {
                if (yb - ya < 0.1f) return;
                HallDressing.Box(b, wall.At(a, (ya + yb) * 0.5f, Proud * 0.5f), wall.A, wall.N, half, (yb - ya) * 0.5f, Proud * 0.92f, k.Timber);
            }
            // Rails, broken by whatever crosses them.
            foreach (float y in new[] { y0 + T, y1 - T, rail })
            {
                if (float.IsNaN(y)) continue;
                float at = from;
                foreach (Gap gap in gaps.Where(g => g.Sill < y + T && g.Head > y - T).OrderBy(g => g.Along))
                {
                    Along(at, gap.Along - gap.Half - 0.1f, y, T);
                    at = gap.Along + gap.Half + 0.1f;
                }
                Along(at, to, y, T);
            }
            // Posts.
            var posts = new List<float> { from + T, to - T };
            foreach (Gap gap in gaps)
            {
                posts.Add(gap.Along - gap.Half - T - 0.02f);
                posts.Add(gap.Along + gap.Half + T + 0.02f);
            }
            posts = posts.Where(a => a >= from + T - 0.01f && a <= to - T + 0.01f).OrderBy(a => a).ToList();
            float spacing = close ? 0.95f : 2.3f;
            var all = new List<float>(posts);
            for (int i = 0; i + 1 < posts.Count; i++)
            {
                float a0 = posts[i], a1 = posts[i + 1];
                float middle = (a0 + a1) * 0.5f;
                if (gaps.Any(g => MathF.Abs(g.Along - middle) < g.Half + 0.05f)) continue;
                int extra = (int)((a1 - a0) / spacing);
                for (int j = 1; j <= extra; j++) all.Add(a0 + (a1 - a0) * j / (extra + 1));
            }
            all.Sort();
            foreach (float a in all)
            {
                if (gaps.Any(g => MathF.Abs(g.Along - a) < g.Half - 0.01f)) continue;
                Up(a, y0 + T * 2f, y1 - T * 2f, posts.Contains(a) ? T : T * 0.78f);
            }
            // Under and over what's let in: a stud under each sill and one over each head.
            foreach (Gap gap in gaps)
            {
                if (gap.Sill > y0 + 0.9f) Up(gap.Along, y0 + T * 2f, gap.Sill - 0.3f, T * 0.7f);
                if (gap.Head < y1 - 0.9f) Up(gap.Along, gap.Head + 0.3f, y1 - T * 2f, T * 0.7f);
            }
            if (close) return;
            // Braces, up from the foot of each end post towards the middle, wherever nothing's in their way.
            float reach = MathF.Min(1.9f, wall.Half * 0.6f);
            float top = float.IsNaN(rail) ? y1 - T * 2f : rail - T;
            foreach (float s in new[] { -1f, 1f })
            {
                float foot = s * (wall.Half - T * 2f), headAt = foot - s * reach;
                if (gaps.Any(g => MathF.Min(foot, headAt) < g.Along + g.Half + 0.2f && MathF.Max(foot, headAt) > g.Along - g.Half - 0.2f && g.Sill < top)) continue;
                b.Beam(wall.At(foot, top, Proud * 0.5f), wall.At(headAt, y0 + T * 2f, Proud * 0.5f), T * 0.72f, k.Timber);
            }
        }

        // Posts up the corners of a boarded wall, and a board along its foot and its head.
        private static void CornerPosts(MeshBuilder b, Look k, Wall wall, float y0, float y1)
        {
            foreach (float s in new[] { -1f, 1f })
            {
                HallDressing.Box(b, wall.At(s * (wall.Half - T), (y0 + y1) * 0.5f, Proud * 0.5f), wall.A, wall.N, T, (y1 - y0) * 0.5f, Proud, k.Timber);
            }
            HallDressing.Box(b, wall.At(0f, y1 - T, Proud * 0.5f), wall.A, wall.N, wall.Half, T, Proud * 0.9f, k.Timber);
        }

        // Dressed stones up the corners of a stone wall, long and short in turn.
        private static void Quoins(MeshBuilder b, Look k, Wall wall, float y0, float y1)
        {
            int course = 0;
            VertexMaterial stone = Hall.Stone(198, 188, 170);
            for (float y = y0 + 0.1f; y < y1 - 0.5f; y += 0.82f, course++)
            {
                float length = course % 2 == 0 ? 0.52f : 0.3f;
                foreach (float s in new[] { -1f, 1f })
                {
                    HallDressing.Box(b, wall.At(s * (wall.Half - length), y + 0.36f, 0.025f), wall.A, wall.N, length, 0.36f, 0.05f, stone);
                }
            }
        }

        // ---------------------------------------------------------------- Doors and windows

        /// <summary>
        /// A window of two lights: glass with a room's light behind it, a frame, a post between the lights and a bar across them, a sill that throws the rain off, and, where it's
        /// asked, shutters folded back against the wall in the house's own colour and a box of flowers under it.
        /// </summary>
        private static void Window(MeshBuilder b, Look k, Wall wall, Gap gap, bool shutters, bool flowers, bool shines, WorldRandom random)
        {
            float middle = (gap.Sill + gap.Head) * 0.5f, halfH = (gap.Head - gap.Sill) * 0.5f, halfW = gap.Half;
            Vector3 centre = wall.At(gap.Along, middle, 0.03f);
            Hall.Face(b, centre - wall.A * halfW - Vector3.UnitY * halfH, centre + wall.A * halfW - Vector3.UnitY * halfH, centre + wall.A * halfW + Vector3.UnitY * halfH,
                centre - wall.A * halfW + Vector3.UnitY * halfH, wall.N, k.Glass);
            if (shines && k.WindowLights > 0)
            {
                k.Lights.Add(AssetLight.Window(wall.At(gap.Along, middle, 0.5f), wall.N));
                k.WindowLights--;
            }
            const float F = 0.075f;
            Vector3 P(float a, float y) => wall.At(gap.Along + a, middle + y, 0.08f);
            HallDressing.Box(b, P(0f, halfH - F), wall.A, wall.N, halfW, F, 0.07f, k.Joinery);
            HallDressing.Box(b, P(0f, -halfH + F), wall.A, wall.N, halfW, F, 0.07f, k.Joinery);
            foreach (float s in new[] { -1f, 0f, 1f }) HallDressing.Box(b, P(s * (halfW - F), 0f), wall.A, wall.N, s == 0f ? F * 0.8f : F, halfH - F * 2f, 0.07f, k.Joinery);
            HallDressing.Box(b, P(0f, halfH * 0.3f), wall.A, wall.N, halfW - F * 2f, F * 0.55f, 0.06f, k.Joinery);
            // Its sill, proud of everything.
            HallDressing.Box(b, wall.At(gap.Along, gap.Sill - 0.09f, 0.16f), wall.A, wall.N, halfW + 0.22f, 0.09f, 0.2f, k.Joinery);

            if (shutters)
            {
                float width = halfW - 0.04f;
                foreach (float s in new[] { -1f, 1f })
                {
                    float at = gap.Along + s * (halfW + 0.14f + width * 0.5f);
                    HallDressing.Box(b, wall.At(at, middle, 0.1f), wall.A, wall.N, width * 0.5f, halfH, 0.045f, k.Shutter);
                    foreach (float y in new[] { -halfH * 0.62f, halfH * 0.62f }) HallDressing.Box(b, wall.At(at, middle + y, 0.155f), wall.A, wall.N, width * 0.5f, 0.07f, 0.02f, k.Joinery);
                }
            }
            if (flowers) FlowerBox(b, k, wall, gap.Along, gap.Sill - 0.2f, halfW + 0.2f, random);
        }

        // A box on brackets, full of whatever's in flower: leaves in a low hedge of them, and blooms of mostly one colour standing out of it.
        private static void FlowerBox(MeshBuilder b, Look k, Wall wall, float along, float top, float half, WorldRandom random)
        {
            HallDressing.Box(b, wall.At(along, top - 0.24f, 0.4f), wall.A, wall.N, half, 0.24f, 0.28f, k.Joinery);
            foreach (float s in new[] { -0.7f, 0.7f }) b.Beam(wall.At(along + s * half, top - 0.55f, 0.04f), wall.At(along + s * half, top - 0.5f, 0.6f), 0.045f, k.Joinery);
            Vector3[] blooms = { new(214, 96, 132), new(240, 226, 212), new(226, 164, 60), new(150, 96, 190), new(222, 92, 78) };
            Vector3 bloom = random.Pick(blooms);
            int count = Math.Max(5, (int)(half * 10f));
            for (int i = 0; i < count; i++)
            {
                float a = along + random.Range(-half + 0.1f, half - 0.1f);
                float out_ = random.Range(0.22f, 0.6f);
                float height = random.Range(0.16f, 0.46f);
                Vector3 foot = wall.At(a, top - 0.04f, out_);
                TownPieces.Ball(b, foot + Vector3.UnitY * (height * 0.45f), new Vector3(0.17f, height * 0.5f, 0.16f), TownPieces.Leaf(random));
                if (!random.Chance(0.8f)) continue;
                Vector3 colour = random.Chance(0.2f) ? random.Pick(blooms) : bloom;
                b.Box(foot + Vector3.UnitY * (height * 0.88f) + wall.N * random.Range(-0.03f, 0.06f), new Vector3(0.1f, 0.075f, 0.1f), Hall.Flat((int)colour.X, (int)colour.Y, (int)colour.Z));
            }
        }

        /// <summary>
        /// The door: boards on ledges in the house's own colour, between posts under a lintel, a worn stone step up to it, and a lantern on the wall beside it, which is the one
        /// light a home keeps burning.
        /// </summary>
        private static void Door(MeshBuilder b, Look k, Wall wall, float along, WorldRandom random)
        {
            float top = Floor + DoorHead;
            HallDressing.Box(b, wall.At(along, (Floor + top) * 0.5f, 0.04f), wall.A, wall.N, DoorHalf, (top - Floor) * 0.5f, 0.05f, k.Door);
            // Its boards' joints, its ledges and a ring to pull it to.
            for (int i = 1; i < 5; i++) HallDressing.Box(b, wall.At(along - DoorHalf + 2f * DoorHalf * i / 5f, (Floor + top) * 0.5f, 0.095f), wall.A, wall.N, 0.018f, (top - Floor) * 0.5f - 0.1f, 0.006f, k.Joinery);
            foreach (float y in new[] { Floor + 0.9f, Floor + DoorHead * 0.5f, top - 0.9f }) HallDressing.Box(b, wall.At(along, y, 0.11f), wall.A, wall.N, DoorHalf - 0.08f, 0.1f, 0.025f, k.Joinery);
            b.Tube(wall.At(along + DoorHalf - 0.42f, Floor + 2.6f, 0.13f), wall.At(along + DoorHalf - 0.42f, Floor + 2.6f, 0.19f), 0.11f, 0.11f, 7, Hall.Iron(92));
            foreach (float s in new[] { -1f, 1f }) HallDressing.Box(b, wall.At(along + s * (DoorHalf + 0.17f), (PlinthTop * 0.3f + top) * 0.5f, 0.08f), wall.A, wall.N, 0.17f, (top - PlinthTop * 0.3f) * 0.5f, 0.1f, k.Timber);
            HallDressing.Box(b, wall.At(along, top + 0.2f, 0.09f), wall.A, wall.N, DoorHalf + 0.5f, 0.2f, 0.11f, k.Timber);
            // Two steps of stone, each one slab, the lower one broader.
            var stone = PlazaStone.Dressed(random, 0.2f, 0.06f);
            Vector3 foot = wall.At(along, 0f, 0f);
            float yaw = MathF.Atan2(wall.N.X, wall.N.Z);
            Hall.At(b, foot, yaw, () =>
            {
                PlazaStone.Block(b, new Vector3(0f, (Floor - 0.5f) * 0.5f, 0.42f), new Vector3(DoorHalf + 0.35f, (Floor + 0.5f) * 0.5f, 0.42f), 0f, 0.05f, stone);
                PlazaStone.Block(b, new Vector3(0f, (Floor * 0.5f - 0.5f) * 0.5f + 0.07f, 1.1f), new Vector3(DoorHalf + 0.7f, (Floor * 0.5f + 0.5f) * 0.5f + 0.07f, 0.4f), 0f, 0.05f, PlazaStone.Dressed(random, 0.1f, 0.06f));
            });
            HallDressing.WallLantern(b, wall.At(along + (along > 0f ? -1f : 1f) * (DoorHalf + 1f), Floor + 4.9f, 0f), wall.N, k.Lights);
        }

        // A little gabled roof over the door on two brackets, short enough that the door's still seen under it from above.
        private static void Hood(MeshBuilder b, Look k, Wall wall, float along, WorldRandom random)
        {
            float eave = Floor + DoorHead + 0.75f, half = DoorHalf + 0.75f, reach = 1.05f, rise = half * 0.62f;
            Vector3 P(float a, float y, float out_) => wall.At(along + a, y, out_);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 e0 = P(s * half, eave, 0f), e1 = P(s * half, eave, reach), r0 = P(0f, eave + rise, 0f), r1 = P(0f, eave + rise, reach);
                Vector3 normal = (Vector3.UnitY + wall.A * (s * 0.7f)).Normalized();
                Hall.Face(b, e1, e0, r0, r1, normal, k.Roof);
                Vector3 drop = Vector3.UnitY * MathF.Min(k.Thick, 0.3f);
                Hall.Face(b, e1 - drop, e1, r1, r1 - drop, wall.N, k.Cut);
                Hall.Face(b, e0 - drop, e1 - drop, e1, e0, wall.A * s, k.Cut);
                // A bracket under each foot of it.
                b.Beam(P(s * (half - 0.25f), eave - 1.3f, 0.06f), P(s * (half - 0.25f), eave - 0.2f, reach - 0.15f), 0.08f, k.Timber);
                b.Beam(P(s * (half - 0.25f), eave - 0.22f, 0.05f), P(s * (half - 0.25f), eave - 0.22f, reach - 0.05f), 0.08f, k.Timber);
            }
            b.Tube(P(0f, eave + rise + 0.02f, 0f), P(0f, eave + rise + 0.02f, reach + 0.05f), 0.13f, 0.13f, 6, k.Ridge);
            Hall.Tri(b, P(-half + 0.2f, eave - 0.05f, reach - 0.06f), P(half - 0.2f, eave - 0.05f, reach - 0.06f), P(0f, eave + rise - 0.2f, reach - 0.06f), wall.N, k.Boards);
        }

        // A shop's front: a wide window whose shutter is let down as a counter, things on it, and an awning over it on two poles.
        private static void ShopFront(MeshBuilder b, Look k, Wall wall, Gap gap, WorldRandom random)
        {
            float halfW = gap.Half;
            Vector3 centre = wall.At(gap.Along, (gap.Sill + gap.Head) * 0.5f, 0.03f);
            float halfH = (gap.Head - gap.Sill) * 0.5f;
            Hall.Face(b, centre - wall.A * halfW - Vector3.UnitY * halfH, centre + wall.A * halfW - Vector3.UnitY * halfH, centre + wall.A * halfW + Vector3.UnitY * halfH,
                centre - wall.A * halfW + Vector3.UnitY * halfH, wall.N, Hall.Flat(34, 28, 24));
            if (k.WindowLights > 0)
            {
                k.Lights.Add(AssetLight.Window(wall.At(gap.Along, gap.Sill + halfH, 0.6f), wall.N));
                k.WindowLights--;
            }
            HallDressing.Box(b, wall.At(gap.Along, gap.Head + 0.14f, 0.08f), wall.A, wall.N, halfW + 0.2f, 0.14f, 0.1f, k.Timber);
            foreach (float s in new[] { -1f, 0f, 1f }) HallDressing.Box(b, wall.At(gap.Along + s * halfW, (gap.Sill + gap.Head) * 0.5f, 0.08f), wall.A, wall.N, 0.1f, halfH, 0.09f, k.Timber);
            // The counter, on two struts, and what's set out on it.
            float counter = gap.Sill;
            HallDressing.Box(b, wall.At(gap.Along, counter - 0.06f, 0.55f), wall.A, wall.N, halfW, 0.06f, 0.55f, k.Shutter);
            foreach (float s in new[] { -0.8f, 0.8f }) b.Beam(wall.At(gap.Along + s * halfW, counter - 1.1f, 0.05f), wall.At(gap.Along + s * halfW, counter - 0.1f, 0.95f), 0.06f, k.Timber);
            for (float a = -halfW + 0.45f; a < halfW - 0.3f; a += random.Range(0.6f, 0.85f))
            {
                Vector3 at = wall.At(gap.Along + a, counter, random.Range(0.3f, 0.8f));
                if (random.Chance(0.55f)) b.Tube(at, at + Vector3.UnitY * 0.2f, 0.26f, 0.3f, 9, Hall.Wood(168, 132, 86));
                else b.Box(at + Vector3.UnitY * 0.13f, new Vector3(0.24f, 0.13f, 0.2f), Hall.Wood(150, 112, 74));
                Vector3 ware = random.Pick(new[] { new Vector3(196, 146, 84), new Vector3(214, 176, 112), new Vector3(170, 60, 50), new Vector3(96, 130, 60), new Vector3(226, 208, 176) });
                b.Box(at + Vector3.UnitY * 0.3f, new Vector3(0.18f, 0.09f, 0.16f), Hall.Flat((int)ware.X, (int)ware.Y, (int)ware.Z));
            }
            Awning(b, wall, gap.Along, halfW + 0.3f, gap.Head + 0.5f, 1.9f, random);
        }

        /// <summary>A striped awning out from a wall, on two poles, with its hem cut in points: cream and one of the Haven's dyes.</summary>
        internal static void Awning(MeshBuilder b, Wall wall, float along, float half, float top, float reach, WorldRandom random)
        {
            Vector3 dye = Paints[random.Int(5)] / 255f;
            float drop = 0.75f;
            int stripes = Math.Max(4, (int)(half * 2f / 0.5f));
            for (int i = 0; i < stripes; i++)
            {
                float a0 = along - half + 2f * half * i / stripes, a1 = along - half + 2f * half * (i + 1) / stripes;
                VertexMaterial cloth = PlazaStone.Cloth(i % 2 == 0 ? dye : new Vector3(0.84f, 0.8f, 0.7f));
                Vector3 w0 = wall.At(a0, top, 0.05f), w1 = wall.At(a1, top, 0.05f), e0 = wall.At(a0, top - drop, reach), e1 = wall.At(a1, top - drop, reach);
                Vector3 up = (Vector3.UnitY + wall.N * 0.4f).Normalized();
                Hall.Face(b, e0, e1, w1, w0, up, cloth);
                Hall.Face(b, e0, e1, w1, w0, -up, cloth);
                Vector3 tip = (e0 + e1) * 0.5f - Vector3.UnitY * 0.3f;
                Hall.Tri(b, e0, e1, tip, wall.N, cloth);
                Hall.Tri(b, e0, e1, tip, -wall.N, cloth);
            }
            VertexMaterial pole = Hall.Wood(132, 104, 76);
            foreach (float s in new[] { -1f, 1f }) b.Tube(wall.At(along + s * (half - 0.1f), 0f, reach - 0.05f), wall.At(along + s * (half - 0.1f), top - drop + 0.03f, reach - 0.05f), 0.07f, 0.06f, 6, pole);
            b.Tube(wall.At(along - half, top - drop, reach - 0.05f), wall.At(along + half, top - drop, reach - 0.05f), 0.045f, 0.045f, 5, pole);
        }

        // A workshop's open end: the wall left out between two posts under a heavy beam, the floor inside dark, and its doors folded back against the wall.
        private static void OpenBay(MeshBuilder b, Look k, Wall wall, Gap gap, float hz, WorldRandom random)
        {
            float halfW = gap.Half, halfH = (gap.Head - gap.Sill) * 0.5f;
            Vector3 centre = wall.At(gap.Along, gap.Sill + halfH, 0.03f);
            Hall.Face(b, centre - wall.A * halfW - Vector3.UnitY * halfH, centre + wall.A * halfW - Vector3.UnitY * halfH, centre + wall.A * halfW + Vector3.UnitY * halfH,
                centre - wall.A * halfW + Vector3.UnitY * halfH, wall.N, Hall.Flat(30, 25, 21));
            HallDressing.Box(b, wall.At(gap.Along, gap.Head + 0.2f, 0.09f), wall.A, wall.N, halfW + 0.3f, 0.22f, 0.12f, k.Timber);
            foreach (float s in new[] { -1f, 1f })
            {
                HallDressing.Box(b, wall.At(gap.Along + s * halfW, gap.Sill + halfH, 0.09f), wall.A, wall.N, 0.16f, halfH, 0.12f, k.Timber);
                b.Beam(wall.At(gap.Along + s * (halfW - 0.2f), gap.Head - 1.3f, 0.1f), wall.At(gap.Along + s * (halfW - 1.4f), gap.Head - 0.05f, 0.1f), 0.09f, k.Timber);
                // A leaf of its doors, swung right back.
                HallDressing.Box(b, wall.At(gap.Along + s * (halfW + 0.3f + halfW * 0.46f), gap.Sill + halfH - 0.1f, 0.12f), wall.A, wall.N, halfW * 0.46f, halfH - 0.15f, 0.05f, k.Door);
            }
            // What's being made, seen just inside: a bench with a board on it.
            VertexMaterial pale = Hall.Wood(190, 156, 112);
            HallDressing.Box(b, wall.At(gap.Along - halfW * 0.25f, gap.Sill + 1.85f, 0.25f), wall.A, wall.N, halfW * 0.55f, 0.07f, 0.4f, pale);
            foreach (float s in new[] { -1f, 1f }) HallDressing.Box(b, wall.At(gap.Along - halfW * 0.25f + s * halfW * 0.45f, gap.Sill + 0.9f, 0.25f), wall.A, wall.N, 0.07f, 0.9f, 0.3f, k.Timber);
        }

        // ---------------------------------------------------------------- The roof

        /// <summary>
        /// A roof of two slopes over walls of half (<paramref name="hx"/>, <paramref name="hz"/>) about (<paramref name="cx"/>, <paramref name="cz"/>), its ridge along x: the coat
        /// itself, its cut edges along the eaves and up the verges, its ridge as its roofing is ridged, and the gables under it. Returns how high the ridge stands.
        /// </summary>
        private static float Roof(MeshBuilder b, Look k, float cx, float cz, float hx, float hz, float wallTop, WorldRandom random, List<(float X, float Half)> brows, float sootAt,
            bool gables, bool mended, bool loftWindow = false)
        {
            float length = hx + k.Verge, reach = hz + k.Eave;
            float eaveY = wallTop + k.Thick - k.Eave * k.Pitch + 0.12f;
            float ridgeY = wallTop + k.Thick + hz * k.Pitch + 0.12f;
            float slope = MathF.Sqrt(reach * reach + (ridgeY - eaveY) * (ridgeY - eaveY));
            float seedA = random.Range(0f, 50f), seedB = random.Range(0f, 50f);
            bool thatch = k.Roofing == Roofing.Thatch;
            float mendFrom = mended ? random.Range(0.42f, 0.6f) : 2f;
            float noiseAt = k.NoiseAt + cx * 3f + cz * 7f;

            // How far a brow lifts the eaves at x, and the coat with them for a way up the slope.
            float Brow(float x)
            {
                float lift = 0f;
                foreach ((float at, float half) in brows)
                {
                    float d = MathF.Abs(x - cx - at) / (half + 0.9f);
                    if (d < 1f) lift = MathF.Max(lift, (thatch ? 1.55f : 0f) * MathF.Pow(MathF.Cos(d * MathF.PI * 0.5f), 2f));
                }
                return lift;
            }
            Vector3 Point(float u, float v, float side)
            {
                float x = cx + (u * 2f - 1f) * length;
                float y = eaveY + (ridgeY - eaveY) * v;
                float z = cz + side * reach * (1f - v);
                float edge = MathF.Min(MathF.Min(u, 1f - u) * 7f, 1f);
                y -= k.Sag * MathF.Pow(MathF.Sin(u * MathF.PI), 2f) * v * MathF.Sqrt(v);
                if (k.Lumps > 0f) y += k.Lumps * edge * MathF.Min(v * 6f, 1f) * (k.Noise.Fbm(x + noiseAt, z * 1.3f + (side > 0f ? seedA : seedB), 2.1f, 2) + 0.5f * MathF.Sin(x * 1.7f + z + seedA));
                if (side > 0f) y += Brow(x) * MathF.Pow(MathF.Max(1f - v / 0.62f, 0f), 2f);
                return new Vector3(x, y, z);
            }
            Func<Vector3, Colour> Tint(float side, Vector3 colour)
            {
                bool shaded = side < 0f;
                return p =>
                {
                    float down = Math.Clamp((ridgeY - p.Y) / MathF.Max(ridgeY - eaveY, 0.1f), 0f, 1f);
                    float across = p.X + noiseAt + (shaded ? 37f : 0f);
                    float broad = k.Noise.Fbm(across, p.Y * 1.6f, 5f, 2);
                    float streak = k.Noise.Fbm(across * 1f, p.Y * 0.22f, 0.8f, 2);
                    float patch = k.Noise.Fbm01(across + 71f, p.Y * 1.5f + 19f, 2.6f, 3);
                    float moss = Math.Clamp((patch - 0.56f) * 3.4f, 0f, 1f) * (0.25f + 0.75f * down) * (shaded ? 1f : 0.4f) * k.Mossy;
                    float tone = 1.05f - 0.16f * down * down + broad * 0.1f + streak * 0.07f;
                    Vector3 c = colour;
                    // What was laid this year is brighter than what's weathered ten.
                    if (!shaded && (p.X - cx + length) / (2f * length) > mendFrom) { c = k.Fresh; moss = 0f; tone = 1f + streak * 0.04f; }
                    if (!float.IsNaN(sootAt))
                    {
                        float aside = MathF.Abs(p.X - cx - sootAt);
                        tone *= 1f - 0.3f * Math.Clamp(1f - aside / 1.5f, 0f, 1f) * Math.Clamp(1f - down * 2.2f, 0f, 1f);
                    }
                    c *= tone;
                    c = Vector3.Lerp(c, c * k.Moss, moss);
                    return new Colour(c);
                };
            }

            int nu = Math.Clamp((int)(2f * length / 0.6f), 8, 56), nv = Math.Clamp((int)(slope / 0.62f), 5, 16);
            Func<Vector3, Colour> south = Tint(1f, k.RoofColour), north = Tint(-1f, k.RoofColour);
            b.Surface(nu, nv, (u, v) => Point(u, v, 1f), k.Roof, south);
            b.Surface(nu, nv, (u, v) => Point(1f - u, v, -1f), k.Roof, north);

            // Its thickness: along the eaves, where the reed's cut ends show, and up both verges.
            Vector3 under = new Vector3(0f, -k.Thick * 1.08f, 0f);
            for (int i = 0; i < nu; i++)
            {
                float u0 = (float)i / nu, u1 = (float)(i + 1) / nu;
                foreach (float side in new[] { 1f, -1f })
                {
                    Vector3 a = Point(u0, 0f, side), c = Point(u1, 0f, side);
                    Vector3 back = new Vector3(0f, 0f, -side * (thatch ? 0.2f : 0.03f));
                    Hall.Face(b, a + under + back, c + under + back, c, a, new Vector3(0f, -0.2f, side), k.Cut);
                }
            }
            for (int j = 0; j < nv; j++)
            {
                float v0 = (float)j / nv, v1 = (float)(j + 1) / nv;
                foreach (float side in new[] { 1f, -1f })
                {
                    foreach (float end in new[] { 0f, 1f })
                    {
                        Vector3 a = Point(end, v0, side), c = Point(end, v1, side);
                        Hall.Face(b, a + under, c + under, c, a, new Vector3(end * 2f - 1f, 0f, 0f), k.Cut);
                    }
                }
            }

            // The ridge.
            float RidgeAt(float u) => Point(u, 1f, 1f).Y;
            int spans = Math.Max(6, (int)(2f * length / 0.55f));
            switch (k.Roofing)
            {
                case Roofing.Thatch:
                    ThatchRidge(b, k, Point, slope, spans, random);
                    break;
                case Roofing.Tile:
                    // Half-round tiles, each lapping the next.
                    for (int i = 0; i < spans; i++)
                    {
                        float u0 = (float)i / spans, u1 = (float)(i + 1) / spans;
                        VertexMaterial tile = Shaded(k.Ridge, random.Range(0.88f, 1.1f));
                        b.Tube(new Vector3(cx + (u0 * 2f - 1f) * length - 0.02f, RidgeAt(u0) - 0.04f, cz), new Vector3(cx + (u1 * 2f - 1f) * length, RidgeAt(u1) - 0.04f, cz), 0.25f, 0.21f, 7, tile, capped: false);
                    }
                    break;
                case Roofing.Slate:
                    for (int i = 0; i < spans; i += 2)
                    {
                        float u0 = (float)i / spans, u1 = MathF.Min((float)(i + 2) / spans, 1f);
                        b.Tube(new Vector3(cx + (u0 * 2f - 1f) * length + 0.012f, RidgeAt(u0) - 0.03f, cz), new Vector3(cx + (u1 * 2f - 1f) * length - 0.012f, RidgeAt(u1) - 0.03f, cz), 0.19f, 0.19f, 6, Shaded(k.Ridge, random.Range(0.92f, 1.06f)));
                    }
                    break;
                default:
                    // Two boards nailed along it, one lapping the other.
                    foreach (float side in new[] { 1f, -1f })
                    {
                        for (int i = 0; i < spans; i += 3)
                        {
                            float u0 = (float)i / spans, u1 = MathF.Min((float)(i + 3) / spans, 1f);
                            Vector3 from = new Vector3(cx + (u0 * 2f - 1f) * length, RidgeAt(u0) + 0.02f, cz + side * 0.13f), to = new Vector3(cx + (u1 * 2f - 1f) * length, RidgeAt(u1) + 0.02f, cz + side * 0.13f);
                            Hall.Board(b, from - Vector3.UnitY * 0.1f, to - Vector3.UnitY * 0.1f, new Vector3(0f, -k.Pitch, side).Normalized(), 0.19f, 0.03f, Shaded(k.Ridge, random.Range(0.9f, 1.08f)));
                        }
                    }
                    break;
            }

            if (!gables) return ridgeY;
            // The gables: lime between a pair of rafters, a collar across them and a post up to the ridge, and a window to the loft where there's one.
            float apex = wallTop + hz * k.Pitch;
            foreach (float end in new[] { -1f, 1f })
            {
                float x = cx + end * hx;
                VertexMaterial fill = k.Walling == Walling.Boarded ? k.Boards : k.Walling == Walling.Stone ? k.Stone : k.Lime;
                Hall.Tri(b, new Vector3(x, wallTop, cz + hz), new Vector3(x, wallTop, cz - hz), new Vector3(x, apex, cz), Vector3.UnitX * end, fill);
                float px = x + end * Proud * 0.5f;
                foreach (float side in new[] { 1f, -1f }) b.Beam(new Vector3(px, wallTop + 0.05f, cz + side * (hz - 0.1f)), new Vector3(px, apex - 0.15f, cz), T * 0.9f, k.Timber);
                b.Beam(new Vector3(px, wallTop + T, cz - hz), new Vector3(px, wallTop + T, cz + hz), T, k.Timber);
                float collar = wallTop + (apex - wallTop) * 0.52f;
                float halfCollar = hz * (1f - 0.52f) - 0.1f;
                b.Beam(new Vector3(px, collar, cz - halfCollar), new Vector3(px, collar, cz + halfCollar), T * 0.8f, k.Timber);
                b.Beam(new Vector3(px, collar, cz), new Vector3(px, apex - 0.2f, cz), T * 0.75f, k.Timber);
                if (loftWindow && end > 0f)
                {
                    // Seen from the street: the loft's little window, in the gable under the collar.
                    Vector3 w = new Vector3(x + end * 0.035f, wallTop + (collar - wallTop) * 0.5f + 0.1f, cz);
                    float hw = MathF.Min(0.62f, hz * 0.3f), hh = MathF.Min(0.7f, (collar - wallTop) * 0.36f);
                    Hall.Face(b, w + new Vector3(0f, -hh, end * hw), w + new Vector3(0f, -hh, -end * hw), w + new Vector3(0f, hh, -end * hw), w + new Vector3(0f, hh, end * hw), Vector3.UnitX * end, k.Glass);
                    foreach (float s in new[] { -1f, 0f, 1f }) b.Beam(w + new Vector3(end * 0.05f, -hh, s * hw), w + new Vector3(end * 0.05f, hh, s * hw), 0.055f, k.Joinery);
                    foreach (float s in new[] { -1f, 1f }) b.Beam(w + new Vector3(end * 0.05f, s * hh, -hw), w + new Vector3(end * 0.05f, s * hh, hw), 0.055f, k.Joinery);
                }
                else
                {
                    foreach (float s in new[] { -0.5f, 0.5f }) b.Beam(new Vector3(px, wallTop + T * 2f, cz + s * hz), new Vector3(px, wallTop + (apex - wallTop) * 0.5f - 0.15f, cz + s * hz), T * 0.7f, k.Timber);
                }
            }
            return ridgeY;
        }

        /// <summary>
        /// A thatcher's block ridge: a second coat laid over the top of both slopes a hand proud of the first, its lower edge cut in points and scallops, two hazel rods pinned
        /// along each side of it with crossed rods between them, and a roll along the very top.
        /// </summary>
        private static void ThatchRidge(MeshBuilder b, Look k, Func<float, float, float, Vector3> point, float slope, int spans, WorldRandom random)
        {
            const float Lift = 0.1f;
            float deep = Math.Clamp(1.2f / slope, 0.1f, 0.3f);
            int cuts = spans * 2;
            float period = 1f / MathF.Max(spans / 2, 1);
            // How far down the slope its edge comes at u: points between scallops.
            float Edge(float u)
            {
                float t = u / period % 1f;
                return deep * (0.7f + 0.3f * MathF.Abs(MathF.Sin(t * MathF.PI)) + (t < 0.08f || t > 0.92f ? 0.06f : 0f));
            }
            Vector3 lift = Vector3.UnitY * Lift;
            foreach (float side in new[] { 1f, -1f })
            {
                Vector3 outward = new Vector3(0f, 1f, side * k.Pitch).Normalized();
                for (int i = 0; i < cuts; i++)
                {
                    float u0 = (float)i / cuts, u1 = (float)(i + 1) / cuts;
                    Vector3 r0 = point(u0, 1f, side) + lift, r1 = point(u1, 1f, side) + lift;
                    Vector3 e0 = point(u0, 1f - Edge(u0), side) + lift * 0.9f, e1 = point(u1, 1f - Edge(u1), side) + lift * 0.9f;
                    Hall.Face(b, e0, e1, r1, r0, outward, k.Ridge);
                    Hall.Face(b, e0 - lift * 0.9f, e1 - lift * 0.9f, e1, e0, new Vector3(0f, 0.3f, side), Shaded(k.Ridge, 0.7f));
                }
                // The rods.
                float[] at = { deep * 0.22f, deep * 0.62f };
                for (int i = 0; i < spans; i++)
                {
                    float u0 = (float)i / spans, u1 = (float)(i + 1) / spans;
                    Vector3 a0 = point(u0, 1f - at[0], side) + lift * 1.25f, a1 = point(u1, 1f - at[0], side) + lift * 1.25f;
                    Vector3 c0 = point(u0, 1f - at[1], side) + lift * 1.25f, c1 = point(u1, 1f - at[1], side) + lift * 1.25f;
                    b.Beam(a0, a1, 0.024f, k.Rod);
                    b.Beam(c0, c1, 0.024f, k.Rod);
                    if (i % 2 == 0) b.Beam(a0, c1, 0.018f, k.Rod);
                    else b.Beam(c0, a1, 0.018f, k.Rod);
                }
            }
            for (int i = 0; i < spans; i++)
            {
                float u0 = (float)i / spans, u1 = (float)(i + 1) / spans;
                b.Tube(point(u0, 1f, 1f) + lift * 0.7f, point(u1, 1f, 1f) + lift * 0.7f, 0.2f, 0.2f, 6, k.Ridge, capped: i == 0 || i == spans - 1);
            }
        }

        // The wall carried up under a brow in the thatch, and the loft's window in it.
        private static void BrowWindow(MeshBuilder b, Look k, Wall wall, float along, float half, float wallTop)
        {
            const int Steps = 6;
            float wide = half + 0.75f;
            for (int i = 0; i < Steps; i++)
            {
                float a0 = -wide + 2f * wide * i / Steps, a1 = -wide + 2f * wide * (i + 1) / Steps;
                float Lift(float a) => 1.42f * MathF.Pow(MathF.Cos(Math.Clamp(MathF.Abs(a) / (half + 0.9f), 0f, 1f) * MathF.PI * 0.5f), 2f);
                Hall.Face(b, wall.At(along + a0, wallTop - 0.3f, 0.02f), wall.At(along + a1, wallTop - 0.3f, 0.02f), wall.At(along + a1, wallTop + Lift(a1), 0.02f), wall.At(along + a0, wallTop + Lift(a0), 0.02f), wall.N, k.Lime);
            }
            float sill = wallTop - 0.5f, head = wallTop + 0.95f;
            Window(b, k, wall, new Gap(along, half * 0.62f, sill, head), shutters: false, flowers: false, shines: false, new WorldRandom(7));
        }

        // A dormer standing out of the front slope, with a little gable of its own.
        private static void Dormer(MeshBuilder b, Look k, float along, float cz, float hz, float wallTop, float half, WorldRandom random)
        {
            float z = cz + hz - 0.5f;
            float foot = wallTop + k.Thick + 0.5f * k.Pitch;
            float top = foot + 2.2f, rise = half * 0.85f;
            float back = cz + hz - (top - wallTop - k.Thick) / k.Pitch;
            float ridgeBack = cz + hz - (top + rise - wallTop - k.Thick) / k.Pitch - 0.3f;
            Hall.Face(b, new Vector3(along - half, foot - 0.4f, z), new Vector3(along + half, foot - 0.4f, z), new Vector3(along + half, top, z), new Vector3(along - half, top, z), Vector3.UnitZ, k.Lime);
            Hall.Tri(b, new Vector3(along - half, top, z), new Vector3(along + half, top, z), new Vector3(along, top + rise, z), Vector3.UnitZ, k.Lime);
            var face = new Wall(new Vector3(0f, 0f, z), Vector3.UnitX, Vector3.UnitZ, 100f);
            Window(b, k, face, new Gap(along, half * 0.6f, foot + 0.25f, top - 0.2f), shutters: false, flowers: false, shines: false, random);
            foreach (float s in new[] { -1f, 1f })
            {
                float x = along + s * half;
                Hall.Tri(b, new Vector3(x, foot - 0.4f, z), new Vector3(x, top, z), new Vector3(x, top, back), Vector3.UnitX * s, k.Lime);
                b.Beam(new Vector3(x, foot - 0.3f, z + 0.03f), new Vector3(x, top, z + 0.03f), T * 0.8f, k.Timber);
                Vector3 e0 = new Vector3(along + s * (half + 0.35f), top - 0.3f, z + 0.35f), e1 = new Vector3(along + s * (half + 0.35f), top - 0.3f, ridgeBack);
                Vector3 r0 = new Vector3(along, top + rise + 0.12f, z + 0.35f), r1 = new Vector3(along, top + rise + 0.12f, ridgeBack);
                Hall.Face(b, e0, e1, r1, r0, new Vector3(s * 0.7f, 1f, 0f), k.Roof);
                Hall.Face(b, e0 - Vector3.UnitY * 0.16f, e0, r0, r0 - Vector3.UnitY * 0.16f, Vector3.UnitZ, k.Cut);
            }
            b.Tube(new Vector3(along, top + rise + 0.14f, z + 0.4f), new Vector3(along, top + rise + 0.14f, ridgeBack), 0.12f, 0.12f, 6, k.Ridge);
        }

        // ---------------------------------------------------------------- Chimneys

        // A chimney built up outside a gable: a broad foot of field stone as high as the eaves, drawn in at two shoulders, and a stack of brick from there past the ridge.
        private static void EndStack(MeshBuilder b, Look k, float hx, float hz, float wallTop, float ridge, float end, WorldRandom random)
        {
            float x = end * (hx + 0.55f);
            float shoulder = wallTop * 0.62f, neck = wallTop + 1.3f, top = ridge + random.Range(1.1f, 1.6f);
            float z = random.Range(-0.4f, 0.4f);
            b.Box(new Vector3(x, (shoulder - 2f) * 0.5f, z), new Vector3(0.6f, (shoulder + 2f) * 0.5f, 1.3f), k.Plinth);
            b.Box(new Vector3(x - end * 0.06f, (shoulder + neck) * 0.5f, z), new Vector3(0.52f, (neck - shoulder) * 0.5f, 0.92f), k.Stone);
            // The shoulders, sloped to shed the rain.
            foreach (float s in new[] { -1f, 1f })
            {
                Hall.Face(b, new Vector3(x - 0.6f, shoulder, z + s * 1.3f), new Vector3(x + 0.6f, shoulder, z + s * 1.3f), new Vector3(x + 0.6f, shoulder + 0.55f, z + s * 0.92f), new Vector3(x - 0.6f, shoulder + 0.55f, z + s * 0.92f),
                    new Vector3(0f, 1f, s), k.Stone);
                Hall.Face(b, new Vector3(x - 0.52f, neck, z + s * 0.92f), new Vector3(x + 0.52f, neck, z + s * 0.92f), new Vector3(x + 0.52f, neck + 0.45f, z + s * 0.56f), new Vector3(x - 0.52f, neck + 0.45f, z + s * 0.56f),
                    new Vector3(0f, 1f, s), k.Stone);
            }
            StackTop(b, k, new Vector3(x - end * 0.1f, neck, z), new Vector2(0.46f, 0.56f), top, random);
        }

        // A chimney through the ridge, with a skirt of mortar where the roof meets it.
        private static void RidgeStack(MeshBuilder b, Look k, Vector3 at, float wallTop, float ridge, WorldRandom random)
        {
            float top = ridge + random.Range(1.3f, 1.8f);
            b.Box(new Vector3(at.X, ridge - 0.2f, at.Z), new Vector3(0.64f, 0.2f, 0.58f), Hall.Stone(120, 114, 104));
            StackTop(b, k, new Vector3(at.X, wallTop, at.Z), new Vector2(0.56f, 0.5f), top, random);
        }

        // The stack itself, in brick darkened towards its top by its own smoke, under a slab and a pot or two, and where its smoke leaves.
        private static void StackTop(MeshBuilder b, Look k, Vector3 foot, Vector2 half, float top, WorldRandom random)
        {
            float middle = foot.Y + (top - foot.Y) * 0.6f;
            b.Box(new Vector3(foot.X, (foot.Y + middle) * 0.5f, foot.Z), new Vector3(half.X, (middle - foot.Y) * 0.5f, half.Y), k.Brick);
            b.Box(new Vector3(foot.X, (middle + top) * 0.5f, foot.Z), new Vector3(half.X, (top - middle) * 0.5f, half.Y), Shaded(k.Brick, 0.8f));
            b.Box(new Vector3(foot.X, top - 0.5f, foot.Z), new Vector3(half.X + 0.09f, 0.09f, half.Y + 0.09f), Shaded(k.Brick, 0.9f));
            b.Box(new Vector3(foot.X, top + 0.07f, foot.Z), new Vector3(half.X + 0.15f, 0.08f, half.Y + 0.15f), Hall.Stone(150, 146, 138));
            int pots = random.Chance(0.5f) ? 2 : 1;
            for (int i = 0; i < pots; i++)
            {
                Vector3 pot = new Vector3(foot.X, top + 0.15f, foot.Z + (pots == 1 ? 0f : (i - 0.5f) * half.Y * 1.1f));
                b.Tube(pot, pot + Vector3.UnitY * 0.62f, 0.23f, 0.17f, 8, Hall.Brick(148, 92, 70), capped: false);
                b.Tube(pot + Vector3.UnitY * 0.62f, pot + Vector3.UnitY * 0.72f, 0.21f, 0.21f, 8, Hall.Flat(26, 22, 20));
                k.Chimneys.Add(Hall.Placed(b, pot + Vector3.UnitY * 1.3f));
            }
        }

        // ---------------------------------------------------------------- What stands against it

        // An open lean-to against a gable: a roof of boards on three posts, and the winter's wood stacked dry under it with its ends to whoever's looking.
        private static void LeanTo(MeshBuilder b, Look k, float hx, float hz, float wallTop, float end, WorldRandom random)
        {
            float wallX = end * hx, postX = end * (hx + 2.6f);
            float high = MathF.Min(wallTop - 0.8f, 5.6f), low = high - 1.5f;
            float z0 = -hz + 1.2f, z1 = hz - 0.9f;
            VertexMaterial timber = Shaded(k.Timber, 1.12f);
            foreach (float z in new[] { z0 + 0.2f, (z0 + z1) * 0.5f, z1 - 0.2f })
            {
                b.Box(new Vector3(postX - end * 0.2f, low * 0.5f - 0.3f, z), new Vector3(0.14f, low * 0.5f + 0.3f, 0.14f), timber);
                b.Beam(new Vector3(postX - end * 0.2f, low - 1f, z), new Vector3(postX - end * 1.1f, low + 0.25f, z), 0.07f, timber);
            }
            b.Beam(new Vector3(postX - end * 0.2f, low + 0.05f, z0), new Vector3(postX - end * 0.2f, low + 0.05f, z1), 0.12f, timber);
            for (float z = z0 + 0.3f; z < z1; z += 1.25f) b.Beam(new Vector3(wallX, high, z), new Vector3(postX + end * 0.25f, low + 0.12f, z), 0.07f, timber);
            // Its roof, in boards gone grey, with the weather in them.
            VertexMaterial boards = new VertexMaterial(MaterialCatalog.Shingle.Index, new Colour(new Vector3(0.74f, 0.7f, 0.64f) * random.Range(0.9f, 1.05f)));
            Vector3 r0 = new Vector3(wallX, high + 0.22f, z1 + 0.35f), r1 = new Vector3(wallX, high + 0.22f, z0 - 0.35f);
            Vector3 e0 = new Vector3(postX + end * 0.45f, low + 0.22f, z1 + 0.35f), e1 = new Vector3(postX + end * 0.45f, low + 0.22f, z0 - 0.35f);
            Vector3 up = new Vector3(end * 0.5f, 1f, 0f);
            Hall.Face(b, e0, e1, r1, r0, up, boards);
            Hall.Face(b, e0 - Vector3.UnitY * 0.14f, e1 - Vector3.UnitY * 0.14f, e1, e0, Vector3.UnitX * end, Shaded(boards, 0.7f));
            Hall.Face(b, e0 - Vector3.UnitY * 0.14f, e0, r0, r0 - Vector3.UnitY * 0.14f, Vector3.UnitZ, Shaded(boards, 0.7f));

            // The wood: rows of split logs across the lean-to, their ends towards the street.
            VertexMaterial bark = Hall.Bark(122, 104, 88), ends = Hall.Wood(196, 160, 114);
            float stackTop = random.Range(1.9f, 2.6f);
            float from = wallX + end * 0.35f, to = postX - end * 0.75f;
            float x0 = MathF.Min(from, to), x1 = MathF.Max(from, to);
            for (float y = 0.3f; y < stackTop; y += 0.33f)
            {
                float shift = ((int)(y / 0.33f) % 2) * 0.16f;
                for (float x = x0 + 0.16f + shift; x < x1 - 0.1f; x += 0.34f)
                {
                    if (y > stackTop - 0.7f && random.Chance(0.3f)) continue;
                    float r = random.Range(0.12f, 0.16f);
                    float front = z1 - 0.5f + random.Range(-0.08f, 0.08f), rear = z0 + 0.6f;
                    b.Tube(new Vector3(x, y, rear), new Vector3(x, y, front), r, r, 5, bark, capped: false);
                    b.Tube(new Vector3(x, y, front - 0.01f), new Vector3(x, y, front + 0.012f), r * 0.96f, r * 0.96f, 5, ends);
                }
            }
            // A chopping block before it, the axe left in it, and what's flown off.
            Vector3 block = new Vector3((x0 + x1) * 0.5f, 0f, z1 + 1.1f);
            b.Tube(block - Vector3.UnitY * 0.3f, block + Vector3.UnitY * 0.95f, 0.5f, 0.46f, 9, bark, capped: false);
            b.Tube(block + Vector3.UnitY * 0.95f, block + Vector3.UnitY * 0.97f, 0.46f, 0.46f, 9, ends);
            b.Beam(block + new Vector3(0.1f, 1.05f, 0f), block + new Vector3(0.75f, 1.95f, 0.1f), 0.04f, Hall.Wood(150, 118, 84));
            b.Box(block + new Vector3(0.07f, 1.04f, 0f), new Vector3(0.05f, 0.14f, 0.2f), Hall.Iron(120));
        }

        // A bench against the wall: a board on two blocks, and a back.
        private static void Seat(MeshBuilder b, Look k, Vector3 at, float half)
        {
            VertexMaterial wood = Hall.Wood(142, 110, 78);
            b.Box(at + new Vector3(0f, 1.15f, 0f), new Vector3(half, 0.06f, 0.34f), wood);
            foreach (float s in new[] { -1f, 1f }) b.Box(at + new Vector3(s * (half - 0.3f), 0.5f, 0f), new Vector3(0.09f, 0.6f, 0.3f), Shaded(wood, 0.85f));
            b.Box(at + new Vector3(0f, 1.9f, -0.32f), new Vector3(half, 0.3f, 0.04f), wood);
        }

        // A rose trained up the wall on a trellis: a stem, a lattice of laths, leaves in clumps and whatever's in flower.
        private static void Climber(MeshBuilder b, Look k, Wall wall, float along, float top, WorldRandom random)
        {
            float half = random.Range(0.85f, 1.2f);
            VertexMaterial lath = Hall.Wood(168, 142, 108);
            for (int i = -2; i <= 2; i++) b.Beam(wall.At(along + i * half * 0.5f, PlinthTop, 0.09f), wall.At(along + i * half * 0.5f, top, 0.09f), 0.022f, lath);
            for (float y = PlinthTop + 0.5f; y < top; y += 0.8f) b.Beam(wall.At(along - half, y, 0.11f), wall.At(along + half, y, 0.11f), 0.022f, lath);
            Vector3[] blooms = { new(226, 110, 140), new(240, 228, 214), new(228, 170, 70), new(196, 76, 70), new(168, 112, 200) };
            Vector3 bloom = random.Pick(blooms);
            VertexMaterial flower = Hall.Flat((int)bloom.X, (int)bloom.Y, (int)bloom.Z);
            VertexMaterial stem = Hall.Wood(96, 78, 56);
            Vector3 last = wall.At(along, 0.1f, 0.14f);
            for (float y = 0.8f; y < top + 0.2f; y += 0.55f)
            {
                float sway = MathF.Sin(y * 1.7f + half * 5f) * half * 0.7f;
                Vector3 next = wall.At(along + sway, y, 0.14f);
                b.Beam(last, next, 0.035f, stem);
                last = next;
                if (y < PlinthTop + 0.3f) continue;
                int clumps = random.Int(2, 4);
                for (int c = 0; c < clumps; c++)
                {
                    Vector3 at = wall.At(along + sway + random.Range(-half, half), y + random.Range(-0.25f, 0.25f), random.Range(0.14f, 0.3f));
                    float size = random.Range(0.2f, 0.36f);
                    TownPieces.Leaves(b, at, size * 1.1f, random, flat: 0.45f);
                    if (random.Chance(0.7f)) b.Box(at + wall.N * 0.12f + new Vector3(random.Range(-0.1f, 0.1f), random.Range(-0.1f, 0.12f), 0f), new Vector3(0.09f, 0.085f, 0.09f), flower);
                }
            }
        }

        // The thatcher's ladder, still up against the eaves.
        private static void Ladder(MeshBuilder b, Look k, Vector3 foot, float top, WorldRandom random)
        {
            VertexMaterial wood = Hall.Wood(176, 146, 104);
            Vector3 head = new Vector3(foot.X, top, foot.Z - 2.2f);
            foreach (float s in new[] { -0.32f, 0.32f }) b.Beam(foot + new Vector3(s, 0f, 0f), head + new Vector3(s, 0f, 0f), 0.05f, wood);
            int rungs = (int)((head - foot).Length / 0.62f);
            for (int i = 1; i < rungs; i++)
            {
                Vector3 at = Vector3.Lerp(foot, head, (float)i / rungs);
                b.Beam(at - Vector3.UnitX * 0.32f, at + Vector3.UnitX * 0.32f, 0.03f, wood);
            }
            // Bundles of reed stacked by its foot.
            VertexMaterial reed = new VertexMaterial(MaterialCatalog.Thatch.Index, new Colour(1.04f, 0.96f, 0.74f));
            for (int i = 0; i < 4; i++)
            {
                Vector3 at = foot + new Vector3(1.2f + (i % 2) * 0.55f, 0.32f + (i / 2) * 0.5f, 0.3f + (i % 2) * 0.1f);
                b.Tube(at - Vector3.UnitZ * 1.1f, at + Vector3.UnitZ * 1.1f, 0.27f, 0.22f, 7, reed);
            }
        }

        // A barrel standing under the eaves to catch what runs off them, full to the brim.
        private static void WaterButt(MeshBuilder b, Look k, Vector3 at, WorldRandom random)
        {
            VertexMaterial staves = Hall.Wood(128, 96, 66);
            b.Tube(at, at + Vector3.UnitY * 0.9f, 0.56f, 0.66f, 11, staves, capped: false);
            b.Tube(at + Vector3.UnitY * 0.9f, at + Vector3.UnitY * 1.85f, 0.66f, 0.58f, 11, staves, capped: false);
            foreach (float y in new[] { 0.3f, 0.95f, 1.6f }) b.Tube(at + Vector3.UnitY * (y - 0.045f), at + Vector3.UnitY * (y + 0.045f), 0.68f, 0.68f, 11, Hall.Iron(96), capped: false);
            b.Tube(at + Vector3.UnitY * 1.72f, at + Vector3.UnitY * 1.75f, 0.55f, 0.55f, 11, Hall.Mirror(52, 64, 66));
        }

        private static VertexMaterial Shaded(VertexMaterial material, float by)
        {
            Colour c = material.colour;
            return new VertexMaterial(material.materialTextureIndex, new Colour(c.Red * by, c.Green * by, c.Blue * by, c.Intensity, c.Alpha));
        }
    }
}
