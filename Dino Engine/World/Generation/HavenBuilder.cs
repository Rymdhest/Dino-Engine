using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Modelling.Procedural.Architecture.Plaza;
using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Generation.SiteBuilder;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Builds the Haven, the one town the Reich has never found, where a hero spends more time than anywhere else (HAVEN.md): a little town on a rise over its harbour, laid out
    /// for the game's camera, which looks north from high in the south, so that everything worth seeing faces south or stands at the head of something. The great waygate is at
    /// the top of it all on its terrace (<see cref="HavenSquare"/>), the square below that, and either side of the gate, each on a terrace of its own up a flight of steps, the
    /// Forge to the west, which is where a Defender goes most, with the market along the foot of its wall, and the Gaming House to the east. A lane runs south from the square's
    /// arch to the Row, the street along the top of the town's wall, with the Archive at its west end on a podium and homes along its north side, each behind its own garden;
    /// and from the Row the Harbour Steps go down through the wall to the tiltyard, the fishers' cottages and the water. Every level is ground laid exactly, with walls hiding
    /// where it's raised (SQUARE.md), and every hall faces south, as a hall's front is the wall that comes down when someone's inside it (HALLS.md). What stands where is
    /// decided here to the metre: nothing is tried and left out, so a Haven with something missing is a fault <c>Dino WorldLab check</c> finds.
    /// </summary>
    internal static class HavenBuilder
    {
        // ---------------------------------------------------------------- The lie of the town

        /// <summary>Metres the town stands above its harbour: five steps.</summary>
        public const float Lift = 2f;
        private const int LiftSteps = 5;
        private const float HallUp = 1.2f;              // The Forge and the Gaming House above the square: three steps.
        private const int HallSteps = 3;
        private const float PodiumUp = 0.8f;            // The Archive above the Row: two.
        private const int PodiumSteps = 2;

        // The rise the town stands on: a box with rounded corners, level on top, falling away in banks all round but along the wall to the south.
        private static readonly Vector2 MoundMiddle = new Vector2(0f, 12.25f);
        private static readonly Vector2 MoundHalf = new Vector2(84f, 65.75f);
        private const float MoundCorner = 24f;
        private const float Bank = 18f;

        public const float WallZ = -53.5f;              // The face of the town's wall, south of its middle.
        private const float WallHalf = 50f;             // How far it runs either way from the Harbour Steps.
        private const float WallThick = 1.3f;
        public const float RowZ = -43.5f;               // The middle of the Row, the street along the wall's top.
        private const float StreetHalf = 3f;            // Half the width of a paved street (RoadClass.Highway).
        private const float StepsHalf = 3f;             // Half the width of the Harbour Steps, between their cheeks.

        // Where each hall stands, from the square's middle, east and north. They all face south, at the camera.
        private static readonly Vector2 ForgeAt = new Vector2(-43.5f, 30.6f);
        private static readonly Vector2 GamingHouseAt = new Vector2(43.5f, 30.6f);
        private static readonly Vector2 ArchiveAt = new Vector2(-47f, -21.7f);
        private const float TerraceFace = 12.6f;        // The face of the two halls' terraces, in line with the fronts of the avenue's beds.
        private const float GateWall = 19.5f;           // Where the gate's terrace ends, either side of the square's middle (HavenSquare.TerraceSide).
        private const float PodiumFace = -39.5f;           // Clear of the Row's kerb.

        // The tiltyard, below the wall, with three groups to try a build on: one alone, a handful, and a crowd.
        private static readonly Vector2 YardAt = new Vector2(2f, -72f);
        private static readonly Vector2 GroundHalf = new Vector2(36f, 12f);
        private const float SingleAlong = 27f;          // Metres east of the yard's middle, and the handful and the crowd west of it, each far enough from the next that nothing aimed at one reaches another.
        private const float SmallAlong = 7f;
        private const float BigAlong = -20f;
        private const float PenReach = 4f;
        private const float FencePiece = 5f;            // Metres of the yard's fence placed at a time, so that whatever stands in the way of one piece leaves the rest standing.
        private const float YardGate = 3.2f;            // Half the width of the way into it.
        private const float DummyGap = 3.4f;            // Between the dummies of a group: close enough that what hits one can reach the next.
        private const int SmallGroup = 5;
        private const int BigGroup = 15;

        private const float GateAhead = 31f;
        private const float StashAside = 17.4f;
        private const float StashNorth = -0.6f;
        private const float ReachForEssentials = 30f;

        /// <summary>The ground kept for halls to come, from the square's middle: each the size of the Forge, facing south.</summary>
        public static readonly Vector2[] Plots = { new Vector2(0f, 62f), new Vector2(64f, 56f) };
        private static readonly Vector2 PlotHalf = new Vector2(16f, 12f);

        public static readonly Vector2 North = new Vector2(0f, 1f);
        public static readonly Vector2 East = new Vector2(1f, 0f);
        private static readonly float FacingCamera = YawFacing(-North);

        public static void Build(SiteBuilder b)
        {
            Vector2 c = b.Centre;
            WorldData world = b.World;

            // The town stands on a rise two metres over its harbour, which is the land as it was. The rise is put into the land itself, so that everything built on it,
            // every road and every tree stands where the ground will be, and the square's own level is the rise's.
            float harbour = AverageGround(world, c + YardAt, 14f);
            world.Trim = true;
            OpenTown(world, c);
            RaiseTown(world, c, harbour + Lift);
            var square = new HavenSquare(b);
            float upper = square.Level, lower = upper - Lift;
            var m = new TownMason(b, "town", 0x70A11E00u);

            // Level ground first, as the streets are planned to meet it: the town's on the rise, the harbour's below the wall, and the square's own.
            m.Level(m.Box(new Vector2(-76f, RowZ - 0.5f), new Vector2(76f, 72f)), upper, 12f);
            m.Level(m.Box(new Vector2(-44f, -90f), new Vector2(46f, WallZ - 2.5f)), lower, 6f);
            square.LayGround();

            PlaceGate(b, c);
            PlaceTable(b, c);
            WorldHall? forge = PlaceHall(b, HallKind.Forge, c + ForgeAt, upper + HallUp);
            WorldHall? gaming = PlaceHall(b, HallKind.GamingHouse, c + GamingHouseAt, upper + HallUp);
            WorldHall? archive = PlaceHall(b, HallKind.Archive, c + ArchiveAt, upper + PodiumUp);
            // Every street before anything else, so that what's put down after keeps off them.
            LayStreets(b, upper);
            PlaceStash(b, c);
            square.Build();

            var town = new HavenTown(m, upper, lower);
            BuildWall(m, upper, lower);
            if (forge != null) HallTerrace(m, forge, upper, west: true);
            if (gaming != null) HallTerrace(m, gaming, upper, west: false);
            if (archive != null) Podium(m, archive, upper);
            PlaceTiltyard(b, m, YardAt, lower);
            town.Build(forge, gaming, archive);
            KeepPlots(b, c);

            square.People(4);
        }

        // ---------------------------------------------------------------- The rise

        // How far a point is outside the top of the rise, in metres: nothing on it.
        private static float OutsideMound(Vector2 p)
        {
            Vector2 q = new Vector2(MathF.Abs(p.X - MoundMiddle.X), MathF.Abs(p.Y - MoundMiddle.Y)) - (MoundHalf - new Vector2(MoundCorner));
            float outside = new Vector2(MathF.Max(q.X, 0f), MathF.Max(q.Y, 0f)).Length + MathF.Min(MathF.Max(q.X, q.Y), 0f) - MoundCorner;
            return MathF.Max(outside, 0f);
        }

        // Lifts the land the town stands on to one level, with banks down to the land as it was all round, short along the south, where the wall holds it up. Only ever up:
        // where the hill behind the town already stands higher, it's left as it is, so nothing's dammed.
        private static void RaiseTown(WorldData world, Vector2 c, float level)
        {
            ScalarField land = world.Elevation;
            float reach = MoundHalf.Length + Bank;
            int minX = Math.Max((int)((c.X - reach) / land.CellSize), 0), maxX = Math.Min((int)((c.X + reach) / land.CellSize) + 1, land.Width - 1);
            int minZ = Math.Max((int)((c.Y - reach) / land.CellSize), 0), maxZ = Math.Min((int)((c.Y + reach) / land.CellSize) + 1, land.Depth - 1);
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = land.WorldPosition(x, z) - c;
                    float south = (1f - Smoothstep(WallHalf + 4f, WallHalf + 16f, MathF.Abs(p.X))) * (1f - Smoothstep(-48f, -32f, p.Y));
                    float width = Lerp(Bank, 6f, south);
                    float share = 1f - Smoothstep(0f, width, OutsideMound(p));
                    if (share <= 0f) continue;
                    int i = land.Index(x, z);
                    if (land.Data[i] < level) land.Data[i] = Lerp(land.Data[i], level, share);
                }
            }
        }

        // The land the town's laid out on is land people walk, all of it: what the survey before it took for the foot of a hill or the back of a beach (WorldData.Reachable)
        // is ground this lays to the metre, so nothing here is left out of a fence's run or a house's plot for standing on it. The sea stays the sea.
        private static void OpenTown(WorldData world, Vector2 c)
        {
            ScalarField land = world.Elevation, open = world.Reachable;
            for (int z = 0; z < open.Depth; z++)
            {
                for (int x = 0; x < open.Width; x++)
                {
                    Vector2 p = open.WorldPosition(x, z) - c;
                    bool town = OutsideMound(p) <= 0f || (p.Y < WallZ + 6f && p.Y > -98f && p.X > -72f && p.X < 86f);
                    if (town && land[x, z] > WorldPlan.SeaLevel + 0.5f) open[x, z] = 1f;
                }
            }
        }

        private static float AverageGround(WorldData world, Vector2 centre, float radius)
        {
            float total = 0f;
            int count = 0;
            for (float x = -radius; x <= radius; x += 4f)
            {
                for (float z = -radius; z <= radius; z += 4f)
                {
                    if (x * x + z * z > radius * radius) continue;
                    total += world.Elevation.Sample(centre + new Vector2(x, z));
                    count++;
                }
            }
            return count > 0 ? total / count : world.Elevation.Sample(centre);
        }

        // ---------------------------------------------------------------- Halls, and the ground they stand on

        private static WorldHall? PlaceHall(SiteBuilder b, HallKind kind, Vector2 at, float floor)
        {
            // WorldLab's check fails a Haven with a hall missing, so a world this happens in doesn't go unnoticed. It isn't tried anywhere else: its terrace is laid to where it is.
            return b.PlaceHall(kind, at, FacingCamera, out WorldHall hall, floor: floor) ? hall : null;
        }

        /// <summary>
        /// The terrace the Forge or the Gaming House stands on, beside the gate's: a forecourt before its door, three steps above the street, held up by a wall of dressed stone
        /// with a parapet, a flight cut into it before the door with the Haven's lanterns on its piers, and the yard between the hall and the gate's own terrace raised with it.
        /// On its far side the ground falls away in a bank, which is the way round for anyone who'd sooner not climb.
        /// </summary>
        private static void HallTerrace(TownMason m, WorldHall hall, float street, bool west)
        {
            // The Forge's is cobbled, as a smith's yard has to be; the barn's is the earth carts have packed.
            GroundPaint floored = west ? GroundPaint.Plaza : GroundPaint.Yard;
            float s = west ? -1f : 1f;
            float floor = hall.Floor, up = floor - street;
            HallDesign d = hall.Design;
            Vector2 at = hall.Centre - m.C;
            float front = at.Y - d.Half.Y;                          // Its front wall.
            float far = at.X + s * (d.Half.X + 3f);                 // The face of the wall at the terrace's far end.
            float near = s * GateWall;                              // And at the gate's end, in line with the gate's own terrace.
            float top = up + PlazaStone.Ground + TownMason.Parapet;
            const float FlightHalf = 4f, Cheek = 1.4f, Thick = 1.2f;

            // The forecourt, and the yard beside the hall, level with its floor; their edges inside the walls that hide them.
            m.Raise(m.Box(new Vector2(far - s * TownMason.Inset, TerraceFace + TownMason.Inset), new Vector2(near + s * TownMason.Inset, front)), floor);
            m.Raise(m.Box(new Vector2(at.X - s * d.Half.X, front), new Vector2(near - s * 0.5f, at.Y + d.Half.Y + 4f)), floor);
            // Cobbled, but for a border of flowers along the inside of its parapet.
            m.Paint(floored, m.Box(new Vector2(far - s * 1.3f, TerraceFace + 2.6f), new Vector2(near + s * 1.3f, front + 0.5f)), 0.5f);
            m.Paint(GroundPaint.Plaza, m.Box(new Vector2(at.X - FlightHalf, TerraceFace), new Vector2(at.X + FlightHalf, front)), 0.4f);
            m.Paint(floored, m.Box(new Vector2(at.X - s * (d.Half.X - 1f), front), new Vector2(near + s * 1.3f, at.Y + d.Half.Y + 3f)), 0.6f);
            foreach ((float from, float to) in new[] { (MathF.Min(far, near) + 1.5f, at.X - FlightHalf - 1.5f), (at.X + FlightHalf + 1.5f, MathF.Max(far, near) - 1.5f) })
            {
                Footprint border = m.Box(new Vector2(from, TerraceFace + 1.25f), new Vector2(to, TerraceFace + 2.4f));
                m.Paint(GroundPaint.Garden, border, 0.4f);
                m.Garden(border);
            }

            // Its front wall, either side of the flight, and a return at each end.
            float gapFrom = at.X - FlightHalf - Cheek, gapTo = at.X + FlightHalf + Cheek;
            m.Run(new Vector2(MathF.Min(far, near), TerraceFace), new Vector2(gapFrom, TerraceFace), -North, WallForm.Dressed, top, up, Thick, street);
            m.Run(new Vector2(gapTo, TerraceFace), new Vector2(MathF.Max(far, near), TerraceFace), -North, WallForm.Dressed, top, up, Thick, street);
            m.Run(new Vector2(far, TerraceFace + 1f), new Vector2(far, front + 1.6f), East * s, WallForm.Dressed, top, up, Thick, street);
            m.Run(new Vector2(near, TerraceFace + 1f), new Vector2(near, HavenSquare.TerraceFront - 0.4f), -East * s, WallForm.Dressed, top, up, Thick, street);
            m.Pier(new Vector2(far - s * 0.5f, TerraceFace + 0.5f), 1.7f, top + 0.4f, 2, Undergrowth.Lavender, street);
            m.Pier(new Vector2(near + s * 0.5f, TerraceFace + 0.5f), 1.7f, top + 0.4f, 2, Undergrowth.Azalea, street);
            m.Flight(new Vector2(at.X, TerraceFace), North, FlightHalf, HallSteps, top, street);
        }

        /// <summary>The Archive's podium: its forecourt two steps over the Row behind a wall low enough to sit on, and a flight right across its door.</summary>
        private static void Podium(TownMason m, WorldHall hall, float street)
        {
            float floor = hall.Floor, up = floor - street;
            HallDesign d = hall.Design;
            Vector2 at = hall.Centre - m.C;
            float front = at.Y - d.Half.Y;
            float westEnd = at.X - d.Half.X + 1f, eastEnd = at.X + d.Half.X - 1f;
            float top = up + PlazaStone.Ground + 0.27f;
            const float FlightHalf = 7f, Cheek = 1.4f, Thick = 1f;
            m.Raise(m.Box(new Vector2(westEnd + TownMason.Inset, PodiumFace + TownMason.Inset), new Vector2(eastEnd - TownMason.Inset, front)), floor);
            m.Paint(GroundPaint.Paving, m.Box(new Vector2(at.X - FlightHalf, PodiumFace), new Vector2(at.X + FlightHalf, front + 0.4f)), 0.4f);
            m.Run(new Vector2(westEnd, PodiumFace), new Vector2(at.X - FlightHalf - Cheek, PodiumFace), -North, WallForm.Seat, top, up, Thick, street);
            m.Run(new Vector2(at.X + FlightHalf + Cheek, PodiumFace), new Vector2(eastEnd, PodiumFace), -North, WallForm.Seat, top, up, Thick, street);
            m.Run(new Vector2(westEnd, PodiumFace + 0.9f), new Vector2(westEnd, front - 0.1f), -East, WallForm.Seat, top, up, Thick, street);
            m.Run(new Vector2(eastEnd, PodiumFace + 0.9f), new Vector2(eastEnd, front - 0.1f), East, WallForm.Seat, top, up, Thick, street);
            m.Flight(new Vector2(at.X, PodiumFace), North, FlightHalf, PodiumSteps, top + 0.45f, street, pierOut: 0f);
        }

        // ---------------------------------------------------------------- The wall

        /// <summary>
        /// The town's wall: dressed stone holding the Row up over the harbour, a parapet along its top, a pier with a lantern at each end where it turns back into the bank,
        /// and the Harbour Steps cut down through the middle of it.
        /// </summary>
        private static void BuildWall(TownMason m, float upper, float lower)
        {
            float top = Lift + PlazaStone.Ground + TownMason.Parapet;
            const float Cheek = 1.4f;
            // The walk along its top, exactly level to the wall's back, and the ground at its foot, exactly as low as the harbour.
            m.Raise(m.Box(new Vector2(-WallHalf + TownMason.Inset, WallZ + TownMason.Inset), new Vector2(WallHalf - TownMason.Inset, RowZ - StreetHalf - 0.4f)), upper);
            m.Raise(m.Box(new Vector2(-WallHalf + 4f, WallZ - 6.1f), new Vector2(WallHalf - 4f, WallZ - 0.12f)), lower);

            m.Run(new Vector2(-WallHalf, WallZ), new Vector2(-StepsHalf - Cheek, WallZ), -North, WallForm.Dressed, top, Lift, WallThick, lower);
            m.Run(new Vector2(StepsHalf + Cheek, WallZ), new Vector2(WallHalf, WallZ), -North, WallForm.Dressed, top, Lift, WallThick, lower);
            foreach (float s in new[] { -1f, 1f })
            {
                // Where it turns back into the bank, which rises along it until the wall's no more than its parapet.
                m.Run(new Vector2(s * WallHalf, WallZ + 1f), new Vector2(s * WallHalf, RowZ - StreetHalf - 0.95f), East * s, WallForm.Dressed, top, Lift, WallThick, lower);
                m.Pier(new Vector2(s * (WallHalf - 0.5f), WallZ + 0.5f), 1.8f, top + 0.45f, 1, null, lower);
            }
            m.Flight(new Vector2(0f, WallZ), North, StepsHalf, LiftSteps, top, lower);

            // Buttresses along its face, which is what says how high it stands from above: every other one with flowers in an urn on its head.
            foreach (float s in new[] { -1f, 1f })
            {
                for (int i = 0; i < 4; i++)
                {
                    float x = s * (9.4f + i * 11.6f);
                    m.Pier(new Vector2(x, WallZ - 0.5f), 1.5f, top + (i % 2 == 0 ? 0.35f : 0.1f), i % 2 == 0 ? 2 : 0, i % 4 == 0 ? Undergrowth.Azalea : Undergrowth.Lavender, lower);
                }
            }
        }

        // ---------------------------------------------------------------- Streets

        private static WorldRoad? Lay(WorldData world, Vector2 c, RoadClass kind, float? startHeight, float? endHeight, params Vector2[] corners)
        {
            WorldRoad? path = RoadStage.BuildRoad(world, corners.Select(corner => c + corner).ToList(), kind, startHeight, endHeight);
            if (path == null) return null;
            world.Roads.Add(path);
            SettlementStage.ReserveRoad(world, path);
            return path;
        }

        /// <summary>Where the two streets out of the square's sides leave it: the market's, to the Forge, and the lane to the Gaming House.</summary>
        public const float MarketZ = 4.5f;
        public const float BarnLaneZ = 7f;

        // The paved streets: the market's west out of the square past the Forge, the lane east past the Gaming House, the lane south from the arch, and the Row along the top
        // of the wall. Then the ways down off the rise at either end of the Row, and along the foot of the wall to them.
        private static void LayStreets(SiteBuilder b, float upper)
        {
            WorldData world = b.World;
            Vector2 c = b.Centre;
            float edge = HavenSquare.SquareRadius - 0.6f;
            float fromArch = -HavenSquare.ArchAt + 2.6f;
            Lay(world, c, RoadClass.Highway, upper, upper, new Vector2(-edge, MarketZ), new Vector2(-26f, MarketZ + 0.1f), new Vector2(-50f, MarketZ), new Vector2(-70f, MarketZ));
            Lay(world, c, RoadClass.Highway, upper, upper, new Vector2(edge, BarnLaneZ), new Vector2(26f, BarnLaneZ + 0.3f), new Vector2(50f, BarnLaneZ), new Vector2(70f, BarnLaneZ));
            Lay(world, c, RoadClass.Highway, upper, upper, new Vector2(0f, -fromArch), new Vector2(0f, -32f), new Vector2(0f, RowZ + StreetHalf));
            // The Row, which at its west end turns down off the rise as a cart's way to the harbour, paved all the way; at its east end a footpath goes down.
            Lay(world, c, RoadClass.Highway, upper, null, new Vector2(62f, RowZ), new Vector2(30f, RowZ), new Vector2(-30f, RowZ), new Vector2(-70f, RowZ), new Vector2(-80.5f, -48.5f), new Vector2(-80f, -57f), new Vector2(-70f, -63f), new Vector2(-62f, -64f));
            Lay(world, c, RoadClass.Footpath, upper, null, new Vector2(62f, RowZ), new Vector2(66.5f, -50f), new Vector2(60.5f, -58f), HavenTown.HarbourWay);
            // And along the wall's foot to both.
            Lay(world, c, RoadClass.Footpath, null, null, new Vector2(-42f, WallZ - 3.4f), new Vector2(-52f, -60f), new Vector2(-62f, -64f));
            Lay(world, c, RoadClass.Footpath, null, null, new Vector2(44f, WallZ - 3.4f), new Vector2(48f, -60.5f), HavenTown.HarbourWay);
            // And the back lanes, of trodden earth, which make a round of each half of the town, so that no street ends at nothing: from the end of the market's street
            // down behind the Archive to the Row, and from the end of the barn's lane down past the joiner's yard to the Row's other end.
            Lay(world, c, RoadClass.Footpath, upper, upper, new Vector2(-70f, MarketZ), new Vector2(-76.5f, -3f), new Vector2(-77.5f, -20f), new Vector2(-75.5f, -35f), new Vector2(-70f, RowZ));
            Lay(world, c, RoadClass.Footpath, upper, upper, new Vector2(70f, BarnLaneZ), new Vector2(69f, -8f), new Vector2(66.5f, -28f), new Vector2(62f, RowZ));
        }

        // ---------------------------------------------------------------- What a hero comes for

        // The great waygate, at the head of everything on the square's terrace, facing down over the square, with a lantern either side. The flight up to it carries its own.
        private static void PlaceGate(SiteBuilder b, Vector2 c)
        {
            Vector2 at = c + North * GateAhead;
            if (b.PlaceNear(StructureCatalog.GreatWaygate, at, YawFacing(-North), ReachForEssentials, out Vector2 gate, padding: 0.4f))
            {
                b.Spawn(SpawnRole.Waygate, gate - North * (StructureCatalog.GreatWaygate.HalfExtents.Y + 3f));
            }
            foreach (float side in new[] { -1f, 1f })
            {
                b.Place(StructureCatalog.StoneLantern, gate + East * (side * (StructureCatalog.GreatWaygate.HalfExtents.X + 1.9f)) - North * 1.6f, 0f, padding: 0.2f);
            }
        }

        // The map table at the top of the square, between the square and the gate, where the way to every land is chosen.
        private static void PlaceTable(SiteBuilder b, Vector2 c)
        {
            if (!b.PlaceNear(StructureCatalog.MapTable, c + North * HavenSquare.TableAhead, YawFacing(-North), ReachForEssentials, out Vector2 table, padding: 0.6f)) return;
            b.Spawn(SpawnRole.MapTable, table - North * (StructureCatalog.MapTable.HalfExtents.Y + 1.8f));
        }

        // The strongbox beside the loggia, facing in across the square.
        private static void PlaceStash(SiteBuilder b, Vector2 c)
        {
            Vector2 boxAt = c + East * StashAside + North * StashNorth;
            if (b.PlaceNear(StructureCatalog.Strongbox, boxAt, YawFacing(-East), ReachForEssentials, out Vector2 box, padding: 0.6f))
            {
                b.Spawn(SpawnRole.Stash, box - East * (StructureCatalog.Strongbox.HalfExtents.Y + 1.6f));
                b.Place(StructureCatalog.Lantern, box + North * 3f, 0f, padding: 0.2f);
            }
        }

        // The plots kept for halls to come: the ground's kept clear, and timber and stone are stacked on each, waiting.
        private static void KeepPlots(SiteBuilder b, Vector2 c)
        {
            foreach (Vector2 plot in Plots)
            {
                Footprint ground = Footprint.Box(c + plot, PlotHalf, FacingCamera);
                if (!b.Fits(ground, 0.3f, 1.5f)) continue;
                b.World.Placement.Add(ground, PlacementLayer.Keepout);
                b.World.Clearings.Add(new Clearing(c + plot, PlotHalf.Length, 10f));
                // What's stacked there, out of the middle, where a hall will stand.
                b.World.Pending.Add(new PendingObject(StructureCatalog.LogStack.Id, c + plot + new Vector2(-PlotHalf.X + 5f, -PlotHalf.Y + 3.5f), 0.1f, Vector3.One, null));
                b.World.Pending.Add(new PendingObject(StructureCatalog.Rubble.Id, c + plot + new Vector2(PlotHalf.X - 4f, -PlotHalf.Y + 3.5f), 1.3f, Vector3.One * 0.8f, null));
            }
        }

        // ---------------------------------------------------------------- The tiltyard

        // A fenced yard below the wall, on raked sand, with three groups to try a build on: one alone, a handful, and a crowd. Whoever's on the wall's walk looks down into it.
        private static void PlaceTiltyard(SiteBuilder b, TownMason m, Vector2 at, float lower)
        {
            Vector2 centre = b.Centre + at;
            Footprint yard = Footprint.Box(centre, GroundHalf, 0f);
            b.Paint(GroundPaint.Sand, Footprint.Box(centre, GroundHalf + new Vector2(0.4f), 0f), 1.2f);
            b.World.Placement.Add(yard, PlacementLayer.Keepout);
            FenceYard(b, centre);

            // Three pens along the yard, one alone, a handful and a crowd, far enough apart that whatever's aimed at one meets only that one, with low fences between them.
            b.Spawn(SpawnRole.Dummy, centre + East * SingleAlong);

            Vector2 small = centre + East * SmallAlong - North * 1f;
            for (int i = 0; i < SmallGroup; i++)
            {
                float angle = i * MathF.Tau / SmallGroup + 0.3f;
                b.Spawn(SpawnRole.Dummy, small + Direction(angle) * DummyGap);
            }

            foreach (float between in new[] { (SingleAlong + SmallAlong + DummyGap) * 0.5f, (SmallAlong - DummyGap + BigAlong + DummyGap * 2.7f) * 0.5f })
            {
                b.Wall(FenceUse.Yard, centre + East * between - North * (GroundHalf.Y - 0.5f), centre + East * between + North * PenReach);
            }

            Vector2 big = centre + East * BigAlong - North * 1f;
            for (int i = 0; i < BigGroup; i++)
            {
                int row = i / SmallGroup;
                int column = i % SmallGroup;
                Vector2 offset = new Vector2((column - 2f) * DummyGap * 1.2f + (row % 2) * DummyGap * 0.6f, (row - 1f) * DummyGap * 1.25f);
                b.Spawn(SpawnRole.Dummy, big + offset);
            }
        }

        // A fence round the yard, open in the middle of its north side, at the foot of the Harbour Steps.
        private static void FenceYard(SiteBuilder b, Vector2 centre)
        {
            Vector2 half = GroundHalf + new Vector2(0.8f);
            float gate = b.Centre.X;        // In line with the steps.
            Vector2[] corners = { centre + new Vector2(-half.X, -half.Y), centre + new Vector2(half.X, -half.Y), centre + new Vector2(half.X, half.Y), centre + new Vector2(-half.X, half.Y) };
            for (int side = 0; side < 4; side++)
            {
                Vector2 from = corners[side];
                Vector2 to = corners[(side + 1) % 4];
                float length = (to - from).Length;
                int pieces = Math.Max(1, (int)MathF.Ceiling(length / FencePiece));
                for (int i = 0; i < pieces; i++)
                {
                    Vector2 start = Vector2.Lerp(from, to, i / (float)pieces);
                    Vector2 end = Vector2.Lerp(from, to, (i + 1f) / pieces);
                    // The north side is the third, from the north-east corner to the north-west.
                    if (side == 2 && MathF.Max(start.X, end.X) > gate - YardGate && MathF.Min(start.X, end.X) < gate + YardGate)
                    {
                        if (start.X > gate + YardGate) b.Wall(FenceUse.Yard, start, new Vector2(gate + YardGate, start.Y), centre);
                        if (end.X < gate - YardGate) b.Wall(FenceUse.Yard, new Vector2(gate - YardGate, end.Y), end, centre);
                        continue;
                    }
                    b.Wall(FenceUse.Yard, start, end, centre);
                }
            }
        }
    }
}
