using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>What a hall is for. Each kind has one design (<see cref="HallDesigns"/>), and the Haven has one of each. Saved by name if ever a hero keeps which are open, so a kind that goes needs a line wherever that's read.</summary>
    public enum HallKind
    {
        Forge,          // Where gear is worked on: a smithy and its crafting bench.
        GamingHouse,    // Where gold is played for.
        Archive,        // Where what a player's heroes have done is kept, each record a display of its own.
    }

    /// <summary>A wall of a hall, in the hall's own frame, whose front is +z, where its main door is.</summary>
    public enum HallSide
    {
        Front,      // +z
        Back,       // -z
        PlusX,
        MinusX,
    }

    /// <summary>A door or a window: a hole right through a wall. Doors reach the floor, and only doors are ways in.</summary>
    public readonly record struct HallOpening(HallSide Side, float Along, float HalfWidth, float Sill, float Head, bool Door, bool Arched = false);

    /// <summary>
    /// Something standing in a hall, in the hall's frame: where its middle is, how far it reaches each way before it's turned by <see cref="Yaw"/>, and how tall it is. What it looks like is
    /// <see cref="Kind"/>'s, built by the hall's mesh to these numbers, and if it's <see cref="Solid"/> it stops whoever walks into it and what's thrown below <see cref="Height"/>.
    /// A round one has a <see cref="Half"/> of (radius, 0).
    /// </summary>
    public readonly record struct HallPiece(PieceKind Kind, Vector2 At, Vector2 Half, float Yaw, float Height, bool Solid, int Variant = 0)
    {
        public bool IsRound => Half.Y == 0f;
    }

    /// <summary>A spot in a hall where the game puts someone or something: a keeper, or what can be used.</summary>
    public readonly record struct HallSpot(SpawnRole Role, Vector2 At);

    /// <summary>One of the Archive's displays: what record it keeps, where its middle is and which way it faces, in the hall's frame.</summary>
    public readonly record struct HallExhibit(ExhibitKind Kind, Vector2 At, float Yaw);

    /// <summary>What can stand in a hall. Each is built to its <see cref="HallPiece"/>'s numbers by the hall's mesh (Modelling/Procedural/Architecture/Halls).</summary>
    public enum PieceKind
    {
        // The Forge.
        Hearth,             // The forge itself: a raised hearth of coals under a stone hood, its chimney through the roof.
        Bellows,
        Anvil,
        Trough,             // Water for quenching.
        ToolRack,           // Tongs and hammers on a board against a wall.
        Workbench,          // The crafting bench: where gear is worked on.
        WeaponRack,
        ArmourStand,
        Shelves,            // Stock, tools and bar iron on shelves against a wall.
        Grindstone,
        LeatherTable,       // Hides and straps on a long table.
        CoalBin,
        Casks,              // Barrels, standing.
        Crates,
        Firewood,

        // The Gaming House.
        GamingTable,        // The great table, where gold is played for.
        Chair,
        Stool,
        Bar,                // A long counter.
        BarShelves,         // Bottles, mugs and casks on shelves behind it.
        Fireplace,
        Armchair,
        RoundTable,         // A small table for dice or cards.
        Stage,              // A low platform for whoever plays music.
        Rug,
        Planter,            // A tub of greenery.

        // Outside.
        Buttress,
        LeanTo,             // An open shed against a wall, with what's kept dry under it.
        Post,
        ChimneyBreast,      // A fireplace's chimney, built out from a wall.

        // The Archive.
        Plinth,             // What an exhibit stands on.
        Bookcase,
        ReadingDesk,
        CandleStand,
        Bench,
        Globe,
        Lectern,
    }

    /// <summary>What one of the Archive's displays keeps. The game says what each means and what it shows; the world only says where it stands.</summary>
    public enum ExhibitKind
    {
        FoesSlain,
        HardestBlow,
        TyrantsFelled,
        Falls,
        SwiftestLand,
        UniquesFound,
        LandsFreed,
        DeepestLand,
        GoldGathered,
    }

    /// <summary>
    /// A hall's plan: its walls, its doors and windows, and what stands in it, all in its own frame (front +z). It's the one description of the hall: the world takes its walls and what stands
    /// in it as footprints from it (<see cref="Generation.SiteBuilder.PlaceHall"/>), and its mesh is built to the same numbers, so what looks solid is what is solid. Read HALLS.md.
    /// </summary>
    public sealed class HallDesign
    {
        public HallKind Kind;
        public string Name = "";

        /// <summary>Metres from the middle to the outside of the walls along x and z.</summary>
        public Vector2 Half;

        /// <summary>How thick the walls are. Global illumination needs a wall to be solid voxels all the way through, and its finest are a quarter of a metre.</summary>
        public float Wall = 0.9f;

        /// <summary>Metres above the floor that the walls end and the roof begins. Everything above it goes, for the camera, when someone's inside.</summary>
        public float WallTop = 9f;

        /// <summary>Metres above the floor that a wall facing the camera comes down to when someone's inside, so that it hides nothing behind it.</summary>
        public float FrontCut = 1.8f;

        /// <summary>Metres past the walls that the roof reaches, which goes with it.</summary>
        public float Eaves = 2.4f;

        /// <summary>
        /// Metres above the floor of the highest thing on the building, chimney or cupola, which anything flying keeps over (LifeLand.Overhead). Measured from its model by
        /// <c>Dino WorldLab halls</c>, which fails if the model rises above it or falls three metres short.
        /// </summary>
        public float Top = 24f;

        /// <summary>Metres in front of a wall facing the camera that come down with it: a porch, open doors, a lantern by the door.</summary>
        public float FrontReach = 1.2f;

        /// <summary>What a foot finds on its floor.</summary>
        public SurfaceMaterial Floor = SurfaceMaterial.Planks;

        public readonly List<HallOpening> Openings = new();
        public readonly List<HallPiece> Pieces = new();

        /// <summary>What stands against it outside its walls, in the same frame: buttresses, a lean-to, a porch's posts. Solid as they say, and kept clear like the hall.</summary>
        public readonly List<HallPiece> Outside = new();
        public readonly List<HallSpot> Spots = new();
        public readonly List<HallExhibit> Exhibits = new();

        /// <summary>Where the village's people come to stand when they visit, in the hall's frame.</summary>
        public readonly List<Vector2> Visits = new();

        /// <summary>The model of its walls, roof and floor, which is always drawn.</summary>
        public string ShellAsset => $"hall.{Kind.ToString().ToLowerInvariant()}";

        /// <summary>The model of what's inside, which is only drawn when it can be seen.</summary>
        public string InsideAsset => $"hall.{Kind.ToString().ToLowerInvariant()}.inside";

        /// <summary>Metres from the middle to the inside of the walls.</summary>
        public Vector2 Inner => Half - new Vector2(Wall);

        public IEnumerable<HallOpening> Doors => Openings.Where(opening => opening.Door);

        /// <summary>The line along the middle of a wall, from one end to the other, in the hall's frame.</summary>
        public (Vector2 From, Vector2 To) WallLine(HallSide side)
        {
            float x = Half.X - Wall * 0.5f;
            float z = Half.Y - Wall * 0.5f;
            return side switch
            {
                HallSide.Front => (new Vector2(-Half.X, z), new Vector2(Half.X, z)),
                HallSide.Back => (new Vector2(-Half.X, -z), new Vector2(Half.X, -z)),
                HallSide.PlusX => (new Vector2(x, -Half.Y), new Vector2(x, Half.Y)),
                _ => (new Vector2(-x, -Half.Y), new Vector2(-x, Half.Y)),
            };
        }

        /// <summary>
        /// The solid runs of wall, in the hall's frame, broken where its doors are: each the middle line of a run, which is as thick as <see cref="Wall"/>. Runs along x go the whole width
        /// and runs along z the whole depth, so the corners are closed twice over.
        /// </summary>
        public IEnumerable<(Vector2 From, Vector2 To)> WallRuns()
        {
            foreach (HallSide side in Enum.GetValues<HallSide>())
            {
                (Vector2 from, Vector2 to) = WallLine(side);
                bool alongX = side is HallSide.Front or HallSide.Back;
                float start = alongX ? from.X : from.Y;
                float end = alongX ? to.X : to.Y;
                var doors = Openings.Where(o => o.Door && o.Side == side).OrderBy(o => o.Along).ToList();
                float at = start;
                foreach (HallOpening door in doors)
                {
                    float gapFrom = door.Along - door.HalfWidth;
                    if (gapFrom > at + 0.05f) yield return (Point(side, from, at), Point(side, from, gapFrom));
                    at = door.Along + door.HalfWidth;
                }
                if (end > at + 0.05f) yield return (Point(side, from, at), Point(side, from, end));
            }
        }

        private static Vector2 Point(HallSide side, Vector2 line, float along) => side is HallSide.Front or HallSide.Back ? new Vector2(along, line.Y) : new Vector2(line.X, along);

        /// <summary>Where a door meets the ground outside, in the hall's frame, <paramref name="out_"/> metres out from the wall.</summary>
        public Vector2 Doorstep(HallOpening door, float out_)
        {
            return door.Side switch
            {
                HallSide.Front => new Vector2(door.Along, Half.Y + out_),
                HallSide.Back => new Vector2(door.Along, -Half.Y - out_),
                HallSide.PlusX => new Vector2(Half.X + out_, door.Along),
                _ => new Vector2(-Half.X - out_, door.Along),
            };
        }

        /// <summary>Which way is out through a wall, in the hall's frame.</summary>
        public static Vector2 Outward(HallSide side) => side switch
        {
            HallSide.Front => Vector2.UnitY,
            HallSide.Back => -Vector2.UnitY,
            HallSide.PlusX => Vector2.UnitX,
            _ => -Vector2.UnitX,
        };

        public HallDesign Door(HallSide side, float along, float halfWidth, float head, bool arched = false)
        {
            Openings.Add(new HallOpening(side, along, halfWidth, 0f, head, true, arched));
            return this;
        }

        public HallDesign Window(HallSide side, float along, float halfWidth, float sill, float head, bool arched = false)
        {
            Openings.Add(new HallOpening(side, along, halfWidth, sill, head, false, arched));
            return this;
        }

        public HallDesign Piece(PieceKind kind, float x, float z, float halfX, float halfZ, float height, bool solid = true, float yaw = 0f, int variant = 0)
        {
            Pieces.Add(new HallPiece(kind, new Vector2(x, z), new Vector2(halfX, halfZ), yaw, height, solid, variant));
            return this;
        }

        /// <param name="yaw">Which way its front faces, as a round footprint doesn't say.</param>
        public HallDesign Round(PieceKind kind, float x, float z, float radius, float height, bool solid = true, int variant = 0, float yaw = 0f)
        {
            Pieces.Add(new HallPiece(kind, new Vector2(x, z), new Vector2(radius, 0f), yaw, height, solid, variant));
            return this;
        }

        public HallDesign Against(PieceKind kind, float x, float z, float halfX, float halfZ, float height, bool solid = true)
        {
            Outside.Add(new HallPiece(kind, new Vector2(x, z), new Vector2(halfX, halfZ), 0f, height, solid));
            return this;
        }

        public HallDesign Spot(SpawnRole role, float x, float z)
        {
            Spots.Add(new HallSpot(role, new Vector2(x, z)));
            return this;
        }

        public HallDesign Exhibit(ExhibitKind kind, float x, float z, float yaw)
        {
            Exhibits.Add(new HallExhibit(kind, new Vector2(x, z), yaw));
            return this;
        }

        public HallDesign Visit(float x, float z)
        {
            Visits.Add(new Vector2(x, z));
            return this;
        }
    }

    /// <summary>
    /// A hall standing in a world: a building that can be walked into, with solid walls broken by its doors and what stands in it solid as it looks. When someone stands inside, its roof
    /// and the wall towards the camera go, for the camera alone (HallCutawaySystem), and the light of its room stays in its room. Read HALLS.md.
    /// </summary>
    public sealed class WorldHall
    {
        public readonly HallDesign Design;
        public readonly Vector2 Centre;
        public readonly float Yaw;

        /// <summary>The height of its floor, which the ground under it is levelled to.</summary>
        public readonly float Floor;

        public WorldHall(HallDesign design, Vector2 centre, float yaw, float floor)
        {
            Design = design;
            Centre = centre;
            Yaw = yaw;
            Floor = floor;
        }

        public HallKind Kind => Design.Kind;

        /// <summary>Its x axis on the ground, as a <see cref="Footprint"/> box's.</summary>
        public Vector2 Axis => new Vector2(MathF.Cos(Yaw), -MathF.Sin(Yaw));

        /// <summary>Its front, +z, on the ground.</summary>
        public Vector2 Front => new Vector2(MathF.Sin(Yaw), MathF.Cos(Yaw));

        public Footprint Footprint => Footprint.Box(Centre, Design.Half, Yaw);

        public Vector2 ToWorld(Vector2 local) => Centre + Axis * local.X + Front * local.Y;

        public Vector2 ToLocal(Vector2 world)
        {
            Vector2 offset = world - Centre;
            return new Vector2(Vector2.Dot(offset, Axis), Vector2.Dot(offset, Front));
        }

        /// <summary>A direction in its frame, on the ground.</summary>
        public Vector2 DirectionToWorld(Vector2 local) => Axis * local.X + Front * local.Y;

        /// <summary>Whether a point is inside its walls, grown by <paramref name="grow"/> metres (or shrunk, if it's less than nothing).</summary>
        public bool Contains(Vector2 world, float grow = 0f)
        {
            Vector2 local = ToLocal(world);
            Vector2 inner = Design.Inner + new Vector2(grow);
            return MathF.Abs(local.X) < inner.X && MathF.Abs(local.Y) < inner.Y;
        }

        /// <summary>The footprint of something standing in it, in the world.</summary>
        public Footprint FootprintOf(in HallPiece piece)
        {
            Vector2 at = ToWorld(piece.At);
            return piece.IsRound ? Footprint.Circle(at, piece.Half.X) : Footprint.Box(at, piece.Half, Yaw + piece.Yaw);
        }
    }
}
